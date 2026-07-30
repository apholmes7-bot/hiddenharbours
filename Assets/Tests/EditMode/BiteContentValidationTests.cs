using NUnit.Framework;
using UnityEditor;
using HiddenHarbours.Fishing;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The authored BITE personalities (owner drop §10.2), judged as they will actually play — the
    /// content-validation twin of <see cref="BiteSequenceSimTests"/>' invariant sweep, the
    /// <see cref="RodFightDataTests"/> pattern. A content agent who authors a bite that breaks the
    /// owner's rulings (a window no human can hit, a fish that never returns after a miss) goes red
    /// HERE, by asset path, before any playtest.
    /// </summary>
    public class BiteContentValidationTests
    {
        // A competent-but-human hand: reads the take and strikes ~0.15 s after it opens. Every
        // authored window must be winnable by this hand — the floor under "different by species".
        private const float HumanReactionSeconds = 0.15f;
        private const float Dt = 0.02f;

        private static (string path, BiteDef def)[] AuthoredDefs()
        {
            var list = new System.Collections.Generic.List<(string, BiteDef)>();
            foreach (string guid in AssetDatabase.FindAssets("t:BiteDef", new[] { "Assets/_Project/Data" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var def = AssetDatabase.LoadAssetAtPath<BiteDef>(path);
                if (def != null) list.Add((path, def));
            }
            return list.ToArray();
        }

        [Test]
        public void BiteAssets_Exist_WithStableUniqueIds()
        {
            var defs = AuthoredDefs();
            Assert.IsNotEmpty(defs, "at least the template bite ships");

            var ids = new System.Collections.Generic.HashSet<string>();
            foreach ((string path, BiteDef def) in defs)
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(def.Id), $"{path}: id is required (rule 2)");
                StringAssert.StartsWith("bite.", def.Id, $"{path}: ids follow type.snake_case");
                Assert.IsTrue(ids.Add(def.Id), $"{path}: duplicate id '{def.Id}' — ids are stable and unique");
            }
        }

        [Test]
        public void EveryAuthoredBite_HonoursTheOwnersRulings()
        {
            foreach ((string path, BiteDef def) in AuthoredDefs())
            {
                // "It keeps nibbling" — a miss is never instantly fatal, so she must be ABLE to return.
                Assert.Greater(def.ReturnChance01, 0f,
                    $"{path}: ReturnChance01 = 0 contradicts the owner's miss-keeps-nibbling ruling");
                Assert.GreaterOrEqual(def.MaxPasses, 2,
                    $"{path}: MaxPasses < 2 means a first miss can be the last — same contradiction");

                // "different by species" only works if every window is still humanly hittable.
                Assert.GreaterOrEqual(def.TakeWindowSeconds, 0.25f,
                    $"{path}: a {def.TakeWindowSeconds:0.00}s window is beyond reading a tell and " +
                    "striking — tight is a personality, unhittable is a bug");

                Assert.GreaterOrEqual(def.FalseNibblesMax, def.FalseNibblesMin, $"{path}: nibble band");
                Assert.GreaterOrEqual(def.NibbleGapMaxSeconds, def.NibbleGapMinSeconds, $"{path}: gap band");
                Assert.GreaterOrEqual(def.ReturnDelayMaxSeconds, def.ReturnDelayMinSeconds, $"{path}: return band");
            }
        }

        [Test]
        public void EveryAuthoredBite_HooksToACompetentHand()
        {
            // Closed-loop: the read-the-take hand must hook every authored personality within its own
            // pass budget, across seeds (the RodFightDataTests PlayCompetent discipline).
            int[] seeds = { 11, 222, 3333 };
            foreach ((string path, BiteDef def) in AuthoredDefs())
                foreach (int seed in seeds)
                {
                    var sim = new BiteSequenceSim(def.ToProfile(), new System.Random(seed));
                    float guard = 0f;
                    while (!sim.IsOver && guard < 300f)
                    {
                        bool strike = sim.Phase == BitePhase.TakeOpen
                                      && sim.PhaseSeconds >= HumanReactionSeconds;
                        sim.Tick(Dt, strike);
                        guard += Dt;
                    }
                    Assert.AreEqual(BitePhase.Hooked, sim.Phase,
                        $"{path} (seed {seed}): a competent hand that reads the take and strikes " +
                        $"{HumanReactionSeconds}s in must hook — the daily loop stays forgiving (P5)");
                }
        }

        [Test]
        public void EverySpeciesBiteReference_Resolves()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:FishSpeciesDef", new[] { "Assets/_Project/Data" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var fish = AssetDatabase.LoadAssetAtPath<FishSpeciesDef>(path);
                if (fish == null || fish.Bite == null) continue;   // opt-in — empty is the legacy bite
                Assert.IsFalse(string.IsNullOrWhiteSpace(fish.Bite.Id),
                    $"{path}: wired Bite asset carries no id — a broken reference or an unauthored def");
            }
        }
    }
}
