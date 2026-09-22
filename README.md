# InventorModel

InventorModel is a focused AI-native parametric **Part modeling system for Autodesk Inventor 2023**.

It combines the modeling core, Inventor Addin, integrated AI chat, MCP server, CLI, Skills, examples, and tests in a single repository.

## Product flow

```text
Text / image / engineering drawing
                ↓
              AI Chat
                ↓
           .imodel DSL
                ↓
   native Inventor sketches/features
                ↓
      inspect + four-view verification
                ↓
        conversational local edits
                ↓
            editable IPT
```

InventorModel uses one modeling representation: **`.imodel`**. It does not introduce a second whole-model JSON layer.

## Current scope

The current version focuses on native Inventor **Part** modeling.

### Sketch

Supported sketch vocabulary includes:

- point
- line
- circle
- exact arc
- ellipse
- rectangle
- centered rectangle
- slot
- polygon
- spline
- common geometric constraints
- dimensions

### Features

Supported Part features include:

- extrude
- revolve
- sweep
- loft
- hole
- fillet
- chamfer
- shell
- rectangular pattern
- circular pattern
- mirror

### Local edits

Existing native models can be modified without rebuilding the entire Part:

```text
set width = 120
suppress fillet1
unsuppress fillet1
delete hole1
```

Script execution and local edits run inside Inventor transactions.

## Integrated AI Chat

The Inventor Addin provides an **AI Chat** entry for conversational modeling.

The embedded chat supports:

- OpenAI-compatible streaming APIs
- Ollama and compatible local endpoints
- compatible cloud model endpoints
- text prompts
- engineering image attachments
- persistent endpoint/model settings
- multi-round tool calling
- local conversation history
- Skills guidance
- direct InventorModel tool execution

The embedded agent uses the same modeling contract as external MCP clients.

For a new model, AI generates `.imodel` and calls `build`.

For small corrections, AI should prefer `modify` instead of regenerating the complete model.

## MCP tools

InventorModel MCP currently exposes:

| Tool | Purpose |
| --- | --- |
| `status` | Check Inventor connection and active Part |
| `build` | Build a native editable Part from `.imodel` source or file |
| `modify` | Apply a local parameter or feature edit |
| `inspect` | Inspect bounds, parameters, and feature tree |
| `render` | Render front, top, right, and isometric PNG views |
| `save` | Save the active Part as an editable IPT |

A typical AI workflow is:

```text
understand
  ↓
plan
  ↓
build
  ↓
inspect
  ↓
render four views
  ↓
local correction when necessary
  ↓
save IPT
```

## Inventor Addin

The Part ribbon contains:

- **AI Chat**
- **Build Script**
- **Four Views**

The Addin build also deploys the required runtime DLLs and the `Skills` directory into the current user's Inventor 2023 Addins directory.

## AI configuration

AI settings are stored at:

```text
%APPDATA%\InventorModel\ai-settings.json
```

Default endpoint:

```text
http://127.0.0.1:11434/v1
```

The following values can be configured from the AI Chat window:

- Base URL
- API key
- model name
- temperature

Conversation history is stored under:

```text
%LOCALAPPDATA%\InventorModel\History
```

## Repository structure

```text
InventorModel
├─ src
│  ├─ InventorModel.Core
│  ├─ InventorModel.Inventor
│  ├─ InventorModel.Addin
│  ├─ InventorModel.Cli
│  └─ InventorModel.Mcp
├─ Skills
├─ examples
├─ tests
└─ docs
```

### Projects

- **InventorModel.Core** — `.imodel` DSL and expression/model definitions
- **InventorModel.Inventor** — native Autodesk Inventor execution layer
- **InventorModel.Addin** — Inventor integration and embedded AI Chat
- **InventorModel.Cli** — command-line interface
- **InventorModel.Mcp** — MCP server for external AI clients
- **InventorModel.Core.Tests** — DSL/core tests

## Examples

The repository currently contains eight `.imodel` examples:

1. plate
2. flange
3. shaft
4. bracket
5. sweep
6. loft
7. shell
8. constraints

See the [examples](examples) directory.

## Build

Requirements:

- Windows x64
- Autodesk Inventor 2023
- .NET SDK capable of building the solution
- Autodesk Inventor Interop assemblies

Build and test:

```powershell
.\build.ps1 -Clean
```

The default Inventor installation root is:

```text
C:\Program Files\Autodesk\Inventor 2023
```

It can be overridden through the MSBuild `InventorInstallRoot` property when necessary.

## Design principles

InventorModel intentionally keeps the architecture small:

- one Part-modeling product
- one `.imodel` modeling representation
- one native Inventor execution layer
- one MCP surface
- one embedded AI conversation entry
- parameterized and editable native Inventor output
- inspect and visual verification before declaring completion

The project currently focuses on **Part modeling** rather than trying to cover Assembly, Drawing, Sheet Metal, CAM, and every Inventor API at once.

More advanced Part capabilities such as richer topology references, advanced holes and threads, ribs, draft, and arbitrary work geometry can be expanded on top of this foundation.

## Documentation

- [DSL](docs/DSL.md)
- [Architecture](docs/ARCHITECTURE.md)
- [Roadmap](docs/ROADMAP.md)
- [AI Skill](Skills/SKILL.md)
