# Feature syntax

## Extrude

```text
extrude <name> from <sketch> depth <expression> [join|cut|new]
extrude <name> from <sketch> through [join|cut|new]
```

The extent direction is positive. `through` uses a positive through-all extent.

## Revolve

```text
revolve <name> from <sketch> axis <X|Y|Z> [angle <expression>] [join|cut|new]
```

The default angle is 360 degrees. The axis is one of the Inventor base work axes, not an arbitrary sketch line.

## Sweep

```text
sweep <name> profile <sectionSketch> path <pathSketch> [join|cut|new]
```

The path is built from the path sketch's lines, arcs, and splines. Keep the path connected and valid for Inventor's specified-path sweep.

## Loft

```text
loft <name> from <section1> <section2> [section3 ...] [join|cut|new]
```

At least two section sketches are required.

## Hole

```text
hole <name> on <plane-or-face> at <x> <y> diameter <expression> through
hole <name> on <plane-or-face> at <x> <y> diameter <expression> depth <expression>
```

The parser also accepts comma-separated coordinates such as `at 20,15`, but `at 20 15` is preferred for readability. Holes are drilled features and remove material; do not add `cut`.

## Fillet

```text
fillet <name> radius <expression>
```

Current limitation: the implementation fillets **all edges of the first solid body**. It does not implement edge selection even though the generic feature parser recognizes an `edges` keyword. Do not emit an `edges` selector.

## Chamfer

```text
chamfer <name> distance <expression>
```

Current limitation: the implementation chamfers **all edges of the first solid body**. Do not emit edge selectors.

## Shell

```text
shell <name> faces <top|bottom|right|left|front|back> thickness <expression>
```

Current limitation: exactly one directional planar face is removed and shell direction is inward. Use the bare side name here, not `face:...`.

## Rectangular pattern

```text
pattern_rect <name> source <feature> count <nx> [ny] spacing <sx> [sy]
```

The current implementation always uses the global X direction for the first direction and global Y for the optional second direction.

```text
pattern_rect holes source h1 count 4 spacing 30
pattern_rect holes source h1 count 4 2 spacing 30 40
```

## Circular pattern

```text
pattern_circular <name> source <feature> axis <X|Y|Z> count <n> [angle <expression>]
```

The default angle is 360 degrees.

## Mirror

```text
mirror <name> source <feature> plane <XY|XZ|YZ>
```

Use a base work plane. A face selector is not a safe mirror-plane input in the current implementation.

## Current capability boundary

Do not claim native support for threaded/tapped holes, countersink/counterbore variants, ribs, draft, face/edge persistent IDs, arbitrary datum geometry, multi-body selectors, feature-specific face identity, or selective fillet/chamfer. Build requested geometry from supported primitives where practical; otherwise state that the requested operation needs an implementation extension.
