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
            // world space with an identity object-to-world matrix.
            var expected = new List<(Vector2 world, Vector2 uv)>();
            foreach (var b in blades)
            {
                var sprite = field.Variants[b.Variant];
                var sv = sprite.vertices;
                float mirror = b.Mirror ? -1f : 1f;
                for (int v = 0; v < sv.Length; v++)
                    expected.Add((new Vector2(b.Position.x + sv[v].x * b.Scale * mirror,
                                              b.Position.y + sv[v].y * b.Scale),
                                  sprite.uv[v]));
            }

            // Bucket the expected vertices so each batched one only tests its own neighbourhood — the
            // fixture is a few hundred vertices, but the island is millions and a quadratic scan here
            // would be the reason somebody deletes this test.
            var buckets = new Dictionary<(int, int), List<int>>();
            for (int i = 0; i < expected.Count; i++)
            {
                var k = Cell(expected[i].world);
                if (!buckets.TryGetValue(k, out var list)) buckets[k] = list = new List<int>();
                list.Add(i);
            }

            var matched = new bool[expected.Count];
            int vertices = 0, unmatched = 0;
            float worstGap = 0f;
            string worstNote = null;

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
                    Vector3 wp = mf.transform.TransformPoint(verts[i]);
                    var world = new Vector2(wp.x, wp.y);
                    vertices++;

                    int best = -1;
                    float bestGap = float.MaxValue;
                    var c = Cell(world);
                    for (int dx = -1; dx <= 1; dx++)
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        if (!buckets.TryGetValue((c.Item1 + dx, c.Item2 + dy), out var near)) continue;
                        foreach (int e in near)
                        {
                            if (matched[e]) continue;
                            if ((expected[e].uv - uvs[i]).sqrMagnitude > UvTolerance * UvTolerance) continue;
                            float gap = (expected[e].world - world).magnitude;
                            if (gap < bestGap) { bestGap = gap; best = e; }
                        }
                    }

                    if (best >= 0 && bestGap <= PositionTolerance)
                    {
                        matched[best] = true;
                        if (bestGap > worstGap) worstGap = bestGap;
                    }
                    else
                    {
                        unmatched++;
                        if (worstNote == null)
                            worstNote = $"first unmatched batched vertex at {world} uv {uvs[i]}; " +
                                        (best >= 0
                                            ? $"nearest sprite-path vertex with the same uv was {bestGap:F4} m away"
                                            : "no sprite-path vertex within a metre carried the same uv");
                    }
                }
            }

            Assert.Greater(vertices, 0, "the batched path built no geometry at all");
            Assert.AreEqual(expected.Count, vertices,
                "The batched path built a different NUMBER of vertices than the sprite path would have.");
            Assert.AreEqual(0, unmatched,
                "A batched tuft is not standing where its SpriteRenderer stood, or is not carrying the " +
                "sprite's own uv. The grass shader reads exactly those two things — the vertex's world " +
                "position and uv.y — so anything else here means the wind and the footstep bend have " +
                $"quietly changed shape. {worstNote}\n" +
                "⚠ Read the DISTANCE before assuming a tolerance problem: a sub-millimetre gap is float " +
                "re-association (the chunk stores vertices relative to its origin and adds it back), while " +
                "anything near a sprite width is a real divergence — mirroring is the usual first suspect.");

            Debug.Log($"[GrassFieldWindParity] {vertices} batched vertices all matched the sprite path; " +
                      $"worst position gap {worstGap * 1000f:F4} mm (tolerance {PositionTolerance * 1000f:F1} mm).");
        }

        /// <summary>
        /// How far a batched vertex may sit from where the sprite path would have put it. <b>0.1 mm</b> —
        /// a three-hundredth of a PIXEL at PPU 32, so it cannot hide a geometry change, but it survives the
        /// float re-association that is unavoidable here: the chunk stores <c>(p − origin)</c> and the
        /// transform adds <c>origin</c> back, and <c>(p − c) + c</c> is not bit-identical to <c>p</c> in
        /// float32. The failure message prints the actual worst gap, so a real divergence cannot pass
        /// itself off as rounding.
        /// </summary>
        const float PositionTolerance = 1e-4f;

        /// <summary>uv must match far more tightly — it is a 0..1 coordinate copied verbatim, with no
        /// arithmetic done to it at all.</summary>
        const float UvTolerance = 1e-6f;

        /// <summary>A 1 m bucket for the neighbourhood search — comfortably wider than the tolerance, so a
        /// match is never missed by falling in the next cell.</summary>
        static (int, int) Cell(Vector2 world) =>
            (Mathf.FloorToInt(world.x), Mathf.FloorToInt(world.y));

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

                int row = GrassField.RowOf(mf.transform.position.y, rowHeight);
                int expected = YSortSprite.OrderFor(
                    GrassField.RowAnchorY(row, rowHeight),
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
            //
            // ⚠ This caught a real convention bug on its first run. Rows were bucketed with `floor` and
            // anchored at the row CENTRE, which sits half a step out of phase with the rounding
            // OrderFor already does — measured over 100 m of world Y, that disagreed with the sprite
            // order on 50% of positions, and the fixture happened to land one tuft on the wrong side.
            // The row mapping is now GrassField.RowOf / RowAnchorY, derived from the band rather than
            // restated, and this test asks for it by name so the two cannot drift apart again.
            var field = MakeField(rowOrderSteps: 1);
            float rowHeight = field.RowHeightMetres;
            Assert.AreEqual(1f / SortingBands.OrdersPerMetre, rowHeight, 1e-6f,
                "a one-step row is no longer one order step tall");

            var blades = field.DeriveBlades();
            Assert.Greater(blades.Count, 0, "sanity: nothing grew");

            var chunkOrderAt = new Dictionary<int, int>();
            foreach (var mf in field.GetComponentsInChildren<MeshFilter>())
            {
                int row = GrassField.RowOf(mf.transform.position.y, rowHeight);
                chunkOrderAt[row] = mf.GetComponent<MeshRenderer>().sortingOrder;
            }

            foreach (var b in blades)
            {
                int asSprite = YSortSprite.OrderFor(
                    b.Position.y, SortingBands.DecorBase, SortingBands.OrdersPerMetre,
                    SortingBands.DecorFloor, SortingBands.DecorCeiling);
                int row = GrassField.RowOf(b.Position.y, rowHeight);
                Assert.IsTrue(chunkOrderAt.TryGetValue(row, out int asChunk),
                    $"a tuft at y={b.Position.y} fell in row {row}, which no chunk covers");
                Assert.AreEqual(asSprite, asChunk,
                    $"at one order step per row a tuft at y={b.Position.y} sorts differently batched " +
                    "than it did as a sprite — the fidelity end of the knob is broken");
            }
        }

        [Test]
        public void TheRowMapping_AgreesWithTheBandAcrossAWholeRegionOfWorldY()
        {
            // The claim above, swept rather than sampled: at one order step per row, EVERY world Y in a
            // region-sized span must take the order its row's anchor takes. This is the test that would
            // have caught the floor/centre convention bug immediately — the fixture only found it because
            // one tuft happened to fall on the wrong side of a row edge.
            float rowHeight = 1f / SortingBands.OrdersPerMetre;
            int mismatches = 0;
            float worstAt = 0f;

            for (float y = -120f; y <= 120f; y += 0.0007311f)   // an irrational-ish step: no lattice bias
            {
                int asSprite = YSortSprite.OrderFor(
                    y, SortingBands.DecorBase, SortingBands.OrdersPerMetre,
                    SortingBands.DecorFloor, SortingBands.DecorCeiling);
                int asChunk = YSortSprite.OrderFor(
                    GrassField.RowAnchorY(GrassField.RowOf(y, rowHeight), rowHeight),
                    SortingBands.DecorBase, SortingBands.OrdersPerMetre,
                    SortingBands.DecorFloor, SortingBands.DecorCeiling);
                if (asSprite != asChunk) { mismatches++; if (worstAt == 0f) worstAt = y; }
            }

            Assert.AreEqual(0, mismatches,
                $"{mismatches} world-Y positions sort differently as a chunk row than as a sprite (first " +
                $"at y={worstAt}). The row mapping is out of phase with the band's own rounding — see " +
                "GrassField.RowOf.");
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
