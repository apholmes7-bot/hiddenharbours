using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
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
    /// <para><b>⚠️ The material budget.</b> <see cref="TheStudyHeadPaintsOnlyMaterialsTheRigDeclares"/>
    /// is a guard: the face the owner accepted on 2026-09-16 paints only materials rig 6 declares, for
    /// every preset, and the bake refuses one that does not. Until that ruling it pinned the opposite,
    /// as a blocker. <see cref="TwoPresetsAlreadySitAboveTheRampBudgetBeforeAnyFaceLands"/> still pins
    /// a BLOCKER, not a success: two presets sit above the ramp ceiling on rig 7's own export. It passes
    /// on the truth as measured, so that the day someone fixes it CI says so out loud instead of
    /// leaving a stale claim in a PR body. Both name what to do when they go red.</para>
    ///
    /// <para><b>The nose at the player's scale.</b> The last section measures what the face looks like
    /// at 32 px/m rather than what it is made of: the boy's and the girl's noses must land a pixel at
    /// every heading that faces the camera, and the push that made them land must have moved no adult's
    /// face. Both run the rig's JavaScript in V8 and need no GPU.</para>
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

        // ---- ⚠️ the material budget: one guard, and one blocker still pinned as measured ---------------

        /// <summary>
        /// <b>Every material the composed face paints is one rig 6 declares, for every preset in the
        /// cast.</b> The mesh bake's packer REFUSES a face whose material is not in the pose's union
        /// rather than resolving it to the first ramp — deliberately, because that resolve is exactly
        /// how mis-coloured art has shipped out of this kit before. So a red here means the player's
        /// skin will not bake: <see cref="CharacterSkinExtractor.AssertComposedFaceAgrees"/> and the
        /// packer both refuse the material by name.
        ///
        /// <para>The fix for a red is in the art, never in this check: author a ramp for the material
        /// in <c>CharacterIso6.makeMats</c>, or remap the face onto a material the rig already declares.
        /// Either one changes a face the owner accepted, so it goes back to the owner. Do not widen the
        /// assertion, sample fewer presets, or allow-list a name.</para>
        ///
        /// <para>Until 2026-09-16 this test was <c>TheStudyHeadStillNeedsTwoMaterialsThisRigDoesNotDeclare</c>
        /// and pinned the pass-06 nose's <c>noseLight</c>/<c>noseShadow</c> as a blocker, on one preset.
        /// The face the owner accepted paints the whole nose <c>skin</c>, so the blocker is gone and
        /// <see cref="CharacterSkinAssetBaker.Compose"/> bakes the face by default.</para>
        /// </summary>
        [Test]
        public void TheStudyHeadPaintsOnlyMaterialsTheRigDeclares()
        {
            using (IRigScriptHost host = BaseHost())
            {
                InstallFace(host);
                var red = new System.Collections.Generic.List<string>();
                foreach (string preset in Cast)
                {
                    string undeclared = host.EvaluateString(
                        "(function(){var b=CharacterIso6.resolveBuild({build:{preset:" + Js(preset) + "}});" +
                        "var M=CharacterIso6.makeMats(b).MATS,o={};" +
                        "CharacterFaceComposition.composed(" + Js(preset) + ")" +
                        ".forEach(function(f){if(!M[f.mat])o[f.mat]=1;});" +
                        "return Object.keys(o).sort().join(',');})()");
                    if (!string.Equals(undeclared, "", StringComparison.Ordinal))
                        red.Add("'" + preset + "' paints " + (undeclared ?? "<no answer>"));
                }

                Assert.That(red, Is.Empty,
                    "The composed face paints materials CharacterIso6.makeMats does not declare: " +
                    string.Join("; ", red) + ". The bake refuses each one by name " +
                    "(CharacterSkinExtractor.AssertComposedFaceAgrees, then the packer), so the player's " +
                    "skin will not bake. Fix the ART — author the ramp in makeMats, or remap the face onto " +
                    "a declared material — and take that face back to the owner. Never widen this check.");
            }
        }

        /// <summary>
        /// ⚠️ <b>BLOCKER, pinned — and it PREDATES the face entirely.</b> The def has 16 ramp slots and
        /// the bake refuses to truncate. Two of the ten presets already reach 17 declared materials on
        /// rig 7's own untouched export, so baking <c>deckboss</c> or <c>packer</c> WITHOUT the face is
        /// blocked whether or not a new face ever lands.
        ///
        /// <para>Composing HELPS: dropping rig 6's head takes both to exactly 16, and the face the owner
        /// accepted on 2026-09-16 paints its nose with rig 6's own <c>skin</c>, so it adds nothing back.
        /// That is 16 of 16, with no headroom: a face or body change that gives either preset one more
        /// declared material refuses the bake. The choices then are to widen the slots (a renderer
        /// change: <c>_RampMeta</c> and the def's limits move as a pair) or to paint with a material the
        /// preset already uses. The player preset is not one of these two.</para>
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

        // ---- the nose at the player's scale ---------------------------------------------------------

        /// <summary>
        /// The instrument: rig 7 follow-up job 4's own nose measure (its kit's <c>face-render.cjs</c>
        /// and <c>check-child-nose.cjs</c>) as one script, proven to return the kit's number at all
        /// eighty preset × heading cells on both the old and the new rigs, in node and in the ClearScript
        /// V8 the editor runs.
        ///
        /// <para><c>nosePixels(preset, heading)</c> rasterises the preset's study head at the rig's own
        /// scale and camera (<c>CharacterIso6.PX</c>, <c>defaultElev</c>) and shading (<c>GAIN</c>,
        /// <c>BIAS</c>, <c>LN</c>, <c>makeMats</c>), all read off the rig, then rasterises it again without
        /// the nose (the triangles meeting the head's forward-most vertex) and counts the pixels that
        /// differ. A nose that changes no pixel is not there. The numbers that are the kit's and not the
        /// rig's are the viewer's: the 0.76 m head cell and the outline darkening (.53/.59/.69). They
        /// decide where a pixel lands, not what the bar is.</para>
        ///
        /// <para><c>composedMicrons(preset)</c> is the composed bind-pose face list (every corner,
        /// material, bone, part and uv) serialised with each number rounded to one micron, so its
        /// SHA-256 names the mesh to 1 µm, which is 3e-5 px at 32 px/m, and no two machines' last float
        /// bit can move it.</para>
        /// </summary>
        const string NoseMeasureJs = @"(function(root){
  'use strict';
  var R=root.CharacterIso6, H=root.CharacterHeadStudy, F=root.CharacterFaceComposition;
  if(!R||!H||!F) throw new Error('nose measure: install characterFaceComposition first');
  var ELEV=R.defaultElev, PPM=R.PX, CELL=Math.ceil(PPM*0.76);
  function raster(faces,mats,angle){
    var w=CELL,h=CELL,cx=CELL/2+0.5,cy=CELL/2,scale=PPM;
    var pixels=new Uint8ClampedArray(w*h*4),depth=new Float32Array(w*h).fill(-1e9);
    var a=angle*Math.PI/180,e=ELEV*Math.PI/180,ca=Math.cos(a),sa=Math.sin(a),ce=Math.cos(e),se=Math.sin(e);
    for(var fi=0;fi<faces.length;fi++){
      var f=faces[fi],mat=mats[f.mat];
      var c=mat.ramp[Math.max(0,Math.min(mat.ramp.length-1,mat.idx))];
      var rgb=Array.isArray(c)?c:[parseInt(c.slice(1,3),16),parseInt(c.slice(3,5),16),parseInt(c.slice(5,7),16)];
      var p=f.v.map(function(q){var x=q[0]*ca-q[1]*sa,y=q[0]*sa+q[1]*ca,z=q[2];return [cx+x*scale,cy+(y*se-z*ce)*scale,y*ce+z*se];});
      for(var k=1;k<p.length-1;k++){
        var A=p[0],B=p[k],C=p[k+1],den=(B[1]-C[1])*(A[0]-C[0])+(C[0]-B[0])*(A[1]-C[1]);if(Math.abs(den)<1e-10)continue;
        for(var y=Math.max(0,Math.floor(Math.min(A[1],B[1],C[1])));y<=Math.min(h-1,Math.ceil(Math.max(A[1],B[1],C[1])));y++)
        for(var x=Math.max(0,Math.floor(Math.min(A[0],B[0],C[0])));x<=Math.min(w-1,Math.ceil(Math.max(A[0],B[0],C[0])));x++){
          var wa=((B[1]-C[1])*(x+.5-C[0])+(C[0]-B[0])*(y+.5-C[1]))/den,wb=((C[1]-A[1])*(x+.5-C[0])+(A[0]-C[0])*(y+.5-C[1]))/den,wc=1-wa-wb;
          if(wa<0||wb<0||wc<0)continue;var d=wa*A[2]+wb*B[2]+wc*C[2],i=y*w+x;if(d<=depth[i])continue;depth[i]=d;
          pixels[i*4]=rgb[0];pixels[i*4+1]=rgb[1];pixels[i*4+2]=rgb[2];pixels[i*4+3]=255;
        }
      }
    }
    var edge=[];for(var yy=1;yy<h-1;yy++)for(var xx=1;xx<w-1;xx++){var j=yy*w+xx;if(pixels[j*4+3]&&(!pixels[(j+1)*4+3]||!pixels[(j+w)*4+3]))edge.push(j);}
    for(var n=0;n<edge.length;n++){var e2=edge[n];pixels[e2*4]=Math.round(pixels[e2*4]*.53);pixels[e2*4+1]=Math.round(pixels[e2*4+1]*.59);pixels[e2*4+2]=Math.round(pixels[e2*4+2]*.69);}
    return pixels;
  }
  function gameFaces(build,faces,angle){
    var M=R.makeMats(build).MATS,LN=R.LN;
    var a=angle*Math.PI/180,ca=Math.cos(a),sa=Math.sin(a),e=ELEV*Math.PI/180,se=Math.sin(e),ce=Math.cos(e);
    var mats={},res=[];
    faces.forEach(function(f,i){
      var key='face'+i;res.push(Object.assign({},f,{mat:key}));
      var m=M[f.mat];if(!m){mats[key]={ramp:['#ff00ff'],idx:0};return;}
      var v=f.v.map(function(q){return [q[0]*ca-q[1]*sa,q[0]*sa+q[1]*ca,q[2]];});
      var u=[0,1,2].map(function(k){return v[1][k]-v[0][k];}),t=[0,1,2].map(function(k){return v[2][k]-v[0][k];});
      var nn=[u[1]*t[2]-u[2]*t[1],u[2]*t[0]-u[0]*t[2],u[0]*t[1]-u[1]*t[0]];
      var len=Math.hypot(nn[0],nn[1],nn[2])||1;nn=nn.map(function(x){return x/len;});
      var shade=function(d){return d[0]*LN[0]+(-d[1]*se+d[2]*ce)*LN[1]+(d[1]*ce+d[2]*se)*LN[2];};
      var b=f.b||0,sh=shade(nn);
      if(sh<0&&b<=-1)sh=shade(nn.map(function(x){return -x;}))*0.9;
      var idx=Math.round(sh*R.GAIN+R.BIAS+b)+(m.off||0);
      mats[key]={ramp:m.ramp,idx:Math.max(0,Math.min(m.ramp.length-1,idx))};
    });
    return {faces:res,mats:mats};
  }
  function nosePixels(preset,angle){
    var b=R.resolveBuild({build:{preset:preset}});
    var faces=H.createHead(Object.assign({},b,{headSize:R.propsOf(b).headK}),[0,0,0]);
    var apex=null;faces.forEach(function(f){f.v.forEach(function(v){if(!apex||v[1]>apex[1])apex=v;});});
    var near=function(p,q){return Math.hypot(p[0]-q[0],p[1]-q[1],p[2]-q[2])<1e-12;};
    var kept=faces.filter(function(f){return !(f.v.length===3&&f.v.some(function(v){return near(v,apex);}));});
    var W=gameFaces(b,faces,angle),K=gameFaces(b,kept,angle);
    var P=raster(W.faces,W.mats,angle),Q=raster(K.faces,K.mats,angle),d=0;
    for(var i=0;i<P.length;i+=4)if(P[i]!==Q[i]||P[i+1]!==Q[i+1]||P[i+2]!==Q[i+2]||P[i+3]!==Q[i+3])d++;
    return d;
  }
  function composedMicrons(preset){
    return JSON.stringify(F.composed(preset),function(k,v){return typeof v==='number'?Math.round(v*1e6):v;});
  }
  root.__hhNose={nosePixels:nosePixels,composedMicrons:composedMicrons};
})(globalThis);";

        /// <summary>The headings a nose CAN land at: job 4 measured 135°, 180° and 225° looking at the
        /// back of the head for all ten presets, so asking those for a nose asks the wrong question.</summary>
        static readonly int[] FacingHeadingsDeg = { 0, 45, 90, 270, 315 };

        static readonly string[] Children = { "boy", "girl" };

        /// <summary>
        /// The eight adults' composed faces as they are on main at 40f4656f, to the micron (see
        /// <see cref="NoseMeasureJs"/>). The rig 7 follow-up's rigs reproduce every one exactly.
        /// </summary>
        static readonly (string Preset, string Sha256)[] AdultFacesOnMain =
        {
            ("fisher", "d62c22340a3127c8dd9dd9d1fea434029ef640be361ed86e4427918fa859d71e"),
            ("ginny", "60fbee9663e5cdcdcf6d8f1c996a0c7bedeb9b1e739d2f300d13a701b717e9fa"),
            ("skipper", "557bfac55e1cdef0c9b2e6b6a1cc70203d620d52c0218c50072ad4558b42ea3b"),
            ("nan", "5a7c145567f5810ab0ca939b0d3678b7425ba3fe615ec45325c5d827f238c6d1"),
            ("deckboss", "358db32a7b418acede58388ca7259d7fbbf48191347d2ea0c218517b7b86637a"),
            ("packer", "c48d10c43e0938924ad3d741ea8c7bf9fd7522c3936464405027d713f22f1911"),
            ("cutter", "a496adfaffb9a45a13deee0f234cef9471fe118bd63a91107fb1e4bdcafd83b0"),
            ("hand", "c8ca09a2189c15e55fb60b12097165f06201ea1e208acd5056c2788a214359e9"),
        };

        static IRigScriptHost MeasuringHost()
        {
            IRigScriptHost host = BaseHost();
            InstallFace(host);
            host.Execute(NoseMeasureJs);
            return host;
        }

        static string Sha256Hex(string text)
        {
            using var h = SHA256.Create();
            var sb = new StringBuilder(64);
            foreach (byte b in h.ComputeHash(Encoding.UTF8.GetBytes(text))) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }

        /// <summary>
        /// <b>The children's noses land at the player's scale.</b> The pass-05/06 face scales the nose
        /// with the head, and a child's head is 0.873 of an adult's: on 40f4656f's rigs the boy's and the
        /// girl's noses land 0, 1, 0, 0, 1 px across the five facing headings at 32 px/m, which is no nose
        /// from the front, from the side or from the back three-quarter. Job 4 pushes the child nose 3 mm
        /// forward and 3 mm wide (<c>characterFaceStudy.js</c>) and they land 2, 2, 2, 4, 2. The bar is
        /// ONE pixel at EVERY facing heading, not a count: the brief asked for a nose that reads.
        /// </summary>
        [Test]
        public void TheChildrenResolveANoseAtEveryFacingHeading([ValueSource(nameof(Children))] string preset)
        {
            using (IRigScriptHost host = MeasuringHost())
            {
                var landed = new List<string>();
                var none = new List<string>();
                foreach (int heading in FacingHeadingsDeg)
                {
                    int px = (int)host.EvaluateNumber("__hhNose.nosePixels(" + Js(preset) + "," +
                                                      heading.ToString(CultureInfo.InvariantCulture) + ")");
                    landed.Add(heading + "°: " + px + " px");
                    if (px <= 0) none.Add(heading + "°");
                }

                Assert.That(none, Is.Empty,
                    "'" + preset + "' shows no nose at " + string.Join(", ", none) + " at the player's " +
                    "scale (" + string.Join(", ", landed) + "). At 32 px/m a child's nose falls below a " +
                    "pixel unless the face study pushes it out; that push lives in characterFaceStudy.js " +
                    "(b.age === 'child'), and the fix is there, never a lower bar.");
            }
        }

        /// <summary>
        /// <b>The child push moved no adult's face.</b> Job 4 gates the push on <c>b.age === 'child'</c>;
        /// this proves the gate held, by content: each adult's composed face, to the micron, is the one
        /// on main at 40f4656f.
        ///
        /// <para>It is a PIN and it is green on main by design; its job is the NEXT edit. When the owner
        /// accepts a change to an adult face, re-record that preset's digest from this test's message in
        /// the PR that changes the face, and say so in the PR body. Never re-record to turn a build
        /// green.</para>
        /// </summary>
        [Test]
        public void TheChildNosePushMovesNoAdultsFace()
        {
            var covered = new List<string>(Children);
            foreach (var (preset, _) in AdultFacesOnMain) covered.Add(preset);
            CollectionAssert.AreEquivalent(Cast, covered,
                "every preset in the cast is either a child the nose guard measures or an adult this pins; " +
                "a preset in neither list is guarded by nothing.");

            using (IRigScriptHost host = MeasuringHost())
            {
                var moved = new List<string>();
                foreach (var (preset, pinned) in AdultFacesOnMain)
                {
                    string live = Sha256Hex(host.EvaluateString("__hhNose.composedMicrons(" + Js(preset) + ")"));
                    if (!string.Equals(live, pinned, StringComparison.Ordinal))
                        moved.Add("'" + preset + "' is now " + live + " (pinned " + pinned + ")");
                }

                Assert.That(moved, Is.Empty,
                    "an adult's composed face moved:\n  " + string.Join("\n  ", moved) + "\nThe child " +
                    "nose push is gated on b.age === 'child', so either that gate leaked or another edit " +
                    "moved the face. If the owner accepted the change, re-record the digest in the same PR " +
                    "and name it in the PR body.");
            }
        }
    }
}
