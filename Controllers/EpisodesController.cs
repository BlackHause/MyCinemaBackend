using Microsoft.AspNetCore.Mvc;
using KodiBackend.Data;
using System.Threading.Tasks;
using KodiBackend.Models; // Důležité přidat

namespace KodiBackend.Controllers
{
    public class EpisodeUpdateRequest
    {
        public string? FileIdent { get; set; }
    }

    [ApiController]
    [Route("api/[controller]")]
    public class EpisodesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public EpisodesController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateEpisode(int id, [FromBody] EpisodeUpdateRequest request)
        {
            var episode = await _context.Episodes.FindAsync(id);
            if (episode == null)
            {
                return NotFound();
            }

            // TADY BYLA CHYBA: Místo StreamUrl použijeme FileIdent
            episode.FileIdent = request.FileIdent; 
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}