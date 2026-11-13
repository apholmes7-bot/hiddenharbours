using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using HiddenHarbours.UI;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The S4.5 rule for what the always-on HUD gives up at a helm
    /// (<see cref="HelmHudSuppression"/>) — the owner's "remove any current game UI that obstructs
    /// the new boat UI", as a pure mapping rather than a hand-picked list of hulls.
    ///
    /// <para>Top-level rather than in the UI folder because the second half of the story is the
    /// SHIPPED CONSOLE ASSETS, and reading those needs the Boats module: a rule keyed on the fit is
    /// only worth anything if the fits it keys on are what the tests think they are.</para>
    /// </summary>
    public class HelmHudSuppressionTests
    {
        private static readonly string[] ShippedHelms =
        {
            "Assets/_Project/Data/Boats/Helms/ConsoleSkiffHelm.asset",
            "Assets/_Project/Data/Boats/Helms/SportSkiffHelm.asset",
            "Assets/_Project/Data/Boats/Helms/NoviHelm.asset",
            "Assets/_Project/Data/Boats/Helms/CapeIslanderHelm.asset",
        };

        private static HelmFit Fit(ConsoleRigKind rig, CompassMount compass,
                                   SounderKind sounder = SounderKind.Depth)
            => new HelmFit(rig, sounder, compass, false, false);

        // ---- the two rules -------------------------------------------------------------------------

        [Test]
        public void Ashore_TheClusterIsHidden_Unchanged()
        {
            Assert.That(HelmHudSuppression.NavCluster(false, HelmControlStyle.None, HelmFit.None),
                        Is.EqualTo(NavClusterPlacement.Hidden));
            // Even standing on a dock next to a fitted boat: the cluster is a read about the boat you
            // are ON, and "aboard" is the only thing that decides whether it exists at all (VS-19).
            Assert.That(HelmHudSuppression.NavCluster(false, HelmControlStyle.Lever,
                                                     Fit(ConsoleRigKind.Novi, CompassMount.Dome)),
                        Is.EqualTo(NavClusterPlacement.Hidden));
        }

        [Test]
        public void ATillerOrRowedHull_KeepsTheClusterExactlyWhereItWas()
        {
            // THE NEGATIVE CONTROL. A dory or a punt has no dash and therefore no compass, no
            // set-and-drift and no apparent-wind read of any kind: this cluster is the only one it
            // gets. Suppressing at the helm must never reach it.
            Assert.That(HelmHudSuppression.NavCluster(true, HelmControlStyle.Tiller, HelmFit.None),
                        Is.EqualTo(NavClusterPlacement.BottomCentre));

            // And a lever hull with no console renderer is the same case — the fallback lone-lever
            // card is small and corner-parked, so there is nothing to move out from under.
            Assert.That(HelmHudSuppression.NavCluster(true, HelmControlStyle.Lever, HelmFit.None),
                        Is.EqualTo(NavClusterPlacement.BottomCentre));
        }

        [Test]
        public void ADashWithACompass_HidesTheCluster_ByDuplication()
        {
            foreach (ConsoleRigKind rig in new[] { ConsoleRigKind.Console, ConsoleRigKind.Sport,
                                                   ConsoleRigKind.Novi, ConsoleRigKind.Cape })
            {
                Assert.That(HelmHudSuppression.NavCluster(true, HelmControlStyle.Lever,
                                                         Fit(rig, CompassMount.Dome)),
                            Is.EqualTo(NavClusterPlacement.Hidden), $"{rig} + dome");
            }
            // The flush Ritchie is a compass too — the RULE is "the dash carries one", not "the dash
            // carries the one mount I happened to think of".
            Assert.That(HelmHudSuppression.NavCluster(true, HelmControlStyle.Lever,
                                                     Fit(ConsoleRigKind.Novi, CompassMount.Flush)),
                        Is.EqualTo(NavClusterPlacement.Hidden));
        }

        [Test]
        public void ADashWithNoCompass_MovesTheClusterInsteadOfHidingIt()
        {
            // The overlap rule standing on its own: both are anchored bottom-centre, so the cluster
            // has to leave — but this boat has no other heading read, so it moves, it does not go.
            foreach (ConsoleRigKind rig in new[] { ConsoleRigKind.Console, ConsoleRigKind.Sport,
                                                   ConsoleRigKind.Novi, ConsoleRigKind.Cape })
            {
                Assert.That(HelmHudSuppression.NavCluster(true, HelmControlStyle.Lever,
                                                         Fit(rig, CompassMount.None)),
                            Is.EqualTo(NavClusterPlacement.ClearOfTheDash), $"{rig}, no compass");
            }
        }

        [Test]
        public void TheRuleIsKeyedOnTheFit_NotOnTheHull()
        {
            // Same rig, opposite answers, decided only by what is fitted — which is what makes a
            // bought, swapped or dev-cycled compass move the HUD with no list to maintain.
            Assert.That(HelmHudSuppression.NavCluster(true, HelmControlStyle.Lever,
                                                     Fit(ConsoleRigKind.Cape, CompassMount.Dome)),
                        Is.Not.EqualTo(HelmHudSuppression.NavCluster(true, HelmControlStyle.Lever,
                                                     Fit(ConsoleRigKind.Cape, CompassMount.None))));

            // And the brow's contents are NOT part of it: a sounder is not a compass.
            foreach (SounderKind brow in new[] { SounderKind.None, SounderKind.Depth, SounderKind.Fish })
                Assert.That(HelmHudSuppression.NavCluster(true, HelmControlStyle.Lever,
                                                Fit(ConsoleRigKind.Novi, CompassMount.None, brow)),
                            Is.EqualTo(NavClusterPlacement.ClearOfTheDash), $"brow {brow}");
        }

        // ---- the shipped fleet ---------------------------------------------------------------------

        [Test]
        public void EveryShippedConsole_ShipsACompass_SoTheClusterStandsDownAtEveryShippedDash()
        {
            // The fact the rule is derived against, asserted on the AUTHORED assets rather than trusted
            // from a handoff. Until the owner's 2026-11-13 ruling the two wheelhouses shipped with no
            // compass, and this test pinned that the cluster had to survive there. They now ship a
            // flush compass, so every shipped dash carries its own heading read and the cluster hides
            // at all four. The overlap rule (ClearOfTheDash) is still needed for any console fitted
            // with no compass, and is proven on its own above
            // (ADashWithNoCompass_MovesTheClusterInsteadOfHidingIt).
            var expected = new Dictionary<string, CompassMount>
            {
                { "helm.console_skiff", CompassMount.Dome },
                { "helm.sport_skiff", CompassMount.Dome },
                { "helm.novi", CompassMount.Flush },
                { "helm.cape_islander", CompassMount.Flush },
            };

            var owned = new List<string>();          // a fresh boat: no purchases layered on
            foreach (string path in ShippedHelms)
            {
                var console = UnityEditor.AssetDatabase.LoadAssetAtPath<HelmConsoleDef>(path);
                Assert.IsNotNull(console, $"the shipped console must exist at {path}");
                Assert.IsTrue(expected.ContainsKey(console.Id), $"unexpected helm id {console.Id}");

                HelmFit fit = BoatEquipment.EffectiveFit(console, owned);
                Assert.That(fit.Compass, Is.EqualTo(expected[console.Id]),
                            $"{console.Id}'s authored DefaultCompass moved: re-derive the HUD " +
                            "suppression rule against it before assuming what the cluster does there");

                Assert.That(HelmHudSuppression.NavCluster(true, HelmControlStyle.Lever, fit),
                            Is.EqualTo(NavClusterPlacement.Hidden),
                            $"{console.Id} carries a compass, so the HUD's duplicate stands down");
            }
        }

        // ---- the shared "is a dash up" predicate ---------------------------------------------------

        [Test]
        public void DashIsUp_MatchesTheOverlayHostsOwnGate()
        {
            // ONE predicate, so the HUD can never yield to a dash that is not on screen (or keep
            // obstructing one that is). These are the four combinations HelmOverlayHost distinguishes.
            Assert.IsTrue(HelmHudSuppression.DashIsUp(HelmControlStyle.Lever,
                                                      Fit(ConsoleRigKind.Console, CompassMount.Dome)));
            Assert.IsFalse(HelmHudSuppression.DashIsUp(HelmControlStyle.Lever, HelmFit.None),
                           "a lever hull with no console falls back to the lone lever card");
            Assert.IsFalse(HelmHudSuppression.DashIsUp(HelmControlStyle.Tiller,
                                                       Fit(ConsoleRigKind.Console, CompassMount.Dome)),
                           "a tiller helm draws the tiller, whatever a console def claims");
            Assert.IsFalse(HelmHudSuppression.DashIsUp(HelmControlStyle.None, HelmFit.None));
        }
    }
}
