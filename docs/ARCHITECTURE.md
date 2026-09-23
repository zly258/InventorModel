# InventorModel Architecture

## Product boundary

InventorModel v0.1 solves one problem:

> Create and modify native Autodesk Inventor Part models from a compact script suitable for AI generation.

It is not an Inventor replacement and it does not wrap every Inventor API.

## Single external representation

The only persistent model input is `.ivmodel`.

```text
.ivmodel DSL
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

- localized AI Modeling ribbon with only AI Chat and AI Settings;
- Chinese / English UI localization;
- independently configurable AI response language;
- Markdown rendering with selectable/copyable conversation text;
- clipboard / drag-and-drop image attachment;
- history management and export;
- structured tool-call traces with formatted JSON;
- context management separated from full exported history;
- provider-specific request settings;
- runtime diagnostics instead of silent non-critical failures;
- one shared WPF theme for stable control sizing, spacing, and padding.

No modeling rules belong in Ribbon code.

### CLI

Automation entry for local workflows:

```text
build <script.ivmodel> <output.ipt>
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
- revision-local indexed planar faces when directional selection is ambiguous;
- named global axes X/Y/Z;
- a sketch-line axis for revolved profiles;
- revision-local edge indexes for selective finishing features.

Topology indexes are deliberately short-lived. They are obtained from `geometry` for the current model revision and must be queried again after topology-changing operations. They are not persistent feature identity.

Future work may add stronger persistent reference mapping while keeping it hidden behind higher-level semantic references.

## Parameterization

User parameters are created as native Inventor parameters. Feature distances and angles retain parameter expressions whenever the DSL passes a parameter name.

Sketch dimensions may also refer to native parameters. This is preferred for geometry expected to change conversationally.

## Verification

AI or a user should not judge a build only from the feature tree. The verification surface combines:

- structured body/sketch/feature counts;
- sketch constraint state and feature health;
- structured feature tree;
- parameter expressions and units;
- bounding-box dimensions;
- bounded revision-local B-Rep topology when precise finishing needs it;
- front/top/right/isometric images.

The four-view renderer temporarily uses shaded-with-edges display for verification and restores the user's previous camera and display mode afterward.

## AI conversation context

The embedded chat keeps a complete transcript for history/export and a separate active model context for inference.

Context-window mode can be either explicit or Auto:

- explicit: the configured real context size is used to reserve output/tool space and predict whether the next request fits;
- Auto: natural-language turns are not summarized proactively. If the provider explicitly reports a context-window overflow, InventorModel compacts older conversation context and retries once.

Model-state payloads are handled more aggressively because they are revision-specific. After a successful build/modify, superseded `inspect`, `geometry`, `render`, older build-result payloads, and rendered verification images are compacted from active inference context. The original user source images and the latest successful complete `.ivmodel` build source remain available.

When full context compaction is still required, old image payloads are removed first, then older turns/tool results are summarized.

Tool calls are surfaced as collapsible trace cards with formatted JSON. The total Tool Call limit is checked before a returned tool batch is committed, preventing unmatched/partially executed tool-call messages.

Reasoning can be disabled with `reasoning_effort: "none"`. Timeout, retry count, output-token limit, response language, context window, and provider-specific top-level JSON parameters are also configurable.

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

## Diagnostics

Expected non-critical UI, Ribbon, COM cleanup, history, and Markdown fallback failures are written to:

```text
%LOCALAPPDATA%\InventorModel\logs\runtime.log
```

Critical modeling failures are not swallowed. They propagate to the caller and abort the active Inventor transaction.

## Technical baseline

- Autodesk Inventor 2023
- Windows x64
- C#
- .NET Framework 4.8 across the solution
- Autodesk Inventor Interop

Existing `InventorMcp@main` code is reference material for proven Inventor API usage only; its previous model architecture is not inherited.
