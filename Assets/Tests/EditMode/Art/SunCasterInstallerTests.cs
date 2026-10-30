using System;
using System.Collections.Generic;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.Art.EditMode
{
    public class SunCasterInstallerTests
    {
        readonly List<Object> _owned = new();
        SunCasterRules _rules;
        SunCasterInstaller _host;

        [SetUp] public void SetUp()
        {
            _rules = Object.Instantiate(Resources.Load<SunCasterRules>(nameof(SunCasterRules)));
            _owned.Add(_rules);
            var go = new GameObject("InstallerFixture");
            _owned.Add(go);
            _host = go.AddComponent<SunCasterInstaller>();
            _host.Configure(_rules);
        }

        [TearDown] public void TearDown()
        {
            for (int i = _owned.Count - 1; i >= 0; i--) Object.DestroyImmediate(_owned[i]);
            _owned.Clear();
        }

        SpriteRenderer Sprite(string name)
        {
            var texture = new Texture2D(16, 64);
            _owned.Add(texture);
            var sprite = UnityEngine.Sprite.Create(texture, new Rect(0, 0, 16, 64), Vector2.zero, 32f);
            sprite.name = "StandingFixture";
            _owned.Add(sprite);
            var go = new GameObject(name);
            _owned.Add(go);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = SortingBands.DecorBase;
            renderer.sprite = sprite;
            return renderer;
        }

        [Test] public void Attachment_IsIdempotent()
        {
            var renderer = Sprite("Crate");
            var first = _host.Consider(renderer);
            Assert.That(first, Is.Not.Null);
            Assert.That(_host.Consider(renderer), Is.SameAs(first));
            Assert.That(renderer.GetComponents<SpriteShadow>(), Has.Length.EqualTo(1));
        }

        [Test] public void ExclusionByRule_LeavesDecalAlone()
        {
            foreach (string name in new[] { "GroundDecal", "wisp", "Swimmer 7", "BowSpray", "HullSplash", "ChimneySmoke" })
                Assert.That(_host.Consider(Sprite(name)), Is.Null, name);
        }

        [Test] public void DisabledRenderer_IsLeftAlone_AndCastsWhenItArrives()
        {
            var renderer = Sprite("Villager");
            renderer.enabled = false;
            Assert.That(_host.Consider(renderer), Is.Null);
            Assert.That(renderer.GetComponent<SpriteShadow>(), Is.Null);
            renderer.enabled = true;
            Assert.That(_host.Consider(renderer), Is.Not.Null);
        }

        [Test] public void ExistingCaster_IsNotModifiedDetachedOrDestroyed()
        {
            var renderer = Sprite("Crate");
            var existing = renderer.gameObject.AddComponent<SpriteShadow>();
            existing.FootOffset = 0.75f;
            existing.CastsGroundContact = true;
            existing.enabled = false;
            var parent = renderer.transform.parent;
            _host.Consider(renderer);
            _host.SetCasting(false);
            Assert.That(renderer.GetComponent<SpriteShadow>(), Is.SameAs(existing));
            Assert.That(existing.FootOffset, Is.EqualTo(0.75f));
            Assert.That(existing.CastsGroundContact, Is.True);
            Assert.That(existing.enabled, Is.False);
            Assert.That(renderer.transform.parent, Is.SameAs(parent));
        }

        [Test] public void PerClassContactAndFootOffset_AreApplied()
        {
            _rules.Classes[0].FootOffset = 0.25f;
            var shadow = _host.Consider(Sprite("Player"));
            Assert.That(shadow.CastsGroundContact, Is.False);
            Assert.That(shadow.FootOffset, Is.EqualTo(0.25f));
        }

        [Test] public void ShadowChildrenAndCanvas_NeverGainCasters()
        {
            var renderer = Sprite("Crate");
            _host.Consider(renderer);
            foreach (var child in renderer.GetComponentsInChildren<SpriteRenderer>())
                if (child != renderer) Assert.That(_host.Consider(child), Is.Null);
            var ui = Sprite("Label");
            ui.gameObject.AddComponent<Canvas>();
            Assert.That(_host.Consider(ui), Is.Null);
        }

        [Test] public void ExistingHiddenPlayerCaster_FollowsCoreWalkingDeckStance()
        {
            var renderer = Sprite("Player");
            var shadow = renderer.gameObject.AddComponent<SpriteShadow>();
            renderer.enabled = false;
            _host.Consider(renderer);
            try
            {
                DeckStance.Publish(renderer, new DeckStanceState(Vector2.zero, 0f, Vector2.zero, Vector2.one));
                Assert.That(shadow.TryGetLampShadowCaster(out _), Is.True);
                Assert.That(renderer.enabled, Is.False);
                DeckStance.Clear(renderer);
                Assert.That(shadow.TryGetLampShadowCaster(out _), Is.False);
            }
            finally { DeckStance.Clear(renderer); }
        }

        [Test] public void SettledPass_AllocatesZeroManagedBytes()
        {
            var standing = Sprite("Crate");
            var decal = Sprite("GroundDecal");
            _host.Consider(standing);
            _host.Consider(decal);
            for (int i = 0; i < 16; i++) { _host.Consider(standing); _host.Consider(decal); }
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 256; i++) { _host.Consider(standing); _host.Consider(decal); }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(allocated, Is.Zero);
        }

        [Test] public void SettledSceneScan_AllocatesZeroManagedBytes()
        {
            Sprite("Crate");
            Sprite("GroundDecal");
            for (int i = 0; i < 16; i++) _host.ScanLoadedScenes();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 16; i++) _host.ScanLoadedScenes();
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(allocated, Is.Zero);
        }

        [Test] public void DistanceCullAndProofToggle_OnlyDisableOwnedCasters()
        {
            var cameraGo = new GameObject("CullFixtureCamera");
            _owned.Add(cameraGo);
            cameraGo.tag = "MainCamera";
            cameraGo.AddComponent<Camera>();
            _rules.CameraDistanceMetres = 10f;
            var renderer = Sprite("Crate");
            renderer.transform.position = new Vector3(100f, 0f, 0f);
            var shadow = _host.Consider(renderer);
            _host.ScanLoadedScenes();
            Assert.That(shadow.enabled, Is.False);
            renderer.transform.position = Vector3.zero;
            _host.ScanLoadedScenes();
            Assert.That(shadow.enabled, Is.True);
            _host.SetCasting(false);
            Assert.That(shadow.enabled, Is.False);
            Assert.That(renderer.enabled, Is.True);
            _host.SetCasting(true);
            Assert.That(shadow.enabled, Is.True);
        }
    }
}
