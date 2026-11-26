#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using HiddenHarbours.World;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.App.Editor
{
    // The Harbour Stack and the three ledges of the Whelps: the forms the lookout's ground names, each a
    // TidalFormDef, drawn by tools/rig-recipes/whelp-forms.mjs and stood under the Whelps' root beside the kit
    // rocks that sit on them.
    //
    // A form is drawn in pieces (the recipe says which): the stack is its body, standing in every tide, and the
    // plinth on the seabed under it; a ledge is one seabed piece. A piece is two pictures on one spot, as a kit
    // rock is: the dry stone, cut one wet band above the sea, and under it the wet stone, cut where the sea
    // stands. Both are cut by the piece's own height sheet, which holds true heights (the recipe builds the form
    // from them), so no scaling is needed. The stack carries the foot a hull cannot pass; the ledges carry none,
    // since there a hull meets the ground's own depth.
    //
    // The sun: only the stack's body throws a shadow, from its own foot (its picture's pivot). The seabed pieces
    // and every wet under-picture are named as what they are (Seabed..., ...WetShade), which the sun caster's
    // rules already leave alone, so one stone throws one shadow and nothing under the sea throws any.
    public static partial class StPetersLayerRefresh
    {
        public const string WhelpFormsContractPath = WhelpsHeights + "/Forms.json";

        /// <summary>The forms' contract (tools/rig-recipes/whelp-forms.mjs writes it).</summary>
        [Serializable]
        public sealed class TidalFormsContract
        {
            public float pixelsPerMetre, liftPerMetre;
            public Form[] forms;

            [Serializable]
            public sealed class Form
            {
                public string id, kind;
                public float floorMetres, rangeMetres, tallestMetres, sortYOffset;
                public int[] pixels;
                public float[] foot;
                public Piece[] pieces;
            }

            [Serializable]
            public sealed class Piece
            {
                public string name, stem;
                public float[] pivot;
                public float offsetY;
            }

            public static TidalFormsContract Parse(string json)
            {
                var c = JsonUtility.FromJson<TidalFormsContract>(json);
                if (c == null || c.forms == null || c.forms.Length == 0)
                    throw new Refusal($"{WhelpsStep}: the forms' contract names no form.");
                foreach (Form f in c.forms)
                    if (string.IsNullOrEmpty(f.id) || !(f.rangeMetres > 0f) || f.pieces == null || f.pieces.Length == 0 ||
                        f.pieces.Any(q => string.IsNullOrEmpty(q.name) || string.IsNullOrEmpty(q.stem)) ||
                        f.foot == null || f.foot.Length < 6 || f.foot.Length % 2 != 0)
                        throw new Refusal($"{WhelpsStep}: the forms' contract holds a form with no id, pieces, range or foot.");
                return c;
            }
        }

        /// <summary>One form to stand in the water.</summary>
        public sealed class TidalForm
        {
            public string Id;
            public Vector2 At;
            public float FloorMetres, RangeMetres, WetBandMetres, SortYOffset;
            public List<TidalFormPiece> Pieces = new List<TidalFormPiece>();
            /// <summary>The foot a hull cannot pass, about <see cref="At"/>; null where a hull may pass.</summary>
            public Vector2[] Foot;
        }

        /// <summary>One piece of a form: its pictures, its heights, and where its pivot lies from the form's centre.</summary>
        public sealed class TidalFormPiece
        {
            public string Name;
            public float OffsetY;
            public SpriteRef Wet, Dry;
            public ObjRef Height;
        }

        /// <summary>What a piece's wet under-picture is called, after the piece.</summary>
        public const string TidalFormWetSuffix = "WetShade";

        /// <summary>How a form's height sheet is read back: (floor, metres a full-white pixel means, lift).</summary>
        public static Vector3 TidalFormDecode(float floorMetres, float rangeMetres, float liftMetres)
        {
            if (!(rangeMetres > 0f)) throw new Refusal($"{WhelpsStep}: a form with no height cannot stand in the tide.");
            return new Vector3(floorMetres, rangeMetres, liftMetres);
        }

        /// <summary>The form's documents, added to the Whelps' patch; returns its transform for the root's list.</summary>
        static long WantTidalForm(TidalForm form, Func<string, string, long> next, List<Wanted> wanted, long rootTr,
            ITidalRockAssets assets)
        {
            string name = form.Id;
            long go = next(name, "GameObject"), tr = next(name, "Transform");
            long foot = form.Foot == null ? 0 : next(name, "PolygonCollider2D");
            // The dry picture takes the order its front edge earns; the wet one lies just under it.
            int order = YSortSprite.OrderFor(form.At.y + form.SortYOffset, SortingBands.DecorBase, SortingBands.OrdersPerMetre,
                SortingBands.DecorFloor + 1, SortingBands.DecorCeiling - 1);
            var parts = new List<long> { tr };
            if (foot != 0) parts.Add(foot);
            var pieceTransforms = new List<long>();
            foreach (TidalFormPiece piece in form.Pieces) pieceTransforms.Add(next(name + "/" + piece.Name, "Transform"));

            wanted.Add(Want(GameObjectYaml(go, name, parts), name, "GameObject"));
            wanted.Add(Want(TransformYaml(tr, go, form.At, pieceTransforms, rootTr), name, "Transform"));
            if (foot != 0)
                wanted.Add(Want(PolygonColliderYaml(foot, go, new List<Vector2[]> { form.Foot }), name, "PolygonCollider2D"));

            for (int i = 0; i < form.Pieces.Count; i++)
            {
                TidalFormPiece piece = form.Pieces[i];
                string dry = name + "/" + piece.Name, wet = dry + "/" + piece.Name + TidalFormWetSuffix;
                long dg = next(dry, "GameObject"), dt = pieceTransforms[i], dsr = next(dry, "SpriteRenderer");
                long sort = next(dry, "YSortSprite"), dface = next(dry, "TidalFaceWaterline");
                long wg = next(wet, "GameObject"), wt = next(wet, "Transform"), wsr = next(wet, "SpriteRenderer");
                long wface = next(wet, "TidalFaceWaterline");

                wanted.Add(Want(GameObjectYaml(dg, piece.Name, new[] { dt, dsr, sort, dface }), dry, "GameObject"));
                wanted.Add(Want(TransformYaml(dt, dg, new Vector2(0f, piece.OffsetY), new[] { wt }, tr), dry, "Transform"));
                wanted.Add(Want(SpriteRendererYaml(dsr, dg, assets.FaceMaterial, piece.Dry, order), dry, "SpriteRenderer"));
                wanted.Add(Want(MonoBehaviourYaml(sort, dg, assets.ScriptFor(typeof(YSortSprite)), new[]
                {
                    "  _dynamic: 0", "  _baseOrder: " + F(SortingBands.DecorBase),
                    "  _orderPerUnit: " + F(SortingBands.OrdersPerMetre), "  _minOrder: " + Int(SortingBands.DecorFloor + 1),
                    "  _maxOrder: " + Int(SortingBands.DecorCeiling - 1), "  _pivotYOffset: " + F(form.SortYOffset - piece.OffsetY),
                }), dry, "YSortSprite"));
                wanted.Add(Want(MonoBehaviourYaml(dface, dg, assets.ScriptFor(typeof(TidalFaceWaterline)),
                    TidalFaceFields(piece.Height, TidalFormDecode(form.FloorMetres, form.RangeMetres, form.WetBandMetres))),
                    dry, "TidalFaceWaterline"));

                wanted.Add(Want(GameObjectYaml(wg, piece.Name + TidalFormWetSuffix, new[] { wt, wsr, wface }), wet, "GameObject"));
                wanted.Add(Want(TransformYaml(wt, wg, Vector2.zero, new List<long>(), dt), wet, "Transform"));
                wanted.Add(Want(SpriteRendererYaml(wsr, wg, assets.FaceMaterial, piece.Wet, order - 1), wet, "SpriteRenderer"));
                wanted.Add(Want(MonoBehaviourYaml(wface, wg, assets.ScriptFor(typeof(TidalFaceWaterline)),
                    TidalFaceFields(piece.Height, TidalFormDecode(form.FloorMetres, form.RangeMetres, 0f))), wet, "TidalFaceWaterline"));
            }
            return tr;
        }

        public static TidalFormsContract LoadTidalFormsContract() =>
            TidalFormsContract.Parse(File.ReadAllText(WhelpFormsContractPath));

        /// <summary>The forms as their Defs and the recipe's contract have them, in id order.</summary>
        public static List<TidalForm> LoadTidalForms(TidalFormsContract contract)
        {
            var forms = new List<TidalForm>();
            foreach (string guid in AssetDatabase.FindAssets("t:" + nameof(TidalFormDef), new[] { WhelpsFolder }))
            {
                var def = AssetDatabase.LoadAssetAtPath<TidalFormDef>(AssetDatabase.GUIDToAssetPath(guid));
                if (def == null) continue;
                TidalFormsContract.Form drawn = contract.forms.SingleOrDefault(f => f.id == def.Id)
                    ?? throw new Refusal($"{WhelpsStep}: {def.Id} is not drawn (no entry in {WhelpFormsContractPath}).");
                if (drawn.pieces[0].stem != def.ArtStem)
                    throw new Refusal($"{WhelpsStep}: {def.Id} names '{def.ArtStem}', its pictures are '{drawn.pieces[0].stem}'.");
                Sprite Picture(string file) => AssetDatabase.LoadAssetAtPath<Sprite>(WhelpsHeights + "/" + file)
                    ?? throw new Refusal($"{WhelpsStep}: {def.Id}: {file} is not an imported picture.");
                var pieces = new List<TidalFormPiece>();
                foreach (TidalFormsContract.Piece q in drawn.pieces)
                {
                    string heightGuid = AssetDatabase.AssetPathToGUID(WhelpsHeights + "/" + q.stem + "_height.png");
                    if (string.IsNullOrEmpty(heightGuid)) throw new Refusal($"{WhelpsStep}: {def.Id}: {q.stem} has no height sheet.");
                    pieces.Add(new TidalFormPiece
                    {
                        Name = q.name, OffsetY = q.offsetY,
                        Wet = EditorShoreRockSprites.RefOf(Picture(q.stem + "_wet.png")),
                        Dry = EditorShoreRockSprites.RefOf(Picture(q.stem + ".png")),
                        Height = new ObjRef(2800000, heightGuid, 3),
                    });
                }
                Vector2[] foot = null;
                if (def.BlocksBoats)
                {
                    foot = new Vector2[drawn.foot.Length / 2];
                    for (int i = 0; i < foot.Length; i++) foot[i] = new Vector2(drawn.foot[2 * i], drawn.foot[2 * i + 1]);
                }
                forms.Add(new TidalForm
                {
                    Id = def.Id, At = def.At, FloorMetres = drawn.floorMetres, RangeMetres = drawn.rangeMetres,
                    WetBandMetres = def.WetBandMetres, SortYOffset = drawn.sortYOffset, Pieces = pieces, Foot = foot,
                });
            }
            forms.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
            if (forms.Select(f => f.Id).Distinct(StringComparer.Ordinal).Count() != forms.Count)
                throw new Refusal($"{WhelpsStep}: forms have duplicate ids.");
            return forms;
        }
    }
}
#endif
