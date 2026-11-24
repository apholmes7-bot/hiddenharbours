using System.Runtime.CompilerServices;

namespace HiddenHarbours.Core
{
    /// <summary>Session state only. No member is part of SaveData or the save schema.</summary>
    public readonly struct DemoEndingSnapshot
    {
        public readonly bool Ended;
        public DemoEndingSnapshot(bool ended) => Ended = ended;
    }

    /// <summary>The end decision after a real sleep and its successful write. Owns no presentation.</summary>
    public sealed class DemoEnding
    {
        public const string WeatherTurnedFlag = "st_peters.weather_turned";
        public const string TextKey = "demo.island_week_complete";

        private bool _ended;
        public DemoEndingSnapshot Snapshot => new DemoEndingSnapshot(_ended);

        public DemoEnding(DemoEndingSnapshot snapshot = default) => _ended = snapshot.Ended;

        public bool AfterSleep(bool demo, bool wayOpenAtBedtime, bool slept, bool committed)
        {
            if (!demo || _ended || !wayOpenAtBedtime || !slept || !committed) return false;
            _ended = true;
            EventBus.Publish(new DemoEnded());
            return true;
        }
    }

    /// <summary>One decision per live save blob. A new game gets a fresh decision; discarded games can be collected.</summary>
    public static class DemoEndingSession
    {
        private static readonly ConditionalWeakTable<SaveData, DemoEnding> Decisions = new ConditionalWeakTable<SaveData, DemoEnding>();

        public static DemoEndingSnapshot Capture(ISaveService save)
            => save?.Current != null && Decisions.TryGetValue(save.Current, out DemoEnding decision)
                ? decision.Snapshot : default;

        /// <summary>Restores a supplied session snapshot silently. It is never inferred from a weather flag or a rest timestamp.</summary>
        public static void Restore(ISaveService save, DemoEndingSnapshot snapshot)
        {
            if (save?.Current == null) return;
            Decisions.Remove(save.Current);
            Decisions.Add(save.Current, new DemoEnding(snapshot));
        }

        internal static DemoSleepTicket BeginSleep()
        {
            if (!DemoBuild.Enabled) return default;
            ISaveService save = GameServices.Save;
            if (save?.Current == null) return default;
            return new DemoSleepTicket(save, save.Current, save.GetFlag(DemoEnding.WeatherTurnedFlag),
                Decisions.GetValue(save.Current, _ => new DemoEnding()));
        }

        internal static void CompleteSleep(in DemoSleepTicket ticket, bool slept, bool committed)
        {
            if (ticket.Decision == null || !ReferenceEquals(ticket.Save, GameServices.Save) ||
                !ReferenceEquals(ticket.Game, ticket.Save.Current)) return;
            ticket.Decision.AfterSleep(DemoBuild.Enabled, ticket.WayOpenAtBedtime, slept, committed);
        }
    }

    internal readonly struct DemoSleepTicket
    {
        public readonly ISaveService Save;
        public readonly SaveData Game;
        public readonly bool WayOpenAtBedtime;
        public readonly DemoEnding Decision;

        public DemoSleepTicket(ISaveService save, SaveData game, bool wayOpenAtBedtime, DemoEnding decision)
        {
            Save = save;
            Game = game;
            WayOpenAtBedtime = wayOpenAtBedtime;
            Decision = decision;
        }
    }
}
