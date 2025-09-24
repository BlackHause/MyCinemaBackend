using System;

namespace KodiBackend.Models
{
    public class HistoryEntry
    {
        public int Id { get; set; }
        public int MediaId { get; set; }
        public string? MediaType { get; set; } // "Movie" nebo "Show"
        public DateTime WatchedAt { get; set; }
    }
}