# First camera practice: Odile's seal picture

Status: design note (not built). Pillars: P1 Sea Has Moods, P3 Living Working Coast, P5 Cozy but with Teeth.
Owner direction (relayed by the coordinator, 2026-11-26): the seals are the player's first practice
with the camera. Odile Marchand, who lives in the Whelps cottage, asks the player to bring her a
picture of the seals.

## What exists today

- **The seals.** `haulout.st_peters.whelps` (Data/Wildlife) and `wildlife.grey_seal` drive a
  `SealHaulout` host in St Peters (root `StPetersSeals`). Who is out is a pure function of
  (worldSeed, gameTime, tide); nothing is saved.
- **The subject contract (Core).** `IWildlifePresence` and `WildlifePresences.CountInFrame(frame,
  subjectId, onLandOnly, scratch)` answer the camera's question: "how many seals are in this
  rectangle of the world, and are they out on the rock?" Each sighting carries the subject id
  (`wildlife.grey_seal`), a world position, a pose (Resting, Alert, Sliding, Swimming, Diving) and a
  stable key per seal. The gulls already have the same kind of seam (`IBirdPresence`).
- **Odile.** `Data/NPCs/OdileMarchand.asset`, her first dialogue, her routine, and the keepers'
  supply gift. She has no request yet.
- **No camera.** There is no camera item, capture action, viewfinder, photo store or photo page.
  The plan lives in `backlog/birds-and-photography.md` (BP-09 to BP-13). The owner ruled on
  2026-11-13 that the camera is always on the player, on the P key.

## What the camera slice needs (in build order)

1. **Capture (BP-09).** P raises a viewfinder: a world-space rectangle in front of the player
   (size and reach in GameConfig). A shutter press asks `WildlifePresences.CountInFrame` (and the
   bird seam) for subjects in frame, and scores the shot: subject id, how many, on land or in the
   water, and whether the nearest one was Resting (a good picture) or Alert/Sliding (a startled
   one). No save yet. The camera reads Core only; it never names `SealHaulout`.
2. **Odile's request as data (BP-12).** A quest/request def in her folder: "a picture of the
   seals hauled out on the Whelps", satisfied by a capture with `subjectId = wildlife.grey_seal`,
   `onLandOnly = true`, count at or above a tunable (suggest 2), and the nearest seal Resting.
   Completion is an existing quest flag, so no new save field. Her lines (asking, a reminder, the
   thank-you, a hint about coming in slowly from downwind at low water) are dialogue assets.
3. **Where a photo is kept (BP-10, BP-11).** Showing the picture to Odile later, or a Notebook
   photo page, needs the photo kept. That is a **new saved record** (BP-11's "recipe" photo of about
   50 bytes: subject, count, pose, time, place) and a save-schema bump. It needs the owner's yes
   before it is built. Until then the request can complete at the moment of capture (the flag is
   the only saved trace).

## The approach puzzle (built, tunable)

The seals make the shot a small puzzle, with every number in `wildlife.grey_seal`:

- Inside **AlertDistanceMetres** (16 m) a resting seal lifts its head if you come in faster than
  **CreepSpeed** (0.5 m/s).
- Inside **SlipDistanceMetres** (7 m), or closing faster than **StartleApproachSpeed** (1.4 m/s),
  it slides into the water and stays off its rock until **CalmReturnSeconds** (40 s) pass with
  nobody inside the alert ring.
- Holding still (or creeping) for **AlertCalmSeconds** (6 s) lets a watching seal settle.
- Wind: both distances x **UpwindDistanceScale** (1.5) when the wind carries you to them,
  x **DownwindDistanceScale** (0.7) when you are downwind; faded to 1 in a light wind.
- A hull (the dory) counts as an intruder too; a person on foot uses **OnFootDistanceScale**.
- Most seals are out at a calm, sunny low tide; fewer at night and in a rough sea.

So the good picture is a slow approach from downwind at low water, from the lookout or the dory.

## Not in this slice

Seal sounds, pups, a fishing effect (seals near the reef taking fish off the line), seals at other
haul-outs, a seal humping across the dry flats to reach the water at a big low tide (today a seal
whose way off the rock is all dry slips out of sight instead of drawing a head on sand). These are
logged ideas only.
