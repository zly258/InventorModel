---
name: inventor-model
description: Create, inspect, verify, modify, and save native Autodesk Inventor 2023 Part models with InventorModel's .ivmodel DSL and its embedded or MCP tools. Use for text/image/drawing-to-part modeling, parameter edits, feature suppression or deletion, four-view verification, and native IPT output.
---

# InventorModel

InventorModel is a focused Autodesk Inventor **Part** modeling skill. The model representation is the line-oriented `.ivmodel` DSL; JSON is only tool transport and must not become a second model format.

## Core rules

- Model only native editable Inventor Parts with syntax implemented by this repository.
- Do not invent DSL commands, feature options, selectors, constraints, or edit operations.
- Define parameters before they are used and keep important dimensions parameterized.
- Keep top-level parameter, sketch, and feature names unique and stable.
- Build in dependency order: parameters -> sketches -> base features -> dependent sketches/features -> finishing features.
- A modeling task uses exactly one session working Part. The first `build` creates it; every later complete `build` replaces generated geometry inside that same Part. Never create multiple Parts to try visual variants.
- Prefer `modify` for supported local edits: `set`, `suppress`, `unsuppress`, and `delete`.
- Rebuild only when a requested change requires sketch topology, feature arguments, feature order, or another unsupported local edit to change.
- After meaningful geometry changes, run `inspect` first. Body count, overall envelope, parameters, and feature tree are deterministic acceptance gates.
- Use `render` only after deterministic gates are plausible, as final visible-shape confirmation rather than as a trigger for open-ended trial and error.
- Never repeat an identical tool call on unchanged model state. After the initial build, allow at most one materially different structural rebuild in a user turn; if the result is still wrong, report the exact unsupported or uncertain geometry instead of approximating repeatedly.
- Keep internal scripts, image attachments, renders, output defaults, and temporary artifacts inside the current InventorModel AI workspace.
- Do not invent scratch paths elsewhere. Save a final IPT outside the workspace only when the user explicitly requests a destination.
- Do not report success until the relevant tool call succeeds.

## Procedure

1. Check `status` when Inventor connection or the active document is uncertain.
2. Read [references/dsl.md](references/dsl.md) and the relevant syntax reference before generating source.
3. Plan the smallest valid native feature tree that matches the requested shape.
4. Generate complete `.ivmodel` source, call `validate`, fix every diagnostic, then call `build`. This creates the single session working Part only if one does not already exist.
5. Validate body count, overall size, parameters, and feature tree with `inspect`. Do not use vision to override known deterministic failures.
6. Read [references/verification.md](references/verification.md) and call `render` for final silhouette/proportion verification when shape matters.
7. Correct parameter or feature-state mistakes with `modify`. For a structural mismatch, make one materially different corrected complete source and `build` it into the same working Part.
8. If deterministic or visual acceptance still fails after that structural correction, stop and report the remaining mismatch/capability gap instead of creating another Part or repeating variants.
9. Save an IPT only when requested or when the workflow requires a native deliverable.

## References

- [DSL fundamentals](references/dsl.md) — grammar, parameters, planes, face selectors, edits, and naming rules.
- [Sketch syntax](references/sketches.md) — supported sketch entities, constraints, dimensions, and limitations.
- [Feature syntax](references/features.md) — supported Part features, operations, selectors, and current limitations.
- [Tools and workflow](references/tools.md) — embedded and MCP tool contracts and efficient call order.
- [Verification and repair](references/verification.md) — inspection, four-view validation, failure patterns, and correction strategy.
- [Modeling patterns](references/patterns.md) — compact validated construction patterns for common mechanical parts.
