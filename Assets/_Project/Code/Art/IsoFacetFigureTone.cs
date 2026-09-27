using System;
using UnityEngine;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// <b>The v9 character tone as ONE pure C# authority</b> — the v9.1 character kit's rules 3, 5, 6, 7
    /// and 11 (its <c>data/shading.v9.json</c>). <see cref="IsoCharacterFigureRenderer"/> folds the kit's
    /// key and form term into the facet shader's one light vector and each material's bias, and the
    /// <c>HH_FIGURE</c> variant of <c>HiddenHarbours/IsoFacet</c> does the rest per face. EditMode tests
    /// pin both halves here, with no GPU.
    ///
    /// <para><b>The fold.</b> The kit shades a face by
    /// <c>s = key·(x, up, toward) + form·(toward − formMid)</c> (kit rules 3 and 5), with the normal's
    /// <c>up</c> and <c>toward</c> taken at the camera's elevation. That is ONE dot product with
    /// <c>LN' = (k0, k1, k2 + form)</c> in the kit's screen basis, less the constant
    /// <c>form·formMid</c>. The figure's object transform already carries her normals into that basis (the
    /// elevation is its <c>HullRotation(0, e)</c>; see <see cref="IsoFacetMath"/>'s class doc), so
    /// <c>LN'</c> goes to the shader through <see cref="IsoFacetMath.ShaderLightVector"/> exactly as rig
    /// 7's <c>LightN</c> does, and is NOT turned by the elevation: turning it as well would count the
    /// tilt twice. The constant moves into each material's bias (<see cref="FoldBias"/>).</para>
    ///
    /// <para>⚠️ <b><c>LN'</c> is not unit length, on purpose</b> (1.3548 at the kit's form 0.5).
    /// <see cref="IsoFacetMath.ShaderLightVector"/> only negates z, and the shader normalises the normal
    /// but never <c>_LN</c>; the fold relies on both. <see cref="IsoFacetMath.ShaderLightVector"/>'s
    /// "the rig's own normalised LN" describes rig 7's input, not this one.</para>
    /// </summary>
    public static class IsoFacetFigureTone
    {
        /// <summary>Kit rule 5 rounds <c>s</c> to the nearest 1e-9 before the tone:
        /// <c>s = round(s·1e9) / 1e9</c>. The shader cannot (float); the GPU plate measures that.</summary>
        public const double ShadeQuantum = 1e9;

        /// <summary>
        /// The folded light vector <c>LN' = (k0, k1, k2 + form)</c>, in the same screen basis (right, up,
        /// toward the eye) as <paramref name="keyScreen"/>. Hand it to
        /// <see cref="IsoFacetMath.ShaderLightVector"/>; never normalise it, and never turn it by the
        /// elevation (see the class doc).
        /// </summary>
        public static Vector3 FoldLight(Vector3 keyScreen, float form) =>
            new Vector3(keyScreen.x, keyScreen.y, keyScreen.z + form);

        /// <summary>
        /// One material's folded bias, <c>bias' = bias − gain·form·formMid</c>: the constant the fold took
        /// out of the dot product, put back per material. <paramref name="gain"/> and
        /// <paramref name="bias"/> are the material's EFFECTIVE pair, <c>def.Gain · m.Gain</c> and
        /// <c>m.BiasOr(def.Bias)</c>.
        /// </summary>
        public static float FoldBias(float gain, float bias, float form, float formMid) =>
            bias - gain * form * formMid;

        /// <summary>
        /// Kit rule 6, the reference the shader's <c>HH_FIGURE</c> fragment is a twin of: the ramp index
        /// one face of one material takes. <paramref name="s"/> is the kit's own shade (rules 3 and 5) and
        /// <paramref name="bias"/> the material's effective bias, both UNFOLDED; the shader carries the
        /// folded pair, whose sum is the same. <paramref name="b"/> is the face's band offset (0 or −1,
        /// the mesh's <c>attrs.y</c>).
        ///
        /// <para>Rounding is JavaScript's <c>Math.round</c>, <c>floor(x + 0.5)</c> (kit rule 7): halves go
        /// up, so <c>−2.5 → −2</c>. The window is clamped as HLSL's <c>clamp</c> does,
        /// <c>min(max(x, lo), hi)</c>, where the kit writes <c>max(lo, min(hi, x))</c>; the two differ only
        /// when <c>lo &gt; hi</c>, which <c>CharacterSkinDef.IsUsable</c> refuses.</para>
        /// </summary>
        public static int Tone(double s, double gain, double bias, double b,
                               int len, int off, int lo, int hi)
        {
            s = Math.Floor(s * ShadeQuantum + 0.5) / ShadeQuantum;   // kit rules 5 and 7
            double fidx = s * gain + bias + b;
            // ==== TWIN A (begin): the v9 tone (kit rule 6), VERBATIM in HiddenHarboursIsoFacet.shader (frag, HH_FIGURE) ====
            int idx = Clamp(Clamp((int)Math.Floor(fidx + 0.5), lo, hi) + off, 0, len - 1);
            // ==== TWIN A (end) ====
            return idx;
        }

        /// <summary>HLSL's <c>clamp</c> on ints, <c>min(max(x, lo), hi)</c>. It never throws, so the twin
        /// does what the shader does even on data <c>IsUsable</c> would refuse.</summary>
        private static int Clamp(int x, int lo, int hi) => Math.Min(Math.Max(x, lo), hi);
    }
}
