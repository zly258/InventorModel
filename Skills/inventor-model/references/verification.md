# Verification and repair

## Inspect first

After a meaningful build or edit, first use the inspection already returned by `build` or `modify`. Call `inspect` separately only when the current state is otherwise unclear. Compare:

- `bodyCount` with the intended body count;
- `featureCount` with the expected feature-tree size;
- `underConstrainedSketchCount` — normally zero for finished driving sketches;
- `unhealthyFeatureCount` — must be zero before visual acceptance;
- `sizeMm` with the requested overall envelope;
- `parameters` with the intended driving dimensions;
- detailed sketch/feature entries only when the summary count indicates a problem or the task needs deeper inspection.

A successful build with the wrong envelope is still a modeling error.

## Four-view verification

Run this only after deterministic `inspect` facts are plausible. Visual review is a final shape gate, not an open-ended retry loop.

Use one final `render` after deterministic gates pass. The default 640 px size is normally sufficient; increase it only when small visual details cannot be judged. It generates:

```text
front.png
top.png
right.png
iso.png
```

Check silhouette, hole/pattern placement, major proportions, missing cuts, unintended bodies, and obvious feature-order errors. Rendering is shaded viewport output, not a hidden-line engineering drawing.

## Repair strategy

Prefer the smallest correction supported by the current implementation:

1. Wrong parameter value -> `set`.
2. Wrong optional finishing feature state -> `suppress` or `unsuppress`.
3. Unwanted feature with no required dependents -> `delete`.
4. Wrong sketch geometry, selector, pattern source/count, feature order, or construction method -> rebuild from corrected complete source inside the same session working Part.

Do not keep applying local edits after the feature tree has become structurally wrong. Do not create another Part to try another visual variant. After the initial build, make at most one materially different structural rebuild per user turn. Identical retries on unchanged state are invalid. If the corrected model still cannot satisfy the requested silhouette, report the exact mismatch or unsupported DSL capability rather than continuing to guess.

## Common failure patterns

- **Unknown parameter**: define it earlier or correct the spelling.
- **Duplicate name**: rename the top-level parameter, sketch, or feature.
- **Unknown sketch plane**: use `XY`, `XZ`, `YZ`, or a supported directional face selector.
- **No solid body exists**: create the base solid before body-dependent face, edge, shell, fillet, chamfer, or pattern operations.
- **Profile creation fails**: make the section a valid closed profile and remove overlaps or self-intersections.
- **Hole placement fails**: verify the selected face/plane and the `at x y` coordinates.
- **Sweep fails**: keep the route connected and compatible with the profile.
- **Loft fails**: provide at least two valid closed section sketches.
- **Fillet/chamfer fails**: call `geometry`, verify the exact current edge indexes, then use `edges i,j,...`; reduce the radius/distance only after confirming the selection.
- **Directional face is ambiguous on a stepped part**: directional selection chooses the outermost matching planar face. Call `geometry` and use `face:index:n` / `faces index:n` when a different planar face is required.
- **Topology index changed**: edge/face indexes are revision-local. Re-query `geometry` after any topology-changing build or edit.
