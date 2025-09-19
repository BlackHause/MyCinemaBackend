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
        public string? FileIdent { get; set; }
        public int? Runtime { get; set; } // TENTO ŘÁDEK JE NOVÝ
    }
}