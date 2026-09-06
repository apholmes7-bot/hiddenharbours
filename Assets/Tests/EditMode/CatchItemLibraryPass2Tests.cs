using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Art;

namespace HiddenHarbours.Tests
{
    /// <summary>
    /// The shipped <c>CatchItemLibrary</c> asset reads CATCH PASS 2.
    ///
    /// <para>This tests the COMMITTED asset rather than a freshly built one, because the asset is what
    /// the game loads. A test that rebuilds the table in memory and then checks the table it just built
    /// proves the builder is self-consistent and says nothing about what shipped — and "the builder was
    /// improved but nobody re-ran it" is precisely the failure this lane exists to fix (the library was
    /// last built 2026-08-23, pointed at pass-1 sheets, while 240 pass-2 sheets sat on disk with no
    /// consumer).</para>
    /// </summary>
    public class CatchItemLibraryPass2Tests
    {
        const string FishIso = "Assets/_Project/Art/Fishing/Iso";
        const string Storage = "Assets/_Project/Art/Fishing/Storage";

        /// <summary>The three kinds pass 1 could not draw at all — the cheapest proof the shipped table
        /// is a pass-2 build and not a pass-1 one wearing new names.</summary>
        static readonly string[] Pass2OnlyKinds = { "oyster", "periwinkle", "scallop" };

        static CatchItemLibrary Library()
        {
            var lib = AssetDatabase.LoadAssetAtPath<CatchItemLibrary>(
                CatchItemLibraryBuilder.LibraryPath);
            Assert.IsNotNull(lib, $"'{CatchItemLibraryBuilder.LibraryPath}' must be committed — every " +
                                  "container fill in the game resolves its contents through it.");
            return lib;
        }

        [Test]
        public void ShippedLibrary_DrawsTheKindsOnlyPass2Bakes()
        {
            CatchItemLibrary lib = Library();
            foreach (string kind in Pass2OnlyKinds)
                Assert.IsNotNull(lib.SpriteFor(kind, 0),
                    $"'{kind}' has no art in the shipped library. Pass 2 bakes CatchItem2_{kind}.png; " +
                    "pass 1 had no such kind at all, so a library without it was built before the drop. " +
                    "Run Art ▸ Import (after a new drop) ▸ Build Catch Item Library.");
        }

        /// <summary>
        /// Nothing in the shipped table resolves to a superseded pass-1 strip.
        ///
        /// <para><b>The premise is asserted first, and it has to be.</b> "No sprite comes from a pass-1
        /// path" is trivially true of an EMPTY table, and equally true of one whose kinds were all
        /// dropped by a broken build — so this would be the green that hides exactly the breakage it is
        /// meant to catch. The loop below therefore counts what it checked and refuses to pass unless it
        /// actually found the seven strip kinds wired.</para>
        /// </summary>
        [Test]
        public void ShippedLibrary_ResolvesNoSpriteToAPass1Strip()
        {
            CatchItemLibrary lib = Library();

            string[] strips =
            {
                "lobster", "crab", "mussel", "clam", "scallop", "oyster", "periwinkle",
            };

            var offenders = new List<string>();
            int checkedSprites = 0;

            foreach (string kind in strips)
            {
                for (int v = 0; v < 4; v++)
                {
                    Sprite s = lib.SpriteFor(kind, v);
                    if (s == null) continue;
                    checkedSprites++;

                    string path = AssetDatabase.GetAssetPath(s);
                    // 'CatchItem_' is pass 1; 'CatchItem2_' is pass 2. The underscore is what tells
                    // them apart, so match the full stem rather than the shared prefix.
                    if (path.Contains($"{Storage}/CatchItem_"))
                        offenders.Add($"{kind}[{v}] → {path}");
                }
            }

            Assert.Greater(checkedSprites, 0,
                "the shipped library wired NO strip-kind sprites at all — every assertion about which " +
                "PASS they came from would be vacuously true. Build the library.");
            Assert.IsEmpty(offenders,
                "these still resolve to superseded pass-1 strips: " + string.Join(", ", offenders));
        }

        /// <summary>
        /// The held art is wired, and it carries the facing count the RIG bakes rather than a number
        /// this test made up: a crustacean is lofted as a solid and turns through eight headings, a
        /// handful of shellfish is drawn with no camera at all and has one.
        /// </summary>
        [Test]
        public void ShippedLibrary_WiresHeldArt_WithEachKindsOwnFacingCount()
        {
            CatchItemLibrary lib = Library();

            Assert.AreEqual(8, lib.HeldFacingsFor("lobster"),
                "a held lobster comes from Crust2Held_lobster.png, which the crustacean rig bakes at " +
                "eight headings around its BACK-GRIP pivot.");
            Assert.AreEqual(1, lib.HeldFacingsFor("clam"),
                "a handful of clams comes from Shell2Hand_clam.png — the shellfish rig takes no dir at " +
                "all, so one facing is the honest count, not a stand-in for a missing seven.");

            Assert.IsNotNull(lib.HeldSprite("lobster", 0, 0), "held lobster, facing 0");
            Assert.IsNotNull(lib.HeldSprite("lobster", 7, 0), "held lobster, facing 7");
            Assert.IsNotNull(lib.HeldSprite("clam", 0, 0), "a hand of clams");

            // A one-facing kind must ignore a facing it does not have rather than resolving to null:
            // the carrier hands over whatever heading it is using without asking first.
            Assert.IsNotNull(lib.HeldSprite("clam", 5, 0),
                "a non-directional handful still draws when asked for facing 5");
        }

        /// <summary>
        /// A kind with no held art answers 0 facings and null, so a carrier falls back to the icon
        /// rather than drawing a blank sprite in the fisher's hand.
        /// </summary>
        [Test]
        public void UnknownKind_HasNoHeldArt_AndDoesNotThrow()
        {
            CatchItemLibrary lib = Library();
            Assert.AreEqual(0, lib.HeldFacingsFor("kraken"));
            Assert.IsNull(lib.HeldSprite("kraken", 0, 0));
            Assert.IsNull(lib.HeldSprite(null, 0, 0));
            Assert.IsNull(lib.SpriteFor("kraken", 0));
        }
    }
}
