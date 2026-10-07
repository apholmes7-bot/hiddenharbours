using HiddenHarbours.Core;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    public class GullPerchTests
    {
        sealed class Offer : IGullPerch
        {
            public string Id { get; set; }
            public Vector2 GroundPoint { get; set; }
            public Vector2 ScreenPoint => GroundPoint + Vector2.up;
            public float SortY => GroundPoint.y;
        }

        [Test]
        public void APerch_IsExclusive_ReleasedByItsBird_AndRemovedWithItsHost()
        {
            var offer = new Offer { Id = "test.perch", GroundPoint = Vector2.one };
            object a = new object(), b = new object();
            GullPerches.Register(offer);
            try
            {
                Assert.IsFalse(GullPerches.TryClaim(Vector2.zero, Vector2.zero, a, out _));
                Assert.IsTrue(GullPerches.TryClaim(Vector2.zero, Vector2.one * 2, a, out var held));
                Assert.AreSame(offer, held);
                Assert.IsFalse(GullPerches.TryClaim(Vector2.zero, Vector2.one * 2, b, out _));
                GullPerches.Release(offer, b);
                Assert.IsTrue(GullPerches.IsClaimedBy(offer, a));
                GullPerches.Release(offer, a);
                Assert.IsTrue(GullPerches.TryClaim(Vector2.zero, Vector2.one * 2, b, out _));
                GullPerches.Unregister(offer);
                Assert.IsFalse(GullPerches.IsClaimedBy(offer, b));
                Assert.IsFalse(GullPerches.TryClaim(Vector2.zero, Vector2.one * 2, a, out _));
            }
            finally { GullPerches.Unregister(offer); }
        }

        [Test]
        public void PerchSelection_IsStableById_RegardlessOfRegistrationOrder()
        {
            var first = new Offer { Id = "test.a" };
            var last = new Offer { Id = "test.z" };
            object bird = new object();
            GullPerches.Register(last); GullPerches.Register(first);
            try
            {
                Assert.IsTrue(GullPerches.TryClaim(Vector2.zero, Vector2.one, bird, out var perch));
                Assert.AreSame(first, perch);
            }
            finally { GullPerches.Unregister(first); GullPerches.Unregister(last); }
        }
    }
}
