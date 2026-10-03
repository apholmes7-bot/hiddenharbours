using System;
using System.Globalization;
using System.IO;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>
    /// One of Claude Design's character rig kits, as the v9 reader reads it: rig 9 (9.2, under
    /// <c>character/rig9/</c>) or rig 10 (10.2, under <c>character/rig10/</c>, landed by the rig 10
    /// intake on 2026-10-02). Rig 10 keeps rig 9's API (skeleton, bindMesh, clip, shadingContract,
    /// BLINK, LOOK, render) and adds four fields to its faces, so one reader reads both and this names
    /// what differs: the files, the global, the revision, the table of presets the game bakes, and
    /// whether the face is drawn as point marks.
    ///
    /// <para>A script host holds ONE kit: <see cref="CharacterSkinExtractor.Load9(IRigScriptHost, CharacterRigKit)"/>
    /// installs it and every reader asks the host which (<see cref="CharacterSkinExtractor.KitOf"/>).
    /// A host nobody loaded reads as rig 9, as before rig 10 existed.</para>
    /// </summary>
    public sealed class CharacterRigKit
    {
        /// <summary>Rig 9.2: the kit the cast is baked from until the rig 10 intake's Phase B
        /// (<see cref="CharacterSkinAssetBaker.LiveRig"/>). Its folder stays as 9.2's record.</summary>
        public static readonly CharacterRigKit Rig9 = new CharacterRigKit(
            CharacterSkinExtractor.V9CatalogKey, CharacterSkinExtractor.V9RigName,
            CharacterSkinExtractor.V9Revision, CharacterSkinExtractor.V9Pass, "v9", "CAST", faceMarks: false);

        /// <summary>Rig 10.2. Its <c>CAST</c> is the ten presets and the twenty NPCs; the game bakes
        /// <c>CAST10</c> alone (owner, 10-01: the twenty NPCs come with the wardrobe).</summary>
        public static readonly CharacterRigKit Rig10 = new CharacterRigKit(
            "characterRig10", "characterIsoRig10", "10.2", 10, "v10", "CAST10", faceMarks: true);

        /// <summary>The <see cref="RigCatalog"/> key of the rig's body script.</summary>
        public readonly string CatalogKey;

        /// <summary>What the rig says it is (<c>G.rig</c>).</summary>
        public readonly string RigName;

        /// <summary>The revision the reader is written for (<c>G.revision</c>).</summary>
        public readonly string Revision;

        /// <summary>The rig's pass (<c>G.pass</c>).</summary>
        public readonly int Pass;

        /// <summary>The tag in the kit's build and options file names (<c>builds/fisher.v9.json</c>).</summary>
        public readonly string FileTag;

        /// <summary>The rig's table of the presets the game bakes: <c>CAST</c> for rig 9, whose cast
        /// is the ten; <c>CAST10</c> for rig 10, whose <c>CAST</c> adds the twenty NPCs.</summary>
        public readonly string PresetTable;

        /// <summary>True when the rig draws its face as point marks and lights its smooth parts on
        /// their own normals: rig 10's paint, whose bind faces carry <c>pt</c>, <c>az</c>, <c>oh</c>
        /// and <c>sn</c> and no <c>minT</c>.</summary>
        public readonly bool FaceMarks;

        CharacterRigKit(string catalogKey, string rigName, string revision, int pass, string fileTag,
                        string presetTable, bool faceMarks)
        {
            CatalogKey = catalogKey;
            RigName = rigName;
            Revision = revision;
            Pass = pass;
            FileTag = fileTag;
            PresetTable = presetTable;
            FaceMarks = faceMarks;
        }

        /// <summary>"rig 9" or "rig 10", for messages.</summary>
        public string Name => "rig " + Pass.ToString(CultureInfo.InvariantCulture);

        /// <summary>"Rig 9" or "Rig 10", for messages that open with it.</summary>
        public string Title => "Rig " + Pass.ToString(CultureInfo.InvariantCulture);

        public RigEntry Entry => RigCatalog.Get(CatalogKey);

        /// <summary>The rig's body script, repo-relative.</summary>
        public string ScriptPath => Entry.ScriptPath;

        /// <summary>The global the rig defines (<c>CharacterIso9</c>, <c>CharacterIso10</c>).</summary>
        public string GlobalName => Entry.GlobalName;

        /// <summary>The pose library, which must run after the body.</summary>
        public string PosesPath => Sidecar("poses");

        /// <summary>The rig's own checks (<c>runChecks</c>), for the guards; never loaded by the bake.</summary>
        public string ChecksPath => Sidecar("checks");

        /// <summary>The folder the kit was landed in, repo-relative: the parent of the folder that
        /// holds the rig script.</summary>
        public string KitFolder =>
            Path.GetDirectoryName(Path.GetDirectoryName(ScriptPath)).Replace('\\', '/');

        public string KitRoot => Path.Combine(RigCatalog.RepoRoot, KitFolder);

        /// <summary>The kit's export of <paramref name="preset"/> (<c>exportBuild</c> under the kit's
        /// header), kit-relative.</summary>
        public string BuildFile(string preset) => $"builds/{preset}.{FileTag}.json";

        /// <summary>The kit's gameplay sidecar of <paramref name="preset"/>, kit-relative.</summary>
        public string GameplayFile(string preset) =>
            $"gameplay/{Path.GetFileNameWithoutExtension(ScriptPath)}.{preset}.gameplay.json";

        /// <summary>The builder's options file, kit-relative.</summary>
        public string OptionsFile => $"data/options.{FileTag}.json";

        /// <summary>The prefix of the mesh names this kit's bake writes (<c>CharSkin9_fisher_bind</c>).</summary>
        public string MeshPrefix => "CharSkin" + Pass.ToString(CultureInfo.InvariantCulture);

        string Sidecar(string part)
        {
            string body = ScriptPath;
            if (!body.EndsWith(".js", StringComparison.Ordinal))
                throw new InvalidOperationException($"{Title}'s body '{body}' is not a .js file.");
            return body.Substring(0, body.Length - 3) + "." + part + ".js";
        }

        public override string ToString() => $"{RigName} {Revision}";
    }
}
