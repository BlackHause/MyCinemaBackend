using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using KodiBackend.Data;
using KodiBackend.Models;
using KodiBackend.Services;
using System.Threading.Tasks;

namespace KodiBackend.Controllers
{
    // NOVÉ DTO TŘÍDY
    public class LinkDto
    {
        public string? FileIdent { get; set; }
        public string? Quality { get; set; }
    }

    public class MovieCreateRequest
    {
        public string? Title { get; set; }
        public List<LinkDto> Links { get; set; } = new();
    }

    public class MovieUpdateRequest
    {
        public List<LinkDto> Links { get; set; } = new();
    }

    [ApiController]
    [Route("api/[controller]")]
    public class MoviesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly TMDbService _tmdbService;

        public MoviesController(ApplicationDbContext context, TMDbService tmdbService)
        {
            _context = context;
            _tmdbService = tmdbService;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Movie>>> GetMovies() => await _context.Movies.Include(m => m.Links).ToListAsync();

        [HttpPost]
        public async Task<ActionResult<Movie>> PostMovie(MovieCreateRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Title))
            {
                return BadRequest("Název filmu nesmí být prázdný.");
            }

            var movieFromTMDb = await _tmdbService.GetMovieDetailsAsync(request.Title);
            if (movieFromTMDb == null)
            {
                return NotFound($"Film s názvem '{request.Title}' nebyl na TMDb nalezen.");
            }

            foreach (var linkDto in request.Links)
            {
                if (!string.IsNullOrWhiteSpace(linkDto.FileIdent))
                {
                    movieFromTMDb.Links.Add(new WebshareLink { FileIdent = linkDto.FileIdent, Quality = linkDto.Quality });
                }
            }

            _context.Movies.Add(movieFromTMDb);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(GetMovie), new { id = movieFromTMDb.Id }, movieFromTMDb);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> PutMovie(int id, MovieUpdateRequest request)
        {
            var movieToUpdate = await _context.Movies.Include(m => m.Links).FirstOrDefaultAsync(m => m.Id == id);
            if (movieToUpdate == null) return NotFound("Film s daným ID nebyl nalezen.");

            // Nejdřív smažeme všechny staré odkazy
            _context.WebshareLinks.RemoveRange(movieToUpdate.Links);
            
            // A pak přidáme nové
            foreach (var linkDto in request.Links)
            {
                if (!string.IsNullOrWhiteSpace(linkDto.FileIdent))
                {
                    movieToUpdate.Links.Add(new WebshareLink { FileIdent = linkDto.FileIdent, Quality = linkDto.Quality });
                }
            }

            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Movie>> GetMovie(int id)
        {
            var movie = await _context.Movies.Include(m => m.Links).FirstOrDefaultAsync(m => m.Id == id);
            if (movie == null) return NotFound();
            return movie;
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteMovie(int id)
        {
            var movie = await _context.Movies.FindAsync(id);
            if (movie == null) return NotFound();
            _context.Movies.Remove(movie);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}