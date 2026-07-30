using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using HiddenHarbours.Tools.RigStudio;

namespace HiddenHarbours.Tests.RigStudio
{
    /// <summary>
    /// The generic acceptance EVERY Rig Studio adapter must pass — run against the registry, so a
    /// kit import that ships an adapter (which is now part of the import standard) is pinned the
    /// moment it lands, with no new test wiring.
    ///
    /// <para>The load-bearing one is the SABOTAGE test: an adapter offered a value its kit never
    /// enumerated must FAIL LOUDLY, never render a fallback. The rigs' shared failure mode is
    /// silence — they resolve options as <c>opts[k] != null ? opts[k] : fallback</c>, so a wrong
    /// value quietly renders something else. The studio's whole promise is that what the owner sees
    /// is what a bake produces; a plausible-looking fallback preview is that promise broken.</para>
    ///
    /// <para>CPU-only, no rendering: every check here either reads schemas or exercises the
    /// validation that runs BEFORE any render, so this file stays cheap no matter how many kits
    /// register.</para>
    /// </summary>
    public class RigStudioAdapterContractTests
    {
        List<RigStudioAdapterRegistry.Entry> _entries;

        [OneTimeSetUp]
        public void Discover() => _entries = RigStudioAdapterRegistry.CreateAll();

        [OneTimeTearDown]
        public void DisposeAll()
        {
            foreach (var entry in _entries) entry.Adapter?.Dispose();
        }

        // ---- discovery -------------------------------------------------------------------------

        [Test]
        public void TheRegistryFindsTheBuildingsAdapter_WithNoRegistrationCall()
        {
            Assert.IsTrue(_entries.Any(e => e.Adapter is VillageBuildingStudioAdapter),
                "VillageBuildingStudioAdapter was not discovered. The registry is TypeCache-driven — " +
                "an adapter appears by existing, with no registration call — so either the adapter " +
                "lost its parameterless constructor or the discovery mechanism broke, and every " +
                "future kit's adapter would vanish with it.");
        }

        [Test]
        public void EveryDiscoveredAdapterConstructs()
        {
            foreach (var entry in _entries)
                Assert.IsNull(entry.Error,
                    $"{entry.Type.Name} failed to construct: {entry.Error}. A broken adapter is a " +
                    "broken tab in the owner's window.");
        }

        // ---- schema well-formedness ------------------------------------------------------------

        [Test]
        public void EveryAdapterDescribesAWellFormedSchema()
        {
            foreach (var entry in _entries.Where(e => e.Adapter != null))
            {
                RigStudioSchema schema = entry.Adapter.Describe();
                string who = entry.Type.Name;

                Assert.IsFalse(string.IsNullOrWhiteSpace(schema.KitName), $"{who}: no kit name");
                Assert.IsNotNull(schema.Axes, $"{who}: null axes");
                Assert.Greater(schema.Axes.Length, 0, $"{who}: a kit with no axes has no studio to be in");

                CollectionAssert.AllItemsAreUnique(schema.Axes.Select(a => a.Key).ToArray(),
                    $"{who}: duplicate axis keys");

                foreach (var axis in schema.Axes)
                {
                    switch (axis.Kind)
                    {
                        case RigStudioAxisKind.Keys:
                            Assert.IsNotNull(axis.Keys, $"{who}.{axis.Key}: Keys axis with no keys");
                            Assert.Greater(axis.Keys.Length, 0, $"{who}.{axis.Key}: empty Keys axis");
                            CollectionAssert.AllItemsAreUnique(axis.Keys, $"{who}.{axis.Key}");
                            Assert.Contains(axis.Default, axis.Keys,
                                $"{who}.{axis.Key}: the default is not one of the enumerated keys");

                            if (axis.Excluded != null)
                                foreach (var kv in axis.Excluded)
                                {
                                    CollectionAssert.DoesNotContain(axis.Keys, kv.Key,
                                        $"{who}.{axis.Key}: '{kv.Key}' is both selectable and " +
                                        "excluded — it must be one or the other");
                                    Assert.IsFalse(string.IsNullOrWhiteSpace(kv.Value),
                                        $"{who}.{axis.Key}: '{kv.Key}' is excluded with no reason — " +
                                        "the kit's own reason is the whole point of showing it");
                                }
                            break;

                        case RigStudioAxisKind.IntRange:
                        case RigStudioAxisKind.Seed:
                            Assert.LessOrEqual(axis.Min, axis.Max, $"{who}.{axis.Key}: Min > Max");
                            Assert.IsInstanceOf<int>(axis.Default, $"{who}.{axis.Key}");
                            int def = (int)axis.Default;
                            Assert.That(def, Is.InRange(axis.Min, axis.Max),
                                $"{who}.{axis.Key}: default outside the kit's own bounds");
                            break;

                        default:
                            Assert.Fail($"{who}.{axis.Key}: unknown axis kind {axis.Kind}");
                            break;
                    }
                }
            }
        }

        [Test]
        public void EveryAdaptersDefaultPoint_PassesItsOwnValidation()
        {
            foreach (var entry in _entries.Where(e => e.Adapter != null))
            {
                RigStudioSchema schema = entry.Adapter.Describe();
                Assert.DoesNotThrow(
                    () => RigStudioPoints.Validate(schema, RigStudioPoints.Defaults(schema)),
                    $"{entry.Type.Name}: the schema's own defaults fail its validation — the " +
                    "window opens broken");
            }
        }

