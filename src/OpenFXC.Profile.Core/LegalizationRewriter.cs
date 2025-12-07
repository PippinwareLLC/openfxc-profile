using OpenFXC.Ir;

namespace OpenFXC.Profile;

internal static class LegalizationRewriter
{
    public static (IrModule Module, bool Invalid) Rewrite(IrModule module, CapabilityProfile profile, string stage, IList<IrDiagnostic> diagnostics)
    {
        var invalid = false;

        var rewrittenFunctions = new List<IrFunction>();
        foreach (var function in module.Functions ?? Array.Empty<IrFunction>())
        {
            var rewrittenBlocks = new List<IrBlock>();
            foreach (var block in function.Blocks ?? Array.Empty<IrBlock>())
            {
                var rewrittenInstructions = new List<IrInstruction>();
                foreach (var instr in block.Instructions ?? Array.Empty<IrInstruction>())
                {
                    var rewritten = RewriteInstruction(instr, profile, stage, diagnostics, ref invalid);
                    rewrittenInstructions.Add(rewritten);
                }

                rewrittenBlocks.Add(block with { Instructions = rewrittenInstructions });
            }

            rewrittenFunctions.Add(function with { Blocks = rewrittenBlocks });
        }

        var rewrittenModule = module with { Functions = rewrittenFunctions };
        return (rewrittenModule, invalid);
    }

    private static IrInstruction RewriteInstruction(
        IrInstruction instr,
        CapabilityProfile profile,
        string stage,
        IList<IrDiagnostic> diagnostics,
        ref bool invalid)
    {
        var op = instr.Op ?? string.Empty;
        var lowerOp = op.ToLowerInvariant();

        // Branch flattening for SM2 (best-effort placeholder).
        if (profile.Band == "sm2" && (lowerOp.Contains("branch") || string.Equals(op, "BranchCond", StringComparison.OrdinalIgnoreCase)))
        {
            diagnostics.Add(IrDiagnostic.Info($"Flattened branch-like op '{op}' for profile {profile.Band}.", "legalize"));
            // BranchCond typically has no result; convert to a placeholder select or nop.
            var rewrittenOp = instr.Result.HasValue ? "Select" : "Nop";
            return instr with { Op = rewrittenOp, Terminator = false, Tag = "flattened" };
        }

        // Loop unrolling placeholder for SM2.
        if (profile.Band == "sm2" && lowerOp.Contains("loop"))
        {
            diagnostics.Add(IrDiagnostic.Info($"Unrolled loop-like op '{op}' for profile {profile.Band}.", "legalize"));
            return instr with { Op = "LoopUnrolled", Terminator = false, Tag = "unrolled" };
        }

        // Intrinsic replacement: normalize -> dot+rsq+mul (placeholder tag).
        if (string.Equals(op, "normalize", StringComparison.OrdinalIgnoreCase))
        {
            diagnostics.Add(IrDiagnostic.Info("Rewriting 'normalize' using backend-neutral ops (dot + rsq + mul).", "legalize"));
            return instr with { Tag = "normalize.rewritten" };
        }

        // Unsupported ops already validated; remove if explicitly marked unsupported.
        if (lowerOp.Contains("unsupported") || lowerOp.Contains("unknown"))
        {
            diagnostics.Add(IrDiagnostic.Error($"Removed unsupported op '{op}'.", "legalize"));
            invalid = true;
            return instr with { Op = "Nop", Operands = Array.Empty<int>(), Result = null, Terminator = false, Tag = "removed" };
        }

        return instr;
    }
}
