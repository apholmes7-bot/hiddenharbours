using NUnit.Framework;
using UnityEditor;
using HiddenHarbours.Fishing;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// Rod Fishing v2 Wave 3 — the AUTHORED personalities, judged as they will actually play (the
    /// content-validation twin of the invariant sweep): every <see cref="RodFightDef"/> asset under
    /// <c>Data/</c> is fought closed-loop through <see cref="RodFightSim"/> across several seeds —
    /// the competent pulse-and-steer hand must LAND it, and a blind pin must SNAP it (P5: daily fish
    /// stay forgiving; skill is a pulse, not a pin). A content agent who authors a personality that
    /// breaks either promise goes red HERE, by asset path, before any playtest.
    /// </summary>
    public class RodFightDataTests
    {
        private static readonly int[] Seeds = { 11, 222, 3333 };

        private static (string path, RodFightDef def)[] AuthoredDefs()
        {
            var list = new System.Collections.Generic.List<(string, RodFightDef)>();
            foreach (string guid in AssetDatabase.FindAssets("t:RodFightDef", new[] { "Assets/_Project/Data" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var def = AssetDatabase.LoadAssetAtPath<RodFightDef>(path);
                if (def != null) list.Add((path, def));
            }
            return list.ToArray();
        }

        [Test]
        public void EveryAuthoredPersonality_LandsToACompetentHand()
        {
            var defs = AuthoredDefs();
            Assert.IsNotEmpty(defs, "at least the template personality ships");
            foreach ((string path, RodFightDef def) in defs)
                foreach (int seed in Seeds)
                {
                    var sim = new RodFightSim(def, new System.Random(seed));
                    Assert.AreEqual(FishFightResult.Landed, RodFightPolicies.PlayCompetent(sim),
                        $"{path} (seed {seed}): the daily rod loop must stay forgiving — a competent " +
                        "pulse-and-steer must land every authored personality");
                }
        }

        [Test]
        public void EveryAuthoredPersonality_SnapsABlindPin()
        {
            foreach ((string path, RodFightDef def) in AuthoredDefs())
                foreach (int seed in Seeds)
                {
                    var sim = new RodFightSim(def, new System.Random(seed));
                    Assert.AreEqual(FishFightResult.Snapped, RodFightPolicies.PlayBlindPin(sim),
                        $"{path} (seed {seed}): holding the reel to the wall must part the line — " +
                        "no authored tuning may make the pin a winning strategy");
                }
        }
    }
}
