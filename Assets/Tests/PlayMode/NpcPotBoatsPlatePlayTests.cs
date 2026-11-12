using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using HiddenHarbours.App;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using HiddenHarbours.Fishing;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// Plates of the NPC lobster boat's day in the real Nine Mile Creek scene, through the game's own camera:
    /// one before (she never leaves her berth), then her day by the clock. GPU only; CI skips it. The
    /// plates land under the temporary cache path and are never committed.
    /// </summary>
    public sealed class NpcPotBoatsPlatePlayTests
    {
        private const string SceneName = "NineMileCreek";
        private const string PlateDir = "npc-lobster";
        private const string DefId = "potday.nmc_doiron_yvette";
        private const float SettleRealSeconds = 0.5f;

        private WharfNightStage _stage;
        private bool _autoBindWas;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            NpcPotBoats.AutoBind = _autoBindWas;
            if (_stage != null) yield return _stage.TearDown();
            _stage = null;
        }

        [UnityTest, Timeout(600000)]
        public IEnumerator HerDay_FromTheBerthToTheBuyer()
        {
            WharfNightStage.RequireAGraphicsDevice();
            Assert.AreNotEqual(SaveStore.DefaultPath, SaveStore.ActivePath, "a plate run never writes the player's save");
            _autoBindWas = NpcPotBoats.AutoBind;

            NpcPotBoats.AutoBind = false;
            _stage = new WharfNightStage(SceneName, PlateDir);
            yield return _stage.Load();
            yield return _stage.SetNight(9f);

            NpcPotDayLibrary library = Resources.Load<NpcPotDayLibrary>(NpcPotDayLibrary.ResourcesPath);
            Assert.IsNotNull(library);
            NpcPotDayDef def = null;
            foreach (NpcPotDayDef d in library.Days) if (d != null && d.Id == DefId) def = d;
            Assert.IsNotNull(def, DefId);

            MooredBoat hull = null;
            foreach (MooredBoat m in Object.FindObjectsByType<MooredBoat>())
                if (m.Owner != null && m.Owner.Id == def.OwnerId) hull = m;
            Assert.IsNotNull(hull, $"the scene has no moored boat for {def.OwnerId}");

            IGameClock clock = GameServices.Clock;
            double day = GameServices.SecondsPerDay;
            int dayIndex = NpcPotDay.DayIndexOf(clock.TotalSeconds, day);
            NpcPotDay today = NpcPotDay.Build(def, GameServices.Environment.WorldSeed, dayIndex, day);
            int midPot = today.PotCount / 2;

            // BEFORE: at the hour she would be hauling, she lies at her berth.
            yield return Shoot("0-before-at-her-berth.png", today.HaulStart[midPot], () => hull.transform.position);

            NpcPotBoats.AutoBind = true;
            float until = Time.realtimeSinceStartup + 3f;
            while ((NpcPotBoats.Instance == null || !NpcPotBoats.Instance.IsBound(DefId)) && Time.realtimeSinceStartup < until)
                yield return null;
            Assert.IsTrue(NpcPotBoats.Instance != null && NpcPotBoats.Instance.IsBound(DefId), "she was not bound in the real scene");
            NpcPotBoats boats = NpcPotBoats.Instance;

            yield return Shoot("1-gear-soaking-before-she-leaves.png", today.DepartSeconds - 20.0, () => def.StringStart);
            yield return Shoot("2-on-passage.png", (today.DepartSeconds + today.GaffStart[0]) * 0.5, () => hull.transform.position);
            yield return Shoot("3-hauling.png", (today.HaulStart[midPot] + today.HaulEnd[midPot]) * 0.5, () => hull.transform.position);

            NpcPotDayCatch caught = boats.CatchOf(DefId);
            Assert.IsNotNull(caught);
            int pot = -1, keeper = -1;
            for (int p = 0; p < caught.PotCount && pot < 0; p++)
                for (int k = 0; k < caught.Animals(p).Count && pot < 0; k++)
                    if (caught.Animals(p)[k].Keeper) { pot = p; keeper = k; }
            if (pot >= 0)
            {
                NpcPotAnimal a = caught.Animals(pot)[keeper];
                yield return Shoot("4-banding-a-keeper.png", a.PickedAt + def.PickSeconds + 0.8 * def.BandSeconds, () => hull.transform.position);
                yield return Shoot("5-carrying-it-to-its-crate.png", a.PickedAt + def.PickSeconds + def.BandSeconds + 0.6 * def.StowSeconds, () => hull.transform.position);
            }
            yield return Shoot("6-setting.png", (today.SetStart[midPot] + today.SetEnd[midPot]) * 0.5, () => hull.transform.position);
            yield return Shoot("7-home-with-her-crates.png", today.ReturnSeconds + 5.0, () => hull.transform.position);

            NpcPotLanding plan = boats.LandingOf(DefId);
            Assert.IsNotNull(plan);
            if (plan.HasLanding)
            {
                double onLadder = -1, onWharf = -1, down = -1;
                Vector2 midRoute = def.LandingRoute[def.LandingRoute.Length / 2];
                for (double t = plan.StartSeconds; t < plan.SoldAtSeconds && down < 0; t += 0.25)
                {
                    NpcLandingMoment m = plan.MomentAt(t);
                    if (onLadder < 0 && m.Ashore && m.Crate >= 0) onLadder = t + 0.5 * def.LadderSeconds;
                    if (onWharf < 0 && m.Ashore && m.Crate >= 0 && m.Position.y > midRoute.y) onWharf = t;
                    if (plan.AtCounterAt(t) > 0) down = t;
                }
                if (onLadder > 0) yield return Shoot("8-up-the-ladder-with-a-crate.png", onLadder, () => plan.MomentAt(onLadder).Position);
                if (onWharf > 0) yield return Shoot("9-along-the-wharf.png", onWharf, () => plan.MomentAt(onWharf).Position);
                if (down > 0) yield return Shoot("10-down-at-the-counter.png", down + 0.5, () => plan.CounterPosition);
                yield return Shoot("11-weighed-and-sold.png", plan.SoldAtSeconds + 1.0, () => plan.CounterPosition);
                yield return Shoot("12-back-aboard.png", plan.EndSeconds + 2.0, () => hull.transform.position);
            }

            // The rest of the fleet: each other boat at her own string, on her own way up the wharf, and
            // at her own patch of the counter.
            int shot = 13;
            foreach (NpcPotDayDef other in library.Days)
            {
                if (other == null || other.Id == DefId || !boats.IsBound(other.Id)) continue;
                MooredBoat otherHull = null;
                foreach (MooredBoat m in Object.FindObjectsByType<MooredBoat>())
                    if (m.Owner != null && m.Owner.Id == other.OwnerId) otherHull = m;
                Assert.IsNotNull(otherHull, other.OwnerId);
                string who = other.Id.Substring(other.Id.LastIndexOf('.') + 1);
                NpcPotDay hers = NpcPotDay.Build(other, GameServices.Environment.WorldSeed, dayIndex, day);
                int mid = hers.PotCount / 2;
                yield return Shoot($"{shot++}-{who}-hauling.png", (hers.HaulStart[mid] + hers.HaulEnd[mid]) * 0.5,
                                   () => otherHull.transform.position);

                NpcPotLanding landing = boats.LandingOf(other.Id);
                if (landing == null || !landing.HasLanding) continue;
                double ladder = -1, wharf = -1, counter = -1;
                Vector2 half = other.LandingRoute[other.LandingRoute.Length / 2];
                for (double t = landing.StartSeconds; t < landing.SoldAtSeconds && counter < 0; t += 0.25)
                {
                    NpcLandingMoment m = landing.MomentAt(t);
                    if (ladder < 0 && m.OnLadder && m.Crate >= 0) ladder = t + 0.5 * other.LadderSeconds;
                    if (wharf < 0 && m.Ashore && m.Crate >= 0 && m.Position.y > half.y) wharf = t;
                    if (landing.AtCounterAt(t) > 0) counter = t;
                }
                if (ladder > 0) yield return Shoot($"{shot++}-{who}-up-her-ladder.png", ladder, () => landing.MomentAt(ladder).Position);
                if (wharf > 0) yield return Shoot($"{shot++}-{who}-along-the-wharf.png", wharf, () => landing.MomentAt(wharf).Position);
                if (counter > 0) yield return Shoot($"{shot++}-{who}-at-the-counter.png", counter + 0.5, () => landing.CounterPosition);
            }
        }

        private IEnumerator Shoot(string name, double clockSeconds, System.Func<Vector2> focus)
        {
            GameServices.Clock.SeekTo(clockSeconds);
            Time.timeScale = 1f;
            float until = Time.realtimeSinceStartup + SettleRealSeconds;
            while (Time.realtimeSinceStartup < until) yield return null;
            yield return _stage.FrameOn(focus());
            _stage.SavePlate(name, _stage.Capture());
            Debug.Log($"[{PlateDir}] {name}: clock {GameServices.Clock.HourOfDay:0.00} h, focus {focus()}");
        }
    }
}
