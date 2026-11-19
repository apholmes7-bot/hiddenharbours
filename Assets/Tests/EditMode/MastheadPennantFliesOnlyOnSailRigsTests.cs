using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>No sail plan, no masthead pennant.</b> The wind pennant was added to every boat and placed at a
    /// fixed deck spot, so on the dory (and every hull with no mast) it hung in the air beside the
    /// gunwale as a small red fleck that moved with the boat. The rule pinned here: the pennant draws only
    /// on a hull whose own data carries a usable sail plan, across every shipped hull, and the renderer
    /// on a boat follows the hull it is wearing right now. Where it does fly, it flies from the masthead
    /// her rig publishes (copied into <c>BoatHullDef.MastheadRigMetres</c>), never the old fixed spot.
    ///
    /// <para>EditMode runs no <c>Awake</c>/<c>LateUpdate</c> on AddComponent, so the lifecycle is called
    /// by reflection (the <c>GiftRefreshesContainerPicturesTests</c> pattern).</para>
    /// </summary>
    public class MastheadPennantFliesOnlyOnSailRigsTests
    {
        private const string BoatsRoot = "Assets/_Project/Data/Boats";

        private readonly List<Object> _spawned = new List<Object>();

        [SetUp]
        public void SetUp() => GameServices.Environment = null;   // no wind service: the pennant still decides

        [TearDown]
        public void TearDown()
        {
            foreach (Object o in _spawned) if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
            GameServices.Reset();
        }

        private static BoatHullDef Hull(string file)
        {
            string path = BoatsRoot + "/" + file;
            var hull = AssetDatabase.LoadAssetAtPath<BoatHullDef>(path);
            Assert.That(hull, Is.Not.Null, path);
            return hull;
        }

        private static void Call(Object target, string method)
        {
            MethodInfo m = target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic |
                                                              BindingFlags.Public);
            Assert.That(m, Is.Not.Null, $"{target.GetType().Name}.{method} exists");
            m.Invoke(target, null);
        }

        private static void Wear(BoatController boat, BoatHullDef hull)
        {
            // The serialized hull field, set directly: SetHull also rebuilds the cabin and presence,
            // which is not what this rule is about.
            FieldInfo f = typeof(BoatController).GetField("_hull", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(f, Is.Not.Null, "BoatController._hull exists");
            f.SetValue(boat, hull);
        }

        private SpriteRenderer PennantAfterAFrame(BoatController boat)
        {
            var telltale = boat.GetComponent<MastheadTelltale>();
            if (telltale == null)
            {
                telltale = boat.gameObject.AddComponent<MastheadTelltale>();
                Call(telltale, "Awake");
            }
            Call(telltale, "LateUpdate");
            Transform child = boat.transform.Find("MastheadTelltale");
            Assert.That(child, Is.Not.Null, "the pennant object is built once at Awake");
            return child.GetComponent<SpriteRenderer>();
        }

        private BoatController Boat(BoatHullDef hull)
        {
            var go = new GameObject("Boat");   // BoatController auto-adds Rigidbody2D + collider + mooring
            _spawned.Add(go);
            var boat = go.AddComponent<BoatController>();
            Wear(boat, hull);
            return boat;
        }

        // ---- the pure rule over the shipped content ---------------------------------------------------

        [Test]
        public void EveryShippedHull_FliesThePennant_OnlyIfItHasASailPlan()
        {
            int flown = 0, struck = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:BoatHullDef", new[] { BoatsRoot }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var hull = AssetDatabase.LoadAssetAtPath<BoatHullDef>(path);
                if (hull == null) continue;
                Assert.That(MastheadTelltale.FliesOn(hull), Is.EqualTo(hull.HasSailPlan),
                    $"{path}: the pennant flies exactly when the hull has a sail plan");
                if (hull.HasSailPlan)
                    Assert.That(hull.HasMasthead, Is.True,
                        $"{path}: a sail hull must carry her measured masthead, or she flies no pennant at all");
                if (hull.HasSailPlan) flown++; else struck++;
            }
            Assert.That(flown, Is.GreaterThan(0), "the sailing rigs still fly it");
            Assert.That(struck, Is.GreaterThan(0), "the hulls with no mast were checked");
        }

        [Test]
        public void TheDoryThePuntsAndTheEngineBoats_FlyNothing_TheSloopsDo()
        {
            Assert.That(MastheadTelltale.FliesOn(Hull("Dory.asset")), Is.False, "the rowed dory has no mast");
            Assert.That(MastheadTelltale.FliesOn(Hull("DoryOutboard.asset")), Is.False);
            Assert.That(MastheadTelltale.FliesOn(Hull("Punt.asset")), Is.False);
            Assert.That(MastheadTelltale.FliesOn(Hull("LobsterBoat.asset")), Is.False);
            Assert.That(MastheadTelltale.FliesOn(Hull("CapeIslander.asset")), Is.False);
            Assert.That(MastheadTelltale.FliesOn(Hull("Sloop30.asset")), Is.True, "a sloop flies it at her masthead");
            Assert.That(MastheadTelltale.FliesOn(Hull("Sloop88.asset")), Is.True);
            Assert.That(MastheadTelltale.FliesOn(null), Is.False, "no hull, nothing to fly");
        }

        // ---- where it flies: her real masthead, from data ---------------------------------------------

        // The old fixed spot every boat used: 1.1 m toward the bow on a 0.9 m staff.
        private static readonly Vector3 OldFixedSpot = new Vector3(0f, 1.1f, 0.9f);

        private static readonly Regex MastheadInAnchors = new Regex(
            "\"ANCHORS\"\\s*:\\s*\\{.*?\"masthead\"\\s*:\\s*\\[\\s*([-0-9.eE]+)\\s*,\\s*([-0-9.eE]+)\\s*,\\s*([-0-9.eE]+)\\s*\\]",
            RegexOptions.Singleline);

        /// <summary>Her masthead as her rig's gameplay sidecar publishes it (ANCHORS.masthead), found
        /// beside the sailing sidecar her polar names.</summary>
        private static Vector3 SidecarMasthead(BoatHullDef hull, string path)
        {
            string sailing = hull.SailPolar.SourceSidecarPath;
            Assert.That(sailing, Does.EndWith(".sailing.json"), $"{path}: her polar names its sailing sidecar");
            string gameplay = sailing.Substring(0, sailing.Length - ".sailing.json".Length) + ".gameplay.json";
            string full = Path.Combine(Directory.GetParent(Application.dataPath).FullName, gameplay);
            Assert.That(File.Exists(full), Is.True, $"{path}: rig sidecar {gameplay}");
            Match m = MastheadInAnchors.Match(File.ReadAllText(full));
            Assert.That(m.Success, Is.True, $"{gameplay}: ANCHORS.masthead");
            return new Vector3(Num(m.Groups[1].Value), Num(m.Groups[2].Value), Num(m.Groups[3].Value));
        }

        private static float Num(string s) => float.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture);

        [Test]
        public void EverySailHull_FliesFromTheMastheadHerRigPublishes_NeverTheOldFixedSpot()
        {
            int sailHulls = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:BoatHullDef", new[] { BoatsRoot }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var hull = AssetDatabase.LoadAssetAtPath<BoatHullDef>(path);
                if (hull == null || !hull.HasSailPlan) continue;
                sailHulls++;

                Vector3 authored = hull.MastheadRigMetres;
                Vector3 rig = SidecarMasthead(hull, path);
                Assert.That(authored.x, Is.EqualTo(rig.x).Within(1e-4f), $"{path}: masthead x is the rig's");
                Assert.That(authored.y, Is.EqualTo(rig.y).Within(1e-4f), $"{path}: masthead y is the rig's");
                Assert.That(authored.z, Is.EqualTo(rig.z).Within(1e-4f), $"{path}: masthead z is the rig's");
                Assert.That(authored, Is.Not.EqualTo(OldFixedSpot), $"{path}: never the old fixed spot");
                Assert.That(authored.z, Is.GreaterThan(OldFixedSpot.z), $"{path}: up the mast, not on a staff");

                // The pennant's anchor is that point, projected the way every deck fitting is, at all
                // eight drawn headings, at the iso rigs' elevation and in plan view.
                foreach (float elevation in new[] { 40f, DeckAreaMath.PlanViewElevationDegrees })
                    for (int h = 0; h < 360; h += 45)
                    {
                        Vector2 want = DeckAreaMath.DeckToWorld(new Vector2(rig.x, rig.y), rig.z, h, elevation);
                        Vector2 got = MastheadTelltale.MastheadLevelOffset(hull, h, elevation);
                        Assert.That(got.x, Is.EqualTo(want.x).Within(1e-4f), $"{path} at {h}°, elev {elevation}: x");
                        Assert.That(got.y, Is.EqualTo(want.y).Within(1e-4f), $"{path} at {h}°, elev {elevation}: y");
                    }
            }
            Assert.That(sailHulls, Is.GreaterThan(0), "the sail hulls were checked");
        }

        [Test]
        public void ASailHullWithNoMeasuredMasthead_FliesNothing_RatherThanFallBack()
        {
            var unmeasured = ScriptableObject.Instantiate(Hull("Sloop30.asset"));
            _spawned.Add(unmeasured);
            unmeasured.MastheadRigMetres = Vector3.zero;
            Assert.That(unmeasured.HasSailPlan, Is.True, "still a sailing hull");
            Assert.That(MastheadTelltale.FliesOn(unmeasured), Is.False, "no masthead measured: no pennant, no guessed spot");
        }

        // ---- on a boat ---------------------------------------------------------------------------------

        [Test]
        public void OnASloop_ThePennantSitsOnHerAuthoredMasthead()
        {
            BoatHullDef sloop = Hull("Sloop30.asset");
            BoatController boat = Boat(sloop);   // at the origin, bow north, no hull picture: plan view
            SpriteRenderer pennant = PennantAfterAFrame(boat);
            Assert.That(pennant.enabled, Is.True);

            Vector2 want = MastheadTelltale.MastheadLevelOffset(sloop, 0f, DeckAreaMath.PlanViewElevationDegrees);
            Vector3 at = pennant.transform.position;
            Assert.That(at.x, Is.EqualTo(want.x).Within(1e-4f), "on her mast, abeam");
            Assert.That(at.y, Is.EqualTo(want.y).Within(1e-4f), "on her mast, fore and aft");
            Assert.That(at.y, Is.Not.EqualTo(OldFixedSpot.y + OldFixedSpot.z).Within(1e-3f), "not the old fixed spot");
        }

        [Test]
        public void OnTheDory_ThePennantIsNeverDrawn()
        {
            BoatController boat = Boat(Hull("Dory.asset"));
            SpriteRenderer pennant = PennantAfterAFrame(boat);
            Assert.That(pennant.enabled, Is.False, "the red fleck beside the dory is gone");
            pennant = PennantAfterAFrame(boat);
            Assert.That(pennant.enabled, Is.False, "and stays gone frame after frame");
        }

        [Test]
        public void AHullSwap_HoistsAndStrikesThePennant()
        {
            BoatController boat = Boat(Hull("Punt.asset"));
            Assert.That(PennantAfterAFrame(boat).enabled, Is.False, "the punt: no pennant");

            Wear(boat, Hull("Sloop30.asset"));
            Assert.That(PennantAfterAFrame(boat).enabled, Is.True, "bought up to a sloop: hoisted");

            Wear(boat, Hull("Dory.asset"));
            Assert.That(PennantAfterAFrame(boat).enabled, Is.False, "back on the dory: struck again");
        }
    }
}
