using System;
using System.Collections.Generic;
using System.Linq;

namespace HiddenHarbours.Tools.RigBaking
{
    public static class HullLayoutGuard
    {
        public static string[] Check(string hull, string walkSchema, string defSchema,
                                     bool deckSidecar, bool kitRoom, bool sheetRoom)
        {
            var errors = new List<string>();
            bool switched = !string.IsNullOrEmpty(defSchema);
            if (!string.IsNullOrEmpty(walkSchema) && deckSidecar)
                errors.Add(hull + ": both deck and walk sidecars");
            if (switched && kitRoom) errors.Add(hull + ": switched hull remains in MeshInteriorHulls");
            if (switched && sheetRoom) errors.Add(hull + ": switched hull remains in BoatInteriors.json");
            if (switched && defSchema != walkSchema) errors.Add(hull + ": LayoutSchema differs from walk schema");
            return errors.ToArray();
        }
    }
}
