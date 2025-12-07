namespace OpenFXC.Profile;

public sealed record CapabilityProfile(
    string Band,
    bool DynamicBranching,
    bool Loops,
    bool Predication,
    int? TextureInstructionLimit,
    bool GradientOps,
    bool VertexTextureFetch,
    int? TempRegisters,
    int? InstructionSlots,
    int? MrtLimit,
    bool SvSemantics,
    string SamplerTypes,
    bool TypedUavs);

public static class CapabilityTable
{
    private static readonly IReadOnlyDictionary<string, CapabilityProfile> Profiles =
        new Dictionary<string, CapabilityProfile>(StringComparer.OrdinalIgnoreCase)
        {
            ["sm2"] = new(
                Band: "sm2",
                DynamicBranching: false,
                Loops: false,
                Predication: false,
                TextureInstructionLimit: 32,
                GradientOps: false,
                VertexTextureFetch: false,
                TempRegisters: 12,
                InstructionSlots: 64,
                MrtLimit: 0,
                SvSemantics: false,
                SamplerTypes: "legacy-only",
                TypedUavs: false),
            ["sm3"] = new(
                Band: "sm3",
                DynamicBranching: true,
                Loops: true,
                Predication: true,
                TextureInstructionLimit: null,
                GradientOps: true, // pixel only, enforced elsewhere
                VertexTextureFetch: true,
                TempRegisters: 32,
                InstructionSlots: 512,
                MrtLimit: 4, // typical MRT count
                SvSemantics: false,
                SamplerTypes: "legacy-only",
                TypedUavs: false),
            ["sm4"] = new(
                Band: "sm4",
                DynamicBranching: true,
                Loops: true,
                Predication: true,
                TextureInstructionLimit: null,
                GradientOps: true,
                VertexTextureFetch: true,
                TempRegisters: null,
                InstructionSlots: null,
                MrtLimit: 8,
                SvSemantics: true,
                SamplerTypes: "modern",
                TypedUavs: true),
            ["sm5"] = new(
                Band: "sm5",
                DynamicBranching: true,
                Loops: true,
                Predication: true,
                TextureInstructionLimit: null,
                GradientOps: true,
                VertexTextureFetch: true,
                TempRegisters: null,
                InstructionSlots: null,
                MrtLimit: 8,
                SvSemantics: true,
                SamplerTypes: "modern",
                TypedUavs: true)
        };

    /// <summary>
    /// Normalize a profile string (e.g., vs_2_0, ps_3_0, sm5) to a capability band key (sm2, sm3, sm4, sm5).
    /// </summary>
    public static string? NormalizeProfile(string? profile)
    {
        if (string.IsNullOrWhiteSpace(profile))
        {
            return null;
        }

        var p = profile.Trim().ToLowerInvariant();

        if (p.StartsWith("sm2") || p.Contains("_2_"))
        {
            return "sm2";
        }

        if (p.StartsWith("sm3") || p.Contains("_3_"))
        {
            return "sm3";
        }

        if (p.StartsWith("sm4") || p.Contains("_4_"))
        {
            return "sm4";
        }

        if (p.StartsWith("sm5") || p.Contains("_5_"))
        {
            return "sm5";
        }

        return null;
    }

    public static bool TryGetProfile(string? normalizedProfile, out CapabilityProfile? profile)
    {
        if (string.IsNullOrWhiteSpace(normalizedProfile))
        {
            profile = null;
            return false;
        }

        return Profiles.TryGetValue(normalizedProfile, out profile);
    }
}
