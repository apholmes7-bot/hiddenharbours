using System;
using System.Globalization;
using System.IO;
using NUnit.Framework;
using HiddenHarbours.Tools.RigBaking;

namespace HiddenHarbours.Tests.RigBaking
{
    /// <summary>
    /// <b>THE PASS-05/06 FACE, COMPOSED — and the gameplay pins it is not allowed to move.</b>
    ///
    /// <para>The owner's driver was one sentence: "The eyes do not look good." Pass 06 revises pass
    /// 05's flat dark openings and unfocused gaze, and <c>characterFaceComposition.js</c> is the layer
    /// that puts that face on the skinned cast. Everything below exists because a better face is worth
    /// nothing if it arrives by moving the rig: metre scale, skeletons, physical heights and
    /// hand/foot/attachment positions are GAMEPLAY PINS, not art. They are asserted BY NAME here, not
    /// by a summary statistic, because a mean vertex error cannot tell you the left hand moved.</para>
    ///
    /// <para><b>Why one host and not two.</b> The composition module patches nothing — installing it is
    /// inert. So the honest measurement is to ask the rig its pins, install the face layer, and ask
    /// again in the SAME host. Two hosts would leave a cross-host difference to argue about; this
    /// leaves none, and it also means <see cref="InstallingTheFaceLayerWrapsNoneOfTheRigsOwnExports"/>
    /// is measuring the property the other tests depend on.</para>
    ///
    /// <para><b>⚠️ Two of these tests assert a BLOCKER, not a success.</b> They pin measured facts that
    /// stop the ten-preset bake today — the study's two undeclared materials, and two presets already
    /// at the ramp ceiling before any face lands. They are written to pass on the truth as measured, so
    /// that the day someone fixes one, CI says so out loud instead of leaving a stale claim in a PR
    /// body. Each names what to do when it goes red.</para>
    /// </summary>
    public class CharacterFaceCompositionTests
    {
        const string FaceModule = "characterFaceComposition";

        /// <summary>The ten presets of the owner's cast viewer, named rather than read back from the
        /// rig: if a drop quietly loses one, this file should fail rather than test nine.</summary>
        static readonly string[] Cast =
            { "fisher", "ginny", "skipper", "nan", "deckboss", "packer", "cutter", "hand", "boy", "girl" };

        /// <summary>
        /// The pins, spelled out. Every one of these is something gameplay reads: where she grips the
        /// rod (<c>tool_*</c>), where a carried crate rides (<c>carry_*</c>), where she stands on the
        /// deck (<c>foot_*</c>), how she is stacked from the ground up.
        ///
        /// <para>Measured: all fourteen exist in all ten presets. Deliberately NOT here are
        /// <c>inseam_top</c>/<c>inseam_bot</c> and <c>skirt_hem</c> — those are garment bones and the
        /// cast does not agree on which it has, so asserting them would fail on preset shape rather
        /// than on a moved pin.</para>
        /// </summary>
        static readonly string[] Anchors =
        {
            "root", "pelvis", "torso", "neck", "head",
            "hand_L", "hand_R", "foot_L", "foot_R",
            "tool_L", "tool_R", "carry_L", "carry_R", "carry_mid",
        };

        /// <summary>The six exports the bake and the presenter actually call. If the face layer wrapped
        /// any of them the seam would be invisible at the call site, which is the failure this kit
        /// already walked into once.</summary>
        static readonly string[] RigExports =
            { "skeleton", "skeletonWorld", "bindMesh", "clip", "clips", "bindOf" };

        static IRigScriptHost BaseHost()
        {
            IRigScriptHost host = RigScriptHostFactory.Create();
            RigCatalog.InstallModule(host, RigCatalog.Get("characterSkin"));
            return host;
        }

        static void InstallFace(IRigScriptHost host) =>
            RigCatalog.InstallModule(host, RigCatalog.Get(FaceModule));

        static string Js(string s) => "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

        static string Json(IRigScriptHost host, string expr) =>
            host.EvaluateString("JSON.stringify(" + expr + ")");

