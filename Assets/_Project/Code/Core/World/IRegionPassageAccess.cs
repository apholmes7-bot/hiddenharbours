namespace HiddenHarbours.Core
{
    /// <summary>Optional travel policy supplied by App; World does not depend on a control implementation.</summary>
    public interface IRegionPassageAccess
    {
        bool CanTravel { get; }
    }
}
