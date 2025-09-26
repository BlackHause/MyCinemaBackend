namespace KodiBackend.Models
{
    public class WebshareLink
    {
        public int Id { get; set; }
        public string? FileIdent { get; set; }
        public string? Quality { get; set; }
        
        // NOVÉ: Vlajka pro ruční přidání/ověření.
        public bool IsManuallyVerified { get; set; } = false; 
        
        // Cizí klíče pro propojení
        public int? MovieId { get; set; }
        public Movie? Movie { get; set; }

        public int? EpisodeId { get; set; }
        public Episode? Episode { get; set; }
    }
}