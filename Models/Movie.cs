namespace KodiBackend.Models
{
    public class Movie
    {
        public int Id { get; set; }
        public string? Title { get; set; }
        public string? Overview { get; set; }
        public int? ReleaseYear { get; set; }
        public double? VoteAverage { get; set; }
        public string? PosterPath { get; set; }
        // POLE JE PŘIDANÉ
        public List<WebshareLink> Links { get; set; } = new();
        // STARÉ POLE ODSTRANĚNO - public string? FileIdent { get; set; }
        public int? Runtime { get; set; }
        public string? Genres { get; set; }
    }
}