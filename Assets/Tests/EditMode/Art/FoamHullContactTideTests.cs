using HiddenHarbours.Art;
using HiddenHarbours.Core;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// The foam parts round a hull's PICTURE, not her plan outline. Her picture rides the tide up and
    /// down the screen (<see cref="IHullPresence.PictureRise"/>) while her plan stays put; a contact taken
    /// from the plan alone left a hull-shaped wake in the foam where she would lie at the baked tide —
    /// a faint second boat above her at low water.
    /// </summary>
    public sealed class FoamHullContactTideTests
    {
        private sealed class RidingHull : IHullPresence
        {
            public HullFootprint Footprint { get; set; }
            public float PictureRise { get; set; }
        }

        // The starter dory's size (Data/Boats/Dory.asset), lying head to the north.
        const float DoryLength = 4.5f, DoryHalfBeam = 0.85f;
        static readonly Vector2 Plan = new Vector2(66f, 57.5f);

        [SetUp] public void SetUp() => HullPresences.Clear();
        [TearDown] public void TearDown() => HullPresences.Clear();

        [Test] public void ShownCenter_CarriesThePlanByTheRise_SnappedToWholeFoamCells()
        {
            float cell = FoamBuffer.CellSize;
            Vector2 low = FoamTransportContacts.ShownCenter(Plan, -1.6f);
            Assert.AreEqual(Plan.x, low.x, "the tide moves her picture up and down the screen only");
            Assert.AreEqual(Plan.y + Mathf.Round(-1.6f / cell) * cell, low.y, 1e-5f);
            Assert.AreEqual(Plan, FoamTransportContacts.ShownCenter(Plan, 0f), "at the baked tide the plan is the picture");
            Assert.AreEqual(Plan, FoamTransportContacts.ShownCenter(Plan, float.NaN), "a bad reading is no rise, never a poisoned field");
            Assert.AreEqual(Plan, FoamTransportContacts.ShownCenter(Plan, float.PositiveInfinity));
        }

        [Test] public void ShownCenter_IsStillWhileTheTideCreepsWithinACell()
        {
            // A still hull's contact must not change every frame as the tide moves a millimetre, or the
            // mask would rebuild every frame.
            Vector2 a = FoamTransportContacts.ShownCenter(Plan, -1.600f);
            Vector2 b = FoamTransportContacts.ShownCenter(Plan, -1.601f);
            Assert.AreEqual(a, b);
        }

        [Test] public void Capture_PutsAHullContactWhereHerPictureIs_NotAtHerPlan()
        {
            var hull = new RidingHull
            {
                Footprint = HullFootprint.FromHeading(Plan, 0f, DoryLength, DoryHalfBeam),
                PictureRise = -1.6f,   // the ebb: her picture is drawn below her plan line
            };
            HullPresences.Register(hull);

            var into = new FoamTransportContacts.Shape[FoamTransport.MaxContacts];
            var window = new Rect(Plan - Vector2.one * 16f, Vector2.one * 32f);
            Assert.IsTrue(FoamTransportContacts.Capture(into, window, 0f, out int count, out var fault), fault.ToString());

            int found = -1;
            for (int i = 0; i < count; i++) if (into[i].Key == 0 && Mathf.Abs(into[i].HalfSize.x - DoryLength * 0.5f) < 1e-4f) found = i;
            Assert.GreaterOrEqual(found, 0, "the registered hull is a foam contact");
            Vector2 expected = FoamTransportContacts.ShownCenter(Plan, hull.PictureRise);
            Assert.AreEqual(expected.x, into[found].Center.x, 1e-5f);
            Assert.AreEqual(expected.y, into[found].Center.y, 1e-5f,
                "the foam parts round her picture; at her plan it draws a ghost of her above the real boat");
            Assert.Less(into[found].Center.y, Plan.y - 1f);
        }
    }
}
