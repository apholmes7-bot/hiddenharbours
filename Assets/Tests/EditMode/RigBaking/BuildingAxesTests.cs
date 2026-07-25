using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using NUnit.Framework;
using HiddenHarbours.Tools.RigBaking;

namespace HiddenHarbours.Tests.RigBaking
{
    /// <summary>
    /// Guards the Building Studio's dial model — the axis tables and the options serialiser.
    ///
    /// <para><b>The failure this file exists to prevent is silence.</b> Both building rigs resolve an
    /// option as <c>opts[k] != null ? opts[k] : fallback</c> and then look the value up in a table, so a
    /// misspelled KEY and an unknown VALUE are both perfectly legal — the rig just quietly renders
    /// something else. Nothing throws, nothing warns, and the studio would offer a dial that does
    /// nothing. So every axis key and every hand-transcribed value is checked against the rig source.</para>
    /// </summary>
    public class BuildingAxesTests
    {
        static string RigSource(string rigKey) =>
            File.ReadAllText(Path.Combine(RigCatalog.RepoRoot, RigCatalog.Get(rigKey).ScriptPath));

        // ---- the keys the rig actually reads -----------------------------------------------------

        [TestCase("wharfBuilding")]
        [TestCase("house")]
        public void EveryAxisKey_IsReadByTheRigsResolve(string rigKey)
        {
            // resolve() reads each option either as opts.<key> or through its g('<key>', default)
            // helper, so one of those two spellings must appear. A key the rig never reads is a dial
            // that silently does nothing.
            string src = RigSource(rigKey);

            foreach (var axis in BuildingAxes.For(rigKey))
            {
                bool read = src.Contains($"opts.{axis.Key}")
                            || src.Contains($"g('{axis.Key}'")
                            || src.Contains($"'{axis.Key}'");
                Assert.IsTrue(read,
                    $"{rigKey}: nothing in the rig reads the option '{axis.Key}' — the studio would " +
                    "show a dial that does nothing (these rigs ignore unknown keys silently).");
            }
        }

        [TestCase("wharfBuilding")]
        [TestCase("house")]
        public void TheWindowDensityOption_IsWinDensity_NotTheRigsInternalWinD(string rigKey)
        {
            // A trap I fell into writing this: the wharf rig's TYPES/PRESETS tables spell the field
            // `winD`, so `winD` looks like the option key. It is not. The line that decides is
            //     winD: opts.winDensity != null ? opts.winDensity : T.winD
            // — LEFT of the colon is the internal build field, RIGHT is the option the caller passes.
            // Dialling `winD` is accepted in silence and does nothing, because these rigs ignore
            // unknown keys. The house rig has the identical shape. The kit README was right.
            string src = RigSource(rigKey);
            StringAssert.Contains("opts.winDensity", src,
                $"{rigKey} should read the window density from opts.winDensity");

            var keys = BuildingAxes.For(rigKey).Select(a => a.Key).ToArray();
            CollectionAssert.Contains(keys, "winDensity");
            CollectionAssert.DoesNotContain(keys, "winD",
                "winD is the rig's INTERNAL field name, not an option key — dialling it does nothing");
        }

        // ---- the hand-transcribed values ----------------------------------------------------------

        [TestCase("wharfBuilding")]
        [TestCase("house")]
        public void EveryInlineChoiceValue_AppearsInTheRigSource(string rigKey)
        {
            // Inline values are the only thing in the studio NOT read live from the rig (those axes
            // export no table). They are therefore the only thing that can drift, so they are grepped.
            string src = RigSource(rigKey);

            foreach (var axis in BuildingAxes.For(rigKey))
            {
                if (axis.Kind != BuildingAxisKind.Choice || axis.Inline == null) continue;

                foreach (var value in axis.Inline)
                {
                    // 'none' is the universal absent-value and is often only an implicit default, so it
                    // is exempt — every other value must be a string the rig actually compares against.
                    if (value == "none") continue;

                    StringAssert.Contains($"'{value}'", src,
                        $"{rigKey}.{axis.Key}: the studio offers '{value}' but the rig source never " +
                        "mentions it — the rig would silently fall through to a default.");
                }
            }
        }

        [TestCase("wharfBuilding")]
        [TestCase("house")]
        public void EveryChoiceAxis_HasSomewhereToGetItsValues(string rigKey)
        {
            foreach (var axis in BuildingAxes.For(rigKey))
            {
                if (axis.Kind != BuildingAxisKind.Choice) continue;
                Assert.IsTrue(axis.RigTable != null || (axis.Inline != null && axis.Inline.Length > 0),
                    $"{rigKey}.{axis.Key} is a dropdown with neither a rig table nor an inline list.");
            }
        }

        [TestCase("wharfBuilding")]
        [TestCase("house")]
        public void EveryNamedRigTable_IsActuallyExported(string rigKey)
        {
            // The studio reads dropdown values from these at runtime; a typo'd table name would leave
            // the dropdown empty with no explanation.
            string src = RigSource(rigKey);

            foreach (var table in BuildingAxes.For(rigKey)
                                              .Where(a => a.RigTable != null)
                                              .Select(a => a.RigTable)
                                              .Distinct())
            {
                bool exported = src.Contains($"{table},") || src.Contains($"{table}:")
                                || src.Contains($"{table} ");
                Assert.IsTrue(exported,
                    $"{rigKey}: the axis table '{table}' is not exported by the rig.");
            }
        }

        [TestCase("wharfBuilding")]
        [TestCase("house")]
        public void AxisKeysAreUnique(string rigKey)
        {
            var keys = BuildingAxes.For(rigKey).Select(a => a.Key).ToArray();
            CollectionAssert.AllItemsAreUnique(keys,
                "a duplicated key would make one dial silently overwrite the other in the literal");
        }

