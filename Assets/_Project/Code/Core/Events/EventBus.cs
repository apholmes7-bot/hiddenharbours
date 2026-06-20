using System;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// A tiny, allocation-free, strongly-typed publish/subscribe bus.
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
        // One delegate slot per event type T, created on first use.
        private static class Channel<T>
        {
            public static Action<T> Handlers;
        }

        public static void Subscribe<T>(Action<T> handler) => Channel<T>.Handlers += handler;

        public static void Unsubscribe<T>(Action<T> handler) => Channel<T>.Handlers -= handler;

        public static void Publish<T>(T message) => Channel<T>.Handlers?.Invoke(message);

        /// <summary>Clear all handlers for a type. Mainly for tests / scene teardown.</summary>
        public static void Clear<T>() => Channel<T>.Handlers = null;
    }
}
