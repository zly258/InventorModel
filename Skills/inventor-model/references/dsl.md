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

## Local edit statements

The complete supported edit set is:

```text
set <parameter> = <expression>
suppress <feature>
unsuppress <feature>
delete <feature>
```

`set` only works on an existing Inventor user parameter. Local edits cannot rewrite a sketch, change a feature's arguments, reorder features, or add a new feature; use a complete rebuild for those changes.
