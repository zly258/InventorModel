# InventorModel

InventorModel creates native Autodesk Inventor Part models from a compact `.imodel` DSL.

## Non-negotiable rules

- Write the DSL directly. Do not create a separate model object file.
- Keep important dimensions as `param` values.
- Give reusable sketch entities and features short stable names.
- Build in the same order an engineer would build the feature tree.
- After a meaningful build, inspect the model and render front/top/right/isometric views.
- When the user asks for a correction, patch the responsible parameter or feature instead of regenerating the whole model.

## Minimal example

```text
part Bracket
param width = 100
param thickness = 12

sketch base on XY
  centerrect 0 0 width 60
end
extrude body from base depth thickness join

hole h1 on face:body:top at 20 15 diameter 10 through
```

## Vocabulary

Sketch: `point line circle arc ellipse rect centerrect slot polygon spline constraint dim`.

Features: `extrude revolve sweep loft hole fillet chamfer shell pattern_rect pattern_circular mirror`.

Edits: `set`, `suppress`, `unsuppress`, `delete`.

The preferred loop is: understand → plan → script → build → inspect → four views → local patch.
