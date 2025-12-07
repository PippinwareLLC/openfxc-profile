# TDD – `openfxc-profile` (Shader Model Legalization for OpenFXC)

## 0. Overview

This document defines the **Test-Driven Development specification** for the **`openfxc-profile`** component of OpenFXC.

`openfxc-profile` is responsible for applying **shader model correctness and hardware constraints** to backend-agnostic IR produced by:

```
openfxc-hlsl parse
openfxc-sem analyze
openfxc-ir lower
openfxc-ir optimize
↓
openfxc-profile legalize   ← YOU ARE HERE
↓
openfxc-dx9 lower
openfxc-dxbc emit
```

This stage **does not** introduce DX9-specific instructions or registers.
It only ensures the IR is **valid for the target shader model** (SM2, SM3, SM4, SM5) before backend lowering.

### Architecture & Packaging

* Core functionality resides in a class library; the CLI is a thin wrapper over that library.
* Unit tests exercise the class library directly (not the CLI).
* `openfxc-profile` should consume the submodule class libraries (`openfxc-ir`, `openfxc-sem`, `openfxc-hlsl`) directly where needed.
* Follow the structural patterns used in `openfxc-ir` to keep repository organization consistent.

---

## 1. Goals of `openfxc-profile`

`openfxc-profile legalize` shall:

1. **Validate** the current IR against Shader Model rules.
2. **Rewrite IR when possible** to satisfy those rules (flattening, constantization, removing unsupported ops).
3. **Reject** shaders that cannot be lowered legally under the requested profile.
4. Produce **profile-legal IR JSON** preserving the IR invariants.

### Allowed transformations:

* Branch flattening (for `ps_2_0`)
* Loop unrolling (simple loops)
* Removal of unsupported operations
* Replacement of unsupported intrinsics (e.g., emulate `normalize`)
* Splitting instructions if needed (e.g., decompose long ops)
* Marking resources as unsupported if they violate profile caps

### Forbidden transformations:

* Introducing DX9-specific opcodes
* Assigning registers (that happens in `openfxc-dx9`)
* Doing bytecode layout manipulations (`openfxc-dxbc`)
* Changing IR into something backend-dependent

---

## 2. CLI Contract

### 2.1 Command

```bash
openfxc-profile legalize --profile <profile> < input.ir.json > output.ir.legal.json
```

Examples:

```bash
openfxc-profile legalize --profile ps_2_0 < shader.ir.json > shader.legal.ir.json
openfxc-profile legalize --profile vs_3_0 < shader.ir.json > shader.legal.ir.json
```

### 2.2 Input

* IR JSON produced by `openfxc-ir optimize`.
* Must include:

  * `profile` (can be overridden by `--profile`)
  * `functions`
  * `blocks`, `instructions`
  * `resources`
  * `values` table

### 2.3 Output

* IR JSON, same shape as input, but:

  * Legalized for specified SM profile
  * Possibly transformed (rewritten, flattened, restricted)
  * With new diagnostics appended
  * May reject via diagnostics if not fixable

### 2.4 Exit Codes

| Code | Meaning                                                           |
| ---- | ----------------------------------------------------------------- |
| 0    | Profile legalization completed successfully (diagnostics allowed) |
| 1    | Internal error                                                    |

---

## 3. Shader Model Capability Table (Canonical)

Profile rules are centrally defined via a capability table:

| Capability              | SM2.0        | SM3.0       | SM4.0          | SM5.0          |
| ----------------------- | ------------ | ----------- | -------------- | -------------- |
| Dynamic branching       | ❌ PS / ❌ VS  | ✔           | ✔              | ✔              |
| Loops                   | Limited**    | ✔           | ✔              | ✔              |
| Predication             | Limited      | ✔           | ✔              | ✔              |
| Texture instructions    | ~32          | More        | Unlimited      | Unlimited      |
| Gradient ops (ddx/ddy)  | ❌ vs / ❌ ps2 | ✔ ps3       | ✔              | ✔              |
| Vertex texture fetch    | ❌            | ✔           | ✔              | ✔              |
| Temp registers          | ~12          | ~32         | Unlimited      | Unlimited      |
| Instruction slots       | ~64          | 512         | Unlimited      | Unlimited      |
| Multiple Render Targets | ❌ ps2        | ✔ ps3       | ✔              | ✔              |
| SV semantics            | ❌            | ❌           | ✔              | ✔              |
| Sampler types           | legacy-only  | legacy-only | modern objects | modern objects |
| Typed UAVs              | ❌            | ❌           | ✔              | ✔              |

**Limited** means static or unrolled loops only.

Tests shall ensure the capability table is used consistently.

---

## 4. Responsibilities of `openfxc-profile legalize`

### 4.1 Validate IR Against Profile Limits

Tests must assert:

