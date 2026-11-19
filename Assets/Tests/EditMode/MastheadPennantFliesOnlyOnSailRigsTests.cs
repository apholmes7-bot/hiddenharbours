using System.Collections.Generic;
using System.Reflection;
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
    /// on a boat follows the hull it is wearing right now.
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

        // ---- on a boat ---------------------------------------------------------------------------------

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
