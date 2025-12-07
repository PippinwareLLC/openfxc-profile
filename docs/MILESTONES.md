# Milestones - openfxc-profile (legalize)

- [ ] M0: CLI skeleton
  - [ ] `openfxc-profile legalize` verb, stdin/file input, profile override, exit codes.
  - [ ] JSON parsing and diagnostics passthrough scaffold.

- [ ] M1: Class library surface
  - [ ] Legalizer implemented as library; CLI wraps it.
  - [ ] Request/response types (profile, diagnostics, invalid flag).
  - [ ] Hooks to use submodule class libraries (`openfxc-ir`, `openfxc-sem`, `openfxc-hlsl`) directly.

- [ ] M2: Capability table codified
  - [ ] Canonical SM2-SM5 capability table encoded.
  - [ ] Shared access for validation, rewrites, and tests.

- [ ] M3: Validation rules
  - [ ] Instruction/temp limits; MRT/texture/gradient/SV restrictions per profile.
  - [ ] Unsupported intrinsic/resource dimension/UAV bans with diagnostics.

- [ ] M4: Rewriting for legality
  - [ ] Branch flattening for `ps_2_0`.
  - [ ] Simple loop unrolling (static SM2 loops).
  - [ ] Intrinsic replacements (e.g., normalize -> dot + rsq + mul) using backend-neutral ops.
  - [ ] Unsupported op splitting/removal without DX9 specifics.

- [ ] M5: Rejection paths + diagnostics
  - [ ] Dynamic branching/loops in SM2 rejected; recursion rejected.
  - [ ] MRT/UAV use below profile; unsupported texture dimensions; SV misuse rejected with clear diagnostics.
  - [ ] `invalid: true` flag set when legalization cannot succeed.

- [ ] M6: IR invariants
  - [ ] `AssertValidIr()` (or equivalent) runs after legalization.
  - [ ] SSA-like values, typed ops, valid masks/refs, well-terminated blocks preserved.

- [ ] M7: Tests
  - [ ] Unit tests per capability (positive/negative) via the class library.
  - [ ] Rewrite tests (flattening, unrolling, intrinsic replacement) stay backend-neutral.
  - [ ] Snapshot tests with real HLSL (legal and illegal cases).
  - [ ] Integration pipeline: `openfxc-hlsl` -> `openfxc-sem` -> `openfxc-ir lower` -> `openfxc-ir optimize` -> `openfxc-profile legalize` + `AssertValidIr()`.

- [ ] M8: Docs + polish
  - [ ] README/agents/TDD updated with CLI usage, compatibility matrix, and diagnostics.
  - [ ] Library usage snippets and end-to-end examples documented.
  - [ ] Repository structure mirrors `openfxc-ir` for consistency.

- [ ] M9: CI and packaging
  - [ ] CI builds/tests with submodule checkout.
  - [ ] Optional: packaging/publishing strategy defined (NuGet/binaries) if needed.
