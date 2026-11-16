using System;
using System.Collections.Generic;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>The shipped roles, looked up by id</b> (crew slice R2). Built once from a set of
    /// <see cref="StaffRoleDef"/>s (or plain id → tasks pairs in a test); after that it is immutable.
    /// A null or blank id never resolves.
    /// </summary>
    public sealed class StaffRoleBook
    {
        /// <summary>Where the role assets live under a <c>Resources</c> folder.</summary>
        public const string ResourcesFolder = "Staff";

        /// <summary>The one task id the voyage calculator reads: somebody aboard must be able to steer.
        /// A contract key like <c>BoatInteriorAnchor.ActionSleep</c>, not content.</summary>
        public const string HelmTask = "task.helm";

        private readonly Dictionary<string, HashSet<string>> _tasks =
            new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        private readonly Dictionary<string, HashSet<string>> _work =
            new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        private readonly List<string> _ids = new List<string>();

        public StaffRoleBook(IEnumerable<StaffRoleDef> roles)
        {
            if (roles == null) return;
            foreach (var role in roles)
            {
                if (role == null) continue;
                Add(role.Id, role.TaskIds, role.WorkIds);
            }
            _ids.Sort(StringComparer.Ordinal);
        }

        /// <summary>A book from plain data, for tests and tools.</summary>
        public static StaffRoleBook FromPairs(IEnumerable<(string Id, string[] Tasks, string[] Work)> roles)
        {
            var book = new StaffRoleBook(null);
            if (roles != null)
                foreach (var (id, tasks, work) in roles) book.Add(id, tasks, work);
            book._ids.Sort(StringComparer.Ordinal);
            return book;
        }

        /// <summary>The shipped roles under <c>Resources/Staff</c>.</summary>
        public static StaffRoleBook FromResources() => new StaffRoleBook(Resources.LoadAll<StaffRoleDef>(ResourcesFolder));

        public static readonly StaffRoleBook Empty = new StaffRoleBook(null);

        /// <summary>Every role id, sorted ordinally.</summary>
        public IReadOnlyList<string> Ids => _ids;

        public bool Contains(string roleId) => !string.IsNullOrWhiteSpace(roleId) && _tasks.ContainsKey(roleId);

        public bool HasTask(string roleId, string taskId)
            => Contains(roleId) && !string.IsNullOrEmpty(taskId) && _tasks[roleId].Contains(taskId);

        public bool IsSpecialistFor(string roleId, string workId)
            => Contains(roleId) && !string.IsNullOrEmpty(workId) && _work[roleId].Contains(workId);

        /// <summary>True when at least one role names this work.</summary>
        public bool AnyRoleWorks(string workId)
        {
            if (string.IsNullOrEmpty(workId)) return false;
            foreach (var set in _work.Values)
                if (set.Contains(workId)) return true;
            return false;
        }

        private void Add(string id, string[] tasks, string[] work)
        {
            // A duplicate id is a content error the validation test reports; the first one wins here so the
            // book never throws at runtime over it.
            if (string.IsNullOrWhiteSpace(id) || _tasks.ContainsKey(id)) return;
            _tasks[id] = ToSet(tasks);
            _work[id] = ToSet(work);
            _ids.Add(id);
        }

        private static HashSet<string> ToSet(string[] values)
        {
            var set = new HashSet<string>(StringComparer.Ordinal);
            if (values != null)
                foreach (string v in values)
                    if (!string.IsNullOrWhiteSpace(v)) set.Add(v);
            return set;
        }
    }
}
