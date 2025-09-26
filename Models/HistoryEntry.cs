using System;

namespace KodiBackend.Models
{
    public class HistoryEntry
    {
        public int Id { get; set; }
        
        // PŮVODNÍ POLE (MUSÍ ZŮSTAT KVŮLI DatabaseController.cs)
        public int? MediaId { get; set; } // Zůstává pro zpětnou kompatibilitu s DatabaseController.cs
        public DateTime? WatchedAt { get; set; } // Zůstává pro zpětnou kompatibilitu
        
        // NOVÁ POLE PRO BLACKLIST A DUPLICITNÍ KONTROLU
        public string? Title { get; set; } 
        public string? MediaType { get; set; } // "Movie" nebo "Show"
        public string? Reason { get; set; } // Důvod selhání (Použijeme pro blacklist)
        public DateTime? Timestamp { get; set; } // Použijeme jako alternativní pole pro čas
    }
}