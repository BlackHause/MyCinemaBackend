// [Controllers/DatabaseController.cs] - UPRAVENÝ OBSAH

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using KodiBackend.Data;
using KodiBackend.Models;
using KodiBackend.Services;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text.Json;
using System.Linq; 
using System;
using Microsoft.AspNetCore.Http;

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
        private readonly IWebshareService _webshareService;

        // UPRAVENÝ KONSTRUKTOR
        public DatabaseController(ApplicationDbContext context, TMDbService tmdbService, IWebshareService webshareService)
        {
            _context = context;
            _tmdbService = tmdbService;
            _webshareService = webshareService;
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

        // --- NOVÝ ENDPOINT: POST /api/Database/refresh-links ---
        [HttpPost("refresh-links")]
        public async Task<IActionResult> RefreshLinks()
        {
            // Maximální stáří kontroly je nastaveno na 3 měsíce (90 dní)
            var staleThreshold = DateTime.UtcNow.AddDays(-90); 
            
            // FILTR: Hledá filmy, které splňují VŠECHNY tři podmínky:
            // 1. Nemají ŽÁDNÝ ručně ověřený odkaz (OCHRANA PŘED PŘEPSÁNÍM)
            // A SOUČASNĚ splňují alespoň jednu z podmínek automatické kontroly:
            //    A. Nemají žádný odkaz (Links.Count == 0) NEBO
            //    B. Nikdy nebyly kontrolovány (LastLinkCheck == null) NEBO
            //    C. Byly naposledy kontrolovány před více než 90 dny
            var moviesToUpdate = await _context.Movies
                .Include(m => m.Links)
                // Krok 1: Vyloučíme filmy s ručně ověřenými odkazy.
                .Where(m => !m.Links.Any(l => l.IsManuallyVerified)) 
                // Krok 2: Aplikujeme automatický filtr na zbytek filmů.
                .Where(m => m.Links.Count == 0 || m.LastLinkCheck == null || m.LastLinkCheck.Value < staleThreshold)
                .ToListAsync();

            if (!moviesToUpdate.Any())
            {
                return Ok(new { Message = "Databáze je aktuální. Žádné filmy nevyžadují aktualizaci odkazů." });
            }

            var updatedCount = 0;
            var failedCount = 0;
            var updatedTitles = new List<string>();
            var failedTitles = new List<string>();

            foreach (var movie in moviesToUpdate)
            {
                try
                {
                    // Odstranění starých odkazů a příprava na novou kontrolu
                    _context.WebshareLinks.RemoveRange(movie.Links);
                    movie.Links.Clear();

                    var webshareLinks = await _webshareService.FindLinksAsync(movie.Title!, movie.ReleaseYear, null, null);

                    if (webshareLinks.Any())
                    {
                        foreach (var linkDto in webshareLinks.Take(4))
                        {
                            // Automatické přidání: IsManuallyVerified je false (default)
                            movie.Links.Add(new WebshareLink { FileIdent = linkDto.Ident, Quality = $"{linkDto.SizeGb:F2} GB" });
                        }
                        updatedCount++;
                        updatedTitles.Add(movie.Title!);
                    }
                    else
                    {
                        failedCount++;
                        failedTitles.Add(movie.Title!);
                    }

                    // VŽDY aktualizujeme čas kontroly, aby se film nezkoušel znovu
                    movie.LastLinkCheck = DateTime.UtcNow;

                    // Ukládání po 10 filmech pro zamezení velkých transakcí
                    if ((updatedCount + failedCount) % 10 == 0) 
                    {
                        await _context.SaveChangesAsync();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Chyba při aktualizaci odkazů pro '{movie.Title}': {ex.Message}");
                    movie.LastLinkCheck = DateTime.UtcNow; 
                    failedCount++;
                    failedTitles.Add(movie.Title!);
                }
            }

            await _context.SaveChangesAsync();

            return Ok(new
            {
                Message = $"Aktualizace dokončena. Úspěšně nalezeno/aktualizováno {updatedCount} odkazů. Nelze nalézt pro {failedCount} filmů.",
                UpdatedTitles = updatedTitles,
                FailedTitles = failedTitles
            });
        }
        // --- KONEC NOVÉHO ENDPOINTU ---


        // === Původní akce pro historii sledování ===

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