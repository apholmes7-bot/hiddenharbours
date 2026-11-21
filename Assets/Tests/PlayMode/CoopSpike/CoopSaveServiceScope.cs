using HiddenHarbours.Core;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.CoopSpike
{
    /// <summary>Restore the session service that a memory-only trial deliberately destroys.</summary>
    internal sealed class CoopSaveServiceScope
    {
        private readonly ISaveService _published = GameServices.Save;
        private SaveService _service = Object.FindAnyObjectByType<SaveService>();
        private readonly bool _hadService;
        private readonly bool _publishedService;
        private readonly string _runPath;
        private GameObject _root;

        public CoopSaveServiceScope()
        {
            _hadService = _service != null;
            _root = _hadService ? _service.gameObject : null;
            _publishedService = ReferenceEquals(_published, _service);
            _runPath = System.Environment.GetEnvironmentVariable(SaveStore.TestRunSaveVariable);
            Assert.That(_runPath, Is.Not.Null.And.Not.Empty,
                "The runner must redirect saves before a trial can replace its service.");
        }

        public void Restore()
        {
            Assert.That(System.Environment.GetEnvironmentVariable(SaveStore.TestRunSaveVariable), Is.EqualTo(_runPath));
            if (_hadService && _service == null)
            {
                // BeforeSceneLoad bootstraps run once per play session, not on the next
                // scene load. Recreate the same service against the unchanged run-owned
                // file. Awake reads it and reinstalls subscriptions; it does not write.
                if (_root == null)
                {
                    _root = new GameObject("[SaveService]");
                    Object.DontDestroyOnLoad(_root);
                }
                _service = _root.AddComponent<SaveService>();
            }
            GameServices.Save = _publishedService ? _service : _published;
            if (_hadService)
            {
                Assert.That(Object.FindObjectsByType<SaveService>(FindObjectsInactive.Include,
                    FindObjectsSortMode.None), Is.EqualTo(new[] { _service }));
                Assert.That(RestSaveResponder.Installed, Is.True);
                Assert.That(RestWakeRestorer.Installed, Is.True);
            }
        }
    }
}
