using System;
using System.Linq;
using HiddenHarbours.Boats;
using HiddenHarbours.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// Puts a land region's wrecks in its scene, from their Defs: one object per <see cref="WreckDef"/>
    /// whose region is this one, lying where the Def says, solid, with a place to stand on either
    /// side and look at her. It creates only what is missing and writes only what differs, so a
    /// second run changes nothing.
    /// </summary>
    public static class RicePointWrecks
    {
        public const string DataFolder = "Assets/_Project/Data/Wrecks";
        public const string NpcFolder = "Assets/_Project/Data/NPCs";
        public const string GroupName = "Wrecks";
        public const string LookPortName = "LookPort", LookStarboardName = "LookStarboard";
        public const float LookRadiusMetres = CumberlandRoutines.TalkRadiusMetres;
        const string RootName = "LandRegion", LogicName = "Logic";

        public static WreckDef[] For(string regionId) => AssetDatabase.FindAssets("t:WreckDef", new[] { DataFolder })
            .Select(g => AssetDatabase.LoadAssetAtPath<WreckDef>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(w => w != null && w.RegionId == regionId).OrderBy(w => w.Id, StringComparer.Ordinal).ToArray();

        public static NpcDef LookNpc(WreckDef wreck) => AssetDatabase.FindAssets("t:NpcDef", new[] { NpcFolder })
            .Select(g => AssetDatabase.LoadAssetAtPath<NpcDef>(AssetDatabase.GUIDToAssetPath(g)))
            .SingleOrDefault(n => n != null && n.Id == wreck.LookNpcId);

        /// <summary>Where a person stands to look at her: one point off each side, world metres.</summary>
        public static Vector2[] LookPoints(WreckDef wreck)
        {
            Vector2 along = wreck.Along, across = new Vector2(along.y, -along.x);
            float off = BeachedWreck.SolidSize(wreck).x * 0.5f + wreck.LookStandOffMeters;
            return new[] { wreck.Position - across * off, wreck.Position + across * off };
        }

        public static void Ensure(Scene scene, LandScenePlanDef plan)
        {
            var wrecks = For(plan.Region.Id);
            if (wrecks.Length == 0) return;
            var root = scene.GetRootGameObjects().Single(g => g.name == RootName);
            var logic = root.transform.Find(LogicName);
            var group = logic.Find(GroupName);
            if (group == null)
            {
                group = new GameObject(GroupName).transform;
                group.SetParent(logic, false);
            }
            foreach (var wreck in wrecks)
            {
                if (!wreck.IsPresentable) throw new InvalidOperationException("Wreck has no hull with a picture: " + wreck.Id);
                var npc = LookNpc(wreck);
                if (npc == null || npc.Kind != InteractKind.Read || !npc.HasDialogue)
                    throw new InvalidOperationException("Wreck needs a thing-to-read with lines: " + wreck.Id);
                var placed = group.Find(wreck.Id);
                if (placed == null)
                {
                    placed = new GameObject(wreck.Id).transform;
                    placed.SetParent(group, false);
                }
                var pose = new Vector3(wreck.Position.x, wreck.Position.y, 0f);
                var turn = Quaternion.Euler(0f, 0f, -wreck.HeadingDegrees);
                if (placed.position != pose || placed.rotation != turn) placed.SetPositionAndRotation(pose, turn);

                var drawn = placed.GetComponent<BeachedWreck>();
                if (drawn == null) drawn = placed.gameObject.AddComponent<BeachedWreck>();
                if (drawn.Wreck != wreck) { drawn.Configure(wreck); EditorUtility.SetDirty(drawn); }

                var solid = placed.GetComponent<BoxCollider2D>();
                if (solid == null) solid = placed.gameObject.AddComponent<BoxCollider2D>();
                Vector2 size = BeachedWreck.SolidSize(wreck);
                if (solid.size != size || solid.offset != Vector2.zero) { solid.size = size; solid.offset = Vector2.zero; EditorUtility.SetDirty(solid); }

                var points = LookPoints(wreck);
                Look(placed, LookPortName, points[0], npc);
                Look(placed, LookStarboardName, points[1], npc);
            }
            var presenter = root.GetComponentInChildren<DialoguePresenter>();
            if (presenter == null) presenter = root.AddComponent<DialoguePresenter>();
            var interactor = root.GetComponent<WorldInteractor>();
            if (interactor == null) interactor = root.AddComponent<WorldInteractor>();
            interactor.Configure(null, presenter, root.GetComponentsInChildren<Interactable>(true), LookRadiusMetres);
            EditorUtility.SetDirty(interactor);
        }

        static void Look(Transform wreck, string name, Vector2 at, NpcDef npc)
        {
            var point = wreck.Find(name);
            if (point == null)
            {
                point = new GameObject(name).transform;
                point.SetParent(wreck, false);
            }
            var where = new Vector3(at.x, at.y, 0f);
            if (point.position != where) point.position = where;
            var look = point.GetComponent<Interactable>();
            if (look == null) look = point.gameObject.AddComponent<Interactable>();
            if (look.Npc != npc) { look.Configure(npc); EditorUtility.SetDirty(look); }
        }
    }
}
