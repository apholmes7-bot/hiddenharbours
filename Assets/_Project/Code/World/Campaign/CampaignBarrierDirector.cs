using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.World
{
    /// <summary>
    /// Stands every <see cref="CampaignBarrierDef"/> in the game: while a wall's flag is unset and the
    /// player's boat is inside its reach in its region, pushes her hull back, lays the storm over the
    /// look and sound (Core's <see cref="LocalWeather"/>), and has her think the wall's one line.
    ///
    /// <para>It adds force to the hull's body and nothing else: no position or speed is ever written,
    /// so nothing teleports, grounds or is damaged. The force is mass times the Def's acceleration,
    /// so a rowed dory and a power boat are turned back alike. The boat is found through Core's helm
    /// slot (rule 4); this lane knows no boat class.</para>
    ///
    /// <para>Self-installing and scene-free, like the other World directors. With no save service
    /// (a fixture, the shell) every wall is down.</para>
    /// </summary>
    public sealed class CampaignBarrierDirector : MonoBehaviour
    {
        /// <summary>The Resources folder every wall asset lives in.</summary>
        public const string ResourcesFolder = "CampaignBarriers";

        /// <summary>How high over her the thought stands, metres (the inner voice's own height).</summary>
        private const float BubbleHeightMetres = 2.2f;

        private static bool _installed;

        public static CampaignBarrierDirector Instance { get; private set; }

        private CampaignBarrierDef[] _walls = System.Array.Empty<CampaignBarrierDef>();
        private Component _hull;
        private Rigidbody2D _body;
        private float _nextLineAt;
        private bool _shading;

        /// <summary>The wall standing in the live region right now, or null.</summary>
        public CampaignBarrierDef Standing { get; private set; }

        /// <summary>How much of the storm is over the player this step (0..1).</summary>
        public float Storm01 { get; private set; }

        /// <summary>The push laid on her hull this step, m/s².</summary>
        public float PushAcceleration { get; private set; }

        /// <summary>How many times she has thought a wall's line since this director woke.</summary>
        public int LinesSpoken { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            if (_installed) return;
            _installed = true;
            var host = new GameObject("CampaignBarrierDirector") { hideFlags = HideFlags.HideAndDontSave };
            DontDestroyOnLoad(host);
            host.AddComponent<CampaignBarrierDirector>();
        }

        private void Awake()
        {
            if (Instance == null) Instance = this;
            if (_walls.Length == 0) _walls = Resources.LoadAll<CampaignBarrierDef>(ResourcesFolder);
        }

        private void OnDestroy()
        {
            Release();
            if (Instance == this) Instance = null;
        }

        private void OnDisable() => Release();

        /// <summary>Replace the walls (fixtures). The committed set comes from Resources.</summary>
        public void LoadWalls(params CampaignBarrierDef[] walls)
        {
            _walls = walls ?? System.Array.Empty<CampaignBarrierDef>();
            _nextLineAt = 0f;
            Release();
        }

        /// <summary>True while <paramref name="wall"/> stands: there is a save, and its flag is unset.</summary>
        public static bool Stands(CampaignBarrierDef wall)
        {
            if (wall == null) return false;
            var save = GameServices.Save;
            if (save == null) return false;
            return string.IsNullOrEmpty(wall.LiftedByFlag) || !save.GetFlag(wall.LiftedByFlag);
        }

        private void FixedUpdate() => Step();

        /// <summary>One physics step of every wall. Public so a proof can step it by hand.</summary>
        public void Step()
        {
            CampaignBarrierDef wall = FindStanding();
            Transform at = null;
            Component hull = null;
            if (wall != null)
            {
                // ⚠ `as Component` then Unity's own null test: a destroyed hull is fake-null.
                hull = (GameServices.Helm.PilotedHull ?? GameServices.Helm.PlayersBoat) as Component;
                if (hull == null) hull = null;
                at = hull != null ? hull.transform : GameServices.PlayerTransform;
            }
            if (wall == null || at == null) { Release(); return; }

            Standing = wall;
            Vector2 position = at.position;
            Storm01 = wall.Storm01At(position);
            PushAcceleration = 0f;

            if (hull != null && Storm01 > 0f)
            {
                if (!ReferenceEquals(hull, _hull))
                {
                    _hull = hull;
                    _body = hull.GetComponent<Rigidbody2D>();
                }
                if (_body != null && _body.bodyType == RigidbodyType2D.Dynamic)
                {
                    PushAcceleration = wall.PushAccelerationAt(position, _body.linearVelocity);
                    if (PushAcceleration > 0f)
                        _body.AddForce(wall.PushDirection * (PushAcceleration * _body.mass), ForceMode2D.Force);
                }
            }

            if (Storm01 > 0f)
            {
                LocalWeather.Set(new LocalWeatherShade(Storm01, wall.StormWind, wall.StormVisibility01,
                    wall.StormSeaState01));
                _shading = true;
            }
            else if (_shading)
            {
                LocalWeather.Clear();
                _shading = false;
            }

            if (Storm01 >= wall.LineAtStorm01 && Storm01 > 0f && !string.IsNullOrEmpty(wall.Line) &&
                Time.unscaledTime >= _nextLineAt)
            {
                _nextLineAt = Time.unscaledTime + wall.LineCooldownSeconds;
                LinesSpoken++;
                EventBus.Publish(new AmbientSpeechRequested(
                    AmbientSpeechId.Next(), at, new Vector3(0f, BubbleHeightMetres, 0f),
                    speakerId: null, speakerName: null, line: wall.Line, voiceId: null,
                    kind: AmbientSpeechKind.InnerVoice));
            }
        }

        private CampaignBarrierDef FindStanding()
        {
            if (GameServices.OpeningCinematicRunning) return null;   // she is a passenger then
            string region = GameServices.CurrentRegionId;
            if (string.IsNullOrEmpty(region)) return null;
            for (int i = 0; i < _walls.Length; i++)
            {
                var wall = _walls[i];
                if (wall != null && wall.RegionId == region && Stands(wall)) return wall;
            }
            return null;
        }

        private void Release()
        {
            if (_shading) LocalWeather.Clear();
            _shading = false;
            Standing = null;
            Storm01 = 0f;
            PushAcceleration = 0f;
            _hull = null;
            _body = null;
        }
    }
}
