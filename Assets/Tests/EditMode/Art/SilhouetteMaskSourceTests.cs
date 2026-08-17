using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Art;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// The fisher's silhouette SOURCE — the hidden child that carries her current sprite into the mask
    /// pass (owner ruling, 2026-08-16). Headless, because everything worth pinning here is CPU state:
    /// what the child is, which pass it can join, and whether it tracks the body. The pixels are
    /// adjudicated on the 4060 by <c>FoliageSilhouettePlayTests</c>.
    ///
    /// <para><b>Why the flip test earns its place.</b> The walk skin faces west by FLIPPING east. A
    /// mask that ignored <c>flipX</c> would put her silhouette on the wrong side of her own body — half
    /// a cell out, which at PPU 32 is the whole figure — and it would look exactly like a sorting bug,
    /// which is the most expensive kind of wrong to diagnose.</para>
    /// </summary>
    public class SilhouetteMaskSourceTests
    {
        private readonly List<Object> _cleanup = new List<Object>();

        [TearDown]
        public void Cleanup()
        {
            foreach (Object o in _cleanup)
                if (o != null) Object.DestroyImmediate(o);
            _cleanup.Clear();
            SilhouetteRegistry.BindIdle();
        }

        GameObject NewFisher(out SpriteRenderer caster)
        {
            var go = new GameObject("Player");
            _cleanup.Add(go);
            caster = go.AddComponent<SpriteRenderer>();
            caster.sprite = NewSprite(Color.white);
            return go;
        }

        Sprite NewSprite(Color fill)
        {
            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, mipChain: false);
            _cleanup.Add(tex);
            var px = new Color[16];
            for (int i = 0; i < px.Length; i++) px[i] = fill;
            tex.SetPixels(px);
            tex.Apply(false);
            var sprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0f), 32f);
            _cleanup.Add(sprite);
            return sprite;
        }

        // ---- the attachment seam --------------------------------------------------------------------

        [Test]
        public void Attach_GivesTheFisherASource_AndIsIdempotent()
        {
            GameObject fisher = NewFisher(out _);

            SilhouetteMaskSource first = PlayerSilhouetteInstaller.Attach(fisher);
            Assert.IsNotNull(first, "the fisher draws a sprite, so she must get a source");

            SilhouetteMaskSource second = PlayerSilhouetteInstaller.Attach(fisher);
            Assert.AreSame(first, second,
                "a second Attach must return the existing source — the host polls, and a builder or a " +
                "hand-authored prefab may have got there first");
        }

        [Test]
        public void Attach_OnSomethingThatDrawsNothing_IsNotAnError()
        {
            var bare = new GameObject("NotAFisher");
            _cleanup.Add(bare);
            Assert.IsNull(PlayerSilhouetteInstaller.Attach(bare),
                "nothing to silhouette is not a failure — the installer is allowed to be optimistic");
            Assert.IsNull(PlayerSilhouetteInstaller.Attach(null));
        }

        [Test]
        public void TheInstaller_AndTheShadowHost_AgreeOnWhoTheFisherIs()
        {
            Assert.AreEqual(PlayerShadowInstaller.DefaultPlayerObjectName,
                            PlayerSilhouetteInstaller.DefaultPlayerObjectName,
                "both hosts find the player BY NAME. If they ever disagree, one of them silently " +
                "attaches to nothing and the symptom is a fisher with a shadow but no silhouette.");
        }

        // ---- what the child IS ----------------------------------------------------------------------

        [Test]
        public void TheMaskChild_CarriesTheMaskMaterial_AndTheMembershipBit()
        {
            GameObject fisher = NewFisher(out _);
            SilhouetteMaskSource source = PlayerSilhouetteInstaller.Attach(fisher);

            SpriteRenderer mask = source.MaskRenderer;
            Assert.IsNotNull(mask, "the source must build its hidden child on enable");
            Assert.AreEqual(SilhouetteMaskSource.MaskChildName, mask.gameObject.name);

            Assert.IsNotNull(mask.sharedMaterial, "the child must carry the mask material");
            Assert.AreEqual(SilhouetteMaskSource.MaskShaderName, mask.sharedMaterial.shader.name,
                "the child draws through the mask shader — any other shader and it would either draw " +
                "nothing into the pass or draw the fisher a second time in the visible scene");

            Assert.AreEqual(SilhouetteRegistry.RenderingLayer,
                            mask.renderingLayerMask & SilhouetteRegistry.RenderingLayer,
                "MEMBERSHIP IS THE BIT. Without it the renderer carries the HHSilhouette pass but is " +
                "filtered out of the list, so the mask is empty and the fisher never shows through.");
        }

        /// <summary>
        /// ⚠️ THE ExecuteAlways ORDERING TRAP, pinned. An <c>[ExecuteAlways]</c> component wakes the
        /// instant it is added, so a child built ACTIVE would run one frame in its default state — an
        /// unconfigured SpriteRenderer with no material and no rendering layer, i.e. one frame of a
        /// white quad in the filtered pass. This project has been bitten by that twice this month. The
        /// observable is that the child is fully configured by the time Attach returns, with no frame
        /// in between; a save/reload MASKS it, which is why it is pinned on a freshly added component.
        /// </summary>
        [Test]
        public void TheMaskChild_IsFullyConfiguredBeforeItIsEverActive()
        {
            GameObject fisher = NewFisher(out _);
            SilhouetteMaskSource source = PlayerSilhouetteInstaller.Attach(fisher);
            SpriteRenderer mask = source.MaskRenderer;

            Assert.IsTrue(mask.gameObject.activeSelf, "the child must end up active");
            Assert.IsNotNull(mask.sharedMaterial,
                "configured BEFORE activation — a child that goes active before its material is set " +
                "draws one frame of an unconfigured renderer into the mask");
            Assert.AreNotEqual(0u, mask.renderingLayerMask & SilhouetteRegistry.RenderingLayer);
            Assert.IsNotNull(mask.sprite, "and it must already be on the fisher's current sprite");
        }

        // ---- what the child DOES --------------------------------------------------------------------

        [Test]
        public void TheMask_FollowsTheBodysCurrentSprite()
        {
            GameObject fisher = NewFisher(out SpriteRenderer caster);
            SilhouetteMaskSource source = PlayerSilhouetteInstaller.Attach(fisher);

            Sprite next = NewSprite(Color.red);
            caster.sprite = next;
            source.SyncNow();

            Assert.AreSame(next, source.MaskRenderer.sprite,
                "an animating body changes sprite several times a second; a mask a frame behind reads " +
                "as a second fisher a step behind the first");
        }

        [Test]
        public void TheMask_FollowsTheBodysFlip()
        {
            GameObject fisher = NewFisher(out SpriteRenderer caster);
            SilhouetteMaskSource source = PlayerSilhouetteInstaller.Attach(fisher);

            caster.flipX = true;
            source.SyncNow();
            Assert.IsTrue(source.MaskRenderer.flipX,
                "the walk skin faces west by flipping east — an unflipped mask puts her silhouette on " +
                "the wrong side of her own body, which reads as a sorting bug");

            caster.flipX = false;
            source.SyncNow();
            Assert.IsFalse(source.MaskRenderer.flipX);
        }

        [Test]
        public void TheMask_GoesWithTheBodyWhenSheStepsToTheWheel()
        {
            GameObject fisher = NewFisher(out SpriteRenderer caster);
            SilhouetteMaskSource source = PlayerSilhouetteInstaller.Attach(fisher);

            source.SyncNow();
            Assert.IsTrue(source.MaskRenderer.enabled, "ashore she is masked");

            // ControlSwitcher disables the caster at the helm — the same gate SpriteShadow keys off.
            caster.enabled = false;
            source.SyncNow();
            Assert.IsFalse(source.MaskRenderer.enabled,
                "a silhouette of a fisher who is not ashore would stamp her shape into the trees from " +
                "the wheelhouse");

            caster.enabled = true;
            source.SyncNow();
            Assert.IsTrue(source.MaskRenderer.enabled, "and stepping back ashore brings it straight back");
        }

        // ---- the registry, which is the feature's zero-cost gate -------------------------------------

        [Test]
        public void ALiveSource_IsWhatMakesTheFeatureRecordAtAll()
        {
            int before = SilhouetteRegistry.Count;

            GameObject fisher = NewFisher(out _);
            SilhouetteMaskSource source = PlayerSilhouetteInstaller.Attach(fisher);
            Assert.AreEqual(before + 1, SilhouetteRegistry.Count,
                "the feature enqueues nothing while Count is 0 — that is the zero-cost guarantee, and " +
                "it is this registration that lifts it");

            source.enabled = false;
            Assert.AreEqual(before, SilhouetteRegistry.Count,
                "a disabled source must leave the set, or a scene with no fisher keeps paying for a " +
                "render pass that stamps nothing");
        }
    }
}
