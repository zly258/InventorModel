# InventorModel

InventorModel is a focused **Autodesk Inventor 2023 Part-modeling MCP server** with a compact AI-oriented Skill package.

It converts the line-oriented `.ivmodel` DSL into native Inventor sketches, parameters, and Part features. The result remains an editable `.ipt` model.

InventorModel intentionally does **not** contain an Inventor Addin, chat UI, model provider, or embedded Agent. AI clients such as Codex, Cursor, Claude-compatible MCP clients, or a custom engineering workbench connect to the standalone MCP server and load the supplied Skills.

## Architecture

```text
External AI client
      │
      ├─ Skills/inventor-model
      │
      └─ InventorModel.Mcp.exe
                 │
                 ▼
            .ivmodel DSL
                 │
                 ▼
       InventorModel.Core
                 │
                 ▼
     InventorModel.Inventor
                 │
                 ▼
      Autodesk Inventor 2023
                 │
                 ▼
         editable native IPT
```

The product boundary is deliberately narrow:

- Part modeling only;
- one persistent model representation: `.ivmodel`;
- one external runtime entry: MCP;
- one canonical Skill package;
- native Inventor output.

Assembly, Drawing, Sheet Metal, Frame, CAM, and other Inventor domains are outside the current scope.

## MCP tools

| Tool | Purpose |
| --- | --- |
| `validate` | Optional dry-run validation of `.ivmodel` source |
| `status` | Check Inventor connection and the working Part |
| `build` | Validate and build complete `.ivmodel` source in one working Part |
| `modify` | Apply a supported local edit |
| `inspect` | Inspect bounds, parameters, sketch constraints, feature tree, and health |
| `geometry` | Query bounded current edge/face topology for precise finishing |
| `render` | Return front/top/right/isometric PNG verification views |
| `save` | Save the working Part as native IPT |

Typical flow:

```text
build
  │
  ├─ deterministic inspection is returned with the build
  │
  ├─ geometry      only when exact edge/face indexes are needed
  │
  ├─ render        one final four-view verification pass
  │
  ├─ modify/build  only when evidence requires a correction
  │
  └─ save
```

A modeling task uses one session working Part. Structural rebuilds replace generated model state inside that same document instead of creating retry Parts.

## Skills

The canonical Skill package is:

```text
Skills/
└─ inventor-model/
   ├─ SKILL.md
   └─ references/
      ├─ dsl.md
      ├─ sketches.md
      ├─ features.md
      ├─ tools.md
      ├─ verification.md
      └─ patterns.md
```

The Skill defines:

- supported DSL syntax;
- model-planning rules;
- efficient MCP call order;
- topology-query rules;
- deterministic verification gates;
- four-view visual verification;
- retry and repair discipline;
- current capability boundaries.

External AI clients should load the Skill rather than guessing InventorModel syntax or tool behavior.

## Modeling capability

### Sketch

- point
- line
- circle
- exact circular arc
- ellipse
- rectangle / centered rectangle
- slot
- polygon
- spline
- common geometric constraints
- driving dimensions

### Part features

- extrude with positive / negative / symmetric direction
- revolve with global-axis or sketch-line axis
- sweep
- loft
- drilled hole with positive / negative direction
- selective fillet
- selective chamfer
- shell
- rectangular pattern
- circular pattern
- mirror

### Local edits

```text
set width = 120
suppress fillet1
unsuppress fillet1
delete hole1
```

Builds and local edits execute inside Inventor Transactions so failures roll back the current operation.

## Verification

`build` and `modify` already return structured inspection. The result includes:

- body count;
- sketch / feature counts;
- overall envelope;
- parameters;
- sketch constraint state;
- feature health;
- feature tree.

Use `geometry` only when current edge/face indexes are required.

Use `render` after deterministic checks pass. It returns:

```text
front.png
top.png
right.png
iso.png
```

The default render size is 640 px.

## Workspace

Writable MCP runtime data is kept under:

```text
%USERPROFILE%\Documents\InventorModel
├─ Workspace
└─ Logs
```

Each MCP process creates a session directory:

```text
Workspace\Sessions\YYYYMMDD\mcp-HHmmss-xxxxxxxx\
├─ renders
├─ scripts
├─ output
└─ temp
```

The effective `.ivmodel` source and default generated artifacts stay inside this workspace. A final IPT is written elsewhere only when the MCP caller explicitly provides a destination.

Runtime diagnostics are written to:

```text
%USERPROFILE%\Documents\InventorModel\Logs\runtime.log
```

## Repository

```text
InventorModel
├─ src
│  ├─ InventorModel.Core
│  ├─ InventorModel.Inventor
│  └─ InventorModel.Mcp
├─ Skills
├─ examples
├─ tests
└─ docs
```

### Projects

- **InventorModel.Core** — DSL, expressions, validation, workspace and shared runtime infrastructure.
- **InventorModel.Inventor** — native Autodesk Inventor execution, inspection and rendering.
- **InventorModel.Mcp** — standalone MCP server and the only runtime entry point.
- **InventorModel.Core.Tests** — parser / validator tests.

## Examples

The repository includes representative `.ivmodel` examples for:

1. plate
2. flange
3. shaft
4. bracket
5. sweep
6. loft
7. shell
8. constrained sketch
9. stepped shaft

## Build

Requirements:

- Windows x64
- Autodesk Inventor 2023
- .NET Framework 4.8
- .NET SDK
- Autodesk Inventor Interop assemblies

Build and test:

```powershell
.\build.ps1 -Clean
```

Default Inventor installation:

```text
C:\Program Files\Autodesk\Inventor 2023
```

Override with `InventorInstallRoot` or `InventorInteropPath` when needed.

Output:

```text
bin\x64\<Configuration>\
├─ InventorModel.Mcp.exe
├─ InventorModel.Core.dll
├─ InventorModel.Inventor.dll
├─ required runtime dependencies
└─ Skills\
```

There is no Addin installation step.

## MCP client configuration

Point your MCP client to the built executable. For example:

```json
{
  "mcpServers": {
    "inventor-model": {
      "command": "D:\\workspace\\inventor\\InventorModel\\bin\\x64\\Debug\\InventorModel.Mcp.exe"
    }
  }
}
```

The MCP process connects to Autodesk Inventor on demand.

## Design principles

- MCP and Skills only.
- No Inventor Addin or embedded chat.
- No duplicated model representation.
- Native editable Inventor features.
- One working Part per MCP session.
- Small stable tool surface.
- Deterministic inspection before visual verification.
- Bounded topology queries.
- No blind repeated rebuilds.
- Explicit capability limits instead of fabricated geometry.
- Complete diagnostics instead of silent failures.

## Current limitations

- Part modeling only.
- Face/edge indexes are revision-local rather than persistent identities.
- Arbitrary datum planes / axes beyond current base-axis and sketch-line support are limited.
- Advanced hole variants and native thread features are not yet complete.
- Complex structural edits may require a complete rebuild of the same working Part.

## Documentation

- [DSL](docs/DSL.md)
- [Architecture](docs/ARCHITECTURE.md)
- [Roadmap](docs/ROADMAP.md)
- [InventorModel Skill](Skills/inventor-model/SKILL.md)

## License

InventorModel is licensed under the [Apache License 2.0](LICENSE).

Copyright 2026 zly258.
