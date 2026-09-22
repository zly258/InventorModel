# Tools and workflow

InventorModel exposes the same core modeling workflow through the embedded AI Chat and the external MCP server.

## Tool contract

| Tool | Purpose | Arguments |
| --- | --- | --- |
| `validate` | Check `.ivmodel` syntax and semantics without starting Inventor. | `script`, or CLI/MCP `path` where supported. |
| `skill_reference` | Embedded AI only: load one detailed reference on demand. | `name` |
| `status` | Check Inventor connection, active Part, and current AI workspace. | none |
| `build` | Create a new native editable Part from complete `.ivmodel`. | Embedded: `script`. MCP: `script` or `path`; `script` takes precedence. |
| `modify` | Apply one supported local edit to the active Part. | `command` |
| `inspect` | Return structured JSON with body/sketch/feature counts, overall size, parameters, and feature tree. | none |
| `render` | Save front/top/right/isometric PNG views. | Embedded: no arguments and always uses the workspace. MCP: optional `directory`; omitted uses the workspace. |
| `save` | Save the active Part as native IPT. | optional `path`, optional `overwrite`; omitted path uses the workspace output directory |

## Efficient call order

For a new part:

```text
status -> validate -> build -> inspect -> render when useful -> modify/rebuild if needed -> save
```

Do not repeatedly call `build` with tiny variations when a parameter change can be expressed as one `modify` call.

Internal AI artifacts must remain in the current workspace. The effective `.ivmodel` source is kept under `scripts`, image attachments under `attachments`, and four-view verification images under `renders`. Do not create ad-hoc scratch files elsewhere.

For an existing active part:

```text
status -> inspect -> modify -> inspect -> render when useful
```

## Build versus modify

Use `modify` for:

```text
set width = 120
suppress rounds
unsuppress rounds
delete mountHole
```

Use a new complete `build` when you need to add/remove sketch entities, change a sketch plane, change non-parameterized feature arguments, add a new feature, reorder the tree, or replace the construction strategy.

## Tool-result discipline

A natural-language plan is not proof that Inventor accepted the model. Treat tool output as authoritative. The `inspect` result is structured JSON; use its counts, dimensions, parameters, and feature list directly instead of parsing human-formatted prose. If a tool returns an error, correct the responsible source or command before proceeding. MCP `render` returns four standard image content blocks; the embedded AI reinjects the four PNGs as multimodal input for the next reasoning round.
