# InventorModel Roadmap

## Product direction

InventorModel remains a narrow **MCP + Skills Part-modeling backend**.

Do not reintroduce:

- Inventor Addin UI;
- embedded AI chat;
- provider configuration;
- conversation history;
- standalone CLI;
- Assembly / Drawing / Sheet Metal product scope.

AI clients and future engineering workbenches should consume InventorModel through MCP.

## Current foundation

### Runtime

- ordered DSL parser and AST;
- arithmetic parameters;
- native Inventor parameters;
- Inventor 2023 connection with visible automatic startup;
- one owned working Part per MCP session;
- transaction-protected execution and rollback;
- semantic source diff and working-model bindings;
- standalone MCP server;
- Documents-based MCP workspace;
- canonical Skill package.

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
- common geometric constraints;
- driving dimensions.

### Part features

- extrude with positive / negative / symmetric direction;
- revolve with global-axis or sketch-line axis;
- sweep;
- loft;
- drilled hole with positive / negative direction;
- selective fillet;
- selective chamfer;
- shell;
- rectangular pattern with configurable axes;
- circular pattern;
- mirror.

### Editing and verification

- parameter set and parameter-only in-place build updates;
- common feature property in-place updates;
- dependency-safe local structural suffix rebuilds;
- suppress / unsuppress;
- feature delete;
- deterministic inspection;
- sketch constraint summary;
- feature health;
- bounded topology queries;
- indexed planar-face selection;
- four-view PNG rendering;
- one working Part per MCP session;
- failed build retries reuse the same working Part;
- explicit `start_inventor` and `new_part` MCP lifecycle controls.

## Validation

Keep every shipped example valid under `ModelValidator`.

Inventor-backed validation should cover:

- every example builds successfully;
- expected body count;
- expected envelope;
- no unhealthy feature;
- rendering succeeds;
- final IPT can be saved.

## Next priorities

### Modeling correctness

- persistent semantic face/edge references beyond revision-local indexes;
- explicit work-plane / work-axis / datum creation;
- countersink / counterbore holes;
- native thread support;
- draft;
- stronger multi-body semantics.

### Parametric editing

- typed length / angle / scalar / integer parameters;
- richer sketch-entity in-place edits;
- stronger dependency graph metadata beyond ordered suffix planning;
- persistent semantic topology references across structural revisions;
- broader in-place editing for advanced feature definitions.

### MCP quality

- keep the tool surface small and stable;
- keep `build` and `modify` self-describing;
- keep topology payloads bounded;
- keep render payloads bounded;
- improve error codes and machine-readable diagnostics;
- extend MCP self-test coverage beyond the current live Inventor lifecycle tests;
- verify protocol compatibility without adding client-specific behavior.

### Skills quality

- keep SKILL.md concise;
- keep detailed syntax in references;
- add validated modeling patterns only;
- keep retry rules explicit;
- keep capability boundaries accurate;
- never document a feature before the implementation and examples support it.

## Scope guard

Do not expand into Assembly, Drawing, Sheet Metal, Weldment, Frame, Tube & Pipe, or CAM until Part modeling is reliable.

Do not rebuild an embedded AI application inside this repository. InventorModel is the modeling backend.
