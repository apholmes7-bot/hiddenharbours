# Wharf buildings pass 2 delivered file table

The drop delivers 29 files. 28 of them land under `docs/art/rigs/wharf-building-kit-v2/`, and all 28 are new paths in the repo. The 29th, `lib/interiorPropRig.js`, does not land: the prop rig has one home, main's `docs/art/rigs/interiorPropRig.js`, and `MultiunitKitIntakeTests.OneInteriorPropRig` pins it there. Drop 14's houses kit made the same exception.

"Identical" means a file already on the base tree (45a09dd6) has the same Git blob id (for text) or the same LFS sha256 (for PNGs). It does not mean the kit's copy replaces that file. Of the 29 files, 22 are new and 7 are identical, so no library differs from main's copy.

What reads the libraries:
- The catalog loads main's `interiorPropRig.js`, `coastalPass.js` and `buildingLifecycleRig.js`, never the kit's `lib/` copies, which serve the viewer.
- The rig bake needs neither `support.js` nor `weatherSky.js`.

`SHA256SUMS.txt` covers the other 28 files. `.gitattributes` lines 281–287 pin the text files to LF, and the nine boards stay in LFS.

| Delivered path | Bytes | Compared content | Reader |
|---|---:|---|---|
| `boards/before-after-fishPlant.png` | 512,889 | New | Human review (LFS) |
| `boards/before-after-gambrelBarn.png` | 231,076 | New | Human review (LFS) |
| `boards/before-after-netShed.png` | 136,830 | New | Human review (LFS) |
| `boards/interiors-fishPlant.png` | 295,945 | New | Human review (LFS) |
| `boards/interiors-gambrelBarn.png` | 132,895 | New | Human review (LFS) |
| `boards/interiors-netShed.png` | 133,029 | New | Human review (LFS) |
| `boards/lifecycle.png` | 185,289 | New | Human review (LFS) |
| `boards/presets-more.png` | 340,037 | New | Human review (LFS) |
| `boards/weather-tealShack.png` | 194,935 | New | Human review (LFS) |
| `checks.txt` | 7,282 | New | Node checks (compared in this intake); no game reader |
| `gameplay/wharfBuilding2.cannery.gameplay.json` | 19,179 | New | `WharfBuildingPass2IntakeTests` (schema, preset, live-model bytes); a placement reader later |
| `gameplay/wharfBuilding2.fishPlant.gameplay.json` | 19,177 | New | `WharfBuildingPass2IntakeTests` (schema, preset, live-model bytes); a placement reader later |
| `gameplay/wharfBuilding2.gambrelBarn.gameplay.json` | 10,406 | New | `WharfBuildingPass2IntakeTests` (schema, preset, live-model bytes); a placement reader later |
| `gameplay/wharfBuilding2.iceHouse.gameplay.json` | 10,457 | New | `WharfBuildingPass2IntakeTests` (schema, preset, live-model bytes); a placement reader later |
| `gameplay/wharfBuilding2.netShed.gameplay.json` | 10,903 | New | `WharfBuildingPass2IntakeTests` (schema, preset, live-model bytes); a placement reader later |
| `gameplay/wharfBuilding2.redShed.gameplay.json` | 11,115 | New | `WharfBuildingPass2IntakeTests` (schema, preset, live-model bytes); a placement reader later |
| `gameplay/wharfBuilding2.tealShack.gameplay.json` | 10,867 | New | `WharfBuildingPass2IntakeTests` (schema, preset, live-model bytes); a placement reader later |
| `lib/buildingLifecycleRig.js` | 36,421 | Identical: `docs/art/rigs/building-lifecycle-kit/buildingLifecycleRig.js`, `docs/art/rigs/village-return/houses-kit/Art/building-lifecycle-kit/buildingLifecycleRig.js` (blob `32a6dfd4`) | Viewer; the catalog loads main's copy (`buildingLifecycle`) |
| `lib/characterIsoRig9.js` | 114,763 | Identical: `docs/art/rigs/character/rig9/Art/characterIsoRig9.js`, `docs/art/rigs/wharf-rig-kit-v2/lib/characterIsoRig9.js` (blob `30c9c560`) | Viewer only |
| `lib/characterIsoRig9.poses.js` | 49,219 | Identical: `docs/art/rigs/character/rig9/Art/characterIsoRig9.poses.js`, `docs/art/rigs/wharf-rig-kit-v2/lib/characterIsoRig9.poses.js` (blob `4e3ed3e9`) | Viewer only |
| `lib/coastalPass.js` | 55,803 | Identical: `docs/art/rigs/village-return/houses-kit/Art/coastalPass.js` (blob `5a6c7625`) | Viewer; the catalog loads main's copy (`coastalPass`) |
| `lib/interiorPropRig.js` | 63,049 | **Not landed.** Identical to `docs/art/rigs/interiorPropRig.js` (blob `e5d6542d`), its one home (`MultiunitKitIntakeTests.OneInteriorPropRig`) | Viewer; the catalog loads main's copy (`interiorProp`) |
| `lib/weatherSky.js` | 13,650 | Identical: `docs/art/rigs/terrain/pass9/lib/weatherSky.js`, `docs/art/rigs/wharf-rig-kit-v2/lib/weatherSky.js` (blob `3efe37b2`) | Viewer only; the catalog leaves it out |
| `README.md` | 10,176 | New | Documentation; its load order is the catalog's |
| `SHA256SUMS.txt` | 2,698 | New | Integrity: `WharfBuildingPass2IntakeTests` |
| `support.js` | 69,150 | Identical: `docs/art/rigs/character/rig9/support.js`, `docs/art/rigs/village-return/houses-kit/support.js`, `docs/art/rigs/wharf-rig-kit-v2/support.js` (blob `cb009b69`) | Viewer only |
| `Wharf Building Iso v2.dc.html` | 50,171 | New | Viewer |
| `wharfBuildingRig2.geo.js` | 96,187 | New | Editor bake: catalog `wharfBuilding2Geometry`; tests; viewer |
| `wharfBuildingRig2.js` | 34,646 | New | Editor bake: catalog `wharfBuilding2`; tests; viewer |
