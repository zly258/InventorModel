# Tools and workflow

InventorModel exposes the same core modeling workflow through the embedded AI Chat and the external MCP server.

## Tool contract

| Tool | Purpose | Arguments |
| --- | --- | --- |
| `validate` | Check `.imodel` syntax and semantics without starting Inventor. | `script`, or CLI/MCP `path` where supported. |
| `skill_reference` | Embedded AI only: load one detailed reference on demand. | `name` |
| `status` | Check Inventor connection and active Part. | none |
| `build` | Create a new native editable Part from complete `.imodel`. | Embedded: `script`. MCP: `script` or `path`; `script` takes precedence. |
| `modify` | Apply one supported local edit to the active Part. | `command` |
| `inspect` | Return body count, feature count, overall size, parameters, and feature tree. | none |
| `render` | Save front/top/right/isometric PNG views. | Embedded: optional `directory`. MCP: `directory` is required. |
| `save` | Save the active Part as native IPT. | `path`, optional `overwrite` |

## Efficient call order

For a new part:

```text
status -> validate -> build -> inspect -> render when useful -> modify/rebuild if needed -> save
```

Do not repeatedly call `build` with tiny variations when a parameter change can be expressed as one `modify` call.

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

A natural-language plan is not proof that Inventor accepted the model. Treat tool output as authoritative. If a tool returns an error, correct the responsible source or command before proceeding. MCP `render` returns four standard image content blocks; the embedded AI reinjects the four PNGs as multimodal input for the next reasoning round.
