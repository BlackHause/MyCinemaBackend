using Microsoft.AspNetCore.Mvc;
using KodiBackend.Data;
using KodiBackend.Models;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace KodiBackend.Controllers
{
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
            // SMAZÁNÍ STARÝCH DAT
            _context.Movies.RemoveRange(_context.Movies);
            _context.Shows.RemoveRange(_context.Shows);
            await _context.SaveChangesAsync();

            // NAHRÁNÍ NOVÝCH DAT - chytřejší postup
            if (backup.Movies != null)
            {
                await _context.Movies.AddRangeAsync(backup.Movies);
            }
            if (backup.Shows != null)
            {
                // Musíme odebrat reference, které způsobují chybu
                foreach (var show in backup.Shows)
                {
                    foreach (var season in show.Seasons)
                    {
                        season.Show = null; // Ignoruj referenci na seriál
                        foreach (var episode in season.Episodes)
                        {
                            episode.Season = null; // Ignoruj referenci na sezónu
                        }
                    }
                }
                await _context.Shows.AddRangeAsync(backup.Shows);
            }
            await _context.SaveChangesAsync();

            return Ok(new { message = $"Import úspěšný. Naimportováno {backup.Movies?.Count ?? 0} filmů a {backup.Shows?.Count ?? 0} seriálů." });
        }
    }
}