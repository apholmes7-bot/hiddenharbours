using System.Collections.Generic;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.World
{
    /// <summary>
    /// The part of the director that a GATHERING needs and a lone walker does not: the thing in the
    /// middle (a fire, and its light), and what people say to each other round it.
    ///
    /// <para><b>Still nothing saved</b> (rule 5). What is in the middle and when is read from the plans
    /// the villagers were handed for today: it is there from the first arrival until the last person
    /// leaves (and a fire for a while after), so loading a save at any hour shows what a day played
    /// through would. Which exchange was had today is session memory, the same as
    /// <see cref="NpcConversationDirector"/>: the worst a reload does is let it be said again.</para>
    /// </summary>
    public sealed partial class BeachLifeDirector
    {
        /// <summary>How often the gatherings are looked at, in real seconds.</summary>
        public const float GatheringTickSeconds = 0.5f;

        /// <summary>How near their own place somebody must be to count as at the gathering, in metres
        /// beyond the ring.</summary>
        public const float AtGatheringSlackMetres = 1f;

        private const int StreamTalk = 11;

        /// <summary>One gathering of one day, as the plans handed out so far describe it.</summary>
        private sealed class Gathering
        {
            public BeachActivityDef Activity;
            public int Day;
            public int Version;
            public Vector2 Centre;
            public float FirstArrivalHour;
            public float LastLeaveHour;
            public int People;
            public GameObject Middle;
            public int TalkedOnDay = int.MinValue;
        }

        private readonly List<Gathering> _gatherings = new List<Gathering>(2);
        private readonly Dictionary<string, VillagerRoutine> _bodies = new Dictionary<string, VillagerRoutine>(16);
        private readonly List<VillagerRoutine> _held = new List<VillagerRoutine>(4);
        private float _bodiesAt = float.NegativeInfinity;
        private float _gatheringClock;
        private bool _subscribed;

        private Gathering _talkingAt;
        private BeachTalk _talk;
        private int _talkLine = -1;
        private int _pendingSpeechId;

        /// <summary>How many gatherings have something in the middle right now (a fire burning).</summary>
        public int MiddlesShown
        {
            get
            {
                int n = 0;
                for (int i = 0; i < _gatherings.Count; i++) if (_gatherings[i].Middle != null) n++;
                return n;
            }
        }

        /// <summary>The thing in the middle of the first gathering that has one, or null.</summary>
        public GameObject FirstMiddle
        {
            get
            {
                for (int i = 0; i < _gatherings.Count; i++)
                    if (_gatherings[i].Middle != null) return _gatherings[i].Middle;
                return null;
            }
        }

        /// <summary>Is an exchange being had at a gathering right now.</summary>
        public bool IsTalkRunning => _talkingAt != null;

        /// <summary>How many lines of beach talk have been asked for since the director woke.</summary>
        public int TalkLinesSpoken { get; private set; }

        /// <summary>Look at the gatherings now, without waiting for the tick (tests, and a load).</summary>
        public void RefreshGatheringsNow() => TickGatherings();

        private void Update()
        {
            if (_gatherings.Count == 0) return;
            _gatheringClock -= Time.unscaledDeltaTime;
            if (_gatheringClock > 0f) return;
            _gatheringClock = GatheringTickSeconds;
            TickGatherings();
        }

        /// <summary>Note a gathering outing as it is handed to a villager.</summary>
        private void NoteGathering(in BeachOuting outing, int dayIndex)
        {
            BeachActivityDef activity = outing.Activity;
            if (activity == null || activity.Kind != BeachActivityKind.Gather) return;

            int version = _version;
            Gathering g = null;
            for (int i = 0; i < _gatherings.Count; i++)
                if (ReferenceEquals(_gatherings[i].Activity, activity)) { g = _gatherings[i]; break; }
            if (g == null) { g = new Gathering { Activity = activity, Day = int.MinValue }; _gatherings.Add(g); }

            if (g.Day != dayIndex || g.Version != version)
            {
                Clear(g);
                g.Day = dayIndex;
                g.Version = version;
                g.Centre = outing.GatherAt;
                g.FirstArrivalHour = outing.ArrivalHour;
                g.LastLeaveHour = outing.LeaveHour;
                g.People = 1;
                return;
            }

            g.FirstArrivalHour = Mathf.Min(g.FirstArrivalHour, outing.ArrivalHour);
            g.LastLeaveHour = Mathf.Max(g.LastLeaveHour, outing.LeaveHour);
            g.People++;
        }

        private void TickGatherings()
        {
            IGameClock clock = GameServices.Clock;
            // A region change replaces the ground, and that moves the version: no region compare needed.
            int version = Version;

            for (int i = _gatherings.Count - 1; i >= 0; i--)
            {
                Gathering g = _gatherings[i];
                bool current = clock != null && g.Day == clock.DayIndex && g.Version == version;
                if (!current)
                {
                    if (ReferenceEquals(_talkingAt, g)) FinishTalk();
                    Clear(g);
                    _gatherings.RemoveAt(i);
                    continue;
                }

                float hour = clock.HourOfDay;
                bool peopleThere = hour >= g.FirstArrivalHour && hour <= g.LastLeaveHour;
                bool middleThere = hour >= g.FirstArrivalHour &&
                                   hour <= g.LastLeaveHour + g.Activity.CentreLingersMinutes / 60f;

                if (middleThere && g.Middle == null) PutDownMiddle(g);
                else if (!middleThere && g.Middle != null) Clear(g);

                if (peopleThere && _talkingAt == null && g.TalkedOnDay != g.Day) TryStartTalk(g);
            }

            // Somebody in an exchange who was destroyed, or walked off, ends it.
            if (_talkingAt != null)
                for (int i = 0; i < _held.Count; i++)
                    if (_held[i] == null) { FinishTalk(); break; }
        }

        private static void PutDownMiddle(Gathering g)
        {
            BeachActivityDef a = g.Activity;
            if (a.CentreSprite == null && !a.CentreIsLit) return;

            var host = new GameObject("BeachGathering:" + a.Id);
            host.transform.position = new Vector3(g.Centre.x, g.Centre.y, 0f);
            g.Middle = host;

            IGroundPropPresentationService draw = GroundPropPresentation.Service;
            if (draw == null) return;
            draw.Dress(host, a.CentreSprite, new GroundGlow
            {
                Lit = a.CentreIsLit,
                Color = a.GlowColor,
                RangeMetres = a.GlowRangeMetres,
                Intensity = a.GlowIntensity,
                Flicker01 = a.GlowFlicker01,
                HeightMetres = a.GlowHeightMetres,
            });
        }

        private static void Clear(Gathering g)
        {
            if (g.Middle != null)
            {
                if (Application.isPlaying) Destroy(g.Middle);
                else DestroyImmediate(g.Middle);
            }
            g.Middle = null;
        }

        private void ClearGatherings()
        {
            FinishTalk();
            for (int i = 0; i < _gatherings.Count; i++) Clear(_gatherings[i]);
            _gatherings.Clear();
        }

        // ---- talk ---------------------------------------------------------------------------------

        private void TryStartTalk(Gathering g)
        {
            BeachTalk[] all = g.Activity.Talk;
            if (all == null || all.Length == 0) return;
            // One voice at a time in the village: the two-handers have the floor when they are on.
            NpcConversationDirector other = NpcConversationDirector.Instance;
            if (other != null && other.Running != null) return;

            IEnvironmentService env = GameServices.Environment;
            if (env == null) return;
            RefreshBodies(g.Activity.Beach.RegionId);

            var day = new BeachDay(env.WorldSeed, g.Day, default, 1f);
            int first = Mathf.Min(all.Length - 1, (int)(RoutineSchedule.Hash01(
                BeachPlanning.GatheringSeed(g.Activity, day), StreamTalk, 0) * all.Length));

            for (int k = 0; k < all.Length; k++)
            {
                BeachTalk talk = all[(first + k) % all.Length];
                if (!EverySpeakerIsThere(talk, g)) continue;
                BeginTalk(g, talk);
                return;
            }
        }

        private bool EverySpeakerIsThere(in BeachTalk talk, Gathering g)
        {
            if (talk.Lines == null || talk.Lines.Length == 0) return false;
            float reach = g.Activity.GatherRadiusMetres + g.Activity.PersonSpacingMetres + AtGatheringSlackMetres;
            for (int i = 0; i < talk.Lines.Length; i++)
            {
                NpcDef speaker = talk.Lines[i].Speaker;
                if (speaker == null) return false;
                if (!_bodies.TryGetValue(speaker.Id, out VillagerRoutine body) || body == null) return false;
                if (!body.IsLiving || body.Pose.Walking || body.IsTalking) return false;
                if (Vector2.Distance(body.transform.position, g.Centre) > reach) return false;
            }
            return true;
        }

        private void BeginTalk(Gathering g, in BeachTalk talk)
        {
            if (!_subscribed)
            {
                EventBus.Subscribe<AmbientSpeechEnded>(OnSpeechEnded);
                _subscribed = true;
            }

            _talkingAt = g;
            _talk = talk;
            _talkLine = -1;
            g.TalkedOnDay = g.Day;

            // Each of them turns to the one who speaks before or after them. A refused hold is not a
            // refused exchange: they are standing in a ring already.
            _held.Clear();
            for (int i = 0; i < talk.Lines.Length; i++)
            {
                VillagerRoutine body = _bodies[talk.Lines[i].Speaker.Id];
                if (_held.Contains(body)) continue;
                _held.Add(body);
            }
            for (int i = 0; i < _held.Count; i++)
                if (_held.Count > 1) _held[i].TryHoldForConversation(_held[(i + 1) % _held.Count].transform);

            SpeakNext();
        }

        private void SpeakNext()
        {
            if (_talkingAt == null) return;
            _talkLine++;
            if (_talk.Lines == null || _talkLine >= _talk.Lines.Length) { FinishTalk(); return; }

            BeachTalkLine line = _talk.Lines[_talkLine];
            NpcDef speaker = line.Speaker;
            if (speaker == null || string.IsNullOrWhiteSpace(line.Text) ||
                !_bodies.TryGetValue(speaker.Id, out VillagerRoutine body) || body == null)
            {
                SpeakNext();
                return;
            }

            var interactable = body.GetComponent<Interactable>();
            Vector3 anchor = interactable != null
                ? interactable.BubbleAnchorOffset
                : new Vector3(0f, Interactable.DefaultBubbleHeightMetres, 0f);

            _pendingSpeechId = AmbientSpeechId.Next();
            TalkLinesSpoken++;
            EventBus.Publish(new AmbientSpeechRequested(
                _pendingSpeechId, body.transform, anchor, speaker.Id, speaker.DisplayName, line.Text,
                speaker.Voice != null ? speaker.Voice.Id : null, AmbientSpeechKind.NpcToNpc));
        }

        private void OnSpeechEnded(AmbientSpeechEnded ended)
        {
            if (_talkingAt == null || ended.Id != _pendingSpeechId) return;
            // The next line answers this one: if nobody could read it, the exchange is over.
            if (ended.Delivered) SpeakNext();
            else FinishTalk();
        }

        private void FinishTalk()
        {
            for (int i = 0; i < _held.Count; i++)
                if (_held[i] != null) _held[i].ReleaseFromConversation();
            _held.Clear();
            _talkingAt = null;
            _talk = default;
            _talkLine = -1;
            _pendingSpeechId = 0;
        }

        private void UnsubscribeTalk()
        {
            if (_subscribed) EventBus.Unsubscribe<AmbientSpeechEnded>(OnSpeechEnded);
            _subscribed = false;
        }

        private void RefreshBodies(string regionId)
        {
            float now = Time.unscaledTime;
            if (now - _bodiesAt <= RosterTrustSeconds && _bodies.Count > 0) return;
            _bodiesAt = now;
            _bodies.Clear();
            foreach (VillagerRoutine v in FindObjectsByType<VillagerRoutine>(FindObjectsSortMode.None))
            {
                RoutineDef r = v != null ? v.Routine : null;
                if (r == null || r.Npc == null || string.IsNullOrEmpty(r.Npc.Id)) continue;
                if (!string.Equals(r.RegionId, regionId, System.StringComparison.Ordinal)) continue;
                _bodies[r.Npc.Id] = v;
            }
        }
    }
}
