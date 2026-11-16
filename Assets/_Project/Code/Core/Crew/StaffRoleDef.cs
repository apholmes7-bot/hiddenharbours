using System;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>One job aboard a boat, as data</b> (crew slice R2; ADR 0003, rule 2). A role names the tasks
    /// its holder does (<c>task.*</c> ids) and the work it is the specialist for (<c>work.*</c> ids: a
    /// fishery, or cargo). Players and hired hands hold the same roles; a player alone can stand in for
    /// any role by walking between stations — just slowly (P4, earn it then automate it).
    ///
    /// <para>One asset per role under <c>Data/Resources/Staff/</c> (loaded by
    /// <see cref="StaffRoleBook.FromResources"/>). Ids are stable and append-only:
    /// <c>role.snake_case</c>. Wages, skills and routines arrive with hiring (M3-05, M3-06); this is
    /// the identity and the task contract only.</para>
    ///
    /// <para><b>Why Core.</b> The roster and the voyage calculator are Core contracts that every lane
    /// reads (boats, fishing, economy, UI), and none of them may reference another's types (rule 4).</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Staff Role", fileName = "StaffRole")]
    public sealed class StaffRoleDef : ScriptableObject
    {
        [Tooltip("Stable id, append-only (CLAUDE.md §5): role.<snake_case>, e.g. role.sternman.")]
        public string Id = "";

        [Tooltip("What the owner and the player read.")]
        public string DisplayName = "";

        [Tooltip("One plain sentence: what this person does aboard.")]
        [TextArea(2, 4)] public string Summary = "";

        [Tooltip("The tasks this role does, as task.<snake_case> ids. A station or a hand's routine asks " +
                 "'does this role do task X?' through StaffRoleBook.HasTask. task.helm is the one the voyage " +
                 "calculator reads: somebody aboard must hold a role with it.")]
        public string[] TaskIds = Array.Empty<string>();

        [Tooltip("The work (fisheries, cargo) this role is the specialist for, as work.<snake_case> ids. " +
                 "Every work id a BoatVoyageDef names must be named by at least one role here.")]
        public string[] WorkIds = Array.Empty<string>();
    }
}
