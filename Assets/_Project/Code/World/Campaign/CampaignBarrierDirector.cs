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
    /// <para>What is drawn is two things, both a function of where he is and the one flag: the storm
    /// itself (<see cref="Look01"/>: wind, sea, murk and rain, at full where the wall's data says) and
    /// the dark front he sees before he is in it (<see cref="Loom01"/>, a boat's view only). The
    /// moment the flag is set both are cleared, the same step.</para>
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
        // One per wall, in the walls' own order: is its line ready for his next approach, and the
        // soonest it may be thought again.
        private bool[] _lineSpent = System.Array.Empty<bool>();
        private float[] _nextLineAt = System.Array.Empty<float>();
        private Component _hull;
        private Rigidbody2D _body;
        private bool _shading;

        /// <summary>The wall standing in the live region right now, or null.</summary>
        public CampaignBarrierDef Standing { get; private set; }

        /// <summary>How much of the storm's push is on the player this step (0..1).</summary>
        public float Storm01 { get; private set; }

        /// <summary>How much of the storm is drawn and heard over the player this step (0..1).</summary>
        public float Look01 { get; private set; }

        /// <summary>How much of the dark front is across his view this step (0..1).</summary>
        public float Loom01 { get; private set; }

        /// <summary>The push laid on her hull this step, m/s².</summary>
        public float PushAcceleration { get; private set; }

        /// <summary>How many times she has thought a wall's line since this director woke.</summary>
        public int LinesSpoken { get; private set; }

        /// <summary>The seconds the line's cool-down is counted in. Null = real seconds since start-up
        /// (it runs on through a pause; nothing of it is saved). A proof sets its own.</summary>
        public System.Func<float> Seconds { get; set; }

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
            if (_walls.Length == 0)
                Take(Resources.LoadAll<CampaignBarrierDef>(ResourcesFolder));
        }

        /// <summary>Put the walls in id order, so which wall is walked first never depends on how the
        /// asset database happened to list them.</summary>
        private static void Sort(CampaignBarrierDef[] walls)
        {
            System.Array.Sort(walls, (a, b) => string.CompareOrdinal(a != null ? a.Id : null, b != null ? b.Id : null));
        }

        private void Take(CampaignBarrierDef[] walls)
        {
            _walls = walls;
            Sort(_walls);
            _lineSpent = new bool[_walls.Length];
            _nextLineAt = new float[_walls.Length];
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
            Take(walls != null ? (CampaignBarrierDef[])walls.Clone() : System.Array.Empty<CampaignBarrierDef>());
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
            CampaignBarrierDef first = FindStanding();
            Transform at = null;
            Component hull = null;
            if (first != null)
            {
                // ⚠ `as Component` then Unity's own null test: a destroyed hull is fake-null.
                hull = (GameServices.Helm.PilotedHull ?? GameServices.Helm.PlayersBoat) as Component;
                if (hull == null) hull = null;
                at = hull != null ? hull.transform : GameServices.PlayerTransform;
            }
            if (first == null || at == null) { Release(); return; }

            Vector2 position = at.position;
            if (hull != null && !ReferenceEquals(hull, _hull))
            {
                _hull = hull;
                _body = hull.GetComponent<Rigidbody2D>();
            }
            Rigidbody2D body = hull != null && _body != null && _body.bodyType == RigidbodyType2D.Dynamic
                ? _body : null;
            Vector2 velocity = body != null ? body.linearVelocity : Vector2.zero;

            // A region may stand several walls (an island ringed on every side). Each one pushes on its
            // own; the strongest storm is the one drawn, heard and thought, and the front that shows
            // most is the one front drawn. The walls are walked in id order (Sort), so the same place
            // and flags always give the same push and the same picture.
            CampaignBarrierDef wall = first, lookWall = first, loomWall = first;
            int wallIndex = -1;
            float storm = 0f, look = 0f, loom = 0f, cap = 0f;
            Vector2 push = Vector2.zero;
            string region = GameServices.CurrentRegionId;
            for (int i = 0; i < _walls.Length; i++)
            {
                var w = _walls[i];
                if (w == null || w.RegionId != region || !Stands(w)) continue;

                float l = w.Look01At(position);
                if (l > look) { look = l; lookWall = w; }
                // The front is a thing seen from a boat, out over open water. Ashore he sees it only
                // with the weather itself, so the village never stands under a dark edge.
                float m = hull != null ? w.Loom01At(position) : l;
                if (m > loom) { loom = m; loomWall = w; }

                float s = w.Storm01At(position);
                if (s <= 0f) { _lineSpent[i] = false; continue; }   // clear of it: his next approach is a new one
                if (s > storm) { storm = s; wall = w; wallIndex = i; }
                if (body == null) continue;
                float a = w.PushAccelerationAt(position, velocity);
                if (a <= 0f) continue;
                push += w.PushDirection * a;
                cap = Mathf.Max(cap, w.MaxPushAcceleration);
            }
            // Where two walls meet (a corner) their pushes add, but never past the stronger cap: the cap
            // is what keeps a boat found far out from being thrown.
            if (push.sqrMagnitude > cap * cap) push = push.normalized * cap;

            Standing = wall;
            Storm01 = storm;
            Look01 = look;
            Loom01 = loom;
            PushAcceleration = push.magnitude;
            if (body != null && PushAcceleration > 0f)
                body.AddForce(push * body.mass, ForceMode2D.Force);

            if (look > 0f || loom > 0f)
            {
                LocalWeather.Set(new LocalWeatherShade(look, lookWall.StormWind, lookWall.StormVisibility01,
                    lookWall.StormSeaState01));
                LocalWeather.SetSquall(new LocalSquall(loom, -loomWall.PushDirection, loomWall.SquallDarkness01,
                    loomWall.SquallFeather01, loomWall.SquallTint, lookWall.StormRain01 * look,
                    lookWall.StormRainStreakScale));
                _shading = true;
            }
            else if (_shading)
            {
                LocalWeather.Clear();
                _shading = false;
            }

            // The line: once as he comes into the weather, not again while he stays in it, and not
            // again on a later approach until its cool-down has run.
            if (wallIndex >= 0 && storm >= wall.LineAtStorm01 && !_lineSpent[wallIndex])
            {
                _lineSpent[wallIndex] = true;
                float now = Seconds != null ? Seconds() : Time.unscaledTime;
                if (!string.IsNullOrEmpty(wall.Line) && now >= _nextLineAt[wallIndex])
                {
                    _nextLineAt[wallIndex] = now + wall.LineCooldownSeconds;
                    LinesSpoken++;
                    EventBus.Publish(new AmbientSpeechRequested(
                        AmbientSpeechId.Next(), at, new Vector3(0f, BubbleHeightMetres, 0f),
                        speakerId: null, speakerName: null, line: wall.Line, voiceId: null,
                        kind: AmbientSpeechKind.InnerVoice));
                }
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
            Look01 = 0f;
            Loom01 = 0f;
            PushAcceleration = 0f;
            _hull = null;
            _body = null;
        }
    }
}
