using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using OpenFXC.Hlsl;
using OpenFXC.Ir;
using OpenFXC.Profile;
using OpenFXC.Sem;

namespace OpenFXC.Profile.Tests;

/// <summary>
/// Optional long-running sweep over all sample `.hlsl`/`.fx` files in `samples/`.
/// Enable via RUN_SAMPLE_CORPUS=1 to exercise parse->sem->lower->optimize->profile legalize.
/// </summary>
public class ProfileCorpusSweepTests
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        MaxDepth = 256
    };

    public static IEnumerable<object[]> CorpusFiles
    {
        get
        {
            if (!Enabled)
            {
                return new[] { new object[] { "SKIP" } };
            }

            var files = EnumerateSampleFiles().ToList();
            if (files.Count == 0)
            {
                return new[] { new object[] { "SKIP_NO_FILES" } };
            }

            return files.Select(f => new object[] { f });
        }
    }

    [Theory]
    [MemberData(nameof(CorpusFiles))]
    public void Sweep_Sample_File(string file)
    {
        if (!Enabled) return;
        if (file == "SKIP" || file == "SKIP_NO_FILES") return;

        var failures = SweepFile(file);
        Assert.True(failures.Count == 0, $"{file} failed: {string.Join(" | ", failures)}");
    }

    private static List<string> SweepFile(string file)
    {
        var failures = new List<string>();
        var hlsl = File.ReadAllText(file);
        var (tokens, lexDiagnostics) = HlslLexer.Lex(hlsl);
        var (root, parseDiagnostics) = Parser.Parse(tokens, hlsl.Length);

        var parseResult = new ParseResult(
            FormatVersion: 1,
            Source: new SourceInfo(Path.GetFileName(file), hlsl.Length),
            Root: root,
            Tokens: tokens,
            Diagnostics: lexDiagnostics.Concat(parseDiagnostics).ToArray());

        var astJson = JsonSerializer.Serialize(parseResult, SerializerOptions);

        // Discover entries from techniques if present.
        var initialSem = new SemanticAnalyzer("ps_4_0", "main", astJson).Analyze();
        var candidates = GatherEntries(initialSem);
        if (candidates.Count == 0)
        {
            candidates.Add(new EntryCandidate("vs_4_0", "main"));
        }

        foreach (var candidate in candidates)
        {
            var candidateTimer = Stopwatch.StartNew();
            try
            {
                var semantic = new SemanticAnalyzer(candidate.Profile, candidate.Entry, astJson).Analyze();
                var semanticJson = JsonSerializer.Serialize(semantic, SerializerOptions);

                var semanticErrors = semantic.Diagnostics.Where(d => IsError(d.Severity)).ToList();
                if (semanticErrors.Count > 0)
                {
                    failures.Add($"{file} [{candidate.Profile}:{candidate.Entry}] semantic diagnostics: {string.Join("; ", semanticErrors.Select(e => e.Message))}");
                    Console.WriteLine($"[PROFILE][FAIL-SEM] {file} [{candidate.Profile}:{candidate.Entry}] {candidateTimer.ElapsedMilliseconds} ms");
                    continue;
                }

                var lowered = new LoweringPipeline().Lower(new LoweringRequest(semanticJson, candidate.Profile, candidate.Entry));
                var loweredInvariantErrors = IrInvariants.Validate(lowered).Where(IsError).ToList();
                if (loweredInvariantErrors.Count > 0)
                {
                    failures.Add($"{file} [{candidate.Profile}:{candidate.Entry}] lowering invariants: {string.Join("; ", loweredInvariantErrors.Select(e => e.Message))}");
                    Console.WriteLine($"[PROFILE][FAIL] {file} [{candidate.Profile}:{candidate.Entry}] {candidateTimer.ElapsedMilliseconds} ms");
                    continue;
                }

                var loweredJson = JsonSerializer.Serialize(lowered, SerializerOptions);
                var optimized = new OptimizePipeline().Optimize(new OptimizeRequest(loweredJson, "constfold,algebraic,dce,component-dce,copyprop", candidate.Profile));
                var optInvariantErrors = IrInvariants.Validate(optimized).Where(IsError).ToList();
                if (optInvariantErrors.Count > 0)
                {
                    failures.Add($"{file} [{candidate.Profile}:{candidate.Entry}] optimized invariants: {string.Join("; ", optInvariantErrors.Select(e => e.Message))}");
                    Console.WriteLine($"[PROFILE][FAIL] {file} [{candidate.Profile}:{candidate.Entry}] {candidateTimer.ElapsedMilliseconds} ms");
                    continue;
                }

                var profileResult = new LegalizationPipeline().Legalize(new LegalizeRequest(optimized, candidate.Profile));
                var profileInvariantErrors = IrInvariants.Validate(profileResult.Module).Where(IsError).ToList();
                var profileErrors = profileResult.Diagnostics.Where(d => IsError(d) && (string.Equals(d.Stage, "legalize", StringComparison.OrdinalIgnoreCase) || string.Equals(d.Stage, "invariant", StringComparison.OrdinalIgnoreCase))).ToList();

                if (profileInvariantErrors.Count > 0)
                {
                    failures.Add($"{file} [{candidate.Profile}:{candidate.Entry}] profile invariants: {string.Join("; ", profileInvariantErrors.Select(e => e.Message))}");
                    Console.WriteLine($"[PROFILE][FAIL] {file} [{candidate.Profile}:{candidate.Entry}] {candidateTimer.ElapsedMilliseconds} ms");
                    continue;
                }

                if (profileErrors.Count > 0)
                {
                    failures.Add($"{file} [{candidate.Profile}:{candidate.Entry}] profile diagnostics: {string.Join("; ", profileErrors.Select(e => e.Message))}");
                    Console.WriteLine($"[PROFILE][FAIL] {file} [{candidate.Profile}:{candidate.Entry}] {candidateTimer.ElapsedMilliseconds} ms");
                    continue;
                }

                Console.WriteLine($"[PROFILE][PASS] {file} [{candidate.Profile}:{candidate.Entry}] {candidateTimer.ElapsedMilliseconds} ms");
            }
            catch (Exception ex)
            {
                failures.Add($"{file} [{candidate.Profile}:{candidate.Entry}] error: {ex.Message}");
                Console.WriteLine($"[PROFILE][ERROR] {file} [{candidate.Profile}:{candidate.Entry}] {candidateTimer.ElapsedMilliseconds} ms :: {ex.Message}");
            }
        }

        return failures;
    }

    private static List<EntryCandidate> GatherEntries(SemanticOutput semantic)
    {
        var list = new List<EntryCandidate>();
        if (semantic.Techniques is not null)
        {
            foreach (var tech in semantic.Techniques)
            {
                foreach (var pass in tech.Passes ?? Array.Empty<FxPassInfo>())
                {
                    foreach (var shader in pass.Shaders ?? Array.Empty<FxShaderBinding>())
                    {
                        if (string.IsNullOrWhiteSpace(shader.Entry)) continue;
                        var profile = shader.Profile ?? ProfileFromStage(shader.Stage);
                        list.Add(new EntryCandidate(profile, shader.Entry));
                    }
                }
            }
        }

        return list
            .GroupBy(e => $"{e.Profile}:{e.Entry}", StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();
    }

    private static string ProfileFromStage(string? stage)
    {
        return stage?.ToLowerInvariant() switch
        {
            "vertex" => "vs_4_0",
            "pixel" => "ps_4_0",
            "geometry" => "gs_4_0",
            "hull" => "hs_5_0",
            "domain" => "ds_5_0",
            "compute" => "cs_5_0",
            _ => "ps_4_0"
        };
    }

    private static IEnumerable<string> EnumerateSampleFiles()
    {
        var repoRoot = GetRepoRoot();
        var sampleRoot = Path.Combine(repoRoot, "samples");
        if (!Directory.Exists(sampleRoot))
        {
            return Array.Empty<string>();
        }

        return Directory.EnumerateFiles(sampleRoot, "*.*", SearchOption.AllDirectories)
            .Where(f => f.EndsWith(".fx", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".hlsl", StringComparison.OrdinalIgnoreCase));
    }

    private static bool Enabled => string.Equals(Environment.GetEnvironmentVariable("RUN_SAMPLE_CORPUS"), "1", StringComparison.Ordinal);

    private static string GetRepoRoot() => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

    private static bool IsError(IrDiagnostic d) => IsError(d.Severity);
    private static bool IsError(string? severity) => string.Equals(severity, "Error", StringComparison.OrdinalIgnoreCase);

    private sealed record EntryCandidate(string Profile, string Entry);
}
