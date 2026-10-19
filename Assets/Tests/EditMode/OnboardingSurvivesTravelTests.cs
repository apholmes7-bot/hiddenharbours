using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>The opening's guide travels with the persistent core</b> — in the committed boot scene, and in the
    /// builder that writes it.
    ///
    /// <para><b>Why it must.</b> Most of the guide's beats are heard AWAY from St Peters: the licence, the rod,
    /// buying the dory and repairing her all happen at the Nine Mile Creek shipwright. A crossing switches off
    /// every root of the region left behind, so a guide left in St Peters' scene went deaf at the first
    /// crossing, never heard the repair, and the opening never closed. On a <c>PersistentObject</c> root its
    /// Awake promotes it into DontDestroyOnLoad with the rest of the core, which no crossing touches; quit to
    /// title takes it down with them. The travel itself is <c>OnboardingSurvivesTravelPlayTests</c>'.</para>
    ///
    /// <para><b>Both read TEXT</b>, never a loaded scene, and name the two scripts by the guids in their own
    /// .meta files: a scene that drops the marker, or a builder whose next rebuild would, reds here.</para>
    /// </summary>
    public class OnboardingSurvivesTravelTests
    {
        const string ScenePath = "Assets/_Project/Scenes/StPeters.unity";
        const string BuilderPath = "Assets/_Project/Code/App/Editor/StPetersBuilder.cs";
        const string GuideMetaPath = "Assets/_Project/Code/World/OnboardingDirector.cs.meta";
        const string PersistentMetaPath = "Assets/_Project/Code/App/PersistentObject.cs.meta";

        static string ScriptGuid(string metaPath)
        {
            string guid = Regex.Match(File.ReadAllText(metaPath), @"(?m)^guid: (?<g>[0-9a-f]{32})\r?$").Groups["g"].Value;
            Assert.That(guid, Has.Length.EqualTo(32), metaPath + " names no script guid");
            return guid;
        }

        /// <summary>One YAML document's body, from its <c>--- !u!</c> header to the next one ("" if absent).</summary>
        static string Body(string yaml, string classId, string fileId) =>
            Regex.Match(yaml, @"(?ms)^--- !u!" + classId + " &" + fileId + @"\r?\n(?<body>.*?)(?=^---|\z)").Groups["body"].Value;

        static bool Runs(string monoBehaviour, string guid) =>
            monoBehaviour.Contains("  m_Script: {fileID: 11500000, guid: " + guid + ", type: 3}");

        /// <summary>Does this one statement put a <paramref name="type"/> on the object it makes, either way?</summary>
        static bool Adds(string statement, string type) =>
            statement.Contains("typeof(" + type + ")") || statement.Contains("AddComponent<" + type + ">");

        [Test]
        public void TheScene_OnboardingRoot_CarriesTheGuideAndPersistentObject()
        {
            string yaml = File.ReadAllText(ScenePath);
            string guideGuid = ScriptGuid(GuideMetaPath);
            string persistentGuid = ScriptGuid(PersistentMetaPath);

            var named = Regex.Matches(yaml, @"(?ms)^--- !u!1 &(?<id>-?\d+)\r?\n(?<body>.*?)(?=^---|\z)")
                             .Cast<Match>()
                             .Where(m => Regex.IsMatch(m.Groups["body"].Value, @"(?m)^  m_Name: Onboarding\r?$"))
                             .ToList();
            Assert.That(named, Has.Count.EqualTo(1), "St Peters authors exactly one \"Onboarding\" GameObject");
            string goId = named[0].Groups["id"].Value;
            string go = named[0].Groups["body"].Value;
            Assert.That(Regex.IsMatch(go, @"(?m)^  m_IsActive: 1\r?$"), Is.True,
                "an inactive root never runs Awake, so it would never be promoted into the core");

            int guides = 0, persistents = 0;
            bool root = false;
            foreach (Match component in Regex.Matches(go, @"(?m)^  - component: \{fileID: (?<id>-?\d+)\}"))
            {
                string id = component.Groups["id"].Value;
                string mb = Body(yaml, "114", id);
                if (mb.Length > 0)
                {
                    Assert.That(mb, Does.Contain("  m_GameObject: {fileID: " + goId + "}"),
                        "MonoBehaviour &" + id + " is listed on Onboarding but points at another GameObject");
                    if (Runs(mb, guideGuid)) guides++;
                    if (Runs(mb, persistentGuid)) persistents++;
                }
                string transform = Body(yaml, "4", id);
                if (transform.Length > 0) root = Regex.IsMatch(transform, @"(?m)^  m_Father: \{fileID: 0\}\r?$");
            }

            Assert.That(guides, Is.EqualTo(1), "Onboarding runs the guide (OnboardingDirector) once");
            Assert.That(persistents, Is.EqualTo(1),
                "⭐ Onboarding carries PersistentObject, so the guide travels with the core and hears the dory " +
                "bought and repaired at Nine Mile Creek — without it the first crossing switches it off");
            Assert.That(root, Is.True, "Onboarding is a scene ROOT: DontDestroyOnLoad promotes roots only");
        }

        [Test]
        public void TheBuilder_AddsBoth_OnTheOneOnboardingObject()
        {
            string builder = File.ReadAllText(BuilderPath);

            var made = Regex.Matches(builder, @"new GameObject\(""Onboarding""[^;]*;");
            Assert.That(made, Has.Count.EqualTo(1), "StPetersBuilder makes the \"Onboarding\" object once");
            string statement = made[0].Value;
            Assert.That(Adds(statement, "OnboardingDirector"), Is.True, "the guide rides on that object: " + statement);
            Assert.That(Adds(statement, "PersistentObject"), Is.True,
                "⭐ and so does PersistentObject, or the next rebuild writes a scene whose guide stays behind " +
                "at the first crossing: " + statement);
            Assert.That(Regex.Matches(builder, @"typeof\(OnboardingDirector\)|AddComponent<OnboardingDirector>").Count,
                Is.EqualTo(1), "no second guide anywhere else in the build");
        }
    }
}
