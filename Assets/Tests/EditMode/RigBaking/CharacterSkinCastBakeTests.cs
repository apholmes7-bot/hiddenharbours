using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Tools.RigBaking;

namespace HiddenHarbours.Tests.RigBaking
{
    /// <summary>
    /// <b>THE CAST BAKE: WHO IT BAKES, WHETHER THEY FIT, AND WHAT IT SWITCHES ON</b>
    /// (ADR 0044, amendment 2026-09-17).
    ///
    /// <para><see cref="CharacterSkinAssetBaker.BakeCastCli"/> bakes the player and the nine NPC presets
    /// in one headless run and fails loud on the first that cannot bake. Everything it could fail on
    /// without Unity is asserted here first. The rig must know every preset. Every composed material
    /// table must fit the facet pass's ramp slots, measured through the bake's OWN bind table (the
    /// list <c>Compose</c> refuses on), with every failing preset named. Every preset must have an art
    /// def to link. The switch a fresh def is created with must be the one the player already ships.</para>
    ///
    /// <para><b>The controls are part of the measurement.</b> The player's composed table is read back
    /// against its COMMITTED def, so the extraction here is the one the bake runs. The same extraction
    /// with the face held back reads 17 for the deckboss and the packer. That proves the ramp guard can
    /// go red, and that the composed face is what brings those two under. Once
    /// <see cref="CharacterSkinAssetBaker.LiveRig"/> names rig 9 (the character intake's Phase B,
    /// 2026-09-26), the committed def is rig 9's, so the control reads it back against rig 9's
    /// extraction instead, and the rig 7 tables here stay a measurement of rig 7.</para>
    ///
    /// <para>No asset is written. The one guard that needs Phase C's assets on disk is
    /// <see cref="EveryCastArtDefLinksItsOwnUsableSkin"/>, and it is red until they land, by design.</para>
    /// </summary>
    public class CharacterSkinCastBakeTests
    {
        /// <summary>The ten, named rather than read back: if the table quietly loses one, this fails
        /// instead of guarding nine.</summary>
        static readonly (string preset, string stem)[] Cast =
        {
            ("fisher", "Fisher"), ("ginny", "Ginny"), ("skipper", "Skipper"), ("nan", "Nan"),
            ("deckboss", "DeckBoss"), ("packer", "Packer"), ("cutter", "Cutter"),
            ("hand", "Hand"), ("boy", "Boy"), ("girl", "Girl"),
        };

        /// <summary>
        /// A1, measured 2026-09-17 through the bake's bind table (idle frame 0, composed face), and
        /// quoted in ADR 0044 §7. When a rig drop moves one of these, re-measure and update the ADR
        /// with this table; do not just edit the number.
        /// </summary>
        static readonly (string preset, int ramps)[] ComposedRamps =
        {
            ("fisher", 10), ("ginny", 14), ("skipper", 12), ("nan", 13), ("deckboss", 16),
            ("packer", 16), ("cutter", 13), ("hand", 14), ("boy", 13), ("girl", 11),
        };

        /// <summary>The player's committed switch on 2026-09-17, spelled out.</summary>
        static readonly string[] PlayerStates = { "idle", "walk", "run", "balance" };

        IRigScriptHost _host, _host9;
        readonly Dictionary<string, RigMeshData> _composed = new Dictionary<string, RigMeshData>();

        /// <summary>Rig 9's host, made on first use: only the player's control reads it, and only
        /// while <see cref="CharacterSkinAssetBaker.LiveRig"/> names rig 9.</summary>
        IRigScriptHost Host9
        {
            get
            {
                if (_host9 == null)
                {
                    _host9 = RigScriptHostFactory.Create();
                    CharacterSkinExtractor.Load9(_host9);
                }
                return _host9;
            }
        }

