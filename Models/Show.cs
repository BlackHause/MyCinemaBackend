using System.Collections.Generic;

namespace KodiBackend.Models
{
    public class Show
    {
        public int Id { get; set; }
        public string? Title { get; set; }
        public string? Overview { get; set; }
        public string? PosterPath { get; set; }
        public ICollection<Season> Seasons { get; set; } = new List<Season>();
    }
}