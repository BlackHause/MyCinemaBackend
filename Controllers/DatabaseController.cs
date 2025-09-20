using Microsoft.AspNetCore.Mvc;
using KodiBackend.Data;
using KodiBackend.Models;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace KodiBackend.Controllers
{
    // Pomocná třída pro zabalení dat
    public class DatabaseBackup
    {
        public List<Movie> Movies { get; set; } = new();
        public List<Show> Shows { get; set; } = new();
    }

    [ApiController]
    [Route("api/[controller]")]
    public class DatabaseController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public DatabaseController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet("export")]
        public async Task<ActionResult<DatabaseBackup>> ExportDatabase()
        {
            var backup = new DatabaseBackup
            {
                Movies = await _context.Movies.AsNoTracking().ToListAsync(),
                Shows = await _context.Shows
                    .Include(s => s.Seasons)
                    .ThenInclude(s => s.Episodes)
                    .AsNoTracking()
                    .ToListAsync()
            };
            return Ok(backup);
        }

        [HttpPost("import")]
        public async Task<IActionResult> ImportDatabase([FromBody] DatabaseBackup backup)
        {
            // SMAZÁNÍ STARÝCH DAT na serveru
            var allEpisodes = await _context.Episodes.ToListAsync();
            if(allEpisodes.Any()) _context.Episodes.RemoveRange(allEpisodes);
            var allSeasons = await _context.Seasons.ToListAsync();
            if(allSeasons.Any()) _context.Seasons.RemoveRange(allSeasons);
            var allShows = await _context.Shows.ToListAsync();
            if(allShows.Any()) _context.Shows.RemoveRange(allShows);
            var allMovies = await _context.Movies.ToListAsync();
            if(allMovies.Any()) _context.Movies.RemoveRange(allMovies);
            await _context.SaveChangesAsync();

            // NAHRÁNÍ NOVÝCH DAT ze souboru
            if (backup.Movies != null) _context.Movies.AddRange(backup.Movies);
            if (backup.Shows != null) _context.Shows.AddRange(backup.Shows);
            await _context.SaveChangesAsync();

            return Ok(new { message = $"Import úspěšný. Naimportováno {backup.Movies?.Count ?? 0} filmů a {backup.Shows?.Count ?? 0} seriálů." });
        }
    }
}