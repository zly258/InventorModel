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
