using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HiddenHarbours.Core;
using NUnit.Framework;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>In-memory production JSON and migration coverage for every persisted system.
    /// Each list has two distinct, populated rows so loss, reordering and defaulting are observable.
    /// No SaveStore, live services or player save files are touched.</summary>
    public class SavedSystemRoundTripTests
    {
        private static FieldInfo[] Fields(Type type) => type
            .GetFields(BindingFlags.Instance | BindingFlags.Public)
            .Where(field => !Attribute.IsDefined(field, typeof(NonSerializedAttribute)))
            .OrderBy(field => field.Name, StringComparer.Ordinal).ToArray();

        public static IEnumerable<TestCaseData> SavedSystems()
        {
            foreach (var field in Fields(typeof(SaveData)))
                yield return new TestCaseData(field.Name).SetName("SavedSystemRoundTrip_" + field.Name);
        }

        [TestCaseSource(nameof(SavedSystems))]
        public void SaveLoad_PreservesEveryValueInSystem(string fieldName)
        {
            var original = (SaveData)Populated(typeof(SaveData), "save", 0);
            original.SchemaVersion = SaveMigration.CurrentVersion;
            original.PlayerRecipe.SchemaVersion = CharacterRecipe.Version;
            string json = SaveSerialization.ToJson(original);

            SaveData loaded = SaveSerialization.FromJson(json);

            Assert.That(loaded, Is.Not.Null);
            var field = typeof(SaveData).GetField(fieldName);
            Compare(field.GetValue(original), field.GetValue(loaded), fieldName);
        }

        [Test]
        public void SaveLoad_ProducesIndependentNestedObjects()
        {
            var original = (SaveData)Populated(typeof(SaveData), "save", 0);
            original.SchemaVersion = SaveMigration.CurrentVersion;
            original.PlayerRecipe.SchemaVersion = CharacterRecipe.Version;
            var loaded = SaveSerialization.FromJson(SaveSerialization.ToJson(original));

            foreach (var field in Fields(typeof(SaveData)))
            {
                if (field.FieldType == typeof(string) || field.FieldType.IsValueType) continue;
                Assert.That(field.GetValue(loaded), Is.Not.SameAs(field.GetValue(original)), field.Name);
            }
            loaded.PlayerRecipe.Hair = "changed.after_load";
            loaded.OwnedBoats.Clear();
            Assert.That(original.PlayerRecipe.Hair, Is.Not.EqualTo(loaded.PlayerRecipe.Hair));
            Assert.That(original.OwnedBoats.Count, Is.EqualTo(2));
        }

        [Test]
        public void SaveSchema_RecursivelyExcludesTideWindAndWeather()
        {
            var visited = new HashSet<Type>();
            Visit(typeof(SaveData), "SaveData", visited);
            var populated = (SaveData)Populated(typeof(SaveData), "save", 0);
            populated.SchemaVersion = SaveMigration.CurrentVersion;
            populated.PlayerRecipe.SchemaVersion = CharacterRecipe.Version;
            string json = SaveSerialization.ToJson(populated);
            foreach (string forbidden in new[] { "tide", "wind", "weather" })
                Assert.That(json.IndexOf(forbidden, StringComparison.OrdinalIgnoreCase), Is.EqualTo(-1), forbidden);
            Assert.That(json, Does.Contain("\"WorldSeed\""));
            Assert.That(json, Does.Contain("\"GameTimeSeconds\""));
        }

        private static void Visit(Type type, string path, HashSet<Type> visited)
        {
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
                type = type.GetGenericArguments()[0];
            foreach (string forbidden in new[] { "tide", "wind", "weather" })
            {
                Assert.That(path.IndexOf(forbidden, StringComparison.OrdinalIgnoreCase), Is.EqualTo(-1), path);
                Assert.That(type.Name.IndexOf(forbidden, StringComparison.OrdinalIgnoreCase), Is.EqualTo(-1), type.Name);
            }
            if (type.IsPrimitive || type.IsEnum || type == typeof(string) || !visited.Add(type)) return;
            foreach (var field in Fields(type)) Visit(field.FieldType, path + "." + field.Name, visited);
        }

        private static object Populated(Type type, string path, int variant)
        {
            if (type == typeof(string)) return "audit." + path.Replace('.', '_') + "_" + variant;
            if (type == typeof(int)) return 1 + variant;
            if (type == typeof(double)) return 12345.625 + variant;
            if (type == typeof(float)) return 11.25f + variant;
            if (type == typeof(bool)) return variant == 0;
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
            {
                var list = (IList)Activator.CreateInstance(type);
                Type element = type.GetGenericArguments()[0];
                list.Add(Populated(element, path, 0));
                list.Add(Populated(element, path, 1));
                return list;
            }
            Assert.That(type.IsSerializable && !type.IsEnum, Is.True,
                "Add an explicit fixture for a new save field type: " + path);
            object value = Activator.CreateInstance(type);
            foreach (var field in Fields(type))
                field.SetValue(value, Populated(field.FieldType, path + "." + field.Name, variant));
            return value;
        }

        private static void Compare(object expected, object actual, string path)
        {
            Assert.That(actual, Is.Not.Null, path);
            Assert.That(actual.GetType(), Is.EqualTo(expected.GetType()), path);
            if (expected is IList list)
            {
                var other = (IList)actual;
                Assert.That(other.Count, Is.EqualTo(list.Count), path);
                for (int i = 0; i < list.Count; i++) Compare(list[i], other[i], path + "[" + i + "]");
            }
            else if (expected is string || expected.GetType().IsPrimitive || expected.GetType().IsEnum)
                Assert.That(actual, Is.EqualTo(expected), path);
            else
                foreach (var field in Fields(expected.GetType()))
                    Compare(field.GetValue(expected), field.GetValue(actual), path + "." + field.Name);
        }
    }
}
