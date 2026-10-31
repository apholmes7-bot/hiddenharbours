using System;
using System.IO;
using HiddenHarbours.Core;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.EditMode
{
    public class SaveDeferredWritesTests
    {
        private SaveService _service;
        private GameObject _host;
        private string _path;
        private string _previousPath;
        private Action<string> _previousHook;
        private int _writes;

        [SetUp]
        public void SetUp()
        {
            _path = Path.Combine(Path.GetTempPath(), "hh_deferred_" + Guid.NewGuid().ToString("N"), "fixture.json");
            _previousPath = System.Environment.GetEnvironmentVariable(SaveStore.TestRunSaveVariable);
            System.Environment.SetEnvironmentVariable(SaveStore.TestRunSaveVariable, _path);
            GameServices.Reset();
            ShellFlow.Reset();
            _host = new GameObject(nameof(SaveDeferredWritesTests));
            _host.SetActive(false);
            _service = _host.AddComponent<SaveService>();
            _service.Open(_path);
            GameServices.Save = _service;
            _service.Save();
            _previousHook = SaveStore.BeforeReplaceForTests;
            SaveStore.BeforeReplaceForTests = path => { if (path == _path) _writes++; };
            _writes = 0;
        }

        [TearDown]
        public void TearDown()
        {
            SaveStore.BeforeReplaceForTests = _previousHook;
            if (_host != null) Object.DestroyImmediate(_host);
            GameServices.Reset();
            ShellFlow.Reset();
            System.Environment.SetEnvironmentVariable(SaveStore.TestRunSaveVariable, _previousPath);
        }

        [Test]
        public void TwoSaveCalls_WriteNothingInsideScope_AndWriteOnceOnDispose()
        {
            byte[] before = File.ReadAllBytes(_path);
            using (_service.DeferWrites())
            {
                _service.Current.PlayerName = "First";
                _service.Save();
                _service.Current.PlayerName = "Last";
                _service.Save();
                Assert.AreEqual(0, _writes);
                CollectionAssert.AreEqual(before, File.ReadAllBytes(_path));
            }
            Assert.AreEqual(1, _writes);
            Assert.AreEqual("Last", SaveStore.Read(_path).PlayerName);
        }

        [Test]
        public void ScopeWithoutSave_WritesNothingOnDispose()
        {
            using (_service.DeferWrites()) { }
            Assert.AreEqual(0, _writes);
        }

        [Test]
        public void NestedScopes_WriteOnceAtOutermostDispose()
        {
            using (_service.DeferWrites())
            {
                _service.Save();
                using (_service.DeferWrites()) _service.Save();
                Assert.AreEqual(0, _writes);
            }
            Assert.AreEqual(1, _writes);
        }

        [Test]
        public void SaveAfterDispose_WritesImmediatelyAgain()
        {
            using (_service.DeferWrites()) _service.Save();
            _service.Current.PlayerName = "After";
            _service.Save();
            Assert.AreEqual(2, _writes);
            Assert.AreEqual("After", SaveStore.Read(_path).PlayerName);
        }

        [Test]
        public void SecondDispose_IsANoOp()
        {
            IDisposable scope = _service.DeferWrites();
            _service.Save();
            scope.Dispose();
            scope.Dispose();
            Assert.AreEqual(1, _writes);
            _service.Save();
            Assert.AreEqual(2, _writes);
        }
    }
}
