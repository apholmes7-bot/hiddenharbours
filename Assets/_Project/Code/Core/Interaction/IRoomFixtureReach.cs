using UnityEngine;
using System.Collections.Generic;

namespace HiddenHarbours.Core
{
    /// <summary>A fixture's spatial gate, shared by selection and final dispatch.</summary>
    public interface IRoomFixtureReach
    {
        bool CanReach(in InteractActor actor);
    }

    /// <summary>The building supplies its existing floor and wall facts to fixture modules.</summary>
    public interface IRoomReachSource
    {
        bool CanReach(Transform fixture, Vector2 actor, Vector2 target);
        bool CanReachExterior(Vector2 actor, Vector2 target);
    }

    public static class RoomFixtureReach
    {
        static readonly List<IRoomReachSource> Sources = new List<IRoomReachSource>();

        public static void Register(IRoomReachSource source)
        {
            if (source != null && !Sources.Contains(source)) Sources.Add(source);
        }

        public static void Unregister(IRoomReachSource source) => Sources.Remove(source);

        public static bool Allows(Component fixture, in InteractActor actor, Vector2 target)
        {
            if (fixture == null || !RoomReach.Finite(actor.Position) || !RoomReach.Finite(target)) return false;
            for (Transform parent = fixture.transform; parent != null; parent = parent.parent)
                if (parent.TryGetComponent<IRoomReachSource>(out var source))
                    return source.CanReach(fixture.transform, actor.Position, target);
            // An outdoor fixture can still be on the far side of the actor's house wall.
            for (int i = 0; i < Sources.Count; i++)
                if (!Sources[i].CanReachExterior(actor.Position, target)) return false;
            return true;
        }

        public static bool Allows(IInteractable target, in InteractActor actor) =>
            !(target is IRoomFixtureReach room) || room.CanReach(actor);
    }
}
