using System;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// Synchronous, main-thread-only notifications, not transactions or success responses.
    /// Each failing handler is logged once and delivery continues without rethrowing.
    /// Successful publishes allocate no bus memory after initialization; subscription changes
    /// rebuild a flattened snapshot. Subscriber work and exception logging have their own costs.
    /// Unexpected exception logs must fail tests, including assertions thrown inside handlers.
    /// Cross-module communication goes through here (or Core interfaces) so feature
    /// modules never reference each other's concrete classes. See
    /// docs/architecture/tech-architecture.md §3 and project-structure.md §5.
    ///
    /// Usage:
    ///   EventBus.Subscribe&lt;DayStarted&gt;(OnDayStarted);
    ///   EventBus.Publish(new DayStarted(dayOfSeason, season));
    ///   EventBus.Unsubscribe&lt;DayStarted&gt;(OnDayStarted);   // in OnDisable/teardown
    /// </summary>
    public static class EventBus
    {
        // Keep delegate combine/remove semantics, including last matching multicast subsequences.
        private static class Channel<T>
        {
            public static Action<T> Handlers;
            public static Action<T>[] Snapshot = Array.Empty<Action<T>>();

            public static void Rebuild()
            {
                if (Handlers == null)
                {
                    Snapshot = Array.Empty<Action<T>>();
                    return;
                }

                var leaves = Handlers.GetInvocationList();
                var snapshot = new Action<T>[leaves.Length];
                for (int i = 0; i < leaves.Length; i++)
                    snapshot[i] = (Action<T>)leaves[i];
                Snapshot = snapshot;
            }
        }

        public static void Subscribe<T>(Action<T> handler)
        {
            if (handler == null) return;
            Channel<T>.Handlers += handler;
            Channel<T>.Rebuild();
        }

        public static void Unsubscribe<T>(Action<T> handler)
        {
            if (handler == null) return;
            var previous = Channel<T>.Handlers;
            Channel<T>.Handlers -= handler;
            if (!ReferenceEquals(previous, Channel<T>.Handlers)) Channel<T>.Rebuild();
        }

        public static void Publish<T>(T message)
        {
            // Mutations install a new array: this dispatch finishes its original snapshot,
            // while a reentrant publish immediately sees the newly installed one.
            var snapshot = Channel<T>.Snapshot;
            for (int i = 0; i < snapshot.Length; i++)
            {
                try { snapshot[i](message); }
                catch (Exception exception) { Debug.LogException(exception); }
            }
        }

        /// <summary>
        /// True when at least one handler is listening on <typeparamref name="T"/>'s channel.
        ///
        /// <para><b>For the one case where publishing into silence is a soft-lock, not a no-op.</b> A
        /// publisher that then WAITS for an answer — the dialogue presenter holding a conversation open
        /// until the wares book it asked for is shut — hangs for ever in a scene where nothing subscribes.
        /// Asking first turns that into a graceful fall-through. It is deliberately not a general
        /// pattern: every other publisher here is fire-and-forget, and a system that changes what it
        /// DOES based on who is listening is a coupling in disguise.</para>
        /// </summary>
        public static bool HasSubscribers<T>() => Channel<T>.Handlers != null;

        /// <summary>Clear all handlers for a type. Mainly for tests / scene teardown.</summary>
        public static void Clear<T>()
        {
            Channel<T>.Handlers = null;
            Channel<T>.Snapshot = Array.Empty<Action<T>>();
        }
    }
}
