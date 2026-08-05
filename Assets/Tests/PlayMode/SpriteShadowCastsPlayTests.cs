using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using HiddenHarbours.Art;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// <b>The sun tells the time on the ground.</b> The owner's 2026-08-05 ruling put shadows under the
    /// trees and under the player, and what has to be true for that to mean anything is that the shadow
    /// CHANGES: long west at dawn, a stub at noon, long east at dusk, gone at night — and, for the fisher,
    /// stepping with the frame the fisher is actually drawn on.
    ///
    /// <para><b>🔴 Why the differentials run through BOTH real objects.</b> The failure this suite exists to
    /// catch is the plumbed-but-unread one (#421): a value that is computed, published and never consumed
    /// looks identical from either end. So nothing here writes a global by hand. The real
    /// <see cref="DayNightController"/> publishes <c>_SunDir</c> / <c>_SunElevation</c> /
    /// <c>_ShadowStrength</c> from a fixed clock, and the real <see cref="SpriteShadow"/> is then read back
    /// through the property block it pushes at its own renderer — the same block the shader samples. Both
    /// ticks are driven SYNCHRONOUSLY (the pattern <c>ShadowStrengthGlobalPlayTests</c> established) so no
    /// auto-installed singleton's own Update can land between the publish and the read.</para>
    ///
    /// <para><b>And every differential carries a negative control that is OBSERVED, not asserted.</b> The
    /// component has a documented fallback — when no cycle is running it evaluates its own arc at a fixed
    /// hour — which would make a dawn/noon/dusk sweep produce three plausible, entirely fake, identical
    /// answers. <see cref="TheSweepIsFlat_WhenNothingIsPublishing_WhichIsWhyTheSweepAboveIsEvidence"/> runs
    /// exactly that and watches it come out flat. If the live read ever regresses to the fallback, the
    /// differential goes flat too and the pair stops agreeing.</para>
    /// </summary>
    public class SpriteShadowCastsPlayTests
    {
        private static readonly int IdShadowColor = Shader.PropertyToID("_ShadowColor");
        private static readonly int IdShadowDir   = Shader.PropertyToID("_ShadowDir");
        private static readonly int IdShadowLen   = Shader.PropertyToID("_ShadowLen");
        private static readonly int IdShadowUV    = Shader.PropertyToID("_ShadowUV");

        private static readonly int IdSunDir         = Shader.PropertyToID("_SunDir");
        private static readonly int IdSunElevation   = Shader.PropertyToID("_SunElevation");
        private static readonly int IdShadowStrength = Shader.PropertyToID("_ShadowStrength");

        /// <summary>Roughly the hours the owner reads: the sun just up, high, and just going.</summary>
        private const float Dawn = 6.5f, Noon = 13f, Dusk = 19.5f, Night = 2f;

        private readonly List<Object> _spawned = new();

        // ---- minimal Core-contract fakes (only the bits the controller reads) ---------------------

        private sealed class FixedClock : IGameClock
        {
            private readonly float _hour;
            public FixedClock(float hour) { _hour = hour; }
            public double TotalSeconds => _hour * 3600.0;
            public GameTime Now => default;
            public Season Season => Season.HighSummer;
            public int Year => 1;
            public int DayIndex => 0;
            public int DayOfSeason => 1;
            public Weekday Weekday => default;
            public bool IsMarketDay => false;
            public float HourOfDay => _hour;
            public float DayFraction => Mathf.Repeat(_hour, 24f) / 24f;
            public bool IsPaused { get; set; }
            public float TimeScale { get; set; } = 1f;
        }

        private sealed class ClearEnvironment : IEnvironmentService
        {
            public int WorldSeed => 0;
            public TideProfile ActiveTideProfile { get; set; }
            public EnvironmentSample Sample()
                => new EnvironmentSample(Vector2.zero, Vector2.zero, 0f, SeaState.Glass, 1f);
            public float TideHeightAt(double totalSeconds) => 0f;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _spawned)
                if (o != null) Object.Destroy(o);
            _spawned.Clear();
            GameServices.Reset();

            // These are PROCESS-WIDE. Leaving a dusk sun published would quietly re-light every test that
            // runs after this file.
            Shader.SetGlobalVector(IdSunDir, Vector4.zero);
            Shader.SetGlobalFloat(IdSunElevation, 0f);
            Shader.SetGlobalFloat(IdShadowStrength, 0f);
        }

        // =====================================================================================
        //  helpers — the real objects, driven synchronously
        // =====================================================================================

        /// <summary>A caster the size and shape of a piece of decor: a plain sprite, one SpriteShadow.</summary>
        private SpriteShadow NewCaster(string name, Sprite sprite = null)
        {
            var go = new GameObject(name);
            _spawned.Add(go);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite != null ? sprite : NewSprite(16, 48);
            return go.AddComponent<SpriteShadow>();
        }

        /// <summary>A throwaway sprite. Distinct textures so two of them can never compare equal.</summary>
        private Sprite NewSprite(int w, int h)
        {
            var tex = new Texture2D(w, h);
            _spawned.Add(tex);
            var sprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0f), 32f);
            _spawned.Add(sprite);
            return sprite;
        }

        /// <summary>
        /// One cell out of a MULTI-ROW SHEET, with its pivot wherever we ask — i.e. the shape every caster in
        /// production actually is. The shipped families are all <c>spriteMode 2</c> slices: one row of tree
        /// variants, four sway rows of shrubs and shore plants, eight facing rows of the fisher, each with the
        /// feet some rows up inside the cell (ADR 0026). <c>NewSprite</c> above is the opposite — one sprite
        /// filling its texture, pivoted at the cell bottom — which is exactly the case the shear always had
        /// right, and why it took a fixture like this one to see the anchor was wrong.
        /// </summary>
        private Sprite NewSlicedSprite(int cellW, int cellH, int rows, int row, float pivotNormY)
        {
            var tex = new Texture2D(cellW, cellH * rows);
            _spawned.Add(tex);
            var sprite = Sprite.Create(tex, new Rect(0, row * cellH, cellW, cellH),
                                       new Vector2(0.5f, pivotNormY), 32f);
            _spawned.Add(sprite);
            return sprite;
        }

        /// <summary>Publish the sun for an hour through the REAL controller, then let the REAL caster
        /// consume it — both synchronously, in that order, so the read cannot race an Update.</summary>
        private void PublishAndConsume(float hour, SpriteShadow caster)
        {
            GameServices.Clock = new FixedClock(hour);
            GameServices.Environment = new ClearEnvironment();

            var go = new GameObject($"DayNightController (h{hour})");
            _spawned.Add(go);
            var controller = go.AddComponent<DayNightController>();
            Invoke(controller, "Tick");
            Invoke(caster, "Tick");
        }

        private static void Invoke(Object target, string method)
        {
            MethodInfo m = target.GetType()
                .GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(m, $"{target.GetType().Name}.{method}() not found (private API moved?)");
            m.Invoke(target, null);
        }

        /// <summary>The pooled child SpriteShadow creates in Awake — the thing that actually draws.</summary>
        private static SpriteRenderer ShadowOf(SpriteShadow caster)
        {
            Transform child = caster.transform.Find("SpriteShadow");
            Assert.IsNotNull(child,
                "SpriteShadow created no child renderer, so nothing is drawn however good the maths is.");
            var sr = child.GetComponent<SpriteRenderer>();
            Assert.IsNotNull(sr, "The pooled shadow child carries no SpriteRenderer.");
            return sr;
        }

        /// <summary>What the caster last pushed at the shader: direction, shear length, alpha.</summary>
        private static (Vector2 dir, float len, float alpha) Projection(SpriteShadow caster)
        {
            var sr = ShadowOf(caster);
            var mpb = new MaterialPropertyBlock();
            sr.GetPropertyBlock(mpb);
            Vector4 dir = mpb.GetVector(IdShadowDir);
            return (new Vector2(dir.x, dir.y), mpb.GetFloat(IdShadowLen), mpb.GetColor(IdShadowColor).a);
        }

        /// <summary>
        /// The PIVOT MAP the caster published for the vertex stage: <c>upFrac = uv.y * x + y</c>, the height
        /// above the caster's feet in caster heights. Read off the same property block the shader samples, so
        /// an unpublished map reads back as it would on the GPU — <c>(0,0)</c>, not a helpful default.
        /// </summary>
        private static Vector2 ShadowUvOf(SpriteShadow caster)
        {
            var mpb = new MaterialPropertyBlock();
            ShadowOf(caster).GetPropertyBlock(mpb);
            Vector4 uv = mpb.GetVector(IdShadowUV);
            return new Vector2(uv.x, uv.y);
        }

        // =====================================================================================
        //  the sun swings, and the shadow with it
        // =====================================================================================

        /// <summary>
        /// The whole intent in one test: a caster standing still under a moving sun throws a DIFFERENT
        /// shadow at each of the four hours the owner reads.
        ///
        /// <list type="bullet">
        /// <item><b>Side.</b> Dawn sun in the east → shadow runs WEST (−x); dusk sun in the west → EAST
        /// (+x). Opposite signs, which no fallback and no frozen value can fake.</item>
        /// <item><b>Length.</b> A low sun rakes and a high sun stubs, so both ends of the day are strictly
        /// longer than noon.</item>
        /// <item><b>Night.</b> Alpha falls to nothing and the renderer stands itself down — a shadow with
        /// no sun is the bug this is watching for.</item>
        /// </list>
        /// </summary>
        [UnityTest]
        public IEnumerator TheShadowSwingsAndLengthens_AcrossDawnNoonDuskAndFadesAtNight()
        {
            var caster = NewCaster("decor-caster");
            yield return null;   // let Awake create the pooled child

            PublishAndConsume(Dawn, caster);
            var dawn = Projection(caster);
            PublishAndConsume(Noon, caster);
            var noon = Projection(caster);
            PublishAndConsume(Dusk, caster);
            var dusk = Projection(caster);

            Debug.Log($"[SpriteShadowCasts] dawn dir {dawn.dir} len {dawn.len:F2} a {dawn.alpha:F3} · " +
                      $"noon dir {noon.dir} len {noon.len:F2} a {noon.alpha:F3} · " +
                      $"dusk dir {dusk.dir} len {dusk.len:F2} a {dusk.alpha:F3}");

            Assert.Less(dawn.dir.x, 0f,
                $"At dawn the sun is in the EAST, so the shadow must run west (−x). Got {dawn.dir}.");
            Assert.Greater(dusk.dir.x, 0f,
                $"At dusk the sun is in the WEST, so the shadow must run east (+x). Got {dusk.dir}.");

            Assert.Greater(dawn.len, noon.len,
                $"A dawn shadow ({dawn.len:F2}) must rake further than a noon stub ({noon.len:F2}).");
            Assert.Greater(dusk.len, noon.len,
                $"A dusk shadow ({dusk.len:F2}) must rake further than a noon stub ({noon.len:F2}).");
            Assert.Greater(noon.len, 0f, "Even at noon there is a stub under the caster.");

            Assert.Greater(noon.alpha, dawn.alpha,
                $"A high noon sun casts a FIRMER shadow ({noon.alpha:F3}) than a sun on the horizon " +
                $"({dawn.alpha:F3}) — the strength folds the sun's own height.");

            // Night: the sun is down, and the caster must stand its renderer down with it.
            PublishAndConsume(Night, caster);
            var night = Projection(caster);
            Assert.AreEqual(0f, night.alpha, 1e-4f,
                $"A shadow at {Night}:00 with alpha {night.alpha:F3}. There is no sun to cast it.");
            Assert.IsFalse(ShadowOf(caster).enabled,
                "The night shadow is invisible but still being drawn — a transparent quad per caster is " +
                "the cost this component pools its renderer to avoid.");
        }

        /// <summary>
        /// 🔴 <b>THE NEGATIVE CONTROL for the sweep above, and it is meant to be read next to it.</b>
        ///
        /// <para>The failure mode being controlled for is precisely #421's: a caster that had quietly
        /// stopped consuming the published sun would still draw a perfectly plausible shadow, because
        /// <see cref="SpriteShadow"/> has a documented fallback that evaluates the arc itself at a fixed
        /// hour. Three plausible, identical, entirely fake answers.</para>
        ///
        /// <para>So this sweeps the SAME three hours while moving only the clock — and the caster holds no
        /// clock reference at all; turning an hour into a sun is a controller's whole job — then publishes
        /// ONE hour properly and watches the answer move. Flat while only the clock moves, different the
        /// instant a controller ticks: the published globals are the input, and nothing else is.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator OnlyAPublishedSunMovesTheShadow_WhichIsWhyTheSweepAboveIsEvidence()
        {
            var caster = NewCaster("decor-caster-unpublished");
            yield return null;

            // Move the CLOCK across the whole day without ticking a controller. Every read below happens
            // in one synchronous block, so nothing else can publish between them either.
            var seen = new List<(Vector2 dir, float len, float alpha)>();
            foreach (float hour in new[] { Dawn, Noon, Dusk })
            {
                GameServices.Clock = new FixedClock(hour);
                Invoke(caster, "Tick");
                seen.Add(Projection(caster));
            }

            Assert.AreEqual(seen[0].len, seen[1].len, 1e-5f,
                $"Dawn read {seen[0].len:F3} and noon {seen[1].len:F3} with NO controller ticking. The " +
                "caster has grown a second, unaudited source of time — and the sweep beside this one has " +
                "stopped being evidence that it reads the published sun.");
            Assert.AreEqual(seen[1].len, seen[2].len, 1e-5f, "noon vs dusk, with only the clock moving");
            Assert.AreEqual(seen[0].dir, seen[2].dir,
                $"The shadow swung from {seen[0].dir} to {seen[2].dir} with only the clock moving.");

            // …and now publish one of those same hours FOR REAL. The answer must move.
            PublishAndConsume(Dusk, caster);
            var published = Projection(caster);
            Assert.Greater(Mathf.Abs(published.len - seen[2].len), 1e-3f,
                $"Dusk read {published.len:F3} published and {seen[2].len:F3} unpublished — the same " +
                "answer either way, which means the caster is not consuming what the controller pushes.");
        }

        // =====================================================================================
        //  the shadow stands at its caster's feet
        // =====================================================================================

        /// <summary>
        /// 🔴 <b>The shadow's ROOT is its caster's PIVOT — at a raking sun, at both ends of the day.</b> This
        /// is the geometric claim the whole feature rests on: a shadow is pinned where its caster meets the
        /// ground, and across every rig family that point is the sprite's pivot, not the bottom of the cell it
        /// was sliced into and not the bottom of the SHEET (ADR 0026).
        ///
        /// <para>The fixture is the case the shipped casters are and the old shadow-suite fixtures are not: a
        /// cell on a multi-row sheet with the feet a quarter of the way up it. It is measured at DAWN and at
        /// DUSK because the two rake opposite ways — a root that is riding along the shear rather than pinned
        /// at the feet lands on the wrong side of the caster when the sun crosses over, so the two together
        /// cannot be satisfied by a constant fudge.</para>
        ///
        /// <para>🔴 <b>The non-degeneracy assert is what carries this on the pre-fix path.</b> Before the fix
        /// the component published no pivot map at all, so it reads back as <c>(0,0,0,0)</c> — which sends
        /// every vertex to zero shear and would satisfy "the root is at the feet" with a flat unsheared blob.
        /// So each hour also asserts that one caster-height up still lands one full shear length down the
        /// ground. Run against the pre-fix component this test fails on that assert at dawn.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator TheShadowRootsAtTheCastersPivot_AtBothEndsOfTheDay()
        {
            // 32×96 cell, top row of four (a 384 px sheet), feet a quarter of the way up with empty headroom.
            Sprite sprite = NewSlicedSprite(32, 96, rows: 4, row: 3, pivotNormY: 0.25f);
            var caster = NewCaster("off-centre-pivot-caster", sprite);
            caster.transform.position = Vector3.zero;   // on the pixel grid, so the snap cannot blur the read
            yield return null;

            foreach (float hour in new[] { Dawn, Dusk })
            {
                // Publish, consume and POSE in one synchronous block — the pattern this file is built on. A
                // frame boundary here would let the previous hour's controller (still alive until TearDown)
                // run its own Update and re-publish its sun over the one under test.
                PublishAndConsume(hour, caster);
                Invoke(caster, "PoseShadow");

                var (dir, len, _) = Projection(caster);
                Vector2 map = ShadowUvOf(caster);

                // Where the caster's PIVOT sits in texture uv — derived here from the sprite itself, not from
                // the component, so this is a cross-check and not a restatement.
                float pivotUv = (sprite.rect.y + sprite.pivot.y) / sprite.texture.height;
                float atPivot = pivotUv * map.x + map.y;          // the vertex stage's one line
                float oneUp = (sprite.rect.y + sprite.pivot.y + sprite.bounds.size.y * sprite.pixelsPerUnit)
                              / sprite.texture.height;

                Vector3 shadowAt = ShadowOf(caster).transform.position;
                Vector3 root = shadowAt + (Vector3)(dir * (len * atPivot));

                Debug.Log($"[SpriteShadowCasts] h{hour} pivot uv.y {pivotUv:F4} map {map} len {len:F2} " +
                          $"dir {dir} · root {root} vs feet {caster.transform.position}");

                Assert.Greater(len, 0f, $"sanity: {hour}:00 is meant to be a raking sun, not a sun that is down");

                // Half a pixel at PPU 32 — the same "underfoot" tolerance the walk test uses.
                Assert.AreEqual(caster.transform.position.x, root.x, 1f / 64f,
                    $"At {hour}:00 the shadow's root is at {root} and the caster stands at " +
                    $"{caster.transform.position} — {Mathf.Abs(atPivot) * len:F2} m of daylight between a " +
                    "caster and its own shadow, at exactly the raking sun that makes shadows worth having.");
                Assert.AreEqual(caster.transform.position.y, root.y, 1f / 64f,
                    $"At {hour}:00 the shadow's root is at {root} and the caster stands at " +
                    $"{caster.transform.position}.");

                // …and it is still a SHADOW while it is anchored: one caster-height up rakes one full length.
                Assert.AreEqual(1f, oneUp * map.x + map.y, 1e-3f,
                    $"At {hour}:00 the silhouette is anchored but no longer shears by its caster's height — " +
                    "a shadow pinned correctly to the feet and drawn as a flat blob is not a shadow. (This " +
                    "is the assert that fails on the pre-fix component, which published no pivot map.)");
            }
        }

        /// <summary>
        /// 🔴 <b>THE NEGATIVE CONTROL for the test above.</b> A caster whose pivot IS its cell-bottom-centre
        /// and whose sprite fills its whole texture is the one shape the old shear had right, so the anchor fix
        /// must leave it EXACTLY where it was: the identity map, <c>upFrac == uv.y</c>.
        ///
        /// <para>That shape is not hypothetical — it is what every other fixture in this file is, which is why
        /// none of their numbers move. If this ever reads anything but the identity, the suites around it have
        /// stopped being a before/after comparison.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator ACasterPivotedAtItsCellBottom_IsLeftExactlyWhereItWas()
        {
            var caster = NewCaster("coincident-pivot-caster");   // NewSprite: full-rect, pivot (0.5, 0)
            yield return null;

            PublishAndConsume(Dawn, caster);
            Vector2 map = ShadowUvOf(caster);

            Assert.AreEqual(SpriteShadow.IdentityShearMap.x, map.x, 0f,
                $"The negative control moved: {map}. This caster was already anchored correctly, so the pivot " +
                "fix must be a no-op on it — otherwise the fix changed the look rather than correcting it.");
            Assert.AreEqual(SpriteShadow.IdentityShearMap.y, map.y, 0f,
                $"The negative control moved: {map}.");
        }

        // =====================================================================================
        //  the player
        // =====================================================================================

        /// <summary>
        /// <b>The fisher's shadow steps with the fisher.</b> The player is the first ANIMATED caster in
        /// production — the walk skin, the iso skin, the haul cycle and the rod-fight poses all swap the
        /// sprite on one renderer — so the silhouette has to track the CURRENT frame, not the frame the
        /// last light tick happened to see.
        ///
        /// <para>🔴 <b>The negative control is built into the fixture</b>: the light recompute is throttled
        /// to one tick every five seconds before the caster ever wakes, so it demonstrably cannot have run
        /// between the sprite swap and the assert. Anything that tracks here tracked because of the
        /// per-frame sync, and nothing else. With the silhouette living in the throttled tick — where it
        /// lived until this pass — the second assert fails outright, which is how the lag was found.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator ThePlayersShadowFollowsTheFrameTheFisherIsDrawnOn()
        {
            Sprite standing = NewSprite(16, 48);
            Sprite midStride = NewSprite(24, 48);
            Assert.AreNotSame(standing, midStride, "sanity: the two poses must be distinguishable");

            // Built INACTIVE so the throttle can be set before Awake runs — otherwise the first Update
            // would already have re-armed the timer at the default 10 Hz.
            var go = new GameObject(PlayerShadowInstaller.DefaultPlayerObjectName);
            _spawned.Add(go);
            go.SetActive(false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = standing;

            // The REAL attachment the host makes — not a re-statement of it.
            var caster = PlayerShadowInstaller.Attach(go);
            Assert.IsNotNull(caster, "The installer did not attach a caster to the player.");
            // One light tick per FIVE seconds. Slow enough that no plausible frame time — including a
            // cold PlayMode start's first long frames — can slip a Tick between the sprite swap and the
            // assert, which is the whole basis of the control.
            typeof(SpriteShadow).GetField("_refreshHz", BindingFlags.NonPublic | BindingFlags.Instance)
                                .SetValue(caster, 0.2f);
            go.SetActive(true);

            yield return null;
            Assert.AreSame(standing, ShadowOf(caster).sprite,
                "The shadow is not silhouetting the pose the fisher is standing in.");

            // Take one step. ONE frame later the shadow must have stepped too — and the 1 Hz light tick
            // cannot have run in that frame.
            sr.sprite = midStride;
            yield return null;

            Assert.AreSame(midStride, ShadowOf(caster).sprite,
                "The fisher changed frame and the shadow did not follow within a frame. The silhouette " +
                "sync has gone back into the throttled tick, so the shadow's legs are up to a whole walk " +
                "frame out of step with the body's.");
        }

        /// <summary>
        /// The shadow follows the player around, still anchored under them, and stands down the moment the
        /// renderer does — which is what makes the helm need no gate of its own (<c>ControlSwitcher</c>
        /// disables the player's renderer at the wheel, and this is what turns the shadow off with it).
        /// </summary>
        [UnityTest]
        public IEnumerator ThePlayersShadowStaysUnderfoot_AndStandsDownWithTheRenderer()
        {
            var go = new GameObject(PlayerShadowInstaller.DefaultPlayerObjectName);
            _spawned.Add(go);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = NewSprite(16, 48);
            var caster = PlayerShadowInstaller.Attach(go);
            yield return null;

            PublishAndConsume(Noon, caster);
            yield return null;   // LateUpdate poses the shadow

            var walkedTo = new Vector3(12f, -3f, 0f);
            go.transform.position = walkedTo;
            yield return null;

            Vector3 shadowAt = ShadowOf(caster).transform.position;
            // Pixel-snapped to the project's 32 PPU grid, so "underfoot" is within half a pixel.
            Assert.AreEqual(walkedTo.x, shadowAt.x, 1f / 64f,
                $"The fisher walked to {walkedTo} and the shadow stayed at {shadowAt}.");
            Assert.AreEqual(walkedTo.y, shadowAt.y, 1f / 64f,
                $"The fisher walked to {walkedTo} and the shadow stayed at {shadowAt}.");

            // Taking the helm hides the fisher's sprite; the shadow must go with it.
            sr.enabled = false;
            Invoke(caster, "Tick");
            Assert.IsFalse(ShadowOf(caster).enabled,
                "The fisher's renderer is off (at the helm) but the shadow is still on the ground — a " +
                "shadow with nobody standing in it.");

            sr.enabled = true;
            Invoke(caster, "Tick");
            Assert.IsTrue(ShadowOf(caster).enabled,
                "Coming back ashore did not bring the shadow back.");
        }

        // =====================================================================================
        //  the budget the casters must not spend
        // =====================================================================================

        /// <summary>
        /// 🔴 <b>The #393 Y-sort budget must survive this pass.</b> Static decor's whole perf promise is
        /// that a <see cref="YSortSprite"/> sorts once and then DISABLES itself, so the engine stops
        /// dispatching its callbacks across ~1300 instances. Every caster this pass adds brings a child
        /// renderer with it, and a child that carried a YSortSprite of its own would double that
        /// population — silently, because it would look perfectly correct.
        ///
        /// <para>It must not carry one for a second reason: the shadow's order is derived from its
        /// caster's every tick (<c>caster.sortingOrder − 1</c>, so it draws UNDER it). A YSortSprite on
        /// the child would recompute that same order from the child's own world Y and fight it.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator TheShadowChildCostsNoYSortDispatch_AndSortsUnderItsCaster()
        {
            var go = new GameObject("ysort-budget-caster");
            _spawned.Add(go);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = NewSprite(16, 48);
            var sort = go.AddComponent<YSortSprite>();
            var caster = go.AddComponent<SpriteShadow>();
            yield return null;

            PublishAndConsume(Noon, caster);
            var shadow = ShadowOf(caster);

            Assert.IsNull(shadow.GetComponent<YSortSprite>(),
                "The pooled shadow child carries a YSortSprite. That doubles the Y-sort population this " +
                "pass adds a caster to, and it fights the order SpriteShadow derives from the caster.");
            Assert.IsFalse(sort.enabled,
                "The CASTER's own static YSortSprite is still dispatching. Adding a shadow must not " +
                "re-arm the per-frame sort the #393 pass stood down.");

            Assert.AreEqual(sr.sortingLayerID, shadow.sortingLayerID,
                "The shadow drifted onto a different sorting layer from its caster.");
            Assert.Less(shadow.sortingOrder, sr.sortingOrder,
                $"The shadow sorts at {shadow.sortingOrder} against its caster's {sr.sortingOrder} — it " +
                "must draw UNDER the thing casting it, not over it.");
        }
    }
}
