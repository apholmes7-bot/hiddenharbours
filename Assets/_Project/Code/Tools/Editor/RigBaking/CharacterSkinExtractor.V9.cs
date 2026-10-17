using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>One painted material as rig 9's <c>shadingContract</c> states it: its own gain, bias
    /// and tone window (lo..hi) over its own ramp, or one fixed colour.</summary>
    public sealed class RigMaterial9
    {
        public string Name;
        public bool Fixed;
        public Color32[] Ramp;
        public string[] RampHex;
        public int Off;
        public double Gain = 1.0;
        public double Bias;
        public int Lo;
        public int Hi;

        /// <summary>The same material in the shape the reference rasterizer and the mesh builder
        /// read. Only the ramp colours and the count matter there: the turntable sign is read on
        /// the silhouette, never on the shade.</summary>
        public RigMaterial ToRigMaterial() => new RigMaterial
        {
            Name = Name,
            Ramp = Ramp,
            RampHex = RampHex,
            Off = Off,
            Gain = Gain,
            Bias = Bias,
            OrderedDither = false,
            FixedIndex = -1,
        };
    }

    // Rig 9: Claude Design's character-rig kit v9.2 (docs/art/rigs/character/rig9/). This half
    // READS it, into the shapes rig 7's reader already hands the baker (RigBone, RigSkinning,
    // RigSkinClip), so the def, the mesh builder and the skin attach code stay shared and only the
    // reading differs. What differs from rig 7, and why each difference is met here and not by
    // editing the rig:
    //  - The body IS the skinned export. There is no rig 6 to agree with: bindMesh, skeleton and
    //    clip are rig 9's own, and so is its shading contract (each material's gain, bias, lo, hi).
    //  - Its face is composed by FACE GROUP (eyes.*, brows.*, mouth.*). Since character PR 2a the
    //    bind mesh carries EVERY group (FaceMeshJs9), each face tagged with its group, the role it
    //    culls at and whether the head snap moves it, and the engine draws one group per slot. The
    //    rest face, FACE_SLOTS[slot][0], is what baseIntent rests every preset on: asserted per
    //    preset, not assumed, and baked as the def's RestFace.
    //  - It exports no TOL and no BAYER. The tolerance is the one its own checks gate on
    //    (checks.js, "gate 1e-6 m") and the dither matrix is the canonical one.
    //  - normBuild falls back to the fisher without a word, so every preset is checked against
    //    CAST before anything is read under its name.
    //  - Its poses are a second file that must run after the body, and its checks a third.
    //
    // Rig 10 (character/rig10/: the kit 10.2 landed by the intake of 2026-10-02, 10.3 over it on
    // 2026-10-03) keeps that API and is read by this same half: CharacterRigKit names its files, global,
    // revision and preset table, and a host holds one kit (Load9(host, kit); every reader asks
    // KitOf(host)). What rig 10 changes, and where it is met:
    //  - Its CAST is the ten presets and the twenty NPCs; the game bakes CAST10 alone (owner, 10-01),
    //    and an unknown preset now throws in the rig itself.
    //  - The cell is 80 x 104 with its pivot at (40, 90): read off the rig, as rig 9's was.
    //  - The face is drawn as point marks (pt), each culled by its own turn band (az, the cosine of
    //    the band) and drawn over the head's own pixels (PT_UNDER, or the hair for an oh mark); the
    //    smooth parts are lit on their own normal (sn). ROLE and minT are gone. ReadFaces9 carries
    //    all four onto each face, and brows.flat binds no faces on purpose (FACE_EMPTY).
    //  - SHADING.keylineDefault is false: rig 10 draws no keyline unless asked (owner ruling K4).
    //  - Since 10.3 it exports what a port of its paint needs (TOL, DITHER, INK with INK_ROLES and
    //    EYE_WHITE, AIM), and its paint reads its tolerances from TOL. The bake reads each there and
    //    copies none (Tol9, GateTolerance9, CullFloor9(host), ReadMarkCull9(host), PaintTolerance9,
    //    Bayer9), each paint site held to read the TOL key the port reads; the materials' ink colours
    //    come from shadingContract, which takes them from INK and EYE_WHITE. The marks' band floor
    //    (hh < floor) is still a literal in paint() with no TOL key, read off it as 10.2's was.
    public static partial class CharacterSkinExtractor
    {
        public const string V9CatalogKey = "characterRig9";
        public const string V9RigName = "characterIsoRig9";
        public const string V9Revision = "9.2";
        public const int V9Pass = 9;

        /// <summary>The tolerance rig 9's own checks gate on (checks.js, "gate 1e-6 m"). Rig 9
        /// exports no TOL, and borrowing rig 7's would judge one rig by another's bar. Rig 10 exports
        /// its own since 10.3 (<c>TOL.gate_m</c>): <see cref="GateTolerance9"/> reads the bar of the rig
        /// a host holds.</summary>
        public const double V9Tolerance = 1e-6;

        public static RigEntry V9Entry => CharacterRigKit.Rig9.Entry;
        public static string V9ScriptPath => CharacterRigKit.Rig9.ScriptPath;
        public static string V9GlobalName => CharacterRigKit.Rig9.GlobalName;
        public static string V9PosesPath => CharacterRigKit.Rig9.PosesPath;
        public static string V9ChecksPath => CharacterRigKit.Rig9.ChecksPath;

        static string ReadRepoText(string repoRelativePath) =>
            File.ReadAllText(Path.Combine(RigCatalog.RepoRoot, repoRelativePath));

        public static string SourceSha256V9() => SourceSha256V9(CharacterRigKit.Rig9);
        public static string PosesSha256V9() => PosesSha256V9(CharacterRigKit.Rig9);
        public static string SourceSha256V9(CharacterRigKit kit) => LfSha256(kit.ScriptPath);
        public static string PosesSha256V9(CharacterRigKit kit) => LfSha256(kit.PosesPath);

        // ---------------------------------------------------------------------------------------
        // Loading
        // ---------------------------------------------------------------------------------------

        /// <summary>Which kit each host holds. Weak, so a disposed host is forgotten with it.</summary>
        static readonly ConditionalWeakTable<IRigScriptHost, CharacterRigKit> HostKits =
            new ConditionalWeakTable<IRigScriptHost, CharacterRigKit>();

        /// <summary>The character rig <paramref name="host"/> holds: the kit
        /// <see cref="Load9(IRigScriptHost, CharacterRigKit)"/> installed there, or rig 9 for a host
        /// nobody loaded, as before rig 10 existed.</summary>
        public static CharacterRigKit KitOf(IRigScriptHost host) =>
            host != null && HostKits.TryGetValue(host, out CharacterRigKit kit) ? kit : CharacterRigKit.Rig9;

        /// <summary>Install the host's kit (rig 9 on a host nobody loaded) and its poses, once.</summary>
        public static void Load9(IRigScriptHost host) => Load9(host, null);

        /// <summary>Install <paramref name="kit"/> (null: the one <paramref name="host"/> holds, or rig
        /// 9) and its poses in <paramref name="host"/>, once. A host that already holds the kit's
        /// revision with its poses is left as it is; re-running the body would wipe what a sidecar
        /// added (the checks) for nothing. A host holds one kit: asking one host for another refuses,
        /// since every reader would then read whichever global it named.</summary>
        public static void Load9(IRigScriptHost host, CharacterRigKit kit)
        {
            if (host == null) throw new ArgumentNullException(nameof(host));
            CharacterRigKit held = HostKits.TryGetValue(host, out CharacterRigKit h) ? h : null;
            kit = kit ?? held ?? CharacterRigKit.Rig9;
            if (held != null && held != kit)
                throw new InvalidOperationException(
                    $"This host holds {held}. One host reads one character rig, so {kit} needs a host of its own.");

            if (!host.EvaluateBool(Loaded9Js(kit)))
            {
                RigCatalog.InstallModule(host, kit.Entry);
                host.Execute(ReadRepoText(kit.PosesPath));

                string g = kit.GlobalName;
                if (!host.EvaluateBool(Loaded9Js(kit)))
                    throw new InvalidOperationException(
                        $"{kit.PosesPath} ran and {g} says posesLoaded=" +
                        host.EvaluateString($"String({g}.posesLoaded)") + ", revision " +
                        host.EvaluateString($"String({g}.revision)") + $". This reader is written for " +
                        $"{kit.Name} revision {kit.Revision} with its poses installed; a rig without its " +
                        "poses bakes clips that do not move.");
                string rig = host.EvaluateString($"String({g}.rig)");
                int pass = (int)host.EvaluateNumber($"{g}.pass");
                if (rig != kit.RigName || pass != kit.Pass)
                    throw new InvalidOperationException(
                        $"{kit.ScriptPath} says it is '{rig}' pass {pass}; this reader is written for " +
                        $"'{kit.RigName}' pass {kit.Pass}.");
            }
            if (held == null) HostKits.Add(host, kit);
        }

        /// <summary>Also install the host's own checks (runChecks), for the guards that replay them.
        /// Never loaded by the bake.</summary>
        public static void Load9Checks(IRigScriptHost host)
        {
            Load9(host);
            CharacterRigKit kit = KitOf(host);
            string g = kit.GlobalName;
            if (host.EvaluateBool($"typeof {g}.runChecks==='function'")) return;
            host.Execute(ReadRepoText(kit.ChecksPath));
            if (!host.EvaluateBool($"typeof {g}.runChecks==='function'"))
                throw new InvalidOperationException($"{kit.ChecksPath} ran and {g}.runChecks is still missing.");
        }

        static string Loaded9Js(CharacterRigKit kit)
        {
            string g = kit.GlobalName;
            return $"typeof {g}==='object'&&{g}!==null&&{g}.posesLoaded===true&&" +
                   $"String({g}.revision)==={Js(kit.Revision)}";
        }

        /// <summary>The presets the host's rig declares for the game to bake, in its own order: rig
        /// 9's <c>CAST</c>, rig 10's <c>CAST10</c>.</summary>
        public static string[] Presets9(IRigScriptHost host)
        {
            Load9(host);
            CharacterRigKit kit = KitOf(host);
            return SplitList(host.EvaluateString($"{kit.GlobalName}.{kit.PresetTable}.join(',')"));
        }

        /// <summary>Refuse a preset the host's rig does not declare for the game. Rig 9's normBuild
        /// would quietly answer with the fisher, and the bake would write the fisher under a cast
        /// member's name. Rig 10 throws on a name it does not know, and also declares twenty NPCs the
        /// game does not bake yet (owner, 10-01: they come with the wardrobe), refused here by name.</summary>
        public static void AssertPreset9(IRigScriptHost host, string preset)
        {
            if (string.IsNullOrEmpty(preset)) throw new ArgumentNullException(nameof(preset));
            CharacterRigKit kit = KitOf(host);
            string g = kit.GlobalName;
            if (host.EvaluateBool($"{g}.{kit.PresetTable}.indexOf({Js(preset)})>=0")) return;
            if (kit.PresetTable != "CAST" && host.EvaluateBool($"{g}.CAST.indexOf({Js(preset)})>=0"))
                throw new ArgumentException(
                    $"'{preset}' is one of {kit.Name}'s NPCs, not one of its {kit.PresetTable}. The game " +
                    "bakes the presets only; the twenty NPCs wait for the wardrobe (owner, 10-01).");
            throw new ArgumentException(
                $"{g}.{kit.PresetTable} has no preset '{preset}'. Rig 9's normBuild falls back to " +
                "the fisher without a word, so this would bake the fisher under that name.");
        }

        // ---------------------------------------------------------------------------------------
        // The face at rest
        // ---------------------------------------------------------------------------------------

        /// <summary>
        /// <paramref name="preset"/>'s REST face: one group per face slot, the slot's FIRST group
        /// (<c>FACE_SLOTS[slot][0]</c>). Refuses a preset whose rest face
        /// (<c>baseIntent(buildOf(p).D).face</c>) is a different group, because then the def's
        /// <see cref="CharacterSkinDef.RestFace"/> would name a face that preset never rests on. (Until
        /// character PR 2a these were the only groups the bind mesh kept; it now keeps them all.)
        /// </summary>
        public static string[] DefaultFaceGroups9(IRigScriptHost host, string preset)
        {
            AssertPreset9(host, preset);
            string g = KitOf(host).GlobalName;
            string[] slots = SplitList(host.EvaluateString($"Object.keys({g}.FACE_SLOTS).join(',')"));
            if (slots.Length == 0)
                throw new InvalidOperationException($"{g}.FACE_SLOTS names no face slot.");
            var groups = new string[slots.Length];
            for (int i = 0; i < slots.Length; i++)
            {
                string first = host.EvaluateString($"String({g}.FACE_SLOTS[{Js(slots[i])}][0])");
                string rest = host.EvaluateString(
                    $"String({g}.baseIntent({g}.buildOf({Js(preset)}).D).face[{Js(slots[i])}])");
                if (!string.Equals(first, rest, StringComparison.Ordinal))
                    throw new InvalidOperationException(
                        $"'{preset}' rests its {slots[i]} on '{rest}' and FACE_SLOTS lists '{first}' " +
                        "first. The bind mesh keeps the first group of each slot, so it would wear a " +
                        "face this preset never rests on.");
                groups[i] = slots[i] + "." + first;
            }
            return groups;
        }

        /// <summary>The JS that yields <paramref name="preset"/>'s faces with its rest face only: every
        /// ungrouped face, plus each slot's rest group. The bind mesh until character PR 2a; still
        /// the rest-face half of the material audit.</summary>
        public static string DefaultFaceMeshJs9(IRigScriptHost host, string preset)
        {
            var sb = new StringBuilder(KitOf(host).GlobalName).Append(".bindMesh(").Append(Js(preset))
                .Append(").filter(function(f){return !f.group");
            foreach (string grp in DefaultFaceGroups9(host, preset))
                sb.Append("||f.group===").Append(Js(grp));
            return sb.Append(";})").ToString();
        }

        // ---------------------------------------------------------------------------------------
        // The face in full (character PR 2a): every group bound, one per slot drawn
        // ---------------------------------------------------------------------------------------

        /// <summary>
        /// Rig 9's face groups in its own <c>GROUP_ORDER</c>, checked against <c>FACE_SLOTS</c>: the
        /// slots are the def's (<see cref="CharacterSkinDef.FaceSlotNames"/>, in that order) and the
        /// order walks them in turn, each slot's states in its FACE_SLOTS order. Everywhere in the
        /// bake a group's id is 1 + its index here, and 0 is no group.
        /// </summary>
        public static string[] FaceGroupOrder9(IRigScriptHost host)
        {
            Load9(host);
            string g = KitOf(host).GlobalName;
            string[] order = SplitList(host.EvaluateString($"{g}.GROUP_ORDER.join(',')"));
            string[] slots = SplitList(host.EvaluateString($"Object.keys({g}.FACE_SLOTS).join(',')"));
            if (!SameList9(slots, CharacterSkinDef.FaceSlotNames))
                throw new InvalidOperationException(
                    $"{KitOf(host).Title}'s FACE_SLOTS are [{string.Join(", ", slots)}] and the def knows " +
                    $"[{string.Join(", ", CharacterSkinDef.FaceSlotNames)}], in that order. A face track " +
                    "is read slot by slot, so a slot the def does not know would be dropped from every frame.");
            var want = new List<string>();
            foreach (string s in slots)
                foreach (string st in SplitList(host.EvaluateString($"{g}.FACE_SLOTS[{Js(s)}].join(',')")))
                    want.Add(s + "." + st);
            if (!SameList9(order, want))
                throw new InvalidOperationException(
                    $"{KitOf(host).Title}'s GROUP_ORDER is [{string.Join(", ", order)}] and FACE_SLOTS walks " +
                    $"[{string.Join(", ", want)}]. The rig appends its face groups in GROUP_ORDER; the two " +
                    "disagreeing means one of them no longer says what the mesh holds.");
            if (order.Length > byte.MaxValue)
                throw new InvalidOperationException(
                    $"{KitOf(host).Title} has {order.Length} face groups and a face track stores a group in a byte.");
            return order;
        }

        /// <summary>
        /// The JS for <paramref name="preset"/>'s bind mesh since character PR 2a: EVERY face, every
        /// face group included (<see cref="AllFacesJs9(IRigScriptHost, string)"/>), once the rig's own
        /// group table (<c>mesh.groups</c>: [start, count] per group) is shown to say what its faces
        /// say: each <see cref="FaceGroupOrder9"/> group owns at least one face, the faces in its run
        /// are all its own, and no face outside the runs claims a group. The engine draws one group per
        /// slot from this one mesh.
        ///
        /// <para>Rig 10 names the groups that draw nothing on purpose (<c>FACE_EMPTY</c>: its flat brow
        /// is the fringe's edge). Such a group must own no face at all: it stays a group, so a clip can
        /// show it, and showing it draws nothing, as in the rig.</para>
        /// </summary>
        public static string FaceMeshJs9(IRigScriptHost host, string preset)
        {
            AssertPreset9(host, preset);
            FaceGroupOrder9(host);
            string g = KitOf(host).GlobalName;
            string faces = AllFacesJs9(host, preset);
            string bad = host.EvaluateString(
                "(function(){var B=" + g + ".buildOf(" + Js(preset) + "),F=" + faces + "," +
                "T=B.mesh.groups||{},GO=" + g + ".GROUP_ORDER,E=" + g + ".FACE_EMPTY||{},held=0,tagged=0;" +
                "for(var i=0;i<GO.length;i++){var r=T[GO[i]];" +
                "if(Object.prototype.hasOwnProperty.call(E,GO[i])){" +
                "if(r&&!(r.length===2&&r[1]===0))return GO[i]+' is one of FACE_EMPTY and its run is '+JSON.stringify(r);" +
                "continue;}" +
                "if(!r||r.length!==2||!(r[1]>0))return GO[i]+' owns no faces (its run is '+JSON.stringify(r)+')';" +
                "for(var k=r[0];k<r[0]+r[1];k++)if(!F[k]||F[k].group!==GO[i])" +
                "return 'face '+k+' sits in the run of '+GO[i]+' and belongs to '+(F[k]?F[k].group:'nothing');" +
                "held+=r[1];}" +
                "for(var e in E)if(Object.prototype.hasOwnProperty.call(E,e)&&GO.indexOf(e)<0)" +
                "return 'FACE_EMPTY names '+e+', which GROUP_ORDER does not list';" +
                "for(var j=0;j<F.length;j++)if(F[j].group)tagged++;" +
                "if(tagged!==held)return tagged+' faces name a group and the runs hold '+held;" +
                "return '';})()");
            if (bad.Length > 0)
                throw new InvalidOperationException(
                    $"'{preset}': {KitOf(host).Name}'s face groups do not hold together: {bad}. The def binds every " +
                    "group and draws one per slot; a group with no faces, or faces its table does not " +
                    "account for, would draw a face the rig never paints.");
            return faces;
        }

        /// <summary>The groups the host's rig names as drawing nothing on purpose (rig 10's
        /// <c>FACE_EMPTY</c>), in GROUP_ORDER's order; none on rig 9.</summary>
        public static string[] EmptyFaceGroups9(IRigScriptHost host)
        {
            Load9(host);
            string g = KitOf(host).GlobalName;
            return SplitList(host.EvaluateString(
                $"(function(){{var E={g}.FACE_EMPTY||{{}};return {g}.GROUP_ORDER.filter(function(k){{" +
                "return Object.prototype.hasOwnProperty.call(E,k);}).join(',');})()"));
        }

        /// <summary>Rig 9's face cull thresholds by role: <c>ROLE.near</c>, <c>.far</c> and
        /// <c>.side</c>'s <c>minT</c>, and the mouth's, which the rig writes as a literal on its mouth
        /// faces and names nowhere else, so it is read off those faces.</summary>
        public readonly struct FaceThresholds9
        {
            public readonly double Near, Far, Side, Mouth;

            public FaceThresholds9(double near, double far, double side, double mouth)
            {
                Near = near; Far = far; Side = side; Mouth = mouth;
            }

            /// <summary>No roles at all: rig 10, whose faces declare no <c>minT</c> and which has no
            /// <c>ROLE</c> table. Every face then culls at the floor alone (a mark also by its own turn
            /// band, <see cref="RigFace.MarkAz"/>), and the def's thresholds are all 0.</summary>
            public static readonly FaceThresholds9 None = new FaceThresholds9(0, 0, 0, 0);

            /// <summary>False for <see cref="None"/>.</summary>
            public bool HasRoles => Near != 0 || Far != 0 || Side != 0 || Mouth != 0;

            /// <summary>The def's <see cref="CharacterSkinDef.FaceMinToward"/>: x near, y far, z side, w mouth.</summary>
            public Vector4 ToVector4() => new Vector4((float)Near, (float)Far, (float)Side, (float)Mouth);

            public override string ToString() =>
                string.Format(CultureInfo.InvariantCulture, "near {0}, far {1}, side {2}, mouth {3}",
                              Near, Far, Side, Mouth);
        }

        /// <summary>
        /// <see cref="FaceThresholds9"/> for <paramref name="preset"/>. Refuses a mouth whose faces
        /// carry more than one threshold (the mesh carries a role, so they could not all be kept),
        /// and four thresholds that are not distinct values inside (0, 1): two roles at one value
        /// are one role, and 0 or 1 is a face that always or never draws.
        ///
        /// <para>Rig 10 has no <c>ROLE</c>: its face is point marks, each culled by its own turn band
        /// (<c>az</c>), and no face declares a <c>minT</c>. It reads as <see cref="FaceThresholds9.None"/>
        /// once every face is shown to declare none; a rig that kept ROLE beside the marks is refused,
        /// since then one of the two would be a rule the bake drops.</para>
        /// </summary>
        public static FaceThresholds9 ReadFaceThresholds9(IRigScriptHost host, string preset)
        {
            AssertPreset9(host, preset);
            CharacterRigKit kit = KitOf(host);
            string g = kit.GlobalName;
            if (kit.FaceMarks)
            {
                if (host.EvaluateBool($"{g}.ROLE!==undefined"))
                    throw new InvalidOperationException(
                        $"{kit.Title} draws its face as marks culled by their own turn band and still " +
                        "exports ROLE. The bake reads one of the two rules; re-read paint() first.");
                string declared = host.EvaluateString(
                    "(function(){var F=" + AllFacesJs9(host, preset) + ";for(var i=0;i<F.length;i++)" +
                    "if((F[i].minT||0)!==0)return 'face '+i+' ('+(F[i].group||F[i].part)+') declares minT '+F[i].minT;" +
                    "return '';})()");
                if (declared.Length > 0)
                    throw new InvalidOperationException(
                        $"'{preset}': {declared}. {kit.Title}'s faces cull at the floor alone and its marks by " +
                        "their turn band; the def has no role to carry a threshold.");
                return FaceThresholds9.None;
            }
            double Role(string role)
            {
                string s = $"{g}.ROLE[{Js(role)}]";
                if (!host.EvaluateBool($"!!{s}&&typeof {s}.minT==='number'&&isFinite({s}.minT)"))
                    throw new InvalidOperationException($"{KitOf(host).Title}'s ROLE.{role}.minT is not a finite number.");
                return host.EvaluateNumber($"{s}.minT");
            }
            string mouthSlot = CharacterSkinDef.FaceSlotNames[CharacterSkinDef.MouthSlot] + ".";
            string mouth = host.EvaluateString(
                "(function(){var F=" + AllFacesJs9(host, preset) + ",s={};" +
                "for(var i=0;i<F.length;i++){var f=F[i];if(f.group&&f.group.indexOf(" + Js(mouthSlot) + ")===0)" +
                "s[String(f.minT||0)]=1;}return Object.keys(s).join(',');})()");
            string[] mouths = SplitList(mouth);
            if (mouths.Length != 1)
                throw new InvalidOperationException(
                    $"'{preset}': {KitOf(host).Name}'s mouth faces carry the thresholds [{mouth}]; the def carries one " +
                    "for the mouth.");
            var t = new FaceThresholds9(Role("near"), Role("far"), Role("side"),
                                        Finite9(mouths[0], $"'{preset}' mouth minT"));
            double[] all = { t.Near, t.Far, t.Side, t.Mouth };
            for (int i = 0; i < all.Length; i++)
            {
                if (!(all[i] > 0 && all[i] < 1))
                    throw new InvalidOperationException($"'{preset}': a face threshold of {t} lies outside (0, 1).");
                for (int j = 0; j < i; j++)
                    if (all[i] == all[j])
                        throw new InvalidOperationException(
                            $"'{preset}': two face roles share a threshold ({t}); the mesh could not tell them apart.");
            }
            return t;
        }

        /// <summary>
        /// Resolve each face's <see cref="RigFace.MinToward"/> to its role (<see cref="RigFace.FaceRole"/>,
        /// as <see cref="CharacterSkinDef.FaceRole"/>). A face no group owns must declare no threshold
        /// (Body: it culls at the floor alone), a mouth face must carry the mouth's, and an eyes or brows
        /// face one of ROLE's three. Anything else is refused by face and value: the mesh carries a role,
        /// never a number, so a threshold no role has would be drawn at another role's. With
        /// <see cref="FaceThresholds9.None"/> (rig 10) every face is Body and must declare no threshold.
        /// </summary>
        public static void ResolveFaceRoles9(IList<RigFace> faces, string[] groupOrder, FaceThresholds9 t,
                                             string label)
        {
            string mouthSlot = CharacterSkinDef.FaceSlotNames[CharacterSkinDef.MouthSlot] + ".";
            for (int i = 0; i < faces.Count; i++)
            {
                RigFace f = faces[i];
                CharacterSkinDef.FaceRole role;
                if (!t.HasRoles)
                {
                    if (f.FaceGroup > groupOrder.Length)
                        throw new InvalidOperationException($"{label} face {i} names group {f.FaceGroup} of {groupOrder.Length}.");
                    if (f.MinToward != 0)
                        throw new InvalidOperationException(
                            $"{label} face {i} declares minT {f.MinToward} on a rig with no face roles; the " +
                            "def would cull it at the floor alone.");
                    role = CharacterSkinDef.FaceRole.Body;
                }
                else if (f.FaceGroup == 0)
                {
                    if (f.MinToward != 0)
                        throw new InvalidOperationException(
                            $"{label} face {i} belongs to no face group and declares minT {f.MinToward}. " +
                            "Only a face group's faces cull by role; the def has no role for it.");
                    role = CharacterSkinDef.FaceRole.Body;
                }
                else
                {
                    if (f.FaceGroup > groupOrder.Length)
                        throw new InvalidOperationException($"{label} face {i} names group {f.FaceGroup} of {groupOrder.Length}.");
                    string grp = groupOrder[f.FaceGroup - 1];
                    bool mouth = grp.StartsWith(mouthSlot, StringComparison.Ordinal);
                    if (mouth && f.MinToward == t.Mouth) role = CharacterSkinDef.FaceRole.Mouth;
                    else if (!mouth && f.MinToward == t.Near) role = CharacterSkinDef.FaceRole.Near;
                    else if (!mouth && f.MinToward == t.Far) role = CharacterSkinDef.FaceRole.Far;
                    else if (!mouth && f.MinToward == t.Side) role = CharacterSkinDef.FaceRole.Side;
                    else
                        throw new InvalidOperationException(
                            $"{label} face {i} ({grp}) culls at minT {f.MinToward}, which is none of its " +
                            $"roles' thresholds ({t}). The mesh carries a role, so it would cull at another.");
                }
                f.FaceRole = (int)role;
            }
        }

        /// <summary>
        /// The floor every face culls at, body faces included: rig 9's paint skips a face when
        /// <c>toward &lt;= Math.max(FLOOR, f.minT||0)</c>, and FLOOR is a literal there (1e-4), named
        /// nowhere else. Read off the rig's own source, which must hold that test exactly once, so
        /// the def carries the rig's bar rather than a copy of it.
        /// </summary>
        public static double CullFloor9() => CullFloor9(CharacterRigKit.Rig9);

        /// <summary>
        /// <see cref="CullFloor9()"/> for a kit whose paint holds its floor as a literal. A rig with
        /// marks culls them in a loop of their own, <c>if(-ny*C.ce+nz*C.se&lt;=FLOOR) continue;</c>,
        /// with a second literal; the def carries one floor, so the two must be the same number. A kit
        /// that exports its tolerances (rig 10.3) holds no literal there: its floor is read from a host
        /// holding it, <see cref="CullFloor9(IRigScriptHost)"/>.
        /// </summary>
        public static double CullFloor9(CharacterRigKit kit)
        {
            if (kit == null) throw new ArgumentNullException(nameof(kit));
            if (kit.ExportsNumbers)
                throw new InvalidOperationException(
                    $"{kit} culls at TOL.cull, which it exports; read the floor from a host holding it (CullFloor9(host)).");
            string source = ReadRepoText(kit.ScriptPath);
            double floor = OneLiteral9(kit, source, @"toward<=Math\.max\(([0-9.eE+-]+),\s*f\.minT\|\|0\)",
                                       "the face cull 'toward<=Math.max(<floor>, f.minT||0)'");
            if (kit.FaceMarks)
            {
                double marks = OneLiteral9(kit, source, @"if\(-ny\*C\.ce\+nz\*C\.se<=([0-9.eE+-]+)\)\s*continue;",
                                           "the mark cull 'if(-ny*C.ce+nz*C.se<=<floor>) continue;'");
                if (marks != floor)
                    throw new InvalidOperationException(
                        $"{kit.ScriptPath} culls its faces at {floor} and its marks at {marks}; the def carries " +
                        "one floor for both.");
            }
            return floor;
        }

        /// <summary>
        /// The floor every face culls at, for the kit <paramref name="host"/> holds: rig 10.3's
        /// <c>TOL.cull</c>, read in V8, its paint holding <c>toward&lt;=Math.max(TOL.cull, f.minT||0)</c>
        /// and the marks' <c>if(-ny*C.ce+nz*C.se&lt;=TOL.cull) continue;</c> once each, so the one floor
        /// the def carries is the one both culls read; rig 9's literal otherwise
        /// (<see cref="CullFloor9(CharacterRigKit)"/>).
        /// </summary>
        public static double CullFloor9(IRigScriptHost host)
        {
            Load9(host);
            CharacterRigKit kit = KitOf(host);
            if (!kit.ExportsNumbers) return CullFloor9(kit);
            string source = ReadRepoText(kit.ScriptPath);
            ReadsTol9(kit, source, @"toward<=Math\.max\(TOL\.cull,\s*f\.minT\|\|0\)",
                      "the face cull 'toward<=Math.max(TOL.cull, f.minT||0)'");
            ReadsTol9(kit, source, @"if\(-ny\*C\.ce\+nz\*C\.se<=TOL\.cull\)\s*continue;",
                      "the mark cull 'if(-ny*C.ce+nz*C.se<=TOL.cull) continue;'");
            return Tol9(host, "cull");
        }

        /// <summary>The two numbers rig 10's mark loop culls by: the shortest horizontal normal a mark
        /// may have before its turn band is not asked (<c>hh&lt;AZFLOOR</c> skips it) and how near a
        /// pixel edge its centre may fall before it draws nothing (<c>Math.abs(cx-Math.round(cx))&lt;EDGE</c>,
        /// the same for y). Rig 10.2 held both as literals; 10.3 reads the edge from
        /// <c>TOL.markEdge</c> and keeps the band floor a literal.</summary>
        public readonly struct MarkCull9
        {
            public readonly double AzFloor, Edge;

            public MarkCull9(double azFloor, double edge) { AzFloor = azFloor; Edge = edge; }
        }

        /// <summary><see cref="MarkCull9"/> for a kit whose paint holds both as literals; zeros for a
        /// kit without marks. A kit that exports its tolerances (rig 10.3) is read from a host holding
        /// it, <see cref="ReadMarkCull9(IRigScriptHost)"/>.</summary>
        public static MarkCull9 ReadMarkCull9(CharacterRigKit kit)
        {
            if (kit == null) throw new ArgumentNullException(nameof(kit));
            if (!kit.FaceMarks) return default;
            if (kit.ExportsNumbers)
                throw new InvalidOperationException(
                    $"{kit} keeps its marks off a pixel edge by TOL.markEdge, which it exports; read the floors " +
                    "from a host holding it (ReadMarkCull9(host)).");
            string source = ReadRepoText(kit.ScriptPath);
            double azFloor = MarkAzFloor9(kit, source);
            MatchCollection m = Regex.Matches(source,
                @"Math\.abs\(cx-Math\.round\(cx\)\)<([0-9.eE+-]+)\s*\|\|\s*Math\.abs\(cy-Math\.round\(cy\)\)<([0-9.eE+-]+)");
            if (m.Count != 1)
                throw new InvalidOperationException(
                    $"{kit.ScriptPath} holds the marks' pixel-edge test {m.Count} times, not once. Re-read paint().");
            double ex = Finite9(m[0].Groups[1].Value, $"{kit.Name}'s mark edge (x)");
            double ey = Finite9(m[0].Groups[2].Value, $"{kit.Name}'s mark edge (y)");
            if (ex != ey)
                throw new InvalidOperationException(
                    $"{kit.ScriptPath} tests a mark's centre against a pixel edge at {ex} across and {ey} down; " +
                    "the def carries one.");
            if (!(azFloor > 0) || !(ex > 0))
                throw new InvalidOperationException(
                    $"{kit.ScriptPath}'s mark literals are {azFloor} (turn band floor) and {ex} (pixel edge); both must be above 0.");
            return new MarkCull9(azFloor, ex);
        }

        /// <summary>
        /// <see cref="MarkCull9"/> for the kit <paramref name="host"/> holds; zeros for a kit without
        /// marks. Rig 10.3's paint reads the pixel edge from <c>TOL.markEdge</c> (for x and y, the test
        /// held there once), so that is read in V8; its band floor is still a literal in paint
        /// (<c>hh&lt;floor</c>, which TOL names no key for), read off the source as 10.2's was.
        /// </summary>
        public static MarkCull9 ReadMarkCull9(IRigScriptHost host)
        {
            Load9(host);
            CharacterRigKit kit = KitOf(host);
            if (!kit.FaceMarks) return default;
            if (!kit.ExportsNumbers) return ReadMarkCull9(kit);
            string source = ReadRepoText(kit.ScriptPath);
            double azFloor = MarkAzFloor9(kit, source);
            ReadsTol9(kit, source,
                      @"Math\.abs\(cx-Math\.round\(cx\)\)<TOL\.markEdge\s*\|\|\s*Math\.abs\(cy-Math\.round\(cy\)\)<TOL\.markEdge",
                      "the marks' pixel-edge test 'Math.abs(cx-Math.round(cx))<TOL.markEdge || …(cy)…<TOL.markEdge'");
            if (!(azFloor > 0))
                throw new InvalidOperationException(
                    $"{kit.ScriptPath}'s turn band floor is {azFloor}; it must be above 0.");
            return new MarkCull9(azFloor, Tol9(host, "markEdge"));
        }

        /// <summary>The marks' band floor: the one literal of
        /// <c>if(hh&lt;floor || -ny/hh&lt;=f.az) continue;</c> in the rig's paint.</summary>
        static double MarkAzFloor9(CharacterRigKit kit, string source) =>
            OneLiteral9(kit, source, @"if\(hh<([0-9.eE+-]+)\s*\|\|\s*-ny/hh<=f\.az\)\s*continue;",
                        "the turn band 'if(hh<<floor> || -ny/hh<=f.az) continue;'");

        static bool IsFinite9(double v) => !double.IsNaN(v) && !double.IsInfinity(v);

        static double OneLiteral9(CharacterRigKit kit, string source, string pattern, string what)
        {
            MatchCollection m = Regex.Matches(source, pattern);
            if (m.Count != 1)
                throw new InvalidOperationException(
                    $"{kit.ScriptPath} holds {what} {m.Count} times, not once. Re-read paint() before baking it.");
            return Finite9(m[0].Groups[1].Value, $"{kit.Name}'s {what}");
        }

        /// <summary>The one number <paramref name="pattern"/> captures, once, in every group it has: a
        /// site that holds a tolerance twice or three times (the inside test's three weights, a
        /// quantum's scale and its divisor) must hold one number there, above 0.</summary>
        static double SameLiteral9(CharacterRigKit kit, string source, string pattern, string what)
        {
            MatchCollection m = Regex.Matches(source, pattern);
            if (m.Count != 1)
                throw new InvalidOperationException(
                    $"{kit.ScriptPath} holds {what} {m.Count} times, not once. Re-read paint() before baking it.");
            GroupCollection g = m[0].Groups;
            double v = Finite9(g[1].Value, $"{kit.Name}'s {what}");
            for (int i = 2; i < g.Count; i++)
                if (Finite9(g[i].Value, $"{kit.Name}'s {what}") != v)
                    throw new InvalidOperationException(
                        $"{kit.ScriptPath} holds {what} with {g[i].Value} beside {g[1].Value}; the port reads one number there.");
            if (!(v > 0))
                throw new InvalidOperationException($"{kit.ScriptPath} holds {what} at {v}; a tolerance is above 0.");
            return v;
        }

        /// <summary>Hold that <paramref name="kit"/>'s paint reads a tolerance where the port does:
        /// <paramref name="pattern"/>, which names the <c>TOL</c> key, once in its source.</summary>
        static void ReadsTol9(CharacterRigKit kit, string source, string pattern, string what)
        {
            int n = Regex.Matches(source, pattern).Count;
            if (n != 1)
                throw new InvalidOperationException(
                    $"{kit.ScriptPath} holds {what} {n} times, not once. The port reads that TOL key where the " +
                    "rig's paint does; re-read paint() before baking it.");
        }

        // ---------------------------------------------------------------------------------------
        // The numbers the rig paints and gates with: rig 10.3's exports (TOL, DITHER), or rig 9's
        // paint literals and canonical dither
        // ---------------------------------------------------------------------------------------

        /// <summary>
        /// One of rig 10.3's tolerances, <c>TOL[key]</c>, read in V8: every tolerance its paint and its
        /// gates use, which its paint reads from there. A value that is not a finite number above 0, and
        /// a kit that exports no TOL, refuse: the bake never falls back to a copy.
        /// </summary>
        public static double Tol9(IRigScriptHost host, string key)
        {
            Load9(host);
            CharacterRigKit kit = KitOf(host);
            if (!kit.ExportsNumbers)
                throw new InvalidOperationException($"{kit} exports no TOL; its tolerances are its paint's own literals.");
            string text = host.EvaluateString(
                "(function(){var T=" + kit.GlobalName + ".TOL;if(!T||typeof T!=='object')return 'none';" +
                "var v=T[" + Js(key) + "];return typeof v==='number'?String(v):typeof v;})()");
            if (text == "none")
                throw new InvalidOperationException($"{kit.Title} exports no TOL, and {kit} should.");
            double v = Finite9(text, $"{kit.Title}'s TOL.{key}");
            if (!(v > 0))
                throw new InvalidOperationException($"{kit.Title}'s TOL.{key} is {text}; a tolerance is above 0.");
            return v;
        }

        /// <summary>The tolerance the host's rig gates its own checks on, metres: rig 10.3's
        /// <c>TOL.gate_m</c>; rig 9's <see cref="V9Tolerance"/>, since 9.2 exports none.</summary>
        public static double GateTolerance9(IRigScriptHost host)
        {
            Load9(host);
            return KitOf(host).ExportsNumbers ? Tol9(host, "gate_m") : V9Tolerance;
        }

        /// <summary>
        /// The tolerances the host's rig paints with, for <see cref="RigPaint9"/>: rig 10.3's
        /// <c>TOL.area</c>, <c>inside</c>, <c>depthScale</c>, <c>tieDepth</c> and <c>shadeScale</c>,
        /// read in V8, its paint holding each read where the port reads it (the marks' depth too); a
        /// rig that exports none, off the literals its paint holds at the same places, each once.
        /// </summary>
        public static RigPaint9.Tolerance PaintTolerance9(IRigScriptHost host)
        {
            Load9(host);
            CharacterRigKit kit = KitOf(host);
            string source = ReadRepoText(kit.ScriptPath);
            if (kit.ExportsNumbers)
            {
                ReadsTol9(kit, source, @"if\(Math\.abs\(area\)<TOL\.area\)\s*continue;",
                          "the triangle's area test 'if(Math.abs(area)<TOL.area) continue;'");
                ReadsTol9(kit, source, @"if\(w0<-TOL\.inside\|\|w1<-TOL\.inside\|\|w2<-TOL\.inside\)\s*continue",
                          "the pixel's inside test 'if(w0<-TOL.inside||w1<-TOL.inside||w2<-TOL.inside) continue'");
                ReadsTol9(kit, source, @"Math\.round\(\(d-db\)\*TOL\.depthScale\)/TOL\.depthScale",
                          "a face's depth quantum 'Math.round((d-db)*TOL.depthScale)/TOL.depthScale'");
                if (kit.FaceMarks)
                    ReadsTol9(kit, source, @"Math\.round\(\(cd-\(f\.db\|\|0\)\)\*TOL\.depthScale\)/TOL\.depthScale",
                              "a mark's depth quantum 'Math.round((cd-(f.db||0))*TOL.depthScale)/TOL.depthScale'");
                ReadsTol9(kit, source, @"SHADING\.form\*\(tw-SHADING\.formMid\)\)\*TOL\.shadeScale\)/TOL\.shadeScale",
                          "the shade's rounding '…SHADING.form*(tw-SHADING.formMid))*TOL.shadeScale)/TOL.shadeScale'");
                ReadsTol9(kit, source, @"dep\[j\]<sd-TOL\.tieDepth \|\| \(Math\.abs\(dep\[j\]-sd\)<=TOL\.tieDepth &&",
                          "the keyline's depth tie 'dep[j]<sd-TOL.tieDepth || (Math.abs(dep[j]-sd)<=TOL.tieDepth &&'");
                return new RigPaint9.Tolerance
                {
                    Area = Tol9(host, "area"),
                    Inside = Tol9(host, "inside"),
                    DepthScale = Tol9(host, "depthScale"),
                    TieDepth = Tol9(host, "tieDepth"),
                    ShadeScale = Tol9(host, "shadeScale"),
                };
            }
            double depth = SameLiteral9(kit, source, @"Math\.round\(\(d-db\)\*([0-9.eE+-]+)\)/([0-9.eE+-]+)",
                                        "a face's depth quantum 'Math.round((d-db)*<scale>)/<scale>'");
            if (kit.FaceMarks &&
                SameLiteral9(kit, source, @"Math\.round\(\(cd-\(f\.db\|\|0\)\)\*([0-9.eE+-]+)\)/([0-9.eE+-]+)",
                             "a mark's depth quantum 'Math.round((cd-(f.db||0))*<scale>)/<scale>'") != depth)
                throw new InvalidOperationException(
                    $"{kit.ScriptPath} quantises its faces' depth and its marks' by two numbers; the port reads one.");
            return new RigPaint9.Tolerance
            {
                Area = SameLiteral9(kit, source, @"if\(Math\.abs\(area\)<([0-9.eE+-]+)\)\s*continue;",
                                    "the triangle's area test 'if(Math.abs(area)<<area>) continue;'"),
                Inside = SameLiteral9(kit, source,
                                      @"if\(w0<-([0-9.eE+-]+)\|\|w1<-([0-9.eE+-]+)\|\|w2<-([0-9.eE+-]+)\)\s*continue",
                                      "the pixel's inside test 'if(w0<-<inside>||w1<-<inside>||w2<-<inside>) continue'"),
                DepthScale = depth,
                TieDepth = SameLiteral9(kit, source,
                                        @"dep\[j\]<sd-([0-9.eE+-]+) \|\| \(Math\.abs\(dep\[j\]-sd\)<=([0-9.eE+-]+) &&",
                                        "the keyline's depth tie 'dep[j]<sd-<tie> || (Math.abs(dep[j]-sd)<=<tie> &&'"),
                ShadeScale = SameLiteral9(kit, source,
                                          @"SHADING\.form\*\((?:toward|tw)-SHADING\.formMid\)\)\*([0-9.eE+-]+)\)/([0-9.eE+-]+)",
                                          "the shade's rounding '…SHADING.form*(toward-SHADING.formMid))*<scale>)/<scale>'"),
            };
        }

        /// <summary>How the def indexes the dither matrix, in the words rig 10.3's <c>DITHER.index</c>
        /// uses: [x, y], x the cell column (<see cref="RigMeshData.Bayer"/>, and the shader's
        /// <c>_Bayer</c>, whose row is x).</summary>
        const string Bayer9Index = "bayer4[x & 3][y & 3], x the cell column, y the cell row";

        /// <summary>
        /// The 4 × 4 dither thresholds a def carries (<see cref="RigMeshData.Bayer"/>, [x, y]): rig
        /// 10.3's <c>DITHER.bayer4</c> under its own <c>threshold</c> rule (both numbers of
        /// <c>(m + 0.5) / 16</c> read off the rule) and its <c>index</c> (<see cref="Bayer9Index"/>),
        /// read in V8; rig 9's canonical matrix, since 9.2 exports none. Rig 10 never dithers
        /// (<c>DITHER.used</c> is false, and the v9 tone rule rounds and never reads the matrix), so a
        /// rig that says it does is refused: the def would draw it undithered.
        /// </summary>
        public static double[,] Bayer9(IRigScriptHost host)
        {
            Load9(host);
            CharacterRigKit kit = KitOf(host);
            var bayer = new double[4, 4];
            if (!kit.ExportsNumbers)
            {
                for (int x = 0; x < 4; x++)
                    for (int y = 0; y < 4; y++)
                        bayer[x, y] = (RigMeshSymbols.CanonicalBayer[x, y] + 0.5) / 16.0;
                return bayer;
            }
            string[] p = host.EvaluateString(
                "(function(){var D=" + kit.GlobalName + ".DITHER;if(!D||typeof D!=='object')return 'none';" +
                "var b=D.bayer4,ok=Array.isArray(b)&&b.length===4&&b.every(function(r){return Array.isArray(r)&&" +
                "r.length===4&&r.every(function(m){return typeof m==='number'&&isFinite(m);});});" +
                "return [String(D.used),ok?b.map(function(r){return r.join(',');}).join(';'):'',String(D.index)," +
                "String(D.threshold)].join('|');})()").Split('|');
            if (p.Length == 1 && p[0] == "none")
                throw new InvalidOperationException($"{kit.Title} exports no DITHER, and {kit} should.");
            if (p.Length != 4)
                throw new InvalidOperationException($"{kit.Title}'s DITHER read as {p.Length} fields, not 4.");
            if (p[0] != "false")
                throw new InvalidOperationException(
                    $"{kit.Title}'s DITHER.used is {p[0]}. The v9 tone rule rounds every tone and never dithers, so " +
                    "a def could not draw what the rig does.");
            if (p[1].Length == 0)
                throw new InvalidOperationException($"{kit.Title}'s DITHER.bayer4 is not 4 x 4 numbers.");
            if (p[2] != Bayer9Index)
                throw new InvalidOperationException(
                    $"{kit.Title}'s DITHER.index reads '{p[2]}', and the def indexes the matrix as '{Bayer9Index}'. " +
                    "Re-read it before baking.");
            Match t = Regex.Match(p[3], @"^\(m \+ ([0-9.]+)\) / ([0-9]+)$");
            if (!t.Success)
                throw new InvalidOperationException(
                    $"{kit.Title}'s DITHER.threshold reads '{p[3]}', not '(m + <offset>) / <divisor>'. Re-read it before baking.");
            double offset = Finite9(t.Groups[1].Value, $"{kit.Title}'s DITHER.threshold offset");
            double divisor = Finite9(t.Groups[2].Value, $"{kit.Title}'s DITHER.threshold divisor");
            if (!(divisor > 0))
                throw new InvalidOperationException($"{kit.Title}'s DITHER.threshold divides by {divisor}.");
            string[] rows = p[1].Split(';');
            for (int x = 0; x < 4; x++)
            {
                string[] cells = rows[x].Split(',');
                for (int y = 0; y < 4; y++)
                    bayer[x, y] = (Finite9(cells[y], $"{kit.Title}'s DITHER.bayer4[{x}][{y}]") + offset) / divisor;
            }
            return bayer;
        }

        /// <summary>True when rig 9 declares its head snap (<c>SHADING.headSnap</c>, which paintSolved
        /// applies unless a caller passes <c>snapHead:false</c>).</summary>
        public static bool HeadSnap9(IRigScriptHost host)
        {
            string s = $"{KitOf(host).GlobalName}.SHADING.headSnap";
            return host.EvaluateBool($"typeof {s}==='string'&&{s}.length>0");
        }

        /// <summary>
        /// Whether the host's rig draws its keyline when a caller does not ask: rig 9 does (paint's
        /// <c>if(o.keyline!==false)</c>), rig 10 does not (<c>if(o.keyline===true)</c>, and its SHADING
        /// says so: <c>keylineDefault:false</c>). Read off the paint source, which must hold exactly one
        /// of the two tests, and held to <c>SHADING.keylineDefault</c> where the rig declares one. The
        /// game follows the rig (owner, 10-01, ruling K4): a figure baked from rig 10 draws no keyline.
        /// </summary>
        public static bool KeylineDefault9(IRigScriptHost host)
        {
            Load9(host);
            CharacterRigKit kit = KitOf(host);
            string source = ReadRepoText(kit.ScriptPath);
            int drawn = Regex.Matches(source, @"if\(o\.keyline!==false\)").Count;
            int asked = Regex.Matches(source, @"if\(o\.keyline===true\)").Count;
            if (drawn + asked != 1)
                throw new InvalidOperationException(
                    $"{kit.ScriptPath} holds 'if(o.keyline!==false)' {drawn} times and 'if(o.keyline===true)' " +
                    $"{asked} times; paint() should hold one of the two, once. Re-read it before baking the keyline.");
            bool byDefault = drawn == 1;
            string s = $"{kit.GlobalName}.SHADING.keylineDefault";
            if (host.EvaluateBool($"{s}!==undefined"))
            {
                if (!host.EvaluateBool($"typeof {s}==='boolean'"))
                    throw new InvalidOperationException($"{kit.Title}'s SHADING.keylineDefault is not true or false.");
                if (host.EvaluateBool(s) != byDefault)
                    throw new InvalidOperationException(
                        $"{kit.Title}'s SHADING.keylineDefault is {host.EvaluateBool(s)} and its paint draws the " +
                        $"keyline by default: {byDefault}. The def carries one of the two.");
            }
            return byDefault;
        }

        /// <summary>The build's head mid point in the head bone's frame (<c>buildOf(p).D.headMid</c>),
        /// metres: the point the head snap rounds and the eye lookAt aims from.</summary>
        public static Vector3d HeadMid9(IRigScriptHost host, string preset)
        {
            AssertPreset9(host, preset);
            string s = $"{KitOf(host).GlobalName}.buildOf({Js(preset)}).D.headMid";
            if (!host.EvaluateBool($"Array.isArray({s})&&{s}.length===3&&{s}.every(function(x){{return isFinite(x);}})"))
                throw new InvalidOperationException($"'{preset}': {KitOf(host).Name}'s D.headMid is not three finite numbers.");
            return new Vector3d(host.EvaluateNumber($"{s}[0]"), host.EvaluateNumber($"{s}[1]"),
                                host.EvaluateNumber($"{s}[2]"));
        }

        /// <summary><paramref name="preset"/>'s rest face as group ids, one per slot
        /// (<see cref="DefaultFaceGroups9"/>, which checks it against baseIntent).</summary>
        public static int[] RestFace9(IRigScriptHost host, string preset, string[] groupOrder)
        {
            string[] rest = DefaultFaceGroups9(host, preset);
            if (rest.Length != CharacterSkinDef.FaceSlots)
                throw new InvalidOperationException($"'{preset}' rests {rest.Length} face slots, not {CharacterSkinDef.FaceSlots}.");
            var ids = new int[rest.Length];
            for (int s = 0; s < rest.Length; s++)
            {
                ids[s] = Array.IndexOf(groupOrder, rest[s]) + 1;
                if (ids[s] == 0)
                    throw new InvalidOperationException($"'{preset}' rests on '{rest[s]}', which GROUP_ORDER does not list.");
            }
            return ids;
        }

        // ---------------------------------------------------------------------------------------
        // Blink and look (character PR 2a): read off the rig, checked against its sidecar
        // ---------------------------------------------------------------------------------------

        /// <summary>Rig 9's BLINK in the def's units: group ids and seconds.</summary>
        public sealed class Blink9
        {
            public CharacterSkinDef.BlinkStep[] Steps;
            public Vector2 IntervalSeconds;
            public float DoubleChance;
            public float DoubleGapSeconds;
            public int[] SkipGroups;

            public override string ToString()
            {
                var sb = new StringBuilder();
                foreach (CharacterSkinDef.BlinkStep s in Steps)
                    sb.Append(sb.Length == 0 ? "" : ", ").Append(s.Group.ToString(CultureInfo.InvariantCulture))
                      .Append('@').Append(s.Seconds.ToString("0.###", CultureInfo.InvariantCulture)).Append('s');
                return string.Format(CultureInfo.InvariantCulture,
                    "steps [{0}], every {1}-{2} s, double {3} after {4} s, skips {5} group(s)",
                    sb, IntervalSeconds.x, IntervalSeconds.y, DoubleChance, DoubleGapSeconds, SkipGroups.Length);
            }
        }

        /// <summary>
        /// Rig 9's <c>BLINK</c>, read and checked against itself: its slot is the eyes; each step names
        /// the eyes and a positive time and nothing else; the steps, played at <c>ms</c> a frame, are
        /// exactly its <c>frames</c> (the four-frame clip the sidecar exports); the interval is two
        /// positive times in order; the double chance lies in [0, 1] and its gap is not negative; and
        /// <c>skipIf</c> names eyes states only. Every state becomes its eyes group id.
        /// </summary>
        public static Blink9 ReadBlink9(IRigScriptHost host)
        {
            Load9(host);
            string[] order = FaceGroupOrder9(host);
            string eyes = CharacterSkinDef.FaceSlotNames[CharacterSkinDef.EyesSlot];
            string g = KitOf(host).GlobalName;
            string row = host.EvaluateString(
                "(function(){var K=" + g + ".BLINK;function n(x){return typeof x==='number'&&isFinite(x)?String(x):'NaN';}" +
                "return [String(K.slot),(K.steps||[]).map(function(s){return String(s[K.slot])+':'+n(s.ms)+':'+Object.keys(s).length;}).join(',')," +
                "(K.frames||[]).join(','),n(K.ms),(K.interval_ms||[]).map(n).join(','),n(K.doubleChance),n(K.doubleGap_ms)," +
                "((K.skipIf||{})[K.slot]||[]).join(','),Object.keys(K.skipIf||{}).join(',')].join('|');})()");
            string[] p = row.Split('|');
            if (p.Length != 9)
                throw new InvalidOperationException($"{KitOf(host).Title}'s BLINK read as {p.Length} fields, not 9.");
            if (p[0] != eyes)
                throw new InvalidOperationException($"{KitOf(host).Title}'s BLINK plays on the '{p[0]}' slot; the def blinks the {eyes}.");

            int Group(string state, string where)
            {
                int id = Array.IndexOf(order, eyes + "." + state) + 1;
                if (id == 0)
                    throw new InvalidOperationException($"{KitOf(host).Title}'s BLINK {where} names '{state}', which is no {eyes} state.");
                return id;
            }

            double frameMs = Finite9(p[3], "BLINK.ms");
            string[] stepRows = SplitList(p[1]);
            if (stepRows.Length == 0) throw new InvalidOperationException($"{KitOf(host).Title}'s BLINK has no steps.");
            var steps = new CharacterSkinDef.BlinkStep[stepRows.Length];
            var played = new List<string>();
            for (int i = 0; i < stepRows.Length; i++)
            {
                string[] s = stepRows[i].Split(':');
                if (s.Length != 3 || s[2] != "2")
                    throw new InvalidOperationException(
                        $"{KitOf(host).Title}'s BLINK step {i} ('{stepRows[i]}') is not one {eyes} state and a time.");
                double ms = Finite9(s[1], $"BLINK.steps[{i}].ms");
                if (!(ms > 0)) throw new InvalidOperationException($"{KitOf(host).Title}'s BLINK step {i} lasts {ms} ms.");
                steps[i] = new CharacterSkinDef.BlinkStep { Group = Group(s[0], $"step {i}"), Seconds = (float)(ms / 1000.0) };
                double frames = ms / frameMs;
                if (frames != Math.Floor(frames))
                    throw new InvalidOperationException(
                        $"{KitOf(host).Title}'s BLINK step {i} lasts {ms} ms, not a whole number of its {frameMs} ms frames.");
                for (int k = 0; k < (int)frames; k++) played.Add(s[0]);
            }
            string[] frameStates = SplitList(p[2]);
            if (!SameList9(played.ToArray(), frameStates))
                throw new InvalidOperationException(
                    $"{KitOf(host).Title}'s BLINK steps play [{string.Join(", ", played)}] at {frameMs} ms a frame and its " +
                    $"frames say [{string.Join(", ", frameStates)}]. The two are one blink; they must agree.");

            string[] iv = SplitList(p[4]);
            if (iv.Length != 2) throw new InvalidOperationException($"{KitOf(host).Title}'s BLINK.interval_ms is [{p[4]}], not two times.");
            double a = Finite9(iv[0], "BLINK.interval_ms[0]"), b = Finite9(iv[1], "BLINK.interval_ms[1]");
            if (!(a > 0) || b < a)
                throw new InvalidOperationException($"{KitOf(host).Title}'s BLINK.interval_ms is [{a}, {b}]; the def needs 0 < a <= b.");
            double chance = Finite9(p[5], "BLINK.doubleChance"), gap = Finite9(p[6], "BLINK.doubleGap_ms");
            if (chance < 0 || chance > 1) throw new InvalidOperationException($"{KitOf(host).Title}'s BLINK.doubleChance is {chance}.");
            if (gap < 0) throw new InvalidOperationException($"{KitOf(host).Title}'s BLINK.doubleGap_ms is {gap}.");
            string[] skipKeys = SplitList(p[8]);
            foreach (string k in skipKeys)
                if (k != eyes)
                    throw new InvalidOperationException(
                        $"{KitOf(host).Title}'s BLINK.skipIf names the '{k}' slot; the def skips on the {eyes} alone.");
            string[] skip = SplitList(p[7]);
            var skipIds = new int[skip.Length];
            for (int i = 0; i < skip.Length; i++) skipIds[i] = Group(skip[i], "skipIf");

            return new Blink9
            {
                Steps = steps,
                IntervalSeconds = new Vector2((float)(a / 1000.0), (float)(b / 1000.0)),
                DoubleChance = (float)chance,
                DoubleGapSeconds = (float)(gap / 1000.0),
                SkipGroups = skipIds,
            };
        }

        /// <summary>Rig 9's LOOK and lookAt in the def's units: bone indices, degrees, group ids.</summary>
        public sealed class Look9
        {
            public int Neck, Head, Chest;
            public float SplitNeck, SplitHead;
            public Vector2 Yaw, Pitch;
            public float HeadShare, EyesBeyondDeg;
            /// <summary>x open, y left, z right.</summary>
            public Vector3Int Eyes;

            public override string ToString() => string.Format(CultureInfo.InvariantCulture,
                "bones neck {0} / head {1} (chest {2}), split {3}/{4}, yaw {5}..{6}, pitch {7}..{8}, " +
                "head share {9}, eyes past {10} deg (groups {11}/{12}/{13})",
                Neck, Head, Chest, SplitNeck, SplitHead, Yaw.x, Yaw.y, Pitch.x, Pitch.y, HeadShare,
                EyesBeyondDeg, Eyes.x, Eyes.y, Eyes.z);
        }

        /// <summary>The bone rig 9's <c>lookAt</c> measures its target in, <c>ix.chest</c>. The rig
        /// names it in lookAt's code and in the look contract's prose ("both in the chest frame"),
        /// not in LOOK; the look's V8 grid test fails if it moves.</summary>
        public const string V9LookFrameBone = "chest";

        /// <summary>
        /// Rig 9's <c>LOOK</c> for <paramref name="preset"/>'s skeleton, checked: its two bones exist,
        /// the second is the head its snap and lookAt use (<c>sk.ix.head</c>), its split names exactly
        /// those two, its limits are ordered pairs through 0, its head share lies in (0, 1] and its
        /// eyes threshold is not negative. The gaze groups come from <c>lookContract().eyes</c>.
        /// </summary>
        public static Look9 ReadLook9(IRigScriptHost host, string preset, RigBone[] bones)
        {
            AssertPreset9(host, preset);
            string[] order = FaceGroupOrder9(host);
            string g = KitOf(host).GlobalName;
            string row = host.EvaluateString(
                "(function(){var L=" + g + ".LOOK,E=" + g + ".lookContract().eyes||{};" +
                "function n(x){return typeof x==='number'&&isFinite(x)?String(x):'NaN';}" +
                "return [(L.bones||[]).join(','),Object.keys(L.split||{}).join(','),n((L.split||{})[(L.bones||[])[0]])," +
                "n((L.split||{})[(L.bones||[])[1]]),(L.yaw||[]).map(n).join(','),(L.pitch||[]).map(n).join(',')," +
                "n(L.headShare),n(L.eyesBeyond_deg),String(E.centre),String(E.left),String(E.right)," +
                "String(" + g + ".buildOf(" + Js(preset) + ").sk.ix.head)].join('|');})()");
            string[] p = row.Split('|');
            if (p.Length != 12) throw new InvalidOperationException($"{KitOf(host).Title}'s LOOK read as {p.Length} fields, not 12.");

            string[] lookBones = SplitList(p[0]);
            if (lookBones.Length != 2)
                throw new InvalidOperationException($"{KitOf(host).Title}'s LOOK turns [{p[0]}]; the def splits a turn onto a neck and a head.");
            string[] splitKeys = SplitList(p[1]);
            Array.Sort(splitKeys, StringComparer.Ordinal);
            string[] sortedBones = (string[])lookBones.Clone();
            Array.Sort(sortedBones, StringComparer.Ordinal);
            if (!SameList9(splitKeys, sortedBones))
                throw new InvalidOperationException($"{KitOf(host).Title}'s LOOK.split names [{p[1]}] and LOOK.bones [{p[0]}].");

            int Bone(string id)
            {
                for (int i = 0; i < bones.Length; i++)
                    if (string.Equals(bones[i].Id, id, StringComparison.Ordinal)) return i;
                throw new InvalidOperationException($"'{preset}' has no bone '{id}', which {KitOf(host).Name}'s look names.");
            }

            var look = new Look9
            {
                Neck = Bone(lookBones[0]),
                Head = Bone(lookBones[1]),
                Chest = Bone(V9LookFrameBone),
                SplitNeck = (float)Finite9(p[2], "LOOK.split[neck]"),
                SplitHead = (float)Finite9(p[3], "LOOK.split[head]"),
                HeadShare = (float)Finite9(p[6], "LOOK.headShare"),
                EyesBeyondDeg = (float)Finite9(p[7], "LOOK.eyesBeyond_deg"),
            };
            if (look.Head != Int9(p[11], "sk.ix.head"))
                throw new InvalidOperationException(
                    $"{KitOf(host).Title}'s LOOK turns '{lookBones[1]}' as its head and the rig snaps bone {p[11]} as the head.");
            Vector2 Limits(string s, string what)
            {
                string[] v = SplitList(s);
                if (v.Length != 2) throw new InvalidOperationException($"{KitOf(host).Title}'s LOOK.{what} is [{s}], not two limits.");
                double lo = Finite9(v[0], $"LOOK.{what}[0]"), hi = Finite9(v[1], $"LOOK.{what}[1]");
                if (!(lo <= 0 && 0 <= hi))
                    throw new InvalidOperationException($"{KitOf(host).Title}'s LOOK.{what} is [{lo}, {hi}], which does not hold 0.");
                return new Vector2((float)lo, (float)hi);
            }
            look.Yaw = Limits(p[4], "yaw");
            look.Pitch = Limits(p[5], "pitch");
            if (!(look.HeadShare > 0 && look.HeadShare <= 1))
                throw new InvalidOperationException($"{KitOf(host).Title}'s LOOK.headShare is {look.HeadShare}.");
            if (look.EyesBeyondDeg < 0)
                throw new InvalidOperationException($"{KitOf(host).Title}'s LOOK.eyesBeyond_deg is {look.EyesBeyondDeg}.");

            string eyes = CharacterSkinDef.FaceSlotNames[CharacterSkinDef.EyesSlot] + ".";
            int Gaze(string grp, string what)
            {
                int id = grp.StartsWith(eyes, StringComparison.Ordinal) ? Array.IndexOf(order, grp) + 1 : 0;
                if (id == 0)
                    throw new InvalidOperationException($"{KitOf(host).Title}'s look contract names '{grp}' for its {what} gaze, which is no eyes group.");
                return id;
            }
            look.Eyes = new Vector3Int(Gaze(p[8], "centre"), Gaze(p[9], "left"), Gaze(p[10], "right"));
            return look;
        }

        /// <summary>
        /// The committed gameplay sidecar's <c>overlays</c> against the rig's own <c>blinkClip()</c>
        /// and <c>lookContract()</c> today, value for value: the sidecar is what an engine that never
        /// runs the rig would read, so the def's blink and look must be the ones it states. Throws on
        /// the first difference, by path.
        /// </summary>
        public static void AssertOverlaysMatchSidecar9(IRigScriptHost host, string preset)
        {
            AssertPreset9(host, preset);
            CharacterRigKit kit = KitOf(host);
            string file = kit.GameplayFile(preset);
            string text = ReadKitText9(kit.KitRoot, file);
            string g = kit.GlobalName;
            host.Execute("globalThis.__hhSide9=JSON.parse(" + JsText9(text) + ");");
            try
            {
                string diff = host.EvaluateString(
                    "(function(){function eq(a,b,p){if(a===b)return '';" +
                    "if(typeof a!=='object'||typeof b!=='object'||a===null||b===null)" +
                    "return p+': the rig has '+JSON.stringify(a)+', the sidecar '+JSON.stringify(b);" +
                    "if(Array.isArray(a)!==Array.isArray(b))return p+': an array against an object';" +
                    "var ka=Object.keys(a).sort(),kb=Object.keys(b).sort();" +
                    "if(ka.join(',')!==kb.join(','))return p+': keys ['+ka.join(',')+'] against ['+kb.join(',')+']';" +
                    "for(var i=0;i<ka.length;i++){var r=eq(a[ka[i]],b[ka[i]],p+'.'+ka[i]);if(r)return r;}return '';}" +
                    "var O=(globalThis.__hhSide9||{}).overlays||{};" +
                    "return eq(JSON.parse(JSON.stringify(" + g + ".blinkClip())),O.blink,'overlays.blink')||" +
                    "eq(JSON.parse(JSON.stringify(" + g + ".lookContract())),O.look,'overlays.look');})()");
                if (diff.Length > 0)
                    throw new InvalidOperationException(
                        $"{file} does not state {KitOf(host).Name}'s blink and look: {diff}. The def bakes the rig's; a " +
                        "sidecar that says otherwise is stale, and re-generating it is the kit's job, not the bake's.");
            }
            finally { host.Execute("delete globalThis.__hhSide9;"); }
        }

        /// <summary>A JS string literal of any text: backslash, quote, line breaks and the two JS line
        /// separators escaped, so JSON text of any size can be handed to JSON.parse.</summary>
        static string JsText9(string s)
        {
            var sb = new StringBuilder(s.Length + 16).Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '\\': sb.Append("\\\\"); break;
                    case '"': sb.Append("\\\""); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\u2028': sb.Append("\\u2028"); break;
                    case '\u2029': sb.Append("\\u2029"); break;
                    default: sb.Append(c); break;
                }
            }
            return sb.Append('"').ToString();
        }

        // ---------------------------------------------------------------------------------------
        // Skeleton
        // ---------------------------------------------------------------------------------------

        /// <summary>
        /// Rig 9's skeleton for <paramref name="preset"/>, from ONE <c>skeleton(p)</c> call, into
        /// rig 7's <see cref="RigBone"/>: <c>rest</c> is local to the parent and <c>bind</c> is the
        /// world pose. The checks are rig 7's: the ids agree with the packed count, every parent is
        /// an earlier index, and bone 0 is the root.
        /// </summary>
        public static RigBone[] ReadSkeleton9(IRigScriptHost host, string preset)
        {
            AssertPreset9(host, preset);
            string g = KitOf(host).GlobalName;
            host.Execute($"globalThis.__hhSkel9={g}.skeleton({Js(preset)});");
            string[] ids = SplitList(host.EvaluateString(
                "globalThis.__hhSkel9.map(function(b){return String(b.id);}).join(',')"));

            // [i32 n] then per bone [i32 parent][f64 rest.pos x3][f64 rest.rot x4]
            //                       [f64 bind.pos x3][f64 bind.rot x4]
            host.Execute(
                "globalThis.__hhSkel9Pack=(function(){var S=globalThis.__hhSkel9;" +
                "var buf=new ArrayBuffer(4+S.length*(4+14*8));var dv=new DataView(buf);var p=0;" +
                "function put(a,n,what){if(!a||a.length!==n)throw new Error(what+' has '+" +
                "(a?a.length:'no')+' components, not '+n);" +
                "for(var k=0;k<n;k++){dv.setFloat64(p,a[k],true);p+=8;}}" +
                "dv.setInt32(p,S.length,true);p+=4;" +
                "for(var i=0;i<S.length;i++){var b=S[i];" +
                "if(typeof b.parent!=='number'||b.parent!==Math.floor(b.parent))" +
                "throw new Error('bone '+b.id+' names parent '+b.parent+', which is not an index');" +
                "dv.setInt32(p,b.parent,true);p+=4;" +
                "put(b.rest&&b.rest.pos,3,b.id+' rest.pos');put(b.rest&&b.rest.rot,4,b.id+' rest.rot');" +
                "put(b.bind&&b.bind.pos,3,b.id+' bind.pos');put(b.bind&&b.bind.rot,4,b.id+' bind.rot');}" +
                "return new Uint8ClampedArray(buf);})();");
            byte[] blob = host.EvaluateBytes("globalThis.__hhSkel9Pack");

            int off = 0;
            int n = BitConverter.ToInt32(blob, off); off += 4;
            if (n != ids.Length)
                throw new InvalidOperationException(
                    $"{KitOf(host).Title}'s skeleton('{preset}') packed {n} bones and named {ids.Length}.");
            var bones = new RigBone[n];
            for (int i = 0; i < n; i++)
            {
                var b = new RigBone { Id = ids[i], Parent = BitConverter.ToInt32(blob, off) };
                off += 4;
                b.RestPos = ReadV3(blob, ref off);
                b.RestRx = D(blob, ref off); b.RestRy = D(blob, ref off);
                b.RestRz = D(blob, ref off); b.RestRw = D(blob, ref off);
                b.WorldPos = ReadV3(blob, ref off);
                b.WorldRx = D(blob, ref off); b.WorldRy = D(blob, ref off);
                b.WorldRz = D(blob, ref off); b.WorldRw = D(blob, ref off);
                if (b.Parent >= i || b.Parent < -1)
                    throw new InvalidOperationException(
                        $"{KitOf(host).Title} bone {i} '{b.Id}' names parent {b.Parent}, which is not an earlier " +
                        "index. The def walks parents before children and would read an unset pose.");
                bones[i] = b;
            }
            if (off != blob.Length)
                throw new InvalidOperationException(
                    $"{KitOf(host).Title}'s skeleton blob was {blob.Length} bytes and {off} were read: packer and reader disagree.");
            if (n == 0 || bones[0].Parent != -1)
                throw new InvalidOperationException($"{KitOf(host).Title}'s bone 0 for '{preset}' is not the root.");
            return bones;
        }

        // ---------------------------------------------------------------------------------------
        // Materials and faces
        // ---------------------------------------------------------------------------------------

        /// <summary>
        /// The painted materials the faces of <paramref name="facesJs"/> use, in the order rig 9's
        /// shading contract declares them. This is the count the engine pays one ramp slot for
        /// each, fixed colours included. Refuses, by name, a face whose material the contract does
        /// not declare, a ramped material with no tone window (lo or hi missing), a window the def
        /// cannot carry (lo below 0 or hi below lo) and a gain or bias that is not a finite number.
        /// Never invents one: a default here would recolour a character without a word.
        /// </summary>
        public static List<RigMaterial9> ReadMaterials9(IRigScriptHost host, string preset, string facesJs)
        {
            AssertPreset9(host, preset);
            string g = KitOf(host).GlobalName;
            string rows = host.EvaluateString(
                "(function(){var C=" + g + ".shadingContract(" + Js(preset) + ").materials;" +
                "var F=" + facesJs + ";var used={};for(var i=0;i<F.length;i++)used[F[i].mat]=1;" +
                "var stray=Object.keys(used).filter(function(k){" +
                "return !Object.prototype.hasOwnProperty.call(C,k);});" +
                "if(stray.length)return '!'+stray.join(',');" +
                "function s(x){return x==null?'':String(x);}" +
                "return Object.keys(C).filter(function(k){return used[k];}).map(function(k){var c=C[k];" +
                "return [k,c.fixed?1:0,c.fixed?s(c.color):(c.ramp||[]).join(','),s(c.off),s(c.gain)," +
                "s(c.bias),s(c.lo),s(c.hi)].join('|');}).join('\\n');})()");
            if (rows.StartsWith("!", StringComparison.Ordinal))
                throw new InvalidOperationException(
                    $"'{preset}' paints faces with {rows.Substring(1)}, which its shading contract does " +
                    "not declare. A face with no declared material has no colour the engine could give it.");

            var list = new List<RigMaterial9>();
            foreach (string row in rows.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string[] p = row.Split('|');
                if (p.Length != 8)
                    throw new InvalidOperationException($"{KitOf(host).Title} material row '{row}' has {p.Length} fields, not 8.");
                var m = new RigMaterial9 { Name = p[0], Fixed = p[1] == "1" };
                string where = $"'{preset}' material '{m.Name}'";
                if (m.Fixed)
                {
                    m.RampHex = new[] { p[2] };
                    m.Ramp = new[] { ParseHex9(p[2], where + " colour") };
                }
                else
                {
                    m.RampHex = p[2].Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                    if (m.RampHex.Length == 0)
                        throw new InvalidOperationException($"{where} has an empty ramp.");
                    m.Ramp = new Color32[m.RampHex.Length];
                    for (int k = 0; k < m.Ramp.Length; k++)
                        m.Ramp[k] = ParseHex9(m.RampHex[k], $"{where} ramp[{k}]");
                    m.Off = p[3].Length == 0 ? 0 : Int9(p[3], where + " off");
                    m.Gain = Finite9(p[4], where + " gain");
                    m.Bias = Finite9(p[5], where + " bias");
                    if (p[6].Length == 0 || p[7].Length == 0)
                        throw new InvalidOperationException(
                            $"{where} has no tone window (lo '{p[6]}', hi '{p[7]}'). The v9 tone rule " +
                            "clamps every step to lo..hi; the bake refuses rather than invent one.");
                    m.Lo = Int9(p[6], where + " lo");
                    m.Hi = Int9(p[7], where + " hi");
                    if (m.Lo < 0 || m.Hi < m.Lo)
                        throw new InvalidOperationException(
                            $"{where} has the tone window {m.Lo}..{m.Hi}; the def carries 0 <= lo <= hi.");
                }
                list.Add(m);
            }
            return list;
        }

        /// <summary>
        /// The faces of <paramref name="facesJs"/>, with each material as an index into
        /// <paramref name="materials"/>, and each face's rig 9 tags: its face group
        /// (<see cref="RigFace.FaceGroup"/>, 1 + its index in GROUP_ORDER), its threshold
        /// (<see cref="RigFace.MinToward"/>) and its head flag (<see cref="RigFace.Head"/>: a posed face's
        /// own <c>head</c>, or on a bind face <c>bone[0][0][0] === ix.head</c> of
        /// <paramref name="preset"/>). The same packed layout as rig 6's pose reader, plus the tags.
        ///
        /// <para>And rig 10's: a point mark (<see cref="RigFace.Mark"/>, <c>pt</c>) with its turn band
        /// (<see cref="RigFace.MarkAz"/>, <c>az</c>) and whether it draws over the hair
        /// (<see cref="RigFace.OverHair"/>, <c>oh</c>); whether a mark may draw over the face's part
        /// (<see cref="RigFace.UnderMark"/>, <c>PT_UNDER[part]</c>) and whether that part is the hair;
        /// and the face's smooth normal (<see cref="RigFace.SmoothNormal"/>, <c>sn</c>). A mark's band
        /// must lie inside (0, 1): the mesh stores it where 0 means no band, and 1 or more never draws.</para>
        /// </summary>
        public static List<RigFace> ReadFaces9(IRigScriptHost host, string facesJs,
                                               List<RigMaterial9> materials, string label,
                                               string preset = null)
        {
            var matOrder = new StringBuilder();
            foreach (RigMaterial9 m in materials) matOrder.Append(Js(m.Name)).Append(',');
            string g = KitOf(host).GlobalName;
            string headIx = preset == null ? "-1" : $"{g}.buildOf({Js(preset)}).sk.ix.head";

            // [i32 n] then per face [i32 nv][i32 mi][f64 b][f64 db][i32 group][i32 head][f64 minT]
            //                       [i32 marks][f64 az][f64 sn x,y,z][f64 x,y,z] x nv
            // marks: 1 a point mark (pt), 2 it draws over the hair too (oh), 4 a mark may draw over its
            // part (PT_UNDER[part], the rig's own truthy test), 8 its part is the hair, 16 it carries a
            // smooth normal (sn). az and sn read NaN where the face has none. Rig 9 exports no PT_UNDER
            // and its faces carry no pt, oh, az or sn: they read 0 (8 on its hair), NaN and NaN.
            host.Execute(
                "globalThis.__hhFaces9Pack=(function(){var F=" + facesJs + ";" +
                "var order=[" + matOrder.ToString().TrimEnd(',') + "];" +
                "var ix={};order.forEach(function(n,i){ix[n]=i;});" +
                "var GO=" + g + ".GROUP_ORDER,HIX=" + headIx + ",PU=" + g + ".PT_UNDER||{};" +
                "var n=0;for(var i=0;i<F.length;i++)n+=F[i].v.length*3;" +
                "var buf=new ArrayBuffer(4+F.length*(8+16+16+4+32)+n*8);var dv=new DataView(buf);var p=0;" +
                "dv.setInt32(p,F.length,true);p+=4;" +
                "for(var i=0;i<F.length;i++){var f=F[i];" +
                "var mi=ix[f.mat];if(mi==null)throw new Error('face '+i+' uses material '+f.mat+" +
                "' which is not in this mesh\\'s own material list');" +
                "var gi=0;if(f.group){gi=GO.indexOf(f.group)+1;" +
                "if(gi<1)throw new Error('face '+i+' names the group '+f.group+', which GROUP_ORDER does not list');}" +
                "var hd=typeof f.head==='boolean'?f.head:" +
                "(HIX>=0&&f.bone&&f.bone[0]&&f.bone[0][0]?f.bone[0][0][0]===HIX:false);" +
                "var mk=(f.pt?1:0)|(f.oh?2:0)|(PU[f.part]?4:0)|(f.part==='hair'?8:0)|(f.sn?16:0);" +
                "dv.setInt32(p,f.v.length,true);p+=4;dv.setInt32(p,mi,true);p+=4;" +
                "dv.setFloat64(p,f.b||0,true);p+=8;dv.setFloat64(p,f.db||0,true);p+=8;" +
                "dv.setInt32(p,gi,true);p+=4;dv.setInt32(p,hd?1:0,true);p+=4;dv.setFloat64(p,f.minT||0,true);p+=8;" +
                "dv.setInt32(p,mk,true);p+=4;dv.setFloat64(p,f.az==null?NaN:f.az,true);p+=8;" +
                "for(var c=0;c<3;c++){dv.setFloat64(p,f.sn?f.sn[c]:NaN,true);p+=8;}" +
                "for(var k=0;k<f.v.length;k++){var v=f.v[k];" +
                "dv.setFloat64(p,v[0],true);p+=8;dv.setFloat64(p,v[1],true);p+=8;" +
                "dv.setFloat64(p,v[2],true);p+=8;}}" +
                "return new Uint8ClampedArray(buf);})();");
            byte[] blob = host.EvaluateBytes("globalThis.__hhFaces9Pack");

            int off = 0;
            int faceCount = BitConverter.ToInt32(blob, off); off += 4;
            var faces = new List<RigFace>(faceCount);
            for (int i = 0; i < faceCount; i++)
            {
                int nv = BitConverter.ToInt32(blob, off); off += 4;
                int mat = BitConverter.ToInt32(blob, off); off += 4;
                double b = D(blob, ref off);
                double db = D(blob, ref off);
                int group = BitConverter.ToInt32(blob, off); off += 4;
                bool head = BitConverter.ToInt32(blob, off) != 0; off += 4;
                double minT = D(blob, ref off);
                int marks = BitConverter.ToInt32(blob, off); off += 4;
                double az = D(blob, ref off);
                Vector3d sn = ReadV3(blob, ref off);
                if (nv < 3)
                    throw new InvalidOperationException($"{label} face {i} has {nv} vertices.");
                bool mark = (marks & 1) != 0, smooth = (marks & 16) != 0;
                if (mark && !double.IsNaN(az) && !(az > 0 && az < 1))
                    throw new InvalidOperationException(
                        $"{label} face {i} is a mark with the turn band az {az}. The mesh stores a band inside " +
                        "(0, 1), where 0 is no band; 1 or more never draws, 0 or less would read as no band.");
                if (smooth && !(IsFinite9(sn.X) && IsFinite9(sn.Y) && IsFinite9(sn.Z)))
                    throw new InvalidOperationException($"{label} face {i}'s smooth normal is not three finite numbers.");
                var vs = new Vector3d[nv];
                for (int k = 0; k < nv; k++) vs[k] = ReadV3(blob, ref off);
                faces.Add(new RigFace
                {
                    V = vs, Mat = mat, B = b, Db = db, FaceGroup = group, Head = head, MinToward = minT,
                    Mark = mark, MarkAz = mark ? az : double.NaN, OverHair = (marks & 2) != 0,
                    UnderMark = (marks & 4) != 0, Hair = (marks & 8) != 0,
                    SmoothNormal = smooth ? sn : (Vector3d?)null,
                });
            }
            if (off != blob.Length)
                throw new InvalidOperationException(
                    $"{label} face blob was {blob.Length} bytes and {off} were read: packer and reader disagree.");
            return faces;
        }

        /// <summary>
        /// A <see cref="RigMeshData"/> of rig 9's faces, for the mesh builder (the bind mesh) and
        /// for the reference rasterizer (the turntable sign's oracle). It carries rig 9's cell,
        /// pivot, scale and elevation, its screen key light, gain 1 and bias 0 (each material
        /// carries its own) and the rig's dither (<see cref="Bayer9"/>: rig 10.3's <c>DITHER</c>, or the
        /// canonical matrix for rig 9, which exports none).
        /// </summary>
        public static RigMeshData NewData9(IRigScriptHost host, string preset, string label,
                                           string facesJs, List<RigMaterial9> materials)
        {
            string g = KitOf(host).GlobalName;
            var data = new RigMeshData
            {
                RigKey = label,
                GlobalName = g,
                SourceFaceExpression = facesJs,
                W = (int)host.EvaluateNumber($"{g}.W"),
                H = (int)host.EvaluateNumber($"{g}.H"),
                PivotX = host.EvaluateNumber($"{g}.pivot.x"),
                PivotY = host.EvaluateNumber($"{g}.pivot.y"),
                PxPerMetre = (int)host.EvaluateNumber($"{g}.PX"),
                DefaultElev = host.EvaluateNumber($"{g}.ELEV"),
                Gain = 1.0,
                Bias = 0.0,
                LightN = V9Shading3(host, "key"),
                Keyline = ParseHex9(host.EvaluateString($"String({g}.SHADING.keyline)"), "SHADING.keyline"),
                BayerWasExported = KitOf(host).ExportsNumbers,
                Bayer = Bayer9(host),
            };
            foreach (RigMaterial9 m in materials) data.Materials.Add(m.ToRigMaterial());
            data.Faces.AddRange(ReadFaces9(host, facesJs, materials, $"{label} ({preset})", preset));
            return data;
        }

        /// <summary>A three-number field of rig 9's SHADING block (key, fleetKey).</summary>
        public static Vector3d V9Shading3(IRigScriptHost host, string field)
        {
            string s = $"{KitOf(host).GlobalName}.SHADING[{Js(field)}]";
            if (!host.EvaluateBool($"Array.isArray({s})&&{s}.length===3&&{s}.every(function(x){{return isFinite(x);}})"))
                throw new InvalidOperationException($"{KitOf(host).Title}'s SHADING.{field} is not three finite numbers.");
            return new Vector3d(host.EvaluateNumber($"{s}[0]"), host.EvaluateNumber($"{s}[1]"),
                                host.EvaluateNumber($"{s}[2]"));
        }

        /// <summary>A one-number field of rig 9's SHADING block (form, formMid).</summary>
        public static double V9ShadingNumber(IRigScriptHost host, string field)
        {
            string s = $"{KitOf(host).GlobalName}.SHADING[{Js(field)}]";
            if (!host.EvaluateBool($"typeof {s}==='number'&&isFinite({s})"))
                throw new InvalidOperationException($"{KitOf(host).Title}'s SHADING.{field} is not a finite number.");
            return host.EvaluateNumber(s);
        }

        // ---------------------------------------------------------------------------------------
        // Clips
        // ---------------------------------------------------------------------------------------

        /// <summary>Every clip rig 9 ships, by the name <c>clip(name, p)</c> takes.</summary>
        public static string[] ClipNames9(IRigScriptHost host) =>
            SplitList(host.EvaluateString($"{KitOf(host).GlobalName}.clipNames().join(',')"));

        /// <summary>The ANIMS rows (the plain clips), for the turntable sign's frame plan.</summary>
        public static string[] Anims9(IRigScriptHost host) =>
            SplitList(host.EvaluateString($"Object.keys({KitOf(host).GlobalName}.ANIMS).join(',')"));

        public static int FrameCount9(IRigScriptHost host, string anim) =>
            (int)host.EvaluateNumber($"{KitOf(host).GlobalName}.ANIMS[{Js(anim)}].frames");

        /// <summary>
        /// One rig 9 clip for <paramref name="preset"/>, read by rig 7's clip reader. The clip's own
        /// <c>anim</c> names it, not the name it was asked for: <c>helm_idle</c> is the idle with the
        /// helm carried. So <paramref name="stateKey"/> is <see cref="CharacterState"/>'s key of
        /// (anim, carry), and the bone order is checked against the skeleton's, because the def
        /// stores indices.
        /// </summary>
        public static RigSkinClip ReadClip9(IRigScriptHost host, string preset, string clipName,
                                            RigBone[] bones, out string stateKey)
        {
            AssertPreset9(host, preset);
            host.Execute($"globalThis.__hhClip={KitOf(host).GlobalName}.clip({Js(clipName)},{Js(preset)});");
            string anim = host.EvaluateString("String(globalThis.__hhClip.anim||'')");
            if (anim.Length == 0)
                throw new InvalidOperationException($"{KitOf(host).Title} clip '{clipName}' names no anim.");
            string label = $"{KitOf(host).Name} clip '{clipName}' ({preset})";
            RigSkinClip rc = ReadLoadedClip(host, label, anim, bones.Length);
            string[] order = SplitList(host.EvaluateString("globalThis.__hhClip.bones.join(',')"));
            for (int b = 0; b < order.Length; b++)
                if (!string.Equals(order[b], bones[b].Id, StringComparison.Ordinal))
                    throw new InvalidOperationException(
                        $"{label} packs bone {b} as '{order[b]}' and the skeleton has '{bones[b].Id}'. " +
                        "The def stores INDICES, so a permuted clip would animate the right skeleton " +
                        "with the wrong limbs and never throw.");
            stateKey = string.IsNullOrEmpty(rc.Carry) ? rc.Anim : new CharacterState(rc.Anim, null, rc.Carry).Key;
            rc.Face = ReadFaceTrack9(host, label, rc.Frames);
            rc.Tool = ReadToolTrack9(host, label, rc.Frames);
            return rc;
        }

        /// <summary>
        /// The loaded clip's FACE track, <c>tracks[k].face</c>, as group ids: frame-major, one per slot
        /// in <see cref="CharacterSkinDef.FaceSlotNames"/> order, each 1 + its index in GROUP_ORDER.
        /// Refuses a frame with no face, with a slot the def does not know, or with a state
        /// GROUP_ORDER does not list: the track is kept verbatim or not at all.
        /// </summary>
        static byte[] ReadFaceTrack9(IRigScriptHost host, string label, int frames)
        {
            FaceGroupOrder9(host);
            var slots = new StringBuilder();
            foreach (string s in CharacterSkinDef.FaceSlotNames) slots.Append(Js(s)).Append(',');
            string ids = host.EvaluateString(
                "(function(){var c=globalThis.__hhClip,GO=" + KitOf(host).GlobalName + ".GROUP_ORDER," +
                "S=[" + slots.ToString().TrimEnd(',') + "],o=[];" +
                "for(var t=0;t<c.tracks.length;t++){var fc=c.tracks[t].face;" +
                "if(!fc||typeof fc!=='object')return '!frame '+t+' carries no face';" +
                "var ks=Object.keys(fc);if(ks.length!==S.length)return '!frame '+t+' names the slots '+ks.join('/');" +
                "for(var s=0;s<S.length;s++){var gi=GO.indexOf(S[s]+'.'+fc[S[s]]);" +
                "if(gi<0)return '!frame '+t+' shows '+S[s]+'.'+fc[S[s]]+', which GROUP_ORDER does not list';" +
                "o.push(gi+1);}}return o.join(',');})()");
            if (ids.StartsWith("!", StringComparison.Ordinal))
                throw new InvalidOperationException($"{label}'s face track: {ids.Substring(1)}.");
            string[] parts = SplitList(ids);
            if (parts.Length != frames * CharacterSkinDef.FaceSlots)
                throw new InvalidOperationException(
                    $"{label}'s face track holds {parts.Length} states for {frames} frames of " +
                    $"{CharacterSkinDef.FaceSlots} slots.");
            var face = new byte[parts.Length];
            for (int i = 0; i < parts.Length; i++) face[i] = checked((byte)Int9(parts[i], $"{label} face state {i}"));
            return face;
        }

        /// <summary>
        /// The loaded clip's TOOL track, <c>tracks[k].tool</c>, one key per frame, verbatim — or empty
        /// when every frame's key is the rig's bare <c>{held:false}</c> (the clip carries no tool).
        /// Refuses a key with a field the def does not carry, since data only still means all of it.
        /// </summary>
        static CharacterSkinDef.ToolKey[] ReadToolTrack9(IRigScriptHost host, string label, int frames)
        {
            string rows = host.EvaluateString(
                "(function(){var c=globalThis.__hhClip,o=[],any=false," +
                "known=['held','kind','pitch','yaw','bend','len','advisory'];" +
                "function n(x){return x==null?'0':String(x);}" +
                "for(var t=0;t<c.tracks.length;t++){var k=c.tracks[t].tool;" +
                "if(!k||typeof k!=='object')return '!frame '+t+' carries no tool key';" +
                "var ks=Object.keys(k);for(var i=0;i<ks.length;i++)if(known.indexOf(ks[i])<0)" +
                "return '!frame '+t+' carries the tool field '+ks[i];" +
                "if(ks.length>1||k.held)any=true;" +
                "o.push([k.held?1:0,k.kind==null?'':String(k.kind),n(k.pitch),n(k.yaw),n(k.bend),n(k.len)," +
                "k.advisory?1:0].join('|'));}" +
                "return any?o.join('\\n'):'';})()");
            if (rows.StartsWith("!", StringComparison.Ordinal))
                throw new InvalidOperationException($"{label}'s tool track: {rows.Substring(1)}.");
            if (rows.Length == 0) return Array.Empty<CharacterSkinDef.ToolKey>();
            string[] lines = rows.Split('\n');
            if (lines.Length != frames)
                throw new InvalidOperationException($"{label}'s tool track holds {lines.Length} keys for {frames} frames.");
            var keys = new CharacterSkinDef.ToolKey[frames];
            for (int i = 0; i < frames; i++)
            {
                string[] p = lines[i].Split('|');
                if (p.Length != 7)
                    throw new InvalidOperationException($"{label}'s tool key {i} read as {p.Length} fields, not 7.");
                string where = $"{label} tool key {i}";
                keys[i] = new CharacterSkinDef.ToolKey
                {
                    Held = p[0] == "1",
                    Kind = p[1],
                    Pitch = (float)Finite9(p[2], where + " pitch"),
                    Yaw = (float)Finite9(p[3], where + " yaw"),
                    Bend = (float)Finite9(p[4], where + " bend"),
                    Length = (float)Finite9(p[5], where + " len"),
                    Advisory = p[6] == "1",
                };
            }
            return keys;
        }

        // ---------------------------------------------------------------------------------------
        // Truth
        // ---------------------------------------------------------------------------------------

        /// <summary>
        /// Rig 9's own render of <paramref name="clip"/> frame <paramref name="frame"/> facing
        /// <paramref name="dir"/>, as RGBA, and the posed faces it painted (world space) as
        /// <paramref name="posed"/>, with their own materials. The head snap is off: the turntable
        /// sign reads the silhouette of the head where the skeleton puts it. The keyline and the
        /// edge drop are on: rig 9's default, and rig 10 is asked for its keyline, so the silhouette
        /// the sign reads is drawn by the same rule on both.
        /// </summary>
        public static byte[] RenderTruth9(IRigScriptHost host, string preset, string clip, int frame,
                                          int dir, out RigMeshData posed) =>
            RenderTruth9(host, preset, clip, frame, dir, new TruthOptions9 { SnapHead = false },
                         out posed, out _);

        /// <summary>What rig 9's <c>render</c> is asked for beyond the clip, the frame and the view.
        /// Every switch defaults to the rig's own default.</summary>
        public sealed class TruthOptions9
        {
            /// <summary>paintSolved's <c>snapHead</c>: move the head faces so the head's mid point sits
            /// on a pixel centre.</summary>
            public bool SnapHead = true;
            /// <summary>paint's keyline pass. Always passed to the rig: rig 9 draws its keyline unless
            /// told not to, rig 10 only when asked (<see cref="CharacterSkinDef.KeylineDefault"/>).</summary>
            public bool Keyline = true;
            /// <summary>paint's inner contour (the edge drop).</summary>
            public bool Edges = true;
            /// <summary>A look turn, x yaw and y pitch in degrees (<c>render({look})</c>), or null.</summary>
            public Vector2? Look;
            /// <summary>The face to paint, one state per slot in <see cref="CharacterSkinDef.FaceSlotNames"/>
            /// order (<c>'open', 'flat', 'flat'</c>), or null for the clip frame's own.</summary>
            public string[] Face;
            /// <summary>The camera elevation in degrees, or null for the rig's own.</summary>
            public double? Elevation;
        }

        /// <summary>
        /// Rig 9's own render with <paramref name="options"/>: the RGBA, the posed faces it painted
        /// (world space, only the groups it drew) as <paramref name="posed"/>, and the head snap it
        /// applied in pixels (<c>render().snap</c>, x right and y down) as <paramref name="snap"/>, or
        /// null with the snap off.
        /// </summary>
        public static byte[] RenderTruth9(IRigScriptHost host, string preset, string clip, int frame,
                                          int dir, TruthOptions9 options, out RigMeshData posed,
                                          out double[] snap) =>
            RenderTruth9(host, preset, clip, frame, dir, options, out posed, out snap, out _);

        /// <summary>
        /// <see cref="RenderTruth9(IRigScriptHost, string, string, int, int, TruthOptions9, out RigMeshData, out double[])"/>,
        /// and the materials the render painted with as the shading contract states them, in the
        /// order <paramref name="posed"/>'s faces index them (<see cref="RigFace.Mat"/>).
        /// </summary>
        public static byte[] RenderTruth9(IRigScriptHost host, string preset, string clip, int frame,
                                          int dir, TruthOptions9 options, out RigMeshData posed,
                                          out double[] snap, out List<RigMaterial9> materials)
        {
            AssertPreset9(host, preset);
            if (options == null) throw new ArgumentNullException(nameof(options));
            string g = KitOf(host).GlobalName;
            var c = CultureInfo.InvariantCulture;
            var o = new StringBuilder();
            o.Append("clip:").Append(Js(clip)).Append(",frame:").Append(frame.ToString(c))
             .Append(",dir:").Append(dir.ToString(c)).Append(",build:").Append(Js(preset))
             .Append(",snapHead:").Append(options.SnapHead ? "true" : "false");
            o.Append(",keyline:").Append(options.Keyline ? "true" : "false");
            if (!options.Edges) o.Append(",edges:false");
            if (options.Elevation.HasValue) o.Append(",elev:").Append(options.Elevation.Value.ToString("R", c));
            if (options.Look.HasValue)
                o.Append(",look:{yaw:").Append(options.Look.Value.x.ToString("R", c))
                 .Append(",pitch:").Append(options.Look.Value.y.ToString("R", c)).Append('}');
            if (options.Face != null)
            {
                if (options.Face.Length != CharacterSkinDef.FaceSlots)
                    throw new ArgumentException($"A face names {CharacterSkinDef.FaceSlots} states, one per slot.", nameof(options));
                o.Append(",face:{");
                for (int s = 0; s < CharacterSkinDef.FaceSlots; s++)
                    o.Append(s == 0 ? "" : ",").Append(CharacterSkinDef.FaceSlotNames[s]).Append(':').Append(Js(options.Face[s]));
                o.Append('}');
            }
            host.Execute($"globalThis.__hhR9={g}.render({{{o}}});");
            byte[] rgba = host.EvaluateBytes("globalThis.__hhR9.rgba");
            const string faces = "globalThis.__hhR9.faces";
            materials = ReadMaterials9(host, preset, faces);
            posed = NewData9(host, preset, $"{g}:{preset}:{clip}:{frame}:dir {dir}", faces, materials);
            if (rgba.Length != posed.W * posed.H * 4)
                throw new InvalidOperationException(
                    $"{KitOf(host).Title} rendered {rgba.Length} bytes for a {posed.W}x{posed.H} cell.");
            snap = null;
            if (host.EvaluateBool("Array.isArray(globalThis.__hhR9.snap)"))
                snap = new[] { host.EvaluateNumber("globalThis.__hhR9.snap[0]"),
                               host.EvaluateNumber("globalThis.__hhR9.snap[1]") };
            return rgba;
        }

        /// <summary>A one-line census for the bake log.</summary>
        public static string Census9(IRigScriptHost host, string preset)
        {
            string g = KitOf(host).GlobalName;
            var c = CultureInfo.InvariantCulture;
            return $"{preset}: {KitOf(host).Name} rev {host.EvaluateString($"String({g}.revision)")}, " +
                   ((int)host.EvaluateNumber($"({AllFacesJs9(host, preset)}).length")).ToString(c) +
                   " bind faces with every face group, " +
                   ((int)host.EvaluateNumber($"{g}.skeleton({Js(preset)}).length")).ToString(c) + " bones, " +
                   ClipNames9(host).Length.ToString(c) + " clips";
        }

        // ---------------------------------------------------------------------------------------
        // Small parsers
        // ---------------------------------------------------------------------------------------

        static string[] SplitList(string joined) =>
            joined.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

        static bool SameList9(IReadOnlyList<string> a, IReadOnlyList<string> b)
        {
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++)
                if (!string.Equals(a[i], b[i], StringComparison.Ordinal)) return false;
            return true;
        }

        static Color32 ParseHex9(string hex, string where)
        {
            if (hex == null || hex.Length != 7 || hex[0] != '#')
                throw new InvalidOperationException($"{where} is '{hex}', not a #rrggbb colour.");
            byte H(int i) => byte.Parse(hex.Substring(i, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            return new Color32(H(1), H(3), H(5), 255);
        }

        static int Int9(string s, string where)
        {
            if (!int.TryParse(s, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int v))
                throw new InvalidOperationException($"{where} is '{s}', not a whole number.");
            return v;
        }

        static double Finite9(string s, string where)
        {
            if (!double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double v) ||
                double.IsNaN(v) || double.IsInfinity(v))
                throw new InvalidOperationException($"{where} is '{s}', not a finite number.");
            return v;
        }
    }
}
