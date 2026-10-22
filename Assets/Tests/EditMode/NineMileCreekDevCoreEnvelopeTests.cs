using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using HiddenHarbours.App;
using HiddenHarbours.App.Editor;
using NUnit.Framework;
using static HiddenHarbours.App.Editor.StPetersLayerRefresh;
using static HiddenHarbours.Tests.EditMode.NineMileCreekLiveGround;
using Envelope = HiddenHarbours.App.Editor.NineMileCreekDevCoreEnvelope;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// ⭐ <b>THE CREEK'S DEV CORE, ONE LEVEL BELOW AN ACTIVE ENVELOPE</b> (NR1a). Travel wakes a destination's roots before
    /// their Start runs, so a dev core baked as an inactive root woke on the way in from St Peters, set its services over
    /// the persistent core's and cleared them as the bootstrap destroyed it; the next region then seeded a second core.
    /// Rice Point's core sits under an envelope (<see cref="LandSceneTemplate"/>); the creek's now does too.
    ///
    /// <para>Text only, with no import and no engine: the committed scene holds the core the bootstrap names, inactive,
    /// under an active envelope root that the roots list carries in its place; step 13 plans nothing on it; on a scene of
    /// the old shape the step makes exactly that move, writes the envelope as Rice Point's is written, and changes nothing
    /// else; and it refuses a core baked active and an envelope changed by hand.</para>
    /// </summary>
    public class NineMileCreekDevCoreEnvelopeTests
    {
        const int GameObjectClass = 1, TransformClass = 4, MonoBehaviourClass = 114, SceneRootsClass = 1660057539;
        const string RicePointScenePath = "Assets/_Project/Scenes/RicePoint.unity";

        // ---- the committed scene --------------------------------------------------------------------------------------

        [Test]
        public void TheCommittedScene_HoldsTheBootstrapsCore_InactiveOneLevelBelowAnActiveEnvelopeRoot()
        {
            SceneYaml s = Scene;
            Doc bootstrap = s.Docs.Single(d => d.IsScript(typeof(DevRegionBootstrap).FullName));
            Doc envelope = s.RootNamed(Envelope.EnvelopeName), core = At(Envelope.EnvelopeName + "/" + Envelope.CoreName);
            Doc envelopeTr = s.TransformOf(envelope), coreTr = s.TransformOf(core);
            List<long> roots = RootsOf(s);
            Assert.That(bootstrap.FieldRef("_devCoreRoot"), Is.EqualTo(core.FileId), "the bootstrap names the core under the envelope");
            Assert.That(core.Field("m_IsActive"), Is.EqualTo("0"), "the core is baked inactive: only the bootstrap wakes it");
            Assert.That(envelope.Field("m_IsActive"), Is.EqualTo("1"), "the envelope is active, so waking the roots changes nothing below it");
            Assert.That(coreTr.FieldRef(Envelope.ParentKey), Is.EqualTo(envelopeTr.FileId), "the core hangs under the envelope");
            Assert.That(envelopeTr.FieldRefs("m_Children"), Is.EqualTo(new[] { coreTr.FileId }), "and is all it holds");
            Assert.That(s.ComponentsOf(envelope).Select(d => d.ClassId), Is.EqualTo(new[] { TransformClass }),
                "the envelope carries a Transform and nothing else");
            Assert.That(roots, Has.Member(envelopeTr.FileId), "the envelope is listed among the scene's roots");
            Assert.That(roots, Has.No.Member(coreTr.FileId), "the core is not");
            Assert.That(s.Docs.Count(d => d.ClassId == GameObjectClass && !d.Stripped && d.Field("m_Name") == Envelope.CoreName),
                Is.EqualTo(1), "the scene holds one dev core");
        }

        [Test]
        public void TheStep_PlansNothingOnTheCommittedScene_OrMovesTheCoreUnderTheEnvelope()
        {
            string before = Scene.Render();
            LayerPatch patch = Envelope.Plan(before, out string after);
            TestContext.WriteLine(patch.Summary());
            Assert.That(Envelope.Plan(after, out string twice).IsEmpty, Is.True, "a second plan, on what the step writes, changes nothing");
            Assert.That(twice == after, Is.True, "and writes the same scene");
            if (patch.IsEmpty)
            {
                TestContext.WriteLine("the committed scene carries step 13");
                Assert.That(after == before, Is.True, "a baked scene is written unchanged");
                return;
            }
            TestContext.WriteLine("the committed scene waits on step 13 (Phase D's bake)");
            Moved(SceneYaml.Parse(before), SceneYaml.Parse(after));
        }

        // ---- the step, on a scene of the old shape ----------------------------------------------------------------------

        [Test]
        public void OnTheOldShape_TheStepPutsTheEnvelopeWhereTheCoreStood_AndChangesNothingElse()
        {
            string before = OldShape(coreActive: false);
            LayerPatch patch = Envelope.Plan(before, out string after);
            TestContext.WriteLine(patch.Summary());
            Assert.That(patch.Ops.Select(o => o.Kind), Is.EquivalentTo(new[] { OpKind.Add, OpKind.Add, OpKind.Edit, OpKind.Edit }),
                "two documents added and two edited, none deleted");
            SceneYaml now = SceneYaml.Parse(after);
            Moved(SceneYaml.Parse(before), now);
            long envelopeTr = now.TransformOf(now.RootNamed(Envelope.EnvelopeName)).FileId;
            Assert.That(RootsOf(now), Is.EqualTo(new[] { ATr, envelopeTr, BTr, BootTr }),
                "the envelope takes the core's place in the hierarchy's order");
            Assert.That(Envelope.Plan(after, out string twice).IsEmpty, Is.True, "a second plan changes nothing");
            Assert.That(twice == after, Is.True, "and writes the same scene");
        }

        [Test]
        public void TheEnvelope_IsWrittenAsRicePointsIs_LineForLine()
        {
            Envelope.Plan(OldShape(coreActive: false), out string after);
            SceneYaml ours = SceneYaml.Parse(after), theirs = SceneYaml.Parse(File.ReadAllText(RicePointScenePath));
            Doc mine = ours.RootNamed(Envelope.EnvelopeName), its = theirs.RootNamed(Envelope.EnvelopeName);
            Assert.That(Shape(mine), Is.EqualTo(Shape(its)), "the envelope's GameObject, as the editor writes Rice Point's");
            Assert.That(Shape(ours.TransformOf(mine)), Is.EqualTo(Shape(theirs.TransformOf(its))),
                "the envelope's Transform, as the editor writes Rice Point's");
        }

        [Test]
        public void TheStep_RefusesACoreBakedActive_AndAnEnvelopeChangedByHand()
        {
            Assert.Throws<Refusal>(() => Envelope.Plan(OldShape(coreActive: true), out _),
                "a core baked active: only the bootstrap may wake it");
            Envelope.Plan(OldShape(coreActive: false), out string after);
            SceneYaml now = SceneYaml.Parse(after);
            long envelope = now.RootNamed(Envelope.EnvelopeName).FileId;
            string asleep = new SceneYaml(now.Preamble, now.Docs.Select(d => d.FileId != envelope ? d
                                : new Doc(d.Text.Replace("  m_IsActive: 1", "  m_IsActive: 0"))).ToList()).Render();
            Assert.That(asleep, Is.Not.EqualTo(after), "premise: the envelope is written inactive");
            Assert.Throws<Refusal>(() => Envelope.Plan(asleep, out _), "an envelope changed by hand is refused, never planned over");
        }

        // ---- what the move leaves -------------------------------------------------------------------------------------

        /// <summary>What step 13 must have done between <paramref name="was"/> and <paramref name="now"/>: added the
        /// envelope's GameObject and Transform just before the roots list, edited the core's Transform and the roots list
        /// by one line each, and left every other document as it was, in its order.</summary>
        static void Moved(SceneYaml was, SceneYaml now)
        {
            Doc core = was.Docs.Single(d => d.ClassId == GameObjectClass && !d.Stripped && d.Field("m_Name") == Envelope.CoreName);
            Doc coreTr = was.TransformOf(core), roots = was.Docs.Single(d => d.ClassId == SceneRootsClass);
            Doc envelope = now.RootNamed(Envelope.EnvelopeName), envelopeTr = now.TransformOf(envelope);

            var wasIds = new HashSet<long>(was.Docs.Select(d => d.FileId));
            Assert.That(wasIds.Where(id => !now.Contains(id)), Is.Empty, "the step deletes nothing");
            List<Doc> docs = now.Docs.ToList();
            int list = docs.FindIndex(d => d.ClassId == SceneRootsClass);
            Assert.That(docs.Where(d => !wasIds.Contains(d.FileId)).Select(d => d.FileId), Is.EqualTo(new[] { envelope.FileId, envelopeTr.FileId }),
                "the step adds the envelope's GameObject and Transform, and nothing else");
            Assert.That(docs.Skip(list - 2).Take(2).Select(d => d.FileId), Is.EqualTo(new[] { envelope.FileId, envelopeTr.FileId }),
                "the two stand just before the roots list, where a new root's documents go");
            Assert.That(was.Docs.Where(d => now.Get(d.FileId).Text != d.Text).Select(d => d.FileId),
                Is.EquivalentTo(new[] { coreTr.FileId, roots.FileId }), "the step edits the core's Transform and the roots list, and nothing else");
            Assert.That(docs.Where(d => wasIds.Contains(d.FileId)).Select(d => d.FileId), Is.EqualTo(was.Docs.Select(d => d.FileId)),
                "every document it holds stays in its order");

            Assert.That(Changed(coreTr, now.Get(coreTr.FileId)),
                Is.EqualTo(new[] { $"  {Envelope.ParentKey}: {{fileID: 0}}", $"  {Envelope.ParentKey}: {{fileID: {envelopeTr.FileId}}}" }),
                "the core's Transform changes one line: it hangs under the envelope");
            Assert.That(Changed(roots, now.Get(roots.FileId)),
                Is.EqualTo(new[] { $"  - {{fileID: {coreTr.FileId}}}", $"  - {{fileID: {envelopeTr.FileId}}}" }),
                "the roots list changes one line: the envelope where the core stood");

            Assert.That(envelope.Field("m_IsActive"), Is.EqualTo("1"), "the envelope is active");
            Assert.That(envelopeTr.FieldRef(Envelope.ParentKey), Is.EqualTo(0L), "at the top of the hierarchy");
            Assert.That(envelopeTr.FieldRefs("m_Children"), Is.EqualTo(new[] { coreTr.FileId }), "with the core its one child");
            Assert.That(new[] { envelopeTr.Field("m_LocalPosition"), envelopeTr.Field("m_LocalRotation"), envelopeTr.Field("m_LocalScale") },
                Is.EqualTo(new[] { "{x: 0, y: 0, z: 0}", "{x: 0, y: 0, z: 0, w: 1}", "{x: 1, y: 1, z: 1}" }),
                "at the origin, unrotated and unscaled: everything under the core stands where it stood");
            Assert.That(now.Get(core.FileId).Text, Is.EqualTo(core.Text), "the core itself is as it was: inactive, its components unchanged");
        }

        /// <summary>The one line two versions of a document differ in, before and after.</summary>
        static string[] Changed(Doc was, Doc now)
        {
            Assert.That(now.Lines.Length, Is.EqualTo(was.Lines.Length), $"&{was.FileId} keeps its line count");
            int[] at = Enumerable.Range(0, was.Lines.Length).Where(i => was.Lines[i] != now.Lines[i]).ToArray();
            Assert.That(at.Length, Is.EqualTo(1), $"&{was.FileId} changes one line");
            return new[] { was.Lines[at[0]], now.Lines[at[0]] };
        }

        static List<long> RootsOf(SceneYaml s) => s.Docs.Single(d => d.ClassId == SceneRootsClass).FieldRefs("m_Roots");

        /// <summary>A document's lines with every local fileID but 0 written #: the shape the editor writes, ids aside.</summary>
        static string[] Shape(Doc d) =>
            d.Lines.Select(l => Regex.Replace(l, @"&\d+|\{fileID: [1-9]\d*\}", m => m.Value[0] == '&' ? "&#" : "{fileID: #}")).ToArray();

        // ---- a scene of the old shape: two roots either side of an inactive core, and the bootstrap that names it ---------

        const long AGo = 101, ATr = 102, CoreGo = 201, CoreTr = 202, RigGo = 301, RigTr = 302, BGo = 401, BTr = 402,
                   BootGo = 501, BootTr = 502, Boot = 503;

        static string OldShape(bool coreActive) => string.Join("\n", new[]
        {
            "%YAML 1.1", "%TAG !u! tag:unity3d.com,2011:",
            Go(AGo, "A", true, ATr), Tr(ATr, AGo, 0),
            Go(CoreGo, Envelope.CoreName, coreActive, CoreTr), Tr(CoreTr, CoreGo, 0, RigTr),
            Go(RigGo, "GameRoot", true, RigTr), Tr(RigTr, RigGo, CoreTr),
            Go(BGo, "B", true, BTr), Tr(BTr, BGo, 0),
            Go(BootGo, "DevRegionBootstrap", true, BootTr, Boot), Tr(BootTr, BootGo, 0),
            $"--- !u!{MonoBehaviourClass} &{Boot}", "MonoBehaviour:", $"  m_GameObject: {{fileID: {BootGo}}}", "  m_Enabled: 1",
            "  m_EditorClassIdentifier: " + typeof(DevRegionBootstrap).Assembly.GetName().Name + "::" + typeof(DevRegionBootstrap).FullName,
            $"  _devCoreRoot: {{fileID: {CoreGo}}}", "  _sceneReviewCamera: {fileID: 0}",
            $"--- !u!{SceneRootsClass} &9223372036854775807", "SceneRoots:", "  m_ObjectHideFlags: 0", "  m_Roots:",
            $"  - {{fileID: {ATr}}}", $"  - {{fileID: {CoreTr}}}", $"  - {{fileID: {BTr}}}", $"  - {{fileID: {BootTr}}}",
        }) + "\n";

        static string Go(long id, string name, bool active, params long[] components) => string.Join("\n",
            new[] { $"--- !u!{GameObjectClass} &{id}", "GameObject:", "  serializedVersion: 6", "  m_Component:" }
                .Concat(components.Select(c => $"  - component: {{fileID: {c}}}"))
                .Concat(new[] { "  m_Name: " + name, "  m_IsActive: " + (active ? "1" : "0") }));

        static string Tr(long id, long go, long parent, params long[] children) => string.Join("\n",
            new[] { $"--- !u!{TransformClass} &{id}", "Transform:", $"  m_GameObject: {{fileID: {go}}}",
                    "  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}", "  m_LocalPosition: {x: 0, y: 0, z: 0}",
                    "  m_LocalScale: {x: 1, y: 1, z: 1}", children.Length == 0 ? "  m_Children: []" : "  m_Children:" }
                .Concat(children.Select(c => $"  - {{fileID: {c}}}"))
                .Concat(new[] { $"  {Envelope.ParentKey}: {{fileID: {parent}}}" }));
    }
}
