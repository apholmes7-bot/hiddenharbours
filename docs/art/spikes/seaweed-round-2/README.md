# Seaweed round 2 — the frond hooks the line, the tail trails the sea: the eyeball pack

The owner's sentence, 2026-07-08: *"seaweed clumps that can get stuck on things and group together from
the waves."* Round 1 (#195 the system, #301 the painted art) drifted, merged, stranded and snagged — but a
snag rested the clump on a **radius** around the buoy, its rotation was a hashed constant, and the art's
own anchors (`DriftWeed.json`: 2–3 `snags` frond tips and one `dragTail` per variant) were never read.
This pack is round 2 for the owner's eye: the anchors are real, and the NPC fleet's gear counts as
something to hook on.

Every plate is the real St Peters bed (`StPetersSeaweed.asset`, the shipped kit) in play at **noon**, the
game's own main camera parked over the subject and read back after the day/night overlay. The
pixel-perfect component was lifted for the render so the camera could zoom to an orthographic size of
2.0 (about 270 screen px per metre; the game's own framing puts a 0.45 m eelgrass at 27 px, which is
why round 1's weed was so easy to miss). Nothing staged but a fence of the player's trap buoys across
the set (published through the same `TrapPlaced` signal a real set produces — the yellow floats are the
game's own) and, for the plates only, the bed rectangle re-aimed into deep water (see the note at the
end).

| plate | what to look for |
|---|---|
| `kelp-hooked-on-trap-buoy.png` | **The owner's plate.** A golden sugar kelp hangs off a trap buoy by one frond tip — the tip that met the line first — and its body streams **down-flow** (east; the set was 0.35 m/s east). The tip sits on the line; the sway (6°) works about the tip, not the clump's centre. |
| `hooked-on-trap-buoy.png` | Two torn mats on two neighbouring lines, both hanging the same way from their tips — the sea pushed each clump past its line and it swung to lie downstream of the catch. |
| `hooked-on-fleet-hull-rim.png` | An eelgrass fouled on an **ambient fisher's port planking** while she lies-to at her spot. She reached the drift through Core `SnagTargets` (radius 1.2 m — the contact is on her rim, not her keel); round 1 could not see her at all. |
| `drifting-tail-trails.png` | A torn mat carried east with its scrap tail trailing **behind** (west) — the drag alignment at 25°/s, read off the same transport the clump moves by. |
| `knob0-drifting-hashed-rotation.png` | **Control, round 1:** every round-2 knob at 0 on a fresh population — the same species, hashed rotation, tail pointing nowhere in particular. |
| `knob0-rested-on-radius.png` | **Control, round 1:** two clumps "snagged" the old way — resting 0.35 m from the buoy on the side they drifted in from, at hashed angles, one lying across the float. Compare with the two hooked plates above. |

## What the anchors are (so the plates can be judged)

- A clump's `snags` are the outer frond tips the rig declared; its `dragTail` is *"the end that trails
  when drifting"*. Both are stored on `DriftWeedKit` in the **sprite's own frame** — cell pixels over the
  sidecar's 32 px/m, y flipped — so a tip placed by them sits on the pixel the art director drew. The
  sidecar's own `m` values are plane-metres (y ÷ 0.72) and a test reconciles the two through that one
  factor.
- **Drifting:** the rotation eases (`DragAlignDegreesPerSecond`, 25) until the tail streams out **behind**
  the transport carrying it (flow + shared wind + trough-seek — the same vector the piece moves by).
- **Hooked:** the tip that led the approach is nailed to the line; the body swings to lie
  **down-transport** of it; it sways about the tip with the wave sampled **at the tip**
  (`SnagSwayDegrees`, 6); the hang eases round when the set changes. The optional swell release
  (`SnagBreakWaveMeters`) ships **off**.
- **Knob 0 is round 1, byte for byte:** the 60 s stepped pose sheet (`SeaweedAnchorsPlayTests`) hashes
  identically on the seam commit and on this PR with every round-2 knob at 0 — sha256 `8e9f0bc2…`,
  1,321,775 bytes, 1800 steps × 12 pieces, 705 snagged piece-steps in it.

## Found while shooting (not touched by this PR)

The shipped `StPetersSeaweed` bed rectangle — centre (5, −30), size 90 × 26 — lies almost entirely on
the island in the current St Peters terrain. Sampled at 13:00 (water level 1.32 m): every point along
y = −30 from x = −30 to x = 30 read 4.7 m **above** the water; only the rectangle's western sliver
(x < −35) is wet, and 13 of the bed's 18 pieces sat dormant at the spawn-depth gate, the rest crowded at
that edge. The plates re-aimed the runtime bed rectangle to (−20, −100) + (80, 25), deep water south of
the island, by reflection for the session only — nothing serialized, the asset is unchanged. A
world-content follow-up should move the bed into the water (the west channel x ∈ [−80, −45] or the
southern reach y < −70 are both deep at every sampled tide).
