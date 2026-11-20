using System;
using System.Collections.Generic;
using System.IO;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>Drive the real relay and cycle against a real save service opened on a disposable
    /// fixture. Both disk bytes and the entire live DTO must stay unchanged, including on shutdown.</summary>
    public class DevAllInstrumentsNeverTouchesTheSaveTests
    {
        private readonly List<Object> _made = new();
        private GameObject _host;
        private SaveService _save;
        private BoatController _boat;
        private HelmControlRelay _relay;
        private DevInstrumentCycle _cycle;
        private GameConfig _config;
        private string _dir;
        private string _path;
        private string _previousPath;
        private Action<string> _previousHook;
        private int _writes;

        [SetUp]
        public void SetUp()
        {
            GameServices.Reset();
            ShellFlow.Reset();
            _dir = Path.Combine(Path.GetTempPath(), "hh_dev_helm_" + Guid.NewGuid().ToString("N"));
            _path = Path.Combine(_dir, "fixture.json");
            _previousPath = System.Environment.GetEnvironmentVariable(SaveStore.TestRunSaveVariable);
            System.Environment.SetEnvironmentVariable(SaveStore.TestRunSaveVariable, _path);
            _host = new GameObject(nameof(DevAllInstrumentsNeverTouchesTheSaveTests));
            _host.SetActive(false); // never bootstrap a service or an editor boat
            _made.Add(_host);
            _save = _host.AddComponent<SaveService>();
            _save.Open(_path);
            GameServices.Save = _save;
            _config = Make<GameConfig>();
            GameServices.Config = _config;
            _boat = _host.AddComponent<BoatController>();
            _relay = _host.AddComponent<HelmControlRelay>();
            _relay.Configure(_boat);
            _relay.DevIgnoreEquipmentGating = true;
            _cycle = _host.AddComponent<DevInstrumentCycle>();
            _cycle.Configure(_boat, _relay);
            _previousHook = SaveStore.BeforeReplaceForTests;
            SaveStore.BeforeReplaceForTests = path => { if (path == _path) _writes++; };
            _writes = 0;
        }

        [TearDown]
        public void TearDown()
        {
            SaveStore.BeforeReplaceForTests = _previousHook;
            foreach (Object obj in _made) if (obj != null) Object.DestroyImmediate(obj);
            _made.Clear();
            GameServices.Reset();
            ShellFlow.Reset();
            System.Environment.SetEnvironmentVariable(SaveStore.TestRunSaveVariable, _previousPath);
            if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
        }

        private T Make<T>() where T : ScriptableObject
        {
            T value = ScriptableObject.CreateInstance<T>();
            _made.Add(value);
            return value;
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ReadsCyclesHullSwapsAndDisabling_LeaveDiskAndLiveSaveIdentical(bool bought)
        {
            const string hullId = "boat.dev_fit_test";
            _save.Current.Money = 731;
            if (bought)
            {
                InstrumentLocker.Add(_save.Current, hullId, BoatEquipment.DepthSounderId);
                InstrumentLocker.Add(_save.Current, hullId, BoatEquipment.GpsId);
            }
            SaveStore.Write(_save.Current, _path);
            byte[] before = File.ReadAllBytes(_path);
            string liveBefore = SaveSerialization.ToJson(_save.Current);

            foreach (string name in new[] { "NoviHelm", "ConsoleSkiffHelm", "CapeIslanderHelm", "SportSkiffHelm" })
            {
                var console = UnityEditor.AssetDatabase.LoadAssetAtPath<HelmConsoleDef>(
                    "Assets/_Project/Data/Boats/Helms/" + name + ".asset");
                Assert.IsNotNull(console);
                var hull = Make<BoatHullDef>();
                hull.Id = hullId;
                hull.Helm = console;
                _boat.SetHull(hull); // the dev picker's in-place hull swap, without its cargo/fit events

                for (int i = 0; i < 12; i++)
                {
                    Assert.IsTrue(_relay.Fit.HasConsole);
                    Assert.AreEqual(console.MaxWind, _relay.Fit.Wind);
                    _cycle.Step();
                    _cycle.StepPilot();
                }
                _config.DevAllHelmInstruments = false;
                var owned = new List<string>();
                InstrumentLocker.OwnedFor(_save.Current, hullId, owned);
                Assert.AreEqual(BoatEquipment.EffectiveFit(console, owned), _relay.Fit);
                SounderKind brow = _relay.DevBrowStep;
                var pilot = _relay.DevPilotStep;
                _cycle.Step();
                _cycle.StepPilot();
                Assert.AreEqual(brow, _relay.DevBrowStep, "switch off also disables the dev keys");
                Assert.AreEqual(pilot, _relay.DevPilotStep);
                _config.DevAllHelmInstruments = true;
            }

            var bare = Make<BoatHullDef>();
            bare.Id = "boat.no_console_test";
            _boat.SetHull(bare);
            Assert.AreEqual(HelmFit.None, _relay.Fit);
            _cycle.Step();
            _cycle.StepPilot();
            Assert.AreEqual(liveBefore, SaveSerialization.ToJson(_save.Current),
                            "no money, ownership, preferences, active hull or other save field may change");
            CollectionAssert.AreEqual(before, File.ReadAllBytes(_path));
            Assert.AreEqual(0, _writes, "even a byte-identical re-save is forbidden");

            Object.DestroyImmediate(_host);
            CollectionAssert.AreEqual(before, File.ReadAllBytes(_path));
            Assert.AreEqual(0, _writes, "destroying the dev components must not flush an override");
        }

        [Test]
        public void FreshRelayAndShippedConfig_DefaultToTheMaximum_ButSwitchOffRestoresOwnership()
        {
            var shippedConfig = UnityEditor.AssetDatabase.LoadAssetAtPath<GameConfig>(
                "Assets/_Project/Data/Config/GameConfig.asset");
            Assert.IsNotNull(shippedConfig);
            Assert.IsTrue(shippedConfig.DevAllHelmInstruments);
            Assert.IsTrue(_config.DevAllHelmInstruments, "new configs also default on");
            var console = Make<HelmConsoleDef>();
            console.SupportsFlushCompass = true;
            console.SupportsRadar = true;
            console.SupportsGps = true;
            console.MaxWind = WindInstrumentGrade.Display;
            var hull = Make<BoatHullDef>();
            hull.Id = "boat.fresh_dev_fit_test";
            hull.Helm = console;
            _boat.SetHull(hull);
            Assert.AreEqual(SounderKind.Fish, _relay.Fit.Sounder);
            Assert.AreEqual(CompassMount.Flush, _relay.Fit.Compass);
            Assert.IsTrue(_relay.Fit.Radar);
            Assert.IsTrue(_relay.Fit.Gps);
            Assert.AreEqual(WindInstrumentGrade.Display, _relay.Fit.Wind);

            _config.DevAllHelmInstruments = false;
            Assert.AreEqual(BoatEquipment.EffectiveFit(console, new List<string>()), _relay.Fit);
            _config.DevAllHelmInstruments = true;
            _relay.DevIgnoreEquipmentGating = false;
            Assert.AreEqual(BoatEquipment.EffectiveFit(console, new List<string>()), _relay.Fit,
                            "the config cannot override the existing dev gate");
        }

        [Test]
        public void WriteDetector_CatchesEvenAByteIdenticalSave_NegativeControl()
        {
            SaveStore.Write(_save.Current, _path);
            byte[] before = File.ReadAllBytes(_path);
            SaveStore.Write(_save.Current, _path);
            CollectionAssert.AreEqual(before, File.ReadAllBytes(_path));
            Assert.AreEqual(1, _writes, "the proof above would fail even on a needless identical write");
        }
    }
}
