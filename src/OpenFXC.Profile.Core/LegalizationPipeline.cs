using System.Text.Json;
using OpenFXC.Ir;

namespace OpenFXC.Profile;

public sealed class LegalizeRequest
{
    public LegalizeRequest(IrModule module, string? profileOverride = null)
    {
        Module = module ?? throw new ArgumentNullException(nameof(module));
        ProfileOverride = string.IsNullOrWhiteSpace(profileOverride) ? null : profileOverride;
    }

    /// <summary>IR module produced by openfxc-ir optimize.</summary>
    public IrModule Module { get; }

    /// <summary>Optional profile override (e.g., ps_2_0, vs_3_0).</summary>
    public string? ProfileOverride { get; }
}

public sealed class LegalizeResult
{
    public LegalizeResult(IrModule module, IReadOnlyList<IrDiagnostic> diagnostics, bool invalid)
    {
        Module = module ?? throw new ArgumentNullException(nameof(module));
        Diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
        Invalid = invalid;
    }

    /// <summary>Profile-legal IR (may still carry diagnostics).</summary>
    public IrModule Module { get; }

    /// <summary>Diagnostics emitted by the legalizer (includes input diagnostics).</summary>
    public IReadOnlyList<IrDiagnostic> Diagnostics { get; }

    /// <summary>True when legalization failed and output is marked invalid.</summary>
    public bool Invalid { get; }
}

public sealed class LegalizationPipeline
{
    private readonly JsonSerializerOptions _serializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>Entry point for class-library callers.</summary>
    public LegalizeResult Legalize(LegalizeRequest request)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        // TODO: implement real legalization logic.
        var module = ApplyProfileOverride(request.Module, request.ProfileOverride);

        var diagnostics = module.Diagnostics?.ToList() ?? new List<IrDiagnostic>();

        var normalizedProfile = CapabilityTable.NormalizeProfile(module.Profile);
        if (normalizedProfile is null || !CapabilityTable.TryGetProfile(normalizedProfile, out var profile) || profile is null)
        {
            diagnostics.Add(IrDiagnostic.Error($"Unknown or missing profile '{module.Profile ?? "<none>"}' for legalization.", "legalize"));
        }
        else
        {
            LegalizationValidator.Validate(module, profile, diagnostics);
            var stage = module.EntryPoint?.Stage ?? "unknown";
            var rewriteResult = LegalizationRewriter.Rewrite(module, profile, stage, diagnostics);
            module = rewriteResult.Module;
            if (rewriteResult.Invalid)
            {
                diagnostics.Add(IrDiagnostic.Error("Unsupported operations removed during legalization.", "legalize"));
            }

            var invariantDiagnostics = IrInvariants.Validate(module);
            if (invariantDiagnostics.Count > 0)
            {
                diagnostics.AddRange(invariantDiagnostics);
            }
        }

        module = module with { Diagnostics = diagnostics };

        var invalid = diagnostics.Any(d => string.Equals(d.Severity, "Error", StringComparison.OrdinalIgnoreCase));

        return new LegalizeResult(module, diagnostics, invalid);
    }

    /// <summary>Convenience helper to parse IR JSON into a module using openfxc-ir types.</summary>
    public IrModule ParseModule(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new ArgumentException("Input IR JSON is empty.", nameof(json));
        }

        var module = JsonSerializer.Deserialize<IrModule>(json, _serializerOptions);
        if (module is null)
        {
            throw new InvalidDataException("Failed to parse IR JSON.");
        }

        return module;
    }

    private static IrModule ApplyProfileOverride(IrModule module, string? profileOverride)
    {
        if (string.IsNullOrWhiteSpace(profileOverride))
        {
            return module;
        }

        return module with { Profile = profileOverride };
    }
}