        [OneTimeSetUp]
        public void LoadTheRigTheBakeLoads()
        {
            // The same host Compose builds, in the same order: the kit, then the face layer.
            _host = RigScriptHostFactory.Create();
            CharacterSkinExtractor.Load(_host);
            RigCatalog.InstallModule(_host, RigCatalog.Get(CharacterSkinExtractor.FaceCatalogKey));
        }

        [OneTimeTearDown]
        public void Dispose()
        {
            _host?.Dispose();
            _host = null;
            _host9?.Dispose();
            _host9 = null;
            _composed.Clear();
        }

        RigMeshData Composed(string preset)
        {
            if (!_composed.TryGetValue(preset, out RigMeshData bind))
            {
                bind = CharacterPoseMeshExtractor.ExtractPose(
                    _host, preset, "idle", 0, faceLayerJs: CharacterSkinExtractor.FaceLayerJs(preset));
                _composed[preset] = bind;
            }
            return bind;
        }

        static string[] NamesOf(RigMeshData bind) => bind.Materials.Select(m => m.Name).ToArray();

        // =======================================================================================
        // who it bakes
        // =======================================================================================

        [Test]
        public void TheCastBakeOrderIsThePlayerThenTheNineInTheRigsOwnCastOrder()
        {
            (string preset, string stem)[] order = CharacterSkinAssetBaker.CastBakeOrder();

            CollectionAssert.AreEqual(Cast, order,
                "the cast bake order moved. The player bakes first, as a refresh of the player's committed def, " +
                "so a broken path fails before nine new files are written.");

            string g = CharacterPoseMeshExtractor.GlobalName;
            string[] rigCast = _host.EvaluateString($"{g}.CAST.join(',')")
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            CollectionAssert.AreEqual(rigCast, order.Select(e => e.preset).ToArray(),
                $"{g}.CAST no longer matches the cast bake. The rig is the authority on its cast.");
        }

        [Test]
        public void EveryCastPresetHasABuildsEntry()
        {
            string g = CharacterPoseMeshExtractor.GlobalName;
            Func<string, bool> hasBuild = p => _host.EvaluateBool($"typeof {g}.BUILDS[\"{p}\"] === 'object'");

            Assert.IsFalse(hasBuild("no_such_preset"),
                "harness: the BUILDS query answered yes for a preset nobody wrote, so it cannot say no");

            string[] missing = CharacterSkinAssetBaker.CastBakeOrder()
                .Select(e => e.preset).Where(p => !hasBuild(p)).ToArray();
            Assert.IsEmpty(missing,
                $"{g}.BUILDS has no entry for [{string.Join(", ", missing)}]. Compose refuses these, so " +
                "the cast bake would stop at the first one.");
        }

        [Test]
        public void EveryCastPresetHasAnArtDefAndNoLinkPointsAtAnotherPreset()
        {
            var problems = new List<string>();
            foreach ((string preset, string stem) in CharacterSkinAssetBaker.CastBakeOrder())
            {
                string visualPath = CharacterSkinAssetBaker.VisualPathFor(stem);
                var visual = AssetDatabase.LoadAssetAtPath<CharacterVisualDef>(visualPath);
                if (visual == null)
                {
                    problems.Add($"{visualPath} is missing");
                    continue;
                }
                if (visual.Skin == null) continue;
                string linked = AssetDatabase.GetAssetPath(visual.Skin);
                string own = CharacterSkinAssetBaker.AssetPathFor(preset);
                if (!string.Equals(linked, own, StringComparison.Ordinal))
                    problems.Add($"{stem}Iso links {linked}, not {own}");
            }
            Assert.IsEmpty(problems, string.Join("\n", problems));
        }

        // =======================================================================================
        // whether they fit
        // =======================================================================================

