namespace HiddenHarbours.Core
{
    /// <summary>Hold the region cover through an asynchronous departure; release when its rig is ready.</summary>
    public readonly struct RegionTravelCover
    {
        public readonly bool IsLoading;
        public RegionTravelCover(bool isLoading) { IsLoading = isLoading; }
    }
}
