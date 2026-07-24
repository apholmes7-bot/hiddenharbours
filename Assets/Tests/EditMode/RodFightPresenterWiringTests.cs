#if UNITY_EDITOR
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Boats;
using HiddenHarbours.Fishing;
using HiddenHarbours.Player;
using HiddenHarbours.App.Editor;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// THE WIRING REGRESSION for the rod-fishing presentation seam — the owner's statue-still playtest
    /// was a wiring-shaped failure that was SILENT BY DESIGN (null-safe greybox), so this suite makes it
    /// impossible to repeat quietly: it runs the REAL <see cref="PersistentCoreBuilder"/> against the
    /// REAL committed sheets and asserts every fight-pose sprite array on the player is NON-EMPTY with
    /// its exact committed cell count, and that the <see cref="RodFightPresenter"/> is present and fed.
    /// A committed sheet that wires zero frames — a re-slice, a renamed sprite, a loader regression —
    /// FAILS the suite instead of shipping an invisible fight.
    /// </summary>
    public class RodFightPresenterWiringTests
    {
        private readonly System.Collections.Generic.List<Object> _spawned = new();
        private PersistentCoreBuilder.Handle _core;

        [TearDown]
        public void TearDown()
        {
            foreach (var go in new[]
                     {
                         _core.ServicesRoot, _core.CameraGo, _core.PlayerGo, _core.DoryGo, _core.GaugeGo,
                         _core.SwitcherGo, _core.LoaderGo,
                         _core.Coordinator != null ? _core.Coordinator.gameObject : null,
                     })
                if (go != null) Object.DestroyImmediate(go);
            _core = default;
            foreach (var o in _spawned)
                if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
            GameServices.Reset();
        }

        private void BuildCore(FishSpeciesDef[] regionFish)
        {
            GameServices.Reset();
            var config = ScriptableObject.CreateInstance<GameConfig>();
            _spawned.Add(config);
            var dory = ScriptableObject.CreateInstance<BoatHullDef>();
            dory.Id = "boat.dory";
            dory.DisplayName = "Dory";
            dory.HoldUnits = 6;
            _spawned.Add(dory);

            _core = PersistentCoreBuilder.Build(new PersistentCoreBuilder.Params
            {
                Config = config,
                StartDory = dory,
                RegionFish = regionFish,
                CameraBackground = Color.black,
                PlayerStartPos = Vector3.zero,
                BoatMooredPos = new Vector3(0f, -10f, 0f),
                CurrentSceneName = "Test",
                TideMean = 0f, TideAmplitude = 1f, TidePhaseHours = 0f,
            });
        }

        private FishSpeciesDef MakeDef(string id)
        {
            var f = ScriptableObject.CreateInstance<FishSpeciesDef>();
            f.Id = id;
            _spawned.Add(f);
            return f;
        }

        // ---- the loader itself, against every committed fight sheet -------------------------------

        // The committed kit's exact shapes: 8 directions × frames. A drop in ANY of these counts means
        // a sheet re-import lost cells (the texture-size-cap trap) or a re-slice renamed them.
        private static readonly (string sheet, int cells)[] FisherSheets =
        {
            ("Fisher_bite", 48), ("Fisher_strike", 48), ("Fisher_reel", 96), ("Fisher_land", 96),
            ("Fisher_hold", 48), ("Fisher_castBack", 48), ("Fisher_castRelease", 64),
        };

        [Test]
        public void LoadIsoDirFrames_LoadsEveryCommittedFightSheet_Whole()
        {
            foreach ((string sheet, int cells) in FisherSheets)
            {
                Sprite[] frames = PersistentCoreBuilder.LoadIsoDirFrames(
                    $"Assets/_Project/Art/Characters/Iso/{sheet}.png");
                Assert.AreEqual(cells, frames.Length,
                    $"{sheet}: the committed sheet slices to {cells} cells — a different count means " +
                    "the import/slicing broke and every pose on it would go silently inert");
                foreach (Sprite s in frames)
                    Assert.IsNotNull(s, $"{sheet}: no holes in the loaded set");
            }
        }

        [Test]
        public void LoadIsoDirFramesChecked_ScreamsOnAnUnslicedExistingSheet_QuietOnAMissingFile()
        {
            // SplashBurst.png EXISTS but is not an 8-direction d/f sheet — the checked loader must
            // log an ERROR (the loud regression the statue bug demanded) and still return empty.
            UnityEngine.TestTools.LogAssert.Expect(LogType.Error,
                new System.Text.RegularExpressions.Regex("wired ZERO frames"));
            Sprite[] bad = PersistentCoreBuilder.LoadIsoDirFramesChecked(
                "Assets/_Project/Art/Fishing/SplashBurst.png");
            Assert.IsEmpty(bad);

            // A file that simply is not there stays a quiet no-op (the pre-art greybox posture).
            Sprite[] missing = PersistentCoreBuilder.LoadIsoDirFramesChecked(
                "Assets/_Project/Art/Characters/Iso/Fisher_does_not_exist.png");
            Assert.IsEmpty(missing);
        }

        // ---- the builder wiring, end to end ---------------------------------------------------------

        [Test]
        public void TheBuilder_WiresEveryFightPoseArray_NonEmpty_FromTheCommittedArt()
        {
            BuildCore(new[] { MakeDef("fish.atlantic_cod"), MakeDef("fish.haddock"), MakeDef("fish.mackerel") });

            var anim = _core.PlayerGo.GetComponent<PlayerFishingAnimator>();
            Assert.IsNotNull(anim, "the fight animator rides the player");

            var so = new SerializedObject(anim);
            foreach ((string field, int cells) in new (string, int)[]
                     {
                         ("_biteFrames", 48), ("_strikeFrames", 48), ("_reelFrames", 96),
                         ("_landFrames", 96), ("_holdFrames", 48), ("_castBackFrames", 48),
                         ("_castReleaseFrames", 64),
                     })
            {
                SerializedProperty prop = so.FindProperty(field);
                Assert.IsNotNull(prop, $"{field} exists on the animator");
                Assert.AreEqual(cells, prop.arraySize,
                    $"{field}: the builder must wire the committed sheet WHOLE — zero (or partial) " +
                    "frames is the silent statue-still failure and must never pass a build again");
                for (int i = 0; i < prop.arraySize; i++)
                    Assert.IsNotNull(prop.GetArrayElementAtIndex(i).objectReferenceValue,
                        $"{field}[{i}]: a null cell would freeze the fisher mid-fight");
            }
        }

        [Test]
        public void TheBuilder_WiresThePresenter_RodBobberFishAndHands()
        {
            BuildCore(new[] { MakeDef("fish.atlantic_cod"), MakeDef("fish.haddock"), MakeDef("fish.mackerel") });

            var presenter = _core.PlayerGo.GetComponent<RodFightPresenter>();
            Assert.IsNotNull(presenter, "the rod-fight presenter rides the player");

            var so = new SerializedObject(presenter);
            Assert.AreEqual(7, so.FindProperty("_rodStates").arraySize, "hold..castRelease, all seven");
            Assert.AreEqual(4, so.FindProperty("_bobberStates").arraySize, "float/nibble/strike/fly");
            Assert.AreEqual(3, so.FindProperty("_species").arraySize,
                "cod + haddock + mackerel key to the roster (pollock waits for its species def)");
            Assert.Greater(so.FindProperty("_rodBehindDirs").arraySize, 0, "the kit's layering rule");
            Assert.Greater(so.FindProperty("_landHandMid").arraySize, 0, "the held-catch hand anchors");
            Assert.Greater(so.FindProperty("_splashFrames").arraySize, 0, "the splash flourish frames");
            Assert.IsNotNull(so.FindProperty("_rippleSprite").objectReferenceValue, "the sink-ring art");

            // Spot-check the deepest wiring: rod state 0 (hold) carries sheet + BOTH anchor tables.
            SerializedProperty hold = so.FindProperty("_rodStates").GetArrayElementAtIndex(0);
            Assert.AreEqual("hold", hold.FindPropertyRelative("State").stringValue);
            int holdCells = hold.FindPropertyRelative("Frames").arraySize;
            Assert.AreEqual(48, holdCells, "Rod_cane_hold slices to 8×6");
            Assert.AreEqual(holdCells, hold.FindPropertyRelative("GripOffsets").arraySize);
            Assert.AreEqual(holdCells, hold.FindPropertyRelative("TipOffsets").arraySize);
        }

        [Test]
        public void TheBuilder_MutesTheGaugesPreFightText_OnlyBecauseTheKitWired()
        {
            BuildCore(new[] { MakeDef("fish.atlantic_cod") });
            var gauge = _core.GaugeGo.GetComponent<RodGaugeView>();
            Assert.IsNotNull(gauge);
            var so = new SerializedObject(gauge);
            Assert.IsTrue(so.FindProperty("_muteDiegeticText").boolValue,
                "with the rod + bobber art wired, the gauge stops double-captioning the diegetic tells");
        }
    }
}
#endif
