using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Art.Editor;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>
    /// <b>WHAT THE PLAYER WOULD ACTUALLY SEE IF THE INTERIOR WERE SIMPLY DRAWN OVER THE BOAT.</b>
    ///
    /// <para>The S0 ruling asked for this before the entry gate's policy is settled. Both spikes agreed
    /// there is nothing on a mesh hull to switch off; what neither could answer from geometry is whether
    /// the overdraw READS — and that is an owner's-eye question, so this produces the pictures rather
    /// than an opinion.</para>
    ///
    /// <para><b>Rendered from the rigs, at the interior's own eight facings, on one camera basis.</b> The
    /// exterior is drawn at the hull's own rock pose and the interior at
    /// <c>scale x</c> that pose — which is exactly what <c>InteriorRockScale</c> does at runtime, and
    /// exactly the disagreement that matters: at 1.0 the two agree by construction, at 0.45 they do not,
    /// and the whole question is whether the eye minds.</para>
    ///
    /// <para><b>The pose is the rig's OWN.</b> <c>rock(i)</c> is asked for frame 1 of 8 — a quarter of
    /// the way round the cycle, where roll, pitch and heave are all live at once. Reading the rig's
    /// answer rather than re-deriving <c>rollA·sin(θ)</c> here means the picture cannot disagree with
    /// the boat for a reason of mine.</para>
    ///
    /// <para>Read-only: renders to PNGs outside the project and writes nothing back. Run with
    /// <c>-executeMethod HiddenHarbours.Tools.RigBaking.BoatInteriorOverdrawSpike.Cli
    /// -overdrawOut &lt;dir&gt;</c>.</para>
    /// </summary>
    public static class BoatInteriorOverdrawSpike
    {
        /// <summary>The two hulls the ruling asked for: the fleet's workboat, and one large ship.</summary>
        private static readonly string[] Hulls = { "lobsterBoatIsoRig", "sternTrawlerIsoRig" };

        /// <summary>The comfort clamp at both ends of the question: full fidelity, and the ruled
        /// default.</summary>
        private static readonly float[] Scales = { 1.0f, 0.45f };

        /// <summary>
        /// Which rock frame to pose. Overridable with <c>-overdrawFrame</c>, because the two frames
        /// worth looking at answer different questions: <b>1 of 8</b> has roll, pitch and heave all off
        /// zero at once (does the composite read at all?), and <b>2 of 8</b> is the crest, where roll and
        /// heave are at their peak and the interior-versus-exterior disagreement is therefore at ITS peak
        /// (does the comfort clamp show?). A judgement taken at frame 1 alone would be taken at a
        /// quarter of the worst case.
        /// </summary>
        private static int _rockFrame = 1;

        [MenuItem("Hidden Harbours/Dev/Spikes/Boat Interior — Overdraw Composite")]
        public static void Run() => Render(DefaultOut());

        public static void Cli()
        {
            string outDir = null;
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], "-overdrawOut", StringComparison.Ordinal)) outDir = args[i + 1];
                if (string.Equals(args[i], "-overdrawFrame", StringComparison.Ordinal) &&
                    int.TryParse(args[i + 1], out int f)) _rockFrame = f;
            }

            try { Render(string.IsNullOrEmpty(outDir) ? DefaultOut() : outDir); }
            catch (Exception e) { Debug.LogError($"[BoatInteriorOverdrawSpike] FAILED: {e}"); }
        }

        private static string DefaultOut()
            => Path.Combine(Directory.GetParent(Application.dataPath).FullName, "overdraw-spike");

        private static void Render(string outDir)
        {
            Directory.CreateDirectory(outDir);
            var log = new StringBuilder("[BoatInteriorOverdrawSpike]\n");

            foreach (string stem in Hulls)
            {
                try { RenderHull(stem, outDir, log); }
                catch (Exception e) { log.AppendLine($"  {stem}: FAILED {e.Message}"); }
            }

            File.WriteAllText(Path.Combine(outDir, "overdraw-log.txt"), log.ToString());
            Debug.Log(log.ToString());
        }

        private static void RenderHull(string stem, string outDir, StringBuilder log)
        {
            using IRigScriptHost host = RigScriptHostFactory.Create();

            var table = BoatInteriorRigHost.ReadHullTable(host);
            if (!BoatInteriorRigHost.TryBind(table, stem, out BoatInteriorRigHost.HullBinding binding,
                                             out string bindError))
            {
                log.AppendLine($"  {stem}: cannot bind — {bindError}");
                return;
            }
            BoatInteriorRigHost.Install(host, in binding);

            string interiorGlobal = BoatInteriorRigHost.Global;
            string cell = $"{interiorGlobal}.cellOf('{binding.Key}')";
            if (!host.EvaluateBool($"!!{cell}"))
            {
                log.AppendLine($"  {stem}: the interior rig resolves no environment for '{binding.Key}'");
                return;
            }

            int w = (int)host.EvaluateNumber($"{cell}.W");
            int h = (int)host.EvaluateNumber($"{cell}.H");

            string[] levels = BoatInteriorRigHost.LevelsOf(host, binding.Key);
            string level = levels.FirstOrDefault(l => l.IndexOf("house", StringComparison.Ordinal) >= 0)
                           ?? levels.FirstOrDefault();
            if (string.IsNullOrEmpty(level))
            {
                log.AppendLine($"  {stem}: the rig draws no levels");
                return;
            }

            // THE RIG'S OWN POSE, not a re-derivation of it.
            double roll = host.EvaluateNumber($"{binding.ExteriorGlobal}.rock({_rockFrame}).roll");
            double pitch = host.EvaluateNumber($"{binding.ExteriorGlobal}.rock({_rockFrame}).pitch");
            double heave = host.EvaluateNumber($"{binding.ExteriorGlobal}.rock({_rockFrame}).heave");

            log.AppendLine($"  {stem}: cell {w}x{h}, level '{level}', rock frame {_rockFrame} = " +
                           $"roll {roll:F3}° pitch {pitch:F3}° heave {heave:F3}px");

            for (int d = 0; d < BoatInteriorRigHost.Facings; d++)
            {
                byte[] ext = host.EvaluateBytes(
                    $"{binding.ExteriorGlobal}.render({d},{Pose("{}", roll, pitch, heave)})");
                Write(outDir, $"{stem}_ext_d{d}.png", ext, w, h, log);

                foreach (float scale in Scales)
                {
                    string intOpts = InteriorOpts(binding.Key, level);
                    byte[] room = host.EvaluateBytes(
                        $"{interiorGlobal}.render({d},{Pose(intOpts, roll * scale, pitch * scale, heave * scale)})");

                    byte[] composite = Over(ext, room);
                    string tag = scale.ToString("0.00", CultureInfo.InvariantCulture).Replace(".", "p");
                    Write(outDir, $"{stem}_over{tag}_d{d}.png", composite, w, h, log);
                }
            }
        }

        /// <summary>Splice roll/pitch/heave into an options literal. The rigs all read them off
        /// <c>opts</c> in one shared <c>camBasis</c>, which is what makes the two renders comparable at
        /// all.</summary>
        private static string Pose(string opts, double roll, double pitch, double heave)
        {
            string body = opts.Trim();
            body = body.Length <= 2 ? "" : body.Substring(1, body.Length - 2) + ",";
            return "{" + body +
                   $"roll:{roll.ToString("R", CultureInfo.InvariantCulture)}," +
                   $"pitch:{pitch.ToString("R", CultureInfo.InvariantCulture)}," +
                   $"heave:{heave.ToString("R", CultureInfo.InvariantCulture)}}}";
        }

        private static string InteriorOpts(string hullKey, string level)
            => BoatInteriorRigHost.OptsLiteral(hullKey, level, "");

        /// <summary>Source-over: the room drawn ON the boat, which is the thing being judged.</summary>
        private static byte[] Over(byte[] under, byte[] over)
        {
            var outBytes = (byte[])under.Clone();
            for (int i = 0; i + 3 < over.Length && i + 3 < outBytes.Length; i += 4)
            {
                int a = over[i + 3];
                if (a == 0) continue;
                if (a == 255)
                {
                    outBytes[i] = over[i]; outBytes[i + 1] = over[i + 1];
                    outBytes[i + 2] = over[i + 2]; outBytes[i + 3] = 255;
                    continue;
                }
                float fa = a / 255f, ia = 1f - fa;
                for (int c = 0; c < 3; c++)
                    outBytes[i + c] = (byte)Mathf.Clamp(over[i + c] * fa + outBytes[i + c] * ia, 0f, 255f);
                outBytes[i + 3] = (byte)Mathf.Clamp(a + outBytes[i + 3] * ia, 0f, 255f);
            }
            return outBytes;
        }

        private static void Write(string dir, string file, byte[] rgba, int w, int h, StringBuilder log)
        {
            if (rgba == null || rgba.Length != w * h * 4)
            {
                log.AppendLine($"    {file}: {rgba?.Length ?? 0} bytes, expected {w * h * 4}");
                return;
            }

            var tex = new Texture2D(w, h, TextureFormat.RGBA32, mipChain: false, linear: false);
            tex.LoadRawTextureData(rgba);
            tex.Apply(false, false);
            File.WriteAllBytes(Path.Combine(dir, file), tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
        }
    }
}
