# InventorModel Architecture

## Product boundary

InventorModel solves one problem:

> Provide a compact, deterministic MCP backend for creating and modifying native Autodesk Inventor Part models.

The project does not include an Inventor Addin, chat UI, LLM provider, Agent runtime, or CLI.

External AI clients supply reasoning and conversation. InventorModel supplies:

- one standalone MCP server;
- one canonical Skill package;
- one compact `.ivmodel` representation;
- native Autodesk Inventor execution.

## System shape

```text
External Agent / Workbench
        │
        ├─ reads Skills/inventor-model
        │
        └─ MCP stdio
             │
             ▼
      InventorModel.Mcp
             │
       ┌─────┴─────┐
       ▼           ▼
      Core      Inventor
       │           │
       └─────┬─────┘
             ▼
     Autodesk Inventor
```

## Single model representation

The only persistent model input is `.ivmodel`.

```text
.ivmodel DSL
   ↓
Parser
   ↓
AST
   ↓
Inventor executor
   ↓
native Sketch / PartFeature tree
```

JSON is MCP transport only. It is not a second whole-model format.

## Layers

### Core

Inventor-independent code:

- DSL tokenizer / parser;
- ordered statements;
- expression evaluation;
- validation;
- shared runtime paths;
- MCP workspace support;
- diagnostics.

### Inventor

All Autodesk API work:

- Inventor connection;
- Part document lifecycle;
- sketch creation;
- constraints and dimensions;
- Part features;
- transactions;
- semantic / indexed geometry selection;
- inspection;
- four-view rendering.

### MCP

The only runtime entry point:

- MCP initialization and tool discovery;
- one working Part per server session;
- `validate`;
- `status`;
- `build`;
- `modify`;
- `inspect`;
- `geometry`;
- `render`;
- `save`.

The MCP layer contains orchestration and transport only. Modeling rules belong in Core/Inventor and the Skill package.

### Skills

Skills are the AI operating contract. They define:

- what syntax exists;
- what tool order is efficient;
- when topology lookup is required;
- how to verify a result;
- how many structural retries are reasonable;
- what the implementation does not support.

## Execution model

Statements execute in source order.

A script can therefore:

1. create parameters;
2. create a sketch;
3. create the base feature;
4. sketch on a resulting face;
5. add dependent features;
6. add finishing features.

Grouping all sketches before all features is forbidden because later sketches can depend on earlier geometry.

## Transactions

A build runs inside one Inventor Transaction:

```text
begin
→ execute ordered statements
→ update
→ commit
```

Any exception aborts the transaction and restores the previous valid state.

A structural rebuild reuses the session working Part and replaces generated model state inside that same document.

## References and topology

InventorModel supports:

- stable script names for parameters, sketches, and features;
- base planes XY / XZ / YZ;
- directional outer faces;
- revision-local indexed planar faces;
- global axes X / Y / Z;
- sketch-line revolve axes;
- revision-local edge indexes for selective finishing.

Topology indexes are short-lived. Query them with `geometry` for the current revision and query again after topology-changing operations.

## Verification

`build` and `modify` return structured inspection so clients should not immediately spend another tool call on `inspect`.

Verification data includes:

- bodies;
- sketches;
- features;
- envelope;
- parameters;
- sketch constraints;
- feature health;
- feature tree.

Use `geometry` only for exact current topology.

Use `render` once deterministic checks are plausible. The renderer produces front, top, right, and isometric PNGs and restores the user's previous viewport state afterward.

## Workspace

All writable runtime data uses one visible root:

```text
%USERPROFILE%\Documents\InventorModel
├─ Workspace
└─ Logs
```

Each MCP process creates:

```text
Workspace\Sessions\YYYYMMDD\mcp-HHmmss-xxxxxxxx\
├─ renders
├─ scripts
├─ output
└─ temp
```

No AppData settings, Addin deployment folders, conversation history, or embedded-chat files are part of InventorModel.

## Build products

The source solution contains only:

```text
InventorModel.Core
InventorModel.Inventor
InventorModel.Mcp
InventorModel.Core.Tests
```

The public runtime product is:

```text
InventorModel.Mcp.exe
+ required DLLs
+ Skills/
```

## Technical baseline

- Windows x64
- Autodesk Inventor 2023
- C#
- .NET Framework 4.8
- Autodesk Inventor Interop
- MCP over stdio
