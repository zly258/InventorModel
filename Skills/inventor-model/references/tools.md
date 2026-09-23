# Tools and workflow

InventorModel exposes the same core modeling workflow through the embedded AI Chat and the external MCP server.

## Tool contract

| Tool | Purpose | Arguments |
| --- | --- | --- |
| `validate` | Optional dry-run syntax/semantic validation without starting Inventor. `build` validates internally. | `script`, or CLI/MCP `path` where supported. |
| `skill_reference` | Embedded AI only: load one detailed reference on demand. | `name` |
| `status` | Check Inventor connection, active Part, and current AI workspace. | none |
| `build` | Validate and build complete `.ivmodel` in the session working Part. The result includes deterministic inspection. | Embedded: `script`. MCP: `script` or `path`; `script` takes precedence. |
| `modify` | Apply one supported local edit to the active Part. | `command` |
| `inspect` | Return body/sketch/feature counts, overall size, parameters, sketch constraint status, feature tree, and feature health. | none |
| `geometry` | Return a bounded first-body topology snapshot with 1-based edge/face indexes for the current model revision. | optional `maxEdges` (default 64), optional `maxFaces` (default 32) |
| `render` | Save front/top/right/isometric PNG views. | optional `size` (default 640); MCP also accepts optional `directory` |
| `save` | Save the active Part as native IPT. | optional `path`, optional `overwrite`; omitted path uses the workspace output directory |

## Efficient call order

For a new part:

```text
build -> use returned inspection -> geometry only when indexed finishing is needed -> render once -> modify/rebuild only when evidence requires it -> save
```

Do not repeatedly call `build` with tiny variations. One task owns one working Part; never create another Part just because visual verification is imperfect. Use `modify` for supported local corrections. A user turn may use the initial build plus at most one materially different structural replacement build.

Internal AI artifacts must remain in the current workspace. The effective `.ivmodel` source is kept under `scripts`, image attachments under `attachments`, and four-view verification images under `renders`. Do not create ad-hoc scratch files elsewhere.

For an existing active part:

```text
status -> inspect -> geometry when topology indexes are needed -> modify -> inspect -> render when useful
```

`geometry` is revision-specific. Any topology-changing build/modify invalidates previously observed edge/face indexes; query again before using them.

## Build versus modify

Use `modify` for:

```text
set width = 120
suppress rounds
unsuppress rounds
delete mountHole
```

Use a new complete `build` when you need to add/remove sketch entities, change a sketch plane, change non-parameterized feature arguments, add a new feature, reorder the tree, or replace the construction strategy. In the embedded AI this is a replacement rebuild of the same working PartDocument, not creation of a second retry document.

## Tool-result discipline

A natural-language plan is not proof that Inventor accepted the model. Treat tool output as authoritative. `build` and `modify` already return structured inspection, so do not waste a tool round by immediately calling `inspect` again. Use `geometry` as the only source for edge/face indexes, keep its limits small, and never reuse those indexes after topology changes. If a tool returns an error, correct the responsible source or command before proceeding. MCP `render` returns four standard image content blocks; the embedded AI reinjects the four PNGs as multimodal input for the next reasoning round.