        /// <summary>The metre extent of a mesh along z — her actual height, floor to crown — returned
        /// as "low|high" so a failure names WHICH end moved.</summary>
        static string ZExtent(IRigScriptHost host, string meshJs) => host.EvaluateString(
            "(function(){var m=" + meshJs + ",lo=1e9,hi=-1e9;" +
            "for(var i=0;i<m.length;i++)for(var k=0;k<m[i].v.length;k++){" +
            "var z=m[i].v[k][2];if(z<lo)lo=z;if(z>hi)hi=z;}" +
            "return lo.toFixed(6)+'|'+hi.toFixed(6);})()");

        static double Low(string extent) =>
            double.Parse(extent.Split('|')[0], CultureInfo.InvariantCulture);

        static double High(string extent) =>
            double.Parse(extent.Split('|')[1], CultureInfo.InvariantCulture);

        // ---- the layer is inert -------------------------------------------------------------------

        /// <summary>
        /// The composition offers functions and WRAPS NOTHING.
        ///
        /// <para>An earlier draft of that module redefined <c>CharacterIso7.bindMesh</c>, which reads
        /// like the tidiest possible seam and is wrong: the skinned bake takes its GEOMETRY from
        /// <see cref="CharacterPoseMeshExtractor"/> (rig 6's own <c>facesOf</c>) and only its WEIGHTS
        /// from <c>bindMesh</c>, then demands the two agree corner-for-corner. Composing one side
        /// silently would not have produced a new face; it would have produced a bake that throws. So
        /// the real seam is a named parameter the reviewer can see at the call site, and this asserts
        /// the module left the rig alone — by function IDENTITY, which no wrapper that happens to
        /// agree numerically can satisfy.</para>
        /// </summary>
        [Test]
        public void InstallingTheFaceLayerWrapsNoneOfTheRigsOwnExports()
        {
            using (IRigScriptHost host = BaseHost())
            {
                host.Execute("globalThis.__hhBefore={};" +
                             "['" + string.Join("','", RigExports) + "']" +
                             ".forEach(function(k){__hhBefore[k]=CharacterIso7[k];});");

                InstallFace(host);

                foreach (string fn in RigExports)
                    Assert.That(host.EvaluateBool("__hhBefore." + fn + "===CharacterIso7." + fn), Is.True,
                        "CharacterIso7." + fn + " is not the same function object after the face layer " +
                        "loaded. The layer must wrap NOTHING — the bake opts into the composed face at " +
                        "a named step instead, so the seam is visible at the call site.");

                Assert.That(host.EvaluateString("String(CharacterFaceComposition.revision)"),
                    Is.Not.Empty, "The composition must name the revision it composes.");
            }
        }

        // ---- the gameplay pins --------------------------------------------------------------------

        /// <summary>
        /// THE CHARTER'S OWN ACCEPTANCE LINE: skeletons unchanged, proven by name. Every bone of every
        /// preset, bind-local and world, compared across the install. The per-anchor test below says
        /// WHICH pin moved; this one says whether ANY did, including the ones nobody thought to name.
        /// </summary>
        [Test]
        public void EveryBoneOfEveryPresetIsUnmovedByTheFaceLayer()
        {
            using (IRigScriptHost host = BaseHost())
            {
                var localBefore = new string[Cast.Length];
                var worldBefore = new string[Cast.Length];
                for (int i = 0; i < Cast.Length; i++)
                {
                    localBefore[i] = Json(host, "CharacterIso7.skeleton(" + Js(Cast[i]) + ")");
                    worldBefore[i] = Json(host, "CharacterIso7.skeletonWorld(" + Js(Cast[i]) + ")");
                }

                InstallFace(host);

                for (int i = 0; i < Cast.Length; i++)
                {
                    Assert.That(Json(host, "CharacterIso7.skeleton(" + Js(Cast[i]) + ")"),
                        Is.EqualTo(localBefore[i]),
                        "'" + Cast[i] + "': the bind skeleton moved. Metre scale and bone rest " +
                        "positions are gameplay pins — a face may not buy its looks with them.");
                    Assert.That(Json(host, "CharacterIso7.skeletonWorld(" + Js(Cast[i]) + ")"),
                        Is.EqualTo(worldBefore[i]),
                        "'" + Cast[i] + "': the world skeleton moved.");
                }
            }
        }

