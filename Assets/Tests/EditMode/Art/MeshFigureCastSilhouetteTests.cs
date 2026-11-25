using System.Collections.Generic;
using System.Reflection;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// <b>The shadow and the foliage outline trace the shape the mesh figure is drawn in.</b> The player
    /// (and a mesh villager) is a posed mesh while the old body sprite stays enabled and hidden with
    /// <c>forceRenderingOff</c>; both features traced that hidden standing sprite whatever the mesh was
    /// doing. These hold the trace to the mesh (the fill and the projection, by hand), the re-trace rule
    /// (only on a new pose or a real move), and the hand-over: the override reaches the shadow's renderer
    /// and texture and the mask's sprite, unflipped, and clearing it gives the body its own sprite back.
    /// </summary>
    public sealed class MeshFigureCastSilhouetteTests
    {
        private const int Bones = 3;
        private readonly List<Object> _owned = new();
        private IsoCharacterFigureRenderer _figure;

        [TearDown]
        public void TearDown()
        {
            if (_figure != null) _figure.LeaveAshore();
            _figure = null;
            for (int i = _owned.Count - 1; i >= 0; i--)
                if (_owned[i] != null) Object.DestroyImmediate(_owned[i]);
            _owned.Clear();
        }

        // ------------------------------------------------------------------ the pure rules

        private static int Count(Color32[] pixels)
        {
            int n = 0;
            foreach (Color32 p in pixels) if (p.a != 0) n++;
            return n;
        }

        [Test]
        public void Fill_CoversExactlyThePixelCentresInside_EitherWinding()
        {
            var points = new[] { new Vector2(2f, 3f), new Vector2(6f, 3f), new Vector2(6f, 7f), new Vector2(2f, 7f) };
            foreach (int[] tris in new[] { new[] { 0, 1, 2, 0, 2, 3 }, new[] { 0, 2, 1, 0, 3, 2 } })
            {
                var pixels = new Color32[10 * 10];
                int covered = MeshFigureCastSilhouette.Fill(points, tris, pixels, 10, 10);
                Assert.AreEqual(16, Count(pixels), "a 4 x 4 pixel square must cover exactly 16 pixel centres");
                Assert.AreEqual(16, covered, "the shared diagonal is counted once");
                for (int y = 0; y < 10; y++)
                    for (int x = 0; x < 10; x++)
                    {
                        bool inside = x >= 2 && x < 6 && y >= 3 && y < 7;
                        Assert.AreEqual(inside, pixels[y * 10 + x].a == 255, $"pixel ({x},{y})");
                    }
            }
        }

        [Test]
        public void Fill_DropsWhatFallsOutsideTheWindow_AndSkipsDegenerateTriangles()
        {
            var points = new[] { new Vector2(-5f, -5f), new Vector2(3f, -5f), new Vector2(3f, 3f), new Vector2(1f, 1f) };
            var pixels = new Color32[4 * 4];
            Assert.DoesNotThrow(() => MeshFigureCastSilhouette.Fill(points, new[] { 0, 1, 2, 0, 3, 2 }, pixels, 4, 4));
            Assert.Greater(Count(pixels), 0);

            var flat = new Color32[4 * 4];
            MeshFigureCastSilhouette.Fill(new[] { Vector2.zero, Vector2.one, Vector2.one * 2f }, new[] { 0, 1, 2 }, flat, 4, 4);
            Assert.AreEqual(0, Count(flat), "a zero-area triangle covers nothing");
        }

        [Test]
        public void Project_MapsCasterLocalMetresToWindowPixels_AndIgnoresDepth()
        {
            var into = new Vector2[2];
            Matrix4x4 m = Matrix4x4.Translate(new Vector3(0.25f, 0f, 9f));
            MeshFigureCastSilhouette.Project(new[] { new Vector3(0.5f, 1f, 0.3f), Vector3.zero }, m, 32f, 40, 16, into);
            Assert.AreEqual(new Vector2(40 + 0.75f * 32f, 16 + 32f), into[0]);
            Assert.AreEqual(new Vector2(40 + 0.25f * 32f, 16f), into[1]);
        }

        [Test]
        public void SameXY_ToleratesDepthAndSubTolerance_ButNotARealMove()
        {
            Matrix4x4 a = Matrix4x4.TRS(new Vector3(1f, 2f, 3f), Quaternion.Euler(0f, 0f, 10f), Vector3.one);
            float tol = MeshFigureCastSilhouette.RetraceTolerance;
            Assert.IsTrue(MeshFigureCastSilhouette.SameXY(a, a, tol));
            Assert.IsTrue(MeshFigureCastSilhouette.SameXY(a, Matrix4x4.Translate(new Vector3(0f, 0f, 5f)) * a, tol),
                "depth never changes the outline");
            Assert.IsTrue(MeshFigureCastSilhouette.SameXY(a, Matrix4x4.Translate(new Vector3(tol * 0.5f, 0f, 0f)) * a, tol));
            Assert.IsFalse(MeshFigureCastSilhouette.SameXY(a, Matrix4x4.Translate(new Vector3(tol * 2f, 0f, 0f)) * a, tol));
            Assert.IsFalse(MeshFigureCastSilhouette.SameXY(a, Matrix4x4.Rotate(Quaternion.Euler(0f, 0f, 2f)) * a, tol),
                "a turn is a new outline");
        }

        [Test]
        public void TheWindowStaysUnderTheGroundContactHeightGate()
        {
            var profile = ScriptableObject.CreateInstance<SpriteShadowProfile>();
            _owned.Add(profile);
            Assert.Less(MeshFigureCastSilhouette.Height / MeshFigureCastShape.FallbackPixelsPerUnit, profile.GroundContactMinHeight,
                "the outline's window is a sprite's height to the ground-contact gate: taller than the gate and " +
                "the figure would grow a tree's pool");
        }

        // ------------------------------------------------------------------ the trace, on a real figure

        private Mesh BindMesh()
        {
            var bind = new Mesh { name = "HHTestCastBind", hideFlags = HideFlags.HideAndDontSave };
            bind.SetVertices(new[]
            {
                new Vector3(-0.2f, 0f, 0f), new Vector3(0.2f, 0f, 0f),
                new Vector3(0.2f, 0f, 1.6f), new Vector3(-0.2f, 0f, 1.6f),
            });
            bind.SetNormals(new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back });
            bind.SetTriangles(new[] { 0, 1, 2, 0, 2, 3 }, 0);
            bind.boneWeights = new[]
            {
                new BoneWeight { boneIndex0 = 0, weight0 = 1f }, new BoneWeight { boneIndex0 = 0, weight0 = 1f },
                new BoneWeight { boneIndex0 = 1, weight0 = 0.5f, boneIndex1 = 2, weight1 = 0.5f },
                new BoneWeight { boneIndex0 = 1, weight0 = 0.5f, boneIndex1 = 2, weight1 = 0.5f },
            };
            bind.bindposes = new[]
            {
                Matrix4x4.identity,
                Matrix4x4.TRS(new Vector3(0f, 0f, -0.8f), Quaternion.identity, Vector3.one),
                Matrix4x4.TRS(new Vector3(0f, 0f, -1.6f), Quaternion.identity, Vector3.one),
            };
            bind.RecalculateBounds();
            _owned.Add(bind);
            return bind;
        }

        private static CharacterSkinDef.SkinClip Clip(string key, int frames)
        {
            var keys = new CharacterSkinDef.BoneKey[frames * Bones];
            for (int f = 0; f < frames; f++)
                for (int b = 0; b < Bones; b++)
                    keys[f * Bones + b] = new CharacterSkinDef.BoneKey
                    {
                        // Frame 1 swings the upper body sideways: a new outline, not a new position.
                        Position = new Vector3(b == 0 ? 0f : 0.25f * f, 0f, 0.8f * b),
                        Rotation = Quaternion.identity,
                    };
            return new CharacterSkinDef.SkinClip
            {
                Anim = key, State = key, FramesPerSecond = 12f, Loop = true, FrameCount = frames, Keys = keys,
            };
        }

        private IsoCharacterFigureRenderer Figure()
        {
            var def = ScriptableObject.CreateInstance<CharacterSkinDef>();
            _owned.Add(def);
            def.Id = "character.test_cast_figure";
            def.BindMesh = BindMesh();
            def.Bones = new[]
            {
                new CharacterSkinDef.Bone { Id = "root", Parent = CharacterSkinDef.NoBone, OwnsVertex = true },
                new CharacterSkinDef.Bone { Id = "spine", Parent = 0, OwnsVertex = true },
                new CharacterSkinDef.Bone { Id = "head", Parent = 1, OwnsVertex = true },
            };
            def.MaxInfluences = 2;
            def.Materials = new[]
            {
                new CharacterSkinDef.Material
                {
                    Name = "skin", Offset = 0,
                    Colors = new[] { new Color32(10, 20, 30, 255), new Color32(40, 50, 60, 255), new Color32(70, 80, 90, 255) },
                },
            };
            var bayer = new float[16];
            for (int i = 0; i < 16; i++) bayer[i] = (i + 0.5f) / 16f;
            def.Bayer16 = bayer;
            def.Keyline = new Color32(12, 14, 18, 255);
            def.LightN = new Vector3(0.3f, -0.5f, 0.81f);
            def.Gain = 1.15f;
            def.Bias = -0.04f;
            def.CellW = 64;
            def.CellH = 92;
            def.PivotPx = new Vector2(32f, 80f);
            def.PxPerMetre = 32;
            def.ElevationDeg = 40f;
            def.Clips = new[] { Clip("idle", 2) };
            def.MeshStates = new[] { "idle" };
            Assert.IsTrue(def.IsUsable(), "the synthetic def is not usable; the test would measure the fixture");

            var go = new GameObject("TestCastFigure");
            _owned.Add(go);
            // The frame her mesh is drawn under, aboard (the hull's posed child) and ashore (AshoreFrame):
            // the rig's up turned toward the camera, so the outline is the screen silhouette.
            go.transform.localRotation = IsoFacetMath.HullRotation(0d, def.ElevationDeg);
            go.transform.localScale = IsoFacetMath.HullScale;
            _figure = go.AddComponent<IsoCharacterFigureRenderer>();
            _figure.Configure(def);
            Assert.IsTrue(_figure.IsConfigured);
            Assert.IsTrue(_figure.SetPose("idle", 0));
            return _figure;
        }

        private Transform Caster(Vector3 at)
        {
            var go = new GameObject("TestCastBody");
            _owned.Add(go);
            go.transform.position = at;
            return go.transform;
        }

        private static Color32[] Expected(IsoCharacterFigureRenderer figure, Transform caster, float ppu)
        {
            Assert.IsTrue(figure.TryGetPosedGeometry(out Vector3[] verts, out int[] tris));
            Matrix4x4 m = caster.worldToLocalMatrix * figure.FacetTransform.localToWorldMatrix;
            var projected = new Vector2[verts.Length];
            MeshFigureCastSilhouette.Project(verts, m, ppu, MeshFigureCastSilhouette.PivotX, MeshFigureCastSilhouette.PivotY, projected);
            var pixels = new Color32[MeshFigureCastSilhouette.Width * MeshFigureCastSilhouette.Height];
            MeshFigureCastSilhouette.Fill(projected, tris, pixels, MeshFigureCastSilhouette.Width, MeshFigureCastSilhouette.Height);
            return pixels;
        }

        private static void AssertTexture(MeshFigureCastSilhouette s, Color32[] expected, string why)
        {
            Color32[] got = s.Sprite.texture.GetPixels32();
            int differ = 0;
            for (int i = 0; i < got.Length; i++) if ((got[i].a != 0) != (expected[i].a != 0)) differ++;
            Assert.AreEqual(0, differ, why + ": the outline must be the posed mesh's own coverage, pixel for pixel");
        }

        [Test]
        public void Refresh_TracesThePosedMesh_AndRetracesOnlyOnANewPoseOrARealMove()
        {
            IsoCharacterFigureRenderer figure = Figure();
            Transform caster = Caster(figure.transform.position);
            var s = new MeshFigureCastSilhouette(32f);
            try
            {
                Assert.IsTrue(s.Refresh(figure, caster));
                Assert.AreEqual(1, s.TraceCount);
                Color32[] first = Expected(figure, caster, 32f);
                Assert.Greater(Count(first), 0, "the figure's posed quad must land inside the window");
                AssertTexture(s, first, "frame 0");

                Assert.IsTrue(s.Refresh(figure, caster));
                Assert.AreEqual(1, s.TraceCount, "the same pose in the same place re-traces nothing");

                caster.position += new Vector3(MeshFigureCastSilhouette.RetraceTolerance * 0.25f, 0f, 0f);
                s.Refresh(figure, caster);
                Assert.AreEqual(1, s.TraceCount, "a sub-tolerance drift re-traces nothing");

                Assert.IsTrue(figure.SetPose("idle", 1));
                s.Refresh(figure, caster);
                Assert.AreEqual(2, s.TraceCount, "a new pose is a new outline");
                AssertTexture(s, Expected(figure, caster, 32f), "frame 1");

                caster.position += new Vector3(0.1f, 0f, 0f);
                s.Refresh(figure, caster);
                Assert.AreEqual(3, s.TraceCount, "the figure moving off the body re-traces");
                AssertTexture(s, Expected(figure, caster, 32f), "moved");

                Sprite same = s.Sprite;
                Assert.AreSame(same, s.Sprite, "a re-trace rewrites the texels and keeps the sprite");
            }
            finally { s.Dispose(); }
        }

        // ------------------------------------------------------------------ the hand-over

        private static readonly MethodInfo ShadowAwake =
            typeof(SpriteShadow).GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly MethodInfo ShadowLateUpdate =
            typeof(SpriteShadow).GetMethod("LateUpdate", BindingFlags.NonPublic | BindingFlags.Instance);

        private Sprite StandingSprite()
        {
            var texture = new Texture2D(16, 32);
            _owned.Add(texture);
            var sprite = Sprite.Create(texture, new Rect(0, 0, 16, 32), new Vector2(0.5f, 0f), 32f);
            sprite.name = "player_stand_0";
            _owned.Add(sprite);
            return sprite;
        }

        private static SpriteRenderer ShadowRenderer(SpriteShadow caster)
        {
            Transform child = caster.transform.Find("SpriteShadow");   // SpriteShadow.EnsureShadow's child
            return child != null ? child.GetComponent<SpriteRenderer>() : null;
        }

        [Test]
        public void TheOverride_ReachesTheShadowsSpriteAndTexture_AndClearingGivesTheBodyBack()
        {
            Assert.That(ShadowAwake, Is.Not.Null, "SpriteShadow.Awake() not found (private API moved?)");
            Assert.That(ShadowLateUpdate, Is.Not.Null, "SpriteShadow.LateUpdate() not found (private API moved?)");
            Sprite standing = StandingSprite();
            var go = new GameObject("TestCastBodyShadow");
            _owned.Add(go);
            var body = go.AddComponent<SpriteRenderer>();
            body.sprite = standing;
            body.flipX = true;
            var shadow = go.AddComponent<SpriteShadow>();
            ShadowAwake.Invoke(shadow, null);
            ShadowLateUpdate.Invoke(shadow, null);

            var outline = new MeshFigureCastSilhouette(32f);
            try
            {
                shadow.SilhouetteOverride = outline.Sprite;
                ShadowLateUpdate.Invoke(shadow, null);
                SpriteRenderer drawn = ShadowRenderer(shadow);
                Assert.That(drawn, Is.Not.Null, "the shadow's own renderer was not found under the caster");
                Assert.AreSame(outline.Sprite, drawn.sprite, "the shadow must draw the mesh's outline, not the hidden standing sprite");
                var mpb = new MaterialPropertyBlock();
                drawn.GetPropertyBlock(mpb);
                Assert.AreSame(outline.Sprite.texture, mpb.GetTexture("_MainTex"), "the shadow's texture must be the outline's");
                Assert.IsTrue(shadow.TryGetLampShadowCaster(out _), "the outline is a lamp caster too");

                shadow.SilhouetteOverride = null;
                ShadowLateUpdate.Invoke(shadow, null);
                Assert.AreSame(standing, drawn.sprite, "clearing the override gives the body its own sprite back");
                drawn.GetPropertyBlock(mpb);
                Assert.AreSame(standing.texture, mpb.GetTexture("_MainTex"));
            }
            finally { outline.Dispose(); }
        }

        [Test]
        public void TheOverride_StandsInForAnExcludedCell_BecauseTheMeshIsWhatIsDrawn()
        {
            var texture = new Texture2D(16, 32);
            _owned.Add(texture);
            var splash = Sprite.Create(texture, new Rect(0, 0, 16, 32), new Vector2(0.5f, 0f), 32f);
            splash.name = "gull_splash_1_d5";   // a cell the art rules forbid from casting (the gull's own test)
            _owned.Add(splash);
            var go = new GameObject("TestCastExcluded");
            _owned.Add(go);
            go.AddComponent<SpriteRenderer>().sprite = splash;
            var shadow = go.AddComponent<SpriteShadow>();
            ShadowAwake.Invoke(shadow, null);
            Assume.That(shadow.StandsDownForExcludedArt, Is.True, "the splash cell is not excluded by the art rules any more");
            Assert.IsFalse(shadow.TryGetLampShadowCaster(out _));

            var outline = new MeshFigureCastSilhouette(32f);
            try
            {
                shadow.SilhouetteOverride = outline.Sprite;
                Assert.IsTrue(shadow.TryGetLampShadowCaster(out _),
                    "the hidden sprite's cell says nothing about the mesh: the outline casts");
            }
            finally { outline.Dispose(); }
        }

        [Test]
        public void TheMask_StampsTheOutlineUnflipped_AndGoesBackToTheBody()
        {
            Sprite standing = StandingSprite();
            var go = new GameObject("TestCastBodyMask");
            _owned.Add(go);
            go.SetActive(false);
            var body = go.AddComponent<SpriteRenderer>();
            body.sprite = standing;
            body.flipX = true;
            var mask = go.AddComponent<SilhouetteMaskSource>();
            go.SetActive(true);
            mask.SyncNow();
            Assume.That(mask.MaskRenderer, Is.Not.Null, "the mask child was not built in EditMode");
            Assert.AreSame(standing, mask.MaskRenderer.sprite);
            Assert.IsTrue(mask.MaskRenderer.flipX, "the body's flip is copied as before");

            var outline = new MeshFigureCastSilhouette(32f);
            try
            {
                mask.SilhouetteOverride = outline.Sprite;
                mask.SyncNow();
                Assert.AreSame(outline.Sprite, mask.MaskRenderer.sprite, "the foliage outline must be the mesh's");
                Assert.IsFalse(mask.MaskRenderer.flipX, "the outline already faces the way he does: never flipped again");

                mask.SilhouetteOverride = null;
                mask.SyncNow();
                Assert.AreSame(standing, mask.MaskRenderer.sprite);
                Assert.IsTrue(mask.MaskRenderer.flipX);
            }
            finally { outline.Dispose(); }
        }

        [Test]
        public void TheCastShape_HandsTheOutlineToBoth_AndTakesItBackTheFrameNoFigureDraws()
        {
            IsoCharacterFigureRenderer figure = Figure();
            Sprite standing = StandingSprite();
            var go = new GameObject("TestCastShapeBody");
            _owned.Add(go);
            go.transform.position = figure.transform.position;
            go.SetActive(false);
            var body = go.AddComponent<SpriteRenderer>();
            body.sprite = standing;
            var shadow = go.AddComponent<SpriteShadow>();
            var mask = go.AddComponent<SilhouetteMaskSource>();
            go.SetActive(true);
            ShadowAwake.Invoke(shadow, null);

            var shape = new MeshFigureCastShape();
            try
            {
                shape.Sync(body, figure);
                Assert.That(shape.Current, Is.Not.Null);
                Assert.AreSame(shape.Current, shadow.SilhouetteOverride);
                Assert.AreSame(shape.Current, mask.SilhouetteOverride);
                Assert.AreEqual(standing.pixelsPerUnit, shape.Current.pixelsPerUnit, "the outline sits on the body's own pixel grid");

                shape.Sync(body, figure);
                Assert.AreEqual(1, shape.Silhouette.TraceCount, "a still frame costs no trace");

                shape.Sync(body, null);
                Assert.That(shadow.SilhouetteOverride, Is.Null, "the frame the body draws, the body casts");
                Assert.That(mask.SilhouetteOverride, Is.Null);

                // At the oars: the body is disabled and there is no walking deck, so neither the shadow
                // nor the mask draws, and nothing is traced for them.
                body.enabled = false;
                Assert.IsFalse(shadow.WouldCast);
                Assert.IsTrue(figure.SetPose("idle", 1));
                shape.Sync(body, figure);
                Assert.AreEqual(1, shape.Silhouette.TraceCount, "no trace for a shadow nobody sees");
                Assert.That(shape.Current, Is.Null);
                body.enabled = true;

                // The figure drawn far from its body (aboard, the body can be left off the seat): the window
                // is empty, and an empty outline must not erase the shadow, so the body's sprite casts.
                go.transform.position = figure.transform.position + new Vector3(10f, 0f, 0f);
                shape.Sync(body, figure);
                Assert.AreEqual(2, shape.Silhouette.TraceCount);
                Assert.AreEqual(0, shape.Silhouette.CoveredPixels);
                Assert.That(shape.Current, Is.Null, "an empty trace hands nothing over");
                Assert.That(shadow.SilhouetteOverride, Is.Null);
                Assert.That(mask.SilhouetteOverride, Is.Null);
            }
            finally { shape.Dispose(); }
        }
    }
}
