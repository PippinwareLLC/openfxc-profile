using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenFXC.Profile;

internal static class Program
{
    private const int InternalErrorExitCode = 1;
    private const int SuccessExitCode = 0;

    public static int Main(string[] args)
    {
        try
        {
            return Run(args);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return InternalErrorExitCode;
        }
    }

    private static int Run(string[] args)
    {
        if (args.Length == 0)
        {
            PrintUsage();
            return InternalErrorExitCode;
        }

        var verb = args[0];
        var rest = args[1..];

        return verb.ToLowerInvariant() switch
        {
            "legalize" => RunLegalize(rest),
            _ => FailWithUsage()
        };
    }

    private static int RunLegalize(string[] args)
    {
        var options = ParseLegalizeOptions(args);
        if (!options.IsValid(out var error))
        {
            Console.Error.WriteLine(error);
            PrintUsage();
            return InternalErrorExitCode;
        }

        var json = ReadAllInput(options.InputPath);
        var pipeline = new LegalizationPipeline();
        var module = pipeline.ParseModule(json);
        var request = new LegalizeRequest(module, options.Profile);
        var result = pipeline.Legalize(request);

        return WriteModuleAndExit(result.Module);
    }

    private static int WriteModuleAndExit(Ir.IrModule module)
    {
        var writerOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        Console.Out.Write(JsonSerializer.Serialize(module, writerOptions));
        Console.Out.WriteLine();
        return SuccessExitCode;
    }

    private static string ReadAllInput(string? inputPath)
    {
        if (string.IsNullOrWhiteSpace(inputPath))
        {
            return Console.In.ReadToEnd();
        }

        return File.ReadAllText(inputPath);
    }

    private static LegalizeOptions ParseLegalizeOptions(string[] args)
    {
        string? profile = null;
        string? input = null;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            switch (arg)
            {
                case "--profile":
                case "-p":
                    profile = NextValue(args, ref i);
                    break;
                case "--input":
                case "-i":
                    input = NextValue(args, ref i);
                    break;
                default:
                    break;
            }
        }

        return new LegalizeOptions(profile, input);
    }

    private static string? NextValue(string[] args, ref int index)
    {
        if (index + 1 >= args.Length)
        {
            return null;
        }

        index++;
        return args[index];
    }

    private static int FailWithUsage()
    {
        PrintUsage();
        return InternalErrorExitCode;
    }

    private static void PrintUsage()
    {
        Console.Error.WriteLine("Usage: openfxc-profile legalize [--profile <name>] [--input <path>] < input.ir.json > output.ir.legal.json");
    }

    private sealed record LegalizeOptions(string? Profile, string? InputPath)
    {
        public bool IsValid(out string? error)
        {
            if (!string.IsNullOrWhiteSpace(InputPath) && !File.Exists(InputPath))
            {
                error = $"Input file not found: {InputPath}";
                return false;
            }

            error = null;
            return true;
        }
    }
}
