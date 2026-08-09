using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using HiddenHarbours.Art;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// <b>THE PROMISE THE BATCHING HAD TO KEEP: a merged tuft bends exactly as the sprite did.</b>
    ///
    /// <para>The meadow moved from a <see cref="SpriteRenderer"/> per tuft to merged chunk meshes, and
    /// <c>HiddenHarboursGrass.shader</c> was not touched — not a line. That is only defensible if a batched
    /// tuft hands the shader the same inputs a sprite did, because the shader's wind and footstep terms read
    /// exactly two things: <b>the vertex's world position</b> and <b>the sprite's own uv</b>. So these tests
    /// check the INPUTS, which is a claim a headless run can actually settle, rather than eyeballing two
    /// screenshots and calling them the same.</para>
    ///
    /// <para><b>⚠ AND THEN THEY SABOTAGE IT — zeroing a STRENGTH, never a speed.</b> A guard that "proves"
    /// the wind by zeroing <c>_SwaySpeed</c> proves nothing: kill the speed and both the steady lean and the
    /// spatial gust pattern survive, so a dead shader passes. The amplitude
    /// (<c>_IdleSway + windStrength × _SwayAmount</c>) multiplies the whole offset, so zeroing THAT must
    /// take the bend to exactly zero everywhere and restoring it must bring the bend back. The asymmetry
    /// between those two is the proof.</para>
    ///
    /// <para><b>⚠ WHAT THESE TESTS CANNOT TELL YOU.</b> Nothing here renders a pixel — CI has no graphics
    /// device and this checkout has no GPU. They pin the inputs, the sorting arithmetic and the shader's
    /// algebra; whether the meadow LOOKS right on the owner's 4060 is a screenshot pair, and it is still
    /// owed. Same honesty <c>StPetersGroundCoverBudgetTests</c> states about frame time.</para>
    /// </summary>
    public class GrassFieldWindParityTests
    {
        const string GrassShaderPath = "Assets/_Project/Art/Shaders/HiddenHarboursGrass.shader";
        const string GrassMaterialPath = "Assets/_Project/Art/Materials/Grass.mat";

        readonly List<Object> _spawned = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < _spawned.Count; i++)
                if (_spawned[i] != null) Object.DestroyImmediate(_spawned[i]);
            _spawned.Clear();
        }

        // =====================================================================================
        // fixture
        // =====================================================================================

        const int CellsX = 9, CellsY = 7, Slots = 2;
        const float CellSize = 0.85f;

        Sprite MakeSprite(string name, int w, int h)
        {
            var tex = new Texture2D(w, h) { name = name + "_tex" };
            var sprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0f), 32f);
            sprite.name = name;
            _spawned.Add(sprite);
            _spawned.Add(tex);
            return sprite;
        }

        Material GrassMaterial()
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(GrassMaterialPath);
            if (mat != null) return mat;
            // The geometry these tests check does not depend on the material, but a chunk is only built
            // when one is bound — so fall back rather than skip the whole file in a bare checkout.
            var fallback = new Material(Shader.Find("Sprites/Default")) { name = "GrassFallback" };
            _spawned.Add(fallback);
            return fallback;
        }

        GrassField MakeField(int rowOrderSteps)
        {
            var go = new GameObject("GrassField_parity");
            _spawned.Add(go);
            var field = go.AddComponent<GrassField>();

            var variants = new[] { MakeSprite("A", 32, 32), MakeSprite("B", 32, 48), MakeSprite("C", 64, 32) };
            var pools = new[]
            {
                new GrassField.HabitatPool { Name = "one", Pool = new[] { 0, 1 }, BroadPool = new[] { 2 } },
                new GrassField.HabitatPool { Name = "two", Pool = new[] { 1, 2 }, BroadPool = new[] { 2 } },
            };

            var plane = new byte[CellsX * CellsY * Slots];
            for (int iy = 0; iy < CellsY; iy++)
            for (int ix = 0; ix < CellsX; ix++)
            for (int s = 0; s < Slots; s++)
            {
                if ((ix + iy + s) % 4 == 0) continue;
                plane[(iy * CellsX + ix) * Slots + s] =
                    GrassFieldScatter.PackSlot(((ix + iy) % 2) + 1, broad: (ix % 3) == 0);
            }

            if (rowOrderSteps > 0)
            {
                var so = new SerializedObject(field);
                so.FindProperty("_rowOrderSteps").intValue = rowOrderSteps;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            field.SetField(
                new Vector2(-4f, -3f), CellSize, CellSize * 0.41f, CellSize * 0.32f,
                CellsX, CellsY, Slots, seed: 0, sitePlane: plane,
                strawCellSize: 4f, strawCellsX: 8, strawCellsY: 6, strawPlane: new byte[8 * 6],
                strawTint: new Color(0.86f, 0.78f, 0.52f, 1f),
                variants: variants, habitats: pools, material: GrassMaterial());

            field.Rebuild();
            return field;
        }

        // =====================================================================================
        // 1. INPUT PARITY — the whole reason the shader did not have to change
        // =====================================================================================

        [Test]
        public void EveryBatchedVertex_LandsWhereTheSpriteRendererWouldHavePutIt()
        {
            var field = MakeField(rowOrderSteps: 4);
            var blades = field.DeriveBlades();
            Assert.Greater(blades.Count, 0, "sanity: the fixture field grew nothing");

            // What the OLD path drew: sprite.vertices, scaled, mirrored about the bottom-centre pivot, and
            // translated to the tuft's world position — a SpriteRenderer submits exactly this, already in
            // world space.
            var expected = new HashSet<string>();
            foreach (var b in blades)
            {
                var sprite = field.Variants[b.Variant];
                var sv = sprite.vertices;
                float mirror = b.Mirror ? -1f : 1f;
                for (int v = 0; v < sv.Length; v++)
                    expected.Add(Key(new Vector2(b.Position.x + sv[v].x * b.Scale * mirror,
                                                 b.Position.y + sv[v].y * b.Scale),
                                     sprite.uv[v]));
            }

            var got = new HashSet<string>();
            int vertices = 0;
            foreach (var mf in field.GetComponentsInChildren<MeshFilter>())
            {
                var mesh = mf.sharedMesh;
                Assert.IsNotNull(mesh, "a grass chunk carries no mesh");
                var verts = mesh.vertices;
                var uvs = mesh.uv;
                Assert.AreEqual(verts.Length, uvs.Length,
                    "a chunk has a uv per vertex mismatch — the shader bends by uv.y, so a missing uv is a " +
                    "tuft that does not bend");
                for (int i = 0; i < verts.Length; i++)
                {
                    Vector3 world = mf.transform.TransformPoint(verts[i]);
                    got.Add(Key(new Vector2(world.x, world.y), uvs[i]));
                    vertices++;
                }
            }

            Assert.Greater(vertices, 0, "the batched path built no geometry at all");
            Assert.IsTrue(got.SetEquals(expected),
                "A batched tuft is not standing where its SpriteRenderer stood, or is not carrying the " +
                "sprite's own uv. The grass shader reads exactly those two things — the vertex's world " +
                "position and uv.y — so anything else here means the wind and the footstep bend have " +
                "quietly changed shape. " +
                $"(batched {got.Count} distinct vertices, sprite path {expected.Count})");
        }

        static string Key(Vector2 world, Vector2 uv) =>
            // Quantised to a hundredth of a millimetre: float-exact equality across two different code
            // paths is a promise neither path can keep, and a 1e-5 m disagreement is not a bend change.
            $"{world.x:F5}|{world.y:F5}|{uv.x:F5}|{uv.y:F5}";

        [Test]
        public void AChunkCarriesOnlyTheChannelsASpriteRendererSubmits()
        {
            // The footstep bend needs NO per-tuft state — it parts each blade from the global _GrassTrail
            // using the blade's own world position. This is that claim, checked: if the batched path had
            // needed a per-tuft attribute the sprite path did not have, it would be an extra channel here.
            var field = MakeField(rowOrderSteps: 4);
            foreach (var mf in field.GetComponentsInChildren<MeshFilter>())
            {
                var mesh = mf.sharedMesh;
                Assert.IsTrue(mesh.HasVertexAttribute(VertexAttribute.Position), "a chunk lost its positions");
                Assert.IsTrue(mesh.HasVertexAttribute(VertexAttribute.TexCoord0),
                    "a chunk lost its uv0 — the shader's whole bend weight is uv.y");
                Assert.IsTrue(mesh.HasVertexAttribute(VertexAttribute.Color),
                    "a chunk lost its colours — the per-tuft tint is the vertex colour the shader " +
                    "multiplies over the sprite gradient");
                Assert.IsFalse(mesh.HasVertexAttribute(VertexAttribute.TexCoord1),
                    "a chunk grew a second uv channel. Nothing in the grass shader reads one, so this is " +
                    "either dead weight or a fork of the wind model that the shader does not know about.");
            }
        }

        [Test]
        public void TheTintRidesTheVertexColour_TuftByTuft()
        {
            var field = MakeField(rowOrderSteps: 4);
            var wanted = new HashSet<Color32>();
            foreach (var b in field.DeriveBlades()) wanted.Add(b.Tint);

            var found = new HashSet<Color32>();
            foreach (var mf in field.GetComponentsInChildren<MeshFilter>())
                foreach (var c in mf.sharedMesh.colors32) found.Add(c);

            Assert.IsTrue(found.IsSubsetOf(wanted),
                "A chunk carries a vertex colour no derived tuft asked for — the straw tint has drifted " +
                "between the derive and the mesh.");
        }

        // =====================================================================================
        // 2. SORTING — the one open design point, made checkable
        // =====================================================================================

        [Test]
        public void EveryChunkRidesTheDecorBand_ThroughYSortSpritesOwnMapping()
        {
            // ADR 0032: no hand-picked orders. A chunk's order must be the band's own answer for the row's
            // centre, and must sit inside the band — a chunk below DecorFloor would draw under the wharf
            // deck and the sea.
            var field = MakeField(rowOrderSteps: 4);
            float rowHeight = field.RowHeightMetres;

            foreach (var mf in field.GetComponentsInChildren<MeshFilter>())
            {
                var mr = mf.GetComponent<MeshRenderer>();
                var group = mf.GetComponent<SortingGroup>();
                Assert.IsNotNull(group,
                    "a grass chunk has no SortingGroup — a MeshRenderer does not compete with sprites on " +
                    "sortingOrder alone, it falls back to world z (ADR 0023)");
                Assert.AreEqual(group.sortingOrder, mr.sortingOrder,
                    "a chunk's SortingGroup and MeshRenderer disagree about its order");

                int row = Mathf.RoundToInt(mf.transform.position.y / rowHeight);
                int expected = YSortSprite.OrderFor(
                    (row + 0.5f) * rowHeight,
                    SortingBands.DecorBase, SortingBands.OrdersPerMetre,
                    SortingBands.DecorFloor, SortingBands.DecorCeiling);

                Assert.AreEqual(expected, mr.sortingOrder,
                    "a chunk's sorting order is not YSortSprite's answer for its row centre — somebody " +
                    "hand-picked an order, which ADR 0032 exists to stop");
                Assert.GreaterOrEqual(mr.sortingOrder, SortingBands.DecorFloor);
                Assert.LessOrEqual(mr.sortingOrder, SortingBands.DecorCeiling);
            }
        }

        [Test]
        public void OneOrderStepPerRow_SortsExactlyAsTheOldSpritePerTuftMeadowDid()
        {
            // THE TRADE, stated as a test. YSortSprite already rounded every tuft onto a quantum of
            // 1/OrdersPerMetre metres, so at rowOrderSteps = 1 a chunk row IS that quantum: every tuft in
            // the row gets the order it had as a sprite. Coarser rows trade that fidelity for draw calls,
            // and the error is bounded by the row height — which is why the knob is expressed in ORDER
            // STEPS rather than metres.
            var field = MakeField(rowOrderSteps: 1);
            Assert.AreEqual(1f / SortingBands.OrdersPerMetre, field.RowHeightMetres, 1e-6f,
                "a one-step row is no longer one order step tall");

            var blades = field.DeriveBlades();
            Assert.Greater(blades.Count, 0, "sanity: nothing grew");

            var chunkOrderAt = new Dictionary<int, int>();
            foreach (var mf in field.GetComponentsInChildren<MeshFilter>())
            {
                int row = Mathf.RoundToInt(mf.transform.position.y / field.RowHeightMetres);
                chunkOrderAt[row] = mf.GetComponent<MeshRenderer>().sortingOrder;
            }

            foreach (var b in blades)
            {
                int asSprite = YSortSprite.OrderFor(
                    b.Position.y, SortingBands.DecorBase, SortingBands.OrdersPerMetre,
                    SortingBands.DecorFloor, SortingBands.DecorCeiling);
                int row = Mathf.FloorToInt(b.Position.y / field.RowHeightMetres);
                Assert.IsTrue(chunkOrderAt.TryGetValue(row, out int asChunk),
                    $"a tuft at y={b.Position.y} fell in row {row}, which no chunk covers");
                Assert.AreEqual(asSprite, asChunk,
                    $"at one order step per row a tuft at y={b.Position.y} sorts differently batched " +
                    "than it did as a sprite — the fidelity end of the knob is broken");
            }
        }

        [Test]
        public void TheRowHeightIsDerivedFromTheBand_NeverPicked()
        {
            // Rule 6: the sorting error against the player is RowHeightMetres, and it must fall out of
            // ADR 0032's band rather than being a number somebody liked.
            foreach (int steps in new[] { 1, 2, 4, 8 })
            {
                var field = MakeField(rowOrderSteps: steps);
                Assert.AreEqual(steps / SortingBands.OrdersPerMetre, field.RowHeightMetres, 1e-6f);
                Assert.AreEqual(steps, field.RowOrderSteps);
                TearDown();
            }
        }

        // =====================================================================================
        // 3. THE SABOTAGE — zero a STRENGTH, not a speed
        // =====================================================================================

        static GrassWindMath.Params ShippedWind() => new GrassWindMath.Params
        {
            IdleSway = 0.04f, SwayAmount = 0.22f, WindLean = 0.6f,
            SwaySpeed = 2.2f, GustScale = 0.35f, GustStrength = 0.7f, PhaseGrid = 1f,
        };

        static readonly Vector2[] Probes =
        {
            new Vector2(0f, 0f), new Vector2(3.25f, -7.5f), new Vector2(-11.1f, 4.4f),
            new Vector2(70f, 33f), new Vector2(-40.5f, -2.75f),
        };

        static readonly Vector2[] Winds =
        {
            new Vector2(1f, 0f), new Vector2(0f, 0.35f), new Vector2(-0.6f, 0.6f), new Vector2(0.2f, -0.9f),
        };

        [Test]
        public void ZeroingTheSTRENGTHS_KillsTheBendEverywhere()
        {
            // The kill switch. swayMag = _IdleSway + windStrength × _SwayAmount multiplies the entire
            // offset, so with both at zero there is no wind, no gust and no lean anywhere, for any time.
            var p = ShippedWind();
            p.IdleSway = 0f;
            p.SwayAmount = 0f;

            foreach (var probe in Probes)
            foreach (var wind in Winds)
            foreach (float t in new[] { 0f, 0.37f, 5f, 123.75f })
                Assert.AreEqual(0f, GrassWindMath.WindOffset(probe, wind, t, p).magnitude, 1e-7f,
                    $"With both sway strengths at zero the grass still bent at {probe} in wind {wind} at " +
                    "t=" + t + ". The strengths are not the amplitude, so the wind guard is guarding " +
                    "nothing.");
        }

        [Test]
        public void RestoringOneSTRENGTH_BringsTheBendBack()
        {
            // The other half: a kill switch that is stuck on is not a kill switch.
            var p = ShippedWind();
            p.IdleSway = 0f;                      // only the wind-driven amplitude survives

            bool anyBend = false;
            foreach (var probe in Probes)
            foreach (var wind in Winds)
                if (GrassWindMath.WindOffset(probe, wind, 1.5f, p).magnitude > 1e-6f) anyBend = true;

            Assert.IsTrue(anyBend,
                "With _SwayAmount restored the grass did not move in any wind at any probe — the wind " +
                "vector is not reaching the offset at all.");
        }

        [Test]
        public void ZeroingTheSPEED_DoesNotKillTheBend_WhichIsWhyItIsNotTheGuard()
        {
            // ⚠ THE LAW, stated as a test. A guard written against _SwaySpeed would pass on a shader that
            // had stopped reading the wind entirely, because the steady lean and the static spatial gust
            // pattern both survive a zero speed. This test exists to keep anybody from "simplifying" the
            // sabotage above into that useless form.
            var p = ShippedWind();
            p.SwaySpeed = 0f;

            bool anyBend = false;
            foreach (var probe in Probes)
            foreach (var wind in Winds)
                if (GrassWindMath.WindOffset(probe, wind, 9f, p).magnitude > 1e-6f) anyBend = true;

            Assert.IsTrue(anyBend,
                "Zeroing the gust SPEED killed the bend, which would make a speed-zero guard look " +
                "meaningful. It is not: re-read the rendering-guard law in GrassWindMath.");
        }

        [Test]
        public void NoWindAtAll_StillLeavesTheIdleBaseline()
        {
            // Dead calm must still breathe a little — that is what _IdleSway is for, and it is also what
            // makes the demo scene show motion before the sim feeds any wind.
            var p = ShippedWind();
            float bend = GrassWindMath.WindOffset(new Vector2(2f, 2f), Vector2.zero, 3f, p).magnitude;
            Assert.Greater(bend, 0f,
                "With no wind the grass froze solid — the idle baseline has been lost.");
        }

        [Test]
        public void TheBendWeightIsSquared_SoARootStaysRootedAndOnlyATessellatedTuftBends()
        {
            Assert.AreEqual(0f, GrassWindMath.BendWeight(0f), 1e-7f, "the root moved");
            Assert.AreEqual(1f, GrassWindMath.BendWeight(1f), 1e-7f, "the tip did not reach full bend");
            Assert.AreEqual(0.25f, GrassWindMath.BendWeight(0.5f), 1e-7f,
                "the midpoint is not the SQUARE of its height — the shaping that keeps the base planted " +
                "has gone linear, which is the FullRect-quad defect WindBendTessellationTests pins");
        }

        // =====================================================================================
        // 4. THE TWIN MUST NOT DESCRIBE A SHADER THAT HAS MOVED ON
        // =====================================================================================

        [Test]
        public void TheShaderStillContainsTheTermsThisTwinModels()
        {
            // A CPU twin nothing renders through can rot silently. This is the anchor: if the shader's
            // wind block is rewritten, this fails and names what changed, instead of the twin quietly
            // certifying arithmetic the GPU no longer runs.
            Assert.IsTrue(File.Exists(GrassShaderPath),
                $"{GrassShaderPath} is missing — the wind twin is now guarding nothing.");
            string src = File.ReadAllText(GrassShaderPath);

            foreach (string term in new[]
            {
                "_WindWorld",                            // the shared-wind global GrassWindBridge publishes
                "_IdleSway + windStr * _SwayAmount",     // the amplitude the sabotage zeroes
                "windStr * _WindLean",                   // the steady lean that survives a zero speed
                "_GustStrength",
                "_PhaseGrid",
                "saturate(IN.uv.y)",                     // the bend weight, read off the SPRITE's uv
                "_GrassTrail",                           // the footstep path, still a global, still no per-tuft state
            })
                Assert.IsTrue(src.Contains(term),
                    $"HiddenHarboursGrass.shader no longer contains '{term}'. GrassWindMath models it, and " +
                    "GrassField's whole no-shader-change argument rests on it — re-derive both before " +
                    "editing this list.");

            // The batched path leans on the sprite's own tessellated mesh precisely because the shader is
            // Cull Off; a mirrored tuft negates local X, which inverts winding.
            Assert.IsTrue(src.Contains("Cull Off"),
                "The grass shader stopped being Cull Off. GrassField mirrors a tuft by negating its local " +
                "X, which inverts triangle winding — mirrored tufts will now disappear.");
        }
    }
}
