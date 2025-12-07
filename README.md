# openfxc-profile

Legalize backend-agnostic IR (from `openfxc-ir optimize`) against shader model rules, rewriting where possible and rejecting when not, before DX9/DXBC lowering.

## Scope (legalize)
- Input: IR JSON from `openfxc-ir optimize`.
- Output: profile-legal IR JSON (SM2-SM5), preserving IR invariants and diagnostics.
- CLI: `openfxc-profile legalize --profile <name> < input.ir.json > output.ir.legal.json` (exit 0 on success with diagnostics allowed, 1 on internal error).

## Architecture
- Core functionality in a class library; CLI is a thin wrapper.
- Unit tests exercise the class library directly (not the CLI).
- Consumes submodule class libraries directly (`openfxc-ir`, `openfxc-sem`, `openfxc-hlsl`).
- Mirror repository layout and packaging patterns used in `openfxc-ir` for consistency.

## Key principles
- Backend-neutral: do not introduce DX9/DXBC opcodes, registers, or layout concerns.
- Rewrite when legal (flatten branches, unroll simple loops, replace unsupported intrinsics); reject with clear diagnostics when not.
- Preserve IR invariants: SSA-like values, typed ops, valid masks, well-terminated blocks, no dangling references.
- Allow diagnostics without crashing; may emit IR marked invalid for debugging.

## Compatibility matrix (current)
| Profile band | Legalize | Notes |
| --- | --- | --- |
| SM2.x (vs_2_0/ps_2_0) | **Supported**: validates caps (temps, instructions, MRT ban, gradient/texture limits), performs branch flattening and simple loop unrolling, replaces unsupported intrinsics. | Rejects dynamic branching/loops, vertex texture fetch, gradients in SM2, MRTs/UAVs, SV semantics. |
| SM3.x (vs_3_0/ps_3_0) | **Supported**: dynamic branching allowed; capability checks applied. | Enforces MRT/UAV restrictions and texture dimension limits; no DX9-specific ops introduced. |
| SM4.x-SM5.x (vs_4_0/ps_4_0/vs_5_0/ps_5_0/cs_5_0) | **Supported**: passes through most constructs; validates resources, SV semantics, UAV typing. | Rejects unsupported dimensions or recursion; maintains backend-neutral IR invariants. |

## Capability snapshot (canonical)
| Capability              | SM2.0                      | SM3.0    | SM4.0/5.0 |
| ----------------------- | -------------------------- | -------- | --------- |
| Dynamic branching       | No (PS/VS)                 | Yes      | Yes       |
| Loops                   | Limited (static/unrolled)  | Yes      | Yes       |
| Gradient ops (ddx/ddy)  | No                         | PS only  | Yes       |
| Vertex texture fetch    | No                         | Yes      | Yes       |
| MRTs                    | No                         | Yes      | Yes       |
| SV semantics            | No                         | No       | Yes       |
| Sampler types           | Legacy only                | Legacy   | Modern    |
| Typed UAVs              | No                         | No       | Yes       |

## Responsibilities
- Validate profile limits: instruction slots, temp registers, MRT allowance, texture/gradient rules, SV usage, unsupported intrinsics.
- Rewrite for legality: branch flattening (PS2), simple loop unrolling, intrinsic replacement (e.g., `normalize` -> dot + rsq + mul), split/remove unsupported ops while staying backend-neutral.
- Reject when illegal: dynamic loops in SM2, recursion, unsupported texture dimensions, MRTs/UAVs below allowed profiles, etc., with clear diagnostics.

## Quickstart
- Build: `dotnet build openfxc-profile.sln`
- Legalize (CLI): `openfxc-profile legalize --profile ps_2_0 < shader.ir.json > shader.ir.legal.json`
- End-to-end example: `openfxc-hlsl parse file.hlsl | openfxc-sem analyze --profile ps_2_0 | openfxc-ir lower | openfxc-ir optimize | openfxc-profile legalize --profile ps_2_0 > file.ir.legal.json`
- Tests: `dotnet test` (unit tests should target the class library directly).

## Docs
- TDD: `docs/TDD.md`
- Agents checklist: `docs/agents.md`
- TODO: `docs/TODO.md`
- Milestones: `docs/MILESTONES.md`
