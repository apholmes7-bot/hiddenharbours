using System.Collections.Generic;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>The messes left in the world</b> (body slice 1, owner ask 2026-11-15: "the vomit stays until a
    /// player does a cleanup action"). A saved list of <see cref="MessRecordDto"/>s: a mess is added when
    /// someone is sick and removed ONLY by <see cref="CleanUp"/>. Nothing expires it.
    ///
    /// <para><b>The world object is a later slice.</b> A region spawns one cleanable object per record in
    /// <see cref="CopyIn"/> when it loads and on <see cref="MessLeft"/>, and despawns it on
    /// <see cref="MessCleanedUp"/>; its cleanup interactable calls <see cref="CleanUp"/>. This registry
    /// holds the truth; the object is only its picture.</para>
    /// </summary>
    public interface IMessRegistry
    {
        /// <summary>Every mess, oldest first. Empty with no save.</summary>
        IReadOnlyList<MessRecordDto> All { get; }

        /// <summary>Leave a mess and return its new stable id (<c>mess.N</c>); null with no save.
        /// Publishes <see cref="MessLeft"/>.</summary>
        string Add(string regionId, UnityEngine.Vector2 position, double gameSeconds);

        /// <summary>The mess with <paramref name="messId"/>, if it is still there.</summary>
        bool TryGet(string messId, out MessRecordDto mess);

        /// <summary>Append the messes in <paramref name="regionId"/> to <paramref name="into"/>; returns how many.</summary>
        int CopyIn(string regionId, List<MessRecordDto> into);

        /// <summary>The cleanup action: remove the mess and publish <see cref="MessCleanedUp"/>. False (and
        /// no signal) for an unknown id or no save.</summary>
        bool CleanUp(string messId);
    }

    /// <summary>A mess was left in the world (body slice 1).</summary>
    public readonly struct MessLeft
    {
        public readonly MessRecordDto Mess;
        public MessLeft(MessRecordDto mess) { Mess = mess; }
    }

    /// <summary>A mess was cleaned up and is gone from the save (body slice 1).</summary>
    public readonly struct MessCleanedUp
    {
        public readonly MessRecordDto Mess;
        public MessCleanedUp(MessRecordDto mess) { Mess = mess; }
    }
}
