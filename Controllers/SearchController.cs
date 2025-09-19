using Microsoft.AspNetCore.Mvc;
using KodiBackend.Data;
using KodiBackend.Models;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace KodiBackend.Controllers
{
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
                .Where(m => m.Title != null && m.Title.ToLower().Contains(lowerQuery))
                .Select(m => new SearchResult
                {
                    Id = m.Id,
                    Title = m.Title,
                    Overview = m.Overview,
                    PosterPath = m.PosterPath,
                    FileIdent = m.FileIdent, // TENTO ŘÁDEK JE NOVÝ A DŮLEŽITÝ
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
                    Type = "Show"
                })
                .ToListAsync();

            var results = movies.Concat(shows).ToList();
            return results;
        }
    }
}