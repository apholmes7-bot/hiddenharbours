#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Player;

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// <b>THE EYEBALL FOR A CARRIED THING</b> — renders the fisher holding a clam, a fish and the rod at
    /// all eight headings, twice: with the hand-prop table and without it. The pair IS the change, so it
    /// can be looked at rather than argued about from a table of numbers.
    ///
    /// <para><b>What it shows that a test cannot.</b> The suites pin that the pin equals the wrist plus
    /// the grip and that the draw order is the row's. They cannot say whether a clam held at north
    /// actually READS as being in her hand, or whether the item that used to disappear behind her now
    /// clears the silhouette. Those are the owner's call and he needs the picture.</para>
    ///
    /// <para><b>⚠️ The drawn CELL is stated, not played.</b> <see cref="IsoCharacterSprite"/> resolves its
    /// facing row and frame in <c>LateUpdate</c>, which does not run outside play mode — so a proof that
    /// merely placed a fisher would render every column at row 0. The row/frame/heading are therefore set
    /// directly, exactly as <see cref="ShopProof"/> switches the two renderers <c>BuildingInterior</c>
    /// would have switched, and for the same reason. Everything downstream of that input is the REAL
    /// runtime: the body cell comes from the real <see cref="CharacterVisualDef"/> and the pose from the
    /// real <see cref="CarryHands.ApplyCarriedPose"/>. That the drawn cell FOLLOWS the fisher is what the
    /// PlayMode twin proves.</para>
    ///
    /// <para><b>⚠️ Renders whatever is on disk.</b> Build the carry-anchor table first
    /// (<see cref="CarryAnchorTableBuilder"/>) or the "after" row is a second copy of the "before" one.</para>
    /// </summary>
    public static class CarryProof
    {
        const string MenuPath = "Hidden Harbours/Art/Proof/Carry Anchor Proof";
        const string OutputFolder = "artifacts/carry-proof";
        const string LogTag = "[CarryProof]";
        const string IsoFisherVisual = "Assets/_Project/Data/Characters/FisherIso.asset";
        const string RodDef = "Assets/_Project/Data/Tools/Rod.asset";
        const string RodSprite = "Assets/_Project/Art/Sprites/Gear/Rod.png";

        /// <summary>Pixels per world unit. 96 keeps a ~1.9 m tall column readable at a glance — this is a
        /// close-up of a hand, not a wide shot of a wharf.</summary>
        const int PixelsPerUnit = 96;

        const int Directions = 8;
        static readonly string[] Facings = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };

        /// <summary>Column pitch and row pitch in metres — wide enough that neighbouring fishers never
        /// overlap at any heading, tight enough that eight fit in one readable sheet.</summary>
        const float ColumnMetres = 1.5f;
        const float RowMetres = 2.6f;

        [MenuItem(MenuPath, priority = 400)]
        public static void Bake()
        {
            var visual = AssetDatabase.LoadAssetAtPath<CharacterVisualDef>(IsoFisherVisual);
            if (visual == null || !visual.HasAnyArt())
            {
                Debug.LogError($"{LogTag} No usable character skin at '{IsoFisherVisual}' — nothing to " +
                               "photograph. Run Build Character Visual Defs first.");
                return;
            }

            var table = AssetDatabase.LoadAssetAtPath<CarryAnchorTableDef>(CarryAnchorTableBuilder.TablePath);
            if (table == null || !table.HasRows)
            {
                Debug.LogError($"{LogTag} No carry-anchor table at '{CarryAnchorTableBuilder.TablePath}' " +
                               "— the 'after' row would be a second copy of 'before'. Run Build Carry " +
                               "Anchor Table first.");
                return;
            }

            // The catch icons are published at BeforeSceneLoad in a build; nothing has done it here, and
            // without them a carried clam draws no sprite at all and the sheet would prove nothing.
            var icons = Resources.Load<IconLibrary>(IconRegistrar.ResourcesPath);
            if (icons != null) icons.RegisterAll();
            else Debug.LogWarning($"{LogTag} No IconLibrary in Resources — carried catches will be blank.");

            Directory.CreateDirectory(OutputFolder);

            int written = 0;
            written += Sheet(visual, table, "clam", Catch("fish.soft_shell_clam", "Soft-shell Clam",
                                                          FishCategory.Shellfish, 0.15f)) ? 1 : 0;
            written += Sheet(visual, table, "fish", Catch("fish.mackerel", "Mackerel",
                                                          FishCategory.Pelagic, 0.6f)) ? 1 : 0;
            written += Sheet(visual, table, "rod", null) ? 1 : 0;

            Debug.Log($"{LogTag} wrote {written} sheet(s) to {OutputFolder}/. Each is 2 rows x 8 columns: " +
                      "TOP = the single hip offset that shipped before, BOTTOM = the rig's hand-prop row. " +
                      "Columns run N, NE, E, SE, S, SW, W, NW left to right.");
        }

        static CatchItem Catch(string id, string name, FishCategory cat, float kg)
            => new CatchItem(id, name, cat, kg, 3, 0.1f, 0.4f, Freshness.Landed(0d));

        /// <summary>One subject's sheet: the fallback row above, the table row below, eight facings each.</summary>
        static bool Sheet(CharacterVisualDef visual, CarryAnchorTableDef table, string name, CatchItem? item)
        {
            var root = new GameObject($"__carryProof_{name}");
            try
            {
                // No Light2D and no material override, deliberately: this project's sprites are
                // Sprite-UNLIT and night is a full-screen multiply overlay (ADR 0016), so a light here
                // would do nothing and a forced material would photograph something the game does not
                // draw. Every renderer below takes the same default the shipped player's does.
                for (int row = 0; row < 2; row++)
                {
                    bool withTable = row == 1;
                    for (int d = 0; d < Directions; d++)
                    {
                        var pos = new Vector2(d * ColumnMetres, -row * RowMetres);
                        if (!Column(root.transform, visual, withTable ? table : null, name, item, d, pos))
                            return false;
                    }
                }

                float width = (Directions - 1) * ColumnMetres + 1.4f;
                float height = RowMetres + 2.2f;
                var centre = new Vector2((Directions - 1) * ColumnMetres * 0.5f, -RowMetres * 0.5f + 0.4f);

                // ⚠️ The first render after building a scene drops sprites, silently (measured on the shop
                // proofs 2026-08-12). Warm it, then keep the second.
                Shot(null, null, centre, width, height);
                return Shot(Path.Combine(OutputFolder, $"{name}-before-after.png"), name,
                            centre, width, height);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>One fisher, drawn at one facing, holding the subject — posed by the REAL hands.</summary>
        static bool Column(Transform parent, CharacterVisualDef visual, CarryAnchorTableDef table,
                           string subject, CatchItem? item, int facingRow, Vector2 at)
        {
            var go = new GameObject($"{subject}_d{facingRow}");
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(at.x, at.y, 0f);

            var body = go.AddComponent<SpriteRenderer>();
            body.sprite = visual.SpriteFor(CharacterGait.Idle, facingRow, 0);
            body.sortingOrder = 100;
            if (body.sprite == null)
            {
                Debug.LogError($"{LogTag} the character skin has no idle cell at row {facingRow} — the " +
                               "sheet would be a picture of a missing body.");
                return false;
            }

            var skin = go.AddComponent<IsoCharacterSprite>();
            skin.Configure(visual);
            StateDrawnCell(skin, facingRow);

            var hands = go.AddComponent<CarryHands>();
            hands.ConfigureCarryAnchors(table);

            if (item.HasValue)
            {
                if (!hands.TryPutInHand(item.Value))
                {
                    Debug.LogError($"{LogTag} the hands refused the catch at row {facingRow}.");
                    return false;
                }
            }
            else if (hands.TryPickUp(Rod(go.transform)) != CarryRefusal.None)
            {
                Debug.LogError($"{LogTag} the hands refused the rod at row {facingRow}.");
                return false;
            }

            // The real pose, from the real component. Everything above is only the input it would have
            // read off a running frame loop.
            hands.ApplyCarriedPose();
            return true;
        }

        /// <summary>A carried rod, wired the way the region builder wires one.</summary>
        static CarriableTool Rod(Transform parent)
        {
            var def = AssetDatabase.LoadAssetAtPath<ToolDef>(RodDef);
            var go = new GameObject("Rod");
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(RodSprite);
            var tool = go.AddComponent<CarriableTool>();
            tool.Configure(def, sr, "tool.proof.rod");
            return tool;
        }

        /// <summary>See the class remarks: the drawn cell is stated because nothing resolves it outside
        /// play mode. Row, frame and heading together, so the two pose paths are asked about the same
        /// picture — the fallback path reads the HEADING and the table path reads the ROW, and a sheet
        /// where those disagreed would be comparing two different fishers.</summary>
        static void StateDrawnCell(IsoCharacterSprite skin, int facingRow)
        {
            const BindingFlags F = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(IsoCharacterSprite).GetField("_facingRow", F).SetValue(skin, facingRow);
            typeof(IsoCharacterSprite).GetField("_frame", F).SetValue(skin, 0);
            typeof(IsoCharacterSprite).GetField("_headingDegrees", F).SetValue(skin, facingRow * 45f);
        }

        /// <summary>Render one orthographic frame and write it. A null path renders and discards.</summary>
        static bool Shot(string path, string name, Vector2 centre, float worldWidth, float worldHeight)
        {
            var camGo = new GameObject("__proofCamera");
            try
            {
                var cam = camGo.AddComponent<Camera>();
                cam.orthographic = true;
                cam.orthographicSize = worldHeight * 0.5f;
                cam.transform.position = new Vector3(centre.x, centre.y, -50f);
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.10f, 0.12f, 0.15f, 1f);
                cam.nearClipPlane = 0.1f;
                cam.farClipPlane = 200f;
                cam.aspect = worldWidth / worldHeight;

                int h = Mathf.RoundToInt(worldHeight * PixelsPerUnit);
                int w = Mathf.RoundToInt(worldWidth * PixelsPerUnit);
                if (w <= 0 || h <= 0) return false;

                var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { useMipMap = false };
                var tex = new Texture2D(w, h, TextureFormat.RGBA32, mipChain: false);
                RenderTexture prev = RenderTexture.active;
                try
                {
                    cam.targetTexture = rt;
                    cam.Render();
                    RenderTexture.active = rt;
                    tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                    tex.Apply(false, false);

                    if (path == null) return true;                 // the warm-up

                    File.WriteAllBytes(path, tex.EncodeToPNG());
                    Debug.Log($"{LogTag} {path} ({w}x{h}) — {name}: top row = the pre-table hip offset, " +
                              "bottom row = the rig's hand-prop row, N..NW left to right.");
                    return true;
                }
                finally
                {
                    RenderTexture.active = prev;
                    cam.targetTexture = null;
                    Object.DestroyImmediate(tex);
                    rt.Release();
                    Object.DestroyImmediate(rt);
                }
            }
            finally
            {
                Object.DestroyImmediate(camGo);
            }
        }
    }
}
#endif
