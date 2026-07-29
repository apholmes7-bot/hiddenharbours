using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// **ONE SEA, ONE SETTINGS** (ADR 0018 §(5) — the GameConfig unification).
    ///
    /// <para>Eight components used to carry their own <c>WaveFieldSettings</c>: the water shader's
    /// bridge, the hull rocking, the seakeeping forces, the wake, the buoys, the trap haul and the
    /// drift weed. Every one of them was annotated "keep identical to the others" — which is a
    /// comment, not a mechanism. Tuning any one of them meant the hull rode a sea the shader was not
    /// drawing, the exact failure ADR 0018 exists to prevent, and the ADR 0027 spectrum's
    /// <c>SpectrumBlend</c> made it a one-slider mistake.</para>
    ///
    /// <para>The fix is that there is now nothing to keep in step: the settings live on
    /// <c>GameConfig</c> and reach every consumer through <see cref="GameServices.WaveField"/>. The
    /// most important test in this file is <see cref="NoComponentCarriesItsOwnCopyOfTheWaveSettings"/>,
    /// which scans the assemblies so the copies cannot come back.</para>
    /// </summary>
    public class WaveFieldSettingsUnificationTests
    {
        private GameConfig _config;

        [TearDown]
        public void ClearConfig()
        {
            GameServices.Config = null;
            if (_config != null) UnityEngine.Object.DestroyImmediate(_config);
            _config = null;
        }

        // ===== (1) THE STRUCTURAL GUARD ===================================================================

        [Test]
        public void NoComponentCarriesItsOwnCopyOfTheWaveSettings()
        {
            // THE TEST THAT KEEPS THE UNIFICATION UNIFIED. A component that declares its own
            // WaveFieldSettings field is, by construction, a second sea — and it fails silently,
            // because both copies start at Default and only diverge once someone tunes one.
            var offenders = new List<string>();

            foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (!asm.GetName().Name.StartsWith("HiddenHarbours", StringComparison.Ordinal)) continue;
                if (asm.GetName().Name.Contains("Tests")) continue;

                foreach (Type type in SafeTypes(asm))
                {
                    if (type == typeof(GameConfig)) continue;   // the ONE legitimate home

                    foreach (FieldInfo f in type.GetFields(BindingFlags.Instance | BindingFlags.Static
                                                           | BindingFlags.Public | BindingFlags.NonPublic
                                                           | BindingFlags.DeclaredOnly))
                    {
                        if (f.FieldType == typeof(WaveFieldSettings) ||
                            f.FieldType == typeof(WaveFieldAnimatorSettings))
                            offenders.Add($"{type.FullName}.{f.Name} ({f.FieldType.Name})");
                    }
                }
            }

            Assert.IsEmpty(offenders,
                "these types keep their own copy of the shared wave settings — read " +
                "GameServices.WaveField / .WaveFieldAnimator instead, or the sea forks the moment " +
                "anyone tunes one of them:\n  " + string.Join("\n  ", offenders));
        }

        private static IEnumerable<Type> SafeTypes(Assembly asm)
        {
            try { return asm.GetTypes(); }
            catch (ReflectionTypeLoadException e) { return e.Types.Where(t => t != null); }
        }

        // ===== (2) THE ACCESSOR CONTRACT ==================================================================

        [Test]
        public void WithNoConfigWired_TheAccessorsFallBackToTheReferenceTuning()
        {
            // EditMode, a bare art scene, every test rig and any scene whose GameRoot has no config
            // run through here. The fallback is what stops the unification from being a new way for
            // the sea to go silent.
            GameServices.Config = null;

            Assert.AreEqual(WaveFieldSettings.Default.DominantWavelengthBase,
                            GameServices.WaveField.DominantWavelengthBase, 1e-6f);
            Assert.AreEqual(WaveFieldSettings.Default.PrimaryAmplitude,
                            GameServices.WaveField.PrimaryAmplitude, 1e-6f);
            Assert.AreEqual(0f, GameServices.WaveField.SpectrumBlend, 1e-6f,
                "unwired must mean the SHIPPED sea — spectrum off, not a surprise");
            Assert.AreEqual(WaveFieldAnimatorSettings.Default.ParameterSmoothingSeconds,
                            GameServices.WaveFieldAnimator.ParameterSmoothingSeconds, 1e-6f);
        }

        [Test]
        public void WithAConfigWired_EveryConsumerReadsTheOwnersValues()
        {
            _config = ScriptableObject.CreateInstance<GameConfig>();
            _config.WaveField.PrimaryAmplitude = 2.75f;
            _config.WaveField.SpectrumBlend = 1f;
            _config.WaveFieldAnimator.ParameterSmoothingSeconds = 9.5f;
            GameServices.Config = _config;

            Assert.AreEqual(2.75f, GameServices.WaveField.PrimaryAmplitude, 1e-6f);
            Assert.AreEqual(1f, GameServices.WaveField.SpectrumBlend, 1e-6f);
            Assert.AreEqual(9.5f, GameServices.WaveFieldAnimator.ParameterSmoothingSeconds, 1e-6f);
        }

        [Test]
        public void TheOwnersValuesReachTheDERIVATION_NotJustTheAccessor()
        {
            // The accessor returning the right numbers proves nothing on its own — what matters is
            // that the field the hull rides and the shader draws actually changes. Tuning through
            // GameConfig must move the trains.
            _config = ScriptableObject.CreateInstance<GameConfig>();
            GameServices.Config = _config;

            var wind = new Vector2(8f, 0f);
            WaveTrains shipped = WaveMath.TrainsFrom(wind, 0.7f, GameServices.WaveField);
            Assert.AreEqual(4, shipped.Count, "config at its defaults = the shipped 4-train sea");

            _config.WaveField.SpectrumBlend = 1f;
            WaveTrains spectral = WaveMath.TrainsFrom(wind, 0.7f, GameServices.WaveField);
            Assert.AreEqual(WaveTrains.MaxTrains, spectral.Count,
                "turning the owner's spectrum dial must reach the derivation — if this fails the " +
                "config is decorative");

            // And it is LIVE: no consumer cached the policy at Awake, so dragging the slider during
            // play moves the sea rather than requiring a restart.
            _config.WaveField.SpectrumBlend = 0f;
            Assert.AreEqual(4, WaveMath.TrainsFrom(wind, 0.7f, GameServices.WaveField).Count,
                "and dialling it back down must take effect immediately too");
        }

        [Test]
        public void ADestroyedConfig_FallsBackInsteadOfThrowing()
        {
            // ⚠️ THE UNITY FAKE-NULL TRAP, pinned. GameConfig is a UnityEngine.Object, so `?.` and
            // `??` bypass its overloaded == and would treat a DESTROYED asset as alive — compile-clean,
            // runtime-red, and only on domain reload or scene teardown. The accessor uses `!= null`
            // precisely so this test passes.
            _config = ScriptableObject.CreateInstance<GameConfig>();
            _config.WaveField.PrimaryAmplitude = 3.5f;
            GameServices.Config = _config;
            Assert.AreEqual(3.5f, GameServices.WaveField.PrimaryAmplitude, 1e-6f, "sanity: alive");

            UnityEngine.Object.DestroyImmediate(_config);

            Assert.DoesNotThrow(() => { var _ = GameServices.WaveField; },
                "a destroyed config must not throw — this is what `??` would do here");
            Assert.AreEqual(WaveFieldSettings.Default.PrimaryAmplitude,
                            GameServices.WaveField.PrimaryAmplitude, 1e-6f,
                "a destroyed config falls back to the reference tuning");
            _config = null;   // already destroyed; keep TearDown from double-destroying
        }

        [Test]
        public void Reset_ClearsTheConfig_SoTestsCannotLeakIntoEachOther()
        {
            _config = ScriptableObject.CreateInstance<GameConfig>();
            _config.WaveField.PrimaryAmplitude = 4.25f;
            GameServices.Config = _config;

            GameServices.Reset();

            Assert.IsNull(GameServices.Config, "Reset must clear the config with the rest");
            Assert.AreEqual(WaveFieldSettings.Default.PrimaryAmplitude,
                            GameServices.WaveField.PrimaryAmplitude, 1e-6f);
        }
    }
}
