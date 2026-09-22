# InventorModel Architecture

## Product boundary

InventorModel v0.1 solves one problem:

> Create and modify native Autodesk Inventor Part models from a compact script suitable for AI generation.

It is not an Inventor replacement and it does not wrap every Inventor API.

## Single external representation

The only persistent model input is `.imodel`.

```text
.imodel DSL
   ↓
Parser
   ↓
in-memory AST
   ↓
Inventor executor
   ↓
native Sketch / PartFeature tree
```

Transport formats are not model formats. There is no second whole-model representation and no conversion pipeline that the AI has to reason about.

## Layers

### Core

Inventor-independent:

- DSL tokenizer/parser;
- ordered statements;
- parameter expression evaluator;
- AST and diagnostics;
- shared AI workspace path policy.

### Inventor

Owns all Autodesk API work:

- application/document lifecycle;
- sketch creation;
- constraints/dimensions;
- Part features;
- semantic base-plane/face selection;
- transactions;
- inspection;
- four-view rendering.

### Addin

Thin Inventor UI entry:

- AI建模 ribbon tab with only AI Chat and AI configuration;
- Markdown rendering;
- clipboard / drag-and-drop image attachment;
- history management and export;
- one shared WPF theme for stable control sizing, spacing, and padding.

No modeling rules belong in Ribbon code.

### CLI

Automation entry for local workflows:

```text
build <script.imodel> <output.ipt>
inspect
render <directory>
```

The CLI and Addin call the same execution services.

## Execution model

Statements execute in source order. This is essential.

A script can therefore:

1. create a base sketch;
2. extrude it;
3. create a new sketch on the resulting top face;
4. add holes or a loft;
5. pattern or finish the resulting feature.

Grouping all sketches before all features is explicitly forbidden.

## Transactions

A build runs in one Inventor Transaction.

```text
begin
→ execute statements
→ update
→ commit
```

Any exception aborts the transaction and returns the document to the previous valid state.

## References

v0.1 uses:

- stable script names for sketches/features;
- base planes: XY/XZ/YZ;
- semantic outer-body faces: top/bottom/left/right/front/back;
- named global axes X/Y/Z.

Raw transient face/edge indices are not part of the DSL.

Future work may add stronger persistent reference mapping, but it must remain hidden behind semantic references.

## Parameterization

User parameters are created as native Inventor parameters. Feature distances and angles retain parameter expressions whenever the DSL passes a parameter name.

Sketch dimensions may also refer to native parameters. This is preferred for geometry expected to change conversationally.

## Verification

AI or a user should not judge a build only from the feature tree. The verification surface combines:

- part/body count;
- feature tree;
- parameter expressions;
- bounding box;
- front/top/right/isometric images.

## AI workspace

Internal AI files are isolated from user project folders and arbitrary temporary locations.

```text
%LOCALAPPDATA%\\InventorModel\\AI\\sessions\\YYYYMMDD\\<session>\\
├─ attachments
├─ renders
├─ scripts
├─ output
├─ temp
└─ history.md
```

Embedded AI always writes scripts, pasted/selected images, verification renders, and default outputs inside this workspace. MCP uses the same default workspace policy.

## Technical baseline

- Autodesk Inventor 2023
- Windows x64
- C#
- .NET Framework 4.8 across the solution
- Autodesk Inventor Interop

Existing `InventorMcp@main` code is reference material for proven Inventor API usage only; its previous model architecture is not inherited.
