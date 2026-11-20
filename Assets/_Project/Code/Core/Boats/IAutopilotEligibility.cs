namespace HiddenHarbours.Core
{
    /// <summary>Authored hull eligibility, supplied by Boats without exposing its Def type.</summary>
    public interface IAutopilotEligibility
    {
        /// <summary>The hull Def's AutopilotOffered flag. Unknown or ambiguous ids refuse.</summary>
        bool CanTakeAutopilot(string hullId);
    }
}
