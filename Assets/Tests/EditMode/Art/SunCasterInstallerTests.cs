using HiddenHarbours.Tests.Support;
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

        SpriteRenderer Sprite(string name, string cell = "StandingFixture")
        {
            var texture = new Texture2D(16, 64);
            _owned.Add(texture);
            var sprite = UnityEngine.Sprite.Create(texture, new Rect(0, 0, 16, 64), Vector2.zero, 32f);
            sprite.name = cell;
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

        /// <summary>
        /// ⭐ A FLOATING MARK THROWS NO LAMP SHADOW. A red channel can a few metres off the St Peters pier-head
        /// lantern laid a six-metre dark spike across open water (2026-11-25). The rule is data: the shipped
        /// rules' <c>floating_mark</c> class, matched on the nav-mark sheet cell, carries
        /// <see cref="SunCasterClassRule.Floats"/>. The can keeps its SUN caster; it is only never a lamp-shadow
        /// candidate. A standing post beside it is untouched.
        /// </summary>
        [Test] public void FloatingMark_ThrowsNoLampShadow_StandingPostStillDoes()
        {
            var can = Sprite("Visual", "NavBuoy_PortCan_s18_0");   // the nav-mark prefab's child, its sheet cell
            var rule = _rules.ClassFor(can);
            Assert.That(rule, Is.Not.Null, "the shipped rules have no class for a nav-mark cell");
            Assert.That(rule.Floats, Is.True, $"a nav-mark cell matched '{rule.Id}', which does not float");

            var canShadow = _host.Consider(can);
            Assert.That(canShadow, Is.Not.Null, "the can keeps its sun caster");
            Assert.That(canShadow.CastsLampShadow, Is.False);
            Assert.That(canShadow.TryGetLampShadowCaster(out _), Is.False,
                "a floating mark must never be a lamp-shadow candidate: open water takes no lamp shadow");

            var post = Sprite("lanternPost_0");
            var postShadow = _host.Consider(post);
            Assert.That(postShadow, Is.Not.Null);
            Assert.That(_rules.ClassFor(post).Floats, Is.False);
            Assert.That(postShadow.CastsLampShadow, Is.True);
            Assert.That(postShadow.TryGetLampShadowCaster(out var state), Is.True, "a standing post still throws");
            Assert.That(state.IsValid, Is.True);
        }

        /// <summary>
        /// The figure's silhouette-mask child draws his own sprite, so any shadow from it is his shadow
        /// twice (the budget plate counted four lamp shadows for one figure between two lamps; in clear
        /// sun its copy showed at the edges of his own). It casts none, sun or lamp: the installer never
        /// gives it a caster and switches off one it finds there. The figure still casts both.
        /// </summary>
        [Test] public void SilhouetteMaskChild_NeverCastsAShadow_SunOrLamp_TheFigureStillDoes()
        {
            var figure = Sprite("Player");
            var mask = Sprite(SilhouetteMaskSource.MaskChildName);
            mask.transform.SetParent(figure.transform, worldPositionStays: false);

            var figureShadow = _host.Consider(figure);
            Assert.That(_host.Consider(mask), Is.Null, "the mask is never given a caster");
            Assert.That(mask.GetComponent<SpriteShadow>(), Is.Null, "no sun caster was added to the mask");
            Assert.That(_host.Consider(mask), Is.Null, "nor on a second scan");
            Assert.That(figureShadow, Is.Not.Null, "the figure keeps his sun caster");
            Assert.That(figureShadow.enabled, Is.True);
            Assert.That(figureShadow.CastsLampShadow, Is.True);
            Assert.That(figureShadow.TryGetLampShadowCaster(out _), Is.True, "the figure himself still throws");

            var premade = Sprite(SilhouetteMaskSource.MaskChildName);
            var premadeShadow = premade.gameObject.AddComponent<SpriteShadow>();
            _host.Consider(premade);
            Assert.That(premadeShadow.enabled, Is.False, "a caster that was already on the mask is switched off");
            Assert.That(premadeShadow.WouldCast, Is.False, "so it draws no sun shadow");
            Assert.That(premadeShadow.CastsLampShadow, Is.False);
            Assert.That(premadeShadow.TryGetLampShadowCaster(out _), Is.False, "and is never a lamp-shadow candidate");
        }

        /// <summary>
        /// A face HUNG at the live waterline (a ladder from a deck down to low water) must not throw its whole
        /// uncut sprite from a foot under the water: the St Peters ladder laid a slanting ladder across open
        /// water at every tide. The installer gives it no caster, on a first scan or a later one. A tidal face
        /// standing up from the bed still casts: a Whelps rock (lip at zero) and a quay crib (lip at its deck, but
        /// no heights hanging from it), since their shadows on dry rock and on the water beside them are true.
        /// The same art without the waterline cut still casts, and the by-name exclusions are unchanged.
        /// </summary>
        [Test] public void HungTidalFace_NeverGainsASunCaster_ARockFaceAndTheDryArtStillDo()
        {
            var heights = new Texture2D(1, 1);
            try { HungFaceCases(heights); }
            finally { Object.DestroyImmediate(heights); }
        }

        void HungFaceCases(Texture2D heights)
        {
            var ladder = Sprite("Fitting_ladder");
            var hung = ladder.gameObject.AddComponent<TidalFaceWaterline>();
            hung.Configure(ladder.transform.position.y, 5.354f);
            hung.ConfigureHeight(heights, -8.12f, 8.12f, -5.354f);   // the St Peters ladder: measured down from its lip
            Assert.That(_host.Consider(ladder), Is.Null, "a face hung from a deck into the tide is given no caster");
            Assert.That(ladder.GetComponent<SpriteShadow>(), Is.Null, "no sun caster was added to it");
            ladder.sortingOrder += 1;   // a changed candidate is evaluated again
            Assert.That(_host.Consider(ladder), Is.Null, "nor when it is looked at again");
            Assert.That(ladder.GetComponent<SpriteShadow>(), Is.Null);

            var dryLadder = Sprite("Fitting_ladder");
            Assert.That(_host.Consider(dryLadder), Is.Not.Null, "the same art standing on land still casts");

            var rockFace = Sprite("Fitting_ladder");
            var rock = rockFace.gameObject.AddComponent<TidalFaceWaterline>();
            rock.Configure(rockFace.transform.position.y, 0f);
            rock.ConfigureHeight(heights, -1.19f, 2.16f, 0f);
            Assert.That(_host.Consider(rockFace), Is.Not.Null, "a tidal face standing up from the bed keeps its caster");

            var crib = Sprite("Fitting_ladder");
            crib.gameObject.AddComponent<TidalFaceWaterline>().Configure(crib.transform.position.y, 3.4f);
            Assert.That(_host.Consider(crib), Is.Not.Null, "a quay crib cut at its deck lip, with no hanging heights, keeps its caster");

            Assert.That(_rules.ExcludedNames, Does.Contain("WharfPass2Deck"), "the by-name exclusions stand as they were");
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
            using var allocation = new ManagedAllocationProbe();
            allocation.Begin();
            for (int i = 0; i < 256; i++) { _host.Consider(standing); _host.Consider(decal); }
            long allocationEvents = allocation.End();
            Assert.That(allocationEvents, Is.Zero);
        }

        [Test] public void SettledSceneScan_AllocatesZeroManagedBytes()
        {
            Sprite("Crate");
            Sprite("GroundDecal");
            for (int i = 0; i < 16; i++) _host.ScanLoadedScenes();
            using var allocation = new ManagedAllocationProbe();
            allocation.Begin();
            for (int i = 0; i < 16; i++) _host.ScanLoadedScenes();
            long allocationEvents = allocation.End();
            Assert.That(allocationEvents, Is.Zero);
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

        [Test] public void DistantArrival_SleepsImmediately_WithoutRemovingItsCaster()
        {
            var cameraGo = new GameObject("ArrivalCullCamera");
            _owned.Add(cameraGo);
            cameraGo.tag = "MainCamera";
            cameraGo.AddComponent<Camera>();
            _rules.CameraDistanceMetres = 10f;
            var far = Sprite("ArrivalCrate");
            far.transform.position = new Vector3(100f, 0f, 0f);
            var shadow = _host.Consider(far);
            Assert.That(shadow, Is.Not.Null);
            Assert.That(shadow.enabled, Is.False, "Sleep on attachment, before any later scan.");
            Assert.That(far.enabled, Is.True);
            Assert.That(far.GetComponent<SpriteShadow>(), Is.SameAs(shadow));
            Assert.That(_host.Consider(Sprite("NearbyCrate")).enabled, Is.True);
        }
    }
}