        // ---- THE SABOTAGE: a lie about an axis value fails loudly, never renders a fallback ------

        [Test]
        public void Sabotage_AKeysValueTheKitNeverEnumerated_IsRefused_NeverRenderedAsAFallback()
        {
            foreach (var entry in _entries.Where(e => e.Adapter != null))
            {
                RigStudioSchema schema = entry.Adapter.Describe();
                RigStudioAxis keysAxis = schema.Axes.FirstOrDefault(
                    a => a.Kind == RigStudioAxisKind.Keys);
                if (keysAxis == null) continue;

                var point = RigStudioPoints.Defaults(schema);
                point[keysAxis.Key] = "__sabotage_not_a_kit_value__";

                RigStudioPreview leaked = null;
                var ex = Assert.Throws<ArgumentException>(
                    () => leaked = entry.Adapter.Preview(point),
                    $"{entry.Type.Name}: previewed a value the kit never enumerated. The rigs " +
                    "ignore unknown values IN SILENCE, so whatever pixels came back are some " +
                    "fallback wearing the requested name — the exact defect class the studio " +
                    "exists to retire.");

                Assert.IsNull(leaked, $"{entry.Type.Name}: threw but still produced pixels");
                StringAssert.Contains(keysAxis.Key, ex.Message,
                    $"{entry.Type.Name}: the refusal must name the axis so the failure explains itself");
            }
        }

        [Test]
        public void Sabotage_AnIntOutsideTheKitsOwnBounds_IsRefused()
        {
            foreach (var entry in _entries.Where(e => e.Adapter != null))
            {
                RigStudioSchema schema = entry.Adapter.Describe();
                RigStudioAxis intAxis = schema.Axes.FirstOrDefault(
                    a => a.Kind == RigStudioAxisKind.IntRange || a.Kind == RigStudioAxisKind.Seed);
                if (intAxis == null) continue;

                var point = RigStudioPoints.Defaults(schema);
                point[intAxis.Key] = intAxis.Max + 1;

                Assert.Throws<ArgumentException>(() => entry.Adapter.Preview(point),
                    $"{entry.Type.Name}: previewed {intAxis.Key} = {intAxis.Max + 1}, outside the " +
                    "kit's own bounds");
            }
        }

        [Test]
        public void Sabotage_AnExcludedValue_IsRefused_WithTheKitsOwnReasonVerbatim()
        {
            // No PR-1 adapter excludes anything yet — this arms the moment one does (the tree kit's
            // Tamarack hold-back is the known first case), because it iterates whatever the
            // registry holds rather than naming adapters.
            foreach (var entry in _entries.Where(e => e.Adapter != null))
            {
                RigStudioSchema schema = entry.Adapter.Describe();
                foreach (var axis in schema.Axes)
                {
                    if (axis.Kind != RigStudioAxisKind.Keys || axis.Excluded == null) continue;

                    foreach (var kv in axis.Excluded)
                    {
                        var point = RigStudioPoints.Defaults(schema);
                        point[axis.Key] = kv.Key;

                        var ex = Assert.Throws<ArgumentException>(
                            () => entry.Adapter.Preview(point),
                            $"{entry.Type.Name}: previewed the EXCLUDED '{kv.Key}' — held back " +
                            "means not bakeable, and the preview must refuse where the bake would");

                        StringAssert.Contains(kv.Value, ex.Message,
                            $"{entry.Type.Name}: the refusal must carry the kit's own reason " +
                            "verbatim, not a paraphrase");
                    }
                }
            }
        }

        // ---- validation plumbing (no adapter, no render) -----------------------------------------

        static RigStudioSchema TinySchema() => new RigStudioSchema
        {
            KitName = "test kit",
            Axes = new[]
            {
                new RigStudioAxis
                {
                    Key = "species", Label = "Species", Kind = RigStudioAxisKind.Keys,
                    Keys = new[] { "a", "b" }, Default = "a",
                    Excluded = new Dictionary<string, string> { ["held"] = "held back by its own gate" },
                },
                new RigStudioAxis
                {
                    Key = "facing", Label = "Facing", Kind = RigStudioAxisKind.IntRange,
                    Min = 0, Max = 7, Default = 0,
                },
            },
        };

        [Test]
        public void Validate_RefusesAMissingAxis_BecauseAnAbsentKeyIsHowRigsFallThroughToDefaults()
        {
            var point = new Dictionary<string, object> { ["species"] = "a" };
            Assert.Throws<ArgumentException>(() => RigStudioPoints.Validate(TinySchema(), point));
        }

        [Test]
        public void Validate_RefusesAStrangerKey()
        {
            var schema = TinySchema();
            var point = RigStudioPoints.Defaults(schema);
            point["winD"] = 0.5;    // the recorded worked example of a silently-ignored key
            Assert.Throws<ArgumentException>(() => RigStudioPoints.Validate(schema, point));
        }

        [Test]
        public void Validate_RefusesAnExcludedValue_AndSpeaksTheKitsReason()
        {
            var schema = TinySchema();
            var point = RigStudioPoints.Defaults(schema);
            point["species"] = "held";
            var ex = Assert.Throws<ArgumentException>(() => RigStudioPoints.Validate(schema, point));
            StringAssert.Contains("held back by its own gate", ex.Message);
        }
    }
}
