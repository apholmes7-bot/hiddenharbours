# Wharf pass 2 delivered file table

All 79 destination paths under `docs/art/rigs/wharf-rig-kit-v2/` are new. "Identical" means an existing copy elsewhere has the same Git blob identity, not that the destination replaces it. Base: d0488e18. PR #888: 243f558f; #882: 60896a0d. The rig bake requires none of `lib/` or `support.js`.

| Delivered path | Bytes | Compared content | Reader |
|---|---:|---|---|
| `checks/out/berths.txt` | 1690 | New | Node validation |
| `checks/out/fits.txt` | 3355 | New | Node validation |
| `checks/out/gangways.txt` | 657 | New | Node validation |
| `checks/out/joins.txt` | 933 | New | Node validation |
| `checks/out/sidecars.txt` | 1914 | New | Node validation |
| `checks/out/snow.txt` | 711 | New | Node validation |
| `checks/out/sprites.txt` | 3615 | New | Node validation |
| `checks/out/sums.txt` | 3212 | New | Node validation |
| `checks/run.js` | 3269 | New | Node validation |
| `checks/wharfChecks.js` | 13853 | New | Node validation |
| `gameplay/wharfRig2.blockQuay.gameplay.json` | 26851 | New | Validation now; placement reader later |
| `gameplay/wharfRig2.breakwater.gameplay.json` | 10530 | New | Validation now; placement reader later |
| `gameplay/wharfRig2.breastingDolphin.gameplay.json` | 10696 | New | Validation now; placement reader later |
| `gameplay/wharfRig2.cappedCrib.gameplay.json` | 28650 | New | Validation now; placement reader later |
| `gameplay/wharfRig2.catwalk.gameplay.json` | 8633 | New | Validation now; placement reader later |
| `gameplay/wharfRig2.concreteFloat.gameplay.json` | 40200 | New | Validation now; placement reader later |
| `gameplay/wharfRig2.concreteQuay.gameplay.json` | 27187 | New | Validation now; placement reader later |
| `gameplay/wharfRig2.drumFloat.gameplay.json` | 35079 | New | Validation now; placement reader later |
| `gameplay/wharfRig2.finger.gameplay.json` | 28341 | New | Validation now; placement reader later |
| `gameplay/wharfRig2.gangway.gameplay.json` | 8758 | New | Validation now; placement reader later |
| `gameplay/wharfRig2.graniteEdge.gameplay.json` | 9773 | New | Validation now; placement reader later |
| `gameplay/wharfRig2.harbour-commercial.gameplay.json` | 76988 | New | Validation now; placement reader later |
| `gameplay/wharfRig2.harbour-cove.gameplay.json` | 75301 | New | Validation now; placement reader later |
| `gameplay/wharfRig2.harbour-fishing.gameplay.json` | 130966 | New | Validation now; placement reader later |
| `gameplay/wharfRig2.harbour-jetty.gameplay.json` | 47040 | New | Validation now; placement reader later |
| `gameplay/wharfRig2.logCrib.gameplay.json` | 28644 | New | Validation now; placement reader later |
| `gameplay/wharfRig2.lowPier.gameplay.json` | 42791 | New | Validation now; placement reader later |
| `gameplay/wharfRig2.mooringDolphin.gameplay.json` | 10527 | New | Validation now; placement reader later |
| `gameplay/wharfRig2.plasticFloat.gameplay.json` | 39071 | New | Validation now; placement reader later |
| `gameplay/wharfRig2.redEdge.gameplay.json` | 9769 | New | Validation now; placement reader later |
| `gameplay/wharfRig2.sheetCell.gameplay.json` | 10219 | New | Validation now; placement reader later |
| `gameplay/wharfRig2.sheetedPier.gameplay.json` | 44861 | New | Validation now; placement reader later |
| `gameplay/wharfRig2.slipway.gameplay.json` | 8787 | New | Validation now; placement reader later |
| `gameplay/wharfRig2.steelPier.gameplay.json` | 45136 | New | Validation now; placement reader later |
| `gameplay/wharfRig2.tallPier.gameplay.json` | 60771 | New | Validation now; placement reader later |
| `gameplay/wharfRig2.timberDolphin.gameplay.json` | 8555 | New | Validation now; placement reader later |
| `gameplay/wharfRig2.timberFloat.gameplay.json` | 39060 | New | Validation now; placement reader later |
| `gameplay/wharfRig2.timberQuay.gameplay.json` | 26896 | New | Validation now; placement reader later |
| `gameplay/wharfRig2.torbayQuay.gameplay.json` | 27225 | New | Validation now; placement reader later |
| `lib/characterIsoRig9.js` | 114763 | `docs/art/rigs/character/rig9/Art/characterIsoRig9.js` | Viewer (not required by rig bake) |
| `lib/characterIsoRig9.poses.js` | 49219 | `docs/art/rigs/character/rig9/Art/characterIsoRig9.poses.js` | Viewer (not required by rig bake) |
| `lib/pixelLanguage.js` | 20114 | `docs/art/rigs/px-cliff-face-kit/bake/pixelLanguage.js`<br>`docs/art/rigs/px-greenery-harmony-kit/Art/pixelLanguage.js`<br>`docs/art/rigs/rock-px-kit/bake/pixelLanguage.js`<br>`#888:docs/art/rigs/terrain/pass9/lib/pixelLanguage.js` | Viewer (not required by rig bake) |
| `lib/pxKit.js` | 65989 | `docs/art/rigs/px-greenery-harmony-kit/Art/pxKit.js`<br>`docs/art/rigs/rock-px-kit/bake/pxKit.js`<br>`#888:docs/art/rigs/terrain/pass9/lib/pxKit.js` | Viewer (not required by rig bake) |
| `lib/pxKit2.js` | 48106 | `docs/art/rigs/px-greenery-harmony-kit/Art/pxKit2.js`<br>`docs/art/rigs/rock-px-kit/bake/pxKit2.js`<br>`#888:docs/art/rigs/terrain/pass9/lib/pxKit2.js` | Viewer (not required by rig bake) |
| `lib/pxKit8.js` | 51195 | `#888:docs/art/rigs/terrain/pass9/lib/pxKit8.js` | Viewer (not required by rig bake) |
| `lib/terrainLight4.js` | 25480 | `#888:docs/art/rigs/terrain/pass9/lib/terrainLight4.js` | Viewer (not required by rig bake) |
| `lib/terrainLight5.js` | 24292 | `#888:docs/art/rigs/terrain/pass9/terrainLight5.js` | Viewer (not required by rig bake) |
| `lib/terrainLight6.js` | 25056 | `#888:docs/art/rigs/terrain/pass9/terrainLight6.js` | Viewer (not required by rig bake) |
| `lib/weatherSky.js` | 13650 | `#888:docs/art/rigs/terrain/pass9/lib/weatherSky.js`<br>`#882:docs/art/rigs/weatherSky.js` | Viewer (not required by rig bake) |
| `maps/sprites.json` | 30432 | New | Bake reference / layout |
| `maps/tallPier_d1_detail.png` | 32289 | New | Bake reference / layout |
| `maps/tallPier_d1_height.png` | 53596 | New | Bake reference / layout |
| `maps/tallPier_d1_layer.png` | 19146 | New | Bake reference / layout |
| `maps/tallPier_d1_light.png` | 81629 | New | Bake reference / layout |
| `maps/tallPier_d1_lit.png` | 63394 | New | Bake reference / layout |
| `maps/tallPier_d1_normal.png` | 29270 | New | Bake reference / layout |
| `maps/tallPier_d1_snow.png` | 79209 | New | Bake reference / layout |
| `maps/tallPier_d1_unlit.png` | 48141 | New | Bake reference / layout |
| `maps/timberFloat_d1_height_sheet.png` | 316980 | New | Bake reference / layout |
| `maps/timberFloat_d1_normal_sheet.png` | 156554 | New | Bake reference / layout |
| `maps/timberFloat_d1_snow_sheet.png` | 367894 | New | Bake reference / layout |
| `maps/timberFloat_d1_unlit_sheet.png` | 218181 | New | Bake reference / layout |
| `README.md` | 31877 | New | Documentation / integrity |
| `renders/berths.png` | 59495 | New | Human review |
| `renders/channels-tallPier.png` | 441443 | New | Human review |
| `renders/float-rock-frames-1-5-9-13.png` | 295672 | New | Human review |
| `renders/harbour-cove-golden.png` | 355339 | New | Human review |
| `renders/harbour-fishing-1400.png` | 424560 | New | Human review |
| `renders/harbour-fishing-snow.png` | 311441 | New | Human review |
| `SHA256SUMS.txt` | 7483 | New | Documentation / integrity |
| `support.js` | 69150 | `docs/art/rigs/character/rig9/support.js` | Viewer (not required by rig bake) |
| `Wharf Rig Pass 2.dc.html` | 47446 | New | Viewer |
| `wharfPlates2.js` | 6832 | New | Viewer |
| `wharfRig2.fam.js` | 46201 | New | Editor rig / checks |
| `wharfRig2.geo.js` | 47724 | New | Editor rig / checks |
| `wharfRig2.js` | 39457 | New | Editor rig / checks |
| `wharfRig2.kit.js` | 34737 | New | Editor rig / checks |
| `wharfRig2.verbs.js` | 25920 | New | Editor rig / checks |
| `wharfStage2.js` | 14407 | New | Viewer |
