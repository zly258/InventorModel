# Tools and workflow

InventorModel exposes one external interface: the standalone MCP server.

## Tool contract

| Tool | Purpose | Arguments |
| --- | --- | --- |
| `validate` | Dry-run syntax/semantic validation without starting Inventor. | `script` or `path` |
| `status` | Check Inventor connection, active Part, session working Part, revision, and MCP workspace. | none |
| `build` | Validate and build complete `.ivmodel` source in the working Part. Duplicate builds of identical source are suppressed on the server. Returns deterministic inspection summary. | `script` or `path` |
| `modify` | Apply one supported conversational edit (`set`, `suppress`, `unsuppress`, `delete`) to the active Part. Returns updated summary inspection. | `command` |
| `inspect` | Return structured inspection. Defaults to compact summary. Use `detail` to query deeper structures. | optional `detail`: `"summary"` (default), `"parameters"`, `"sketches"`, `"features"`, `"all"` |
| `geometry` | Query bounded revision-local edge/face topology with optional deterministic filters. | optional `entity` (`"all"`, `"edge"`, `"face"`), `curveType`, `surfaceType`, `axis`, `nearX`, `nearY`, `nearZ`, `tolerance`, `radius`, `minLength`, `maxLength`, `maxEdges`, `maxFaces` |
| `render` | Render PNG verification views. Defaults to four views. Intermediate checks can specify a subset. | optional `views` (e.g. `"front,iso"`), optional `size` (default 640), optional `directory` |
| `save` | Save the working Part as native IPT. | optional `path`, optional `overwrite` |

## Efficient call order

For a new Part:

```text
build
→ evaluate returned summary inspection against expected invariants
→ geometry (with filters) only when exact finishing topology indexes are needed
→ render (intermediate subset or full) when visual verification matters
→ modify or one materially different rebuild if evidence requires it
→ save
```

1. **Do not immediately call `inspect` after `build` or `modify`**: both already return the updated summary inspection.
2. **Server-side duplicate protection**: repeating identical `build` calls returns cached inspection (`unchanged: true`) without touching Inventor.
3. **Structured error recovery**: if a tool call fails, read `errorCode` (`dsl_validation`, `selector_not_found`, `feature_failed`, etc.) and `recommendedAction` to fix the source directly.

## Filtered geometry query

Instead of dumping dozens of unindexed edges into context, use deterministic geometric filters:

```json
// Query circular edges near Z=40
{
  "entity": "edge",
  "curveType": "circle",
  "nearZ": 40.0
}
```

```json
// Query top planar face
{
  "entity": "face",
  "surfaceType": "plane",
  "axis": "Z"
}
```

Returned indexes are 1-based and belong strictly to the current model revision.

## Render view filtering

```json
// Quick intermediate verification
{
  "views": "front,iso",
  "size": 512
}

// Final acceptance
{
  "views": "front,top,right,iso",
  "size": 640
}
```

## Workspace discipline

Generated MCP artifacts remain in the session workspace:

```text
Documents\InventorModel\Workspace\Sessions\...\
├─ scripts
├─ renders
├─ output
└─ temp
```

Save the final IPT elsewhere only when the user or calling workflow explicitly requests a destination path.
