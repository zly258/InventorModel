---
name: inventor-model
description: Create, inspect, verify, modify, and save native Autodesk Inventor 2023 Part models with InventorModel's .imodel DSL and its embedded or MCP tools. Use for text/image/drawing-to-part modeling, parameter edits, feature suppression or deletion, four-view verification, and native IPT output.
---

# InventorModel

InventorModel is a focused Autodesk Inventor **Part** modeling skill. The model representation is the line-oriented `.imodel` DSL; JSON is only tool transport and must not become a second model format.

## Core rules

- Model only native editable Inventor Parts with syntax implemented by this repository.
- Do not invent DSL commands, feature options, selectors, constraints, or edit operations.
- Define parameters before they are used and keep important dimensions parameterized.
- Keep top-level parameter, sketch, and feature names unique and stable.
- Build in dependency order: parameters -> sketches -> base features -> dependent sketches/features -> finishing features.
- Prefer one complete `build` for a new Part.
- Prefer `modify` only for supported local edits: `set`, `suppress`, `unsuppress`, and `delete`.
- Rebuild when a requested change requires sketch topology, feature arguments, feature order, or another unsupported local edit to change.
- After meaningful geometry changes, run `inspect`. Use `render` when shape verification matters.
- Keep internal scripts, image attachments, renders, output defaults, and temporary artifacts inside the current InventorModel AI workspace.
- Do not invent scratch paths elsewhere. Save a final IPT outside the workspace only when the user explicitly requests a destination.
- Do not report success until the relevant tool call succeeds.

## Procedure

1. Check `status` when Inventor connection or the active document is uncertain.
2. Read [references/dsl.md](references/dsl.md) and the relevant syntax reference before generating source.
3. Plan the smallest valid native feature tree that matches the requested shape.
4. Generate complete `.imodel` source, call `validate`, fix every diagnostic, then call `build`.
5. Validate body count, overall size, parameters, and feature tree with `inspect`.
6. Read [references/verification.md](references/verification.md) and call `render` when visual verification is useful.
7. Correct parameter or feature-state mistakes with `modify`; rebuild for structural geometry changes.
8. Save an IPT only when requested or when the workflow requires a native deliverable.

## References

- [DSL fundamentals](references/dsl.md) — grammar, parameters, planes, face selectors, edits, and naming rules.
- [Sketch syntax](references/sketches.md) — supported sketch entities, constraints, dimensions, and limitations.
- [Feature syntax](references/features.md) — supported Part features, operations, selectors, and current limitations.
- [Tools and workflow](references/tools.md) — embedded and MCP tool contracts and efficient call order.
- [Verification and repair](references/verification.md) — inspection, four-view validation, failure patterns, and correction strategy.
- [Modeling patterns](references/patterns.md) — compact validated construction patterns for common mechanical parts.
