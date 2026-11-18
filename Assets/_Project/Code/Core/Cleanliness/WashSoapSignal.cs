using System.Reflection;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// Reads whether a wash is soaped off <see cref="PlayerWashChanged"/>, for the suds.
    ///
    /// <para><b>A guard, on purpose.</b> The <c>Soaped</c> flag arrives with the soap item, which lands on its
    /// own. Until it does, this answers <c>false</c> (plain water, no suds); once the field exists it is read as
    /// it is. The lookup is made once; each read boxes the event once, and a wash changes only at its two edges,
    /// so nothing here runs per frame. When the soap item is on main this becomes a direct <c>e.Soaped</c>.</para>
    /// </summary>
    public static class WashSoapSignal
    {
        private static readonly FieldInfo SoapedField =
            typeof(PlayerWashChanged).GetField("Soaped", BindingFlags.Public | BindingFlags.Instance);

        /// <summary>True once the wash event carries the soap flag.</summary>
        public static bool IsWired => SoapedField != null && SoapedField.FieldType == typeof(bool);

        /// <summary>Whether this wash uses soap; false when the event cannot say.</summary>
        public static bool IsSoaped(in PlayerWashChanged e)
            => e.Washing && IsWired && (bool)SoapedField.GetValue(e);
    }
}
