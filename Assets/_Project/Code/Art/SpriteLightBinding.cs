using UnityEngine;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// The ONE writer of the shared lit-sprite properties — the C# side of
    /// <c>Assets/_Project/Art/Shaders/Include/SpriteLitDecor.hlsl</c>.
    ///
    /// <para><b>Why a static and not just a component.</b> Two components legitimately need to publish
    /// these: <see cref="SpriteLightBinder"/> (the drop-on for any decor renderer) and
    /// <see cref="TreeTrunkAnchor"/> (which also owns the tree's <c>_TrunkAnchor</c> and so cannot simply
    /// be replaced by the binder without moving a second, unrelated property). Two components writing the
    /// same six properties from two copies of the same careful code is how the mask and the normal end up
    /// bound by one and cleared by the other. This is that code, once.</para>
    ///
    /// <para><b>⚠️ THE TWO TRAPS, both already paid for elsewhere in this project.</b>
    /// <list type="number">
    /// <item><b>GET the block before you modify it.</b> A SpriteRenderer keeps its own per-renderer
    /// overrides — the sprite texture among them — in this same block. Handing it a FRESH one drops them
    /// and the sprite draws as a white rectangle.</item>
    /// <item><b><see cref="MaterialPropertyBlock.SetTexture(int, Texture)"/> THROWS on null.</b> It does
    /// not quietly clear the override. So bind only what is really here and let the always-written flags
    /// be what turns the response off. Clearing a sheet therefore leaves a stale texture bound but inert,
    /// which is the safe direction: an inert texture costs a sampler slot, a wrong one costs a wrong
    /// look.</item>
    /// </list></para>
    ///
    /// <para><b>Rules.</b> Visual-only — it writes shader properties and nothing else: no sim, no save
    /// (rule 5). No per-frame writes: callers apply on enable/validate (rule 7). Every value is data from
    /// a bake or a transform, never a literal (rule 6).</para>
    /// </summary>
    public static class SpriteLightBinding
    {
        /// <summary>The light sheet: R key light · G back rim · B depth · A coverage.
        /// <b>That order is OURS</b> — the reference technique this serves says green = front and
        /// blue = rim, and a snippet ported from it looks subtly wrong rather than broken. The tree rig
        /// bakes it as <c>&lt;species&gt;_mask.png</c>; the shoreline-plant and shrub rigs bake the same
        /// four channels as <c>&lt;key&gt;_light.png</c>. Same contract, different file name.</summary>
        public const string MaskProperty = "_LightMask";

        /// <summary>The view-space normal sheet. <b>OPTIONAL</b>: only the tree rig ships one. The
        /// shoreline-plant and shrub rigs consume their normals AT BAKE and what survives to runtime is
        /// the light channel alone — a supported path, not a degraded one, because
        /// <c>SpriteLightKey</c>/<c>SpriteLightRim</c> already fall back to the baked mask wherever a
        /// texel has no normal (which is how the tree's own keyline ring is lit).</summary>
        public const string NormalProperty = "_LightNormal";

        /// <summary>The state sheet carrying the no-rim flag. <b>OPTIONAL.</b> See
        /// <see cref="RimGateChannelProperty"/> for why the channel is published rather than assumed.</summary>
        public const string RimGateProperty = "_LightRimGate";

        /// <summary>
        /// Which channel of <see cref="RimGateProperty"/> means "this pixel may not rim" (255 where
        /// forbidden), as a one-hot selector the shader dots against the texel.
        ///
        /// <para>🔴 <b>It is published, never inferred, because the rigs genuinely disagree.</b> The
        /// shoreline plants put the flag in their tide sheet's BLUE (strap pixels: blades, culms, fronds);
        /// the shrubs put it in their calendar sheet's RED (veil pixels). Both committed contracts say the
        /// same sentence about it — "This is the branch. Read it, do not infer it." A shared consumer that
        /// guessed would be right for one family and silently wrong for the other.</para>
        /// </summary>
        public const string RimGateChannelProperty = "_LightRimGateChannel";

        /// <summary>Set to 1 only when a LIGHT SHEET is bound. The shader's fallback textures are opaque
        /// black, which would read as "coverage everywhere" — so the response is gated on an explicit flag
        /// rather than on sniffing a texture that is never null.</summary>
        public const string ChannelsProperty = "_LightChannels";

        /// <summary>
        /// Where this sprite STANDS, in world space (<c>xy</c>), with <c>w</c> = 1 meaning "published".
        /// The shader needs it to point a LOCAL light — the boat lamp — at the right sprite.
        ///
        /// <para>🔴 <b>It cannot come from <c>unity_ObjectToWorld</c>.</b> Unity submits SpriteRenderer
        /// meshes with their vertices ALREADY IN WORLD SPACE and an IDENTITY object-to-world matrix, so
        /// <c>_m03/_m13</c> reads (0,0) for every sprite in the scene. Measured 2026-07-29 on the trees: a
        /// 3 m-range lamp parked on the first tree of a three-tree lineup lit all three equally, because
        /// every one of them evaluated the lamp from the world origin.</para>
        /// </summary>
        public const string RootProperty = "_SpriteRootWS";

        internal static readonly int MaskId = Shader.PropertyToID(MaskProperty);
        internal static readonly int NormalId = Shader.PropertyToID(NormalProperty);
        internal static readonly int RimGateId = Shader.PropertyToID(RimGateProperty);
        internal static readonly int RimGateChannelId = Shader.PropertyToID(RimGateChannelProperty);
        internal static readonly int ChannelsId = Shader.PropertyToID(ChannelsProperty);
        internal static readonly int RootId = Shader.PropertyToID(RootProperty);

        /// <summary>No gate bound: every pixel may rim. This is the tree, and it is the default.</summary>
        public static readonly Vector4 RimGateNone = Vector4.zero;

        /// <summary>The SHRUB rig's no-rim flag: <c>&lt;key&gt;_calendar.png</c> RED is 255 on veil pixels.</summary>
        public static readonly Vector4 RimGateRed = new Vector4(1f, 0f, 0f, 0f);

        /// <summary>The SHORELINE-PLANT rig's no-rim flag: <c>&lt;key&gt;_tide.png</c> BLUE is 255 on
        /// strap pixels (blades, culms, fronds, sheets).</summary>
        public static readonly Vector4 RimGateBlue = new Vector4(0f, 0f, 1f, 0f);

        /// <summary>
        /// Publish one renderer's light binding into <paramref name="block"/> and set it on the renderer.
        /// Safe to call with any combination of nulls — that is the point.
        /// </summary>
        /// <param name="renderer">The renderer to publish onto. Null is a no-op.</param>
        /// <param name="block">A reusable block owned by the caller, so this allocates nothing.</param>
        /// <param name="mask">The light sheet. REQUIRED for the response to run.</param>
        /// <param name="normal">The view-space normal sheet, or null (the plants and shrubs).</param>
        /// <param name="rimGate">The state sheet carrying the no-rim flag, or null (the tree).</param>
        /// <param name="rimGateChannel">Which channel of <paramref name="rimGate"/> forbids a rim.</param>
        /// <param name="root">Where this sprite stands, in world space.</param>
        public static void Apply(
            SpriteRenderer renderer, MaterialPropertyBlock block,
            Texture2D mask, Texture2D normal, Texture2D rimGate, Vector4 rimGateChannel, Vector3 root)
        {
            if (renderer == null || block == null) return;

            // GET first, then modify, then SET — see the class doc, trap 1.
            renderer.GetPropertyBlock(block);
            Fill(block, mask, normal, rimGate, rimGateChannel, root);
            renderer.SetPropertyBlock(block);
        }

        /// <summary>
        /// Write the light binding into an ALREADY-FETCHED block, without touching the renderer.
        ///
        /// <para>For a caller that has its own properties to add to the same block —
        /// <see cref="TreeTrunkAnchor"/> and its <c>_TrunkAnchor</c>. Without this, such a caller has to
        /// call <see cref="Apply"/> and then do a second get-modify-SET of its own, which is two
        /// <c>SetPropertyBlock</c> calls where one would do. The sequence is: GET once, call this, write
        /// your own properties, SET once.</para>
        /// </summary>
        public static void Fill(
            MaterialPropertyBlock block,
            Texture2D mask, Texture2D normal, Texture2D rimGate, Vector4 rimGateChannel, Vector3 root)
        {
            if (block == null) return;

            // ⚠️ Trap 2: SetTexture throws on null, so bind only what is here.
            if (mask != null) block.SetTexture(MaskId, mask);
            if (normal != null) block.SetTexture(NormalId, normal);
            if (rimGate != null) block.SetTexture(RimGateId, rimGate);

            // The response runs on the LIGHT SHEET alone. A normal is a bonus, not a prerequisite —
            // demanding both is what would have kept the plants and shrubs unlit forever.
            block.SetFloat(ChannelsId, mask != null ? 1f : 0f);

            // 🔴 NO GATE MEANS A ZERO SELECTOR, ENFORCED HERE. Unity's fallback "black" texture is
            // (0, 0, 0, 1) — its ALPHA is 1, not 0. So a consumer that published an alpha-channel
            // selector without binding a sheet would read "no rim anywhere" off the fallback and lose
            // every rim it has. Zeroing the selector whenever the sheet is absent makes that
            // unrepresentable rather than merely unlikely.
            block.SetVector(RimGateChannelId, rimGate != null ? rimGateChannel : RimGateNone);

            // Where this sprite stands. w = 1 says "published", which is what lets the shader tell a real
            // stand position from the (0,0,0,0) a material default would hand it.
            block.SetVector(RootId, new Vector4(root.x, root.y, 0f, 1f));
        }

        /// <summary>
        /// Read back the <see cref="ChannelsProperty"/> flag this renderer will actually hand the shader:
        /// 1 when a light sheet reached it, 0 otherwise. Asserted against the RENDERER rather than the
        /// fields that were written to it, because "the value reached the block" is the thing that breaks.
        /// </summary>
        public static float ChannelsOn(SpriteRenderer renderer, float fallback = -1f)
        {
            var block = BlockOn(renderer);
            if (block == null) return fallback;
            return block.HasFloat(ChannelsId) ? block.GetFloat(ChannelsId) : fallback;
        }

        /// <summary>The stand position this renderer will hand the shader, or a <c>w</c> of 0 when nothing
        /// published one.</summary>
        public static Vector4 RootOn(SpriteRenderer renderer)
        {
            var block = BlockOn(renderer);
            if (block == null) return Vector4.zero;
            return block.HasVector(RootId) ? block.GetVector(RootId) : Vector4.zero;
        }

        /// <summary>The texture this renderer will hand <see cref="MaskProperty"/>, or null.</summary>
        public static Texture MaskOn(SpriteRenderer renderer) => TextureOn(renderer, MaskId);

        /// <summary>The texture this renderer will hand <see cref="NormalProperty"/>, or null.</summary>
        public static Texture NormalOn(SpriteRenderer renderer) => TextureOn(renderer, NormalId);

        /// <summary>The texture this renderer will hand <see cref="RimGateProperty"/>, or null.</summary>
        public static Texture RimGateOn(SpriteRenderer renderer) => TextureOn(renderer, RimGateId);

        /// <summary>The one-hot channel selector this renderer will hand the shader, or zero.</summary>
        public static Vector4 RimGateChannelOn(SpriteRenderer renderer)
        {
            var block = BlockOn(renderer);
            if (block == null) return Vector4.zero;
            return block.HasVector(RimGateChannelId) ? block.GetVector(RimGateChannelId) : Vector4.zero;
        }

        private static Texture TextureOn(SpriteRenderer renderer, int id)
        {
            var block = BlockOn(renderer);
            if (block == null) return null;
            return block.HasTexture(id) ? block.GetTexture(id) : null;
        }

        private static MaterialPropertyBlock BlockOn(SpriteRenderer renderer)
        {
            if (renderer == null || !renderer.HasPropertyBlock()) return null;
            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            return block;
        }
    }
}
