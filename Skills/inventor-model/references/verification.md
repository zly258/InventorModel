# Verification and repair

## Deterministic invariant gates

Always evaluate the summary inspection returned by `build` or `modify` against the pre-established Expected Invariants ([strategy.md](strategy.md)):

- `bodyCount`: Must match target (almost always 1). If > 1, unexpected disconnected bodies exist.
- `sizeMm`: Compare X, Y, Z bounding dimensions directly with drawing dimensions. A build that succeeds but has wrong dimensions is a failure.
- `unhealthyFeatureCount`: Hard failure gate. Must be 0 before visual acceptance.
- `underConstrainedSketchCount`: Parametric quality warning. Investigate if fully constrained sketch is required, but do not rebuild solely for this if geometry matches.
- Detailed inspection: Call `inspect(detail="parameters"|"sketches"|"features")` only when summary counters signal a specific internal failure.

## Visual verification (`render`)

Run `render` only after deterministic inspection gates pass:

- **Intermediate checks**: If verifying a specific cut or hole orientation, render a fast subset:
  ```json
  {"views": "front,iso", "size": 512}
  ```
- **Final acceptance**: Render the full four-view set (`"front,top,right,iso"`) at default 640 px:
  Check silhouette, hole/cut penetration, boss proportions, and missing features.

## Structured error recovery

When an MCP operation fails, consume the structured error payload:

| Error Code | Stage | Recommended Recovery |
| --- | --- | --- |
| `dsl_validation` | `dsl_validation` | Check variable names, syntax types, and undefined parameter references. |
| `dsl_parse` | `dsl_parse` | Correct plane, axis, or keyword spellings per syntax references. |
| `selector_not_found` | `inventor_feature` | Edge/face index invalid for current revision. Call `geometry` with filters (`entity`, `nearZ`, etc.) to get fresh indexes. |
| `feature_failed` | `inventor_feature` | Ensure profile is closed, non-self-intersecting, and hole/fillet dimensions fit the solid. |
| `document_invalid` | `inventor_session` | Solid body missing. Check `status` and construct base solid feature first. |
| `save_failed` | `file_io` | Check destination path permissions or provide `overwrite: true`. |

## Repair hierarchy

Prefer the smallest corrective action:

1. **Parameter error** -> call `modify(command="set <name> = <val>")`.
2. **Finishing feature failure** -> query `geometry` with filters; rebuild once with corrected edge indexes.
3. **Structural / topology error** -> rebuild once with a materially corrected `.ivmodel` script in the same working Part.
4. **Stopping rule**: Do not execute open-ended rebuild loops. If the corrected model still fails after one structural rebuild, report the exact mismatch or capability boundary.
