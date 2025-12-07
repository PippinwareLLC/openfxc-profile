using System.Globalization;
using OpenFXC.Ir;

namespace OpenFXC.Profile;

internal static class LegalizationValidator
{
    public static void Validate(IrModule module, CapabilityProfile profile, IList<IrDiagnostic> diagnostics)
    {
        var stage = module.EntryPoint?.Stage ?? "unknown";

        ValidateInstructionAndTemps(module, profile, diagnostics);
        ValidateGradientOps(module, profile, diagnostics, stage);
        ValidateSvSemantics(module, profile, diagnostics);
        ValidateTextureSampling(module, profile, diagnostics, stage);
        ValidateUavsAndMrt(module, profile, diagnostics);
    }

    private static void ValidateInstructionAndTemps(IrModule module, CapabilityProfile profile, IList<IrDiagnostic> diagnostics)
    {
        var instructionCount = CountInstructions(module);
        if (profile.InstructionSlots is int maxInstructions && instructionCount > maxInstructions)
        {
            diagnostics.Add(IrDiagnostic.Error(
                $"Instruction count {instructionCount} exceeds profile limit {maxInstructions} for {profile.Band}.",
                "legalize"));
        }

        var tempCount = module.Values?.Count(v => string.Equals(v.Kind, "Temp", StringComparison.OrdinalIgnoreCase)) ?? 0;
        if (profile.TempRegisters is int maxTemps && tempCount > maxTemps)
        {
            diagnostics.Add(IrDiagnostic.Error(
                $"Temporary register count {tempCount} exceeds profile limit {maxTemps} for {profile.Band}.",
                "legalize"));
        }
    }

    private static void ValidateGradientOps(IrModule module, CapabilityProfile profile, IList<IrDiagnostic> diagnostics, string stage)
    {
        if (profile.GradientOps)
        {
            return;
        }

        var gradientOps = new[] { "ddx", "ddy", "dsx", "dsy" };
        var hasGradient = module.Functions?
            .SelectMany(f => f.Blocks ?? Array.Empty<IrBlock>())
            .SelectMany(b => b.Instructions ?? Array.Empty<IrInstruction>())
            .Any(instr => gradientOps.Any(op => string.Equals(instr.Op, op, StringComparison.OrdinalIgnoreCase))) ?? false;

        if (hasGradient)
        {
            diagnostics.Add(IrDiagnostic.Error(
                $"Gradient operations are not supported in profile {profile.Band} (stage: {stage}).",
                "legalize"));
        }
    }

    private static void ValidateSvSemantics(IrModule module, CapabilityProfile profile, IList<IrDiagnostic> diagnostics)
    {
        if (profile.SvSemantics)
        {
            return;
        }

        var hasSv = module.Values?.Any(v => v.Semantic?.StartsWith("SV_", true, CultureInfo.InvariantCulture) == true) ?? false;
        if (hasSv)
        {
            diagnostics.Add(IrDiagnostic.Error(
                $"SV semantics are not allowed in profile {profile.Band}.",
                "legalize"));
        }
    }

    private static void ValidateTextureSampling(IrModule module, CapabilityProfile profile, IList<IrDiagnostic> diagnostics, string stage)
    {
        var textureOps = CountTextureOps(module);
        if (profile.TextureInstructionLimit is int texLimit && textureOps > texLimit)
        {
            diagnostics.Add(IrDiagnostic.Error(
                $"Texture instruction count {textureOps} exceeds profile limit {texLimit} for {profile.Band}.",
                "legalize"));
        }

        var isVertexStage = stage.StartsWith("vs", StringComparison.OrdinalIgnoreCase) || string.Equals(stage, "vertex", StringComparison.OrdinalIgnoreCase);
        if (!profile.VertexTextureFetch && isVertexStage && textureOps > 0)
        {
            diagnostics.Add(IrDiagnostic.Error(
                $"Vertex texture fetch is not supported in profile {profile.Band}.",
                "legalize"));
        }
    }

    private static void ValidateUavsAndMrt(IrModule module, CapabilityProfile profile, IList<IrDiagnostic> diagnostics)
    {
        var resources = module.Resources ?? Array.Empty<IrResource>();

        if (!profile.TypedUavs)
        {
            var hasUav = resources.Any(r =>
                r.Writable ||
                string.Equals(r.Kind, "uav", StringComparison.OrdinalIgnoreCase) ||
                r.Type.Contains("uav", StringComparison.OrdinalIgnoreCase));

            if (hasUav)
            {
                diagnostics.Add(IrDiagnostic.Error(
                    $"Typed UAVs are not supported in profile {profile.Band}.",
                    "legalize"));
            }
        }

        if (profile.MrtLimit is int mrtLimit && mrtLimit == 0)
        {
            var hasMrt = resources.Any(r =>
                r.Kind.Contains("rendertarget", StringComparison.OrdinalIgnoreCase) ||
                r.Name.StartsWith("COLOR", StringComparison.OrdinalIgnoreCase));

            if (hasMrt)
            {
                diagnostics.Add(IrDiagnostic.Error(
                    $"Multiple render targets are not supported in profile {profile.Band}.",
                    "legalize"));
            }
        }
    }

    private static int CountInstructions(IrModule module)
    {
        return module.Functions?
                   .SelectMany(f => f.Blocks ?? Array.Empty<IrBlock>())
                   .SelectMany(b => b.Instructions ?? Array.Empty<IrInstruction>())
                   .Count() ?? 0;
    }

    private static int CountTextureOps(IrModule module)
    {
        return module.Functions?
                   .SelectMany(f => f.Blocks ?? Array.Empty<IrBlock>())
                   .SelectMany(b => b.Instructions ?? Array.Empty<IrInstruction>())
                   .Count(instr =>
                       instr.Op.Contains("tex", StringComparison.OrdinalIgnoreCase) ||
                       instr.Op.Contains("sample", StringComparison.OrdinalIgnoreCase)) ?? 0;
    }
}
