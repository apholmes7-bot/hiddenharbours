using System;
using HiddenHarbours.Player;

namespace HiddenHarbours.Tests.Support
{
    /// <summary>Finish a transition in fixtures that test the resulting station, not its motion.</summary>
    public static class TimedBoarding
    {
        public static bool Interact(ControlSwitcher switcher)
        {
            bool accepted = switcher.TryInteract();
            Complete(switcher);
            return accepted;
        }

        public static bool BeginInteract(ControlSwitcher switcher)
        {
            bool accepted = switcher.BeginInteract();
            Complete(switcher);
            return accepted;
        }

        public static void Complete(ControlSwitcher switcher)
        {
            const float step = 1f / 120f;
            for (int i = 0; i < 24000 && switcher.IsBoardingMove; i++)
                switcher.TickBoardingMove(step);
            if (switcher.IsBoardingMove)
                throw new InvalidOperationException("The fixture's boarding move did not finish in 200 seconds.");
        }
    }
}