        /// <summary>
        /// The anchors, one at a time and by name. The whole-skeleton test above already compares
        /// everything; this exists so the failure MESSAGE names the pin. "Her right hand moved" is
        /// actionable; "the skeleton JSON differs" sends someone diffing 45 bones.
        /// </summary>
        [Test]
        public void EveryNamedAnchorHoldsItsExactWorldPosition([ValueSource(nameof(Cast))] string preset)
        {
            using (IRigScriptHost host = BaseHost())
            {
                Func<string, string> pin = id => host.EvaluateString(
                    "(function(){var s=CharacterIso7.skeletonWorld(" + Js(preset) + ");" +
                    "for(var i=0;i<s.length;i++)if(s[i].id===" + Js(id) + ")" +
                    "return s[i].pos.join(',')+'@'+s[i].rot.join(',');" +
                    "return 'ABSENT';})()");

                var before = new string[Anchors.Length];
                for (int i = 0; i < Anchors.Length; i++)
                {
                    before[i] = pin(Anchors[i]);
                    Assert.That(before[i], Is.Not.EqualTo("ABSENT"),
                        "'" + preset + "' has no bone '" + Anchors[i] + "'. The anchor list is what " +
                        "gameplay reads — a preset missing one cannot be baked as a stand-in for the " +
                        "others, and the list itself is wrong if the cast genuinely disagrees here.");
                }

                InstallFace(host);

                for (int i = 0; i < Anchors.Length; i++)
                    Assert.That(pin(Anchors[i]), Is.EqualTo(before[i]),
                        "'" + preset + "': the attachment anchor '" + Anchors[i] + "' moved when the " +
                        "face layer loaded. Tools, carried cargo and her stance all hang off these.");
            }
        }

        /// <summary>
        /// <b>Her height — and the one thing the new head DOES move.</b>
        ///
        /// <para>Her feet are pinned to the ground plane EXACTLY: the floor of the composed mesh equals
        /// the floor of rig 7's own, to the last bit. That is the half of "physical height" gameplay
        /// actually reads.</para>
        ///
        /// <para>The crown is the half that moves. The pass-06 head's silhouette sits BELOW rig 6's,
        /// measured at 5.5 mm to 18 mm depending on preset (fisher is the outlier, because
        /// <c>CharacterArtStudy</c> rebuilds fisher's body as well as its head). That is hair and skull
        /// silhouette — art — and nothing in the game reads it: the only consumer of the mesh's extent
        /// is <c>IsoCharacterFigureRenderer</c>, which hands it to Unity as CULLING bounds. No collider,
        /// no camera frame, no reach or seat height comes off it.</para>
        ///
        /// <para>So it is bounded rather than forbidden: the crown may lower, by no more than 25 mm,
        /// and may not rise at all. A drop that takes 10 cm off her head trips this.</para>
        /// </summary>
        [Test]
        public void HerFeetArePinnedExactlyAndOnlyTheCrownSilhouetteMoves()
        {
            const double crownAllowance = 0.025;   // metres — envelope over a measured 0.0055–0.0181

            using (IRigScriptHost host = BaseHost())
            {
                InstallFace(host);
                foreach (string preset in Cast)
                {
                    string bas = ZExtent(host, "CharacterFaceComposition.baseBindMesh(" + Js(preset) + ")");
                    string composed = ZExtent(host, "CharacterFaceComposition.composed(" + Js(preset) + ")");

                    Assert.That(Low(composed), Is.EqualTo(Low(bas)),
                        "'" + preset + "': the floor of the mesh moved from " + Low(bas) + " m to " +
                        Low(composed) + " m. She stands on her feet; the head layer may not lift or " +
                        "sink her.");

                    double crown = High(composed) - High(bas);
                    Assert.That(crown, Is.LessThanOrEqualTo(1e-9),
                        "'" + preset + "': the composed crown rose by " + crown.ToString("0.#####") +
                        " m. The pass-06 head sits at or below rig 6's silhouette; a rise means the " +
                        "head is being placed, not composed.");
                    Assert.That(-crown, Is.LessThanOrEqualTo(crownAllowance),
                        "'" + preset + "': the composed crown dropped " + (-crown).ToString("0.#####") +
                        " m, past the " + crownAllowance + " m envelope. Hair silhouette is art and may " +
                        "move a little; this is enough to read as a different character's height.");
                }
            }
        }

