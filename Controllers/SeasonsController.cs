using Microsoft.AspNetCore.Mvc;
using KodiBackend.Data;
using KodiBackend.Models;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using System.Linq; // Potřebné pro .Select() atd.

namespace KodiBackend.Controllers
{
    public class SeasonCreateRequest
    {
        public int ShowId { get; set; }
        public int SeasonNumber { get; set; }
        public int ReleaseYear { get; set; }
        public int EpisodeCount { get; set; } // Nová vlastnost
    }

    [ApiController]
    [Route("api/[controller]")]
    public class SeasonsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public SeasonsController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpPost]
        public async Task<ActionResult<Season>> PostSeason(SeasonCreateRequest request)
        {
            var show = await _context.Shows.FindAsync(request.ShowId);
            if (show == null)
            {
                return NotFound("Seriál nebyl nalezen.");
            }

            var newSeason = new Season
            {
                SeasonNumber = request.SeasonNumber,
                ReleaseYear = request.ReleaseYear,
                ShowId = request.ShowId
            };
            
            // --- NOVINKA: Automatické vytvoření prázdných epizod ---
            for (int i = 1; i <= request.EpisodeCount; i++)
            {
                newSeason.Episodes.Add(new Episode
                {
                    EpisodeNumber = i,
                    Title = $"Epizoda {i}", // Prozatímní název
                    Overview = "Popisek bude doplněn později."
                });
            }

            _context.Seasons.Add(newSeason);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetSeason", new { id = newSeason.Id }, newSeason);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteSeason(int id)
        {
            var season = await _context.Seasons.FindAsync(id);
            if (season == null)
            {
                return NotFound();
            }

            _context.Seasons.Remove(season);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        [HttpGet("{id}", Name = "GetSeason")]
        public async Task<ActionResult<Season>> GetSeason(int id)
        {
            var season = await _context.Seasons.FindAsync(id);
            if (season == null)
            {
                return NotFound();
            }
            return season;
        }
    }
}