# InventorModel

InventorModel is an AI-assisted, parametric **Autodesk Inventor 2023 Part modeling system**.

It keeps one modeling representation — the compact `.ivmodel` DSL — and converts it directly into native Inventor sketches, parameters, and features. The result remains an editable Inventor `.ipt` model rather than a generated mesh or opaque intermediate format.

## Workflow

```text
Text / image / engineering drawing
                ↓
           Inventor AI Chat
                ↓
             .ivmodel
                ↓
     native Inventor Part model
                ↓
       inspect + four views
                ↓
      conversational correction
                ↓
            editable IPT
```

The project intentionally focuses on **Part modeling**. Assembly, Drawing, Sheet Metal, Frame, CAM, and other Inventor domains are outside the current product boundary.

## Current modeling capability

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

### Features

- extrude with positive / negative / symmetric direction
- revolve with global-axis or sketch-line axis
- sweep
- loft
- drilled hole with positive / negative direction
- fillet
- chamfer
- shell
- rectangular pattern
- circular pattern
- mirror

### Local edits

Small changes can be applied to the current native Part without regenerating everything:

```text
set width = 120
suppress fillet1
unsuppress fillet1
delete hole1
```

Model execution and local edits run inside Inventor transactions.

## Integrated AI Chat

The Inventor Addin contains an AI modeling workspace with:

- OpenAI-compatible streaming chat
- Ollama and compatible local endpoints
- compatible cloud endpoints
- text and engineering-image input
- file picker, drag-and-drop, and Ctrl+V image paste
- Markdown streaming output
- selectable conversation text and partial copy
- formatted Tool Call arguments and results
- multi-round model build / inspect / repair
- four-view visual verification
- local conversation history
- batch history export and deletion
- per-session AI workspace
- runtime diagnostics log

The Ribbon stays intentionally small:

- **AI Chat / AI 对话**
- **AI Settings / AI 配置**

All modeling, inspection, rendering, and saving actions remain Agent tools instead of becoming extra Ribbon buttons.

## Language

UI language and AI response language are configured independently.

### UI language

- Simplified Chinese
- English

The setting applies to AI Chat, History, Settings, and the InventorModel Ribbon.

### AI response language

- Follow UI
- Simplified Chinese
- English

The Agent system prompt explicitly keeps user-facing replies in the configured language even when tool output, diagnostics, code, or Skill references use another language.

DSL keywords, API identifiers, file paths, and code are not translated.

## AI settings

Settings are stored at:

```text
%APPDATA%\InventorModel\ai-settings.json
```

Default endpoint:

```text
http://127.0.0.1:11434/v1
```

| Setting | Purpose |
| --- | --- |
| Base URL | OpenAI-compatible endpoint |
| API Key | Optional for local services |
| Model | Endpoint model name |
| UI language | Chinese or English |
| AI response language | Follow UI, Chinese, or English |
| Temperature | Generation randomness |
| Reasoning | Enable or disable model thinking |
| Max output tokens | 0 uses provider default |
| Max Tool Calls | Per-user-request Agent tool-call limit |
| Context window | 0 = Auto, or enter the real model context size |
| Request timeout | HTTP request timeout in seconds |
| Retry count | Network / 429 / 5xx retries |
| Advanced request parameters | Additional provider-specific top-level JSON parameters |

Advanced parameters are merged into the OpenAI-compatible request. Core fields such as `model`, `messages`, `tools`, `temperature`, and `reasoning_effort` cannot be overridden there.

## Context management

InventorModel separates **full conversation history** from the **active model context**.

Full history is preserved for history viewing and export.

Active context follows these rules:

1. **Auto context mode** does not compress proactively.
2. If the provider reports that the context window was exceeded, older context is compacted and the request is retried once.
3. If a real context-window size is configured, InventorModel estimates whether the next request fits before sending it.
4. Old image payloads are removed first.
5. If more space is needed, older turns and tool results are compacted.
6. Recent turns, recent tool chains, and the latest complete `.ivmodel` source are retained.

This avoids early context loss while still allowing long modeling sessions to continue.

## Agent controls

The Agent has a configurable Tool Call limit per user request.

