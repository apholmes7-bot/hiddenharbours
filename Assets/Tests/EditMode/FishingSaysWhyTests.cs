using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Economy;
using HiddenHarbours.Fishing;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>She never gets away without a reason, and a rough sea says it is worth fishing.</b>
    ///
    /// <para>Until this, a missed strike, a lost fight, empty water and an unlicensed catch all wrote to
    /// the developer log only: the player saw the line come back and nothing else. Each now puts one
    /// short sentence on the notice channel the rest of the catch loop already speaks through
    /// (<see cref="DevNotice"/>). These tests drive the real controller into each failure and read that
    /// channel.</para>
    ///
    /// <para>The second half pins the sea line: the Core cue fires on the first cast of a blow and not
    /// again until the sea has come back down, and World's director answers the cue with the authored
    /// line. Fishing and World meet only on <see cref="InnerVoiceCue"/>.</para>
    /// </summary>
    public class FishingSaysWhyTests
    {
        private const string Region = "region.coddle_cove";

        private sealed class FakeHold : IHold
        {
            private readonly List<CatchItem> _items = new();
            public int CapacityUnits => 20;
            public int UsedUnits => _items.Count;
            public IReadOnlyList<CatchItem> Items => _items;
            public bool TryAdd(CatchItem item) { _items.Add(item); return true; }
            public void Clear() => _items.Clear();
        }

        private sealed class FakeLicenses : ILicenseService
        {
            private readonly HashSet<string> _held = new();
            public bool IsLicensed(string licenseId) => string.IsNullOrEmpty(licenseId) || _held.Contains(licenseId);
            public void Grant(string licenseId) { if (!string.IsNullOrEmpty(licenseId)) _held.Add(licenseId); }
            public int Count => _held.Count;
        }

        /// <summary>A sea whose state the test sets by hand. Everything else is flat and still.</summary>
        private sealed class FakeSea : IEnvironmentService
        {
            public float Sea01;
            public int WorldSeed => 0;
            public TideProfile ActiveTideProfile { get; set; }
            public EnvironmentSample Sample()
                => new EnvironmentSample(Vector2.zero, Vector2.zero, 0f, SeaState.Calm, 1f, Sea01);
            public float TideHeightAt(double totalSeconds) => 0f;
            public float WaterLevelAt(double totalSeconds) => 0f;
        }

        private readonly List<Object> _spawned = new();
        private readonly List<string> _said = new();
        private readonly List<string> _cues = new();
        private readonly List<AmbientSpeechRequested> _spoken = new();

        private void OnNotice(DevNotice e) => _said.Add(e.Text);
        /// <summary>Stands in for World's director on the bus: hears every cue, and acknowledges it
        /// as spoken unless the test says the line was refused.</summary>
        private bool _lineIsRefused;
        private void OnCue(InnerVoiceCue e)
        {
            _cues.Add(e.CueId);
            if (!_lineIsRefused) EventBus.Publish(new InnerVoiceCueSpoken(e.CueId));
        }
        private void OnSpoken(AmbientSpeechRequested e) => _spoken.Add(e);

        private int Said(string line)
        {
            int n = 0;
            for (int i = 0; i < _said.Count; i++) if (_said[i] == line) n++;
            return n;
        }

        [SetUp]
        public void SetUp()
        {
            _said.Clear();
            _cues.Clear();
            _spoken.Clear();
            _lineIsRefused = false;
            ClearBus();
            GameServices.Reset();
            InteractionGate.Reset();
            EventBus.Subscribe<DevNotice>(OnNotice);
            EventBus.Subscribe<InnerVoiceCue>(OnCue);
            EventBus.Subscribe<AmbientSpeechRequested>(OnSpoken);
        }

        [TearDown]
        public void TearDown()
        {
            ClearBus();
            GameServices.Reset();
            InteractionGate.Reset();
            foreach (var o in _spawned) if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        private static void ClearBus()
        {
            EventBus.Clear<DevNotice>();
            EventBus.Clear<InnerVoiceCue>();
            EventBus.Clear<InnerVoiceCueSpoken>();
            EventBus.Clear<AmbientSpeechRequested>();
            EventBus.Clear<FishingStateChanged>();
            EventBus.Clear<FishCaught>();
        }

        // ---- rig ----------------------------------------------------------------------------------

        private BiteDef MakeBite(int nibbles, float window, float returnChance, int maxPasses)
        {
            var b = ScriptableObject.CreateInstance<BiteDef>();
            b.Id = "bite.test";
            b.FalseNibblesMin = nibbles; b.FalseNibblesMax = nibbles;
            b.NibbleGapMinSeconds = 0.5f; b.NibbleGapMaxSeconds = 1f;
            b.NibbleDipSeconds = 0.3f;
            b.TakeWindowSeconds = window;
            b.ReturnChance01 = returnChance;
            b.ReturnDelayMinSeconds = 0.5f; b.ReturnDelayMaxSeconds = 1f;
            b.MaxPasses = maxPasses;
            _spawned.Add(b);
            return b;
        }

        private FishSpeciesDef MakeFish(string id, string displayName, BiteDef bite = null,
                                        string region = Region)
        {
            var f = ScriptableObject.CreateInstance<FishSpeciesDef>();
            f.Id = id; f.DisplayName = displayName; f.Category = FishCategory.InshoreGroundfish;
            f.RegionIds = new[] { region };
            f.AllowedGear = Gear.Handline | Gear.Longline;
            f.Seasons = SeasonMask.AllYear;
            f.MinTide = -10f; f.MaxTide = 10f; f.StartHour = 0f; f.EndHour = 24f;
            f.MinWeightKg = 1f; f.MaxWeightKg = 6f;
            f.BaseValue = 12; f.SupplyElasticity = 0.2f; f.SpawnWeight = 1f;
            f.Bite = bite;
            _spawned.Add(f);
            return f;
        }

        private LicenseDef MakeLicence(string id, string displayName, string speciesId)
        {
            var lic = ScriptableObject.CreateInstance<LicenseDef>();
            lic.Id = id; lic.DisplayName = displayName; lic.PermittedSpeciesIds = new[] { speciesId };
            _spawned.Add(lic);
            return lic;
        }

        private FishingController MakeController(FishSpeciesDef[] pool, int seed, LicenseDef[] licences = null,
                                                 Gear gear = Gear.Handline)
        {
            var go = new GameObject("Fisher");
            _spawned.Add(go);
            var c = go.AddComponent<FishingController>();
            c.Configure(new FakeHold(), pool, Region, gear, seed, licences);
            // OnEnable does not run for a plain component in EditMode, so wire the acknowledgement the
            // way OnEnable does. (Were it to run as well, a second Kept() is a no-op.)
            EventBus.Subscribe<InnerVoiceCueSpoken>(c.OnInnerVoiceCueSpoken);
            return c;
        }

        private static bool IsResult(FishingPhase p)
            => p == FishingPhase.Landed || p == FishingPhase.Snapped || p == FishingPhase.NoBite
               || p == FishingPhase.Idle;

        private static void TickUntil(FishingController c, FishingPhase phase, float maxSeconds = 120f)
        {
            float t = 0f;
            while (c.Phase != phase && t < maxSeconds) { c.Tick(0.05f, false); t += 0.05f; }
            Assert.AreEqual(phase, c.Phase, $"never reached {phase} (sat in {c.Phase})");
        }

        // ---- the four failures, each with its line --------------------------------------------------

        [Test]
        public void StrikingATease_Says_StruckTooSoon()
        {
            var c = MakeController(new[] { MakeFish("fish.cod", "Cod", MakeBite(1, 1.5f, 1f, 3)) }, seed: 42);
            FlickGestures.CastLine(c);

            TickUntil(c, FishingPhase.BiteNibble);
            Assert.AreEqual(0, _said.Count, "nothing is said while she is only teasing");

            c.Tick(0.05f, actionHeld: true);   // fooled: struck the tease

            Assert.AreEqual(1, Said(FishingStrings.StruckTooSoon), "the early strike is named, once");
            Assert.AreEqual(1, _said.Count, "and it is the only thing said");
        }

        [Test]
        public void SleepingThroughTheTake_Says_SheSpatIt()
        {
            // No teases, a short window, one pass: never striking lets the take lapse and she is gone.
            var c = MakeController(new[] { MakeFish("fish.cod", "Cod", MakeBite(0, 0.4f, 0f, 1)) }, seed: 7);
            FlickGestures.CastLine(c);

            TickUntil(c, FishingPhase.NoBite);

            Assert.AreEqual(1, Said(FishingStrings.SpatIt), "the late strike is named, once for the one pass");
            Assert.AreEqual(0, Said(FishingStrings.StruckTooSoon));
            Assert.AreEqual(0, Said(FishingStrings.NothingBiting),
                "she was there and was missed: that is not empty water");
        }

        [Test]
        public void EmptyWater_Says_NothingBitingHere()
        {
            // The only fish in the pool lives in another region, so the wait resolves to nothing.
            var elsewhere = MakeFish("fish.cod", "Cod", bite: null, region: "region.somewhere_else");
            var c = MakeController(new[] { elsewhere }, seed: 42);
            FlickGestures.CastLine(c);

            TickUntil(c, FishingPhase.NoBite);

            Assert.AreEqual(1, Said(FishingStrings.NothingBiting), "the empty wait is explained, once");
            Assert.AreEqual(1, _said.Count);

            TickUntil(c, FishingPhase.Idle);
            Assert.AreEqual(1, _said.Count, "the result beat adds nothing as it eases back to Idle");
        }

        [Test]
        public void ALostFight_Says_SheThrewTheHook()
        {
            var c = MakeController(new[] { MakeFish("fish.cod", "Cod") }, seed: 999);
            FlickGestures.CastLine(c);
            float t = 0f;
            while (c.Phase != FishingPhase.Fighting && t < 30f) { c.Tick(0.05f, false); t += 0.05f; }
            Assert.AreEqual(FishingPhase.Fighting, c.Phase);

            t = 0f;
            while (!IsResult(c.Phase) && t < 60f) { c.Tick(0.05f, actionHeld: true); t += 0.05f; }   // just hold

            Assert.AreEqual(FishingPhase.Snapped, c.Phase, "a pinned reel loses her");
            Assert.AreEqual(1, Said(FishingStrings.ThrewTheHook), "the lost fight is explained, once");
            Assert.AreEqual(1, _said.Count);
        }

        // ---- the licence: the rule stays, the silence goes ------------------------------------------

        [Test]
        public void AWonFightWithoutTheLicence_NamesTheSpeciesAndTheLicence()
        {
            var cod = MakeFish("fish.atlantic_cod", "Atlantic Cod");
            var licences = new[] { MakeLicence("license.cod", "Cod Fishing License", "fish.atlantic_cod") };
            GameServices.Licenses = new FakeLicenses();   // holds nothing

            var c = MakeController(new[] { cod }, seed: 999, licences: licences);
            FlickGestures.CastLine(c);
            float t = 0f;
            while (c.Phase != FishingPhase.Fighting && t < 30f) { c.Tick(0.05f, false); t += 0.05f; }
            t = 0f;
            while (!IsResult(c.Phase) && t < 120f)
            { c.Tick(0.05f, actionHeld: c.State.Tension01 < 0.5f); t += 0.05f; }   // a paced, winning fight

            Assert.AreEqual(FishingPhase.Snapped, c.Phase, "the rule is unchanged: unlicensed cod goes back");
            Assert.AreEqual(1, _said.Count, "one sentence explains it");
            StringAssert.Contains("Atlantic Cod", _said[0], "the line names the species from its Def");
            StringAssert.Contains("Cod Fishing License", _said[0], "and the licence from its Def");
            Assert.AreEqual(0, Said(FishingStrings.ThrewTheHook),
                "she did not throw the hook: the fight was won, and the line must not blame the player");
        }

        [Test]
        public void TheLicenceLine_IsBuiltFromData_AndDegradesToAnUnnamedOne()
        {
            var cod = MakeFish("fish.atlantic_cod", "Atlantic Cod");
            var mackerel = MakeFish("fish.mackerel", "Mackerel");
            var licences = new[]
            {
                MakeLicence("license.mackerel", "Mackerel Permit", "fish.mackerel"),
                MakeLicence("license.cod", "Cod Fishing License", "fish.atlantic_cod"),
            };

            string codLine = FishingStrings.LicenceNeeded(cod, licences);
            StringAssert.Contains("Atlantic Cod", codLine);
            StringAssert.Contains("Cod Fishing License", codLine);
            StringAssert.DoesNotContain("Mackerel", codLine, "it names the licence that gates THIS species");

            string mackerelLine = FishingStrings.LicenceNeeded(mackerel, licences);
            StringAssert.Contains("Mackerel Permit", mackerelLine);

            Assert.AreEqual(FishingStrings.LicenceNeededUnnamed, FishingStrings.LicenceNeeded(cod, null));
            Assert.AreEqual(FishingStrings.LicenceNeededUnnamed, FishingStrings.LicenceNeeded((FishSpeciesDef)null, licences));
            Assert.AreEqual(FishingStrings.LicenceNeededUnnamed, FishingStrings.LicenceNeeded("Cod", ""));
        }

        [Test]
        public void EveryFailureHasItsOwnLine()
        {
            Assert.AreEqual(FishingStrings.StruckTooSoon, FishingStrings.MissedStrike(BiteMiss.Early));
            Assert.AreEqual(FishingStrings.SpatIt, FishingStrings.MissedStrike(BiteMiss.Late));
            Assert.IsNull(FishingStrings.MissedStrike(BiteMiss.None), "no miss, nothing to explain");

            var lines = new[]
            {
                FishingStrings.StruckTooSoon, FishingStrings.SpatIt, FishingStrings.ThrewTheHook,
                FishingStrings.NothingBiting, FishingStrings.LicenceNeededUnnamed,
            };
            var seen = new HashSet<string>();
            foreach (string s in lines)
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(s));
                Assert.IsTrue(seen.Add(s), $"'{s}' is said for two different failures");
            }
        }

        // ---- the sea announces good fishing ---------------------------------------------------------

        [Test]
        public void TheSeaPromise_FiresOncePerWeatherChange()
        {
            SeaFishingSettings s = SeaFishingSettings.Default;
            float calm = SeaFightMath.CozySeaCeiling01 * 0.5f;
            const float rough = 0.9f;
            var promise = new SeaPromise();

            Assert.IsFalse(promise.OnCast(calm, in s), "everyday weather promises nothing");
            Assert.IsTrue(promise.OnCast(rough, in s), "the first cast of the blow");
            promise.Kept();
            Assert.IsFalse(promise.OnCast(rough, in s), "the second cast of the same blow is silent");
            Assert.IsFalse(promise.OnCast(1f, in s), "and it building further is still the same blow");
            Assert.IsFalse(promise.OnCast(calm, in s), "it has dropped: nothing to say, but it re-arms");
            Assert.IsTrue(promise.OnCast(rough, in s), "the next blow is announced again");
            promise.Kept();
            Assert.IsFalse(promise.OnCast(rough, in s));
        }

        [Test]
        public void TheSeaPromise_IsSpentOnlyWhenTheLineWasSaid()
        {
            SeaFishingSettings s = SeaFishingSettings.Default;
            float calm = SeaFightMath.CozySeaCeiling01 * 0.5f;
            const float rough = 0.9f;
            var promise = new SeaPromise();

            Assert.IsTrue(promise.OnCast(rough, in s));
            Assert.IsFalse(promise.Promised, "asking is not saying");
            Assert.IsTrue(promise.OnCast(rough, in s), "refused the first time, so the next cast asks again");
            promise.Kept();
            Assert.IsTrue(promise.Promised);
            Assert.IsFalse(promise.OnCast(rough, in s), "said: this blow is spent");

            Assert.IsFalse(promise.OnCast(calm, in s));
            promise.Kept();   // a stray acknowledgement in calm weather
            Assert.IsTrue(promise.OnCast(rough, in s), "cannot silence the next blow");
        }

        [Test]
        public void TheSeaPromise_UsesTheExistingEverydayCeiling_AndNeedsTheRewardOn()
        {
            SeaFishingSettings s = SeaFishingSettings.Default;
            Assert.IsFalse(SeaPromise.SeaHelps(SeaFightMath.CozySeaCeiling01, in s),
                "AT the everyday ceiling is still everyday weather");
            Assert.IsTrue(SeaPromise.SeaHelps(SeaFightMath.CozySeaCeiling01 + 0.01f, in s));
            Assert.IsFalse(SeaPromise.SeaHelps(float.NaN, in s));

            // The promise is only honest where the sea really does fish better.
            Assert.Less(SeaFightMath.BiteDelayScale(0.9f, s.SeaBoldness01), 1f);
            Assert.Greater(SeaFightMath.WeightBias01(0.9f, s.SeaBigFishBias01), 0f);

            var off = s;
            off.SeaBoldness01 = 0f;
            off.SeaBigFishBias01 = 0f;
            Assert.IsFalse(SeaPromise.SeaHelps(1f, in off),
                "with both rewards switched off a storm helps nobody, so nothing is promised");
            Assert.IsFalse(new SeaPromise().OnCast(1f, in off));
        }

        [Test]
        public void TheSeaPromise_IsAFunctionOfTheSeasItWasShown()
        {
            // Same samples in the same order, same answers: no clock, no randomness (rule 5).
            SeaFishingSettings s = SeaFishingSettings.Default;
            float[] seas = { 0.1f, 0.7f, 0.8f, 0.2f, 0.95f, 0.95f, 0.5f, 0.51f };
            var a = new SeaPromise();
            var b = new SeaPromise();
            for (int i = 0; i < seas.Length; i++)
            {
                bool wantA = a.OnCast(seas[i], in s);
                bool wantB = b.OnCast(seas[i], in s);
                Assert.AreEqual(wantA, wantB, $"sample {i}");
                if (wantA) { a.Kept(); b.Kept(); }
            }
        }

        [Test]
        public void TheController_RaisesTheCue_OnTheFirstCastOfABlow_Only()
        {
            var sea = new FakeSea { Sea01 = 0.9f };
            GameServices.Environment = sea;
            // Water with nothing in it, so each cast runs out to NoBite and back to Idle on its own.
            var elsewhere = MakeFish("fish.cod", "Cod", bite: null, region: "region.somewhere_else");
            var c = MakeController(new[] { elsewhere }, seed: 42);

            FlickGestures.CastLine(c);
            Assert.AreEqual(1, _cues.Count, "the first cast into a rough sea raises the cue");
            Assert.AreEqual(InnerVoiceCues.FeedingSea, _cues[0]);

            TickUntil(c, FishingPhase.Idle);
            FlickGestures.CastLine(c);
            Assert.AreEqual(1, _cues.Count, "a second cast in the same weather does not repeat it");

            sea.Sea01 = 0.1f;                       // the blow passes
            TickUntil(c, FishingPhase.Idle);
            FlickGestures.CastLine(c);
            Assert.AreEqual(1, _cues.Count, "a calm sea promises nothing");

            sea.Sea01 = 0.9f;                       // and another comes in
            TickUntil(c, FishingPhase.Idle);
            FlickGestures.CastLine(c);
            Assert.AreEqual(2, _cues.Count, "the weather changed, so the next blow is announced");
        }

        [Test]
        public void ARefusedLine_DoesNotSpendTheBlow_TheNextCastAsksAgain()
        {
            var sea = new FakeSea { Sea01 = 0.9f };
            GameServices.Environment = sea;
            var elsewhere = MakeFish("fish.cod", "Cod", bite: null, region: "region.somewhere_else");
            var c = MakeController(new[] { elsewhere }, seed: 42);

            _lineIsRefused = true;                  // a conversation owns the screen, say
            FlickGestures.CastLine(c);
            Assert.AreEqual(1, _cues.Count, "the cue is raised");

            TickUntil(c, FishingPhase.Idle);
            _lineIsRefused = false;
            FlickGestures.CastLine(c);
            Assert.AreEqual(2, _cues.Count, "nobody said it, so the same blow asks again");

            TickUntil(c, FishingPhase.Idle);
            FlickGestures.CastLine(c);
            Assert.AreEqual(2, _cues.Count, "now it was said: the blow is spent");
        }

        [Test]
        public void HandGatheringGear_InABlow_RaisesNoFeedingCue()
        {
            GameServices.Environment = new FakeSea { Sea01 = 0.9f };
            var clam = MakeFish("fish.clam", "Clam", bite: null, region: "region.somewhere_else");
            var c = MakeController(new[] { clam }, seed: 42, gear: Gear.ClamFork);

            FlickGestures.CastLine(c);

            Assert.AreEqual(FishingPhase.Waiting, c.Phase, "the gather did reach the wait");
            Assert.AreEqual(0, _cues.Count, "a fork in the sand is not a cast line: no feeding thought");
        }

        [Test]
        public void WithNoEnvironment_TheControllerPromisesNothing()
        {
            var elsewhere = MakeFish("fish.cod", "Cod", bite: null, region: "region.somewhere_else");
            var c = MakeController(new[] { elsewhere }, seed: 42);
            FlickGestures.CastLine(c);
            Assert.AreEqual(0, _cues.Count, "no environment reads as a flat calm");
        }

        // ---- World answers the cue with the authored line -------------------------------------------

        private InnerVoiceLineDef MakeCueLine(string cueId, string text)
        {
            var d = ScriptableObject.CreateInstance<InnerVoiceLineDef>();
            d.Id = "innervoice.test_cue";
            d.Line = text;
            d.Trigger = InnerVoiceTrigger.Cue;
            d.SubjectId = cueId;
            d.OnceOnly = false;
            d.CooldownSeconds = 0f;
            _spawned.Add(d);
            return d;
        }

        [Test]
        public void TheDirector_SpeaksTheLineAuthoredAgainstTheCue()
        {
            var lib = ScriptableObject.CreateInstance<InnerVoiceLibrary>();
            lib.Lines = new[] { MakeCueLine(InnerVoiceCues.FeedingSea, "They'll be feeding in this.") };
            _spawned.Add(lib);

            var go = new GameObject("InnerVoiceDirector (test)");
            _spawned.Add(go);
            var director = go.AddComponent<InnerVoiceDirector>();
            director.LoadLibrary(lib);
            _spoken.Clear();

            var heard = new List<string>();
            void OnHeard(InnerVoiceCueSpoken e) => heard.Add(e.CueId);
            EventBus.Subscribe<InnerVoiceCueSpoken>(OnHeard);

            Assert.IsFalse(director.HearCue("cue.nobody.wrote_this"), "an unknown cue is silence, not an error");
            Assert.IsFalse(director.HearCue(null));
            Assert.AreEqual(0, _spoken.Count);

            InteractionGate.IsBlocked = true;       // a conversation owns the screen
            Assert.IsFalse(director.HearCue(InnerVoiceCues.FeedingSea));
            Assert.AreEqual(0, _spoken.Count, "refused: nothing is said");
            Assert.AreEqual(0, heard.Count, "and nothing is acknowledged, so the publisher will ask again");
            InteractionGate.IsBlocked = false;

            Assert.IsTrue(director.HearCue(InnerVoiceCues.FeedingSea));
            Assert.AreEqual(1, heard.Count, "a line that went out is acknowledged, once");
            Assert.AreEqual(InnerVoiceCues.FeedingSea, heard[0]);
            Assert.AreEqual(1, _spoken.Count, "one cue, one thought");
            Assert.AreEqual("They'll be feeding in this.", _spoken[0].Line);
            Assert.AreEqual(AmbientSpeechKind.InnerVoice, _spoken[0].Kind);
            Assert.AreEqual("innervoice.test_cue", director.LastSpokenId);
        }

        [Test]
        public void ACueLine_MatchesItsCueWhole_AndNothingElse()
        {
            InnerVoiceLineDef d = MakeCueLine(InnerVoiceCues.FeedingSea, "Hm.");
            Assert.IsTrue(d.WantsCue(InnerVoiceCues.FeedingSea));
            Assert.IsFalse(d.WantsCue(InnerVoiceCues.FeedingSea + ".more"));
            Assert.IsFalse(d.WantsCue(null));
            Assert.IsFalse(d.WantsOffer(InnerVoiceCues.FeedingSea), "a cue line is not an interact-offer line");

            d.Trigger = InnerVoiceTrigger.InteractOffered;
            Assert.IsFalse(d.WantsCue(InnerVoiceCues.FeedingSea), "and an offer line does not answer cues");
        }

        [Test]
        public void TheShippedLibrary_HasALineForTheFeedingSea()
        {
            var lib = Resources.Load<InnerVoiceLibrary>(InnerVoiceLibrary.ResourcesPath);
            Assert.IsNotNull(lib, "the inner-voice library must load from Resources");

            InnerVoiceLineDef found = null;
            foreach (InnerVoiceLineDef d in lib.Lines)
                if (d != null && d.WantsCue(InnerVoiceCues.FeedingSea)) { found = d; break; }

            Assert.IsNotNull(found,
                $"no line in the library answers '{InnerVoiceCues.FeedingSea}', so the rod loop's cue " +
                "would be raised and never heard");
            Assert.IsFalse(string.IsNullOrWhiteSpace(found.Line));
            Assert.IsFalse(found.OnceOnly,
                "the rod loop already says it once per blow; a once-ever line would go silent after the first storm");
        }
    }
}
