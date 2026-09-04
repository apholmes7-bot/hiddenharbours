using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// <see cref="LampShadowSystem"/>'s pairing and pooling rules, driven explicitly — the shape
    /// <c>WaterLightBridgeTests</c> established. Edit mode runs no <c>OnEnable</c>, so lamps and
    /// casters are registered by hand here exactly as their runtime hooks do; the hooks themselves
    /// are proved in <c>LampShadowPlayTests</c>.
    ///
    /// <para><b>Teardown matters.</b> The registries are static and the day/night tint is a global;
    /// both are put back so no later fixture inherits this one's lamps or its night.</para>
    /// </summary>
    public class LampShadowSystemTests
    {
        private static readonly int IdDayNightTint = Shader.PropertyToID("_DayNightTint");
        private static readonly int IdHullIds = Shader.PropertyToID("_HullIds");
        private static readonly int IdShadowColor = Shader.PropertyToID("_ShadowColor");

        /// <summary>The shipped 02:00 tint (ADR 0013's profile at the dead of night): deep enough that every gate is open.</summary>
        private static readonly Color Night = new Color(0.016f, 0.020f, 0.040f, 1f);

        private const string DoryMeshPath = "Assets/_Project/Data/Boats/HullMeshes/DoryIsoHullMesh.asset";

        private readonly List<Object> _spawned = new List<Object>();
        private LampShadowSystem _system;
        private Camera _cam;
        private Color _tintBefore;

        /// <summary>A caster with no scene behind it — the interface is all the system needs.</summary>
        private sealed class FakeCaster : ILampShadowCaster
        {
            public LampShadowCasterState State;
            public bool Valid = true;
            public bool TryGetLampShadowCaster(out LampShadowCasterState state) { state = State; return Valid && state.IsValid; }
        }

        [SetUp]
        public void SetUp()
        {
            LampShadowSystem.ClearRegistries();
            _tintBefore = Shader.GetGlobalColor(IdDayNightTint);
            Shader.SetGlobalColor(IdDayNightTint, Night);

            var host = new GameObject("LampShadowSystemTestHost") { hideFlags = HideFlags.HideAndDontSave };
            _spawned.Add(host);
            _system = host.AddComponent<LampShadowSystem>();
            _system.Profile = LampShadowProfile.CreateDefault();
            _spawned.Add(_system.Profile);

            var camGo = new GameObject("LampShadowTestCam");
            _spawned.Add(camGo);
            _cam = camGo.AddComponent<Camera>();
            _cam.orthographic = true;
            _cam.transform.position = new Vector3(0f, 0f, -10f);
            _cam.nearClipPlane = 0.3f;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object o in _spawned) if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
            LampShadowSystem.ClearRegistries();
            Shader.SetGlobalColor(IdDayNightTint, _tintBefore);
        }

        // ---- helpers ------------------------------------------------------------------------------

        private SceneLight Lamp(Vector2 at, float range = 9f, float halfAngle = 180f, Vector2? facing = null)
        {
            var go = new GameObject("lamp");
            _spawned.Add(go);
            go.transform.position = new Vector3(at.x, at.y, 0f);
            if (facing.HasValue) go.transform.up = new Vector3(facing.Value.x, facing.Value.y, 0f);
            var light = go.AddComponent<SceneLight>();
            light.Shape = halfAngle >= 180f ? SceneLight.LightShape.Radial : SceneLight.LightShape.Cone;
            light.ConeHalfAngle = Mathf.Min(halfAngle, 180f);
            light.Range = range;
            light.Intensity = 1.5f;
            LampShadowSystem.RegisterLight(light);   // what OnEnable does at runtime (edit mode runs none)
            return light;
        }

        private Texture2D Sheet()
        {
            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            _spawned.Add(tex);
            return tex;
        }

        private FakeCaster Caster(Vector2 foot, float width = 0.5f, float height = 2f)
        {
            var caster = new FakeCaster
            {
                State = new LampShadowCasterState
                {
                    Foot = foot,
                    RectMin = new Vector2(foot.x - width * 0.5f, foot.y),
                    RectMax = new Vector2(foot.x + width * 0.5f, foot.y + height),
                    Sheet = Sheet(),
                    UvRect = new Vector4(0f, 0f, 1f, 1f),
                },
            };
            LampShadowSystem.RegisterCaster(caster);
            return caster;
        }

        private int EnabledSlots()
        {
            int n = 0;
            for (int i = 0; i < _system.PoolSize; i++)
                if (_system.SlotRenderer(i) != null && _system.SlotRenderer(i).enabled) n++;
            return n;
        }

        // ---- the pairing ----------------------------------------------------------------------------

        [Test]
        public void ACasterInsideALampsRange_GetsAShadow_AndOneOutsideDoesNot()
        {
            Lamp(Vector2.zero, range: 9f);
            FakeCaster inside = Caster(new Vector2(3f, 0f));
            Caster(new Vector2(30f, 0f));

            _system.PublishFrame(_cam);

            Assert.AreEqual(1, _system.ActiveShadowCount, "one pair is in range");
            Assert.AreSame(inside, _system.SlotCaster(0));
            Assert.AreEqual(1, EnabledSlots(), "one quad drawn, the rest pooled off");
        }

        [Test]
        public void AtNoon_TheLampsThrowNothing_TheNoonControl()
        {
            Lamp(Vector2.zero);
            Caster(new Vector2(3f, 0f));
            Shader.SetGlobalColor(IdDayNightTint, Color.white);   // a bright noon: the gate is shut

            _system.PublishFrame(_cam);

            Assert.AreEqual(0, _system.ActiveShadowCount, "the shadow gates with its lamp — nothing at noon");
            Assert.AreEqual(0, EnabledSlots());
        }

        [Test]
        public void AtStrengthZero_NothingIsDrawn_ThePassthrough()
        {
            Lamp(Vector2.zero);
            Caster(new Vector2(3f, 0f));
            _system.Profile.Strength = 0f;

            _system.PublishFrame(_cam);

            Assert.AreEqual(0, _system.ActiveShadowCount);
            Assert.AreEqual(0, EnabledSlots(), "strength 0 is today's frame: no quad is even enabled");
        }

        [Test]
        public void ALampThatOptsOut_OrIsDark_ThrowsNothing()
        {
            SceneLight lamp = Lamp(Vector2.zero);
            Caster(new Vector2(3f, 0f));

            lamp.CastsShadows = false;
            _system.PublishFrame(_cam);
            Assert.AreEqual(0, _system.ActiveShadowCount, "opted out");

            lamp.CastsShadows = true;
            lamp.Intensity = 0f;
            _system.PublishFrame(_cam);
            Assert.AreEqual(0, _system.ActiveShadowCount, "a dark lamp throws nothing");
        }

        [Test]
        public void ACasterOutsideTheCone_ThrowsNothing_EvenInsideTheRange()
        {
            Lamp(Vector2.zero, range: 9f, halfAngle: 26f, facing: Vector2.up);
            FakeCaster ahead = Caster(new Vector2(0f, 3f));
            Caster(new Vector2(3f, 0f));   // abeam: 90° off a 26° cone

            _system.PublishFrame(_cam);

            // Both are paired (the pairing is by range); only the one in the beam is DRAWN.
            Assert.AreEqual(1, EnabledSlots(), "the abeam caster is paired but its alpha is 0 — nothing drawn");
            int drawn = -1;
            for (int i = 0; i < _system.PoolSize; i++)
                if (_system.SlotRenderer(i).enabled) drawn = i;
            Assert.AreSame(ahead, _system.SlotCaster(drawn));
        }

        [Test]
        public void PastThePool_TheNearestPairsWin()
        {
            _system.Profile.MaxShadows = 3;
            Lamp(Vector2.zero, range: 20f);
            // Register FAR to NEAR so a system that kept the first three it saw would fail.
            FakeCaster c5 = Caster(new Vector2(5f, 0f));
            FakeCaster c4 = Caster(new Vector2(4f, 0f));
            FakeCaster c3 = Caster(new Vector2(3f, 0f));
            FakeCaster c2 = Caster(new Vector2(2f, 0f));
            FakeCaster c1 = Caster(new Vector2(1f, 0f));

            _system.PublishFrame(_cam);

            Assert.AreEqual(3, _system.PoolSize, "the pool is the profile's budget");
            Assert.AreEqual(3, _system.ActiveShadowCount, "saturates at the budget, never past it");
            Assert.AreSame(c1, _system.SlotCaster(0), "nearest first");
            Assert.AreSame(c2, _system.SlotCaster(1));
            Assert.AreSame(c3, _system.SlotCaster(2));
            Assert.IsNull(_system.SlotCaster(3));
            Assert.AreNotSame(c4, _system.SlotCaster(2));
            Assert.AreNotSame(c5, _system.SlotCaster(2));
        }

        [Test]
        public void UnregisteringTheLamp_ReleasesItsShadows()
        {
            SceneLight lamp = Lamp(Vector2.zero);
            Caster(new Vector2(3f, 0f));
            _system.PublishFrame(_cam);
            Assert.AreEqual(1, _system.ActiveShadowCount, "precondition");

            LampShadowSystem.UnregisterLight(lamp);
            _system.PublishFrame(_cam);
            Assert.AreEqual(0, _system.ActiveShadowCount, "a dead lamp must not leave a shadow lying on the ground");
            Assert.AreEqual(0, EnabledSlots());
        }

        [Test]
        public void RegisteringTwice_DoesNotDoubleTheShadow()
        {
            SceneLight lamp = Lamp(Vector2.zero);
            FakeCaster c = Caster(new Vector2(3f, 0f));
            LampShadowSystem.RegisterLight(lamp);
            LampShadowSystem.RegisterCaster(c);

            _system.PublishFrame(_cam);
            Assert.AreEqual(1, _system.ActiveShadowCount, "one lamp, one caster, one shadow");
        }

        // ---- the quad: where it sorts, where it sits ------------------------------------------------

        [Test]
        public void TheShadowQuad_SortsAtTheCeiling_AndSitsNearerThanAGlowQuad()
        {
            Lamp(Vector2.zero);
            Caster(new Vector2(3f, 0f));
            _system.PublishFrame(_cam);

            MeshRenderer mr = _system.SlotRenderer(0);
            Assert.IsTrue(mr.enabled);
            Assert.AreEqual(SceneLight.MaxSortingOrder, mr.sortingOrder,
                "the shadow draws at the same ceiling order as the glow it darkens");
            Assert.Greater(mr.sortingOrder, 0, "and it fits in the 16-bit field — no wrap");
            Assert.AreEqual(SceneLight.MaxSortingOrder, mr.GetComponent<SortingGroup>().sortingOrder);

            float expected = LampShadowSystem.PinnedDepth(_cam);
            Assert.AreEqual(expected, mr.transform.position.z, 1e-5f, "pinned in front of the camera");
            float glowZ = LightMath.CameraDepthZ(_cam.transform.position.z, _cam.transform.forward.z,
                                                _cam.nearClipPlane, SceneLight.DefaultCameraDepthOffset);
            Assert.Less(mr.transform.position.z, glowZ, "nearer than a light quad, so it draws after the glow");
        }

        [Test]
        public void TheShadowQuad_CoversTheShearedSilhouette_AndCarriesTheLampsAlphaAtTheFeet()
        {
            SceneLight lamp = Lamp(new Vector2(-3f, 0f));
            FakeCaster c = Caster(new Vector2(0f, 0f), width: 0.5f, height: 2f);
            _system.PublishFrame(_cam);

            MeshRenderer mr = _system.SlotRenderer(0);
            LampShadowProfile p = _system.Profile;
            // Predict from the same maths: the shadow runs +x, so the quad must reach past the caster's own rect.
            Vector2 foot = LampShadowMath.SnapToPixels(c.State.Foot, p.PixelsPerUnit);
            Vector2 dir = LampShadowMath.ShadowDirection(lamp.WorldOrigin, foot, Vector2.down);
            float len = LampShadowMath.ShadowLengthMultiple(
                LampShadowMath.LampElevation(lamp.LampHeightMeters, 3f, p.MinLampHeightMeters),
                p.LengthAtNoon, p.LengthAtHorizon, p.MaxLength);
            LampShadowMath.ShearedBounds(c.State.RectMin, c.State.RectMax, foot, dir, len, out Vector2 bmin, out Vector2 bmax);

            Vector3 pos = mr.transform.position;
            Vector3 scale = mr.transform.localScale;
            Assert.LessOrEqual(pos.x, bmin.x + 1e-4f);
            Assert.GreaterOrEqual(pos.x + scale.x, bmax.x - 1e-4f);
            Assert.LessOrEqual(pos.y, bmin.y + 1e-4f);
            Assert.GreaterOrEqual(pos.y + scale.y, bmax.y - 1e-4f);
            Assert.Greater(bmax.x, c.State.RectMax.x + 1f, "the rake genuinely extends past the caster");

            var mpb = new MaterialPropertyBlock();
            mr.GetPropertyBlock(mpb);
            float alpha = mpb.GetColor(IdShadowColor).a;
            float shape = LampShadowMath.LampShapeAtFoot(lamp.WorldOrigin, lamp.BeamDirection, lamp.Range, 180f,
                                                         lamp.AngularSoftness, lamp.EdgeSoftness, foot);
            Assert.AreEqual(p.Strength * shape, alpha, 1e-4f,
                "alpha = strength × the lamp's own falloff at the feet (the gate is fully open at 02:00)");
            Assert.Greater(alpha, 0f);
            Assert.Less(alpha, p.Strength, "three metres out, the radial falloff has already taken some");
        }

        // ---- casters -------------------------------------------------------------------------------

        [Test]
        public void ASpriteShadowCaster_PublishesItsCellRectItsFeetAndItsSheet()
        {
            var tex = new Texture2D(16, 96, TextureFormat.RGBA32, false);
            _spawned.Add(tex);
            // The second row of a two-row sheet, pivot a tenth of the way up — the shape every caster in production is.
            var sprite = Sprite.Create(tex, new Rect(0f, 48f, 16f, 48f), new Vector2(0.5f, 0.1f), 32f);
            _spawned.Add(sprite);

            var go = new GameObject("post");
            _spawned.Add(go);
            go.transform.position = new Vector3(4f, 2f, 0f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            var caster = go.AddComponent<SpriteShadow>();

            Assert.IsTrue(caster.TryGetLampShadowCaster(out LampShadowCasterState s));
            Assert.AreEqual(4f, s.Foot.x, 1e-5f);
            Assert.AreEqual(2f, s.Foot.y, 1e-5f, "the feet are the pivot (foot offset 0)");
            Assert.AreEqual(4f - 0.25f, s.RectMin.x, 1e-5f);
            Assert.AreEqual(2f - 4.8f / 32f, s.RectMin.y, 1e-5f, "the cell hangs 4.8 px below the pivot");
            Assert.AreEqual(4f + 0.25f, s.RectMax.x, 1e-5f);
            Assert.AreEqual(s.RectMin.y + 1.5f, s.RectMax.y, 1e-5f);
            Assert.AreSame(tex, s.Sheet);
            Assert.AreEqual(new Vector4(0f, 0.5f, 1f, 0.5f), s.UvRect, "the second row's uv");
            Assert.IsFalse(s.IsHull);
        }

        [Test]
        public void AMeshHull_IsACasterWithTheHullMaterial_AndHandsItsIdsToTheShader()
        {
            var def = AssetDatabase.LoadAssetAtPath<HullMeshDef>(DoryMeshPath);
            Assert.IsNotNull(def, "the dory's hull mesh def must exist");

            var host = new GameObject("dory");
            _spawned.Add(host);
            var hull = host.AddComponent<IsoFacetHullRenderer>();   // [ExecuteAlways]: OnEnable registers her id
            hull.Configure(IsoFacetHullPresentationService.ToSetup(def));
            hull.ApplyPose();
            Assert.Greater(hull.HullId, 0, "precondition: she holds a facet id");

            HullLampShadowCaster caster = HullLampShadowCaster.Fit(host);
            Assert.AreSame(caster, HullLampShadowCaster.Fit(host), "Fit is idempotent");
            LampShadowSystem.RegisterCaster(caster);

            Assert.IsTrue(caster.TryGetLampShadowCaster(out LampShadowCasterState s));
            Assert.IsTrue(s.IsHull);
            Assert.Greater(s.RectMax.x - s.RectMin.x, 1f, "her overlay quad is the cell, metres wide");
            Assert.Greater(s.RectMax.y - s.RectMin.y, 1f);
            Assert.AreEqual(0f, s.Foot.x, 1e-4f, "her feet are her waterline pivot at the root");
            Assert.AreEqual(0f, s.Foot.y, 1e-4f);

            Lamp(new Vector2(-4f, 0f), range: 12f);
            _system.PublishFrame(_cam);

            Assert.AreEqual(1, _system.ActiveShadowCount);
            Assert.IsTrue(_system.SlotIsHull(0), "a hull draws with the screen-texture variant");
            Assert.AreEqual(LampShadowSystem.HullMaterialPath, _system.SlotRenderer(0).sharedMaterial.name);

            var mpb = new MaterialPropertyBlock();
            _system.SlotRenderer(0).GetPropertyBlock(mpb);
            Vector4 ids = mpb.GetVector(IdHullIds);
            Assert.AreEqual(hull.HullId / 255f, ids.x, 1e-6f, "her id, over 255, as the overlay shader reads it");
            Assert.AreEqual(hull.ForeHullId / 255f, ids.y, 1e-6f);
            Assert.AreEqual(IsoFacetHullRenderer.DeckOccupantSlots, ids.z, 1e-6f);
        }

        [Test]
        public void ThePresentationService_FitsAHullCasterWhereItFitsHerLamps()
        {
            var def = AssetDatabase.LoadAssetAtPath<HullMeshDef>(DoryMeshPath);
            Assert.IsNotNull(def);
            var host = new GameObject("dory-installed");
            _spawned.Add(host);

            var service = new IsoFacetHullPresentationService();
            Assert.IsNotNull(service.Install(host, def), "the dory installs as a mesh hull");
            Assert.IsNotNull(host.GetComponent<HullLampShadowCaster>(),
                "every mesh hull the service builds throws — boats cast (owner ruling 2026-08-05)");
        }

        [Test]
        public void ThePresentationService_TakesTheCasterWithTheRendererItRemoves()
        {
            var def = AssetDatabase.LoadAssetAtPath<HullMeshDef>(DoryMeshPath);
            Assert.IsNotNull(def);
            var host = new GameObject("dory-removed");
            _spawned.Add(host);

            var service = new IsoFacetHullPresentationService();
            Assert.IsNotNull(service.Install(host, def));
            Assert.IsNotNull(host.GetComponent<HullLampShadowCaster>(), "precondition: she casts");

            service.Remove(host);
            Assert.IsNull(host.GetComponent<HullLampShadowCaster>(),
                "a host sent back to the sprite path has no id block to cast with — the caster leaves with the renderer");
        }

        /// <summary>
        /// <b>The owner's dial is an ASSET, and the asset is the shipped numbers.</b> Rule 6: the tunables live
        /// where the owner can edit them, and <c>Resources/LampShadowProfile.asset</c> is what
        /// <see cref="LampShadowSystem"/> loads at <see cref="LampShadowSystem.ProfileResourcePath"/>. This
        /// holds that asset to the code defaults value by value (the GameConfigAssetCoverage pattern), so a
        /// stale asset — a field added to the code and never written to the file, or a value that drifted —
        /// reddens here rather than silently shipping a different look from the one the tests describe.
        /// </summary>
        [Test]
        public void TheShippedProfileAsset_LoadsFromResources_AndCarriesTheCodeDefaults()
        {
            var asset = Resources.Load<LampShadowProfile>(LampShadowSystem.ProfileResourcePath);
            Assert.IsNotNull(asset,
                $"Resources/{LampShadowSystem.ProfileResourcePath}.asset is missing — the owner has no dial " +
                "for Strength or the length curve, and the system is running on the code defaults instead");

            var code = LampShadowProfile.CreateDefault();
            _spawned.Add(code);
            Assert.AreEqual(code.Strength, asset.Strength, 1e-6f, "Strength");
            Assert.AreEqual(code.ShadowColor, asset.ShadowColor, "ShadowColor");
            Assert.AreEqual(code.MaxShadows, asset.MaxShadows, "MaxShadows");
            Assert.AreEqual(code.RefreshHz, asset.RefreshHz, 1e-6f, "RefreshHz");
            Assert.AreEqual(code.LengthAtNoon, asset.LengthAtNoon, 1e-6f, "LengthAtNoon");
            Assert.AreEqual(code.LengthAtHorizon, asset.LengthAtHorizon, 1e-6f, "LengthAtHorizon");
            Assert.AreEqual(code.MaxLength, asset.MaxLength, 1e-6f, "MaxLength");
            Assert.AreEqual(code.MinLampHeightMeters, asset.MinLampHeightMeters, 1e-6f, "MinLampHeightMeters");
            Assert.AreEqual(code.MinShearDenominator, asset.MinShearDenominator, 1e-6f, "MinShearDenominator");
            Assert.AreEqual(code.PixelSnap, asset.PixelSnap, "PixelSnap");
            Assert.AreEqual(code.PixelsPerUnit, asset.PixelsPerUnit, 1e-6f, "PixelsPerUnit");

            // And the FILE carries every serialized field the code declares, and none the code no longer
            // does — a key missing from the YAML deserialises to the C# default and would pass the value
            // checks above by accident only while the two happen to agree.
            string path = AssetDatabase.GetAssetPath(asset);
            string yaml = System.IO.File.ReadAllText(path);
            var declared = new List<string>();
            foreach (var f in typeof(LampShadowProfile).GetFields(
                         System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic))
                if (f.GetCustomAttributes(typeof(SerializeField), true).Length > 0) declared.Add(f.Name);
            Assert.Greater(declared.Count, 5, "the profile declares its tunables as serialized fields");
            foreach (string name in declared)
                StringAssert.Contains("\n  " + name + ":", yaml, $"{path} does not carry '{name}' — the asset is behind the code");
            foreach (System.Text.RegularExpressions.Match m in
                     System.Text.RegularExpressions.Regex.Matches(yaml, @"(?m)^  (_[A-Za-z0-9]+):"))
                Assert.Contains(m.Groups[1].Value, declared, $"{path} carries '{m.Groups[1].Value}', which the code no longer declares");
        }

        [Test]
        public void TheCodeDefaults_AreTheShippedNumbers()
        {
            var p = LampShadowProfile.CreateDefault();
            _spawned.Add(p);
            Assert.AreEqual(0.8f, p.Strength, 1e-6f);
            Assert.AreEqual(24, p.MaxShadows);
            Assert.AreEqual(10f, p.RefreshHz, 1e-6f);
            Assert.AreEqual(0.35f, p.LengthAtNoon, 1e-6f);
            Assert.AreEqual(5f, p.LengthAtHorizon, 1e-6f);
            Assert.AreEqual(7f, p.MaxLength, 1e-6f);
            Assert.AreEqual(0.5f, p.MinLampHeightMeters, 1e-6f);
            Assert.IsTrue(p.PixelSnap);
            Assert.AreEqual(32f, p.PixelsPerUnit, 1e-6f);
        }

        /// <summary>
        /// <b>⭐ THE CARRIER RULE.</b> A lamp POST carries its light and its own sun-shadow caster on ONE
        /// GameObject, which no other object in the game does — so its lamp-to-feet distance is just the
        /// light's origin offset, the smallest anywhere, and <see cref="LampShadowSystem"/> would sort it
        /// to the very front of the nearest-N pool. Every post would then spend a pooled quad throwing a
        /// stub of itself at its own foot, and the bollard four metres away — the thing the lamp was put
        /// there to reveal — would be crowded out of the budget.
        ///
        /// <para>Driven at a pool of ONE so the crowding is the assertion, not a detail: with the rule the
        /// single slot goes to the bollard, and the post's self-pair never enters the sort at all.</para>
        /// </summary>
        [Test]
        public void ALampNeverThrowsTheSilhouetteOfTheCasterItIsMountedOn_TheCarrierRule()
        {
            _system.Profile.MaxShadows = 1;   // the pool is sized inside PublishFrame

            // The post: one GameObject carrying BOTH the lamp and the caster, the way LampPosts builds it.
            var tex = new Texture2D(16, 96, TextureFormat.RGBA32, false);
            _spawned.Add(tex);
            var sprite = Sprite.Create(tex, new Rect(0f, 0f, 16f, 96f), new Vector2(0.5f, 0f), 32f);
            _spawned.Add(sprite);

            var post = new GameObject("lampPost");
            _spawned.Add(post);
            post.transform.position = Vector3.zero;
            post.AddComponent<SpriteRenderer>().sprite = sprite;
            var light = post.AddComponent<SceneLight>();
            light.Shape = SceneLight.LightShape.Radial;
            light.ConeHalfAngle = 180f;
            light.Range = 9f;
            light.Intensity = 1.5f;
            light.OriginOffset = new Vector2(0f, -0.2f);      // the Lightpost preset's own offset
            LampShadowSystem.RegisterLight(light);
            var self = post.AddComponent<SpriteShadow>();
            LampShadowSystem.RegisterCaster(self);

            // What the lamp is FOR: something else, four metres away and therefore much further from the
            // lamp than the post is from itself.
            FakeCaster bollard = Caster(new Vector2(4f, 0f));

            Assert.IsTrue(self.TryGetLampShadowCaster(out LampShadowCasterState mine),
                "the post really is a valid caster — the rule has to exclude it, not rely on it failing");
            Assert.Less((mine.Foot - light.WorldOrigin).sqrMagnitude, 1f,
                "and it really is the nearest thing to its own lamp, which is why it would win the sort");

            _system.PublishFrame(_cam);

            Assert.AreEqual(1, _system.ActiveShadowCount, "the one slot is spent");
            Assert.AreSame(bollard, _system.SlotCaster(0),
                "and it is spent on the bollard, not on the post throwing a stub of itself at its own foot");
        }

        /// <summary>
        /// The rule is about the CARRIER, not about being close: a caster standing right beside a lamp on
        /// its own GameObject still throws. Without this the "fix" could have been a minimum-distance
        /// cutoff, which would have silently stopped a crate against a post from casting at all.
        /// </summary>
        [Test]
        public void ACasterBesideALampButNotOnIt_StillThrows()
        {
            SceneLight lamp = Lamp(Vector2.zero, range: 9f);
            FakeCaster hardUpAgainstIt = Caster(new Vector2(0.2f, 0f));

            _system.PublishFrame(_cam);

            Assert.AreEqual(1, _system.ActiveShadowCount);
            Assert.AreSame(hardUpAgainstIt, _system.SlotCaster(0),
                "proximity is not the rule; sharing a GameObject with the lamp is");
            Assert.NotNull(lamp);
        }

    }
}
