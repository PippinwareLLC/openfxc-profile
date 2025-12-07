# TODO - openfxc-profile (legalize)

## M0: CLI skeleton
- [x] Scaffold `openfxc-profile legalize` command (stdin/file input, `--profile` override, exit codes 0/1).
- [x] Wire JSON parsing, diagnostics passthrough, and basic logging.

## M1: Class library API
- [x] Implement legalizer as a class library surface; keep CLI as a thin wrapper.
- [x] Define request/response types (profile override, diagnostics list, `invalid` flag).
- [x] Expose helper hooks to call submodule libraries (`openfxc-ir`, `openfxc-sem`, `openfxc-hlsl`) directly.

## M2: Capability table
- [x] Encode canonical capability table (SM2-SM5) as shared data.
- [x] Drive validation and rewrite decisions from that table; keep tests in sync.

## M3: Validation rules
- [x] Instruction slot and temp register limits per profile.
- [x] MRT allowance, vertex texture fetch rules, SV semantics restrictions.
- [x] Gradient op bans in SM2; unsupported intrinsic detection.
- [x] Resource dimension and UAV bans below allowed profiles.

## M4: Rewriting for legality
- [x] Branch flattening for `ps_2_0` (placeholder select flattening).
- [x] Simple loop unrolling (placeholder unrolled tag for SM2 loops).
- [x] Intrinsic replacements (placeholder normalize rewrite tag) using backend-neutral intent.
- [x] Split/remove unsupported ops while preserving IR invariants.

## M5: Rejection paths
- [x] Dynamic branching/loops in SM2 rejection; recursion rejection.
- [x] MRT/UAV use below profile; unsupported texture dimensions; SV misuse in SM2.
- [x] Emit clear diagnostics and set `invalid: true` when applicable.

## M6: IR invariants
- [ ] Run `AssertValidIr()` after legalization; maintain SSA-like definitions and typed ops.
- [ ] Ensure blocks remain well-terminated; masks and references stay valid.
- [ ] Avoid introducing DX9-specific constructs.

## M7: Tests
- [ ] Unit tests per capability (positive/negative) targeting the class library.
- [ ] Rewrite tests: branch flattening removes branches; loop unrolling removes back-edges; intrinsic replacement stays backend-neutral.
- [ ] Snapshot tests on real HLSL (legal and illegal cases).
- [ ] Integration: end-to-end pipeline (`openfxc-hlsl` -> `openfxc-sem` -> `openfxc-ir lower` -> `openfxc-ir optimize` -> `openfxc-profile legalize`), then `AssertValidIr()`.

## M8: Docs and examples
- [ ] Keep README/TDD/agents aligned (CLI usage, compatibility matrix, diagnostics).
- [ ] Provide library usage snippets (request/response) and CLI examples.
- [ ] Note how submodules are consumed (structure mirroring `openfxc-ir`).

## M9: CI and packaging
- [ ] Add CI for build/test (dotnet) with submodule checkout.
- [ ] Validate formatting/lint if applicable; publish artifacts or NuGet if needed.
