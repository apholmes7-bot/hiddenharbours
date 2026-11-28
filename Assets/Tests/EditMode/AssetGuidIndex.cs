using System.Collections.Generic;
using System.IO;

namespace HiddenHarbours.Tests.EditMode
{
    // Shared text index for the tile-reference guard and the source-content budget.
    public static class AssetGuidIndex
    {
        public static Dictionary<string, string> Build(IEnumerable<string> roots)
        {
            var index = new Dictionary<string, string>();
            foreach (string root in roots)
            {
                if (!Directory.Exists(root)) continue;
                foreach (string meta in Directory.GetFiles(root, "*.meta", SearchOption.AllDirectories))
                {
                    if (!meta.EndsWith(".meta", System.StringComparison.Ordinal)) continue;
                    string guid = GuidOfMeta(meta);
                    if (guid != null && !index.ContainsKey(guid))
                        index[guid] = meta.Substring(0, meta.Length - ".meta".Length).Replace('\\', '/');
                }
            }
            return index;
        }

        static string GuidOfMeta(string path)
        {
            using (var reader = new StreamReader(path))
            {
                for (int i = 0; i < 8; i++)
                {
                    string line = reader.ReadLine();
                    if (line == null) break;
                    if (line.StartsWith("guid: ", System.StringComparison.Ordinal))
                        return line.Substring(6).Trim().ToLowerInvariant();
                }
            }
            return null;
        }

    }
}
