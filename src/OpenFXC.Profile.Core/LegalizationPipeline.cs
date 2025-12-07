using System.Text.Json;
using OpenFXC.Ir;

namespace OpenFXC.Profile;

public sealed class LegalizeRequest
{
    public LegalizeRequest(IrModule module, string? profileOverride)
    {
        Module = module ?? throw new ArgumentNullException(nameof(module));
        ProfileOverride = string.IsNullOrWhiteSpace(profileOverride) ? null : profileOverride;
    }

    public IrModule Module { get; }

    public string? ProfileOverride { get; }
}

public sealed class LegalizeResult
{
    public LegalizeResult(IrModule module, bool invalid = false)
    {
        Module = module ?? throw new ArgumentNullException(nameof(module));
        Invalid = invalid;
    }

    public IrModule Module { get; }

    public bool Invalid { get; }
}

public sealed class LegalizationPipeline
{
    private readonly JsonSerializerOptions _serializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public LegalizeResult Legalize(LegalizeRequest request)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        // TODO: implement real legalization logic.
        var module = ApplyProfileOverride(request.Module, request.ProfileOverride);
        return new LegalizeResult(module);
    }

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
