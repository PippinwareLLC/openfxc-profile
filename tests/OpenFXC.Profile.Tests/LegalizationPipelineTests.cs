using OpenFXC.Ir;
using OpenFXC.Profile;

namespace OpenFXC.Profile.Tests;

public class LegalizationPipelineTests
{
    [Fact]
    public void Legal_Module_RemainsValid()
    {
        var module = TestModuleFactory.MakeLegalModule("ps_3_0");
        var pipeline = new LegalizationPipeline();

        var result = pipeline.Legalize(new LegalizeRequest(module));

        Assert.False(result.Invalid);
        Assert.DoesNotContain(result.Diagnostics, d => d.Severity == "Error");
    }

    [Fact]
    public void GradientOps_AreRejected_InSm2()
    {
        var module = TestModuleFactory.MakeSimpleModule(
            profile: "ps_2_0",
            stage: "ps",
            body: new[]
            {
                new IrInstruction { Op = "ddx", Operands = new[] { 2 }, Result = 1, Type = "float" },
                new IrInstruction { Op = "Return", Operands = new[] { 1 }, Terminator = true }
            },
            values: new[]
            {
                new IrValue { Id = 1, Type = "float", Kind = "Temp" },
                new IrValue { Id = 2, Type = "float", Kind = "Temp" }
            });

        var pipeline = new LegalizationPipeline();
        var result = pipeline.Legalize(new LegalizeRequest(module));

        Assert.Contains(result.Diagnostics, d => d.Severity == "Error" && d.Message.Contains("Gradient operations", StringComparison.OrdinalIgnoreCase));
        Assert.True(result.Invalid);
    }

    [Fact]
    public void DynamicBranching_IsRejected_InSm2()
    {
        var module = TestModuleFactory.MakeBranchModule("ps_2_0");

        var pipeline = new LegalizationPipeline();
        var result = pipeline.Legalize(new LegalizeRequest(module));

        Assert.Contains(result.Diagnostics, d => d.Severity == "Error" && d.Message.Contains("Dynamic branching", StringComparison.OrdinalIgnoreCase));
        Assert.True(result.Invalid);
    }

    [Fact]
    public void Recursion_IsRejected()
    {
        var module = TestModuleFactory.MakeRecursiveModule("ps_3_0");

        var pipeline = new LegalizationPipeline();
        var result = pipeline.Legalize(new LegalizeRequest(module));

        Assert.Contains(result.Diagnostics, d => d.Severity == "Error" && d.Message.Contains("Recursion", StringComparison.OrdinalIgnoreCase));
        Assert.True(result.Invalid);
    }
}

internal static partial class TestModuleFactory
{
    public static IrModule MakeLegalModule(string profile)
    {
        var value1 = new IrValue { Id = 1, Type = "float", Kind = "Temp" };
        var value2 = new IrValue { Id = 2, Type = "float", Kind = "Temp" };

        var block = new IrBlock
        {
            Id = "entry",
            Instructions = new[]
            {
                new IrInstruction { Op = "Assign", Operands = new[] { value1.Id }, Result = value2.Id, Type = "float" },
                new IrInstruction { Op = "Return", Operands = new[] { value2.Id }, Terminator = true }
            }
        };

        var function = new IrFunction
        {
            Name = "main",
            ReturnType = "float",
            Parameters = Array.Empty<int>(),
            Blocks = new[] { block }
        };

        return new IrModule
        {
            Profile = profile,
            EntryPoint = new IrEntryPoint { Function = "main", Stage = "ps" },
            Functions = new[] { function },
            Values = new[] { value1, value2 },
            Resources = Array.Empty<IrResource>(),
            Diagnostics = Array.Empty<IrDiagnostic>()
        };
    }

    public static IrModule MakeSimpleModule(string profile, string stage, IReadOnlyList<IrInstruction> body, IReadOnlyList<IrValue> values)
    {
        var entryBlock = new IrBlock { Id = "entry", Instructions = body };
        var function = new IrFunction
        {
            Name = "main",
            ReturnType = "float",
            Parameters = Array.Empty<int>(),
            Blocks = new[] { entryBlock }
        };

        return new IrModule
        {
            Profile = profile,
            EntryPoint = new IrEntryPoint { Function = "main", Stage = stage },
            Functions = new[] { function },
            Values = values,
            Resources = Array.Empty<IrResource>(),
            Diagnostics = Array.Empty<IrDiagnostic>()
        };
    }

    public static IrModule MakeBranchModule(string profile)
    {
        var condValue = new IrValue { Id = 1, Type = "bool", Kind = "Temp" };
        var retValue = new IrValue { Id = 2, Type = "float", Kind = "Temp" };

        var entry = new IrBlock
        {
            Id = "entry",
            Instructions = new[]
            {
                new IrInstruction { Op = "BranchCond", Operands = new[] { condValue.Id }, Terminator = true, Tag = "then;else" }
            }
        };

        var thenBlock = new IrBlock
        {
            Id = "then",
            Instructions = new[]
            {
                new IrInstruction { Op = "Return", Operands = new[] { retValue.Id }, Terminator = true }
            }
        };

        var elseBlock = new IrBlock
        {
            Id = "else",
            Instructions = new[]
            {
                new IrInstruction { Op = "Return", Operands = new[] { retValue.Id }, Terminator = true }
            }
        };

        var function = new IrFunction
        {
            Name = "main",
            ReturnType = "float",
            Parameters = Array.Empty<int>(),
            Blocks = new[] { entry, thenBlock, elseBlock }
        };

        return new IrModule
        {
            Profile = profile,
            EntryPoint = new IrEntryPoint { Function = "main", Stage = "ps" },
            Functions = new[] { function },
            Values = new[] { condValue, retValue },
            Resources = Array.Empty<IrResource>(),
            Diagnostics = Array.Empty<IrDiagnostic>()
        };
    }

    public static IrModule MakeRecursiveModule(string profile)
    {
        var retValue = new IrValue { Id = 1, Type = "float", Kind = "Temp" };

        var entryBlock = new IrBlock
        {
            Id = "entry",
            Instructions = new[]
            {
                new IrInstruction { Op = "Call", Tag = "main" },
                new IrInstruction { Op = "Return", Operands = new[] { retValue.Id }, Terminator = true }
            }
        };

        var function = new IrFunction
        {
            Name = "main",
            ReturnType = "float",
            Parameters = Array.Empty<int>(),
            Blocks = new[] { entryBlock }
        };

        return new IrModule
        {
            Profile = profile,
            EntryPoint = new IrEntryPoint { Function = "main", Stage = "ps" },
            Functions = new[] { function },
            Values = new[] { retValue },
            Resources = Array.Empty<IrResource>(),
            Diagnostics = Array.Empty<IrDiagnostic>()
        };
    }
}
