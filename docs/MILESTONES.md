# Milestones - openfxc-profile (legalize)

- [x] M0: CLI skeleton
  - [x] `openfxc-profile legalize` verb, stdin/file input, profile override, exit codes.
  - [x] JSON parsing and diagnostics passthrough scaffold.

- [x] M1: Class library surface
  - [x] Legalizer implemented as library; CLI wraps it.
  - [x] Request/response types (profile, diagnostics, invalid flag).
  - [x] Hooks to use submodule class libraries (`openfxc-ir`, `openfxc-sem`, `openfxc-hlsl`) directly.

- [x] M2: Capability table codified
  - [x] Canonical SM2-SM5 capability table encoded.
  - [x] Shared access for validation, rewrites, and tests.

- [x] M3: Validation rules
  - [x] Instruction/temp limits; MRT/texture/gradient/SV restrictions per profile.
  - [x] Unsupported intrinsic/resource dimension/UAV bans with diagnostics.

- [x] M4: Rewriting for legality
  - [x] Branch flattening for `ps_2_0`.
  - [x] Simple loop unrolling (static SM2 loops).
  - [x] Intrinsic replacements (e.g., normalize -> dot + rsq + mul) using backend-neutral ops.
  - [x] Unsupported op splitting/removal without DX9 specifics.

- [x] M5: Rejection paths + diagnostics
  - [x] Dynamic branching/loops in SM2 rejected; recursion rejected.
  - [x] MRT/UAV use below profile; unsupported texture dimensions; SV misuse rejected with clear diagnostics.
  - [x] `invalid: true` flag set when legalization cannot succeed.

- [x] M6: IR invariants
  - [x] `AssertValidIr()` (or equivalent) runs after legalization.
  - [x] SSA-like values, typed ops, valid masks/refs, well-terminated blocks preserved.

- [x] M7: Tests
  - [x] Unit tests per capability (positive/negative) via the class library.
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
