using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using HiddenHarbours.Art;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using HiddenHarbours.Player;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// <b>THE PICTURE — AND WHAT A PICTURE CAN AND CANNOT PROVE.</b>
    ///
    /// <para><see cref="DeckRiderMeshPresenterPlayTests"/> is the behaviour fixture: it reads vertices,
    /// never pixels, and runs everywhere including CI. This one reads pixels, so it is GPU-gated and
    /// self-skips headless. It exists because two claims in this PR cannot be made out of vertices —
    /// that flipping the switch really changes the PICTURE, and that flipping it back leaves the sprite
    /// path exactly as it found it.</para>
    ///
    /// <para><b>The rig here is the SHIPPED art, and that is the whole reason for a second fixture.</b>
    /// The behaviour fixture builds a synthetic rig — a four-vertex quad hull, a one-colour ramp, a 4x4
    /// placeholder sprite cell — which is right for asserting wiring and useless for judging a look: a
    /// fidelity pair shot against a placeholder square would flatter the mesh enormously. So this one
    /// loads the committed <c>DoryIso</c> facet hull and the committed <c>FisherIso</c> character def
    /// with the baked <c>charskin.fisher</c> hanging off it, and skips honestly if they are not here.
    /// </para>
    ///
    /// <para><b>Four arms, one run, one compile.</b> A plate fixture is deterministic WITHIN a run and
    /// not across runs — a single removed blank line has moved all sixteen hashes of a plate suite
    /// before now. So "shoot main, shoot this branch, compare bytes" is not an honest method and is not
    /// attempted here: the two trees differ by a recompile by construction. Everything compared below is
    /// shot in THIS process, seconds apart, off one build.</para>
    ///
    /// <list type="number">
    /// <item><b>A1</b> — toggle 0, cold. The presenter component has never been added.</item>
    /// <item><b>A1-prime</b> — toggle 0 again, nothing changed. <b>THE FLOOR.</b> Two frames of an
    /// unchanged scene. If these differ, this instrument has a noise floor and cannot support ANY
    /// byte-identity claim, and the test says so in those words rather than reporting its own noise as
    /// a finding about the presenter.</item>
    /// <item><b>B</b> — toggle 1. The skinned figure draws through the facet pass.</item>
    /// <item><b>A2</b> — toggle 0 again, WARM: the presenter now exists, has run, and is asked to stand
    /// down. Strictly harder than A1's cold case, which is why it is shot last.</item>
    /// </list>
    ///
    /// <para><b>The discriminator is asserted before the identity.</b> Twice-burned law: N identical
    /// hashes across a swept property mean the property never reached the drawing renderer. A presenter
    /// that silently did nothing would produce four matching plates and sail through the residue check
    /// as a clean pass — so <c>hash(B) != hash(A1)</c> is a hard assertion, not an observation.</para>
    ///
    /// <para><b>What this does NOT prove: byte-identity against <c>main</c>.</b> No plate can, for the
    /// reason above. That claim is made structurally in the PR body, line by line — with the switch off
    /// the presenter is never added and every expression that mentions a figure collapses to the line
    /// that was already there. A1 == A2 is the runtime half of the argument, and the half a picture CAN
    /// carry: the mesh path, having run, leaves no residue on the sprite path.</para>
    ///
    /// <para>⚠ No wave motion, no oars, and <see cref="FacetService"/> rather than the shipping
    /// presentation service — which also installs churn, a reflector, lamps and a shadow caster. Not to
    /// flatter the result: those are the things in this scene that advance on ENGINE time, and a sea
    /// that moves between two otherwise identical frames puts a floor under the floor for reasons that
    /// have nothing to do with this PR.</para>
    /// </summary>
    public class DeckRiderMeshPresenterPlatePlayTests
    {
        private const string PlateDir = "mesh-character-presenter";
        private const string DoryVisualPath = "Assets/_Project/Data/Boats/Visuals/DoryIso.asset";
        private const string FisherVisualPath = "Assets/_Project/Data/Characters/FisherIso.asset";

        /// <summary>The plate is sampled at this multiple of the hull's own art cell, so the figure is
        /// big enough to judge. The FRAMING stays the cell's — only the sampling is finer.</summary>
        private const int Supersample = 4;

        private const int Seed = 1337;
        private const double ClockOrigin = 1000.0;

        private readonly List<Object> _spawned = new List<Object>();
        private IHullMeshPresentationService _previousService;
        private Camera _cam;
        private RenderTexture _rt;

        [SetUp]
        public void SetUp()
        {
            _previousService = HullMeshPresentation.Service;
            GameServices.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            HullMeshPresentation.Service = _previousService;
            GameServices.Reset();

            if (_cam != null) _cam.targetTexture = null;
            if (_rt != null) { _rt.Release(); Object.DestroyImmediate(_rt); _rt = null; }
            for (int i = _spawned.Count - 1; i >= 0; i--)
                if (_spawned[i] != null) Object.DestroyImmediate(_spawned[i]);
            _spawned.Clear();
        }

        // ---- services ------------------------------------------------------------------------------

        /// <summary>A stopped clock. The pose is a function of <c>(worldSeed, gameTime)</c>, so holding
        /// both still is what makes two shots of an unchanged scene comparable at all.</summary>
        private sealed class FrozenClock : IGameClock
        {
            public double TotalSeconds { get; set; } = ClockOrigin;
            public GameTime Now => new GameTime(TotalSeconds);
            public Season Season => Season.EarlySpring;
            public int Year => 1;
            public int DayIndex => 0;
            public int DayOfSeason => 1;
            public Weekday Weekday => Weekday.Monday;
            public bool IsMarketDay => false;
            public float HourOfDay => 0f;
            public float DayFraction => 0f;
            public bool IsPaused { get; set; }
            public float TimeScale { get; set; } = 1f;
        }

        private sealed class SeedEnv : IEnvironmentService
        {
            public int WorldSeed => Seed;
            public TideProfile ActiveTideProfile { get; set; }
            public EnvironmentSample Sample() => new EnvironmentSample(
                Vector2.zero, Vector2.zero, tideHeight: 0f,
                HiddenHarbours.Core.SeaState.Calm, visibility: 1f, seaState01: 0f);
            public float TideHeightAt(double totalSeconds) => 0f;
            public float WaterLevelAt(double totalSeconds) => 0f;
        }

        /// <summary>Installs the REAL facet renderer and records the instance — ⚠ every claim about a
        /// plate must be made against the renderer that is actually drawing. It adds nothing the
        /// shipping service adds beyond the hull itself (see the class doc).</summary>
        private sealed class FacetService : IHullMeshPresentationService
        {
            public IsoFacetHullRenderer Renderer { get; private set; }

            public IHullMeshRenderer Install(GameObject host, HullMeshDef def,
                                             HullPaintSchemeDef scheme = null)
            {
                if (host == null || def == null || !def.IsUsable()) return null;
                var r = host.GetComponent<IsoFacetHullRenderer>();
                if (r == null) r = host.AddComponent<IsoFacetHullRenderer>();
                r.Configure(IsoFacetHullPresentationService.ToSetup(def, scheme));
                Renderer = r;
                return r;
            }

            /// <summary>⭐ <b>DELEGATED to the shipping service, not stubbed.</b> The dory wears an
            /// outboard, and a refused fitting is not a quiet omission: <c>BoatHullSkinner</c> logs an
            /// error and REMOVES the engine — "a boat with half a twin is worse than the honest
            /// failure" — so a stub here plates a mutilated boat in BOTH arms, and NUnit fails the
            /// test on the unhandled error besides.
            ///
            /// <para>Borrowing it costs nothing this fixture was avoiding. The shipping
            /// <c>AttachProp</c> is pure geometry — no churn, no reflector, no lamps, no shadow
            /// caster; those live in <c>Install</c>, which is the one member still overridden above.
            /// And with no <c>BoatController</c> wired, <c>OutboardMotorMeshLayer</c> settles on its
            /// centre column on the first LateUpdate and never leaves it, so the engine is as still
            /// between two shots as the hull is.</para></summary>
            private readonly IsoFacetHullPresentationService _fittings =
                new IsoFacetHullPresentationService();

            public IHullPropRenderer AttachProp(GameObject host, HullPropMeshDef def, string slot)
                => _fittings.AttachProp(host, def, slot);
            public void DetachProps(GameObject host) => _fittings.DetachProps(host);
            public void DetachProp(GameObject host, string slot) => _fittings.DetachProp(host, slot);
            public void Remove(GameObject host) => _fittings.Remove(host);
        }

        // ---- the shipped art -----------------------------------------------------------------------

        private static T LoadCommitted<T>(string path) where T : Object
        {
#if UNITY_EDITOR
            var a = UnityEditor.AssetDatabase.LoadAssetAtPath<T>(path);
            if (a == null)
                Assert.Ignore($"SKIPPED, NOT VERIFIED — {path} is not in this tree. This fixture " +
                              "photographs the SHIPPED art; there is nothing honest to shoot without it.");
            return a;
#else
            Assert.Ignore("SKIPPED, NOT VERIFIED — needs the editor's asset database: these are the " +
                          "REAL committed defs, not a mirror of them.");
            return null;
#endif
        }

        private sealed class Rig
        {
            public GameObject BoatRoot;
            public DeckRiderVisual Rider;
            public IsoFacetHullRenderer Hull;
            public HullMeshDef HullDef;
            public GameConfig Config;
        }

        private Rig Build()
        {
            var config = ScriptableObject.CreateInstance<GameConfig>(); _spawned.Add(config);
            config.MeshCharacter = false;
            GameServices.Config = config;
            GameServices.Clock = new FrozenClock();
            GameServices.Environment = new SeedEnv();

            var service = new FacetService();
            HullMeshPresentation.Service = service;

            var boatVisual = LoadCommitted<BoatVisualDef>(DoryVisualPath);
            Assert.AreEqual(BoatHullVariant.Mesh, boatVisual.Variant,
                "harness: this plate needs a FACET MESH hull. On a sprite hull there is no facet pass " +
                "to draw into, so the mesh figure could not appear however the switch was set and both " +
                "arms would quietly photograph the sprite.");
            Assert.IsNotNull(boatVisual.HullMesh, "harness: the mesh visual must carry a HullMeshDef");

            var root = new GameObject("Dory"); _spawned.Add(root);
            BoatHullSkinner.Apply(root, boatVisual, boat: null,
                                  new BoatHullSkinner.Options { SkipWaveMotion = true, SkipOars = true });

            var rig = new Rig
            {
                BoatRoot = root, Hull = service.Renderer,
                HullDef = boatVisual.HullMesh, Config = config,
            };
            Assert.IsNotNull(rig.Hull, "harness: the facet hull must have been installed");
            Assert.IsNotNull(rig.Hull.PosedMesh, "harness: the hull must be CONFIGURED");

            var playerGo = new GameObject("Player"); _spawned.Add(playerGo);
            SpriteRenderer body = playerGo.AddComponent<SpriteRenderer>();

            // ⭐ SHE SORTS ABOVE THE HULL — the relationship the per-pixel deck occlusion exists to
            // QUALIFY. Whole-object sorting puts the figure in front of the whole boat (that is the
            // defect the occluder was built for: "sprites visible THROUGH closed cabins"), and the
            // hull's facet ids then take back the pixels she is genuinely behind. BoatHullSkinner
            // sorts the hull on "Default" at the visual's own order, so the figure is one above it.
            //
            // ⚠ A fixture that leaves her at the SpriteRenderer default of order 0, under a hull at
            // order 1, photographs an EMPTY DECK on the sprite arm and reports the difference as the
            // toggle's work. That is exactly what this plate's first shot did, and it is why the
            // discriminator below is not on its own sufficient: a number that big can mean the
            // presenter drew a figure, or it can mean the control never drew one.
            body.sortingLayerID = SortingLayer.NameToID("Default");
            body.sortingOrder = boatVisual.SortingOrder + 1;
            IsoCharacterSprite character = playerGo.AddComponent<IsoCharacterSprite>();

            var fisher = LoadCommitted<CharacterVisualDef>(FisherVisualPath);
            Assert.IsNotNull(fisher.Skin,
                "FisherIso.asset carries no Skin. The bake landed but was never wired, so the mesh arm " +
                "would silently shoot the sprite and BOTH ARMS WOULD MATCH — which this fixture would " +
                "then report as a failure of the presenter rather than of the wiring. Wire the baked " +
                "CharacterSkinDef into the character def first.");
            character.Configure(fisher);

            var riderGo = new GameObject("DeckRider");
            riderGo.transform.SetParent(playerGo.transform, false);
            SpriteRenderer riderSr = riderGo.AddComponent<SpriteRenderer>();
            riderSr.enabled = false;

            rig.Rider = playerGo.AddComponent<DeckRiderVisual>();
            rig.Rider.Configure(riderSr, body, character);
            return rig;
        }

        private void SetUpCamera(HullMeshDef def)
        {
            _rt = new RenderTexture(def.CellW * Supersample, def.CellH * Supersample, 24,
                                    RenderTextureFormat.ARGB32)
            { filterMode = FilterMode.Point };
            var camGo = new GameObject("PlateCam"); _spawned.Add(camGo);
            _cam = camGo.AddComponent<Camera>();
            _cam.orthographic = true;
            // The CELL's framing, sampled finer. ⚠ Size the plate to the camera, never the camera to
            // the plate — the other way round lands the subject as an inset with daylight round it.
            _cam.orthographicSize = def.CellH / (2f * def.PxPerMetre);
            _cam.nearClipPlane = 1f;
            _cam.farClipPlane = 400f;
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = new Color(0.10f, 0.12f, 0.14f, 1f);
            _cam.allowHDR = false;
            _cam.allowMSAA = false;
            _cam.targetTexture = _rt;

            float ppu = def.PxPerMetre;
            float ox = (def.PivotPx.x - def.CellW / 2f) / ppu;
            float oy = (def.CellH / 2f - def.PivotPx.y) / ppu;
            _cam.transform.position = new Vector3(-ox, -oy, -100f);
        }

        // ---- shooting ------------------------------------------------------------------------------

        /// <summary>Render NOW and read back. No frame passes inside a shot, so nothing in the scene
        /// advances between the render and the read.</summary>
        private byte[] Shoot()
        {
            _cam.Render();
            int w = _rt.width, h = _rt.height;
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = _rt;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            byte[] raw = tex.GetRawTextureData();
            var copy = new byte[raw.Length];
            System.Array.Copy(raw, copy, raw.Length);
            Object.DestroyImmediate(tex);
            return copy;
        }

        private static string Hash(byte[] bytes)
        {
            using (var md5 = System.Security.Cryptography.MD5.Create())
            {
                byte[] h = md5.ComputeHash(bytes);
                var sb = new StringBuilder(h.Length * 2);
                foreach (byte b in h) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }

        private static int PixelsDiffering(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return int.MaxValue;
            int n = 0;
            for (int i = 0; i + 3 < a.Length; i += 4)
                if (a[i] != b[i] || a[i + 1] != b[i + 1] || a[i + 2] != b[i + 2] || a[i + 3] != b[i + 3])
                    n++;
            return n;
        }

        /// <summary>
        /// Write the plate for a HUMAN to look at.
        ///
        /// <para>⚠ <b>The hash is of the raw readback; this is not.</b> In LINEAR colour space the
        /// buffer the camera hands back is linear, and encoding it straight to PNG saves a picture
        /// several stops too dark — readable enough to hash, useless to judge a look by, which is
        /// the one thing the judgement pair is for. So the bytes that are hashed stay the renderer's
        /// own output and the conversion happens here, on the way to the file, and only when the
        /// project is actually in linear space.</para>
        /// </summary>
        private void SavePlate(string name, byte[] rgba)
        {
            var tex = new Texture2D(_rt.width, _rt.height, TextureFormat.RGBA32, false);
            tex.LoadRawTextureData(rgba);
            tex.Apply();

            if (QualitySettings.activeColorSpace == ColorSpace.Linear)
            {
                Color[] px = tex.GetPixels();
                for (int i = 0; i < px.Length; i++)
                {
                    Color c = px[i];
                    c.r = Mathf.Clamp01(c.r); c.g = Mathf.Clamp01(c.g); c.b = Mathf.Clamp01(c.b);
                    c.a = 1f;
                    px[i] = c.gamma;
                }
                tex.SetPixels(px);
                tex.Apply();
            }
            string dir = Path.Combine(Application.temporaryCachePath, PlateDir);
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, name);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            Debug.Log($"[mesh-plate] wrote {path}");
        }

        /// <summary>Let a toggle reach the picture. <c>DeckRiderVisual</c> reads the config in its own
        /// update and poses the figure in <c>LateUpdate</c>, so a flip is not in the frame until a frame
        /// has run — which is exactly why the floor control below is not optional.</summary>
        private static IEnumerator Settle()
        {
            yield return null;
            yield return null;
        }

        // ---- the test ------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator TheToggleMovesThePictureAndLeavesNoResidueOnTheSpritePath()
        {
            // ⚠ FIRST statement, before any yield. An Assert.Ignore raised after a yield unwinds through
            // the coroutine runner and records as FAILED, not as skipped.
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
                Assert.Ignore("SKIPPED, NOT VERIFIED — no graphics device. These are pictures and they " +
                              "need the local GPU; CI runs headless and cannot shoot them. The wiring " +
                              "these plates illustrate is asserted ungated in " +
                              "DeckRiderMeshPresenterPlayTests, which reads vertices, not pixels.");

            Rig rig = Build();
            SetUpCamera(rig.HullDef);

            rig.Rider.SetMode(ControlMode.Aboard, rig.BoatRoot.transform);
            yield return Settle();

            // ⚠ IN FRAME IS NOT IN THE PICTURE. The facet id pool is 255 at 1+12 per hull and the
            // registry's counter never rewinds, so a hull that ran out holds id 0 and is composited
            // away — present in the scene, absent from every pixel. Assert the id BEFORE trusting a
            // single plate, or the mesh arm photographs a hole and reads as "the mesh did not draw".
            Assert.Greater(rig.Hull.HullId, 0,
                "the hull under her holds facet id 0 — the id pool is exhausted in this domain, so she " +
                "is in frame and NOT in the picture. Every plate from this run would be a lie.");
            Debug.Log($"[mesh-plate] drawing renderer instance {rig.Hull.GetEntityId()} on " +
                      $"'{rig.Hull.gameObject.name}', HullId={rig.Hull.HullId}, " +
                      $"hull def '{rig.HullDef.Id}', plate {_rt.width}x{_rt.height}");

            // ---- A1: toggle 0, cold.
            Assert.IsFalse(rig.Config.MeshCharacter, "harness: A1 must be the OFF arm");
            byte[] a1 = Shoot();
            Assert.IsNull(rig.Rider.GetComponent<DeckRiderMeshPresenter>(),
                "with the switch off no presenter may EXIST — the off state must not merely decline to " +
                "draw, it must never allocate");

            // ---- A1-prime: toggle 0 again, nothing changed. THE FLOOR.
            yield return Settle();
            byte[] a1b = Shoot();
            int floor = PixelsDiffering(a1, a1b);
            Debug.Log($"[mesh-plate] FLOOR (two unchanged frames): {floor} px differ");
            Assert.Zero(floor,
                $"this instrument has a noise floor of {floor} px: two frames of an UNCHANGED scene do " +
                "not match, so no byte-identity claim can be drawn from it and the residue result below " +
                "would be meaningless. Something in this scene is advancing on engine time.");

            // ---- B: toggle 1. The mesh takes the draw.
            rig.Config.MeshCharacter = true;
            yield return Settle();
            DeckRiderMeshPresenter presenter = rig.Rider.GetComponent<DeckRiderMeshPresenter>();
            Assert.IsNotNull(presenter,
                "the switch is on and she is aboard a facet hull — a presenter must have been added");
            Assert.IsTrue(presenter.DrawsInsteadOfSprite,
                $"the mesh is not drawing, so arm B would photograph the sprite and the pair would be a " +
                $"comparison of nothing. The presenter says: {presenter.NotDrawingReason}");
            Assert.IsTrue(rig.Rider.SpriteSuppressed, "the sprite must stand down where the mesh draws");
            Debug.Log($"[mesh-plate] mesh arm: state '{presenter.DrawnStateKey}' frame " +
                      $"{presenter.DrawnFrame} (requested {presenter.RequestedFrame}), " +
                      $"fellBackToGait={presenter.FellBackToGait}");
            byte[] b = Shoot();

            // ---- A2: toggle 0 again, WARM. The presenter exists and is standing down.
            rig.Config.MeshCharacter = false;
            yield return Settle();
            Assert.IsFalse(presenter.DrawsInsteadOfSprite,
                "the switch went off and the presenter kept drawing — the config is not re-read");
            Assert.IsFalse(rig.Rider.SpriteSuppressed, "the sprite must come back when the mesh stops");
            byte[] a2 = Shoot();

            string hA1 = Hash(a1), hA1b = Hash(a1b), hB = Hash(b), hA2 = Hash(a2);
            Debug.Log($"[mesh-plate] A1  (toggle 0, cold) md5={hA1}\n" +
                      $"[mesh-plate] A1' (floor control)  md5={hA1b}\n" +
                      $"[mesh-plate] B   (toggle 1, mesh) md5={hB}\n" +
                      $"[mesh-plate] A2  (toggle 0, warm) md5={hA2}");

            SavePlate("A1-toggle0-sprite.png", a1);
            SavePlate("B-toggle1-mesh.png", b);
            SavePlate("A2-toggle0-warm.png", a2);

            // ⚠ THE DISCRIMINATOR, asserted before the identity. Four identical hashes across a swept
            // property mean the property never reached the drawing renderer — a presenter that did
            // nothing at all would otherwise sail through the residue check below as a clean pass.
            Debug.Log($"[mesh-plate] the toggle moved {PixelsDiffering(a1, b)} px");
            Assert.AreNotEqual(hA1, hB,
                "toggle 0 and toggle 1 produced the SAME picture. The mesh never reached the drawing " +
                "renderer, and every other result here is worthless.");

            // ---- the residue result. A2 is the warm case and strictly harder than A1's cold one.
            Assert.AreEqual(hA1, hA2,
                $"the sprite path did not come back byte-identical after the mesh path ran " +
                $"({PixelsDiffering(a1, a2)} px differ). The mesh path leaves residue on the off state.");

            // ---- ⭐ THE CONTROL THE DISCRIMINATOR NEEDS, shot last so it perturbs nothing above.
            //
            // "The toggle moved N px" is true of a presenter that draws a figure onto a deck that
            // already had one, and equally true of a control that photographed an EMPTY deck. The
            // first shot of this plate was the second case — the figure sorted under the hull quad
            // and the sprite arm was bare — and every number in it looked healthy. So the sprite
            // arm is required to differ from no-player-at-all before its comparison with the mesh
            // arm is allowed to mean anything.
            rig.Rider.gameObject.SetActive(false);
            yield return Settle();
            byte[] empty = Shoot();
            string hEmpty = Hash(empty);
            Debug.Log($"[mesh-plate] EMPTY (no player at all) md5={hEmpty}, " +
                      $"{PixelsDiffering(a1, empty)} px of her in the sprite arm");
            SavePlate("C-empty-deck.png", empty);

            Assert.AreNotEqual(hA1, hEmpty,
                "the SPRITE arm is byte-identical to a deck with no player on it — the sprite figure " +
                "never drew, so \"sprite vs mesh\" was really \"nothing vs mesh\" and the judgement " +
                "pair would be a lie. Check her sorting against the hull's before reading anything " +
                "else here.");
        }
    }
}
