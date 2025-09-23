using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using KodiBackend.Data;
using KodiBackend.Models;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;

namespace KodiBackend.Controllers
{
    // NOVÁ DTO TŘÍDA
    public class EpisodeUpdateRequest
    {
        public List<LinkDto> Links { get; set; } = new();
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
            var episode = await _context.Episodes.Include(e => e.Links).FirstOrDefaultAsync(e => e.Id == id);
            if (episode == null)
            {
                return NotFound();
            }

            // Nejdřív smažeme všechny staré odkazy
            _context.WebshareLinks.RemoveRange(episode.Links);
            
            // A pak přidáme nové
            foreach (var linkDto in request.Links)
            {
                if (!string.IsNullOrWhiteSpace(linkDto.FileIdent))
                {
                    episode.Links.Add(new WebshareLink { FileIdent = linkDto.FileIdent, Quality = linkDto.Quality });
                }
            }
            
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}