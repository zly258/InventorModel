# Modeling patterns

These patterns use only syntax implemented by the current code.

## Parameterized plate

```text
part Plate
param width = 100
param height = 60
param thickness = 10

sketch base on XY
  centerrect 0 0 width height
end

extrude body from base depth thickness join
```

## Flange with circular bolt pattern

```text
part Flange
param outer = 120
param bore = 50
param thickness = 16
param bolt = 10
param boltRadius = 45

sketch ring on XY
  circle outerCircle 0 0 outer/2
  circle boreCircle 0 0 bore/2
end

extrude body from ring depth thickness join
hole bolt1 on face:body:top at boltRadius 0 diameter bolt through
pattern_circular bolts source bolt1 axis Z count 6 angle 360
```

## Plate with rectangular hole pattern

```text
part MountPlate
param width = 120
param depth = 80
param thickness = 12
param holeDiameter = 10

sketch base on XY
  centerrect 0 0 width depth
end

extrude body from base depth thickness join
hole h1 on face:body:top at -40 -20 diameter holeDiameter through
pattern_rect holes source h1 count 3 2 spacing 40 40
```

## Sweep

```text
part SweepPart
param tubeRadius = 8

sketch section on YZ
  circle sectionCircle 0 0 tubeRadius
end

sketch route on XY
  line route1 0 0 50 0
  arc bend 50 20 20 -90 0
  line route2 70 20 70 70
end

sweep body profile section path route join
```

## Local parameter correction

After a parameterized model is built:

```text
set width = 140
```

Use `modify` for this rather than rebuilding the whole Part.

## Symmetric plate

Use a symmetric extrusion when the design should stay centered about the sketch plane:

```text
part SymmetricPlate
param width = 100
param height = 60
param thickness = 10

sketch base on XY
  centerrect 0 0 width height
end

extrude body from base depth thickness direction symmetric join
```

## Stepped shaft from a drawing centerline

For turned parts, define an explicit sketch line as the revolution axis instead of relying on a global axis that may not match the profile orientation:

```text
part SteppedShaft
param l1 = 40
param l2 = 50
param l3 = 30
param r1 = 12
param r2 = 18
param r3 = 10
param total = l1+l2+l3

sketch profile on XY
  line axisLine 0 0 total 0
  line p1 0 0 0 r1
  line p2 0 r1 l1 r1
  line p3 l1 r1 l1 r2
  line p4 l1 r2 l1+l2 r2
  line p5 l1+l2 r2 l1+l2 r3
  line p6 l1+l2 r3 total r3
  line p7 total r3 total 0
  line p8 total 0 0 0
end

revolve body from profile axis line:1 angle 360 join
```

The selected axis line becomes construction geometry before Inventor builds the solid profile.
