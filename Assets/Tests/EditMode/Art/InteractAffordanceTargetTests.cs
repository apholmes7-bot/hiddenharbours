using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Art;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// The interact highlight copies only a renderer that is DRAWN, and never lights a registrant on the
    /// player's own object. The case that made this a rule: after a dig, the belt pail (registered on the
    /// player) was offered, and the highlight copied the player's body sprite, which the mesh figure holds
    /// off with forceRenderingOff. A faint copy of the old sprite sheet stood over the mesh player.
    /// </summary>
    public class InteractAffordanceTargetTests
    {
        readonly List<GameObject> _made = new();
        readonly List<SpriteRenderer> _scratch = new();
        Texture2D _tex;
        Sprite _sprite;

        [SetUp]
        public void SetUp()
        {
            _tex = new Texture2D(8, 8, TextureFormat.RGBA32, mipChain: false);
            _sprite = Sprite.Create(_tex, new Rect(0, 0, 8, 8), new Vector2(0.5f, 0f), 32f);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _made) if (go != null) Object.DestroyImmediate(go);
            _made.Clear();
            Object.DestroyImmediate(_sprite);
            Object.DestroyImmediate(_tex);
            GameServices.PlayerTransform = null;
            Interactables.Clear();
        }

        GameObject Make(string name, Transform parent = null)
        {
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            _made.Add(go);
            return go;
        }

        SpriteRenderer Body(GameObject go)
        {
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = _sprite;
            return sr;
        }

        [Test]
        public void ADrawnRenderer_IsDrawn()
        {
            Assert.IsTrue(InteractAffordanceTargets.IsDrawn(Body(Make("Bed"))));
        }

        [Test]
        public void ARendererHeldOffByForceRenderingOff_IsNotDrawn()
        {
            // How the mesh figure hides the player's body sprite: enabled stays true.
            SpriteRenderer body = Body(Make("Player"));
            body.forceRenderingOff = true;

            Assert.IsTrue(body.enabled, "the precondition: the body is still enabled");
            Assert.IsFalse(InteractAffordanceTargets.IsDrawn(body),
                "A renderer held off with forceRenderingOff is not on screen. Copying it is how a " +
                "legacy sprite came back over the mesh player.");
        }

        [Test]
        public void DisabledInactiveClearOrEmpty_AreNotDrawn()
        {
            SpriteRenderer off = Body(Make("Off"));
            off.enabled = false;

            SpriteRenderer inactive = Body(Make("Inactive"));
            inactive.gameObject.SetActive(false);

            SpriteRenderer clear = Body(Make("Clear"));
            clear.color = new Color(1f, 1f, 1f, 0f);

            SpriteRenderer empty = Make("Empty").AddComponent<SpriteRenderer>();

            Assert.IsFalse(InteractAffordanceTargets.IsDrawn(off), "disabled");
            Assert.IsFalse(InteractAffordanceTargets.IsDrawn(inactive), "inactive object");
            Assert.IsFalse(InteractAffordanceTargets.IsDrawn(clear), "alpha zero");
            Assert.IsFalse(InteractAffordanceTargets.IsDrawn(empty), "no sprite");
            Assert.IsFalse(InteractAffordanceTargets.IsDrawn(null), "null");
        }

        [Test]
        public void Resolve_SkipsAHiddenRenderer_AndTakesTheFirstDrawnOne()
        {
            GameObject fixture = Make("Fixture");
            SpriteRenderer hidden = Body(fixture);
            hidden.forceRenderingOff = true;
            SpriteRenderer shown = Body(Make("Shown", fixture.transform));

            Assert.AreSame(shown, InteractAffordanceTargets.Resolve(fixture.transform, null, _scratch),
                "The highlight must light what is on screen, not the first renderer in the hierarchy.");
            Assert.AreEqual(0, _scratch.Count, "the scratch list is handed back empty");
        }

        [Test]
        public void Resolve_NothingDrawn_IsNull()
        {
            GameObject fixture = Make("MeshDrawnFixture");
            Body(fixture).forceRenderingOff = true;
            Body(Make("Child", fixture.transform)).enabled = false;

            Assert.IsNull(InteractAffordanceTargets.Resolve(fixture.transform, null, _scratch));
        }

        [Test]
        public void Resolve_ARegistrantOnThePlayer_IsNeverLit_EvenWhenHisSpriteDraws()
        {
            // The belt pail is registered on the player's own object. With the legacy fallback on, his
            // sprite really draws; lighting it still says nothing about the pail.
            GameObject player = Make("Player");
            Body(player);
            GameServices.PlayerTransform = player.transform;

            Assert.IsTrue(InteractAffordanceTargets.IsOnPlayer(player.transform, GameServices.PlayerTransform));
            Assert.IsNull(InteractAffordanceTargets.Resolve(player.transform, GameServices.PlayerTransform, _scratch),
                "A registrant on the player has no picture of its own; the only thing to copy there is " +
                "the player, so nothing is lit.");
        }

        [Test]
        public void Resolve_TheMeshPlayersHiddenBody_IsNeverCopied_WithOrWithoutAPlayerSeam()
        {
            // The exact scene of the bug, with the player seam unpublished (a region played directly):
            // the drawn rule alone still refuses the hidden body.
            GameObject player = Make("Player");
            SpriteRenderer body = Body(player);
            body.forceRenderingOff = true;

            Assert.IsNull(InteractAffordanceTargets.Resolve(player.transform, null, _scratch));
        }

        [Test]
        public void IsOnPlayer_OnlyMatchesThePlayersOwnObject()
        {
            GameObject player = Make("Player");
            GameObject held = Make("HeldThing", player.transform);
            GameObject bed = Make("Bed");

            Assert.IsTrue(InteractAffordanceTargets.IsOnPlayer(player.transform, player.transform));
            Assert.IsFalse(InteractAffordanceTargets.IsOnPlayer(held.transform, player.transform),
                "A thing with its own renderer under him (a carried pail) keeps its highlight.");
            Assert.IsFalse(InteractAffordanceTargets.IsOnPlayer(bed.transform, player.transform));
            Assert.IsFalse(InteractAffordanceTargets.IsOnPlayer(bed.transform, null), "no player published");
        }

        [Test]
        public void ThePresenter_ResolvesThroughTheRule_AndNeverTakesTheFirstChildRenderer()
        {
            // The presenter itself runs only in Play mode, so pin that it asks this rule (at bind time and
            // every armed frame) rather than keeping a second copy of the lookup that could drift back.
            string src = System.IO.File.ReadAllText("Assets/_Project/Code/Art/InteractAffordancePresenter.cs");

            StringAssert.DoesNotContain("GetComponentInChildren<SpriteRenderer>", src,
                "The first child renderer can be a hidden one (the mesh player's body).");
            StringAssert.Contains("InteractAffordanceTargets.Resolve(", src);
            StringAssert.Contains("InteractAffordanceTargets.IsDrawn(_target)", src,
                "A target that stops drawing while armed (the mesh takes over) must drop the overlays.");
        }
    }
}
