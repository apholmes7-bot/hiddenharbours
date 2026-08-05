#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Art.Editor
{
    /// <summary>
    /// The PLACEMENT half of the interior kit — "what rooms and furniture can I put in a scene, which
    /// sprite is which facing, and what does one of them look like once it is standing there?" Sibling
    /// of <see cref="VillageBuildingCatalog"/>, and the one place the placement pass and the tests go
    /// through so they cannot drift apart.
    ///
    /// <para><b>It owns no numbers.</b> Cell, pivot, facing offset and footprint metres all come from
    /// <see cref="InteriorKit"/> reading the bake's own <c>Interiors.json</c>. This type answers
    /// questions about SPRITES and SCENE OBJECTS only.</para>
    ///
    /// <para><b>What it deliberately does not do: colliders, triggers, enter/exit.</b> Same line the
    /// building catalog draws — that is the gameplay lane's call, after placement, and it lives in
    /// <c>BuildingInterior</c> and the region builder. This file makes a room LOOK right; it does not
    /// decide what walking into one means.</para>
    ///
    /// <para><b>Three things about a room that are not true of other decor:</b></para>
    /// <list type="bullet">
    ///   <item><b>It sorts BELOW the Y-sort band, not inside it.</b> The room sheet is floor and FAR
    ///   walls — the near ones are dropped in the bake — so every pixel of it is spatially behind an
    ///   occupant. A <c>YSortSprite</c> would sort it by its floor CENTRE and hide the player behind
    ///   the floor the moment they walked past the middle of the room.
    ///   <see cref="RoomSortingOrder"/>.</item>
    ///   <item><b>Its furniture sorts normally.</b> Props are separate sprites whose origin is their own
    ///   floor centre, which is exactly what <c>YSortSprite</c> wants, so walking behind the table and
    ///   in front of it both work with no special case.</item>
    ///   <item><b>Turning one means swapping the sub-sprite</b> — the art is baked at a fixed camera, so
    ///   a rotated transform would tip the whole projection.</item>
    /// </list>
    /// </summary>
    public static class InteriorCatalog
    {
        /// <summary>
        /// The room sheet's sorting order: ONE below <c>YSortSprite</c>'s safe band (which starts at 2),
        /// so no occupant — player, NPC or prop — can ever be sorted behind the floor they are standing
        /// on, and above the ground/water bands, which sit at large negative orders.
        ///
        /// <para>It is a fixed order rather than a Y-sorted one on purpose. See the class remarks: the
        /// sheet has no near walls to draw over anybody.</para>
        ///
        /// <para>⚠️ This shares its value with the top of the wharf deck's −4..1 band. Not reachable in
        /// the pilot (a room is inside a building, on land) and worth knowing before the first interior
        /// goes into a wharf shed.</para>
        /// </summary>
        public const int RoomSortingOrder = 1;

        /// <summary>Pre-YSort sorting order for furniture. <c>YSortSprite</c> OWNS the final value; this
        /// is only what a prop shows before that runs. Matches the building kit's seed so the two sets
        /// read alike.</summary>
        public const int PropSortingOrder = 4;

        // =================================================================================
        //  what is placeable
        // =================================================================================

        /// <summary>One placeable room or prop: its contract entry and the questions a placer asks.</summary>
        public readonly struct Placement
        {
            public readonly InteriorKit.Entry Entry;
            public readonly bool IsProp;

            public Placement(InteriorKit.Entry entry, bool isProp) { Entry = entry; IsProp = isProp; }

            public bool IsValid => Entry != null;
            public string Key => Entry.key;

            public string Stem => IsProp
                ? InteriorKit.PropStemFor(Entry.key)
                : InteriorKit.RoomStemFor(Entry.key);

            public string SheetPath => IsProp
                ? InteriorKit.PropSheetPath(Entry.key)
                : InteriorKit.RoomSheetPath(Entry.key);

            /// <summary>Footprint in GROUND metres — the room's <c>Wd</c>/<c>Ln</c>, or the prop's
            /// <c>footprint(name,opts)</c>. Unsquashed, as the rig reports it; the world squash is
            /// <c>InteriorFootprint</c>'s job, not this one's.</summary>
            public Vector2 FootprintMetres => IsProp
                ? new Vector2(Entry.propFootprintWidth, Entry.propFootprintDepth)
                : new Vector2(Entry.footprintWidthMetres, Entry.footprintLengthMetres);

            public string Label =>
                $"{Entry.label} — {FootprintMetres.x:0.##} × {FootprintMetres.y:0.##} m";
        }

        /// <summary>Every placeable room the committed contract claims, in the kit's own order. Returns
        /// empty (never throws) when the contract is missing, so a tool can say so rather than fall
        /// over.</summary>
        public static List<Placement> ScanRooms() => Scan(InteriorKit.Load()?.rooms, isProp: false);

        /// <summary>Every placeable prop.</summary>
        public static List<Placement> ScanProps() => Scan(InteriorKit.Load()?.props, isProp: true);

        static List<Placement> Scan(InteriorKit.Entry[] entries, bool isProp)
        {
            var list = new List<Placement>();
            if (entries == null) return list;
            foreach (var e in entries) if (e != null) list.Add(new Placement(e, isProp));
            return list;
        }

        /// <summary>One room by key, or an invalid placement if the bake did not cover it.</summary>
        public static Placement FindRoom(string key) => Find(ScanRooms(), key);

        /// <summary>One prop by key, or an invalid placement.</summary>
        public static Placement FindProp(string key) => Find(ScanProps(), key);

        static Placement Find(List<Placement> all, string key)
        {
            foreach (var p in all)
                if (string.Equals(p.Key, key, StringComparison.Ordinal)) return p;
            return default;
        }

        // =================================================================================
        //  sprites
        // =================================================================================

        /// <summary>
        /// One facing's sprite, matched BY NAME (never by <see cref="AssetDatabase.LoadAllAssetsAtPath"/>'s
        /// array order, which Unity does not promise). The sheets import spriteMode MULTIPLE, so a plain
        /// <c>LoadAssetAtPath&lt;Sprite&gt;</c> returns null for them.
        ///
        /// <para>Returns null (no throw) when the sheet is missing or unsliced, so a partial art state
        /// still places what it can.</para>
        /// </summary>
        public static Sprite LoadFacing(Placement placement, int facing)
        {
            if (!placement.IsValid || facing < 0 || facing >= placement.Entry.facings) return null;

            string wanted = placement.IsProp
                ? InteriorKit.PropSpriteNameFor(placement.Entry.key, facing)
                : InteriorKit.RoomSpriteNameFor(placement.Entry.key, facing);

            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(placement.SheetPath))
                if (o is Sprite s && string.Equals(s.name, wanted, StringComparison.Ordinal))
                    return s;
            return null;
        }

        // =================================================================================
        //  the canonical scene objects
        // =================================================================================

        /// <summary>
        /// Turn <paramref name="go"/> into the canonical ROOM sprite: a <see cref="SpriteRenderer"/> at
        /// <see cref="RoomSortingOrder"/>, honest metric scale, pinned rotation, and <b>no</b>
        /// <see cref="YSortSprite"/> — see the class remarks for why a floor must not Y-sort.
        ///
        /// <para>Returns the renderer so the caller can hand it to <c>BuildingInterior</c>, which is what
        /// switches it on and off at the threshold. It starts DISABLED: a room nobody is standing in is
        /// a room you should not be able to see through the walls of.</para>
        /// </summary>
        public static SpriteRenderer ConfigureRoom(GameObject go, Placement placement, Sprite sprite)
        {
            var sr = Configure(go, placement, sprite, RoomSortingOrder, ySort: false);
            sr.enabled = false;
            return sr;
        }

        /// <summary>
        /// Turn <paramref name="go"/> into a canonical FURNITURE sprite: a
        /// <see cref="SpriteRenderer"/> seeded at <see cref="PropSortingOrder"/> plus a
        /// <see cref="YSortSprite"/>, which recomputes the order from world Y the moment it is enabled.
        /// Static, like all the other decor — a chair does not move, so it sorts once and stands its
        /// per-frame dispatch down.
        /// </summary>
        public static SpriteRenderer ConfigureProp(GameObject go, Placement placement, Sprite sprite) =>
            Configure(go, placement, sprite, PropSortingOrder, ySort: true);

        static SpriteRenderer Configure(GameObject go, Placement placement, Sprite sprite,
                                        int sortingOrder, bool ySort)
        {
            if (go == null) throw new ArgumentNullException(nameof(go));

            if (!placement.IsValid)
                throw new ArgumentException(
                    "Configure was handed a placement with no contract entry, so there is no pivot, no " +
                    "footprint and no facing count to give it. Placing on a plausible default is " +
                    "exactly the reading this kit's contract replaces — fix the caller.",
                    nameof(placement));

            // ⚠️ `GetComponent<T>() ?? AddComponent<T>()` LOOKS right and is wrong: `??` tests the real
            // C# reference, so it never reaches UnityEngine.Object's overloaded `==` and a "fake null"
            // component satisfies it. Always compare with `== null`.
            var sr = go.GetComponent<SpriteRenderer>();
            if (sr == null) sr = go.AddComponent<SpriteRenderer>();

            sr.sprite = sprite;
            sr.sortingOrder = sortingOrder;

            // Honest metric size. The rig baked the camera's foreshortening in at 32 px = 1 m, so
            // scaling to the footprint metres is the tempting wrong move — it would break the pixel grid
            // and the scale ladder in one go.
            go.transform.localScale = Vector3.one;

            // Baked at a FIXED camera: rotating the transform would tip the projection. Turning is a
            // sub-sprite swap, never a rotation.
            go.transform.localRotation = Quaternion.identity;

            if (ySort && go.GetComponent<YSortSprite>() == null) go.AddComponent<YSortSprite>();

            return sr;
        }

        // =================================================================================
        //  measurements a placer reports (all derived, none stored)
        // =================================================================================

        /// <summary>PPU from the bake's own contract — the number the sheets were baked at, not the
        /// import default, so the two cannot disagree silently.</summary>
        public static int PixelsPerUnit()
        {
            InteriorKit.Contract c = InteriorKit.Load();
            return c != null && c.ppu > 0 ? c.ppu : (int)ArtImportPipeline.PixelsPerUnit;
        }

        /// <summary>⭐ The interior facing that stands under an exterior facing, read off the contract's
        /// MEASURED offset. The one call anything placing a room under a shell should make.</summary>
        public static int InteriorFacingFor(int exteriorFacing) =>
            InteriorKit.InteriorFacingFor(InteriorKit.Load(), exteriorFacing);

        /// <summary>
        /// A one-line report of what is placeable, for a tool's info box and the builder's log: how many
        /// rooms and props, the biggest sheet against the import cap, and the measured facing offset —
        /// which is the number a reader most wants to see confirmed.
        /// </summary>
        public static string Summary()
        {
            InteriorKit.Contract c = InteriorKit.Load();
            if (c == null) return "No interiors baked.";

            int rooms = c.rooms?.Length ?? 0, props = c.props?.Length ?? 0;
            int widest = 0, tallest = 0;
            foreach (var e in c.rooms ?? Array.Empty<InteriorKit.Entry>())
            { widest = Mathf.Max(widest, e.sheetW); tallest = Mathf.Max(tallest, e.sheetH); }
            foreach (var e in c.props ?? Array.Empty<InteriorKit.Entry>())
            { widest = Mathf.Max(widest, e.sheetW); tallest = Mathf.Max(tallest, e.sheetH); }

            return $"{rooms} room(s) and {props} prop(s) at {c.facings} facings each; interior facing = " +
                   $"exterior facing + {c.exteriorFacingOffset} (MEASURED at bake time); biggest sheet " +
                   $"{widest}×{tallest} px against the {InteriorKit.ImportSizeCap} px cap; " +
                   $"{InteriorKit.TotalPngMib(c):0.00} MiB of PNG, " +
                   $"{InteriorKit.TotalRuntimeMib(c):0.00} MiB RGBA32 at runtime.";
        }
    }
}
#endif
