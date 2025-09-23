using System.Collections.Generic;

namespace KodiBackend.Models
{
    public class Season
    {
        public int Id { get; set; }
        public int SeasonNumber { get; set; }
        
        // TENTO ŘÁDEK PŘIDEJTE:
        public int? ReleaseYear { get; set; }

        public int ShowId { get; set; } // Odkaz na seriál, ke kterému patří
        public Show? Show { get; set; }

        public ICollection<Episode> Episodes { get; set; } = new List<Episode>();
    }
}