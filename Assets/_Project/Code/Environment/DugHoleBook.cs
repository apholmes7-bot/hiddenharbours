using System.Collections.Generic;
using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.Environment
{
    /// <summary>One hole on show: where, when it was dug, when it goes, and whose it is.</summary>
    public struct OpenDugHole
    {
        public Vector2 Position;
        public double DugAtSeconds;
        public double EndsAtSeconds;
        /// <summary>True for a villager's hole (from a <see cref="IDugHoleSource"/>), false for the player's.</summary>
        public bool Villager;
    }

    /// <summary>
    /// <b>Which dug holes are open now</b> — the rules behind <see cref="DugHolePresenter"/>, engine-light so
    /// EditMode can drive them without a scene.
    ///
    /// <para><b>The player's holes</b> are told in (one per dig, at the blade's ground point) and remembered
    /// in a small fixed ring for the SESSION only: they are not in the save, so a load forgets them. Each
    /// one's end is worked out the moment it is dug (<see cref="DugHoleMath.EndsAt"/>: the end of its day or
    /// the first tide over it), so after that it costs one compare to know whether it is still open.</para>
    ///
    /// <para><b>The villagers' holes</b> are never told in and never stored: each refresh asks the sources
    /// (<see cref="DugHoleSources"/>) for the holes their diggers have dug so far today, which follow from
    /// the clock. Their ends are remembered by key for the day only, so the tide is walked once per hole,
    /// not once per refresh; the memory is dropped at midnight and on a region change.</para>
    /// </summary>
    public sealed class DugHoleBook
    {
        private struct PlayerHole
        {
            public Vector2 Position;
            public double DugAt;
            public double EndsAt;
            public string Region;
        }

        private PlayerHole[] _player = new PlayerHole[0];
        private int _playerCount;
        private int _playerNext;

        private readonly Dictionary<ulong, double> _villagerEnds = new Dictionary<ulong, double>(64);
        private long _villagerDay = long.MinValue;
        private string _villagerRegion;
        private readonly List<DugHole> _asked = new List<DugHole>(64);

        /// <summary>The holes open at the last <see cref="Refresh"/>, at most the cap it was given.</summary>
        public readonly List<OpenDugHole> Open = new List<OpenDugHole>(64);

        /// <summary>How many player holes are remembered (open or not).</summary>
        public int PlayerHoleCount => _playerCount;

        /// <summary>
        /// The player dug at <paramref name="position"/> at <paramref name="now"/>. Returns false when no hole
        /// is left (dug under water, or the master switch is off). A dig on top of a hole that is still open
        /// digs that hole again rather than drawing a second one.
        /// </summary>
        public bool AddPlayerHole(Vector2 position, double now, string regionId, IDugHoleGround ground,
                                  in DugHoleSettings s, double secondsPerDay)
        {
            if (!s.Enabled) return false;
            double ends = DugHoleMath.EndsAt(ground, position, now, secondsPerDay, s.TideStepSeconds(secondsPerDay));
            if (ends <= now) return false;

            float same = s.SameHoleMetres * s.SameHoleMetres;
            for (int i = 0; i < _playerCount; i++)
            {
                ref PlayerHole h = ref _player[i];
                if (!string.Equals(h.Region, regionId, System.StringComparison.Ordinal)) continue;
                if (!DugHoleMath.IsOpen(h.DugAt, h.EndsAt, now)) continue;
                if ((h.Position - position).sqrMagnitude <= same) return true;
            }

            int capacity = Mathf.Max(1, s.MaxPlayerHoles);
            if (_player.Length != capacity) Resize(capacity);
            _player[_playerNext] = new PlayerHole { Position = position, DugAt = now, EndsAt = ends, Region = regionId };
            _playerNext = (_playerNext + 1) % capacity;
            if (_playerCount < capacity) _playerCount++;
            return true;
        }

        /// <summary>Forget every player hole (a load replaces the world they were dug in).</summary>
        public void ClearPlayerHoles()
        {
            _playerCount = 0;
            _playerNext = 0;
        }

        /// <summary>
        /// Work out the holes open at <paramref name="now"/> in <paramref name="regionId"/> into <see cref="Open"/>
        /// (at most <paramref name="cap"/>): the player's from the ring, the villagers' from
        /// <paramref name="source"/> (null = every registered <see cref="DugHoleSources"/>). Returns the count.
        /// </summary>
        public int Refresh(double now, string regionId, IDugHoleGround ground, in DugHoleSettings s,
                           double secondsPerDay, int cap, IDugHoleSource source = null)
        {
            Open.Clear();
            if (!s.Enabled || cap <= 0) return 0;

            for (int i = 0; i < _playerCount && Open.Count < cap; i++)
            {
                ref PlayerHole h = ref _player[i];
                if (!string.Equals(h.Region, regionId, System.StringComparison.Ordinal)) continue;
                if (!DugHoleMath.IsOpen(h.DugAt, h.EndsAt, now)) continue;
                Open.Add(new OpenDugHole { Position = h.Position, DugAtSeconds = h.DugAt, EndsAtSeconds = h.EndsAt });
            }

            long day = secondsPerDay > 0.0 ? (long)System.Math.Floor(now / secondsPerDay) : 0L;
            if (day != _villagerDay || !string.Equals(regionId, _villagerRegion, System.StringComparison.Ordinal))
            {
                _villagerEnds.Clear();
                _villagerDay = day;
                _villagerRegion = regionId;
            }

            _asked.Clear();
            if (source != null) source.CollectDugHoles(now, _asked);
            else DugHoleSources.CollectAll(now, _asked);

            double dayStart = DugHoleMath.DayStartSeconds(now, secondsPerDay);
            double step = s.TideStepSeconds(secondsPerDay);
            for (int i = 0; i < _asked.Count && Open.Count < cap; i++)
            {
                DugHole h = _asked[i];
                if (h.DugAtSeconds < dayStart) continue;   // an earlier day's: gone at its midnight
                double ends;
                if (h.Key == 0UL || !_villagerEnds.TryGetValue(h.Key, out ends))
                {
                    ends = DugHoleMath.EndsAt(ground, h.Position, h.DugAtSeconds, secondsPerDay, step);
                    if (h.Key != 0UL) _villagerEnds[h.Key] = ends;
                }
                if (!DugHoleMath.IsOpen(h.DugAtSeconds, ends, now)) continue;
                Open.Add(new OpenDugHole
                {
                    Position = h.Position, DugAtSeconds = h.DugAtSeconds, EndsAtSeconds = ends, Villager = true,
                });
            }
            return Open.Count;
        }

        private void Resize(int capacity)
        {
            var grown = new PlayerHole[capacity];
            int keep = Mathf.Min(_playerCount, capacity);
            // Keep the newest: walk back from the last written slot.
            for (int k = 0; k < keep; k++)
            {
                int from = ((_playerNext - 1 - k) % _player.Length + _player.Length) % Mathf.Max(1, _player.Length);
                grown[keep - 1 - k] = _player[from];
            }
            _player = grown;
            _playerCount = keep;
            _playerNext = keep % capacity;
        }
    }
}