        [Test]
        public void EveryComposedMaterialTableFitsTheRampSlots()
        {
            var over = new List<string>();
            foreach (var entry in CharacterSkinAssetBaker.CastBakeOrder())
            {
                RigMeshData bind = Composed(entry.preset);
                if (bind.Materials.Count > CharacterSkinDef.RampSlots)
                    over.Add($"{entry.preset} uses {bind.Materials.Count}: {string.Join(", ", NamesOf(bind))}");
            }
            Assert.IsEmpty(over,
                $"these presets need more than the facet pass's {CharacterSkinDef.RampSlots} ramp slots, and " +
                "the cast bake stops at the first:\n" + string.Join("\n", over) + "\nSplit the preset. The " +
                "shader's ramp table (_RampMeta) belongs to the water lane.");
        }

        [Test]
        public void TheComposedRampCountsAreTheTableADR0044Quotes()
        {
            string[] measured = ComposedRamps.Select(r => $"{r.preset} {Composed(r.preset).Materials.Count}").ToArray();
            string[] quoted = ComposedRamps.Select(r => $"{r.preset} {r.ramps}").ToArray();
            CollectionAssert.AreEqual(quoted, measured,
                "the composed ramp counts moved from the table ADR 0044 §7 quotes. Update the ADR's table " +
                "and this one together.");
        }

        [Test]
        public void WithTheFaceHeldBackTheDeckbossAndThePackerNeedASeventeenthRamp()
        {
            // The sabotage control, through the SAME extractor: rig 6's own head reads 17 for these
            // two. A guard that could never see 17 would pass on any rig.
            foreach (string preset in new[] { "deckboss", "packer" })
            {
                RigMeshData bare = CharacterPoseMeshExtractor.ExtractPose(_host, preset, "idle", 0);
                Assert.That(bare.Materials.Count, Is.EqualTo(CharacterSkinDef.RampSlots + 1),
                    $"'{preset}' without the composed face now reads {bare.Materials.Count} materials, " +
                    "not 17. If the rig's own head fits now, the ramp guard's control is gone. Find another " +
                    "preset that can fail it before deleting this.");
                Assert.That(Composed(preset).Materials.Count, Is.LessThanOrEqualTo(CharacterSkinDef.RampSlots));
            }
        }

        [Test]
        public void ThePlayersComposedTableIsTheCommittedDefsTable()
        {
            var committed = AssetDatabase.LoadAssetAtPath<CharacterSkinDef>(
                CharacterSkinAssetBaker.AssetPathFor(CharacterRigBakeMenu.PlayerPreset));
            Assert.IsNotNull(committed, "harness: the player's committed skin def is the control");

            if (CharacterSkinAssetBaker.LiveRigIsV9)
            {
                // The bake runs ComposeV9: rig 9's rest face, its table read by ReadMaterials9. What it
                // refuses on is MaxMaterials(ToneRule.V9), counted by CharacterSkinBakeGuardTests' v9
                // guards; the rig 7 ramp tables above do not measure it.
                string player = CharacterRigBakeMenu.PlayerPreset;
                string[] rig9 = CharacterSkinExtractor.ReadMaterials9(
                        Host9, player, CharacterSkinExtractor.DefaultFaceMeshJs9(Host9, player))
                    .Select(m => m.Name).ToArray();
                CollectionAssert.AreEqual(committed.Materials.Select(m => m.Name).ToArray(), rig9,
                    "rig 9's extraction here is not the one that baked the player's committed def, so the " +
                    "v9 material counts measure some other table than the one the bake refuses on.");
                return;
            }

            CollectionAssert.AreEqual(committed.Materials.Select(m => m.Name).ToArray(),
                                      NamesOf(Composed(CharacterRigBakeMenu.PlayerPreset)),
                "the extraction here is not the one that baked the player's committed def, so the ramp " +
                "counts above measure some other table than the one the bake refuses on.");
        }

        // =======================================================================================
        // what it switches on
        // =======================================================================================

