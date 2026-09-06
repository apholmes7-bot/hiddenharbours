# Sailing hulls' sidecars — why they are in a subfolder

The two sloops of the sail rig kit ship the fleet's `boat-gameplay-geometry@1` sidecar **and** a new
`boat-sailing@1` one. Both live here rather than in the parent `docs/art/rigs/gameplay/`, for the same
reason `vehicles/` exists:

> `DeckSidecarImportParityTests` treats every `*.gameplay.json` in the parent folder as a boat deck that
> **is worn by a hull** — its last assertion is "imported but worn by nobody". A deck is reached through a
> `BoatVisualDef`, a visual is created by the mesh bake, and the sloops' mesh bake is **blocked upstream**
> (`HullMeshFleet.BakeBlocked`: both rigs publish `geometry().ids` but stamp ~45% of their faces with no
> level, and the levels they do stamp come from a different vocabulary). So a sidecar in the parent folder
> today would import a deck that nothing can walk — a red test describing a real gap, but not one this
> folder placement should be asserting.

**They move up one level in the same PR that unblocks the bake**, together with their deck defs, their
`VisualsBySidecar` wiring rows and their visuals. Nothing else about them changes: the files are
byte-identical to the kit's, the LF pin in `.gitattributes` follows the folder, and
`SailRigKitTests.ManifestToRepoPath` is the one place the path is written down.

| file | schema | read by |
| --- | --- | --- |
| `sloopIsoRig.gameplay.json` | `hidden-harbours/boat-gameplay-geometry@1` | `DeckSidecarReader` (when promoted) |
| `sloopIsoRig.sailing.json` | `hidden-harbours/boat-sailing@1` | `SailingSidecarReader` → `SailPolarImporter` |
| `sloop88IsoRig.gameplay.json` | `hidden-harbours/boat-gameplay-geometry@1` | as above |
| `sloop88IsoRig.sailing.json` | `hidden-harbours/boat-sailing@1` | as above |

⚠️ All four are **generated, never typed**, and stamped with `derivedFromRigSha256` from the rig bytes read
in the same run. If a rig is reshaped, regenerate the whole kit with `SAIL_KIT.write()` — never patch a
number. `SailRigKitTests` compares the full 64-character digest; a prefix check is not a hash check.
