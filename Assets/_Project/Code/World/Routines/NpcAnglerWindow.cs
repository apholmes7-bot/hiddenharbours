namespace HiddenHarbours.World
{
    /// <summary>
    /// <b>When a villager is at his station, on the clock a figure poses on</b>: the block running at an hour,
    /// what it names him doing, and when he arrived there and will leave, turned from the routine's hours into
    /// seconds of the flow clock (<see cref="HiddenHarbours.Core.IGameClock.FlowSeconds"/>). Pure: equal
    /// inputs give equal answers, nothing is stored (rule 5). An angler's slot may begin only inside this
    /// window (<see cref="HiddenHarbours.Core.AnglerTimeline.SlotMayBegin"/>).
    /// </summary>
    public static class NpcAnglerWindow
    {
        /// <summary>
        /// The block running at <paramref name="hourOfDay"/> and the activity it names (null for none), with
        /// his arrival at its station and the next block's departure as flow seconds about
        /// <paramref name="flowNow"/>. Arrival is in the future while he is still walking.
        /// </summary>
        public static NpcActivityDef At(RoutinePlan plan, float hourOfDay, float secondsPerGameHour,
                                        double flowNow, out int block, out double arrivalFlow,
                                        out double departureFlow)
        {
            block = -1;
            arrivalFlow = flowNow;
            departureFlow = flowNow;
            if (plan == null || plan.BlockCount == 0) return null;
            plan.StationWindowAt(hourOfDay, secondsPerGameHour, out block, out float since, out float until);
            arrivalFlow = flowNow - (double)since * secondsPerGameHour;
            departureFlow = flowNow + (double)until * secondsPerGameHour;
            return plan.DoingAt(block);
        }
    }

    /// <summary>
    /// <b>One run at a station, latched</b>: his arrival and departure on the flow clock, taken once when he
    /// is first free to fish and held until the run is unlatched (a new block, a talk, a hurry). The hour clock
    /// is a float, so <see cref="NpcAnglerWindow.At"/> re-read every frame jitters by a few milliseconds; a slot
    /// that ends within that jitter of departure would otherwise begin and un-begin mid-session.
    /// </summary>
    public struct AnglerRunWindow
    {
        /// <summary>True once <see cref="Latch"/> has taken this run's window.</summary>
        public bool IsLatched { get; private set; }

        /// <summary>The latched arrival, flow seconds. After an interruption, no earlier than the moment he
        /// was free again, so he begins no slot that started while he was talking.</summary>
        public double ArrivalFlow { get; private set; }

        /// <summary>The latched departure, flow seconds.</summary>
        public double DepartureFlow { get; private set; }

        /// <summary>Takes the window if none is held; a no-op while latched.</summary>
        public void Latch(double arrivalFlow, double departureFlow, double flowNow, bool interrupted)
        {
            if (IsLatched) return;
            ArrivalFlow = interrupted ? System.Math.Max(arrivalFlow, flowNow) : arrivalFlow;
            DepartureFlow = departureFlow;
            IsLatched = true;
        }

        /// <summary>Lets the next <see cref="Latch"/> take a fresh window.</summary>
        public void Unlatch() => IsLatched = false;
    }
}