* **Too many instructions** triggers an error.
* **Too many temps** triggers an error.
* **Gradient op (`ddx`, `ddy`) in SM2** → diagnostic.
* **Texture sampling in VS2** → diagnostic.
* **Vertex texture fetch in SM2** → diagnostic.
* **SV semantics used in SM2** → diagnostic.
* **Undefined or unsupported intrinsics** → error.

---

### 4.2 Rewrite IR to Fit Profile (When Possible)

Tests for *legalizable* cases:

#### 4.2.1 Branch Flattening (ps_2_0)

Input HLSL:

```hlsl
if (x > 0) { a = 1; } else { a = 2; }
```

IR:

* Branch structure

Legalization for PS2:

* Flatten to equivalent select:

```
%cond = Compare %x > 0
%a_then = Const 1
%a_else = Const 2
%a = Select %cond, %a_then, %a_else
```

Test asserts:

* No `Branch`/`BranchCond` remain.
* Control flow converted to straight-line code with a `Select`-like op (or backend-neutral equivalent).

---

#### 4.2.2 Loop Unrolling (SM2)

Input HLSL:

```hlsl
for (int i = 0; i < 4; i++)
    sum += weights[i];
```

Legalization:

* Emit 4 explicit adds
* Remove loop structure

Tests:

* Loop is removed.
* 4 IR ops inserted.
* No back-edges remain in CFG.

---

#### 4.2.3 Removing Unsupported Ops

Example:

* `Normalize` not supported natively in SM2 VS.

  * legalizer shall rewrite as:

```
len = Dot %v, %v
inv = Rsq len
res = Mul %v, inv
```

Where:

* `Dot`, `Rsq`, `Mul` remain backend-neutral IR ops.

Test:

* IR rewriting occurs and produces no DX9-specific operations.

---

### 4.3 Reject IR When Not Legalizable

Tests:

* Dynamic loop → diagnostic + error (SM2).
* Function recursion → error (all profiles).
* Use of unsupported dimension on texture type → error.
* MRTs used in SM2 → error.

Failure mode:

```json
"diagnostics": [
  {
    "id": "PROFILE2001",
    "severity": "Error",
    "message": "Dynamic branching not supported in ps_2_0 profile",
    "span": { ... }
  }
]
```

Legalization may continue producing IR for debug purposes, but clearly marked `invalid: true`.

---

### 4.4 Preserve IR Invariants

After legalization:

* Values still SSA-like.
* Blocks still well-terminated.
* Types preserved.
* No DX9-specific concepts injected.
* Masks still valid.
* All reference IDs are valid.

Tests:

* Run `AssertValidIr()` after legalizer in all integration tests.

---

## 5. Test Strategy

### 5.1 Unit Tests Per Profile Capability

Each capability from the table gets its own test file.

#### Example: SM2 Dynamic Branch Test

Input IR:

* `BranchCond` block structure.

Expected:

* Diagnostic: “dynamic branching unsupported”
* IR flattened.

#### Example: SM4 SV Semantic Pass-Through

Input:

* IR with `SV_Position` and modern resources.

Expected:

* No diagnostics.
* IR unchanged.

---

### 5.2 Snapshot Tests

Using real HLSL examples:

#### Simple SM2 shader

```hlsl
float4 main(float4 pos : POSITION) : COLOR0
{
    return pos;
}
```

Expected:

* No changes needed.
* IR exactly preserved.

#### SM2 with illegal feature

```hlsl
float4 main(float4 pos : POSITION) : COLOR0
{
    return ddx(pos);
}
```

Expected:

* Diagnostic error.
* IR unchanged or rewritten (depending on legality).

---

### 5.3 Failure Mode Tests

Ensure clear diagnostics when no legal transformation exists:

* Texture array in SM2.
* UAVs in SM3.
* Classes/interfaces in SM2.
* Vertex shader trying to write to COLOR0 in SM4 model.

---

### 5.4 Integration Tests

End-to-end:

```bash
openfxc-hlsl parse file.hlsl \
  | openfxc-sem analyze \
  | openfxc-ir lower \
  | openfxc-ir optimize \
  | openfxc-profile legalize --profile ps_2_0 \
    > file.ir.legal.json
```

Assert:

* Diagnostics only where expected.
* IR invariants hold.
* Rewriting applied correctly.

---

## 6. Definition of Done (DoD)

`openfxc-profile` is complete when:

1. CLI command `openfxc-profile legalize` works from stdin to stdout.
2. Every capability in the SM table has:

   * A positive test
   * A negative test
   * A clear diagnostic
3. Legalizer performs:

   * Branch flattening (PS2)
   * Loop unrolling (simple loops)
   * Intrinsic replacement for unsupported ops
   * IR validation for profile rules
4. No DX9-specific instructions, registers, or constraints appear in the output IR.
5. IR invariants are preserved:

   * SSA-like value definitions
   * Typed ops
   * Correct block termination
   * No dangling references
6. Integration tests across SM2, SM3, SM4, and SM5 pass.
7. Legalizer can fail safely with a clean diagnostic.