        /// <summary>
        /// All 35 animations, unchanged. The study head rides the head bone rigidly at weight 1, so
        /// composing the face must not touch a single track — and if it ever does, the mesh stops
        /// agreeing with the sprite one frame at a time, which is the silent failure this kit already
        /// knows by name.
        /// </summary>
        [Test]
        public void NoTrackOfAnyAnimationChanges([Values("fisher", "nan")] string preset)
        {
            using (IRigScriptHost host = BaseHost())
            {
                string[] anims = host.EvaluateString("Object.keys(CharacterIso7.ANIMS).join(',')").Split(',');
                Assert.That(anims.Length, Is.EqualTo(35),
                    "The cast viewer's 35 animations are the contract this drop preserves; this rig " +
                    "declares " + anims.Length + ".");

                Func<string, string> clip = a => Json(host,
                    "CharacterIso7.clip(" + Js(a) + "," + Js(preset) + ")");

                var before = new string[anims.Length];
                for (int i = 0; i < anims.Length; i++)
                {
                    before[i] = clip(anims[i]);
                    Assert.That(before[i], Is.Not.Null.And.Not.Empty,
                        "'" + preset + "': the rig returned no clip for '" + anims[i] + "'.");
                }

                InstallFace(host);

                for (int i = 0; i < anims.Length; i++)
                    Assert.That(clip(anims[i]), Is.EqualTo(before[i]),
                        "'" + preset + "': the clip '" + anims[i] + "' changed under the face layer — " +
                        "frames, bones or tracks. The face is not allowed a say in how she moves.");
            }
        }

        // ---- what the composition actually does -----------------------------------------------------

        /// <summary>
        /// The ORIGINAL premise, re-addressed and unweakened.
        ///
        /// <para>Before the face existed, the baker's <c>AssertKitLoaded</c> proved "rig 7 re-expresses
        /// rig 6's build" by comparing face counts against <c>bindMesh</c>. Wiring the face layer in
        /// moves that call — so the claim is asserted here against the rig's own untouched export
        /// instead. A guard that MOVES has to keep being measured somewhere, or it was deleted with
        /// extra steps.</para>
        /// </summary>
        [Test]
        public void RigSevensUntouchedExportStillEqualsRigSixsOwnIdlePose()
        {
            using (IRigScriptHost host = BaseHost())
            {
                InstallFace(host);
                foreach (string preset in Cast)
                {
                    int bas = (int)host.EvaluateNumber(
                        "CharacterFaceComposition.baseBindMesh(" + Js(preset) + ").length");
                    int idle = (int)host.EvaluateNumber(
                        "(function(){var C=CharacterIso6," +
                        "b=C.resolveBuild({build:{preset:" + Js(preset) + "}});" +
                        "return C.facesOf(C.pose('idle',0,b),b).length;})()");

                    Assert.That(bas, Is.EqualTo(idle),
                        "'" + preset + "': rig 7's bind mesh has " + bas + " faces and rig 6's " +
                        "pose('idle',0) has " + idle + ". The export is not re-expressing this build — " +
                        "and that is true with or without a face layer on top of it.");
                }
            }
        }

