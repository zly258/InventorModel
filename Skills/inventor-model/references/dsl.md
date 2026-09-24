# DSL fundamentals

## File structure

A normal model is ordered as:

```text
part <name>
param <name> = <expression>

sketch <name> on <plane-or-face>
  ...
end

<feature> ...
```

Lines beginning with `#`, or text after `#` on a line, are comments. Quoted tokens are accepted by the tokenizer, but simple identifier names are preferred.

## Parameters and expressions

Lengths are interpreted in millimeters. Angles are interpreted in degrees by angle-consuming commands. Expressions support `+ - * / ( )` and may reference parameters defined earlier.

```text
param width = 120
param half = width / 2
param draftAngle = 30 deg
```

Use an explicit `deg` suffix when a user parameter represents an angle so the native Inventor parameter is created with angle units.

Do not use functions, powers, conditionals, arrays, or undefined variables; the current expression evaluator does not implement them.

## Names

Top-level parameter, sketch, and feature names share one uniqueness check. Reusing one of those names causes a duplicate-name failure. Sketch entity names are local to a sketch.

Prefer short stable names such as `base`, `body`, `mountHole`, and `boltPattern`.

## Sketch planes

Base planes are `XY`, `XZ`, and `YZ`.

A sketch or hole can also use a directional planar face selector or an explicit revision-local face index:

```text
face:<label>:top
face:<label>:bottom
face:<label>:right
face:<label>:left
face:<label>:front
face:<label>:back
face:index:<n>
```

Example:

```text
sketch topProfile on face:body:top
```

Directional selectors resolve the **outermost** planar face on the first solid body whose normal matches the requested side; the middle label is descriptive and is not a persistent feature identity. For stepped or ambiguous geometry, call `geometry` and use `face:index:<n>`. Face indexes are valid only for that exact topology revision and must be queried again after topology-changing operations.

## Feature operations

For features that support an operation, use `join`, `cut`, or `new`. If omitted, the implementation defaults to `join`.

## Local modify commands

The `modify` MCP tool accepts these small deltas:

```text
set <parameter> = <expression>
edit <feature> <property> <value>
suppress <feature>
unsuppress <feature>
delete <feature>
```

Examples:

```text
set width = 120
edit hole1 diameter 12
edit extrude1 depth 25
edit fillet1 radius 4
```

`set`, `suppress`, `unsuppress`, and `delete` are also valid standalone DSL edit statements. The `edit <feature> ...` form is a `modify`-tool command rather than a complete-model DSL statement.

Feature-property edits use the same safe in-place updater as incremental `build`. Supported cases include extrude depth/direction, revolve angle, hole diameter/depth, fillet radius, chamfer distance, shell thickness, pattern count/spacing/angle, and mirror plane. Topology-changing edits such as replacing a profile, changing selected fillet edges, or changing a pattern source must be submitted as a complete target model through `build`.
