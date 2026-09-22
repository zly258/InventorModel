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

Keep the scope on Part modeling and strengthen correctness before adding more product domains.

### Modeling correctness

- persistent semantic face/edge references instead of directional first-body selection;
- selective fillet and chamfer;
- explicit work-plane / work-axis / datum creation;
- countersink and counterbore holes;
- native thread support;
- draft and other common mechanical finishing features;
- stronger multi-body semantics.

### Parametric editing

- typed parameter semantics for length / angle / scalar / integer values;
- richer sketch edit commands;
- persistent source-to-sketch/feature mapping;
- safer structural patching before falling back to a complete rebuild.

### AI verification

- keep `inspect` structured and machine-readable;
- expand verification diagnostics beyond overall bounds;
- continue using deterministic front/top/right/isometric renders;
- improve model-repair decisions from inspect + render rather than repeated blind rebuilds.

### Reliability

- keep context compaction demand-driven;
- keep Tool Call batches protocol-consistent;
- keep UI/COM cleanup failures observable through runtime diagnostics;
- add Inventor-backed integration verification for every shipped example.

Do not expand into Assembly, Drawing, Sheet Metal, Weldment, Frame, Tube & Pipe or CAM until the Part loop is reliable.