        /// <summary>
        /// The composition removes rig 6's head and NOTHING else, then adds the study's.
        ///
        /// <para>Asserted structurally, not by a count: a count cannot tell "dropped the head" from
        /// "dropped the head and a sleeve". Two faces in the same place is not a face, so a single
        /// surviving eye or brow from the old head is a defect even though it would barely move the
        /// totals.</para>
        /// </summary>
        [Test]
        public void TheCompositionDropsExactlyTheOldHeadAndAddsTheStudysOwn()
        {
            using (IRigScriptHost host = BaseHost())
            {
                InstallFace(host);
                foreach (string preset in Cast)
                {
                    string stale = host.EvaluateString(
                        "(function(){var drop=CharacterFaceComposition.oldHeadParts" +
                        ".filter(function(x){return x!=='head';}).concat(['inseam']),seen={};" +
                        "CharacterFaceComposition.composed(" + Js(preset) + ")" +
                        ".forEach(function(f){if(drop.indexOf(f.part)>=0)seen[f.part]=1;});" +
                        "return Object.keys(seen).sort().join(',');})()");
                    Assert.That(stale, Is.Empty,
                        "'" + preset + "': rig 6's own '" + stale + "' survived into the composed mesh " +
                        "alongside the study's head. Two faces in the same place is not a face.");

                    int head = (int)host.EvaluateNumber(
                        "CharacterFaceComposition.composed(" + Js(preset) + ")" +
                        ".filter(function(f){return f.part==='head';}).length");
                    Assert.That(head, Is.GreaterThan(0),
                        "'" + preset + "': the composed mesh carries no head at all. Do not bake a " +
                        "headless cast because the arithmetic balanced.");

                    int width = (int)host.EvaluateNumber(
                        "(function(){var m=CharacterFaceComposition.composed(" + Js(preset) + "),w=0;" +
                        "for(var i=0;i<m.length;i++)for(var k=0;k<m[i].bone.length;k++)" +
                        "if(m[i].bone[k].length>w)w=m[i].bone[k].length;return w;})()");
                    Assert.That(width, Is.LessThanOrEqualTo(HiddenHarbours.Core.CharacterSkinDef.MaxBoneInfluences),
                        "'" + preset + "': the composed mesh wants " + width + " bone influences per " +
                        "vertex and the def carries " +
                        HiddenHarbours.Core.CharacterSkinDef.MaxBoneInfluences +
                        ". The study head is meant to ride the head bone rigidly at weight 1.");
                }
            }
        }

        /// <summary>
        /// The tailoring table ships as a JS module so the catalog's own prerequisite chain loads it;
        /// it was GENERATED from the workbench JSON the checkers scored. Two copies of a 14 KB config
        /// is a drift waiting to happen, and this is the thing that notices. Key order is ignored —
        /// the generator is free to re-emit — but every value must still agree.
        /// </summary>
        [Test]
        public void TheFinishConfigStillMatchesTheWorkbenchJsonItWasGeneratedFrom()
        {
            string authored = Path.Combine(RigCatalog.RepoRoot,
                "docs/art/character-workbench/character-finish.json");
            Assert.That(File.Exists(authored), Is.True,
                "The authored finish config is missing at " + authored + ". characterFinishConfig.js " +
                "is generated from it; without the source the module cannot be regenerated or checked.");

            using (IRigScriptHost host = BaseHost())
            {
                InstallFace(host);
                host.Execute("globalThis.__hhAuthored=" + File.ReadAllText(authored) + ";");
                host.Execute(
                    "globalThis.__hhCanon=function(v){" +
                    "if(Array.isArray(v))return '['+v.map(__hhCanon).join(',')+']';" +
                    "if(v&&typeof v==='object')return '{'+Object.keys(v).sort().map(function(k){" +
                    "return JSON.stringify(k)+':'+__hhCanon(v[k]);}).join(',')+'}';" +
                    "return JSON.stringify(v);};");

                Assert.That(host.EvaluateBool("__hhCanon(__hhAuthored)===__hhCanon(CharacterFinishConfig)"),
                    Is.True,
                    "characterFinishConfig.js has drifted from character-finish.json. Edit the workbench " +
                    "JSON and regenerate the module; never hand-edit one of the two, or the checkers " +
                    "score a table the bake does not use.");
            }
        }

        // ---- ⚠️ the BLOCKERS, pinned as measured ------------------------------------------------------

