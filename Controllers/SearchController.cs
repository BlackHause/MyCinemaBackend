using Microsoft.AspNetCore.Mvc;
using KodiBackend.Data;
using KodiBackend.Models;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace KodiBackend.Controllers
{
    // Předpokládám, že tato třída je definovaná jinde ve vašem projektu
    // ZMĚNA: Třída SearchResult by měla mít nyní jinou strukturu
    public class SearchResult
    {
        public int Id { get; set; }
        public string? Title { get; set; }
        public string? Overview { get; set; }
        public string? PosterPath { get; set; }
        // Vlastnost pro indikaci, zda má položka odkazy
        public bool HasLinks { get; set; } 
        public string? Type { get; set; }
    }


    [ApiController]
    [Route("api/[controller]")]
    public class SearchController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public SearchController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<SearchResult>>> Search([FromQuery] string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return new List<SearchResult>();
            }

            var lowerQuery = query.ToLower();

            // Prohledáme filmy
            var movies = await _context.Movies
                .Include(m => m.Links) // TOTO JE DŮLEŽITÉ PŘIDAT PRO ZÍSKÁNÍ ODKAZŮ
                .Where(m => m.Title != null && m.Title.ToLower().Contains(lowerQuery))
                .Select(m => new SearchResult
                {
                    Id = m.Id,
                    Title = m.Title,
                    Overview = m.Overview,
                    PosterPath = m.PosterPath,
                    // PŘIDÁN NOVÝ ŘÁDEK: Kontrola, zda existují nějaké odkazy
                    HasLinks = m.Links.Any(),
                    Type = "Movie"
                })
                .ToListAsync();

            // Prohledáme seriály
            var shows = await _context.Shows
                .Where(s => s.Title != null && s.Title.ToLower().Contains(lowerQuery))
                .Select(s => new SearchResult
                {
                    Id = s.Id,
                    Title = s.Title,
                    Overview = s.Overview,
                    PosterPath = s.PosterPath,
                    // U seriálů se odkazy řeší na úrovni epizod, ne show
                    HasLinks = false, 
                    Type = "Show"
                })
                .ToListAsync();

            var results = movies.Concat(shows).ToList();
            return results;
        }
    }
}