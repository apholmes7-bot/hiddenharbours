namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>The player on a seat</b> (kitchens slice 1): a plain object a seat interactable holds while the
    /// player sits. <see cref="Sit"/> remembers when and where and says so on the bus; <see cref="Bank"/>
    /// hands the seated stretch to <see cref="INeedsService.RestSeated"/> (the slow tick may call it so a
    /// read mid-sit is honest); <see cref="Stand"/> banks and says so. While seated,
    /// <see cref="Posture"/> is what a meal eaten now is worth (<see cref="INeedsService.Eat(float, EatingPosture)"/>).
    ///
    /// <para><b>⚠ SEAM(sit pose):</b> nothing on main holds the player still in a pose, so no boat or
    /// building seat interactable is shipped in this slice. The Player module's sit pose calls
    /// <see cref="Sit"/> and <see cref="Stand"/> on this; every rule it needs is already here.</para>
    /// </summary>
    public sealed class SeatSession
    {
        private readonly INeedsService _needs;
        private readonly System.Func<IGameClock> _clock;

        public SeatSession(INeedsService needs = null, System.Func<IGameClock> clock = null)
        {
            _needs = needs;
            _clock = clock ?? (() => GameServices.Clock);
        }

        /// <summary>The seat sat on, or empty.</summary>
        public string SeatId { get; private set; } = "";

        /// <summary>True while sitting.</summary>
        public bool IsSeated => SeatId.Length > 0;

        /// <summary>True while sitting at a dining seat.</summary>
        public bool AtDiningSeat { get; private set; }

        /// <summary>The instant sitting began (or was last banked from).</summary>
        public double BankedFromGameSeconds { get; private set; }

        /// <summary>When the current sit began.</summary>
        public double SatAtGameSeconds { get; private set; }

        /// <summary>What a meal eaten now is worth.</summary>
        public EatingPosture Posture => DiningRules.PostureAt(IsSeated, AtDiningSeat);

        private INeedsService Needs => _needs ?? GameServices.Needs;

        private double Now => _clock()?.TotalSeconds ?? BankedFromGameSeconds;

        /// <summary>Sit on <paramref name="seatId"/>. Sitting on another seat while seated stands up first.</summary>
        public void Sit(string seatId, bool atDiningSeat)
        {
            if (string.IsNullOrEmpty(seatId)) return;
            if (IsSeated) Stand();
            double now = Now;
            SeatId = seatId;
            AtDiningSeat = atDiningSeat;
            SatAtGameSeconds = now;
            BankedFromGameSeconds = now;
            EventBus.Publish(new PlayerSat(seatId, atDiningSeat));
        }

        /// <summary>Hand the seated stretch since the last bank to the needs, and bank from now.</summary>
        public void Bank()
        {
            if (!IsSeated) return;
            Needs?.RestSeated(BankedFromGameSeconds);
            BankedFromGameSeconds = Now;
        }

        /// <summary>Get up: bank the rest and say how long it was.</summary>
        public void Stand()
        {
            if (!IsSeated) return;
            Bank();
            string seat = SeatId;
            double seated = BankedFromGameSeconds - SatAtGameSeconds;
            SeatId = "";
            AtDiningSeat = false;
            EventBus.Publish(new PlayerStood(seat, seated > 0d ? seated : 0d));
        }
    }
}
