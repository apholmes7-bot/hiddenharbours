# Sail rig kit — the intake spike (2026-09-06)

The owner's drop of two sailing hulls, verified. **Nothing here is read off the drop's READMEs**; every
number was measured in the repo's own ClearScript V8 against the bytes committed in the same PR, and
where a measurement contradicts the drop, both are given.

| file | what it is |
| --- | --- |
| [`probe-record.md`](probe-record.md) | the transcript — provenance, azimuth, the pose law, the silent fallbacks, the seam measurements, the facts PR 1 needs, and the two places the drop's prose is wrong |
| [`seam-proposal.md`](seam-proposal.md) | the decision request for `lead-architect`: how a boat whose picture is a *continuous pose* crosses a seam built for rigid geometry |

The guards that must keep holding live in `Assets/Tests/EditMode/RigBaking/SailRigKitTests.cs`. This
folder is evidence; the tests are the contract.

## The plates

All rendered from the **rigs' own renderer** — the mesh bake is blocked upstream
(`HullMeshFleet.BakeBlocked`), so there is no mesh to show beside them yet.

| plate | what to look at |
| --- | --- |
| `sloop-30-stowed-8-facings.png` | the stowed boat, all eight headings. **This plate is its own azimuth witness**: at dir 2 — the cell the rig labels `'E'` — the bow points WEST. Counter-clockwise, as the rest of the fleet. |
| `sloop-88-stowed-8-facings.png` | the same for the 88, at 0.55× |
| `sloop-states.png` | the five named rig states, both hulls. All five draw a different picture (measured by buffer fingerprint, not by eye) |
| `sloop-30-points-of-sail.png` | ★ **the one to judge.** The seven points of sail at AUTO_TRIM — boom, camber, luff and heel all from `sailPose(awa, aws, sheets)` |
| `sloop-88-points-of-sail.png` | the same seven with the staysail set |
| `sloop-cabin-cut.png` | both cabins, cut at the boot-top lip. One rig, no interior sidecar — the rooms are DECK levels |
| `sloop-88-cabin-cut.png` | her saloon and lower sole, larger |
| `sloop-30-colourways.png` | the eight schemes, shared value-for-value with the skiffs and the punt |
| `sloop-88-scale.png` | the 88 against the Sloop 30, the Cape Islander and the tanker, all normalised to one metres-per-pixel |

## What the owner is being asked

1. **The look** — do these two read as the fleet's boats? (`sloop-30-points-of-sail.png` first.)
2. **The no-go coupling** (`probe-record.md` §5): the polar says no-go below **twa 35 true**, the sprite
   flogs below **awa 25 apparent**. They cannot both be what the player feels. The kit's own note
   recommends pinching to the sprite; the disagreement is 4 of 150 grid cells on the 30 and 14 of 150
   on the 88, and — contrary to both READMEs — it bites in **light** air, not heavy.
3. **Hoist-as-reef**: neither rig has reef points. `hoist < 1` shortens the luff and grows the pile on
   the boom. Does that read as a reef, or is a reef-point pass an upstream ask?

## Upstream asks recorded here

- ⚠️ **Level stamps** (blocking every mesh of these hulls): both rigs publish `geometry().ids` and then
  stamp ~45 % of their faces with no level, in a vocabulary that is not `ids`. See `probe-record.md` §4
  and `HullMeshFleet.BakeBlocked`.
- Reef points on both sloops, if hoist-as-reef is refused.
- The 88's owner's cabin is not cut open in pass 1.
