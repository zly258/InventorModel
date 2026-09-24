---
name: inventor-model
description: Create, inspect, verify, modify, and save native Autodesk Inventor 2023 Part models through the InventorModel MCP server and its .ivmodel DSL. Use for text/image/drawing-to-part modeling, parameter edits, feature suppression or deletion, four-view verification, and native IPT output.
---

# InventorModel

InventorModel creates and edits native Autodesk Inventor **Part** models via line-oriented `.ivmodel` DSL scripts executed against the standalone MCP server.

## Core Rules

- **Construction Strategy First**: Read [references/strategy.md](references/strategy.md). Select the primary strategy (e.g., revolve for rotational parts, base extrude + cuts for prismatic parts, shell for housings) before writing DSL.
- **Expected Invariants**: Before building, define internal target invariants (Part Class, Primary Construction, Expected Body Count [normally 1], Expected Envelope X/Y/Z, Key Features). Compare returned inspection against these invariants.
- **Single Working Part**: Exactly one session working Part is used. `build` starts visible Inventor and creates the first Part automatically when needed. Later builds update or rebuild inside that same Part. Call `new_part` only when the user explicitly requests a genuinely new model.
- **Inspection Discipline**: Always consume the deterministic inspection summary already returned by `build` and `modify`. Call `inspect` only when the model state may have changed externally or when deep diagnostic detail (`detail="parameters"|"sketches"|"features"|"all"`) is needed.
- **Finishing Discipline**: Always place fillets and chamfers last. Never guess edge/face indexes; query `geometry` using deterministic filters (such as `entity="edge"`, `curveType="circle"`, `nearZ=...`) on the current revision.
- **No Identical Retries**: Never repeat identical tool calls. Identical builds are suppressed on the server. If a build fails, inspect the structured error (`errorCode`, `stage`, `recommendedAction`) and correct the script.
- **Modify vs Build**: Prefer conversational `modify` for small deltas: `set`, `edit <feature> <property> <value>`, `suppress`, `unsuppress`, or `delete`. For a complete target state, call `build`; the server chooses no-op, parameter update, feature update, local structural rebuild, or same-document full rebuild automatically.
- **Verification Discipline**: Use `render` only after deterministic inspection matches expected invariants. Use view subsets (`views="front,iso"`) for quick checks and all four views for final sign-off.
- **Workspace Containment**: Keep scripts, renders, and temporary outputs inside the session workspace. Save outside only when explicitly requested.

## Procedure

1. **Plan & Invariants**: Determine mechanical strategy from [references/strategy.md](references/strategy.md) and establish expected invariants (envelope, bodies, features). `status` is a pure query. Normally call `build` directly; it starts visible Inventor automatically if required.
2. **Draft DSL**: Follow [references/dsl.md](references/dsl.md), [references/sketches.md](references/sketches.md), and [references/features.md](references/features.md). Define parameters before use; construct features in strict dependency order.
3. **Build Base**: Call `build(script=...)`. The server builds and returns the deterministic inspection summary.
4. **Compare Invariants**: Check `bodyCount == 1`, `unhealthyFeatureCount == 0`, and `sizeMm` against your expected invariants.
5. **Add Finishing (Optional)**: If fillets/chamfers are needed, query `geometry` with specific filters (`curveType`, `nearZ`, etc.) to obtain revision-local indexes, then append finishing features in a single pass.
6. **Visual Check**: Call `render` once for silhouette/proportion confirmation.
7. **Refine or Deliver**: Apply parameter fixes with `modify`. If a structural mistake is found, allow at most one corrected rebuild. Save native IPT via `save` when requested.

## References

- [Construction strategy & invariants](references/strategy.md) — strategy priority, invariant verification, finishing discipline.
- [DSL fundamentals](references/dsl.md) — grammar, parameters, planes, face selectors, edits, and naming rules.
- [Sketch syntax](references/sketches.md) — sketch entities, constraints, dimensions, and centerline axes.
- [Feature syntax](references/features.md) — extrude, revolve, sweep, loft, hole, fillet, chamfer, shell, patterns, mirror.
- [Tools and workflow](references/tools.md) — MCP tool contracts, parameters, filtered geometry, and structured errors.
- [Verification and repair](references/verification.md) — invariant gates, view checks, error recovery, and failure patterns.
- [Modeling patterns](references/patterns.md) — concise validated examples for common mechanical components.
