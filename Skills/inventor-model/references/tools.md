# Tools and workflow

InventorModel exposes one external interface: the standalone MCP server.

## Tool contract

| Tool | Purpose | Arguments |
| --- | --- | --- |
| `validate` | Optional dry-run syntax/semantic validation. `build` validates internally. | `script` or `path` |
| `status` | Check Inventor connection, active Part, session working Part, and MCP workspace. | none |
| `build` | Validate and build complete `.ivmodel` source in the session working Part. The result includes deterministic inspection. | `script` or `path`; `script` takes precedence |
| `modify` | Apply one supported local edit to the working Part. | `command` |
| `inspect` | Return body/sketch/feature counts, size, parameters, sketch constraints, feature tree, and feature health. | none |
| `geometry` | Return bounded revision-local edge/face topology. | optional `maxEdges` (default 64), optional `maxFaces` (default 32) |
| `render` | Return front/top/right/isometric PNG verification images. | optional `size` (default 640), optional `directory` |
| `save` | Save the working Part as native IPT. | optional `path`, optional `overwrite` |

## Efficient call order

For a new Part:

```text
build
→ use returned inspection
→ geometry only when exact topology indexes are needed
→ render once when visual verification matters
→ modify or one materially different rebuild if evidence requires it
→ save
```

Do not immediately call `inspect` after `build` or `modify`; both already return the updated inspection.

Do not repeatedly call `build` with small guesses. One MCP session owns one working Part. A structural rebuild replaces generated state in the same PartDocument.

## Workspace discipline

Generated MCP artifacts remain in the session workspace:

```text
Documents\InventorModel\Workspace\Sessions\...\
├─ scripts
├─ renders
├─ output
└─ temp
```

Do not invent ad-hoc scratch directories. Save the final IPT elsewhere only when the user or calling workflow explicitly requests a destination.

## Existing Part workflow

```text
status
→ inspect when current state is unknown
→ geometry only when topology indexes are needed
→ modify
→ use returned inspection
→ render when useful
```

`geometry` is revision-specific. Any topology-changing build or edit invalidates previously observed edge/face indexes.

## Build versus modify

Use `modify` for:

```text
set width = 120
suppress rounds
unsuppress rounds
delete mountHole
```

Use a complete `build` when the requested correction changes sketch topology, feature arguments, feature order, adds new features, or changes the construction strategy.

## Tool-result discipline

MCP tool results are authoritative.

- A successful natural-language plan is not proof that Inventor accepted the model.
- `unhealthyFeatureCount` must be zero before visual acceptance.
- `underConstrainedSketchCount` is a parametric-quality warning unless full constraint is explicitly required.
- Use `geometry` as the only source for revision-local face/edge indexes.
- If `geometry.truncated` is true, increase only the required limit.
- `render` returns four image content blocks that the external MCP client can pass back to its multimodal model.
- If a tool fails, correct the responsible source/command rather than repeating the same call unchanged.
