# Sketch syntax

A sketch begins with `sketch <name> on <plane>` and ends with `end`.

## Entities

| Entity | Syntax | Notes |
| --- | --- | --- |
| point | `point <name> <x> <y>` | Named point. |
| line | `line <name> <x1> <y1> <x2> <y2>` | Named line. |
| circle | `circle <name> <cx> <cy> <radius>` | Radius is created as a driving dimension. |
| arc | `arc <name> <cx> <cy> <radius> <startDeg> <endDeg>` | Exact circular arc; radius is driven. |
| ellipse | `ellipse <name> <cx> <cy> <majorRadius> <minorRadius>` | Major axis is aligned to sketch X. |
| rect | `rect <x> <y> <width> <height>` | Corner-based rectangle; width and height are driven. |
| centerrect | `centerrect <cx> <cy> <width> <height>` | Centered rectangle; width and height are driven. |
| slot | `slot <name> <cx> <cy> <length> <width> <angleDeg>` | Straight slot; use length >= width. |
| polygon | `polygon <cx> <cy> <radius> <count> [rotationDeg]` | Radius is center-to-vertex. |
| spline | `spline <name> <x1> <y1> <x2> <y2> <x3> <y3> [...]` | Requires at least three points. |

Coordinates and lengths may use valid parameter expressions when they fit in one token, for example `width/2`, `-height/2`, or `radius*2`.

## Geometric constraints

```text
constraint horizontal <line>
constraint vertical <line>
constraint parallel <entity1> <entity2>
constraint perpendicular <entity1> <entity2>
constraint tangent <entity1> <entity2>
constraint concentric <entity1> <entity2>
constraint equal <entity1> <entity2>
constraint coincident <entity1> <entity2>
```

Use only entity combinations accepted by Inventor for the selected constraint.

## Dimensions

```text
dim length <line> <expression>
dim radius <entity> <expression>
dim diameter <entity> <expression>
```

A `circle` or `arc` already receives a driving radius dimension from its creation argument. Avoid adding a second radius or diameter dimension to the same entity because it can over-constrain the sketch.

## Closed profiles

`extrude`, `revolve`, `sweep`, and `loft` create solid profiles with Inventor's `Profiles.AddForSolid()`. The section must form a valid closed region for solid modeling.

## Not supported

Do not emit `project` or `offset`; they are not implemented by the sketch executor. Do not assume trim, extend, construction geometry, arbitrary work points/axes, or arbitrary projected topology are available unless the code is extended first.
