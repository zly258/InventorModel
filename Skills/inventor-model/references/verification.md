# Verification and repair

## Inspect first

After a meaningful build or edit, use `inspect` and compare:

- `bodies` with the intended body count;
- `features` with the expected feature-tree size;
- `size_mm X Y Z` with the requested overall envelope;
- `parameters` with the intended driving dimensions;
- `feature_tree` with the planned feature names and suppression states.

A successful build with the wrong envelope is still a modeling error.

## Four-view verification

Use `render` to generate:

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
4. Wrong sketch geometry, selector, pattern source/count, feature order, or construction method -> rebuild from corrected complete source.

Do not keep applying local edits after the feature tree has become structurally wrong.

## Common failure patterns

- **Unknown parameter**: define it earlier or correct the spelling.
- **Duplicate name**: rename the top-level parameter, sketch, or feature.
- **Unknown sketch plane**: use `XY`, `XZ`, `YZ`, or a supported directional face selector.
- **No solid body exists**: create the base solid before body-dependent face, edge, shell, fillet, chamfer, or pattern operations.
- **Profile creation fails**: make the section a valid closed profile and remove overlaps or self-intersections.
- **Hole placement fails**: verify the selected face/plane and the `at x y` coordinates.
- **Sweep fails**: keep the route connected and compatible with the profile.
- **Loft fails**: provide at least two valid closed section sketches.
- **Fillet/chamfer fails**: current code applies the operation to every edge; reduce complexity/radius or use another construction.
- **Directional face resolves the wrong face**: face selection is orientation-based, not feature-topology persistent; restructure the model or extend the selector implementation.