The limit is checked **before an assistant tool-call batch is committed to the conversation**, so a response cannot leave partially executed / unmatched Tool Calls in the message history.

Reasoning can also be disabled. For compatible OpenAI-style endpoints the request sends:

```json
{
  "reasoning_effort": "none"
}
```

This is useful when response speed is more important than extended reasoning.

## Tools

| Tool | Purpose |
| --- | --- |
| `validate` | Optional dry-run validation; `build` validates internally |
| `status` | Check Inventor, active Part, and AI workspace |
| `build` | Validate + build a native editable Part and return inspection |
| `modify` | Apply a supported local edit |
| `inspect` | Inspect size, parameters, sketch constraints, and feature health |
| `geometry` | Query bounded current edge/face topology for precise finishing |
| `render` | Render front, top, right, and isometric views (640 px default) |
| `save` | Save the active Part as native IPT |

Typical flow:

```text
build (validation + inspection included)
  ↓
geometry (only when edge/face indexes are needed)
  ↓
render once
  ↓
modify or rebuild when necessary
  ↓
save
```

Tool results are authoritative. The Agent should not report modeling success before Inventor confirms the operation.

## AI workspace

Runtime AI files are isolated under:

```text
%LOCALAPPDATA%\InventorModel\AI
```

Each chat or MCP session has its own directory:

```text
AI\sessions\YYYYMMDD\chat-HHmmss-xxxxxxxx\
├─ attachments
├─ renders
├─ scripts
├─ output
├─ temp
└─ history.md
```

A final IPT is written outside the workspace only when an explicit destination is requested.

Runtime diagnostics are written to:

```text
%LOCALAPPDATA%\InventorModel\logs\runtime.log
```

Non-critical UI / COM cleanup failures are logged instead of being silently swallowed. Modeling failures still propagate and Inventor transactions are rolled back.

## Repository

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

- **InventorModel.Core** — DSL, expressions, AI workspace, shared runtime infrastructure
- **InventorModel.Inventor** — native Autodesk Inventor execution
- **InventorModel.Addin** — Ribbon integration, AI Chat, settings, history, Tool execution
- **InventorModel.Cli** — command-line entry point
- **InventorModel.Mcp** — standalone MCP server
- **InventorModel.Core.Tests** — DSL/core tests

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
9. stepped shaft with a sketch-line revolve axis

See the `examples` directory.

## Build

Requirements:

- Windows x64
- Autodesk Inventor 2023
- .NET Framework 4.8
- a .NET SDK capable of building the solution
- Autodesk Inventor Interop assemblies

Build and test:

```powershell
.\build.ps1 -Clean
```

Default Inventor installation:

```text
C:\Program Files\Autodesk\Inventor 2023
```

Override it with `InventorInstallRoot` or `InventorInteropPath` when needed.

Build output is consolidated under:

```text
bin\x64\<Configuration>
```

The Addin build installs the runtime DLLs, `InventorModel.addin`, and Skills into the current user's Inventor 2023 Addins directory.

## Design principles

InventorModel is intentionally narrow:

- one Part-modeling product
- one `.ivmodel` representation
- native editable Inventor output
- small stable Agent tool surface
- validation inside build, with optional dry-run validation
- deterministic inspection returned by build/modify
- one final visual verification after deterministic gates
- local edits when possible
- rebuild only when model structure must change
- complete diagnostics instead of silent failures

## Current limitations

The current implementation still has important Part-modeling limits:

- face/edge topology indexes are revision-local rather than persistent identities
- arbitrary datum planes / axes beyond base axes and sketch-line revolve axes are limited
- advanced hole variants and native thread features are not yet complete
- the parameter model still needs richer unit/type semantics
- complex structural script edits may require a rebuild rather than a local patch

These are the next areas to strengthen before expanding the product into additional Inventor domains.

## Documentation

- [DSL](docs/DSL.md)
- [Architecture](docs/ARCHITECTURE.md)
- [Roadmap](docs/ROADMAP.md)
- [AI Skill](Skills/inventor-model/SKILL.md)

## License

InventorModel is licensed under the [Apache License 2.0](LICENSE).

Copyright 2026 zly258.
