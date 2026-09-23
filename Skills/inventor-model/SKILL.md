---
name: inventor-model
description: Create, inspect, verify, modify, and save native Autodesk Inventor 2023 Part models through the InventorModel MCP server and its .ivmodel DSL. Use for text/image/drawing-to-part modeling, parameter edits, feature suppression or deletion, four-view verification, and native IPT output.
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
- After meaningful geometry changes, run `inspect` first. Body count, overall envelope, parameters, sketch constraint status, feature tree, and feature health are deterministic acceptance gates.
- Before any topology-indexed finishing operation, call `geometry` on the current model state with the smallest useful limits and use the returned 1-based edge/face indexes. Never guess indexes from an earlier topology state.
- Prefer feature `direction positive|negative|symmetric` over changing sketch planes merely to flip a feature. Use a sketch-line axis for revolved/turned profiles when the drawing centerline defines the intended shaft axis.
- Use `render` only after deterministic gates are plausible, as final visible-shape confirmation rather than as a trigger for open-ended trial and error.
- Never repeat an identical tool call on unchanged model state. After the initial build, allow at most one materially different structural rebuild in a user turn; if the result is still wrong, report the exact unsupported or uncertain geometry instead of approximating repeatedly.
- Keep generated scripts, renders, default outputs, and temporary artifacts inside the current InventorModel MCP workspace.
- Do not invent scratch paths elsewhere. Save a final IPT outside the workspace only when the user explicitly requests a destination.
- Do not report success until the relevant tool call succeeds.

## Procedure

1. Check `status` when Inventor connection or the active document is uncertain.
2. Read [references/dsl.md](references/dsl.md) and the relevant syntax reference before generating source.
3. Plan the smallest valid native feature tree that matches the requested shape.
4. Generate complete `.ivmodel` source and call `build`. `build` validates internally and returns the deterministic inspection in the same tool result. Use `validate` only for an explicit dry run or syntax debugging.
5. Evaluate the inspection returned by `build`: body count, envelope, parameters, sketch constraint status, feature tree, and feature health. Do not immediately call `inspect` again.
6. If the model needs selective fillets, chamfers, shell faces, or an exact indexed planar face, call `geometry` with small limits and use indexes from this exact model revision. Add finishing operations only after topology is known.
7. Read [references/verification.md](references/verification.md) and call one final `render` for silhouette/proportion verification when shape matters. The default 640 px four-view set is preferred for speed.
8. Correct parameter or feature-state mistakes with `modify`; its result already includes the updated inspection. For a structural mismatch, make one materially different corrected complete source and `build` it into the same working Part.
9. If deterministic or visual acceptance still fails after that structural correction, stop and report the remaining mismatch/capability gap instead of creating another Part or repeating variants.
10. Save an IPT only when requested or when the workflow requires a native deliverable.

## References

- [DSL fundamentals](references/dsl.md) — grammar, parameters, planes, face selectors, edits, and naming rules.
- [Sketch syntax](references/sketches.md) — supported sketch entities, constraints, dimensions, and limitations.
- [Feature syntax](references/features.md) — supported Part features, operations, selectors, and current limitations.
- [Tools and workflow](references/tools.md) — MCP tool contracts and efficient call order.
- [Verification and repair](references/verification.md) — inspection, four-view validation, failure patterns, and correction strategy.
- [Modeling patterns](references/patterns.md) — compact validated construction patterns for common mechanical parts.
