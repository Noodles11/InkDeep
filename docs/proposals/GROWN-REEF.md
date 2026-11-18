# Grown reef — proposal

The cube reef's rock should read as something grown, not cut: live-rock walls that bulge out and tuck in as they
rise, ledges and lumps everywhere, arches and tubes where the rock reaches across, and almost no flat ground. The
references: a canyon of overhanging, mushroom-headed stacks under god rays; porous live rock; boulder-stacked slopes
furred with growth; a narrow slot between knobbly walls opening onto bright water; a deep coral corridor of
plate-and-fan walls over a sand floor.

Status: built (CUBE-WORLD-PLAN §5, DIVE §1): terraced floors whose edges slope away, grown walls, deep knobs and bays, tunnels' throats flaring into wider water, rims round wells' mouths, natural arches and rounded lips on every roof. The blue ink look (ART-LOOKS) is in.

## What it is now, and why it reads cut

- The rock is one heightfield per cube (`CubeTerrain`): rock above the swim plane, a shelf floor or the void
  below. Walls come from blurred cavern noise, so their outline at her plane is smooth and blobby.
- Drawn, every wall is stretched straight up 60 m (`topdown_ground`'s `rise`): a vertical sheet with even contour
  stripes, the same at every height. That sheet, more than anything, is what reads as cut.
- The shelf floor is nearly flat (0.3 m of noise every ~5 m); tunnel and well edges are carved, then blurred.
- Roofs exist (roofed tunnels, gate roofs, socket plugs), but they are flat slabs at 3.6 m.

## The plan

Everything below the swim plane stays visual only; the sim's rock (what stops her) changes only in step 3, and
always keeps her ways at least as wide as now.

### 1. Ground that is never flat
- The shelf floor gets relief at three scales: mounds 1–1.5 m high every 6–10 m, lumps 0.3–0.6 m every 2–3 m, and
  grain. Every flat stretch varies in height every couple of metres.
- Rubble and boulder heaps at the walls' feet (the third reference), and the odd loose rock out in the open.
- All of it stays under her plane with a margin, so nothing she can touch changes.
- The first layer keeps broader, gentler sandy stretches (relief at about half), but no dead-flat floor.

### 2. Walls that grow
- The risen wall is no longer a straight sheet. As it rises it is pushed in and out by 3D noise: bulges, undercuts
  and overhangs, mushroom heads, the face stepping back in ledges every 2–4 m of height. This happens in the ground's
  vertex stage, so the sim's outline at her plane is untouched.
- The face is drawn as live rock: pits and holes in its shading, lumps catching her light. Lines follow the bulges,
  not even contour stripes.
- The cut's caps stay as they are (hatched, flat at the cut), since they are a drawing convention.

### 3. Grown outlines, no rough-cut holes
- The walls' outline at her plane gets small rounded lobes (knobs and bays of 1–2 m) over the big cavern shapes.
  Each passage keeps its least width.
- Tunnels and wells get rounded, lipped edges: a rim of rock that swells up round a well's mouth, and a tunnel that
  opens like a throat, never a punched hole.

### 4. Arches and tubes
- Where two walls come within a few metres of each other over a passage, the rock sometimes reaches across above
  her: an arch, lumpy and grown, and thicker at its feet than its crown.
- A roofed tunnel becomes a tube: a rounded vault of rock over the way, open in places, not a flat slab.
- They show from above as rock over the water. The cutout already cuts what is on the camera's side of her, so she
  stays visible under them.

### Later
- Underwater ruins in the deeper layers (your note), built on the same grown rock.

## Order and checks

1, 2, 4, then 3. Step 1 and step 2 are visual only and can land on their own; 3 changes the rock she collides with
and needs the reef tests (ways through stay open, gates, wells, sockets) to pass. Each step is checked in captures
on the warm-up layer and a deep one before it is committed.
