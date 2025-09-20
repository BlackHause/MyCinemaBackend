using Microsoft.AspNetCore.Mvc;
using KodiBackend.Data;
using KodiBackend.Models;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text.Json; // Přidáno pro práci s JSON

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
                    .Include(s => s.Seasons).ThenInclude(s => s.Episodes)
                    .AsNoTracking().ToListAsync()
            };
            return Ok(backup);
        }

        // TATO FUNKCE JE NOVÁ A LEPŠÍ
        [HttpPost("import")]
        public async Task<IActionResult> ImportDatabase(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest("Nebyl nahrán žádný soubor.");
            }

            DatabaseBackup? backup;
            try
            {
                // Načteme data přímo z nahraného souboru
                using var stream = file.OpenReadStream();
                backup = await JsonSerializer.DeserializeAsync<DatabaseBackup>(stream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch (JsonException ex)
            {
                return BadRequest($"Chyba při čtení souboru JSON: {ex.Message}");
            }

            if (backup == null)
            {
                return BadRequest("Soubor se nepodařilo zpracovat.");
            }

            // Smazání starých dat
            _context.Movies.RemoveRange(_context.Movies);
            _context.Shows.RemoveRange(_context.Shows);
            await _context.SaveChangesAsync();

            // Nahrání nových dat
            if (backup.Movies != null) await _context.Movies.AddRangeAsync(backup.Movies);
            if (backup.Shows != null) await _context.Shows.AddRangeAsync(backup.Shows);
            await _context.SaveChangesAsync();

            return Ok(new { message = $"Import úspěšný. Naimportováno {backup.Movies?.Count ?? 0} filmů a {backup.Shows?.Count ?? 0} seriálů." });
        }
    }
}