# Wave 1b placement contract

This wave serves P1 (the readable tide board) and P3 (the working Landing and cannery). It extends the existing key-scene patch only; it never rebuilds the region.

## Lead-architect rulings

`KeySceneDef.PlacementIds` is an additive authoring field. Empty means all otherwise eligible rows, preserving existing assets. A non-empty list restricts a placed scene to those stable ids. It does not alter a piece's content, ownership, variant, height or order. The importer preserves the list and refuses a selected id disappearing from its source. The step refuses duplicate or unknown selections. Whelps selects its bench only. The east end selects eighteen rows while the ice house and its two pieces wait for a committed grass refresh. Other scenes retain their previous placement flags.

Core owns `IGullPerch` and the scene-lifetime `GullPerches` offers/reservations. World owns the serialized `GullPerch` point and registers it while enabled. Art's existing `GullFlock` claims a nearby offer on its normal decision tick, uses its existing landing state, and leaves after a daytime settle window. Offers are sorted by stable id; claims are exclusive and released on departure, timeout, component disable or host removal. No save fields or random source are added. The point has no Update, LateUpdate, FixedUpdate, renderer or animation. World and Art do not reference one another.

A mount retains its authored ground point and absolute height. Its screen point adds only the rise above the host's ground, using the shared bake height scale. The cannery perches retain their own numbers; the cannery host supplies their sort line, not a replacement coordinate. Visual agreement with the sidecar's ridge pixel remains an explicit plate acceptance check. A free-standing building remains at its authored world XY. Its collider rotates the kit's ground footprint then projects local depth by sin(40 degrees), following ADR 0042. The grass guard applies the inverse projection to the committed field's actual sites.

The four builds extend `VillageBuildingKit` with preset option overrides, carried in the bake contract. Albedo-only baking uses the existing menu entry. A build cannot place without a baked and sliced catalog sprite. The east-end import checks the exact ruled source SHA-256 and reads all twenty-one heights from the committed painted ground.

## Deferred work

- Ice house, its wood and its barrow: wait for grass-field refresh. That refresh belongs to one StPetersLayerRefresh root patch, never Build(). Owner decision remains open.
- `path.stp_loft_spur`, 0.9 m: the next St Peters ground PR.
- Other scenes' kits, lights, wildlife, seasonal/tidal variants and restored cannery gameplay remain in their assigned waves.
- Art desk routing: Alder Fall intake route, ownership of pass 2's other seventeen takes, and CD adoption of the committed seventy-id map remain the handoff's asks.

## Verification status

The source compiled outside Unity and in the pinned 6000.5.0f1 editor. Importer and Core reservation fixtures also passed on .NET 8. The four builds are baked, sliced and verified; the other sheets remain unchanged. The production importer changes no assets on repetition. The second scene-patch application has no operations and leaves identical scene bytes.

The local EditMode blast radius covers 1,116 cases across 62 fixtures. Five initial failures exposed changed premises: document append order, the perch component whitelist, the tide-board anchor count and source hash, and four additional building calibrations. The focused repair run passed all 48 cases, including those five. Reconstruction checks every document byte-for-byte by fileID, including hierarchy lists, and requires the committed scene to yield an empty patch.

The tide-board rebake shortens the post from 4.92 to 4.70 m and the tallest cell from 140 to 135 px. Remaining marks retain their absolute tide levels. The unchanged rig omits the -1.70 half-mark at the lower drawing boundary of the new foot; the Art desk/owner should review that visible difference.

Grass measured against the projected kit footprints: bait store 0 sites, net loft 0, net shed 0, deferred ice house 5. The prior unsquashed probe's 12 sites was not the picture footprint. Content validation passed all 26 cases. The nine-frame O3 GPU capture passed; existing GPU-only failures from the blast radius are named in the slot handback and remain unresolved. Those cases are skipped in baseline CI, which does not establish whether their GPU failures predate this wave.

Visual acceptance is still open. The authored gull_a perch is 4.38 m right and 4.512 m below baked ridge pixel {446,169.8}, approximately 140 by 144 sheet pixels. The guard proves the Landing pilehead height and cannery host resolution; it does not claim that the roof perches are within five centimetres of the picture. Authored coordinates are preserved for owner/Art resolution.

The owner also identified the terrain difference in the paired plates. TerrainSplatSurface builds one flat quad; TerrainSplat's vertex stage does not project terrain height, and its fragment stage samples the height field at world XY for bands and shading. CD's renderer uses the height field to draw relief. Thus the game's texture picture does not follow the board's visible topography. This is a separate terrain-rendering acceptance issue, not fixed by adjusting the camera or moving a prop. No terrain renderer change is included in this placement wave.

CI is forecast by name against successful run 37453862407, preserving all 265 EditMode and 113 PlayMode baseline skips. The new GPU camera case adds one expected Null-device skip. CI on the PR's own head and the outstanding visual decisions remain required.
