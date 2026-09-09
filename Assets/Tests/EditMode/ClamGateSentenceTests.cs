using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Fishing;
using HiddenHarbours.Player;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>"Pressing e the first time I was warned that I needed a bucket, but after that it continued to
    /// let me store clams."</b> — the owner, 2026-09-09, at the St Peters flats. This fixture is the
    /// instrument that names which mechanism can say that sentence, and the guard that stops it being
    /// said to a fisher who is carrying a pail.
    ///
    /// <para><b>What the source said before a line was changed, and what this pins.</b>
    /// <see cref="ClamDig.TryDig"/> can only reach <i>"Nowhere to put a clam — you need a bucket."</i>
    /// when the in-hand landing is not taken AND no hold answers. Two candidate mechanisms were on the
    /// charter; the fixture answers both:
    /// <list type="number">
    ///   <item><b>"The first press raced the hands."</b>
    ///   <see cref="A_press_before_the_hands_are_up_says_nothing_about_a_bucket"/> shows it cannot: the
    ///   shovel gate two checks earlier reads the SAME relay (<c>CarriedItem.InHand</c> →
    ///   <see cref="GameServices.Hands"/>) that the in-hand landing reads
    ///   (<see cref="GameServices.CatchHands"/>), and one <c>CarryHands.OnEnable</c> publishes both. A
    ///   press that has got as far as the pail has already proved the hands exist, so a late-hands press
    ///   refuses about the SHOVEL, never about a bucket.</item>
    ///   <item><b>"The hole's provider reference is dead."</b> That one is real wherever a hole's
    ///   serialized <c>_bucketProvider</c> does not survive the scene that wrote it — and
    ///   <see cref="A_hole_with_a_dead_provider_fills_the_pail_on_her_belt_instead_of_lying"/> is the
    ///   fix: the pail is a fact about HER, published on <see cref="GameServices.PlayerHold"/>, so the
    ///   dig asks her when the hole has nothing.</item>
    /// </list></para>
    ///
    /// <para><b>The rule is not touched, only the lie.</b> The 2026-08-13 ruling still stands in every
    /// test here: with hands present a clam lands IN HER HAND and the pail stays empty until she puts it
    /// there, and a clam already in hand still refuses with its own sentence. What changed is that the
    /// bucket sentence is now reachable only where it is TRUE — she has no pail at all.</para>
    /// </summary>
    public class ClamGateSentenceTests
    {
        // ---- the world under the flat ----------------------------------------------------------------

        private sealed class FlatTerrain : ITidalTerrain
        {
            public float ElevationAt(Vector2 worldPos) => 1.0f;   // bared ground
        }

        private sealed class FlatEnv : IEnvironmentService
        {
            public float Level = 0.5f;                            // …under a low tide
            public int WorldSeed => 0;
            public TideProfile ActiveTideProfile { get; set; }
            public EnvironmentSample Sample() => default;
            public float TideHeightAt(double totalSeconds) => Level;
            public float WaterLevelAt(double totalSeconds) => Level;
        }

        private sealed class FakeSaveService : ISaveService
        {
            public FakeSaveService(SaveData data) { Current = data; }
            public SaveData Current { get; }
            public bool GetFlag(string key) => false;
            public void SetFlag(string key, bool value) { }
            public void Save() { }
        }

        private sealed class StampClock : IGameClock
        {
            public double TotalSeconds { get; set; }
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

        private const string BucketSentence = "Nowhere to put a clam — you need a bucket.";
        private const string ShovelSentence = "Your hands are empty — you need the shovel to dig.";
        private const string InHandSentence = "You're already holding a clam — put it in the bucket first.";

        private readonly List<Object> _spawned = new();
        private readonly List<string> _said = new();
        private void OnNotice(DevNotice e) => _said.Add(e.Text);

        private SaveData _save;

        [SetUp]
        public void SetUp()
        {
            _said.Clear();
            EventBus.Clear<DevNotice>();
            EventBus.Clear<FishCaught>();
            EventBus.Clear<CatchLanded>();
            EventBus.Subscribe<DevNotice>(OnNotice);
            GameServices.Reset();
            Interactables.Clear();
            InteractVerb.Reset();

            _save = SaveMigration.NewGame();
            _save.OwnedGear.Add("gear.shovel");
            _save.OwnedGear.Add("gear.bucket");      // she OWNS a pail — that is what made the lie a lie

            GameServices.Clock = new StampClock { TotalSeconds = 1000d };
            GameServices.TidalTerrain = new FlatTerrain();
            GameServices.Environment = new FlatEnv();
            GameServices.Save = new FakeSaveService(_save);
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Unsubscribe<DevNotice>(OnNotice);
            EventBus.Clear<DevNotice>();
            EventBus.Clear<FishCaught>();
            EventBus.Clear<CatchLanded>();
            Interactables.Clear();
            InteractVerb.Reset();
            GameServices.Reset();
            foreach (var o in _spawned) if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        // ---- fixtures --------------------------------------------------------------------------------

        private FishSpeciesDef Clam()
        {
            var f = ScriptableObject.CreateInstance<FishSpeciesDef>();
            f.Id = "fish.soft_shell_clam"; f.DisplayName = "Soft-shell Clam";
            f.Category = FishCategory.Shellfish;
            f.MinWeightKg = 0.05f; f.MaxWeightKg = 0.2f; f.BaseValue = 2;
            f.SupplyElasticity = 0.45f; f.SpoilPerDay = 0.5f;
            _spawned.Add(f);
            return f;
        }

        /// <summary>A real pail. <paramref name="onHerBelt"/> publishes it the way a live
        /// <c>ClamBucket.OnEnable</c> does — EditMode never fires <c>OnEnable</c>, and a fixture that
        /// forgot this line would take the no-pail path while looking like it had one.</summary>
        private ClamBucket Pail(bool onHerBelt)
        {
            var go = new GameObject(onHerBelt ? "Player(pail on her belt)" : "Pail(wired to the hole)");
            _spawned.Add(go);
            var b = go.AddComponent<ClamBucket>();
            b.Configure(20, requireOwnedBucket: false);
            if (onHerBelt) GameServices.PlayerHold = b;
            return b;
        }

        /// <summary>
        /// A hole on the bared flat. <paramref name="provider"/> is written into the REAL serialized
        /// <c>_bucketProvider</c> field (reflection, this suite's precedent) rather than handed to
        /// <c>Configure</c>, because the whole question is what happens when THAT field does not resolve
        /// — a hole whose scene was rebuilt round a different core, or authored in another region.
        /// </summary>
        private ClamDig Hole(GameObject provider, bool landInHand, string shovelToolId = null,
                             IHold cached = null)
        {
            var go = new GameObject("ClamHole");
            _spawned.Add(go);
            var d = go.AddComponent<ClamDig>();
            d.Configure(Clam(), cached, spot: go.transform, shovelGearId: "gear.shovel",
                        seed: 7, reachRadius: 1.25f);
            d.ConfigureInteract("fixture.clam_hole.test", shovelToolId, landInHand);
            typeof(ClamDig).GetField("_bucketProvider", BindingFlags.Instance | BindingFlags.NonPublic)
                           .SetValue(d, provider);
            return d;
        }

        private string LastSaid => _said.Count == 0 ? "(nothing)" : _said[_said.Count - 1];

        // ---- 1. MECHANISM A, ANSWERED ----------------------------------------------------------------

        [Test]
        public void A_press_before_the_hands_are_up_says_nothing_about_a_bucket()
        {
            // Nothing published: this is EXACTLY "the carry hands have not published themselves yet".
            // GameServices.Hands and GameServices.CatchHands are written by one CarryHands.OnEnable and
            // cleared by one OnDestroy, so they are null together — and the shovel gate reads the first
            // of them two checks BEFORE the pail is ever consulted.
            ClamDig dig = Hole(provider: null, landInHand: true, shovelToolId: ToolDef.ShovelId);

            Assert.IsFalse(dig.TryDig(), "no hands, no dig");
            Assert.AreEqual(ShovelSentence, LastSaid,
                            "A late-hands press refuses about the SHOVEL. It cannot reach the pail: the " +
                            "gate that stops it reads the same relay the in-hand landing reads, and one " +
                            "publisher writes both. Mechanism A cannot produce the bucket sentence.");
            CollectionAssert.DoesNotContain(_said, BucketSentence,
                            "…and above all it must never blame her gear for the core's boot order.");
        }

        [Test]
        public void Even_if_the_two_hand_relays_disagreed_the_clam_would_go_in_her_pail_not_a_sentence()
        {
            // The belt-and-braces case: the ONE state in which the old code could have said the sentence
            // with the shovel in hand — the read relay up, the catch relay not. Production cannot reach
            // it (one OnEnable writes both), but if it ever did, the answer must be her pail, not a lie.
            CarryHands hands = ToolInHand.Holding(ToolDef.ShovelId, _spawned);
            GameServices.CatchHands = null;
            ClamBucket belt = Pail(onHerBelt: true);
            ClamDig dig = Hole(provider: null, landInHand: true, shovelToolId: ToolDef.ShovelId);

            Assert.IsTrue(dig.TryDig(), "the dig lands");
            Assert.AreEqual(1, belt.UsedUnits, "…in the pail on her belt");
            Assert.IsNull(hands.Carried as CarriableCatch,
                          "…because there was no catch relay to hand it to");
            CollectionAssert.DoesNotContain(_said, BucketSentence, "and not one word about a bucket");
        }

        // ---- 2. MECHANISM B, FIXED -------------------------------------------------------------------

        [Test]
        public void A_hole_with_a_dead_provider_fills_the_pail_on_her_belt_instead_of_lying()
        {
            // The hole knows nothing (its serialized provider did not survive), she carries a pail. This
            // is the shape the owner met, and the sentence it used to produce was false in his hand.
            ClamBucket belt = Pail(onHerBelt: true);
            ClamDig dig = Hole(provider: null, landInHand: false);

            Assert.IsTrue(dig.TryDig(), "a dug clam has somewhere to go: her own pail");
            Assert.AreEqual(1, belt.UsedUnits);
            CollectionAssert.DoesNotContain(_said, BucketSentence,
                            "the sentence was a lie about her gear; the hole's wiring was the fault");
        }

        [Test]
        public void The_bucket_sentence_survives_only_where_it_is_TRUE()
        {
            // No hold on the flat, no pail on her person. Now the words are honest, and P5 still says
            // them — a refused press earns a sentence.
            ClamDig dig = Hole(provider: null, landInHand: false);

            Assert.IsFalse(dig.TryDig());
            Assert.AreEqual(BucketSentence, LastSaid,
                            "with no pail anywhere the sentence is the truth and must still be said");
        }

        [Test]
        public void A_live_provider_still_wins_over_the_pail_on_her_belt()
        {
            // Nothing is taken away: a hole that WAS wired keeps filling what it was wired to.
            ClamBucket belt = Pail(onHerBelt: true);
            ClamBucket wired = Pail(onHerBelt: false);
            ClamDig dig = Hole(provider: wired.gameObject, landInHand: false);

            Assert.IsTrue(dig.TryDig());
            Assert.AreEqual(1, wired.UsedUnits, "the hole's own hold took it");
            Assert.AreEqual(0, belt.UsedUnits, "…and the fallback stayed a fallback");
        }

        [Test]
        public void A_hold_that_has_been_destroyed_falls_back_rather_than_reading_a_corpse()
        {
            // ⚠️ _bucket is INTERFACE-typed, so a destroyed ClamBucket cached in it compares non-null for
            // ever (an interface reference does not carry UnityEngine.Object's overloaded ==). Without
            // the laundering line in EnsureBucket this press reads a corpse instead of her pail.
            ClamBucket belt = Pail(onHerBelt: true);
            ClamBucket wired = Pail(onHerBelt: false);
            GameObject wiredGo = wired.gameObject;
            ClamDig dig = Hole(provider: wiredGo, landInHand: false, cached: wired);

            Object.DestroyImmediate(wiredGo);
            Assert.IsNotNull(GameServices.PlayerHold,
                             "a pail that never owned the slot must not clear it on its way out");

            Assert.IsTrue(dig.TryDig(), "a dead hold is not a hold — the dig looks again");
            Assert.AreEqual(1, belt.UsedUnits, "…and lands the clam in the pail on her belt");
        }

        // ---- 3. THE OWNER'S ACCEPTANCE ---------------------------------------------------------------

        [Test]
        public void Press_one_and_press_five_land_in_the_same_place()
        {
            // "Dig five clams from a fresh load, read every sentence." The destination must not depend on
            // which press it is — that is the whole of the owner's complaint, stated as an assertion.
            CarryHands hands = ToolInHand.Holding(ToolDef.ShovelId, _spawned);
            ClamBucket belt = Pail(onHerBelt: true);

            for (int press = 1; press <= 5; press++)
            {
                ClamDig dig = Hole(provider: null, landInHand: true, shovelToolId: ToolDef.ShovelId);
                Assert.IsTrue(dig.TryDig(), $"press {press} digs");
                Assert.IsInstanceOf<CarriableCatch>(hands.Carried,
                                                    $"press {press}: the clam is IN HER HAND (08-13)");
                Assert.IsTrue(hands.TryGiveCatchTo(belt), $"press {press}: and it stacks in the pail");
                Assert.AreEqual(press, belt.UsedUnits, $"press {press}: same destination as press 1");
            }

            CollectionAssert.DoesNotContain(_said, BucketSentence,
                            "five presses, and not one of them mentions a bucket she is carrying");
        }

        [Test]
        public void The_hands_still_come_first_and_a_clam_already_in_hand_still_refuses()
        {
            // The 08-13 ruling, untouched: a full hand is its own problem with its own sentence, and it
            // is answered BEFORE the pail is consulted.
            ToolInHand.Holding(ToolDef.ShovelId, _spawned);
            Pail(onHerBelt: true);

            Assert.IsTrue(Hole(null, landInHand: true, shovelToolId: ToolDef.ShovelId).TryDig());
            Assert.IsFalse(Hole(null, landInHand: true, shovelToolId: ToolDef.ShovelId).TryDig(),
                           "she is already holding one");
            Assert.AreEqual(InHandSentence, LastSaid, "a full hand and a full pail are different problems");
        }

        // ---- 4. THE TABLE THE CHARTER ASKED FOR ------------------------------------------------------

        [Test]
        public void The_gate_table_A_beside_B()
        {
            // One place to read what every combination now does. Printed, so a failure is diagnosable
            // from the CI log alone rather than from five separate red lines.
            string handsDeadProvider = WhereDoesItGo(handsUp: true,  providerLive: false, pailOnHerBelt: true);
            string handsLiveProvider = WhereDoesItGo(handsUp: true,  providerLive: true,  pailOnHerBelt: true);
            string noHandsDead       = WhereDoesItGo(handsUp: false, providerLive: false, pailOnHerBelt: true);
            string noHandsLive       = WhereDoesItGo(handsUp: false, providerLive: true,  pailOnHerBelt: true);
            string noHandsNoPail     = WhereDoesItGo(handsUp: false, providerLive: false, pailOnHerBelt: false);

            var report = new System.Text.StringBuilder("clam dig — where a press puts the clam\n");
            report.AppendLine($"  hands up, provider dead, pail on belt : {handsDeadProvider}");
            report.AppendLine($"  hands up, provider live, pail on belt : {handsLiveProvider}");
            report.AppendLine($"  no hands, provider dead, pail on belt : {noHandsDead}");
            report.AppendLine($"  no hands, provider live, pail on belt : {noHandsLive}");
            report.AppendLine($"  no hands, provider dead, NO pail      : {noHandsNoPail}");
            Debug.Log(report.ToString());

            Assert.AreEqual("her hand", handsDeadProvider, "hands first — the 08-13 ruling");
            Assert.AreEqual("her hand", handsLiveProvider, "hands first, the wiring is irrelevant");
            Assert.AreEqual("the pail on her belt", noHandsDead, "THE FIX — this row used to be the lie");
            Assert.AreEqual("the hole's own hold", noHandsLive, "nothing is taken away from a wired hole");
            Assert.AreEqual($"refused: {BucketSentence}", noHandsNoPail,
                            "the only surviving home of the sentence, and there it is true");
        }

        /// <summary>Run one row of the table and say, in words, where the clam ended up.</summary>
        private string WhereDoesItGo(bool handsUp, bool providerLive, bool pailOnHerBelt)
        {
            GameServices.Hands = null;
            GameServices.CatchHands = null;
            GameServices.PlayerHold = null;
            _said.Clear();

            CarryHands hands = handsUp ? ToolInHand.Holding(ToolDef.ShovelId, _spawned) : null;
            ClamBucket belt = pailOnHerBelt ? Pail(onHerBelt: true) : null;
            ClamBucket wired = providerLive ? Pail(onHerBelt: false) : null;
            ClamDig dig = Hole(wired != null ? wired.gameObject : null,
                               landInHand: handsUp, shovelToolId: handsUp ? ToolDef.ShovelId : null);

            if (!dig.TryDig()) return $"refused: {LastSaid}";
            if (hands != null && hands.Carried is CarriableCatch) return "her hand";
            if (wired != null && wired.UsedUnits > 0) return "the hole's own hold";
            if (belt != null && belt.UsedUnits > 0) return "the pail on her belt";
            return "nowhere — a dropped clam";
        }
    }
}
