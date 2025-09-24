using Microsoft.AspNetCore.Mvc;
using KodiBackend.Data;
using KodiBackend.Models;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text.Json;
using System.Linq; 
using KodiBackend.Services; 

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
        private readonly TMDbService _tmdbService; 

        public DatabaseController(ApplicationDbContext context, TMDbService tmdbService)
        {
            _context = context;
            _tmdbService = tmdbService;
        }

        [HttpGet("export")]
        public async Task<ActionResult<DatabaseBackup>> ExportDatabase()
        {
            var backup = new DatabaseBackup
            {
                // PŘIDÁNO .Include(m => m.Links) pro načtení odkazů k filmům
                Movies = await _context.Movies
                    .Include(m => m.Links) 
                    .AsNoTracking().ToListAsync(),

                // PŘIDÁNO .ThenInclude() pro načtení odkazů k epizodám
                Shows = await _context.Shows
                    .Include(s => s.Seasons).ThenInclude(s => s.Episodes).ThenInclude(e => e.Links)
                    .AsNoTracking().ToListAsync()
            };
            return Ok(backup);
        }

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

            _context.Movies.RemoveRange(_context.Movies);
            _context.Shows.RemoveRange(_context.Shows);
            await _context.SaveChangesAsync();

            if (backup.Movies != null) await _context.Movies.AddRangeAsync(backup.Movies);
            if (backup.Shows != null) await _context.Shows.AddRangeAsync(backup.Shows);
            await _context.SaveChangesAsync();

            return Ok(new { message = $"Import úspěšný. Naimportováno {backup.Movies?.Count ?? 0} filmů a {backup.Shows?.Count ?? 0} seriálů." });
        }
        
        [HttpPost("update-all")]
        public async Task<IActionResult> UpdateAllMetadata()
        {
            var movies = await _context.Movies.ToListAsync();
            int moviesUpdated = 0;
            foreach (var movie in movies)
            {
                if (string.IsNullOrEmpty(movie.Title)) continue;
                
                var tmdbData = await _tmdbService.GetMovieDetailsAsync(movie.Title);
                if (tmdbData != null)
                {
                    movie.Overview = tmdbData.Overview;
                    movie.PosterPath = tmdbData.PosterPath;
                    movie.ReleaseYear = tmdbData.ReleaseYear;
                    movie.VoteAverage = tmdbData.VoteAverage;
                    movie.Runtime = tmdbData.Runtime;
                    movie.Genres = tmdbData.Genres;
                    moviesUpdated++;
                }
            }

            var shows = await _context.Shows
                .Include(s => s.Seasons)
                .ThenInclude(s => s.Episodes)
                .ToListAsync();
            int showsUpdated = 0;
            foreach (var existingShow in shows)
            {
                if (string.IsNullOrEmpty(existingShow.Title)) continue;

                var tmdbData = await _tmdbService.CreateShowFromTMDb(existingShow.Title);
                if (tmdbData != null)
                {
                    existingShow.Overview = tmdbData.Overview;
                    existingShow.PosterPath = tmdbData.PosterPath;
                    existingShow.Genres = tmdbData.Genres;

                    // Chytré porovnání sérií a epizod (už nic nemaže)
                    foreach (var tmdbSeason in tmdbData.Seasons)
                    {
                        var existingSeason = existingShow.Seasons.FirstOrDefault(s => s.SeasonNumber == tmdbSeason.SeasonNumber);
                        if (existingSeason == null)
                        {
                            existingShow.Seasons.Add(tmdbSeason);
                        }
                        else
                        {
                            existingSeason.ReleaseYear = tmdbSeason.ReleaseYear;
                            foreach (var tmdbEpisode in tmdbSeason.Episodes)
                            {
                                var existingEpisode = existingSeason.Episodes.FirstOrDefault(e => e.EpisodeNumber == tmdbEpisode.EpisodeNumber);
                                if (existingEpisode == null)
                                {
                                    existingSeason.Episodes.Add(tmdbEpisode);
                                }
                                else
                                {
                                    existingEpisode.Title = tmdbEpisode.Title;
                                    existingEpisode.Overview = tmdbEpisode.Overview;
                                    existingEpisode.Runtime = tmdbEpisode.Runtime;
                                }
                            }
                        }
                    }
                    showsUpdated++;
                }
            }

            await _context.SaveChangesAsync();
            
            return Ok(new { message = $"Aktualizace dokončena. Upraveno {moviesUpdated} filmů a {showsUpdated} seriálů." });
        }

        // === Nové akce pro historii sledování ===

        [HttpPost("history")]
        public async Task<IActionResult> AddToHistory([FromBody] HistoryEntryDto entryDto)
        {
            if (entryDto == null || string.IsNullOrEmpty(entryDto.MediaType))
            {
                return BadRequest("Neplatná data pro záznam historie.");
            }

            // Kontrola, zda médium existuje
            if (entryDto.MediaType == "Movie")
            {
                if (!await _context.Movies.AnyAsync(m => m.Id == entryDto.MediaId))
                {
                    return NotFound($"Film s ID {entryDto.MediaId} nebyl nalezen.");
                }
            }
            else if (entryDto.MediaType == "Show")
            {
                if (!await _context.Shows.AnyAsync(s => s.Id == entryDto.MediaId))
                {
                    return NotFound($"Seriál s ID {entryDto.MediaId} nebyl nalezen.");
                }
            }
            else
            {
                return BadRequest("Neplatný typ média. Podporovány jsou pouze 'Movie' a 'Show'.");
            }
            
            var entry = new HistoryEntry
            {
                MediaId = entryDto.MediaId,
                MediaType = entryDto.MediaType,
                WatchedAt = DateTime.UtcNow
            };
            
            _context.HistoryEntries.Add(entry);
            await _context.SaveChangesAsync();
            
            return Ok();
        }

        [HttpGet("history/movies")]
        public async Task<ActionResult<IEnumerable<Movie>>> GetWatchedMovies()
        {
            var watchedMoviesIds = await _context.HistoryEntries
                .Where(h => h.MediaType == "Movie")
                .OrderByDescending(h => h.WatchedAt)
                .Select(h => h.MediaId)
                .Take(20) // Omezíme na posledních 20 filmů
                .ToListAsync();

            var movies = await _context.Movies
                .Where(m => watchedMoviesIds.Contains(m.Id))
                .Include(m => m.Links)
                .ToListAsync();

            // Udržení pořadí podle historie
            var orderedMovies = watchedMoviesIds
                .Join(movies, id => id, movie => movie.Id, (id, movie) => movie)
                .ToList();
                
            return Ok(orderedMovies);
        }

        [HttpGet("history/shows")]
        public async Task<ActionResult<IEnumerable<Show>>> GetWatchedShows()
        {
            var watchedShowsIds = await _context.HistoryEntries
                .Where(h => h.MediaType == "Show")
                .OrderByDescending(h => h.WatchedAt)
                .Select(h => h.MediaId)
                .Take(20) // Omezíme na posledních 20 seriálů
                .ToListAsync();

            var shows = await _context.Shows
                .Where(s => watchedShowsIds.Contains(s.Id))
                .Include(s => s.Seasons).ThenInclude(s => s.Episodes).ThenInclude(e => e.Links)
                .ToListAsync();
            
            // Udržení pořadí podle historie
            var orderedShows = watchedShowsIds
                .Join(shows, id => id, show => show.Id, (id, show) => show)
                .ToList();

            return Ok(orderedShows);
        }
    }

    public class HistoryEntryDto
    {
        public int MediaId { get; set; }
        public string? MediaType { get; set; }
    }
}