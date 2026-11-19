namespace ImmichFrame.Core.Interfaces
{
    public interface IServerBehaviorSettings
    {
        public List<string> Webcalendars { get; }
        /// <summary>How many days of events the calendars show, starting today (1 = today only).</summary>
        public int CalendarDaysAhead { get; }
        /// <summary>One of <see cref="SlideshowOrders"/>; anything else is treated as random.</summary>
        public string SlideshowOrder { get; }
        public int RefreshAlbumPeopleInterval { get; }
        public string? WeatherApiKey { get; }
        public string? WeatherLatLong { get; }
        public string? UnitSystem { get; }
        public string? Webhook { get; }
        public string? AuthenticationSecret { get; }
        public string? AdminPassword { get; }
    }

    /// <summary>The ways the slideshow can pick the next photo from an account's library.</summary>
    public static class SlideshowOrders
    {
        /// <summary>A random photo each time; one can come back before others have been shown.</summary>
        public const string Random = "random";
        /// <summary>Every photo once, in a shuffled order, then a new shuffle.</summary>
        public const string Shuffle = "shuffle";
        /// <summary>By date taken, oldest first, then from the start again.</summary>
        public const string OldestFirst = "oldest";
        /// <summary>By date taken, newest first, then from the start again.</summary>
        public const string NewestFirst = "newest";

        public static string Normalize(string? order) => order?.Trim().ToLowerInvariant() switch
        {
            Shuffle => Shuffle,
            OldestFirst => OldestFirst,
            NewestFirst => NewestFirst,
            _ => Random
        };
    }
}
