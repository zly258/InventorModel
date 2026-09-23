# Construction Strategy and Invariants

Before generating `.ivmodel` source, determine the mechanical construction strategy and expected invariants. Do not jump straight into syntax without selecting the primary strategy.

## 1. Strategy Priority by Part Class

| Part Class | Primary Construction | Strategy Rules |
| --- | --- | --- |
| **Rotational / Turned part** (shaft, flange, bushing) | Single closed profile + `revolve` | Draw the half cross-section profile on `XY` or `XZ`. Use the centerline `axis line:n` or global axis. **Never stack multiple cylinders and join them.** |
| **Prismatic machined part** (bracket, block, mount) | Base `extrude` + secondary cuts/holes | Form the primary solid envelope first. Add cut extrusions and drilled holes sequentially on planar faces. |
| **Plate / Cover** | `centerrect` + `direction symmetric` | Center geometry on the sketch plane where possible for symmetric work planes and easy mirroring. |
| **Repeated features** (bolt circle, hole grid) | Single native seed feature + `pattern` | Create exactly one seed hole/cut, then use `pattern_rect` or `pattern_circular`. Do not place multiple individual holes manually in a sketch. |
| **Hollow housing / Tank** | Solid envelope + `shell` | Create the entire outer solid shape first, then remove the open planar face with `shell faces <side> thickness <t>`. |
| **Finishing features** (fillets, chamfers) | Dedicated final finishing step | **Always apply fillets and chamfers last.** Finishing operations alter topology. Never place intermediate fillets between structural features. |
| **Complex outline** | Single closed sketch profile | Combine lines and arcs within one sketch rather than boolean-joining multiple primitive blocks. |

---

## 2. ModelIntent & Expected Invariants

Before writing DSL, formulate the internal acceptance target (mental contract):

```text
Part Class: Rotational
Primary Construction: Revolve
Expected Body Count: 1
Expected Envelope: X ~ 120 mm, Y ~ 36 mm, Z ~ 36 mm
Expected Key Features: 3 shaft steps, 1 keyway cut, 2 chamfers
```

### Verification Against Invariants
- When `build` returns `inspection`, compare the returned summary (`bodyCount`, `sizeMm`, `featureCount`) directly against your expected invariants.
- If `sizeMm` or `bodyCount` deviates from the invariants, identify the topological or dimensional cause immediately.
- Do not accept a model simply because "Inventor built without errors"; it must satisfy the expected invariants.

---

## 3. Finishing & Topology Discipline

1. **Avoid Guessing Topology**:
   Never guess edge or face indexes. Finishing operations (`fillet`, `chamfer`, indexed `shell`) require real 1-based indexes from the active revision.
2. **Deterministic Query with Filters**:
   When selecting edges for finishing, call `geometry` with filters (e.g. `entity="edge"`, `curveType="circle"`, `nearZ=40`). This narrows candidates deterministically instead of parsing dozens of unindexed lines.
3. **Single Finishing Pass**:
   Apply all selective edge finishing in one final build revision to avoid topological invalidation between incremental finishes.
