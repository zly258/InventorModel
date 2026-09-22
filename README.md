# InventorModel

InventorModel is a focused AI-native parametric **Part** modeling system for Autodesk Inventor 2023.

The first release intentionally has a narrow product boundary: Part modeling only. It does not try to automate Assembly, Drawing, Sheet Metal, CAM or every Inventor API.

## Product loop

```text
Text / image / engineering drawing
              ↓
             AI
              ↓
       .imodel DSL
              ↓
 native Inventor sketches/features
              ↓
 inspect + four-view verification
              ↓
 conversational edit
              ↓
        editable IPT
```

There is one modeling representation: `.imodel`. There is no second whole-model JSON format.

## v0.1 foundation

Sketch commands include point, line, circle, exact arc, ellipse, rectangle, centered rectangle, slot, polygon, spline, common geometric constraints and dimensions.

Part features include extrude, revolve, sweep, loft, drilled hole, fillet, chamfer, shell, rectangular/circular pattern and mirror.

The DSL also supports direct conversational edits on an existing native model:

```text
set width = 120
suppress fillet1
unsuppress fillet1
delete hole1
```

The executor runs each script/edit inside an Inventor transaction.

## Interfaces

- Inventor Addin: Build Script + Four Views
- CLI: build / inspect / render
- MCP executable: status / build / modify / inspect / render / save
- Skills: concise AI modeling workflow
- Examples: eight `.imodel` samples

## Technology

- Autodesk Inventor 2023
- Windows x64
- C#
- .NET Framework 4.8 for Inventor integration
- .NET Standard 2.0 for the DSL core
- native Autodesk Inventor Interop API

## Build

```powershell
.\build.ps1 -Clean
```

The Addin is deployed to the current user's Inventor 2023 Addins directory.

## v0.1 boundary

The initial release provides the general Part-modeling foundation needed to validate AI modeling quality. Features that require more semantic selection work—advanced holes/threads, ribs, draft, persistent topology editing, arbitrary work geometry—remain subsequent Part-modeling improvements rather than pretending to be complete in v0.1.

See `docs/DSL.md`, `docs/ARCHITECTURE.md`, `docs/ROADMAP.md`, and `Skills/SKILL.md`.
