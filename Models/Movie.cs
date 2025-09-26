// [Models/Movie.cs]

using System.Collections.Generic;
using System; 

namespace KodiBackend.Models
{
    public class Movie
    {
        public int Id { get; set; }
        
        // NOVÁ VLASTNOST: TMDb ID pro unikátní identifikaci filmu
        public int? TMDbId { get; set; } 
        
        public string? Title { get; set; }
        public string? Overview { get; set; }
        public int? ReleaseYear { get; set; }
        public double? VoteAverage { get; set; }
        public string? PosterPath { get; set; }
        public int? Runtime { get; set; }
        public string? Genres { get; set; }
        
        // Vlastnost pro sledování poslední kontroly odkazů
        public DateTime? LastLinkCheck { get; set; } = null; 

        // Navigační vlastnost pro odkazy
        public List<WebshareLink> Links { get; set; } = new();
    }
}