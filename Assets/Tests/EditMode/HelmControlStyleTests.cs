using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Boats;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The tiller-vs-lever decision and the per-hull detent resolution (ADR 0025 S1) — the PURE rules
    /// of <see cref="HelmControlRelay"/>, EditMode-tested without a scene (registration itself is
    /// enable-lifetime and EditMode never fires OnEnable — that half is covered in PlayMode,
    /// <c>HelmControlPlayTests</c>).
    /// </summary>
    public class HelmControlStyleTests
    {
        private BoatHullDef Hull(PropulsionType propulsion, HelmConsoleDef helm = null,
                                 int aheadOverride = 0, int asternOverride = 0)
        {
            var h = ScriptableObject.CreateInstance<BoatHullDef>();
            h.Propulsion = propulsion;
            h.Helm = helm;
            h.HelmAheadNotches = aheadOverride;
            h.HelmAsternNotches = asternOverride;
            return h;
        }

        [Test]
        public void StyleFor_OarsHull_ShowsNothing()
        {
            // The diegetic rule: a bare rowed dory carries no instrument.
            Assert.That(HelmControlRelay.StyleFor(null), Is.EqualTo(HelmControlStyle.None));
            Assert.That(HelmControlRelay.StyleFor(Hull(PropulsionType.Oars)),
                        Is.EqualTo(HelmControlStyle.None));
        }

        [Test]
        public void StyleFor_EngineHull_WithoutConsole_IsTheTiller()
        {
            // A motor earns one tiller (dory outboard, the punts).
            Assert.That(HelmControlRelay.StyleFor(Hull(PropulsionType.Engine)),
                        Is.EqualTo(HelmControlStyle.Tiller));
        }

        [Test]
        public void StyleFor_EngineHull_WithConsole_IsTheLever()
        {
            var console = ScriptableObject.CreateInstance<HelmConsoleDef>();
            Assert.That(HelmControlRelay.StyleFor(Hull(PropulsionType.Engine, console)),
                        Is.EqualTo(HelmControlStyle.Lever));
        }

        [Test]
        public void EffectiveNotches_HullOverrideWins_ZeroFallsBackToConfig()
        {
            HelmThrottleSettings cfg = HelmThrottleSettings.Default;   // 4 / 2

            HelmControlRelay.EffectiveNotches(Hull(PropulsionType.Engine), in cfg,
                                              out int ahead, out int astern);
            Assert.That(ahead, Is.EqualTo(cfg.AheadNotches), "0 = config default");
            Assert.That(astern, Is.EqualTo(cfg.AsternNotches));

            HelmControlRelay.EffectiveNotches(Hull(PropulsionType.Engine, null, 3, 1), in cfg,
                                              out ahead, out astern);
            Assert.That(ahead, Is.EqualTo(3), "the hull's own coarser tiller ladder");
            Assert.That(astern, Is.EqualTo(1));

            HelmControlRelay.EffectiveNotches(null, in cfg, out ahead, out astern);
            Assert.That(ahead, Is.EqualTo(cfg.AheadNotches), "null hull → config");
        }
    }
}
