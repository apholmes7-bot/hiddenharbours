using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Environment;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// Guards the runtime tide-reveal's PURE colour mapping (<see cref="TidalFlatVisual.CellColor"/>): the
    /// greybox visual that makes the St Peters falling tide unmistakable. The headline bug was that the
    /// reveal read FROZEN — these pin that the mapping actually MOVES with the water level (sand when
    /// exposed → blue as it covers), and that it is deterministic (a pure function of water level + authored
    /// elevation; no RNG, no time except via the level — CLAUDE.md rule 5), so the picture can never
    /// disagree with the walkability/boat-cross gate that reads the same number.
    /// </summary>
    public class TidalFlatVisualTests
    {
        // A clear, well-separated greybox palette for the assertions.
        static readonly Color Dry     = new Color(0.84f, 0.76f, 0.55f, 1f);
        static readonly Color Wet     = new Color(0.62f, 0.60f, 0.50f, 1f);
        static readonly Color Shallow = new Color(0.32f, 0.52f, 0.56f, 0.92f);
        static readonly Color Deep    = new Color(0.10f, 0.22f, 0.32f, 0.96f);
        const float DeepDepth = 2.5f;
        const float WetBand   = 0.8f;

        static Color C(float waterLevel, float terrain)
            => TidalFlatVisual.CellColor(waterLevel, terrain, Dry, Wet, Shallow, Deep, DeepDepth, WetBand);

        // ---- exposed ground reads as sand; submerged ground reads as water ---------------------

        [Test]
        public void ExposedGround_WellAboveWaterline_IsFullyDrySand()
        {
            // Ground 5 m above the water (>> wet band) → fully dry sand.
            Assert.AreEqual(Dry, C(waterLevel: 0f, terrain: 5f));
        }

        [Test]
        public void GroundRightAtWaterline_IsWetSand_TheGlisteningRevealEdge()
        {
            // depth == 0 → exposed by zero margin → the just-bared wet colour.
            Assert.AreEqual(Wet, C(waterLevel: 2f, terrain: 2f));
        }

        [Test]
        public void SubmergedShallow_IsShallowWater()
        {
            // 0.1 m of water over the ground → essentially the shallow colour.
            Color c = C(waterLevel: 2.1f, terrain: 2f);
            Assert.That(ChannelDist(c, Shallow), Is.LessThan(ChannelDist(c, Deep)),
                "a thin water column reads as shallow, not deep");
        }

        [Test]
        public void SubmergedDeep_AtOrBeyondDeepDepth_IsDeepWater()
        {
            // >= DeepDepth of water → fully deep.
            Assert.AreEqual(Deep, C(waterLevel: 2f + DeepDepth, terrain: 2f));
            Assert.AreEqual(Deep, C(waterLevel: 2f + DeepDepth + 3f, terrain: 2f)); // clamps, never past deep
        }

        // ---- THE REVEAL: a FIXED sandbar cell walks dry → wet → shallow → deep as the tide RISES,
        //      and back as it falls. This is the "it actually moves" guard (the bug was it didn't). ----

        [Test]
        public void FallingTide_MovesAFixedCell_FromBlueToSand()
        {
            const float sandbarCrest = 1.6f;   // the St Peters bar crest (mirrors the builder constant)

            // High water (+2.5 m): covered → water (not sand).
            Color high = C(waterLevel: 2.5f, terrain: sandbarCrest);
            Assert.IsTrue(IsWaterish(high), "at high water the bar cell reads as water");

            // Mid-fall, water just below the crest (+1.2 m): bared → sand.
            Color mid = C(waterLevel: 1.2f, terrain: sandbarCrest);
            Assert.IsTrue(IsSandish(mid), "as the tide falls past the crest the cell bares to sand");

            // Low water (−2.0 m): fully dry sand.
            Color low = C(waterLevel: -2.0f, terrain: sandbarCrest);
            Assert.AreEqual(Dry, low, "at low water the bar is fully dry");

            // The transition is real, not frozen: high != low.
            Assert.AreNotEqual(high, low, "the cell's colour MUST change across the tide (the bug: it didn't)");
        }

        [Test]
        public void DeeperWater_IsNeverLighterThanShallower_OverASubmergedCell()
        {
            const float ground = 0f;
            Color prev = C(0.01f, ground);
            for (float wl = 0.25f; wl <= DeepDepth + 1f; wl += 0.25f)
            {
                Color cur = C(wl, ground);
                // Monotone toward deep: each step is at least as close to Deep as the previous.
                Assert.That(ChannelDist(cur, Deep), Is.LessThanOrEqualTo(ChannelDist(prev, Deep) + 1e-4f),
                    $"colour should march toward deep as depth grows (wl={wl})");
                prev = cur;
            }
        }

        // ---- determinism (rule 5): same inputs → identical colour, every call --------------------

        [Test]
        public void CellColor_IsDeterministic_ForIdenticalInputs()
        {
            for (int i = 0; i < 5; i++)
                Assert.AreEqual(C(1.234f, 0.567f), C(1.234f, 0.567f),
                    "the mapping is a pure function — no RNG, no hidden state");
        }

        // ---- helpers -----------------------------------------------------------------------------

        static float ChannelDist(Color a, Color b)
            => Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b);

        static bool IsSandish(Color c) => ChannelDist(c, Dry) + ChannelDist(c, Wet)
                                        < ChannelDist(c, Shallow) + ChannelDist(c, Deep);

        static bool IsWaterish(Color c) => !IsSandish(c);
    }
}
