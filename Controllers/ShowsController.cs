using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using KodiBackend.Data;
using KodiBackend.Models;
using KodiBackend.Services;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;

namespace KodiBackend.Controllers
{
    public class ShowCreateRequest
    {
        public string? Title { get; set; }
    }

    [ApiController]
    [Route("api/[controller]")]
    public class ShowsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly TMDbService _tmdbService;

        public ShowsController(ApplicationDbContext context, TMDbService tmdbService)
        {
            _context = context;
            _tmdbService = tmdbService;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Show>>> GetShows()
        {
            return await _context.Shows.ToListAsync();
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Show>> GetShow(int id)
        {
            var show = await _context.Shows
                .Include(s => s.Seasons)
                    .ThenInclude(s => s.Episodes)
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == id);

            if (show == null)
            {
                return NotFound();
            }

            return show;
        }

        [HttpPost]
        public async Task<ActionResult<Show>> PostShow(ShowCreateRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Title))
            {
                return BadRequest("Název seriálu nesmí být prázdný.");
            }

            // TADY BYLA CHYBA - POUŽÍVÁME SPRÁVNÝ NÁZEV FUNKCE
            var showFromTMDb = await _tmdbService.CreateShowFromTMDb(request.Title);
            
            if (showFromTMDb == null)
            {
                return NotFound($"Seriál s názvem '{request.Title}' nebyl na TMDb nalezen.");
            }

            _context.Shows.Add(showFromTMDb);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetShow), new { id = showFromTMDb.Id }, showFromTMDb);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteShow(int id)
        {
            var show = await _context.Shows.FindAsync(id);
            if (show == null)
            {
                return NotFound();
            }

            _context.Shows.Remove(show);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}