namespace KodiBackend.Models
{
    public class SearchResult
    {
        public int Id { get; set; }
        public string? Title { get; set; }
        public string? Overview { get; set; }
        public string? PosterPath { get; set; }
        public string? Type { get; set; } // "Movie" nebo "Show"
        public string? FileIdent { get; set; } // TENTO ŘÁDEK CHYBĚL
    }
}