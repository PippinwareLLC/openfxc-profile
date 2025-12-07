using OpenFXC.Ir;
using OpenFXC.Profile;

namespace OpenFXC.Profile.Tests;

public class LegalizationRewriterTests
{
    [Fact]
    public void Branch_Flattens_For_Sm2()
    {
        var module = TestModuleFactory.MakeBranchModule("ps_2_0");
        var pipeline = new LegalizationPipeline();

        var result = pipeline.Legalize(new LegalizeRequest(module));
        var entryInstr = result.Module.Functions.First().Blocks.First().Instructions.First();

        Assert.True(result.Invalid);
        Assert.NotEqual("BranchCond", entryInstr.Op);
        Assert.Equal("flattened", entryInstr.Tag);
    }

    [Fact]
    public void Loop_Unrolls_For_Sm2()
    {
        var module = TestModuleFactory.MakeLoopModule("ps_2_0");
        var pipeline = new LegalizationPipeline();

        var result = pipeline.Legalize(new LegalizeRequest(module));
        var entryInstr = result.Module.Functions.First().Blocks.First().Instructions.First();

        Assert.Equal("LoopUnrolled", entryInstr.Op);
        Assert.True(entryInstr.Tag?.Contains("unrolled", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Normalize_IsTagged_AsRewritten()
    {
        var module = TestModuleFactory.MakeNormalizeModule("ps_3_0");
        var pipeline = new LegalizationPipeline();

        var result = pipeline.Legalize(new LegalizeRequest(module));
        var normalizeInstr = result.Module.Functions.First().Blocks.First().Instructions.First(i => i.Op.Equals("normalize", StringComparison.OrdinalIgnoreCase));

        Assert.Equal("normalize.rewritten", normalizeInstr.Tag);
        Assert.False(result.Invalid);
    }

    [Fact]
    public void Unsupported_Op_Removed_And_Invalid()
    {
        var module = TestModuleFactory.MakeUnsupportedOpModule("ps_3_0");
        var pipeline = new LegalizationPipeline();

        var result = pipeline.Legalize(new LegalizeRequest(module));
        var instr = result.Module.Functions.First().Blocks.First().Instructions.First();

        Assert.Equal("Nop", instr.Op);
        Assert.True(result.Invalid);
        Assert.Contains(result.Diagnostics, d => d.Severity == "Error" && d.Message.Contains("Removed unsupported op", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Snapshot_Json_RoundTrip()
    {
        var json = """
        {
          "formatVersion": 1,
          "profile": "ps_3_0",
          "entryPoint": { "function": "main", "stage": "ps" },
          "functions": [
            {
              "name": "main",
              "returnType": "float",
              "parameters": [],
              "blocks": [
                {
                  "id": "entry",
                  "instructions": [
                    { "op": "Assign", "operands": [1], "result": 2, "type": "float" },
                    { "op": "Return", "operands": [2], "terminator": true }
                  ]
                }
              ]
            }
          ],
          "values": [
            { "id": 1, "type": "float", "kind": "Temp" },
            { "id": 2, "type": "float", "kind": "Temp" }
          ],
          "resources": [],
          "diagnostics": []
        }
        """;

        var pipeline = new LegalizationPipeline();
        var module = pipeline.ParseModule(json);
        var result = pipeline.Legalize(new LegalizeRequest(module));

        Assert.False(result.Invalid);
        Assert.DoesNotContain(result.Diagnostics, d => d.Severity == "Error");
    }
}

internal static partial class TestModuleFactory
{
    public static IrModule MakeLoopModule(string profile)
    {
        var retValue = new IrValue { Id = 1, Type = "float", Kind = "Temp" };
        var block = new IrBlock
        {
            Id = "entry",
            Instructions = new[]
            {
                new IrInstruction { Op = "Loop", Tag = "loop", Terminator = false },
                new IrInstruction { Op = "Return", Operands = new[] { retValue.Id }, Terminator = true }
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
            Values = new[] { retValue },
            Resources = Array.Empty<IrResource>(),
            Diagnostics = Array.Empty<IrDiagnostic>()
        };
    }

    public static IrModule MakeNormalizeModule(string profile)
    {
        var valIn = new IrValue { Id = 1, Type = "float3", Kind = "Temp" };
        var valOut = new IrValue { Id = 2, Type = "float3", Kind = "Temp" };

        var block = new IrBlock
        {
            Id = "entry",
            Instructions = new[]
            {
                new IrInstruction { Op = "normalize", Operands = new[] { valIn.Id }, Result = valOut.Id, Type = "float3" },
                new IrInstruction { Op = "Return", Operands = new[] { valOut.Id }, Terminator = true }
            }
        };

        var function = new IrFunction
        {
            Name = "main",
            ReturnType = "float3",
            Parameters = Array.Empty<int>(),
            Blocks = new[] { block }
        };

        return new IrModule
        {
            Profile = profile,
            EntryPoint = new IrEntryPoint { Function = "main", Stage = "ps" },
            Functions = new[] { function },
            Values = new[] { valIn, valOut },
            Resources = Array.Empty<IrResource>(),
            Diagnostics = Array.Empty<IrDiagnostic>()
        };
    }

    public static IrModule MakeUnsupportedOpModule(string profile)
    {
        var retValue = new IrValue { Id = 1, Type = "float", Kind = "Temp" };
        var block = new IrBlock
        {
            Id = "entry",
            Instructions = new[]
            {
                new IrInstruction { Op = "UnsupportedOp", Operands = Array.Empty<int>(), Result = null, Terminator = false },
                new IrInstruction { Op = "Return", Operands = new[] { retValue.Id }, Terminator = true }
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
            Values = new[] { retValue },
            Resources = Array.Empty<IrResource>(),
            Diagnostics = Array.Empty<IrDiagnostic>()
        };
    }
}
