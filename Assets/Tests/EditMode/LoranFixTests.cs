using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The Loran-C slice: TD maths both ways, the deterministic error (bounded, repeatable, seed-keyed,
    /// worse on a baseline extension), the lamp (fix, blink, no signal, not fitted), the numbers store,
    /// steering to stored numbers, the helm slot, and the shipped chain data. The maths tests build
    /// their own chain so a retune of the shipped assets cannot turn them red; the content tests read
    /// the shipped assets.
    /// </summary>
    public class LoranFixTests
    {
        private const string LoranFolder = "Assets/_Project/Data/Boats/Loran";
        private const string ReceiverPath = LoranFolder + "/StandardLoranReceiver.asset";
        private const string OfferPath = "Assets/_Project/Data/Resources/Catalog/Instruments/LoranOffer.asset";
        private const string CapeHelmPath = "Assets/_Project/Data/Boats/Helms/CapeIslanderHelm.asset";
        private const int Seed = 1234;
        private const double Noon = 12.0 * LoranMath.SecondsPerGameHour;

        private readonly List<Object> _spawned = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _spawned)
                if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        // ---- builders -------------------------------------------------------------------------------

        private LoranChainDef Chain(string id, Vector2 master, Vector2 x, Vector2 y, float range)
        {
            var c = ScriptableObject.CreateInstance<LoranChainDef>();
            c.Id = id;
            c.GroupRepetitionInterval = 5880;
            c.Master = master;
            c.Secondaries = new[]
            {
                new LoranSecondary { Letter = "X", Position = x, CodingDelayMicroseconds = 11000f },
                new LoranSecondary { Letter = "Y", Position = y, CodingDelayMicroseconds = 26000f },
            };
            c.RangeMetres = range;
            c.RepeatableErrorMicroseconds = 0.1f;
            c.DriftErrorMicroseconds = 0.03f;
            c.ErrorCellMetres = 500f;
            c.DriftBucketHours = 6f;
            c.FringeErrorGain = 3f;
            _spawned.Add(c);
            return c;
        }

        private LoranChainDef Bay() => Chain("loran_chain.test_bay", new Vector2(-6000f, 4000f),
                                             new Vector2(7000f, 6000f), new Vector2(2000f, -9000f), 30000f);

        private LoranReceiverDef Receiver(params LoranChainDef[] chains)
        {
            var r = ScriptableObject.CreateInstance<LoranReceiverDef>();
            r.Id = "loran_receiver.test";
            r.Chains = chains;
            _spawned.Add(r);
            return r;
        }

        private static readonly Vector2[] HomeWater =
        {
            new Vector2(0f, 0f), new Vector2(2400f, -1200f), new Vector2(-1500f, 800f),
            new Vector2(5000f, -3000f), new Vector2(-2500f, -4000f),
        };

        // ---- maths ----------------------------------------------------------------------------------

        [Test]
        public void IdealTds_RoundTripToTheSamePosition()
        {
            LoranChainDef bay = Bay();
            LoranReceiverDef rx = Receiver(bay);
            foreach (Vector2 p in HomeWater)
            {
                double a = LoranMath.TimeDifference(bay, 0, p, rx.PropagationMetresPerMicrosecond);
                double b = LoranMath.TimeDifference(bay, 1, p, rx.PropagationMetresPerMicrosecond);
                Vector2 guess = p + new Vector2(800f, -600f);
                Assert.IsTrue(LoranMath.TryPositionFromTds(bay, 0, 1, a, b, guess, rx, out Vector2 back), p.ToString());
                Assert.Less((back - p).magnitude, 1f, p.ToString());
            }
        }

        [Test]
        public void Td_IncludesBaselineAndCodingDelay_AtTheMaster()
        {
            LoranChainDef bay = Bay();
            double c = 299.69;
            // At the master |P - S| is the baseline and |P - M| is zero, so the TD is two baselines'
            // travel plus the coding delay.
            double baseline = LoranMath.Distance(bay.Master, bay.Secondaries[0].Position);
            double td = LoranMath.TimeDifference(bay, 0, bay.Master, c);
            Assert.AreEqual(2.0 * baseline / c + 11000.0, td, 1e-6);
        }

        [Test]
        public void Geometry_IsWorseOnABaselineExtension()
        {
            LoranChainDef bay = Bay();
            double c = 299.69;
            Vector2 away = (bay.Master - bay.Secondaries[0].Position).normalized;
            Vector2 extension = bay.Master + away * 3000f;
            double home = LoranMath.GeometryMetresPerMicrosecond(bay, 0, 1, Vector2.zero, c);
            double ext = LoranMath.GeometryMetresPerMicrosecond(bay, 0, 1, extension, c);
            Assert.Less(home, 500.0, "the home water sits well inside the chain");
            Assert.Greater(ext, 5.0 * home, "a baseline extension must dilute the fix");
        }

        [Test]
        public void Error_IsBoundedByTheChainNumbers()
        {
            LoranChainDef bay = Bay();
            for (int i = 0; i < 200; i++)
            {
                var p = new Vector2(-9000f + i * 97.3f, 7000f - i * 81.1f);
                double t = i * 1931.7;
                double f = LoranMath.RangeFraction(bay, p);
                double bound = (bay.RepeatableErrorMicroseconds + bay.DriftErrorMicroseconds) * LoranMath.RangeGain(bay, f);
                for (int s = 0; s < 2; s++)
                    Assert.LessOrEqual(Math.Abs(LoranMath.ErrorMicroseconds(bay, s, Seed, p, t, f)), bound + 1e-9);
            }
        }

        [Test]
        public void Error_IsDeterministic_AndKeyedByTheSeed()
        {
            LoranChainDef bay = Bay();
            bool differs = false;
            foreach (Vector2 p in HomeWater)
            {
                double f = LoranMath.RangeFraction(bay, p);
                double e1 = LoranMath.ErrorMicroseconds(bay, 0, Seed, p, Noon, f);
                double e2 = LoranMath.ErrorMicroseconds(bay, 0, Seed, p, Noon, f);
                Assert.AreEqual(e1, e2, 0.0, "same inputs, same error, bit for bit");
                if (LoranMath.ErrorMicroseconds(bay, 0, Seed + 1, p, Noon, f) != e1) differs = true;
            }
            Assert.IsTrue(differs, "another world seed must give another error field");
        }

        [Test]
        public void Error_IsSmooth_NoJumpAcrossACellEdge()
        {
            LoranChainDef bay = Bay();
            // Straddle a cell edge (cells are 500 m) by one metre: value noise must not step.
            var a = new Vector2(1499.5f, 250f);
            var b = new Vector2(1500.5f, 250f);
            double f = LoranMath.RangeFraction(bay, a);
            double ea = LoranMath.ErrorMicroseconds(bay, 0, Seed, a, Noon, f);
            double eb = LoranMath.ErrorMicroseconds(bay, 0, Seed, b, Noon, f);
            // A hard step at the edge could be as big as twice the amplitude; smooth noise moves at
            // most a few thousandths of a microsecond over one metre.
            Assert.Less(Math.Abs(ea - eb), 0.05 * bay.RepeatableErrorMicroseconds);
        }

        [Test]
        public void Quantize_RoundsToTheReadoutStep()
        {
            Assert.AreEqual(11034.2, LoranMath.Quantize(11034.21, 0.1), 1e-9);
            Assert.AreEqual(11034.3, LoranMath.Quantize(11034.26, 0.1), 1e-9);
        }

        [Test]
        public void StableIdHash_IsFnv1a()
        {
            Assert.AreEqual(2166136261u, LoranMath.StableIdHash(""));
            Assert.AreEqual(0xE40C292Cu, LoranMath.StableIdHash("a"));
        }

        // ---- the set --------------------------------------------------------------------------------

        [Test]
        public void NotFitted_ReadsNothing()
        {
            LoranReading r = LoranReceiver.Read(Receiver(Bay()), false, Vector2.zero, Seed, Noon);
            Assert.AreEqual(LoranSignal.NotFitted, r.Signal);
            Assert.IsFalse(r.HasNumbers);
        }

        [Test]
        public void HomeWater_GivesAFix_WithErrorOfTensOfMetres()
        {
            LoranReceiverDef rx = Receiver(Bay());
            foreach (Vector2 p in HomeWater)
            {
                LoranReading r = LoranReceiver.Read(rx, true, p, Seed, Noon);
                Assert.AreEqual(LoranSignal.Fix, r.Signal, p.ToString());
                Assert.AreEqual("loran_chain.test_bay", r.ChainId);
                Assert.AreEqual("X", r.LetterA);
                Assert.AreEqual("Y", r.LetterB);
                Assert.IsTrue(r.HasFix);
                float off = (r.FixPosition - p).magnitude;
                Assert.Less(off, 150f, "a good fix is near, not exact: " + p);
            }
        }

        [Test]
        public void Read_IsDeterministic()
        {
            LoranReceiverDef rx = Receiver(Bay());
            foreach (Vector2 p in HomeWater)
            {
                LoranReading a = LoranReceiver.Read(rx, true, p, Seed, Noon);
                LoranReading b = LoranReceiver.Read(rx, true, p, Seed, Noon);
                Assert.AreEqual(a.TdA, b.TdA, 0.0);
                Assert.AreEqual(a.TdB, b.TdB, 0.0);
                Assert.AreEqual(a.FixPosition, b.FixPosition);
                Assert.AreEqual(a.Signal, b.Signal);
            }
        }

        [Test]
        public void ShownTds_AreOnTheReadoutStep()
        {
            LoranReceiverDef rx = Receiver(Bay());
            LoranReading r = LoranReceiver.Read(rx, true, new Vector2(2400f, -1200f), Seed, Noon);
            double steps = r.TdA / rx.DisplayResolutionMicroseconds;
            Assert.AreEqual(Math.Round(steps), steps, 1e-6);
        }

        [Test]
        public void OutOfRange_HasNoSignal()
        {
            LoranReading r = LoranReceiver.Read(Receiver(Bay()), true, new Vector2(100000f, -100000f), Seed, Noon);
            Assert.AreEqual(LoranSignal.NoSignal, r.Signal);
            Assert.IsFalse(r.HasNumbers);
        }

        [Test]
        public void FringeOfRange_Blinks()
        {
            LoranChainDef bay = Bay();
            LoranReceiverDef rx = Receiver(bay);
            // Inside range but past the fringe fraction from the farthest station.
            Vector2 p = new Vector2(0f, 0f);
            float far = Mathf.Max((p - bay.Master).magnitude,
                                  Mathf.Max((p - bay.Secondaries[0].Position).magnitude,
                                            (p - bay.Secondaries[1].Position).magnitude));
            bay.RangeMetres = far / ((rx.FringeRangeFraction + 1f) * 0.5f);
            LoranReading r = LoranReceiver.Read(rx, true, p, Seed, Noon);
            Assert.AreEqual(LoranSignal.Blink, r.Signal);
            Assert.IsTrue(r.HasNumbers, "a blinking set still shows numbers");
        }

        [Test]
        public void BaselineExtension_RefusesThatPair()
        {
            LoranChainDef bay = Bay();
            LoranReceiverDef rx = Receiver(bay);
            Vector2 away = (bay.Master - bay.Secondaries[0].Position).normalized;
            LoranReading r = LoranReceiver.Read(rx, true, bay.Master + away * 3000f, Seed, Noon);
            Assert.AreNotEqual(LoranSignal.Fix, r.Signal, "no good fix on a baseline extension");
        }

        [Test]
        public void TheSetPicksTheBetterChain()
        {
            LoranChainDef bay = Bay();
            LoranChainDef far = Chain("loran_chain.test_far", new Vector2(28000f, -2000f),
                                      new Vector2(10000f, -34000f), new Vector2(48000f, -30000f), 50000f);
            LoranReceiverDef rx = Receiver(far, bay);
            Assert.AreEqual(bay.Id, LoranReceiver.Read(rx, true, Vector2.zero, Seed, Noon).ChainId);
            Assert.AreEqual(far.Id, LoranReceiver.Read(rx, true, new Vector2(30000f, -25000f), Seed, Noon).ChainId);
        }

        // ---- the numbers ----------------------------------------------------------------------------

        [Test]
        public void ComingBackToTheNumbers_ComesBackToTheSpot()
        {
            LoranReceiverDef rx = Receiver(Bay());
            var store = new LoranWaypointStore(rx.MaxStoredWaypoints);
            var ground = new Vector2(2400f, -1200f);
            LoranReading there = LoranReceiver.Read(rx, true, ground, Seed, Noon);
            Assert.IsTrue(store.TrySave(there, out int n));
            Assert.AreEqual(1, n);
            Assert.IsTrue(store.TryGet(n, out LoranWaypoint wp));

            // Same spot, same hour: the set reads the same numbers, so it is ON the mark even though
            // its fix is off the true position by the chain's error.
            LoranSteer back = LoranReceiver.Steer(rx, LoranReceiver.Read(rx, true, ground, Seed, Noon), wp);
            Assert.IsTrue(back.HasSteer);
            Assert.Less(back.DistanceMetres, 1f);

            // Hours later the drift has moved a little; still close.
            LoranReading later = LoranReceiver.Read(rx, true, ground, Seed, Noon + 9.0 * LoranMath.SecondsPerGameHour);
            LoranSteer drift = LoranReceiver.Steer(rx, later, wp);
            Assert.IsTrue(drift.HasSteer);
            Assert.Less(drift.DistanceMetres, 120f);
        }

        [Test]
        public void SteeringFromAfar_PointsAtTheGround()
        {
            LoranReceiverDef rx = Receiver(Bay());
            var store = new LoranWaypointStore(rx.MaxStoredWaypoints);
            var ground = new Vector2(2400f, -1200f);
            var boat = new Vector2(-1500f, 800f);
            Assert.IsTrue(store.TrySave(LoranReceiver.Read(rx, true, ground, Seed, Noon), out int n));
            store.TryGet(n, out LoranWaypoint wp);

            LoranSteer steer = LoranReceiver.Steer(rx, LoranReceiver.Read(rx, true, boat, Seed, Noon), wp);
            Assert.IsTrue(steer.HasSteer);
            float trueBearing = NavMath.BearingDegrees(boat, ground);
            float trueDistance = NavMath.DistanceMetres(boat, ground);
            Assert.Less(Mathf.Abs(Mathf.DeltaAngle(trueBearing, steer.BearingDegrees)), 5f);
            Assert.AreEqual(trueDistance, steer.DistanceMetres, 300f);
        }

        [Test]
        public void Steer_WithoutAFix_IsNone()
        {
            LoranReceiverDef rx = Receiver(Bay());
            var wp = new LoranWaypoint(1, "loran_chain.test_bay", 0, 1, 11000.0, 26000.0);
            Assert.IsFalse(LoranReceiver.Steer(rx, LoranReading.Off(LoranSignal.NoSignal), wp).HasSteer);
        }

        [Test]
        public void Store_RefusesWhenFull_AndReusesTheLowestFreeNumber()
        {
            var store = new LoranWaypointStore(3);
            for (int i = 1; i <= 3; i++)
            {
                Assert.IsTrue(store.TryEnter("loran_chain.bay", 0, 1, 11000.0 + i, 26000.0, out int n));
                Assert.AreEqual(i, n);
            }
            Assert.IsTrue(store.IsFull);
            Assert.IsFalse(store.TryEnter("loran_chain.bay", 0, 1, 11010.0, 26000.0, out int refused));
            Assert.AreEqual(0, refused);

            Assert.IsTrue(store.Remove(2));
            Assert.IsFalse(store.Remove(2));
            Assert.IsTrue(store.TryEnter("loran_chain.bay", 0, 1, 11020.0, 26000.0, out int reused));
            Assert.AreEqual(2, reused);
            for (int i = 0; i < store.Count; i++) Assert.AreEqual(i + 1, store.At(i).Number, "kept in number order");
        }

        [Test]
        public void Store_RefusesAReadingWithNoNumbers()
        {
            var store = new LoranWaypointStore(3);
            Assert.IsFalse(store.TrySave(LoranReading.Off(LoranSignal.NoSignal), out int n));
            Assert.AreEqual(0, n);
            Assert.AreEqual(0, store.Count);
        }

        // ---- the helm slot --------------------------------------------------------------------------

        [Test]
        public void Loran_FitsOnlyAHelmWithRoomForIt()
        {
            var console = ScriptableObject.CreateInstance<HelmConsoleDef>();
            _spawned.Add(console);
            console.Rig = ConsoleRigKind.Cape;
            var owned = new HashSet<string> { BoatEquipment.LoranId };

            console.SupportsLoran = false;
            Assert.IsFalse(BoatEquipment.EffectiveFit(console, owned).Loran);
            console.SupportsLoran = true;
            Assert.IsTrue(BoatEquipment.EffectiveFit(console, owned).Loran);
            Assert.IsFalse(BoatEquipment.EffectiveFit(console, new HashSet<string>()).Loran);

            console.DefaultLoran = true;
            console.SupportsLoran = false;
            Assert.IsFalse(BoatEquipment.EffectiveFit(console, new HashSet<string>()).Loran,
                           "a default the helm has no room for is not fitted");
            Assert.IsFalse(HelmFit.None.Loran);
        }

        // ---- shipped data ---------------------------------------------------------------------------

        [Test]
        public void ShippedReceiver_IsValid_AndCoversHomeAndTheDeepGrounds()
        {
            var rx = UnityEditor.AssetDatabase.LoadAssetAtPath<LoranReceiverDef>(ReceiverPath);
            Assert.IsNotNull(rx, ReceiverPath);
            Assert.IsNull(rx.Problem());
            Assert.AreEqual(3, rx.Chains.Length);
            foreach (LoranChainDef c in rx.Chains) Assert.IsNull(c.Problem(), c.Id);

            Assert.AreEqual(LoranSignal.Fix, LoranReceiver.Read(rx, true, Vector2.zero, Seed, Noon).Signal,
                            "the home bay");
            Assert.AreEqual(LoranSignal.Fix, LoranReceiver.Read(rx, true, new Vector2(30000f, -25000f), Seed, Noon).Signal,
                            "the deep-water grounds to the south-east");
            Assert.AreEqual(LoranSignal.NoSignal,
                            LoranReceiver.Read(rx, true, new Vector2(150000f, 150000f), Seed, Noon).Signal,
                            "far beyond every chain");
        }

        [Test]
        public void ShippedOffer_IsTheLoranInstrument_AndTheCapeHelmTakesIt()
        {
            var offer = UnityEditor.AssetDatabase.LoadAssetAtPath<HiddenHarbours.Economy.InstrumentOffer>(OfferPath);
            Assert.IsNotNull(offer, OfferPath);
            Assert.AreEqual(BoatEquipment.LoranId, offer.Id);
            Assert.Greater(offer.Price, 0);

            var cape = UnityEditor.AssetDatabase.LoadAssetAtPath<HelmConsoleDef>(CapeHelmPath);
            Assert.IsNotNull(cape, CapeHelmPath);
            Assert.IsTrue(cape.SupportsLoran);
            Assert.IsTrue(BoatEquipment.EffectiveFit(cape, new HashSet<string> { BoatEquipment.LoranId }).Loran);
        }
    }
}