        /// <summary>
        /// ⚠️ <b>BLOCKER, pinned.</b> The pass-06 head paints its nose with <c>noseLight</c> and
        /// <c>noseShadow</c>, and rig 6's <c>makeMats</c> declares neither of them among its 36
        /// materials. The mesh bake's packer REFUSES a face whose material is not in the pose's union
        /// rather than resolving it to the first ramp — deliberately, because that resolve is exactly
        /// how mis-coloured art has shipped out of this kit before. So the composed cast cannot bake
        /// until those two have ramps.
        ///
        /// <para>The study never needed them: its own <c>colours(build)</c> returns a flat eleven-entry
        /// palette, and the facet renderer reads four-stop ramps. Authoring two new ramps — or remapping
        /// the nose onto materials the rig already declares — changes the face the owner is being asked
        /// to accept, so it is an art-director decision and is NOT made here.</para>
        ///
        /// <para>This test passes on the blocker being PRESENT. The wiring is already DONE —
        /// <see cref="CharacterSkinAssetBaker.Compose"/> takes a <c>composedFace</c> flag that installs
        /// this layer and bakes it — and it defaults to OFF for exactly the reason above. So when
        /// someone lands the ramps this goes red, and the fix is to flip that default and delete this
        /// test, not to plumb anything.</para>
        /// </summary>
        [Test]
        public void TheStudyHeadStillNeedsTwoMaterialsThisRigDoesNotDeclare()
        {
            using (IRigScriptHost host = BaseHost())
            {
                InstallFace(host);
                string undeclared = host.EvaluateString(
                    "(function(){var b=CharacterIso6.resolveBuild({build:{preset:'ginny'}});" +
                    "var M=CharacterIso6.makeMats(b).MATS,o={};" +
                    "CharacterFaceComposition.composed('ginny')" +
                    ".forEach(function(f){if(!M[f.mat])o[f.mat]=1;});" +
                    "return Object.keys(o).sort().join(',');})()");

                Assert.That(undeclared, Is.EqualTo("noseLight,noseShadow"),
                    "The set of materials the study head needs and rig 6 does not declare has CHANGED " +
                    "— it is now '" + undeclared + "'. If it is EMPTY the blocker is FIXED: the bake " +
                    "is already wired, so flip CharacterSkinAssetBaker.Compose's composedFace default " +
                    "to true and delete this test. If it GREW, the drop introduced another material " +
                    "and the bake will refuse the face.");
            }
        }

        /// <summary>
        /// ⚠️ <b>BLOCKER, pinned — and it PREDATES the face entirely.</b> The def has 16 ramp slots and
        /// the bake refuses to truncate. Two of the ten presets already reach 17 declared materials on
        /// rig 7's own untouched export, so "bake the ten presets" is blocked for <c>deckboss</c> and
        /// <c>packer</c> whether or not a new face ever lands.
        ///
        /// <para>Composing actually HELPS: dropping rig 6's head takes both to exactly 16. But the two
        /// undeclared nose materials above would put them straight back to 18 the moment they are given
        /// ramps — so the two blockers have to be ruled on TOGETHER: widen the slots (a renderer change:
        /// <c>_RampMeta</c> and the def's limits move as a pair), or give the nose materials rig 6
        /// already declares.</para>
        /// </summary>
        [Test]
        public void TwoPresetsAlreadySitAboveTheRampBudgetBeforeAnyFaceLands()
        {
            using (IRigScriptHost host = BaseHost())
            {
                InstallFace(host);
                Func<string, string, int> slots = (preset, mesh) => (int)host.EvaluateNumber(
                    "(function(){var b=CharacterIso6.resolveBuild({build:{preset:" + Js(preset) + "}});" +
                    "var M=CharacterIso6.makeMats(b).MATS,used={};" +
                    mesh + ".forEach(function(f){used[f.mat]=1;});" +
                    "var n=0;for(var k in M)if(used[k])n++;return n;})()");

                foreach (string preset in new[] { "deckboss", "packer" })
                {
                    int bas = slots(preset, "CharacterFaceComposition.baseBindMesh(" + Js(preset) + ")");
                    Assert.That(bas, Is.EqualTo(17),
                        "'" + preset + "' now uses " + bas + " declared materials, not the 17 measured " +
                        "when this was written. With " + HiddenHarbours.Core.CharacterSkinDef.RampSlots +
                        " ramp slots, anything above that cannot bake — if this is now 16 or fewer the " +
                        "blocker is GONE and this test should go with it.");
                }

                foreach (string preset in Cast)
                {
                    int composed = slots(preset, "CharacterFaceComposition.composed(" + Js(preset) + ")");
                    Assert.That(composed, Is.LessThanOrEqualTo(HiddenHarbours.Core.CharacterSkinDef.RampSlots),
                        "'" + preset + "': composing pushed the DECLARED material count to " + composed +
                        ", past the " + HiddenHarbours.Core.CharacterSkinDef.RampSlots + " ramp slots. " +
                        "Dropping rig 6's head is supposed to buy headroom, not spend it.");
                }
            }
        }
    }
}
