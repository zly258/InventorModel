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
fillet <name> edges <i1,i2,...|all> radius <expression>
```

Use `geometry` immediately before selecting edges. Edge indexes are 1-based and belong only to the current topology revision. Prefer an explicit comma-separated set such as `edges 1,4,7`. Use `edges all` only when the design truly requires every edge to be rounded.

## Chamfer

```text
chamfer <name> edges <i1,i2,...|all> distance <expression>
```

As with fillet, use edge indexes returned by `geometry` for the current model revision. Do not guess or reuse indexes after a topology-changing operation.

## Shell

```text
shell <name> faces <top|bottom|right|left|front|back|index:n> thickness <expression>
```

Exactly one planar face is removed and shell direction is inward. Directional names choose the outermost planar face matching that normal. For stepped or ambiguous geometry, use `geometry` and an explicit `index:n` selector.

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

Do not claim native support for threaded/tapped holes, countersink/counterbore variants, ribs, draft, persistent face/edge IDs, arbitrary datum geometry, multi-body selectors, or feature-specific face identity. Selective fillet/chamfer and indexed planar faces are supported with **revision-local 1-based indexes** from `geometry`; those indexes are not persistent across topology changes.