        // ---- the serialiser -----------------------------------------------------------------------

        [Test]
        public void ToOptionsLiteral_WritesAValidJsObject()
        {
            var dialled = new Dictionary<string, object>
            {
                ["type"] = "shack",
                ["size"] = 0.35,
                ["dock"] = true,
                ["stacks"] = 2,
            };

            string js = BuildingAxes.ToOptionsLiteral(dialled);

            Assert.AreEqual("{type:'shack',size:0.35,dock:true,stacks:2}", js);
        }

        [Test]
        public void ToOptionsLiteral_AppendsElevOnlyWhenGiven()
        {
            var d = new Dictionary<string, object> { ["size"] = 0.5 };

            Assert.AreEqual("{size:0.5}", BuildingAxes.ToOptionsLiteral(d));
            Assert.AreEqual("{size:0.5,elev:25}", BuildingAxes.ToOptionsLiteral(d, 25.0));

            // Empty + elev must still be a valid object, not "{,elev:40}".
            Assert.AreEqual("{elev:40}",
                            BuildingAxes.ToOptionsLiteral(new Dictionary<string, object>(), 40.0));
        }

        [Test]
        public void Literal_UsesInvariantCulture_SoACommaDecimalCannotSplitAnOption()
        {
            // On a de-DE/fr-FR machine, 0.35 formats as "0,35" — which inside a JS object literal is a
            // COMMA, silently turning one option into two and shifting every value after it. This is
            // the single nastiest bug available in this file, so it is pinned under a hostile culture.
            var prev = Thread.CurrentThread.CurrentCulture;
            try
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE");

                Assert.AreEqual("0.35", BuildingAxes.Literal(0.35));
                Assert.AreEqual("0.5", BuildingAxes.Literal(0.5f));

                string js = BuildingAxes.ToOptionsLiteral(
                    new Dictionary<string, object> { ["size"] = 0.35, ["weather"] = 0.6 });
                Assert.AreEqual("{size:0.35,weather:0.6}", js);
                Assert.AreEqual(1, js.Count(c => c == ','), "exactly one separator between two options");
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = prev;
            }
        }

        [Test]
        public void Literal_QuotesAndEscapesStrings()
        {
            Assert.AreEqual("'shack'", BuildingAxes.Literal("shack"));
            Assert.AreEqual(@"'it\'s'", BuildingAxes.Literal("it's"));
            Assert.AreEqual(@"'a\\b'", BuildingAxes.Literal(@"a\b"));
            Assert.AreEqual("true", BuildingAxes.Literal(true));
            Assert.AreEqual("null", BuildingAxes.Literal(null));
        }

        [Test]
        public void Literal_RefusesATypeItCannotRepresent()
        {
            // Better a loud throw than a JS syntax error inside the rig call, which surfaces as an
            // unreadable engine exception with no hint where it came from.
            Assert.Throws<ArgumentException>(() => BuildingAxes.Literal(new object()));
        }

        [TestCase("wharfBuilding")]
        [TestCase("house")]
        public void Defaults_SerialiseCleanly(string rigKey)
        {
            string js = BuildingAxes.ToOptionsLiteral(BuildingAxes.Defaults(rigKey));

            Assert.IsTrue(js.StartsWith("{") && js.EndsWith("}"));
            Assert.IsFalse(js.Contains(",,"), "no empty option slots");
            Assert.IsFalse(js.Contains("{,"), "no leading separator");
            Assert.AreEqual(BuildingAxes.For(rigKey).Length,
                            js.Count(c => c == ',') + 1,
                            "one separator between each pair of axes");
        }

        // ---- the build name -----------------------------------------------------------------------

        [Test]
        public void SafeStem_MakesAnOwnerTypedNameFilenameSafe()
        {
            // The studio lets the owner name a build freely; the name becomes a path. A slash would
            // otherwise fail deep inside File.WriteAllBytes with a path error, far from the cause.
            Assert.AreEqual("Bait_Shed", BuildingAxes.SafeStem("Bait Shed", "x"));
            Assert.AreEqual("Ned_s_shed", BuildingAxes.SafeStem("Ned's shed", "x"));
            Assert.AreEqual("a_b", BuildingAxes.SafeStem("a/b", "x"));
            Assert.AreEqual("Shed-2", BuildingAxes.SafeStem("Shed-2", "x"), "hyphens survive");
            Assert.AreEqual("net_shed", BuildingAxes.SafeStem("net_shed", "x"), "underscores survive");
        }

        [Test]
        public void SafeStem_FallsBackRatherThanProducingAnEmptyPath()
        {
            Assert.AreEqual("Build", BuildingAxes.SafeStem("", "Build"));
            Assert.AreEqual("Build", BuildingAxes.SafeStem("   ", "Build"));
            Assert.AreEqual("Build", BuildingAxes.SafeStem("///", "Build"),
                            "a name of nothing but separators must not become an empty filename");
        }

        // ---- the rig set --------------------------------------------------------------------------

        [Test]
        public void BothBuildingRigs_AreDrivable()
        {
            CollectionAssert.AreEquivalent(new[] { "house", "wharfBuilding" }, BuildingAxes.RigKeys);
            foreach (var key in BuildingAxes.RigKeys)
                Assert.Greater(BuildingAxes.For(key).Length, 10, $"{key} should expose its full surface");
        }

        [Test]
        public void AnUnknownRigKey_Throws()
        {
            Assert.Throws<ArgumentException>(() => BuildingAxes.For("lighthouse"));
        }
    }
}
