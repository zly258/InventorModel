# InventorModel

InventorModel is an open-source, AI-oriented parametric Part-modeling layer for **Autodesk Inventor 2023**.

It gives AI clients a small MCP tool surface and a compact `.ivmodel` DSL, then converts that DSL into native Inventor sketches, parameters, and features. The result is a normal editable `.ipt` model rather than a mesh or disposable generated artifact.

InventorModel is deliberately narrow: **Part modeling, MCP, and Skills only**. It does not embed a chat UI, model provider, Agent runtime, or Inventor Addin.

## Why InventorModel

AI-driven CAD becomes slow and unreliable when the model must choose among dozens of low-level CAD tools and repeatedly round-trip through the LLM.

InventorModel keeps the AI-facing surface small:

- one compact modeling language: `.ivmodel`;
- one persistent working Part per MCP session;
- one canonical Skill package;
- a small set of coarse-grained MCP tools;
- deterministic inspection before visual verification;
- native editable Inventor features.

A typical model can be described once, built in one call, inspected deterministically, visually checked from four views, and then saved.

## Quick start

### 1. Build

Build requirements:

- Windows x64
- Autodesk Inventor 2023
- .NET SDK
- Autodesk Inventor Interop assemblies from the installed Inventor

Build, test, and create the standalone executable:

```powershell
.\build.ps1 -Clean
```

Run the real Inventor integration tests explicitly when validating document lifecycle or COM behavior:

```powershell
.\build.ps1 -Clean -RunInventorTests
```

The integration runner launches `Inventor.exe` directly when no active Inventor COM object exists, waits for Inventor to register in the Running Object Table, and then attaches to that visible instance. This avoids relying on COM class activation for process startup.

The generated MCP server is a **self-contained .NET 8 single-file executable**. A target workstation does not need a separate .NET runtime installation; Autodesk Inventor is still required.

Default Inventor installation:

```text
C:\Program Files\Autodesk\Inventor 2023
```

Override `InventorInstallRoot` or `InventorInteropPath` when Inventor is installed elsewhere.

### 2. Configure an MCP client

Point an MCP-compatible client to the generated executable:

```json
{
  "mcpServers": {
    "inventor-model": {
      "command": "D:\\workspace\\inventor\\InventorModel\\bin\\x64\\Debug\\InventorModel.exe"
    }
  }
}
```

Load the supplied `Skills/inventor-model` Skill package in the AI client.

### 3. Build a model

A minimal `.ivmodel` file:

```text
part Plate
param width = 100
param height = 60
param thickness = 10

sketch base on XY
  centerrect 0 0 width height
end

extrude body from base depth thickness join
```

The `build` tool validates the source, starts a visible Inventor instance when needed, creates the first working Part, and then synchronizes later models into that same Part. It automatically selects the cheapest safe strategy: no-op, parameter update, feature update, local structural rebuild, or same-document full rebuild.

## Architecture

```text
External AI client
      │
      ├─ Skills/inventor-model
      │
      └─ InventorModel.exe
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

The current product boundary is:

- Part modeling only;
- one persistent model representation: `.ivmodel`;
- one external runtime entry: MCP;
- one canonical Skill package;
- native Inventor output.

Assembly, Drawing, Sheet Metal, Frame, CAM, and other Inventor domains are intentionally outside the current scope.

## MCP tools

| Tool | Purpose |
| --- | --- |
| `validate` | Optional dry-run validation of `.ivmodel` syntax and semantics |
| `status` | Pure query: check whether Inventor is running and report the current session working Part without starting Inventor |
| `start_inventor` | Explicitly start or attach to Inventor and make it visible without creating a document |
| `new_part` | Explicitly create a new session working Part; use only when a genuinely new model is requested |
| `build` | Synchronize a complete `.ivmodel` model into the session working Part using incremental update when safe |
| `modify` | Apply a supported local edit to the working Part |
| `inspect` | Inspect parameters, sketches, features, bounds, and model health |
| `geometry` | Query bounded current edge/face topology for precise finishing |
| `render` | Return front/top/right/isometric PNG verification views |
| `save` | Save the working Part as a native IPT |

Recommended workflow:

```text
build
  │
  ├─ inspection     returned automatically
  ├─ geometry       only when exact current topology is required
  ├─ render         final visual verification
  ├─ modify/build   only when evidence requires a correction
  └─ save
```

Identical builds are suppressed when the source has not changed. Parameter and supported feature changes update native Inventor objects in place. Structural changes rebuild only the dirty suffix when safe. Even the fallback full rebuild reuses the same session working document instead of creating retry Parts.

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
      ├─ patterns.md
      └─ strategy.md
```

