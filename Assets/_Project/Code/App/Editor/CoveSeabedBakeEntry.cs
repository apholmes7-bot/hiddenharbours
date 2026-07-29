#if UNITY_EDITOR
using UnityEditor;

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// The batch-mode entry for the cove seabed bake — <c>-executeMethod</c> fodder, so the committed
    /// texture is reproducible from a command line and not only from a menu click. A committed texture
    /// nobody can regenerate is a texture nobody can correct.
    ///
    /// <para>It opens NO scene. <see cref="GreyboxBuilder.BakeCoveSeabed"/> builds the cove's terrain
    /// from the builder's own constants in a throwaway object, so there is nothing here that could
    /// dirty a committed scene — and it works from a clean checkout, where the committed
    /// <c>Greybox.unity</c> carries no logic tree at all.</para>
    ///
    /// <code>
    /// Unity.exe -batchmode -quit -projectPath &lt;repo&gt;
    ///   -executeMethod HiddenHarbours.App.Editor.CoveSeabedBakeEntry.Run
    /// </code>
    /// </summary>
    public static class CoveSeabedBakeEntry
    {
        public static void Run()
        {
            GreyboxBuilder.BakeCoveSeabed();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
    }
}
#endif