        [Test]
        public void TheCastSwitchesOnExactlyTheStatesThePlayerShips()
        {
            var committed = AssetDatabase.LoadAssetAtPath<CharacterSkinDef>(
                CharacterSkinAssetBaker.AssetPathFor(CharacterRigBakeMenu.PlayerPreset));
            Assert.IsNotNull(committed, "harness: the player's committed skin def is the reference");

            CollectionAssert.AreEqual(PlayerStates, committed.MeshStates,
                "the player's committed switch moved. Decide whether the cast follows it before " +
                "changing either list.");
            CollectionAssert.AreEqual(committed.MeshStates, CharacterSkinAssetBaker.CastMeshStates,
                "a fresh cast def would switch on a different set of states than the player ships.");
        }

        [Test]
        public void EveryCastStateIsAClipTheRigBakes()
        {
            string[] anims = CharacterSkinExtractor.Anims(_host);
            string[] missing = CharacterSkinAssetBaker.CastMeshStates.Where(s => !anims.Contains(s)).ToArray();
            Assert.IsEmpty(missing,
                $"the rig bakes no clip for [{string.Join(", ", missing)}], so every fresh cast def would " +
                "refuse its switch and the cast bake would stop on the first NPC.");
        }

        [Test]
        public void EveryCastArtDefLinksItsOwnUsableSkin()
        {
            // ⚠️ RED UNTIL PHASE C lands the baked defs and the links. It is the list that bake must
            // deliver, named per character.
            var missing = new List<string>();
            foreach ((string preset, string stem) in CharacterSkinAssetBaker.CastBakeOrder())
            {
                var visual = AssetDatabase.LoadAssetAtPath<CharacterVisualDef>(CharacterSkinAssetBaker.VisualPathFor(stem));
                CharacterSkinDef skin = visual != null ? visual.Skin : null;
                if (skin == null) missing.Add($"{stem}Iso has no skin");
                else if (!skin.IsUsable()) missing.Add($"{stem}Iso links an unusable '{skin.Id}'");
                else if (skin.Id != CharacterSkinAssetBaker.IdFor(preset)) missing.Add($"{stem}Iso links '{skin.Id}'");
                else if (skin.MeshStates == null || skin.MeshStates.Length == 0)
                    missing.Add($"'{skin.Id}' switches on no state and draws nothing");
            }
            Assert.IsEmpty(missing,
                "run the cast bake (CharacterSkinAssetBaker.BakeCastCli) and commit the skins and the " +
                "links:\n" + string.Join("\n", missing));
        }

        // =======================================================================================
        // the switch's own refusals
        // =======================================================================================

        static CharacterSkinDef DefWithClips(params string[] keys)
        {
            var def = ScriptableObject.CreateInstance<CharacterSkinDef>();
            def.Id = "charskin.test_states";
            def.Clips = keys.Select(k => new CharacterSkinDef.SkinClip { Anim = k, State = k }).ToArray();
            return def;
        }

        [Test]
        public void AFreshSwitchIsACopyOfStatesTheDefHasClipsFor()
        {
            CharacterSkinDef def = DefWithClips("idle", "walk");
            try
            {
                var states = new[] { "walk", "idle" };
                string[] written = CharacterSkinAssetBaker.StatesWithClips(def, states);
                CollectionAssert.AreEqual(states, written);
                Assert.AreNotSame(states, written, "the def must not share the caller's array");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(def);
            }
        }

        [Test]
        public void AFreshSwitchNamesEveryStateItCannotWrite()
        {
            CharacterSkinDef def = DefWithClips("idle", "walk");
            try
            {
                var e = Assert.Throws<InvalidOperationException>(() =>
                    CharacterSkinAssetBaker.StatesWithClips(def, new[] { "run", "idle", "idle", "" }));
                StringAssert.Contains("run (no clip)", e.Message);
                StringAssert.Contains("idle (twice)", e.Message);
                StringAssert.Contains("(blank)", e.Message);
                StringAssert.Contains("[idle, walk]", e.Message, "the refusal must list the clips the def has");

                var empty = Assert.Throws<InvalidOperationException>(() =>
                    CharacterSkinAssetBaker.StatesWithClips(def, new string[0]));
                StringAssert.Contains("draws nothing", empty.Message);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(def);
            }
        }
    }
}
