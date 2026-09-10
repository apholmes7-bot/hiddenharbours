using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using HiddenHarbours.Art;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// <b>THE BIRD COMES DOWN ONTO HER SHADOW.</b> That sentence is the whole point of the rig intake,
    /// and it is the one thing no EditMode test can prove: the state machine says the pivot is pinned
    /// and the altitude runs to zero, but whether the PRESENTER turns those two numbers into a sprite
    /// descending onto a stationary shadow is a question about transforms in a running scene.
    ///
    /// <para><b>Altitude is a SCREEN OFFSET, never a scale</b> (owner, 2026-09-09: <i>"there arent giant
    /// gulls that fly across the camera"</i>). So this fixture measures two numbers on every tick of the
    /// arrival: the sprite's lift above its own pivot, which must equal <c>altitude x cos 40</c> and run
    /// to EXACTLY zero, and the shadow's world foot — <c>node.TransformPoint(0, -FootOffset, 0)</c>, the
    /// expression <see cref="SpriteShadow"/> itself uses to anchor the shade — which must not move at
    /// all while she settles. It also asserts her lossy scale is the SAME at height and on the ground,
    /// because a rig that cheated altitude with a scale would pass every other test in this file.</para>
    ///
    /// <para><b>The fixture is the clock.</b> <see cref="GullFlock.Update"/> is the only clock read in
    /// the system, so every flock under test here is DISABLED before its first frame and stepped with
    /// explicit milliseconds through <see cref="GullFlock.Tick(double)"/>. That is what makes
    /// "two runs on one seed agree" a test rather than a hope — and what lets the last test run one
    /// flock across real frames at <c>timeScale 0</c> and the other with no frames at all, and demand
    /// the same sim out of both.</para>
    ///
    /// <para><b>⚠ The wheel owns a flock-bound bird's anim.</b> <c>flock()</c> hands out fly/glide/swoop
    /// directly and the machine adopts them verbatim — <c>swoop -&gt; glide</c> is NOT a declared edge and
    /// the wheel takes it all the time. The edge table therefore governs from the moment a bird LEAVES
    /// the wheel, which is exactly where this fixture starts recording.</para>
    ///
    /// <para><b>Headless-safe by construction (⚠ do not relax).</b> Nothing renders, reads pixels or
    /// calls <c>Camera.Render</c>. CI runs with a null graphics device, where a ReadPixels PlayMode test
    /// does not fail — it KILLS the editor with no results XML at all. The camera here exists only
    /// because <c>AmbientGlobals.ResolveCamera()</c> returning null makes <c>Tick</c> hide the flock and
    /// return.</para>
    /// </summary>
    public class SeagullLandingPlayTests
    {
        readonly List<GameObject> _spawned = new();

        GullFlock _installed;
        bool _installedWasEnabled;
        Camera _camera;
        SeagullVisualDef _visual;
        SeagullBehaviour _table;

        Color _tintBefore;
        Vector4 _windBefore;
        float _timeScaleBefore;
        IGameClock _clockBefore;
        IEnvironmentService _environmentBefore;

        /// <summary>One 60 Hz frame, as milliseconds. Small enough that <c>land</c> (440 ms) is more
        /// than twenty samples of the descent, and far under <c>stand</c>'s 520 ms frame so the arrival
        /// is caught on frame 0.</summary>
        const double TickMilliseconds = 1000.0 / 60.0;

        /// <summary>Long enough on the wheel that a bird has real height to lose before she is
        /// commanded down — the descent is only worth measuring if there was one.</summary>
        const int WarmUpTicks = 90;

        /// <summary>A ceiling, not an expectation, and sized off the SLOWEST way down rather than the
        /// typical one. A bird commanded out of <c>swoop</c> falls at 3 m/s and is down in three
        /// seconds — but <c>glide -> land</c> is a declared edge too, so a bird already gliding stays
        /// in glide and sinks at 0.4 m/s. From the top of the flock's 3-14 m band that is thirty-four
        /// seconds, and a fixture that gave up at twenty would be red on the wheel's own dice.</summary>
        const int PatienceTicks = 3600;

        /// <summary>Well inside the working area (14 x 9 m about the camera) so the settled bird is not
        /// re-homed onto the wheel by <c>OutsideWorkingArea</c> the moment she is down.</summary>
        static readonly Vector2 GroundSpot = new Vector2(2.5f, -1.5f);
        static readonly Vector2 WaterSpot = new Vector2(-3.5f, 2f);

        /// <summary>An off-axis wind, in the rig's <c>atan2(vx, vy)</c> convention, so the into-wind
        /// run-in is a real two-leg path rather than a straight line down a world axis.</summary>
        const double WindHeading = 0.75;

        [SetUp]
        public void SetUp()
        {
            // ⚠ ONE AUDIO LISTENER: Unity logs "There are no audio listeners in the scene" every frame
            // of a listener-less play-mode scene, and the last test here advances real frames.
            Spawn("AudioListener").AddComponent<AudioListener>();

            var camGo = Spawn("TestCamera");
            camGo.tag = "MainCamera";
            _camera = camGo.AddComponent<Camera>();
            _camera.orthographic = true;
            _camera.orthographicSize = 8f;
            // Tick reads the camera's XY as the flock's centre, so the working area is about the origin.
            _camera.transform.position = new Vector3(0f, 0f, -10f);

            _tintBefore = Shader.GetGlobalColor("_DayNightTint");
            _windBefore = Shader.GetGlobalVector("_WindWorld");
            _timeScaleBefore = Time.timeScale;
            PinGlobals();

            // ⚠ THE SIM MUST NOT BE ABLE TO REACH A CLOCK. With no environment service the water level
            // is a constant and the sea state is 0, so what is under a bird and how a floating one rocks
            // are functions of position alone — which is what makes the last two tests here honest.
            _clockBefore = GameServices.Clock;
            _environmentBefore = GameServices.Environment;
            GameServices.Clock = null;
            GameServices.Environment = null;

            _visual = Resources.Load<SeagullVisualDef>(SeagullVisualDef.ResourcesPath);
            Assert.IsNotNull(_visual,
                "Resources/" + SeagullVisualDef.ResourcesPath + " did not load — without the baked " +
                "sheet the flock disables itself at Awake and every assertion below is vacuous");
            Assert.IsNotNull(_visual.Cell(0, SeagullStates.Land, 0),
                "the sheet has no land/frame-0 cell for facing 0. Draw early-returns on a null cell " +
                "WITHOUT writing the transform or the foot offset, so a missing sprite would make the " +
                "descent look stationary rather than fail");
            _table = _visual.Behaviour;
            Assert.IsNotNull(_table, "the def carries no state table");

            // ⚠ The host is hidden and DontDestroyOnLoad, so it is found among ALL loaded objects
            // rather than in the scene. Finding it IS the test that it self-installed.
            _installed = FindInstalledFlock();
            Assert.IsNotNull(_installed,
                "GullFlock did not install itself before the first scene — the gulls are ambient, " +
                "they are wired into no scene, and a host that fails to install is a silent absence");
            // It runs in EVERY PlayMode scene on the wall clock. Stand it down for the duration so the
            // only thing stepping a flock in this file is this fixture.
            _installedWasEnabled = _installed.enabled;
            _installed.enabled = false;
        }

        [TearDown]
        public void TearDown()
        {
            if (_installed != null) _installed.enabled = _installedWasEnabled;
            Time.timeScale = _timeScaleBefore;
            Shader.SetGlobalColor("_DayNightTint", _tintBefore);
            Shader.SetGlobalVector("_WindWorld", _windBefore);
            GameServices.Clock = _clockBefore;
            GameServices.Environment = _environmentBefore;

            foreach (GameObject go in _spawned) if (go != null) Object.Destroy(go);
            _spawned.Clear();
        }

        GameObject Spawn(string name)
        {
            var go = new GameObject(name);
            _spawned.Add(go);
            return go;
        }

        static GullFlock FindInstalledFlock()
        {
            foreach (GullFlock f in Resources.FindObjectsOfTypeAll<GullFlock>())
            {
                if (f == null) continue;
                GameObject go = f.gameObject;
                if (go.name == "GullFlock" &&
                    (go.hideFlags & HideFlags.HideAndDontSave) == HideFlags.HideAndDontSave)
                    return f;
            }
            return null;
        }

        /// <summary>
        /// A fresh flock of the shipped component with the shipped defaults, DISABLED before it can see
        /// a frame. The self-installed host is the one the game runs and the one this file asserts
        /// exists; these are the ones it MEASURES, because a host that has been wheeling gulls through
        /// every earlier test in the run does not start a descent from a known place.
        /// </summary>
        GullFlock NewFlock(string name)
        {
            GullFlock flock = Spawn(name).AddComponent<GullFlock>();
            flock.enabled = false;   // ⚠ before its first Update: this fixture hands it every millisecond
            Assert.Greater(flock.BirdCount, 0, name + " built no birds");
            Assert.IsNotNull(flock.Behaviour, name + " has no state table");
            return flock;
        }

        static void Step(GullFlock flock, int ticks)
        {
            for (int t = 0; t < ticks; t++) flock.Tick(TickMilliseconds);
        }

        /// <summary>
        /// Broad day, no wind — the two shader globals the flock reads, pinned.
        ///
        /// <para>⚠ Called again after every frame this fixture lets pass. The rest of the ambient world
        /// self-installs too, and <c>DayNightController</c> and the wind bridges rewrite these globals
        /// in their own <c>Update</c>. The flock reads BOTH into its sim — a dim tint puts the whole
        /// flock down to roost, and the wind offsets the wheel's centre — so a fixture that let them
        /// drift across its frames would be varying the weather while claiming to vary the clock, and
        /// could not tell the two apart.</para>
        /// </summary>
        static void PinGlobals()
        {
            Shader.SetGlobalColor("_DayNightTint", Color.white);
            Shader.SetGlobalVector("_WindWorld", Vector4.zero);
        }

        static string Id(GullFlock flock, int bird) =>
            flock.Behaviour.IdOf(flock.BirdState(bird).State);

        /// <summary>The sprite's offset above its OWN pivot — the screen lift Draw applies for
        /// altitude. Zero means the bird is standing on the spot her shadow is drawn at.</summary>
        static float Lift(GullFlock flock, int bird) =>
            flock.BirdTransform(bird).position.y - (float)flock.BirdState(bird).Y;

        /// <summary>Where the shadow is anchored in the world, by the expression SpriteShadow uses to
        /// place it: the caster's origin shifted DOWN by the foot offset.</summary>
        static Vector2 ShadowFoot(GullFlock flock, int bird)
        {
            Transform node = flock.BirdTransform(bird);
            Vector3 foot = node.TransformPoint(new Vector3(0f, -flock.BirdShadow(bird).FootOffset, 0f));
            return new Vector2(foot.x, foot.y);
        }

        /// <summary>
        /// One bird's SIM, copied out. Deliberately not the transform: the presenter's rock pose is a
        /// function of the SEA, which is the world's business, while determinism is a claim about the
        /// flock's own arithmetic. What must agree run to run is this.
        /// </summary>
        readonly struct Flight
        {
            public readonly int State, Frame, Dir, Intent;
            public readonly double X, Y, Altitude, Heading;
            public readonly bool Bound;

            public Flight(in SeagullSimBird b)
            {
                State = b.State; Frame = b.Frame; Dir = b.Dir; Intent = b.IntentState;
                X = b.X; Y = b.Y; Altitude = b.AltitudeMetres; Heading = b.Heading;
                Bound = b.FlockBound;
            }
        }

        static Flight[] Capture(GullFlock flock)
        {
            var shot = new Flight[flock.BirdCount];
            for (int i = 0; i < shot.Length; i++) shot[i] = new Flight(flock.BirdState(i));
            return shot;
        }

        /// <summary>Two flights are the same flight, bird for bird, to the bit. No tolerance: the
        /// arithmetic is the same arithmetic on the same inputs, and anything else is a bug.</summary>
        static void AssertSameFlight(Flight[] a, Flight[] b, string what)
        {
            Assert.AreEqual(a.Length, b.Length, what + ": different flock sizes");
            for (int i = 0; i < a.Length; i++)
            {
                Assert.AreEqual(a[i].State, b[i].State, what + ": bird " + i + " state");
                Assert.AreEqual(a[i].Frame, b[i].Frame, what + ": bird " + i + " frame");
                Assert.AreEqual(a[i].Dir, b[i].Dir, what + ": bird " + i + " facing");
                Assert.AreEqual(a[i].Intent, b[i].Intent, what + ": bird " + i + " intent");
                Assert.AreEqual(a[i].Bound, b[i].Bound, what + ": bird " + i + " flock-bound");
                Assert.AreEqual(a[i].X, b[i].X, what + ": bird " + i + " x");
                Assert.AreEqual(a[i].Y, b[i].Y, what + ": bird " + i + " y");
                Assert.AreEqual(a[i].Altitude, b[i].Altitude, what + ": bird " + i + " altitude");
                Assert.AreEqual(a[i].Heading, b[i].Heading, what + ": bird " + i + " heading");
            }
        }

        // ── the host ────────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// The gulls are ambient: no scene wires them, so the host installs itself before the first
        /// scene loads and every bird it builds carries its own shadow. The shadow rides the SAME
        /// transform as the sprite, which is what makes the foot offset cancel the altitude lift
        /// exactly rather than approximately, and it casts no ground-contact disc — that darkened patch
        /// belongs to a thing with mass overhead, not to a gull three metres up.
        /// </summary>
        [Test]
        public void TheFlockInstallsItselfAndGivesEveryBirdItsOwnShadow()
        {
            Assert.AreEqual(GullConfig.Default.Count, _installed.BirdCount,
                "the installed host did not build the shipped default flock");
            Assert.IsNotNull(_installed.Behaviour, "the installed host loaded no state table");

            for (int i = 0; i < _installed.BirdCount; i++)
            {
                SpriteShadow shadow = _installed.BirdShadow(i);
                Assert.IsNotNull(shadow, "bird " + i + " has no shadow — the pair IS the effect");
                Assert.AreSame(_installed.BirdTransform(i), shadow.transform,
                    "bird " + i + "'s shadow must ride the same transform as her sprite, or the foot " +
                    "offset cancels a lift that was applied somewhere else");
                Assert.IsFalse(shadow.CastsGroundContact,
                    "bird " + i + " casts a ground-contact disc; a flying gull is not standing on the " +
                    "patch it would darken");
            }
        }

        /// <summary>
        /// The first bird still ON THE WHEEL at or above <paramref name="minAltitude"/>.
        /// Chosen rather than assumed: the rig's settle window is twenty seconds and every bird draws
        /// her own place in it, so whether a given bird has already taken a decision of her own by the
        /// end of a warm-up is a fact to read, not to hard-code. Flock-bound also pins WHAT she is
        /// doing — the wheel only ever assigns fly, glide or swoop, and her intent is clear — so a
        /// descent commanded from here starts somewhere the table can be held to.
        /// </summary>
        static int BirdAloft(GullFlock flock, double minAltitude, int startAt = 0)
        {
            for (int i = startAt; i < flock.BirdCount; i++)
            {
                SeagullSimBird b = flock.BirdState(i);
                if (b.FlockBound && b.IntentState < 0 &&
                    flock.Behaviour.SurfaceOf(b.State) == SeagullSurface.None &&
                    b.AltitudeMetres >= minAltitude) return i;
            }
            return -1;
        }

        // ── the descent ─────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// <b>THE ACCEPTANCE TEST.</b> A bird commanded down comes to rest on the spot her shadow was
        /// already drawn on: the shadow's world foot does not move by so much as a ten-thousandth of a
        /// metre through the whole of <c>land</c>, while the sprite's lift above that foot falls from
        /// exactly <c>0.6 m x cos 40</c> — the entry altitude the sidecar gives <c>land</c>, turned into
        /// a screen offset — to exactly zero on frame 0 of <c>stand</c>.
        ///
        /// <para>And her scale never changes. The same lossy scale at fourteen metres and on the
        /// shingle is the owner's ruling as an assertion: altitude is an offset up the screen, so a
        /// gull is 1.4 m across at every height rather than a giant one near the camera.</para>
        /// </summary>
        [Test]
        public void ABirdCommandedDownDescendsOntoAStationaryShadow()
        {
            GullFlock flock = NewFlock("DescendingFlock");
            Step(flock, WarmUpTicks);

            int bird = BirdAloft(flock, 2.0);
            Assert.GreaterOrEqual(bird, 0,
                "no bird was 2 m up after the warm-up; a descent with no height to lose measures nothing");
            Vector3 scaleAloft = flock.BirdTransform(bird).lossyScale;

            int land = _table.IndexOf(SeagullStates.Land);
            Assert.IsTrue(flock.CommandLanding(bird, GroundSpot, land, WindHeading),
                "the table refused to bring bird " + bird + " down from " + Id(flock, bird));

            var lifts = new List<float>();
            var feet = new List<Vector2>();
            bool down = false;
            int frameOnArrival = -1;
            for (int t = 0; t < PatienceTicks && !down; t++)
            {
                flock.Tick(TickMilliseconds);
                string id = Id(flock, bird);
                if (id != SeagullStates.Land && id != SeagullStates.Stand) continue;
                lifts.Add(Lift(flock, bird));
                feet.Add(ShadowFoot(flock, bird));
                if (id == SeagullStates.Stand) { down = true; frameOnArrival = flock.BirdState(bird).Frame; }
            }

            Assert.IsTrue(down,
                "bird " + bird + " never reached stand in " + PatienceTicks + " ticks; she is in " +
                Id(flock, bird) + " at " + flock.BirdState(bird).AltitudeMetres + " m");
            Assert.GreaterOrEqual(lifts.Count, 8,
                "the arrival lasted " + lifts.Count + " ticks — that is not a descent, it is a snap");

            // THE LIFT: altitude x cos 40, and nothing else. Never a scale, never a fudge.
            Assert.AreEqual(_table.EntryAltitude(land) * IsoGround.HeightScale, lifts[0], 1e-4,
                "the first frame of land must sit exactly entry-altitude x cos 40 above the pivot");
            for (int i = 1; i < lifts.Count; i++)
                Assert.LessOrEqual(lifts[i], lifts[i - 1] + 1e-5f,
                    "the bird gained height at tick " + i + " of her own landing (" +
                    lifts[i - 1] + " -> " + lifts[i] + ")");
            Assert.Greater(lifts[0] - lifts[lifts.Count - 1], 0.4f,
                "she barely moved; the whole descent was under half a world unit");
            Assert.AreEqual(0f, lifts[lifts.Count - 1], 1e-6f,
                "she came to rest ABOVE her shadow — the two must meet, not nearly meet");
            Assert.AreEqual(0, frameOnArrival,
                "the offset reached zero somewhere other than frame 0 of stand");
            Assert.AreEqual(0.0, flock.BirdState(bird).AltitudeMetres, 0.0,
                "a standing bird's altitude must be exactly zero, not about zero");

            // THE SHADOW: already on the spot, and it never moved.
            Assert.Less(Vector2.Distance(feet[0], GroundSpot), 1e-4f,
                "the shadow was not anchored on the commanded spot when land began");
            for (int i = 1; i < feet.Count; i++)
                Assert.Less(Vector2.Distance(feet[i], feet[0]), 1e-4f,
                    "the shadow slid " + Vector2.Distance(feet[i], feet[0]) + " m at tick " + i +
                    " of the arrival; a shadow that moves while she settles reads as a miss");

            // THE SCALE: the same bird at height and on the ground (owner, 2026-09-09).
            Vector3 scaleDown = flock.BirdTransform(bird).lossyScale;
            Assert.AreEqual(1f, scaleAloft.x, 1e-6f, "the airborne bird is not drawn at unit scale");
            Assert.AreEqual(1f, scaleAloft.y, 1e-6f, "the airborne bird is not drawn at unit scale");
            Assert.AreEqual(scaleAloft.x, scaleDown.x, 1e-6f,
                "she changed width between the sky and the ground — altitude is being drawn as a scale");
            Assert.AreEqual(scaleAloft.y, scaleDown.y, 1e-6f,
                "she changed height between the sky and the ground — altitude is being drawn as a scale");

            // And she STAYS there: a settled bird is not re-homed onto the wheel under the camera.
            Step(flock, 90);
            Assert.AreEqual(SeagullStates.Stand, Id(flock, bird), "she did not stay down");
            Assert.AreEqual(0f, Lift(flock, bird), 1e-6f, "she drifted up off her shadow after landing");
            Assert.Less(Vector2.Distance(ShadowFoot(flock, bird), GroundSpot), 1e-4f,
                "her shadow wandered off the spot after she was down");
        }

        // ── the table ───────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// <b>The sidecar's transition table is the law, and the chains are the ones it declares.</b>
        /// A bird cannot drop out of <c>fly</c> straight onto the shingle: the only way down is
        /// through the wheel's own <c>swoop</c>/<c>glide</c> and then the one-shot chain
        /// <c>land -> stand</c> or <c>splash -> float</c>. This walks two birds down — one to ground,
        /// one to water — and asserts every state change she makes after she leaves the wheel is an
        /// edge the sidecar spells out.
        ///
        /// <para>⚠ Only changes made OFF the wheel are judged. A flock-bound bird's anim is assigned
        /// by <c>Flock()</c>, which is free to put her from swoop back into glide — a pair the table
        /// does not declare as an edge, and does not need to, because no state machine took it.</para>
        ///
        /// <para>The charter's one-line rule — "only takeoff leaves a surface, only land/splash arrive
        /// on one" — is swept over all thirteen states here rather than trusted.</para>
        /// </summary>
        [Test]
        public void EveryChangeAfterSheLeavesTheWheelIsAnEdgeTheTableDeclares()
        {
            int land = _table.IndexOf(SeagullStates.Land);
            int splash = _table.IndexOf(SeagullStates.Splash);
            int takeoff = _table.IndexOf(SeagullStates.Takeoff);
            int fly = _table.IndexOf(SeagullStates.Fly);

            // The facts that make the chain non-negotiable, read off the table itself.
            Assert.IsFalse(_table.CanGo(fly, land),
                "fly -> land is an edge; a gull is dropping out of the sky onto the beach");
            Assert.IsFalse(_table.IsLoop(land), "land loops; a one-shot arrival must end");
            Assert.AreEqual(SeagullStates.Stand, _table.IdOf(_table.Next(land)),
                "land no longer chains into stand");
            Assert.AreEqual(SeagullStates.Float, _table.IdOf(_table.Next(splash)),
                "splash no longer chains into float");
            for (int s = 0; s < _table.StateCount; s++)
            {
                Assert.AreEqual(s == takeoff, _table.LeavesSurface(s),
                    "'only takeoff leaves a surface' is broken by " + _table.IdOf(s));
                Assert.AreEqual(s == land || s == splash, _table.ArrivesOnSurface(s),
                    "'only land/splash arrive on a surface' is broken by " + _table.IdOf(s));
            }
            Assert.AreEqual(SeagullSurface.Ground, _table.SurfaceOf(_table.Next(land)),
                "the end of the land chain is not on the ground");
            Assert.AreEqual(SeagullSurface.Water, _table.SurfaceOf(_table.Next(splash)),
                "the end of the splash chain is not on the water");

            GullFlock flock = NewFlock("EdgeFlock");
            Step(flock, WarmUpTicks);

            int toGround = BirdAloft(flock, 1.0);
            Assert.GreaterOrEqual(toGround, 0, "no bird was airborne to send to the shingle");
            int toWater = BirdAloft(flock, 1.0, toGround + 1);
            Assert.GreaterOrEqual(toWater, 0, "only one bird was airborne; the water arrival needs a second");

            Assert.IsTrue(flock.CommandLanding(toGround, GroundSpot, land, WindHeading),
                "the table refused a ground arrival from " + Id(flock, toGround));
            Assert.IsTrue(flock.CommandLanding(toWater, WaterSpot, splash, WindHeading),
                "the table refused a water arrival from " + Id(flock, toWater));

            var chainG = new List<int>();
            var chainW = new List<int>();
            int lastG = -1;
            int lastW = -1;
            for (int t = 0; t < PatienceTicks; t++)
            {
                flock.Tick(TickMilliseconds);
                lastG = Record(flock, toGround, chainG, lastG);
                lastW = Record(flock, toWater, chainW, lastW);
                if (Id(flock, toGround) == SeagullStates.Stand &&
                    Id(flock, toWater) == SeagullStates.Float) break;
            }

            AssertChain(chainG, SeagullStates.Land, SeagullStates.Stand, "the ground bird");
            AssertChain(chainW, SeagullStates.Splash, SeagullStates.Float, "the water bird");

            Assert.AreEqual(0.0, flock.BirdState(toGround).AltitudeMetres, 0.0,
                "the standing bird kept some altitude");
            Assert.AreEqual(0.0, flock.BirdState(toWater).AltitudeMetres, 0.0,
                "the floating bird kept some altitude");

            // PR 4 owns perches. Nothing in this PR may put a bird on one.
            for (int i = 0; i < flock.BirdCount; i++)
                Assert.AreNotEqual(SeagullSurface.Perch, _table.SurfaceOf(flock.BirdState(i).State),
                    "bird " + i + " perched; perches are PR 4 and this PR lands on flat ground and water");
        }

        /// <summary>
        /// Records bird <paramref name="bird"/>'s state if it changed since <paramref name="last"/>,
        /// asserting the change is a declared edge. Only counts once she is off the wheel — see the
        /// warning on the test above. Returns the state to compare against next tick.
        /// </summary>
        int Record(GullFlock flock, int bird, List<int> chain, int last)
        {
            SeagullSimBird b = flock.BirdState(bird);
            if (b.FlockBound) return -1;
            if (last < 0) { chain.Add(b.State); return b.State; }
            if (b.State == last) return last;
            Assert.IsTrue(_table.CanGo(last, b.State),
                _table.IdOf(last) + " -> " + _table.IdOf(b.State) +
                " is not a transition the sidecar declares");
            chain.Add(b.State);
            return b.State;
        }

        /// <summary>
        /// The whole descent is three states: the wheel state she peeled off in, the arrival, and the
        /// surface it chains to. Which flock state she starts in is hers — <c>CommandAlight</c> only
        /// routes through <c>swoop</c> when the arrival is not already reachable — so that one is
        /// asserted by KIND, and the two that the sidecar pins are asserted by name.
        /// </summary>
        void AssertChain(List<int> chain, string arrival, string surface, string who)
        {
            var names = new List<string>();
            for (int i = 0; i < chain.Count; i++) names.Add(_table.IdOf(chain[i]));
            string trail = string.Join(" -> ", names.ToArray());

            Assert.AreEqual(3, chain.Count, who + " walked " + trail + "; expected three states");
            Assert.IsTrue(_table.IsFlockDriven(chain[0]),
                who + " left the wheel in " + names[0] + ", which is not a flock state (" + trail + ")");
            Assert.AreEqual(arrival, names[1], who + " arrived through the wrong state (" + trail + ")");
            Assert.AreEqual(surface, names[2], who + " ended up somewhere else (" + trail + ")");
        }

        // ── the seed ────────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// <b>One seed, one flight.</b> Two flocks built from the same <see cref="GullConfig"/>, handed
        /// the same milliseconds and given the same command, are in exactly the same places doing
        /// exactly the same things — no tolerance, because there is nothing here for a tolerance to
        /// absorb: it is the same arithmetic twice. Rule 5, made checkable.
        ///
        /// <para>The comparator is proved able to fail before it is trusted: the same flock, snapshotted
        /// four seconds apart, must make it throw. A gull cruises at 9 m/s, so if those two snapshots
        /// compare equal the comparator is looking at nothing.</para>
        /// </summary>
        [Test]
        public void TwoFlocksOnOneSeedFlyTheSameFlight()
        {
            GullFlock p = NewFlock("SeedFlockA");
            GullFlock q = NewFlock("SeedFlockB");

            Step(p, WarmUpTicks);
            Step(q, WarmUpTicks);
            AssertSameFlight(Capture(p), Capture(q), "after the warm-up");

            int land = _table.IndexOf(SeagullStates.Land);
            int birdP = BirdAloft(p, 1.0);
            int birdQ = BirdAloft(q, 1.0);
            Assert.GreaterOrEqual(birdP, 0, "no bird was on the wheel to command");
            Assert.AreEqual(birdP, birdQ,
                "the two flocks disagree about which bird is where — the seed is not carrying");

            Assert.IsTrue(p.CommandLanding(birdP, GroundSpot, land, WindHeading));
            Assert.IsTrue(q.CommandLanding(birdQ, GroundSpot, land, WindHeading));

            Step(p, 240);
            Step(q, 240);

            Assert.AreEqual(p.SimMilliseconds, q.SimMilliseconds, 1e-9,
                "the two flocks are not even on the same clock");
            AssertSameFlight(Capture(p), Capture(q), "after the descent");

            // ⚠ THE CONTROL. If this passes, the comparator above is asserting nothing.
            Flight[] early = Capture(p);
            Step(p, 240);
            Flight[] late = Capture(p);
            bool moved = false;
            for (int i = 0; i < early.Length; i++)
                if (System.Math.Abs(late[i].X - early[i].X) > 1.0 ||
                    System.Math.Abs(late[i].Y - early[i].Y) > 1.0) moved = true;
            Assert.IsTrue(moved, "four seconds passed and no bird moved a metre; the sim is not running");
            Assert.Throws<AssertionException>(
                () => AssertSameFlight(early, late, "four seconds apart"),
                "the comparator called two different moments identical; it cannot fail, so it proves nothing");
        }

        // ── the clock ───────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// <b>Nothing in the sim path reads the wall clock.</b> The flock is handed milliseconds and
        /// integrates them; <c>Time</c> is touched in exactly one place, <c>Update</c>, which is what
        /// turns frames into that number. So a flock stepped 150 times with no frames at all and a
        /// flock stepped 150 times across real frames with <c>Time.timeScale</c> pinned to zero — real
        /// seconds passing, scaled seconds not — must land on the same flight.
        ///
        /// <para>Both flocks are DISABLED, so <c>Update</c> never fires on either and the only
        /// milliseconds either one sees are the ones this fixture hands over. If any part of the
        /// simulation reached for <c>Time.time</c>, <c>Time.deltaTime</c> or a frame count behind the
        /// seam, the stuttered flock would drift away from the steady one.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator TheSimAdvancesOnTheMillisecondsItIsHandedNotTheWallClock()
        {
            const int Ticks = 150;
            GullFlock steady = NewFlock("SteadyFlock");
            GullFlock stuttered = NewFlock("StutteredFlock");

            Step(steady, Ticks);

            Time.timeScale = 0f;
            float realBefore = Time.realtimeSinceStartup;
            for (int t = 0; t < Ticks; t++)
            {
                stuttered.Tick(TickMilliseconds);
                if (t % 10 != 9) continue;
                yield return null;
                PinGlobals();   // ⚠ the world ran a frame; put its globals back. See PinGlobals.
            }
            Assert.Greater(Time.realtimeSinceStartup - realBefore, 0f,
                "no real time passed across those frames; the stutter arm is not a stutter");

            // 1e-6 of a millisecond, not zero: 150 additions of 16.6 recurring accumulate their own
            // rounding. Wall-clock contamination would show up whole milliseconds away from this.
            Assert.AreEqual(Ticks * TickMilliseconds, steady.SimMilliseconds, 1e-6,
                "the flock's own clock is not the sum of the milliseconds it was handed");
            Assert.AreEqual(steady.SimMilliseconds, stuttered.SimMilliseconds, 1e-9,
                "the same number of ticks produced two different sim times");
            AssertSameFlight(Capture(steady), Capture(stuttered),
                "stepped across real frames at timeScale 0 rather than in one burst");
        }
    }
}
