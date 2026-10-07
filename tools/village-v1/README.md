# VILLAGE V1 generator

Run from the repository root with Python 3.9 or later. No Unity or third-party Python packages are used.

```powershell
python tools/village-v1/generate.py --scene artifacts/village-v1/scene.json --report artifacts/village-v1/generator-report.json
python tools/village-v1/generate.py --scene artifacts/village-v1/scene.json --check
```

The plan input is `docs/design/st-peters-village-plan/st-peters-village-plan.json`. The external `--scene` input is the village member of the owner's 2026-10-01 one-scene package, copied into this box's ignored artifacts directory. The generator never opens Unity, extracts a zip, downloads inputs, or reads a save.

It writes 61 Force Text assets: one village plan, nine lots, nine yards, 23 routes, three commons and 16 lights. The plan carries the single nine-row footprint table and all 121 package pieces. Piece metadata retains all seven pending kit asks. Nine stations are offered as data for V2; they do not change routines.

The id map is append-only. Existing old-to-new mappings always win; new position-bearing piece ids receive unused ordinals in package order. A reordered reissue cannot rename an existing piece. Existing meta GUIDs remain authoritative; an initial GUID is derived from the asset's stable path. Source omissions never delete existing assets or mapping rows: a later reissue must review retirements explicitly. The original package ids are preserved only in the map and local evidence.

`--check` reads without writing and reports any generated difference. Its text comparison normalizes checkout line endings, including the JSON map, which has no special `.gitattributes` rule. A normal generation followed by another generation writes identical bytes. Code metas are created only when absent and existing code metas are preserved.

V1 applies the charter's three (a) decisions and the two architecture leans. Its deliberate source reconciliations are:

- Exact plan positions replace the package's two-decimal building and light positions. The two W homes take `jobOneAsk`, with side-entry doors and lanterns retaining their original offset from those doors.
- The store and post-office walks are 1.5 m. Other house walks remain 1.1 m. Both shops' authored gate is on the first walk segment, below its intermediate point; neither point is silently moved.
- Bake keys come from the plan's preset names. The three new-home keys and footprints remain provisional until their V3 bake.
- The fire pit uses the plan's `WindowGlow` reach, 3.4 m; its package metadata retains the differing 5.4 m request. Lamps carry the game's shadow-enabled default so the guards count potential pairs.
- Source fence quantity summaries remain on yards. Package panel anchors remain on their piece rows and drive the potential caster count. Five package panel counts differ from the plan summaries; the report names them.
- The two Shore Walk furniture rows belong to the west-garden collection with explicit Shore Walk route references. This does not claim they lie inside the garden outline.
- Roadside rows retain the source `Road` keys (`bar_head_road` or `slip_road`) and have no V1 `RouteId`. PR 5 B owns those roads; V1 creates no route reference for them.
- One external Ginny window and 12 rejected street-line lamp candidates remain distinct comparison rows. The street-line rows are inactive.

The six types are in `Assets/_Project/Code/World/Village/`. Routes expose `Id`, `Class`, `WidthMetres`, `Points` (world XY), and optional `LotId` for house walks. PR 5 B can consume all 23 routes from `Assets/_Project/Data/Regions/StPetersVillage/` in its single paint pass. Ground-dependent guards remain PR 5 B's work.
