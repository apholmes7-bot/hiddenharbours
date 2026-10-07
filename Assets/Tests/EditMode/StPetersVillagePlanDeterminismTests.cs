using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using HiddenHarbours.World;
using NUnit.Framework;
using static HiddenHarbours.Tests.EditMode.StPetersVillageTestData;

namespace HiddenHarbours.Tests.EditMode
{
    public sealed class StPetersVillagePlanDeterminismTests
    {
        [Test]
        public void G17_TwoDerivationsAreIdenticalToTheBit()
        {
            var d = Load();
            var first = d.Derive(); var second = d.Derive();
            CollectionAssert.AreEqual(Bytes(first), Bytes(second), "V1 guard 17: entire derived village: bit-identical runs");
        }

        [Test]
        public void G18_InputEnumerationOrderDoesNotChangeTheDerivation()
        {
            var d = Load(); var first = d.Derive();
            var second = VillagePlanDerivation.Derive(d.Plan, d.Lots.Reverse().ToArray(), d.Yards.Reverse().ToArray(),
                d.Routes.Reverse().ToArray(), d.Lights.Reverse().ToArray());
            CollectionAssert.AreEqual(Bytes(first), Bytes(second), "V1 guard 18: entire derived village: stable order");
        }

        // Every public result field participates, including exact IEEE float bytes and empty arrays.
        public static byte[] Bytes(object value)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            { Write(writer, value); return stream.ToArray(); }
        }

        private static void Write(BinaryWriter writer, object value)
        {
            writer.Write(value != null);
            if (value == null) return;
            if (value is string text) { writer.Write(text); return; }
            if (value is float number) { writer.Write(number); return; }
            if (value is int count) { writer.Write(count); return; }
            if (value is bool flag) { writer.Write(flag); return; }
            if (value is Array array)
            { writer.Write(array.Length); foreach (var element in array) Write(writer, element); return; }
            foreach (var field in value.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public).OrderBy(f => f.Name, StringComparer.Ordinal))
            { writer.Write(field.Name); Write(writer, field.GetValue(value)); }
        }
    }
}
