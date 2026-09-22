# InventorModel Roadmap

## v0.1 objective

Create a first usable Autodesk Inventor Part modeling core driven by one compact custom DSL.

The release boundary is deliberately narrow: **Part modeling only**. The modeling foundation inside that boundary must be broad enough to build ordinary mechanical parts without immediately falling back to raw Inventor API calls.

## v0.1 implemented foundation

### Runtime

- ordered DSL parser and in-memory AST;
- arithmetic parameters;
- native Inventor user parameters;
- Inventor 2023 session bootstrap;
- transaction-protected execution and rollback;
- Addin and CLI entry points.

### Sketch

- point;
- line;
- circle;
- arc;
- ellipse;
- rectangle / centered rectangle;
- slot;
- polygon;
- spline;
- horizontal / vertical / parallel / perpendicular / tangent / concentric / equal / coincident constraints;
- line length, radius and diameter dimensions.

### Part features

- extrude;
- revolve;
- sweep;
- loft;
- drilled hole;
- fillet;
- chamfer;
- shell;
- rectangular pattern;
- circular pattern;
- mirror.

### Editing and verification

- named parameters and features;
- parameter set;
- suppress / unsuppress;
- feature delete;
- model summary;
- feature tree summary;
- bounding-box dimensions;
- front / top / right / isometric PNG rendering.

## v0.1 validation

The repository contains representative `.imodel` examples for:

- plate;
- flange and bolt pattern;
- revolved part;
- bracket and rectangular pattern;
- sweep;
- loft;
- shell;
- constrained sketch.

Validation should be run in Inventor 2023 on Windows x64 with `build.ps1 -Clean` and by opening/building every example.

## Next after v0.1

Only after this base is stable:

- more semantic edge/face selectors instead of broad selectors;
- work-plane / work-axis creation;
- countersink and counterbore variants;
- native thread;
- draft;
- richer sketch edit commands;
- persistent source-to-feature mapping for stronger local script patching;
- thin AI connector if needed.

Do not expand into Assembly, Drawing, Sheet Metal, Weldment, Frame, Tube & Pipe or CAM until the Part loop is reliable.
