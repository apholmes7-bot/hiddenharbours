using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// <b>NO GULL CAN FIRE A DOOR.</b> The creek's crossing to St Peters is a 6 × 120 m band (owner, 2026-10-17: a
    /// larger door each way), and a door fires for any collider with a body behind it. Gulls belong to places
    /// (#957), and the creek's breakwater and wharf birds fly their away lines across that band, as they already
    /// cross the 6 × 40 m door it replaced. What keeps a gull from firing a door is what the flock builds: a
    /// picture and its shadow, with nothing a trigger can feel.
    ///
    /// <para>This case builds a flock the way the flock's own installer does, a new object given the component,
    /// and looks at every object it made: the host, every bird of the pool, and each bird's shadow and contact
    /// pool. A surface school is one more place for the same pool birds, so its birds are among them: the flock
    /// builds nothing else for it. A later change that gives a gull, or anything a gull carries, a collider or a
    /// body turns this red. The EditMode scan in <c>NineMileCreekCrossingDoorTests</c> reads the same code.</para>
    ///
    /// <para>It is a PlayMode case because only a running scene builds a flock: the birds are made in the flock's
    /// Awake, and each shadow's children in the shadow's own. Nothing here renders or needs a frame.</para>
    /// </summary>
    public class NineMileCreekCrossingDoorPlayTests
    {
        GameObject _host;
        IBirdPresence _presenceBefore;

        [SetUp]
        public void SetUp() => _presenceBefore = GameServices.BirdPresence;

        [TearDown]
        public void TearDown()
        {
            if (_host != null) Object.DestroyImmediate(_host);
            _host = null;
            // A new flock takes the calls' registration from the installed one: hand it back.
            GameServices.BirdPresence = _presenceBefore;
        }

        [Test]
        public void NoGullCanFireADoor_TheFlockTheGameBuildsCarriesNoColliderAndNoBody()
        {
            // As the installer does it: a new object, given the flock. Its Awake builds the pool.
            _host = new GameObject("NoGullCanFireADoor");
            GullFlock flock = _host.AddComponent<GullFlock>();
            Assert.Greater(flock.BirdCount, 0, "the flock built no birds (no sheet), so the case would prove nothing");

            Transform[] made = _host.GetComponentsInChildren<Transform>(includeInactive: true);
            var physical = new List<string>();
            int shadows = 0;
            foreach (Transform t in made)
            {
                if (t.GetComponent<SpriteShadow>() != null) shadows++;
                foreach (Component c in t.GetComponents<Component>())
                    if (c is Collider2D || c is Collider || c is Rigidbody2D || c is Rigidbody)
                        physical.Add(c.GetType().Name + " on " + PathOf(t));
            }
            TestContext.WriteLine($"{flock.BirdCount} birds, {shadows} with a shadow, {made.Length} objects looked at");
            Assert.That(physical, Is.Empty,
                "a gull that carries a collider or a body can fire a door: " + string.Join("; ", physical));
        }

        static string PathOf(Transform t)
        {
            var path = new StringBuilder(t.name);
            for (Transform p = t.parent; p != null; p = p.parent) path.Insert(0, p.name + "/");
            return path.ToString();
        }
    }
}
