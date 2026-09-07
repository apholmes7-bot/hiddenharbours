#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Art;
using HiddenHarbours.Fishing;
using HiddenHarbours.Player;

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// <b>The review plates for the catch pass 2 INTEGRATION</b> — what the game will actually draw,
    /// composed from the COMMITTED sliced sheets and the COMMITTED tables.
    ///
    /// <para><b>Why these read the tables and not the rigs.</b> The intake lane's plates were rendered
    /// through the rigs in a browser, which answers "did the art director draw it well". These answer a
    /// different question — "does the game reach it" — so every pixel here is fetched the way the
    /// running game fetches it: fish through <see cref="RodKitImporter.BuildFishSpecies"/> and its size
    /// ladder, items and held art through the built <see cref="CatchItemLibrary"/>. A plate that went
    /// back to the rig could look perfect while the game still drew nothing.</para>
    ///
    /// <para><b>Pixels come from the PNG on disk, cropped by the SPRITE's rect.</b> Sprite textures are
    /// not import-readable and forcing them would dirty import settings for a plate. Loading the file
    /// bytes into a scratch texture and cropping by <c>sprite.rect</c> keeps the slicing under test —
    /// a mis-sliced sheet crops the wrong cell and the plate shows it — without touching the project.</para>
    ///
    /// <para>Run headless:
    /// <c>-executeMethod HiddenHarbours.App.Editor.CatchPass2IntegrationPlates.Cli</c></para>
    /// </summary>
    public static class CatchPass2IntegrationPlates
    {
        const string OutFolder = "docs/art/spikes/catch-pass-2-integration";
        const string FishIso = "Assets/_Project/Art/Fishing/Iso";
        const string Storage = "Assets/_Project/Art/Fishing/Storage";
        const int Dirs = 8;

        static readonly Color32 Bg = new Color32(24, 26, 30, 255);
        static readonly Color32 Ink = new Color32(214, 220, 226, 255);
        static readonly Color32 Dim = new Color32(120, 130, 140, 255);
        static readonly Color32 Mark = new Color32(255, 96, 96, 255);

        [MenuItem("Hidden Harbours/Art/Plates ▸ Catch Pass 2 Integration", priority = 51)]
        public static void Build() => Cli();

        public static void Cli()
        {
            string root = Directory.GetParent(Application.dataPath)!.FullName;
            string outDir = Path.Combine(root, OutFolder);
            Directory.CreateDirectory(outDir);

            int made = 0, failed = 0;
            foreach (var (name, fn) in new (string, System.Func<Plate>)[]
            {
                ("fish-on-the-line", FishOnTheLine),
                ("fish-on-the-deck", FishOnTheDeck),
                ("held-lobster", HeldLobster),
                ("hand-of-clams", HandOfClams),
                ("hod-empty", HodEmpty),
            })
            {
                // Each plate stands alone: one that cannot find its art says so and the rest still
                // render, so a half-run bake produces four useful plates and one loud gap rather than
                // nothing at all.
                try
                {
                    Plate p = fn();
                    if (p == null) { Debug.LogError($"[Plates] '{name}' found no art — SKIPPED."); failed++; continue; }
                    File.WriteAllBytes(Path.Combine(outDir, name + ".png"), p.EncodePng());
                    Debug.Log($"[Plates] {name}.png — {p.W}×{p.H}");
                    made++;
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[Plates] '{name}' FAILED: {ex.Message}\n{ex}");
                    failed++;
                }
            }

            Debug.Log($"[Plates] {made} written, {failed} failed → {OutFolder}");
        }

        // =====================================================================================
        // the plates
        // =====================================================================================

        /// <summary>
        /// Cod on the line at all three rungs, eight headings, with the MOUTH anchor marked in red —
        /// the point the fishing line is tied to. The whole reason the sidecar went rung-major is that
        /// this marker has to sit on the mouth at the small and large rungs too, not just the middle
        /// one it was measured at.
        /// </summary>
        static Plate FishOnTheLine()
        {
            FishSpeciesVisual cod = Species("fish.atlantic_cod");
            if (cod == null || cod.Rungs == null || cod.Rungs.Length == 0) return null;

            const int cell = 64, gap = 4, left = 74, top = 26;
            var plate = new Plate(left + Dirs * (cell + gap), top + cod.Rungs.Length * (cell + gap) + 10);
            plate.Text(4, plate.H - 12, "COD ON THE LINE - DART F0 - RED DOT IS THE BAKED MOUTH", Ink);

            for (int d = 0; d < Dirs; d++)
                plate.Text(left + d * (cell + gap) + 22, plate.H - 22, "D" + d, Dim);

            for (int r = 0; r < cod.Rungs.Length; r++)
            {
                FishRungVisual rung = cod.Rungs[r];
                int y = plate.H - top - (r + 1) * (cell + gap);
                plate.Text(4, y + cell / 2, $"{rung.Kg:0.0}KG", Ink);
                plate.Text(4, y + cell / 2 - 9, rung.TwoHanded ? "CRADLE" : "1 HAND", Dim);

                for (int d = 0; d < Dirs; d++)
                {
                    int x = left + d * (cell + gap);
                    int idx = d * Mathf.Max(1, rung.DartFramesPerDir);
                    if (rung.DartFrames == null || idx >= rung.DartFrames.Length) continue;
                    Sprite s = rung.DartFrames[idx];
                    if (s == null) continue;

                    plate.Blit(s, x, y);

                    // The mouth is published in world metres from the fish pivot; world y is UP and a
                    // texture's y is up too, so the only conversion is the sheet's own PPU.
                    if (rung.DartMouthOffsets != null && idx < rung.DartMouthOffsets.Length)
                    {
                        Vector2 m = rung.DartMouthOffsets[idx];
                        float ppu = s.pixelsPerUnit;
                        int mx = x + Mathf.RoundToInt(s.pivot.x + m.x * ppu);
                        int my = y + Mathf.RoundToInt(s.pivot.y + m.y * ppu);
                        plate.Cross(mx, my, Mark);
                    }
                }
            }
            return plate;
        }

        /// <summary>Every species' deck lay, as the container fill draws it — through the built
        /// library, so this is the table's answer and not the sheet's.</summary>
        static Plate FishOnTheDeck()
        {
            CatchItemLibrary lib = Library();
            if (lib == null) return null;

            string[] kinds = { "cod", "haddock", "pollock", "mackerel", "bass", "flounder", "herring" };
            const int cell = 64, gap = 4, left = 84, top = 26, variants = 4;

            var plate = new Plate(left + variants * (cell + gap), top + kinds.Length * (cell + gap) + 10);
            plate.Text(4, plate.H - 12, "DECK LAYS THROUGH THE BUILT LIBRARY - 4 VARIANTS", Ink);
            for (int v = 0; v < variants; v++)
                plate.Text(left + v * (cell + gap) + 24, plate.H - 22, "V" + v, Dim);

            for (int k = 0; k < kinds.Length; k++)
            {
                int y = plate.H - top - (k + 1) * (cell + gap);
                plate.Text(4, y + cell / 2, kinds[k].ToUpperInvariant(), Ink);
                for (int v = 0; v < variants; v++)
                {
                    Sprite s = lib.SpriteFor(kinds[k], v);
                    if (s != null) plate.Blit(s, left + v * (cell + gap), y);
                }
            }
            return plate;
        }

        /// <summary>A held lobster at eight headings — baked around the BACK-GRIP pivot, and until this
        /// lane the fisher's hand drew a UI icon instead.</summary>
        static Plate HeldLobster() => HeldStrip("lobster", "HELD LOBSTER - CRUST2HELD, 8 HEADINGS, GRIP PIVOT");

        /// <summary>A hand of clams — one facing, because the shellfish rig takes no camera at all.</summary>
        static Plate HandOfClams() => HeldStrip("clam", "HAND OF CLAMS - SHELL2HAND, NON-DIRECTIONAL");

        static Plate HeldStrip(string kind, string title)
        {
            CatchItemLibrary lib = Library();
            if (lib == null) return null;
            int facings = lib.HeldFacingsFor(kind);
            if (facings == 0) return null;

            const int cell = 64, gap = 4, top = 26;
            var plate = new Plate(Mathf.Max(360, facings * (cell + gap) + 8), top + cell + gap + 10);

            int zoom = 1;
            for (int d = 0; d < facings; d++)
            {
                int x = 4 + d * (cell + gap);
                int y = plate.H - top - cell - gap;
                Sprite s = lib.HeldSprite(kind, d, 0);
                if (s == null) continue;
                zoom = Plate.ZoomFor(s, cell);
                plate.Blit(s, x, y, zoom, cell);
                plate.Text(x + 22, plate.H - 22, "D" + d, Dim);
            }

            plate.Text(4, plate.H - 12, zoom > 1 ? $"{title} - SHOWN {zoom}X" : title, Ink);
            return plate;
        }

        /// <summary>
        /// The hod, back layer then front, at eight headings.
        ///
        /// <para>It is EMPTY, and that is the finding rather than a gap in the plate: the pass-2 bake
        /// produced these two layers and a rim quad, and nothing that draws a full one. The fill is a
        /// runtime composition over <c>CatchKit2.heap</c> which no baker ever called.</para>
        /// </summary>
        static Plate HodEmpty()
        {
            Sprite[] back = Cells($"{Storage}/Hod2_back.png");
            Sprite[] front = Cells($"{Storage}/Hod2_front.png");
            if (back.Length == 0 || front.Length == 0) return null;

            const int cell = 40, gap = 4, top = 26;
            var plate = new Plate(Mathf.Max(360, Dirs * (cell + gap) + 8), top + cell + gap + 10);
            plate.Text(4, plate.H - 12, "HOD - BACK + FRONT, 8 HEADINGS. EMPTY: NO FILL WAS BAKED", Ink);

            for (int d = 0; d < Dirs; d++)
            {
                int x = 4 + d * (cell + gap);
                int y = plate.H - top - cell - gap;
                if (d < back.Length) plate.Blit(back[d], x, y);
                if (d < front.Length) plate.Blit(front[d], x, y);
                plate.Text(x + 10, plate.H - 22, "D" + d, Dim);
            }
            return plate;
        }

        // =====================================================================================
        // sources
        // =====================================================================================

        static CatchItemLibrary Library() =>
            AssetDatabase.LoadAssetAtPath<CatchItemLibrary>(CatchItemLibraryBuilder.LibraryPath);

        static FishSpeciesVisual Species(string id)
        {
            var roster = AssetDatabase.FindAssets("t:FishSpeciesDef", new[] { "Assets/_Project/Data/Fish" })
                .Select(g => AssetDatabase.LoadAssetAtPath<FishSpeciesDef>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(d => d != null).ToArray();
            return RodKitImporter.BuildFishSpecies(roster)
                                 .FirstOrDefault(s => s.FishId == id);
        }

        static Sprite[] Cells(string path) =>
            AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>()
                         .OrderBy(s => s.name, System.StringComparer.Ordinal).ToArray();

        // =====================================================================================
        // the canvas
        // =====================================================================================

        sealed class Plate
        {
            public readonly int W, H;
            readonly Color32[] _px;
            static readonly Dictionary<string, Color32[]> _sheets = new Dictionary<string, Color32[]>();
            static readonly Dictionary<string, Vector2Int> _sizes = new Dictionary<string, Vector2Int>();

            public Plate(int w, int h)
            {
                W = w; H = h;
                _px = new Color32[w * h];
                for (int i = 0; i < _px.Length; i++) _px[i] = Bg;
            }

            public byte[] EncodePng()
            {
                var t = new Texture2D(W, H, TextureFormat.RGBA32, false);
                t.SetPixels32(_px);
                t.Apply();
                byte[] png = t.EncodeToPNG();
                Object.DestroyImmediate(t);
                return png;
            }

            /// <summary>
            /// Alpha-composite one sliced cell into the <paramref name="slot"/>-sized box whose
            /// bottom-left is (dx, dy), magnified <paramref name="zoom"/>× with nearest-neighbour and
            /// centred.
            ///
            /// <para><b>Why a plate may magnify.</b> At catch pass 2's strict world scale a handful of
            /// clams is an 8×8 cell and a periwinkle is one pixel. Blitted 1:1 into a 64 px slot that
            /// is a speck in a corner — technically the truth, and useless for the judgement the plate
            /// exists to support. Nearest-neighbour keeps every pixel exactly as baked, and the zoom
            /// factor is printed on the plate so nobody reads it as the shipping size.</para>
            /// </summary>
            public void Blit(Sprite s, int dx, int dy, int zoom = 1, int slot = 0)
            {
                string path = AssetDatabase.GetAssetPath(s);
                if (!_sheets.TryGetValue(path, out Color32[] sheet))
                {
                    // The file's own bytes — sprite textures are not import-readable, and making them
                    // so would dirty import settings for the sake of a plate.
                    var tmp = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    string abs = Path.Combine(Directory.GetParent(Application.dataPath)!.FullName, path);
                    if (!File.Exists(abs) || !tmp.LoadImage(File.ReadAllBytes(abs)))
                    { Object.DestroyImmediate(tmp); return; }
                    sheet = tmp.GetPixels32();
                    _sheets[path] = sheet;
                    _sizes[path] = new Vector2Int(tmp.width, tmp.height);
                    Object.DestroyImmediate(tmp);
                }

                Vector2Int size = _sizes[path];
                Rect r = s.rect;
                int z = Mathf.Max(1, zoom);
                int ox = slot > 0 ? (slot - (int)r.width * z) / 2 : 0;
                int oy = slot > 0 ? (slot - (int)r.height * z) / 2 : 0;

                for (int y = 0; y < (int)r.height; y++)
                for (int x = 0; x < (int)r.width; x++)
                {
                    int sx = (int)r.x + x, sy = (int)r.y + y;
                    if (sx < 0 || sy < 0 || sx >= size.x || sy >= size.y) continue;
                    Color32 c = sheet[sy * size.x + sx];
                    if (c.a == 0) continue;
                    for (int zy = 0; zy < z; zy++)
                    for (int zx = 0; zx < z; zx++)
                        Set(dx + ox + x * z + zx, dy + oy + y * z + zy, c);
                }
            }

            /// <summary>The integer magnification that makes a cell fill <paramref name="slot"/> px
            /// without exceeding it — 1 for anything already big enough.</summary>
            public static int ZoomFor(Sprite s, int slot)
            {
                int big = Mathf.Max((int)s.rect.width, (int)s.rect.height);
                return big <= 0 ? 1 : Mathf.Max(1, slot / big);
            }

            public void Set(int x, int y, Color32 c)
            {
                if (x < 0 || y < 0 || x >= W || y >= H) return;
                int i = y * W + x;
                if (c.a == 255) { _px[i] = c; return; }
                Color32 d = _px[i];
                float a = c.a / 255f;
                _px[i] = new Color32(
                    (byte)(c.r * a + d.r * (1 - a)),
                    (byte)(c.g * a + d.g * (1 - a)),
                    (byte)(c.b * a + d.b * (1 - a)), 255);
            }

            /// <summary>A 3×3 open cross — visible over both dark water and a pale belly.</summary>
            public void Cross(int x, int y, Color32 c)
            {
                for (int i = -3; i <= 3; i++)
                {
                    if (Mathf.Abs(i) > 1) { Set(x + i, y, c); Set(x, y + i, c); }
                }
            }

            public void Text(int x, int y, string s, Color32 c)
            {
                foreach (char ch in s.ToUpperInvariant())
                {
                    byte[] g = Glyph(ch);
                    for (int col = 0; col < 5; col++)
                    for (int row = 0; row < 7; row++)
                        if ((g[col] & (1 << row)) != 0) Set(x + col, y + 6 - row, c);
                    x += 6;
                }
            }

            // A 5×7 column bitmap, LSB = top row. Only the characters these plates use.
            const string Chars = " -._:/0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            static readonly byte[] Font =
            {
                0x00,0x00,0x00,0x00,0x00, 0x08,0x08,0x08,0x08,0x08, 0x00,0x60,0x60,0x00,0x00,
                0x40,0x40,0x40,0x40,0x40, 0x00,0x36,0x36,0x00,0x00, 0x20,0x10,0x08,0x04,0x02,
                0x3E,0x51,0x49,0x45,0x3E, 0x00,0x42,0x7F,0x40,0x00, 0x42,0x61,0x51,0x49,0x46,
                0x21,0x41,0x45,0x4B,0x31, 0x18,0x14,0x12,0x7F,0x10, 0x27,0x45,0x45,0x45,0x39,
                0x3C,0x4A,0x49,0x49,0x30, 0x01,0x71,0x09,0x05,0x03, 0x36,0x49,0x49,0x49,0x36,
                0x06,0x49,0x49,0x29,0x1E, 0x7E,0x11,0x11,0x11,0x7E, 0x7F,0x49,0x49,0x49,0x36,
                0x3E,0x41,0x41,0x41,0x22, 0x7F,0x41,0x41,0x22,0x1C, 0x7F,0x49,0x49,0x49,0x41,
                0x7F,0x09,0x09,0x09,0x01, 0x3E,0x41,0x49,0x49,0x7A, 0x7F,0x08,0x08,0x08,0x7F,
                0x00,0x41,0x7F,0x41,0x00, 0x20,0x40,0x41,0x3F,0x01, 0x7F,0x08,0x14,0x22,0x41,
                0x7F,0x40,0x40,0x40,0x40, 0x7F,0x02,0x0C,0x02,0x7F, 0x7F,0x04,0x08,0x10,0x7F,
                0x3E,0x41,0x41,0x41,0x3E, 0x7F,0x09,0x09,0x09,0x06, 0x3E,0x41,0x51,0x21,0x5E,
                0x7F,0x09,0x19,0x29,0x46, 0x46,0x49,0x49,0x49,0x31, 0x01,0x01,0x7F,0x01,0x01,
                0x3F,0x40,0x40,0x40,0x3F, 0x1F,0x20,0x40,0x20,0x1F, 0x3F,0x40,0x38,0x40,0x3F,
                0x63,0x14,0x08,0x14,0x63, 0x07,0x08,0x70,0x08,0x07, 0x61,0x51,0x49,0x45,0x43,
            };

            static byte[] Glyph(char ch)
            {
                int i = Chars.IndexOf(ch);
                if (i < 0) i = 0;
                var g = new byte[5];
                System.Array.Copy(Font, i * 5, g, 0, 5);
                return g;
            }
        }
    }
}
#endif
