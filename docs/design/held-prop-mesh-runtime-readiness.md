# Held-prop mesh runtime readiness

The baked data is ready to resolve, but the runtime handover is blocked by the
depth-aware plotted-line path required by [ADR 0053](../adr/0053-held-props-are-facet-meshes-on-the-figures-sockets.md).
Enabling faces alone would omit rod blanks, shovel shafts, bucket bails and fish eyes.
This change supplies data lookup and tests; it does not enable mesh props.

## Data and fallback inventory

All 50 committed held states have a reference from their owning content:

| Content | Baked states | Resolution |
| --- | ---: | --- |
| Tools | 3 rods, 1 shovel | `ToolDef.HeldMeshFor(variant)` uses the authored variant, with ordinal matching |
| Catch | 7 fish | `CatchItemLibrary.HeldMeshFor(kind)` |
| Containers | 39 bucket states | `CatchItemLibrary.BucketMesh(tier, band, group)` |

Tool lookup returns null for a missing variant or mesh array. It never substitutes the
first array element for an unknown tier. The shovel's single state has an empty variant;
an empty string resolves that authored state, while null means no requested state.
Resolution returns the referenced def; a renderer must also check `IsUsable()` before
hiding a sprite. These APIs carry no item-name switch or runtime asset search.

The catch kinds clam, crab, lobster, mussel, oyster, periwinkle and scallop have held
sprites and no held mesh. Fuel containers, NPC pots and landing crates also have no
`HeldPropMeshDef` pairing. Those objects keep their current sprites. New geometry is
outside this change.

Of the 50 baked states, 37 record plotted lines: 3 rod blanks, 1 shovel shaft,
26 bucket bails and 7 fish eye records. The 13 tray states have no plotted lines.
The current runtime Art code has no consumer of `HeldPropMeshDef.Lines` or `LineRamps`.
`IsoFacetPropRenderer` only accepts a hull fitting setup and draws its face mesh.
ADR 0053 explicitly sequences the figure mount and facet thin-line work before the
visible runtime handover. A general mesh renderer or a substitute material does not
satisfy that contract.

## Sorting and anchors to preserve

`CarryHands.Pose` is the current authority. When a carry-anchor row exists, it uses
the character's drawn facing and frame, the row's pin and item facing, and **the row's
`Behind` flag**. It must not recompute behind/in-front from heading on that path.
The fallback alone uses `CarryMath.AheadOrdersFor`: away headings sort behind;
toward and exactly abeam headings sort in front. The left hand takes one extra sorting
order to avoid a tie between two held items.

Both paths use the body's current sorting layer and order through `RideBodyBand`,
and add `DrawnBodyOffsetLocal` to ride the body at its drawn deck height. A replacement
must preserve those decisions, including the anchor path's north-facing exception.
The ashore figure's facet overlay currently derives its bounds from the character's
cell and reach. Simply parenting a prop into that figure does not provide a new held
extent or the existing sprite's separate sort order. The mount must account for both.

The fight has a separate rod drawer and suppresses the carried tool through
`SetDrawnElsewhere`. A future mesh prop must obey that suppression so a cast cannot
draw both rods. Fishing phase code is outside this change.

## Remaining implementation

1. Provide and verify the ADR's depth-aware plotted-line path and figure mount, with
   the prop's complete extent and the existing front/behind rule. Art review owns any
   shader or appearance change. No shader, material, rig or baked asset is changed here.
2. Connect that renderer to the existing carry pose, and hide a sprite only after the
   complete mesh drawer succeeds for a visibly meshed holder. Missing or unusable data,
   a sprite holder, and a refused renderer all retain the sprite.
3. Add the requested default-on mesh-prop switch in a separate held-props section of
   `GameConfig` and its serialized asset. Do this with its runtime consumer; an inert
   switch would not restore sprites. Keep its insertion apart from fishing and helm work.
4. Reuse one held draw object per occupied hand. Reconfigure on item/state changes;
   update only transforms, sorting and visibility each frame. Stand down and return
   sprite ownership on placement, disable, destruction and holder-mode changes.

The lookup added here only scans an existing array; it allocates no object or collection.
The runtime object lifecycle remains unimplemented while the renderer is blocked.

## Verification

`HeldPropMeshLookupTests` contains five EditMode tests: tool variants independent of
array order, the unlabelled single state, missing tool meshes, missing catch/bucket
states, and exhaustive resolution through every committed tool/library owner.
The content test compares the complete referenced set with all committed held mesh defs
and checks usability. It also checks null resolution for each unmeshed catch kind.

The source and fixture compile against Unity reference assemblies without launching
the editor. An independent YAML audit resolved all 50 states and identified the seven
catch fallbacks and 37 line-bearing states. These are compilation and static-data
checks, not executed Unity tests. No local Unity, PlayMode, plate or pixel proof ran.

Once the renderer is available, the required PlayMode matrix remains: bucket, rod and
fish, each held by a mesh holder and a sprite holder; exactly one drawer in each state;
then switch off and back on. Add missing/unusable-def, separate fight-rod ownership,
two-hand sorting, placement, disable and repeated-frame reuse cases. Existing carry
anchor and drawn-body tests must retain their assertions.
