using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using KodiBackend.Data;
using KodiBackend.Models;
using KodiBackend.Services;
using System.Threading.Tasks;

namespace KodiBackend.Controllers
{
    public class MovieCreateRequest
    {
        public string? Title { get; set; }
        public string? FileIdent { get; set; } // Změna zde
    }

    public class MovieUpdateRequest
    {
        public string? FileIdent { get; set; } // Změna zde
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
        public async Task<ActionResult<IEnumerable<Movie>>> GetMovies() => await _context.Movies.ToListAsync();

        [HttpPost]
        public async Task<ActionResult<Movie>> PostMovie(MovieCreateRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.FileIdent))
            {
                return BadRequest("Název filmu a FileIdent nesmí být prázdné.");
            }

            var movieFromTMDb = await _tmdbService.GetMovieDetailsAsync(request.Title);
            if (movieFromTMDb == null)
            {
                return NotFound($"Film s názvem '{request.Title}' nebyl na TMDb nalezen.");
            }

            movieFromTMDb.FileIdent = request.FileIdent; // Změna zde
            _context.Movies.Add(movieFromTMDb);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(GetMovie), new { id = movieFromTMDb.Id }, movieFromTMDb);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> PutMovie(int id, MovieUpdateRequest movieUpdate)
        {
            var movieToUpdate = await _context.Movies.FindAsync(id);
            if (movieToUpdate == null) return NotFound("Film s daným ID nebyl nalezen.");

            movieToUpdate.FileIdent = movieUpdate.FileIdent; // Změna zde
            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Movie>> GetMovie(int id)
        {
            var movie = await _context.Movies.FindAsync(id);
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