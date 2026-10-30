using System.Collections.Generic;
using HiddenHarbours.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HiddenHarbours.Art
{
    /// <summary>Installs existing sprite shadows in loaded regions and on later arrivals.</summary>
    [DisallowMultipleComponent]
    public sealed class SunCasterInstaller : MonoBehaviour
    {
        struct Candidate
        {
            public SpriteRenderer Renderer;
            public Sprite Sprite;
            public int Order;
            public bool Enabled;
            public bool Active;
            public SpriteShadow Shadow;
        }

        static SunCasterInstaller _instance;
        readonly List<GameObject> _roots = new();
        readonly List<SpriteRenderer> _renderers = new();
        readonly Dictionary<SpriteRenderer, Candidate> _known = new();
        readonly List<SpriteShadow> _installed = new();
        readonly List<SpriteRenderer> _departed = new();
        readonly Dictionary<SceneHandle, bool> _sceneOptOuts = new();
        SunCasterRules _rules;
        float _timer;
        bool _castingEnabled = true;

        public IReadOnlyList<SpriteShadow> Installed => _installed;
        public static SunCasterInstaller Instance => _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetHost() => _instance = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void InstallHost()
        {
            if (_instance != null) return;
            var rules = Resources.Load<SunCasterRules>(nameof(SunCasterRules));
            if (rules == null || !rules.IsValid(out _)) return;
            var host = new GameObject(nameof(SunCasterInstaller)) { hideFlags = HideFlags.HideAndDontSave };
            DontDestroyOnLoad(host);
            _instance = host.AddComponent<SunCasterInstaller>();
            _instance.Configure(rules);
        }

        public void Configure(SunCasterRules rules)
        {
            _rules = rules;
            _known.Clear();
            _sceneOptOuts.Clear();
            ScanLoadedScenes();
        }

        void OnEnable()
        {
            SceneManager.sceneLoaded += SceneLoaded;
            SceneManager.sceneUnloaded += SceneUnloaded;
        }
        void OnDisable()
        {
            SceneManager.sceneLoaded -= SceneLoaded;
            SceneManager.sceneUnloaded -= SceneUnloaded;
        }
        void OnDestroy() { if (_instance == this) _instance = null; }
        void SceneLoaded(Scene scene, LoadSceneMode mode) => ScanScene(scene);
        void SceneUnloaded(Scene scene) => _sceneOptOuts.Remove(scene.handle);

        void Update()
        {
            if (_rules == null) return;
            _timer -= Time.unscaledDeltaTime;
            if (_timer > 0f) return;
            _timer = _rules.ArrivalScanSeconds;
            ScanLoadedScenes();
        }

        public void ScanLoadedScenes()
        {
            if (_rules == null) return;
            for (int i = 0; i < SceneManager.sceneCount; i++) ScanScene(SceneManager.GetSceneAt(i));
            _departed.Clear();
            foreach (var pair in _known)
                if (pair.Value.Renderer == null) _departed.Add(pair.Key);
            foreach (var renderer in _departed) _known.Remove(renderer);
            for (int i = _installed.Count - 1; i >= 0; i--)
                if (_installed[i] == null) _installed.RemoveAt(i);
            RefreshVisibility();
        }

        public void SetCasting(bool enabled)
        {
            _castingEnabled = enabled;
            RefreshVisibility();
        }

        void RefreshVisibility()
        {
            Camera camera = _rules != null && _rules.CameraDistanceMetres > 0f ? Camera.main : null;
            float rangeSquared = _rules != null ? _rules.CameraDistanceMetres * _rules.CameraDistanceMetres : 0f;
            foreach (var shadow in _installed)
            {
                if (shadow == null) continue;
                bool on = _castingEnabled && (camera == null ||
                    ((Vector2)(shadow.transform.position - camera.transform.position)).sqrMagnitude <= rangeSquared);
                if (shadow.enabled != on) shadow.enabled = on;
            }
        }

        public void ScanScene(Scene scene)
        {
            if (_rules == null || !scene.IsValid() || !scene.isLoaded) return;
            if (!_sceneOptOuts.TryGetValue(scene.handle, out bool optOut))
            {
                optOut = _rules.ExcludesScene(scene.name);
                _sceneOptOuts.Add(scene.handle, optOut);
            }
            if (optOut) return;
            _roots.Clear();
            if (_roots.Capacity <= scene.rootCount) _roots.Capacity = scene.rootCount + 1;
            scene.GetRootGameObjects(_roots);
            foreach (var root in _roots)
            {
                _renderers.Clear();
                root.GetComponentsInChildren(true, _renderers);
                foreach (var renderer in _renderers) Consider(renderer);
            }
        }

        /// <summary>Only new or changed candidates evaluate data; a settled pass allocates nothing.</summary>
        public SpriteShadow Consider(SpriteRenderer renderer)
        {
            if (_rules == null || renderer == null) return null;
            if (renderer.sortingOrder >= SortingBands.SunShadePool) return null;
            _known.TryGetValue(renderer, out var previous);
            bool active = renderer.gameObject.activeInHierarchy;
            if (previous.Renderer == renderer && previous.Shadow != null) return previous.Shadow;
            if (renderer.TryGetComponent<SpriteShadow>(out var existing))
            {
                var rule = _rules.ClassFor(renderer);
                if (rule != null && rule.CastOnWalkingDeck) SpriteShadow.Attach(renderer, true);
                _known[renderer] = new Candidate { Renderer = renderer, Shadow = existing };
                return existing;
            }
            if (previous.Renderer == renderer && previous.Sprite == renderer.sprite && previous.Order == renderer.sortingOrder &&
                previous.Enabled == renderer.enabled && previous.Active == active) return null;
            var next = new Candidate { Renderer = renderer, Sprite = renderer.sprite, Order = renderer.sortingOrder,
                Enabled = renderer.enabled, Active = active };
            if (_rules.Qualifies(renderer, out var settings))
            {
                next.Shadow = SpriteShadow.Attach(renderer, settings != null && settings.CastOnWalkingDeck);
                next.Shadow.CastsGroundContact = settings != null ? settings.CastsGroundContact : _rules.DefaultGroundContact;
                next.Shadow.FootOffset = settings != null ? settings.FootOffset : _rules.DefaultFootOffset;
                _installed.Add(next.Shadow);
            }
            _known[renderer] = next;
            return next.Shadow;
        }
    }
}
