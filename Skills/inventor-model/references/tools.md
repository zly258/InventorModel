# Tools and workflow

InventorModel exposes one external interface: the standalone MCP server.

## Tool contract

| Tool | Purpose | Arguments |
| --- | --- | --- |
| `validate` | Dry-run syntax/semantic validation without starting Inventor. | `script` or `path` |
| `status` | Pure query: report whether Inventor is running plus active/working Part, revision, strategy, and MCP workspace. Never starts Inventor. | none |
| `start_inventor` | Start Inventor if needed, attach to the running instance, and force it visible. Does not create a document. | none |
| `new_part` | Explicitly create and activate a new session working Part. Use only for an intentional new model. | none |
| `build` | Synchronize complete `.ivmodel` source into the working Part. Duplicate builds are suppressed; other builds automatically choose parameter update, feature update, local structural rebuild, or same-document full rebuild. | `script` or `path` |
| `modify` | Apply one local delta: `set`, `edit <feature> <property> <value>`, `suppress`, `unsuppress`, or `delete`. | `command` |
| `inspect` | Return structured inspection. Defaults to compact summary. Use `detail` to query deeper structures. | optional `detail`: `"summary"` (default), `"parameters"`, `"sketches"`, `"features"`, `"all"` |
| `geometry` | Query bounded revision-local edge/face topology with optional deterministic filters. | optional `entity` (`"all"`, `"edge"`, `"face"`), `curveType`, `surfaceType`, `axis`, `nearX`, `nearY`, `nearZ`, `tolerance`, `radius`, `minLength`, `maxLength`, `maxEdges`, `maxFaces` |
| `render` | Render PNG verification views. Defaults to four views. Intermediate checks can specify a subset. | optional `views` (e.g. `"front,iso"`), optional `size` (default 640), optional `directory` |
| `save` | Save the working Part as native IPT. | optional `path`, optional `overwrite` |

## Efficient call order

For a new Part, call `build` directly. If Inventor is not running, the server starts it visibly and creates the first working Part:

```text
build
→ evaluate returned summary inspection against expected invariants
→ geometry (with filters) only when exact finishing topology indexes are needed
→ render (intermediate subset or full) when visual verification matters
→ modify or one materially different rebuild if evidence requires it
→ save
```

1. **Do not immediately call `inspect` after `build` or `modify`**: both already return the updated summary inspection.
2. **Server-side incremental synchronization**: repeating identical `build` calls is a no-op. Parameter-only and supported feature changes update native Inventor objects in place. Structural changes rebuild the smallest safe suffix.
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
