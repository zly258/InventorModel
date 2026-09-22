# InventorModel DSL v0.1

The language is intentionally line-oriented and small. There is one modeling representation: `.imodel`.

## Parameters

Lengths are millimeters and angles are degrees. Expressions support `+ - * / ()`.

```text
param width = 100
param half = width / 2
```

## Sketches

```text
sketch base on XY
  point p1 0 0
  line l1 0 0 100 0
  circle c1 50 30 10
  arc a1 0 0 20 0 90
  rect 0 0 100 60
  centerrect 0 0 100 60
  ellipse e1 0 0 30 15
  slot s1 0 0 80 20 0
  polygon 0 0 30 6 30
  spline curve 0 0 20 10 40 0
  constraint horizontal l1
  dim length l1 100
end
```

Base planes are `XY`, `XZ`, and `YZ`. A later sketch can use a semantic face such as `face:body:top`.

## Features

```text
extrude body from base depth 12 join
revolve shaft from profile axis Y angle 360 join
sweep pipe profile section path route join
loft transition from section1 section2 join
hole h1 on face:body:top at 20 20 diameter 10 through
fillet f1 radius 3
chamfer c1 distance 2
shell s1 faces top thickness 2
pattern_rect p1 source h1 count 2 3 spacing 40 30
pattern_circular p2 source h1 axis Z count 6 angle 360
mirror m1 source h1 plane YZ
```

## Conversational edits

```text
set width = 120
suppress fillet1
unsuppress fillet1
delete hole1
```

The implementation parses the script directly into an in-memory AST and executes native Inventor operations. There is no second persistent model representation.
