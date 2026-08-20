using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using HiddenHarbours.UI;

namespace HiddenHarbours.Tests.UI.EditMode
{
    /// <summary>
    /// The tide table's side of the owner's 2026-08-20 key ruling: the notebook takes <c>N</c>, the
    /// tide table moves to <c>Tab</c>. The notebook's half is pinned beside <c>NotebookPresenter</c>
    /// (World tests) — these assemblies deliberately cannot see each other, so each holds its own
    /// half and the shared fact is the literal: <c>Key.N</c> belongs to the book.
    ///
    /// <para>The DEFAULT is what is pinned, via the same serialized field the owner may rebind —
    /// <c>TidePanelInput</c> installs itself from <c>HudController</c> by <c>AddComponent</c>, so the
    /// code default IS the shipped binding (nothing scene-serialized overrides it; the
    /// scene-wired-is-not-builder-wired trap does not apply to a self-installed component).</para>
    /// </summary>
    public class TidePanelKeyTests
    {
        private static Key DefaultToggleKey()
        {
            var go = new GameObject("[TidePanelKeyTest]");
            try
            {
                var input = go.AddComponent<TidePanelInput>();
                FieldInfo field = typeof(TidePanelInput).GetField(
                    "_toggleKey", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.NotNull(field, "_toggleKey is TidePanelInput's serialized binding field");
                return (Key)field.GetValue(input);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void TheTideTableDefaultsToTab()
        {
            Assert.AreEqual(Key.Tab, DefaultToggleKey(),
                "owner ruling 2026-08-20: the tide table lives on Tab");
        }

        [Test]
        public void TheTideTableDoesNotSitOnTheNotebooksKey()
        {
            Assert.AreNotEqual(Key.N, DefaultToggleKey(),
                "Key.N is NotebookPresenter.OpenKey (owner ruling 2026-08-20) — two live consumers " +
                "of one key in the same context is the collision the dev-key ledger exists to stop");
        }
    }
}
