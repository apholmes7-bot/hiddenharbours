using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>THE FENCE IS PROVED ON THE ALGORITHM ELSEWHERE; THIS PROVES IT ON THE SHIPPED DATA.</b>
    ///
    /// <para><see cref="CharacterSkinPoseTests"/> builds synthetic clips and shows that
    /// <c>BuildHonestFrameMap</c> holds the previous honest frame, leads in from the first, and
    /// reports a wholly poisoned clip instead of hiding it. That is the ALGORITHM, and it would pass
    /// just as happily against a def full of blow-ups. Whether the asset the game actually loads is
    /// clean is a different question, and only the committed def can answer it.</para>
    ///
    /// <para><b>Two invariants, the narrow one first.</b> Every state the map can NAME must be entirely
    /// honest: a rig drop that re-poisons <c>walk</c> reds that on the next CI run, naming the key the
    /// player would have reached. Then EVERY clip the def carries must be honest too, reachable or not.
    /// The second is a moved premise, said out loud: this class used to leave the four <c>mount*</c>
    /// clips out on purpose, because their boots stepped past the fence (18 of fisher's 64 mount frames
    /// on 40f4656f's rigs) and no state key reached them. Job 3 of the rig 7 follow-up re-authored
    /// those boots inside it, and a clip that is unreachable today is one state-map entry away from the
    /// player, so the fence now holds for all of them. Neither test pins a frame number or an
    /// excursion: the fence (<see cref="CharacterSkinPose.FenceMetres"/>) is a blow-up bar, and
    /// everything inside it stays the art-director's to change in <c>docs/art/rigs/**</c>.</para>
    ///
    /// <para>The reachable set is ENUMERATED FROM THE MAP rather than spelled out below. A stance
    /// that later earns its own ANIMS row joins this guard the moment it is mapped, instead of
    /// quietly escaping a hand-written list that nobody remembered to extend.</para>
    /// </summary>
    public sealed class CharacterSkinDefContentTests
    {
        const string AssetPath = "Assets/_Project/Data/Characters/Skin/fisher.asset";
        const string ExpectedId = "charskin.fisher";

        static CharacterSkinDef Load()
        {
            var def = AssetDatabase.LoadAssetAtPath<CharacterSkinDef>(AssetPath);
            Assert.IsNotNull(def,
                $"no CharacterSkinDef at {AssetPath}. The player preset is BAKED by " +
                "CharacterSkinAssetBaker and COMMITTED — it is content, not a build product, " +
                "because a machine without the rig kit must still be able to draw her.");
            return def;
        }

        static SortedSet<string> StatesTheMapCanName()
        {
            var keys = new SortedSet<string>(StringComparer.Ordinal);
            foreach (CharacterStance stance in Enum.GetValues(typeof(CharacterStance)))
                foreach (CharacterGait gait in Enum.GetValues(typeof(CharacterGait)))
                    keys.Add(CharacterSkinStateMap.StateKeyFor(stance, gait));
            return keys;
        }

        [Test]
        public void ThePlayerPresetIsCommittedUnderItsStableId()
        {
            CharacterSkinDef def = Load();

            Assert.AreEqual(ExpectedId, def.Id,
                "rule 2: def ids are append-only and STABLE. Renaming this one silently unbinds " +
                "every reference that resolves by id rather than by guid.");
            Assert.IsTrue(def.IsUsable(),
                "the baker refuses to write an unusable def, so an unusable one on disk means the " +
                "file was edited by hand or a merge resolved it wrongly.");
            Assert.IsNotNull(def.BindMesh,
                "the bind mesh is a SUB-ASSET of this file; a null one means the .asset was " +
                "committed without it and the figure has nothing to skin.");
            Assert.Greater(def.BoneCount, 0);
        }

        [Test]
        public void EveryStateTheMapCanNameIsBakedAndEntirelyInsideTheFence()
        {
            CharacterSkinDef def = Load();
            SortedSet<string> reachable = StatesTheMapCanName();

            Assert.IsNotEmpty(reachable,
                "the map named nothing at all — StateKeyFor has stopped returning state keys and " +
                "every assertion below would pass vacuously.");

            foreach (string key in reachable)
            {
                Assert.IsTrue(def.TryGetClip(key, out CharacterSkinDef.SkinClip clip),
                    $"the state map can ask for '{key}' and the committed bake does not carry it. " +
                    "The presenter would fall back to a shorter clip and draw the wrong body " +
                    "silently — see CharacterSkinStateMap.Resolve's fellBack.");

                CharacterSkinPose.BuildHonestFrameMap(
                    clip, def.BoneCount, CharacterSkinPose.FenceMetres, out int fenced);

                Assert.AreEqual(0, fenced,
                    $"'{key}' is reachable from the state map and {fenced} of its {clip.FrameCount} " +
                    "frames are past the fence. A reachable clip must be honest end to end: the " +
                    "fence exists so a POISONED clip cannot be drawn, not so a poisoned clip can " +
                    "be shipped and quietly patched at runtime.");
            }
        }

        /// <summary>
        /// <b>Every clip, not only the reachable ones.</b> On the def committed at 40f4656f this names
        /// <c>mountUp</c>, <c>mountDown</c>, <c>mountCab</c> and <c>mountCabDown</c>; after the re-bake of
        /// job 3's boots it names nothing.
        /// </summary>
        [Test]
        public void EveryClipTheDefCarriesIsEntirelyInsideTheFence()
        {
            CharacterSkinDef def = Load();
            Assert.IsNotEmpty(def.Clips,
                "the def carries no clips, and every assertion below would pass vacuously.");

            var past = new List<string>();
            foreach (CharacterSkinDef.SkinClip clip in def.Clips)
            {
                CharacterSkinPose.BuildHonestFrameMap(
                    clip, def.BoneCount, CharacterSkinPose.FenceMetres, out int fenced);
                if (fenced > 0) past.Add($"'{clip.StateKey}' {fenced} of {clip.FrameCount}");
            }

            Assert.IsEmpty(past,
                $"{past.Count} of the def's {def.Clips.Length} clips have frames past the " +
                $"{CharacterSkinPose.FenceMetres} m fence: {string.Join(", ", past)}. The fence hides such " +
                "frames at runtime, which is a crash guard and not a licence: every clip is one state-map " +
                "entry away from the player. If the rigs already fix these clips the committed def is " +
                "STALE, so re-bake the player (\"Bake character SKIN (the player, ADR 0044 d)\"); if they " +
                "do not, the fix is in the rig, never a wider fence.");
        }

        [Test]
        public void TheDefDeclaresOnlyStatesItActuallyCarries()
        {
            CharacterSkinDef def = Load();

            Assert.IsNotEmpty(def.MeshStates,
                "MeshStates is the ADR 0041 per-state switch and it is authored ON THIS ASSET. An " +
                "empty list means the presenter can never draw anything, which would make the " +
                "toggle-1 plate in this PR impossible to shoot and the mesh path unverifiable.");

            foreach (string state in def.MeshStates)
                Assert.IsTrue(def.TryGetClip(state, out _),
                    $"MeshStates turns on '{state}' and the bake carries no such clip. A switch " +
                    "for a state that does not exist reads as coverage and delivers nothing.");
        }
    }
}
