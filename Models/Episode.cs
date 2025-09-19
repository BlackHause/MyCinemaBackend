namespace KodiBackend.Models
{
    public class Episode
    {
        public int Id { get; set; }
        public int SeasonId { get; set; }
        public Season Season { get; set; } = null!;
        public string? Title { get; set; }
        public int EpisodeNumber { get; set; }
        public string? Overview { get; set; }
        public string? FileIdent { get; set; }
        public int? Runtime { get; set; } // TENTO ŘÁDEK JE NOVÝ
    }
}