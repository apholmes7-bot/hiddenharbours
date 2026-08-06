using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using HiddenHarbours.Core;
using HiddenHarbours.Art;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// ⭐⭐ THE WALL LAYERS AGAINST A WALKER — the claim EditMode cannot make.
    ///
    /// <para><see cref="HiddenHarbours.Tests.EditMode.StPetersCliffWallTests"/> proves the ARITHMETIC of
    /// the ladder on every chunk of the real coast. It cannot prove the component then wires that number
    /// into a renderer that competes with sprites at all — a MeshRenderer does not obey
    /// <c>sortingOrder</c> on its own, and the <see cref="SortingGroup"/> that makes it ("sort as 2D",
    /// ADR 0023) is created at runtime. A wall with a perfect order and no group draws by world Z and
    /// pops in front of everything, which is the exact bug <c>RegionValidatorWindow</c> was written to
    /// catch and which no EditMode assertion reaches.</para>
    ///
    /// <para>So: build a wall, stand a Y-sorted sprite at its clifftop and another at its foot, and ask
    /// the question a player would.</para>
    /// </summary>
    public class CliffWallSortingPlayTests
    {
        private GameObject _wall, _walker;
        private Material _material;
        private Texture2D _channel;

        /// <summary>A 6 m south-facing wall running 4 m of shore, dropping 6 m over a 1.75 m plunge —
        /// the shape St Peters' own due-south sector actually produces.</summary>
        const float Drop = 6f, PlungeNorthing = 1.75f, RunMetres = 4f;
        const int Stations = 17;

        [TearDown]
        public void TearDown()
        {
            if (_wall != null) Object.Destroy(_wall);
            if (_walker != null) Object.Destroy(_walker);
            if (_material != null) Object.Destroy(_material);
            if (_channel != null) Object.Destroy(_channel);
        }

        private CliffWallSurface MakeWall()
        {
            var shader = Shader.Find("HiddenHarbours/CliffFace");
            Assert.IsNotNull(shader, "HiddenHarbours/CliffFace is missing — the wall cannot render.");
            _material = new Material(shader);

            // The kit's PNGs are gitignored (PR #427) so a runner has none; the sorting is independent
            // of what the channels contain, and a 4x4 stand-in keeps this test runnable everywhere.
            _channel = new Texture2D(4, 4);

            var brow = new Vector2[Stations];
            var toe = new Vector2[Stations];
            var drop = new float[Stations];
            for (int i = 0; i < Stations; i++)
            {
                float s = RunMetres * i / (Stations - 1);
                brow[i] = new Vector2(s, 0f);
                toe[i] = new Vector2(s, -PlungeNorthing);
                drop[i] = Drop;
            }

            _wall = new GameObject("cliff-wall-test");
            _wall.transform.position = new Vector3(brow[0].x, brow[0].y, 0f);
            var surface = _wall.AddComponent<CliffWallSurface>();
            surface.Configure(brow, toe, drop, _material, _channel, _channel, _channel, profile: null,
                              wallAzimuth: 180f, batter: 76f,
                              bakeLight: new Vector3(-0.60f, -0.55f, 0.58f),
                              faceMetresS: 12f, faceMetresT: 9f,
                              subdivideMetres: 0.25f, profileMetres: 1.15f);
            return surface;
        }

        private int WalkerOrderAt(float y)
        {
            if (_walker != null) Object.Destroy(_walker);
            _walker = new GameObject("walker");
            _walker.SetActive(false);
            _walker.transform.position = new Vector3(0f, y, 0f);
            _walker.AddComponent<SpriteRenderer>();
            var sort = _walker.AddComponent<YSortSprite>();
            sort.Dynamic = true;
            _walker.SetActive(true);
            return _walker.GetComponent<SpriteRenderer>().sortingOrder;
        }

        /// <summary>
        /// The wall really does get a SortingGroup, and its order really is the one the geometry law
        /// derived. Without the group the mesh sorts by world Z against every sprite in the region.
        /// </summary>
        [UnityTest]
        public IEnumerator TheWallSortsThroughASortingGroup_AtItsDerivedOrder()
        {
            CliffWallSurface surface = MakeWall();
            yield return null;

            var group = _wall.GetComponentInChildren<SortingGroup>();
            Assert.IsNotNull(group,
                "the wall's mesh has no SortingGroup — a MeshRenderer does not compete with sprites by " +
                "sortingOrder on its own (ADR 0023), and RegionValidatorWindow fails a scene like this");
            Assert.AreEqual(surface.SortingOrder, group.sortingOrder);

            var renderer = _wall.GetComponentInChildren<MeshRenderer>();
            Assert.IsNotNull(renderer, "the wall built no mesh");
            Assert.AreEqual(surface.SortingOrder, renderer.sortingOrder);

            Assert.That(surface.SortingOrder,
                Is.InRange(SortingBands.DecorFloor, SortingBands.DecorCeiling),
                "a wall outside the decor band cannot interleave with the decor standing on it");
        }

        /// <summary>
        /// ⭐ THE PLAYER TEST. Stand at the foot and you are in front of the wall; stand on the clifftop
        /// and it hides you. Both, from the same wall, with the same YSortSprite every tuft of grass
        /// uses — no special case for cliffs anywhere.
        /// </summary>
        [UnityTest]
        public IEnumerator AWalkerAtTheFootDrawsInFront_AndOneOnTheClifftopDrawsBehind()
        {
            CliffWallSurface surface = MakeWall();
            yield return null;

            // The foot: a metre south of the drawn toe, where the foreshore is.
            int atFoot = WalkerOrderAt(surface.SortY - 1f);
            Assert.Greater(atFoot, surface.SortingOrder,
                $"a walker at the wall's foot sorts at {atFoot} against the wall's " +
                $"{surface.SortingOrder} — they would be drawn BEHIND a wall they are standing in " +
                "front of, which is the whole reason the chunk sorts by its toe");

            // The clifftop: a metre north of the brow, on the plateau.
            int onTop = WalkerOrderAt(1f);
            Assert.Less(onTop, surface.SortingOrder,
                $"a walker on the clifftop sorts at {onTop} against the wall's " +
                $"{surface.SortingOrder} — they would be drawn IN FRONT of the wall they are standing " +
                "on top of");

            // …and right AT the lip, which is the tightest case the chunk length is bounded for.
            int atLip = WalkerOrderAt(0f);
            Assert.LessOrEqual(atLip, surface.SortingOrder,
                "a walker at the very lip must still sort behind the face that falls away below them");
        }

        /// <summary>
        /// Rebuilding converges: the second Configure replaces the mesh child rather than adding one.
        /// This is what makes a builder Refresh idempotent, and it fails LOUDLY here rather than as a
        /// region that gains a second coast every time the owner rebuilds it.
        /// </summary>
        [UnityTest]
        public IEnumerator ReconfiguringReplacesTheMesh_RatherThanStackingASecondWall()
        {
            CliffWallSurface surface = MakeWall();
            yield return null;
            Assert.AreEqual(1, _wall.GetComponentsInChildren<MeshRenderer>().Length);

            var brow = new Vector2[Stations];
            var toe = new Vector2[Stations];
            var drop = new float[Stations];
            for (int i = 0; i < Stations; i++)
            {
                float s = RunMetres * i / (Stations - 1);
                brow[i] = new Vector2(s, 0f);
                toe[i] = new Vector2(s, -PlungeNorthing);
                drop[i] = Drop;
            }
            surface.Configure(brow, toe, drop, _material, _channel, _channel, _channel, null,
                              180f, 76f, new Vector3(-0.60f, -0.55f, 0.58f), 12f, 9f, 0.25f, 1.15f);
            yield return null;

            Assert.AreEqual(1, _wall.GetComponentsInChildren<MeshRenderer>().Length,
                "a second Configure stacked a second wall on the first");
            Assert.AreEqual(1, _wall.GetComponentsInChildren<SortingGroup>().Length);
        }

        // =========================================================================================
        //  ⭐ THE STRATA AND THE DECALS — the finishing pass, as renderers that actually exist
        // =========================================================================================

        /// <summary>
        /// ⭐⭐ <b>A FINISHED CHUNK IS FOUR RENDERERS, AND THE DECALS COMPOSITE OVER THE ROCK.</b>
        ///
        /// <para>EditMode can prove the geometry law and the coast can prove the plan, but neither can
        /// prove that <c>Configure</c> then BUILDS the bands and the strips — a band whose texture failed
        /// to reach it silently draws nothing, and a decal at the wrong order silently draws behind the
        /// wall it is supposed to seat. Both look like "the art did not land" rather than like an error,
        /// which is exactly the class of thing that ships.</para>
        ///
        /// <para><b>And the +1 must stay INSIDE the group.</b> The decals draw over the face; the group
        /// is what keeps that private, so the chunk still meets the region on ONE position-derived order
        /// and ADR 0032's rule against fixed orders in the decor band is not bent.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator AStratifiedChunkBuildsABandPerRock_AndSeatsADecalAtEachEnd()
        {
            CliffWallSurface surface = MakeStratifiedWall();
            yield return null;

            Assert.AreEqual(4, surface.RendererCount,
                "a stratified chunk with both decals must build four renderers: the soil band, the rock " +
                "band, the brow strip and the toe strip");

            var byName = new Dictionary<string, MeshRenderer>();
            foreach (MeshRenderer r in _wall.GetComponentsInChildren<MeshRenderer>())
                byName[r.gameObject.name] = r;

            foreach (string expected in new[]
            {
                CliffWallSurface.BandChildPrefix + "Overburden",
                CliffWallSurface.BandChildPrefix + "Rock",
                CliffWallSurface.BrowChildName,
                CliffWallSurface.ToeChildName,
            })
                Assert.IsTrue(byName.ContainsKey(expected),
                    $"no '{expected}' renderer — built: {string.Join(", ", byName.Keys)}");

            var group = _wall.GetComponent<SortingGroup>();
            Assert.IsNotNull(group,
                "the CHUNK carries no SortingGroup — a MeshRenderer does not compete with sprites by " +
                "sortingOrder on its own (ADR 0023), and RegionValidatorWindow looks for one in the " +
                "PARENTS of every mesh");
            Assert.AreEqual(surface.SortingOrder, group.sortingOrder,
                "the group must meet the region at the chunk's own position-derived order");

            // The faces sit at the chunk's order; the decals one above it — and that +1 is INSIDE the
            // group, so it never spends an order of the decor band.
            Assert.AreEqual(surface.SortingOrder,
                            byName[CliffWallSurface.BandChildPrefix + "Rock"].sortingOrder);
            Assert.Greater(byName[CliffWallSurface.BrowChildName].sortingOrder, surface.SortingOrder,
                "the brow strip would draw BEHIND the rock it is seating into the clifftop");
            Assert.Greater(byName[CliffWallSurface.ToeChildName].sortingOrder, surface.SortingOrder,
                "the toe strip would draw BEHIND the foot it is finishing");
        }

        /// <summary>A four-renderer chunk converges on a rebuild exactly as a one-renderer one does —
        /// which is what keeps a builder Refresh from growing a second coast each time, now that a chunk
        /// is a stack rather than a single mesh.</summary>
        [UnityTest]
        public IEnumerator AStratifiedChunkAlsoConverges_RatherThanStackingEightRenderers()
        {
            CliffWallSurface surface = MakeStratifiedWall();
            yield return null;
            Assert.AreEqual(4, _wall.GetComponentsInChildren<MeshRenderer>().Length);

            ConfigureStratified(surface);
            yield return null;

            Assert.AreEqual(4, _wall.GetComponentsInChildren<MeshRenderer>().Length,
                "a second Configure stacked a second set of bands and decals on the first");
            Assert.AreEqual(1, _wall.GetComponentsInChildren<SortingGroup>().Length,
                "a rebuild added a second SortingGroup");
        }

        private CliffWallSurface MakeStratifiedWall()
        {
            var shader = Shader.Find("HiddenHarbours/CliffFace");
            Assert.IsNotNull(shader, "HiddenHarbours/CliffFace is missing — the wall cannot render.");
            _material = new Material(shader);
            _channel = new Texture2D(4, 4);

            _wall = new GameObject("cliff-wall-stratified-test");
            _wall.transform.position = new Vector3(0f, 0f, 0f);
            var surface = _wall.AddComponent<CliffWallSurface>();
            ConfigureStratified(surface);
            return surface;
        }

        private void ConfigureStratified(CliffWallSurface surface)
        {
            var brow = new Vector2[Stations];
            var toe = new Vector2[Stations];
            var drop = new float[Stations];
            for (int i = 0; i < Stations; i++)
            {
                float s = RunMetres * i / (Stations - 1);
                brow[i] = new Vector2(s, 0f);
                toe[i] = new Vector2(s, -PlungeNorthing);
                drop[i] = Drop;
            }

            // A 1.8 m soil horizon on a 6 m face: the top band is soil, the rest rock.
            const float Horizon = 1.8f;
            var bands = new[]
            {
                new CliffFaceBand
                {
                    Unlit = _channel, Normal = _channel, Mask = _channel,
                    StartSurfaceMetres = 0f, EndSurfaceMetres = Horizon, Label = "Overburden",
                },
                new CliffFaceBand
                {
                    Unlit = _channel, Normal = _channel, Mask = _channel,
                    StartSurfaceMetres = Horizon, EndSurfaceMetres = 0f, Label = "Rock",
                },
            };

            surface.Configure(brow, toe, drop, _material, bands, profile: null,
                              browDecal: _channel, toeDecal: _channel,
                              alongOffsetMetres: 17f, rowsBasisSurfaceMetres: 8f,
                              wallAzimuth: 180f, batter: 76f,
                              bakeLight: new Vector3(-0.60f, -0.55f, 0.58f),
                              faceMetresS: 12f, faceMetresT: 9f,
                              subdivideMetres: 0.25f, profileMetres: 1.15f,
                              stripMetresT: 4f, browLineAt: 0.42f);
        }
    }
}