The Skill describes:

- supported DSL syntax;
- modeling strategy;
- efficient MCP call order;
- topology-query rules;
- deterministic verification gates;
- four-view visual verification;
- retry and repair discipline;
- current capability boundaries.

External AI clients should load the Skill instead of guessing InventorModel syntax or tool behavior.

## Modeling capability

### Sketches

- point
- line
- circle
- exact circular arc
- ellipse
- rectangle and centered rectangle
- slot
- polygon
- spline
- common geometric constraints
- driving dimensions

### Part features

- extrude with positive, negative, or symmetric direction
- revolve with global-axis or sketch-line axis
- sweep
- loft
- drilled hole with positive or negative direction
- selective fillet
- selective chamfer
- shell
- rectangular pattern
- circular pattern
- mirror

### Local edits

The `modify` MCP tool accepts small conversational deltas:

```text
set width = 120
edit hole1 diameter 12
edit extrude1 depth 25
edit fillet1 radius 4
suppress fillet1
unsuppress fillet1
delete hole1
```

`edit <feature> <property> <value>` uses the same safe in-place updater as incremental `build`. Unsupported topology-changing properties are rejected with a request to submit the complete target model through `build`.

Builds and local edits execute inside Inventor Transactions so failed operations can roll back cleanly.

## Verification

`build` and `modify` return structured inspection directly. The summary includes:

- body count;
- sketch and feature counts;
- overall envelope;
- parameters;
- sketch constraint state;
- feature health;
- feature tree.

Use `geometry` only when current edge/face indexes are actually needed.

Use `render` after deterministic checks pass. The default verification set is:

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

Each MCP process creates an isolated session:

```text
Workspace\Sessions\YYYYMMDD\mcp-HHmmss-xxxxxxxx\
├─ renders
├─ scripts
├─ output
└─ temp
```

The effective `.ivmodel` source and intermediate artifacts stay inside the workspace. A final IPT is written elsewhere only when the MCP caller explicitly supplies a destination.

Runtime diagnostics are written to:

```text
%USERPROFILE%\Documents\InventorModel\Logs\runtime.log
```

## Repository layout

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

- **InventorModel.Core** — DSL, expressions, validation, workspace, and shared runtime infrastructure.
- **InventorModel.Inventor** — native Inventor execution, inspection, and rendering.
- **InventorModel.Mcp** — standalone MCP server and the only runtime entry point.
- **InventorModel.Core.Tests** — parser, validator, semantic diff, and rebuild-planner tests.
- **InventorModel.Inventor.Tests** — opt-in live Inventor lifecycle and incremental-update tests.

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

## Build output

```text
bin\x64\<Configuration>\
├─ InventorModel.exe
├─ mcp.manifest.json
└─ Skills\
```

`InventorModel.exe` contains the .NET runtime and managed application dependencies. The final package keeps only `InventorModel.exe` and `mcp.manifest.json` at the top level; the external `Skills/` directory remains readable by AI clients.

There is no Inventor Addin installation step, and the target workstation does not need a separate .NET runtime installation. Autodesk Inventor itself remains an external prerequisite.

## Design principles

- MCP and Skills only.
- No Inventor Addin or embedded chat.
- No duplicated model representation.
- Native editable Inventor features.
- One working Part per MCP session.
- Small, stable, coarse-grained tool surface.
- Deterministic inspection before visual verification.
- Bounded topology queries.
- No blind repeated rebuilds.
- Explicit capability limits instead of fabricated geometry.
- Complete diagnostics instead of silent failures.

## Current limitations

- Part modeling only.
- Face/edge indexes are revision-local rather than persistent identities.
- Arbitrary datum planes and axes beyond current base-axis and sketch-line support are limited.
- Advanced hole variants and native thread features are not yet complete.
- Complex structural edits may require a complete rebuild of the same working Part.

## Documentation

- [DSL](docs/DSL.md)
- [Architecture](docs/ARCHITECTURE.md)
- [Roadmap](docs/ROADMAP.md)
- [InventorModel Skill](Skills/inventor-model/SKILL.md)

## Open source and commercial use

InventorModel is open source under the **Apache License 2.0**.

Apache-2.0 permits commercial use, modification, and redistribution subject to its license terms. The open-source core can therefore be used in commercial engineering workflows and products.

Commercial products, enterprise deployment, integration, support, custom modeling capabilities, and other services can be offered separately without changing the open-source status of InventorModel.

See [LICENSE](LICENSE) for the complete terms.

Copyright 2026 zly258.
