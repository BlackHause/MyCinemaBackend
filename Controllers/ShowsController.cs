using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using KodiBackend.Data;
using KodiBackend.Models;
using KodiBackend.Services;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System;
using System.Text.RegularExpressions;
using System.Globalization;
using System.Text;

namespace KodiBackend.Controllers
{
    public class ShowCreateRequest
    {
        public string? Title { get; set; }
    }
    
    // TŘÍDA BulkAddRequest BYLA ODSTRANĚNA (Váš původní, správný stav pro kompilaci)
    // public class BulkAddRequest 
    // {
    //     public int Count { get; set; }
    // }

    [ApiController]
    [Route("api/[controller]")]
    public class ShowsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly TMDbService _tmdbService;
        private readonly IWebshareService _webshareService;
        private readonly CSFDService _csfdService;

        private const int MaxEpisodesForProcessing = 1000; 
        private static readonly Regex JapaneseRegex = new Regex(@"[\p{IsHiragana}\p{IsKatakana}\p{IsCJKUnifiedIdeographs}]", RegexOptions.Compiled); 

        public ShowsController(ApplicationDbContext context, TMDbService tmdbService, IWebshareService webshareService, CSFDService csfdService)
        {
            _context = context;
            _tmdbService = tmdbService;
            _webshareService = webshareService;
            _csfdService = csfdService;
        }

        private static string NormalizeTitleForComparison(string? title)
        {
            if (string.IsNullOrWhiteSpace(title)) return string.Empty;
            
            string normalized = RemoveDiacritics(title).ToLowerInvariant();
            normalized = Regex.Replace(normalized, @"\s*[:\-\—\/].+$", "");
            normalized = Regex.Replace(normalized, @"\s*\d{4}\s*$", "");
            normalized = Regex.Replace(normalized, @"\s+z\s*$", "");
            normalized = Regex.Replace(normalized, @"\s+ii+\s*$", "");
            normalized = Regex.Replace(normalized, @"\s*\d+$", "");
            
            return new string(normalized
                .Where(c => !char.IsPunctuation(c) && !char.IsWhiteSpace(c))
                .ToArray());
        }

        private static string RemoveDiacritics(string text)
        {
            var normalizedString = text.Normalize(NormalizationForm.FormD);
            var stringBuilder = new StringBuilder();

            foreach (var c in normalizedString)
            {
                var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
                if (unicodeCategory != UnicodeCategory.NonSpacingMark)
                {
                    stringBuilder.Append(c);
                }
            }
            return stringBuilder.ToString().Normalize(NormalizationForm.FormC);
        }

        private async Task<(List<string> added, List<string> skipped, List<string> failed)> AddShowsFromTitlesAsync(List<string> allTitles, int targetCount)
        {
            var addedTitles = new List<string>();
            var skippedTitles = new List<string>();
            var failedTitles = new List<string>();
            
            var existingTitles = new HashSet<string>(await _context.Shows
                .Where(s => s.Title != null)
                .Select(s => NormalizeTitleForComparison(s.Title!))
                .ToListAsync());
            
            var blacklistedTitlesNormalized = new HashSet<string>(await _context.HistoryEntries
                .Where(h => h.MediaType == "Show" && h.Reason != null && h.Title != null)
                .Select(h => NormalizeTitleForComparison(h.Title!))
                .ToListAsync());
            
            var showsToAdd = new List<Show>();

            // KLÍČOVÁ OPRAVA 1: Načteme existující FileIdent do paměti, abychom se vyhnuli DB chybě.
            var existingFileIdents = new HashSet<string>(await _context.WebshareLinks
                .Where(l => l.FileIdent != null)
                .Select(l => l.FileIdent!)
                .ToListAsync());

            foreach (var title in allTitles)
            {
                if (addedTitles.Count >= targetCount) break;

                if (string.IsNullOrWhiteSpace(title)) continue;
                string normalizedTitle = NormalizeTitleForComparison(title);

                if (existingTitles.Contains(normalizedTitle))
                {
                    skippedTitles.Add(title);
                    continue; 
                }

                if (blacklistedTitlesNormalized.Contains(normalizedTitle))
                {
                    failedTitles.Add(title);
                    continue; 
                }

                var showFromTMDb = await _tmdbService.CreateShowFromTMDb(title);
                if (showFromTMDb == null || showFromTMDb.Title == null || showFromTMDb.TMDbId == null)
                {
                    continue;
                }
                
                if (await _context.Shows.AnyAsync(s => s.TMDbId == showFromTMDb.TMDbId))
                {
                    skippedTitles.Add(showFromTMDb.Title);
                    continue;
                }

                string? skipReason = null;
                int totalEpisodes = showFromTMDb.Seasons.Sum(s => s.Episodes.Count);
                string genresLower = showFromTMDb.Genres?.ToLowerInvariant() ?? "";
                
                if (genresLower.Contains("animace") || genresLower.Contains("anime") || 
                    JapaneseRegex.IsMatch(showFromTMDb.Title))
                {
                    skipReason = "Ignorováno: Anime/Animace nebo japonské znaky v titulu.";
                }
                else if (totalEpisodes > MaxEpisodesForProcessing)
                {
                    skipReason = $"Překročen limit {MaxEpisodesForProcessing} epizod ({totalEpisodes} nalezeno).";
                }

                bool foundAnyLink = false;
                if (skipReason == null)
                {
                    foreach (var season in showFromTMDb.Seasons.OrderBy(s => s.SeasonNumber)) 
                    {
                        foreach (var episode in season.Episodes)
                        {
                            var webshareLinks = await _webshareService.FindLinksAsync(
                                showFromTMDb.Title, 
                                null, 
                                season.SeasonNumber, 
                                episode.EpisodeNumber);
                            
                            if (webshareLinks.Any())
                            {
                                foundAnyLink = true;
                                foreach (var linkDto in webshareLinks.Take(4))
                                {
                                    // *** KLÍČOVÁ OPRAVA 2: Ověření duplicity odkazu před přidáním. ***
                                    if (!existingFileIdents.Contains(linkDto.Ident))
                                    {
                                        episode.Links.Add(new WebshareLink 
                                        { 
                                            FileIdent = linkDto.Ident, 
                                            Quality = $"{linkDto.SizeGb:F2} GB" 
                                        });
                                        // Přidáme ident do HashSetu pro kontrolu v rámci stejného běhu
                                        existingFileIdents.Add(linkDto.Ident); 
                                    }
                                }
                            }
                        }
                    }
                    
                    if (!foundAnyLink) { skipReason = "Nenalezen žádný Webshare odkaz."; }
                }

                if (skipReason != null)
                {
                    _context.HistoryEntries.Add(new HistoryEntry 
                    { 
                        Title = title, 
                        MediaType = "Show", 
                        Reason = skipReason,
                        Timestamp = DateTime.UtcNow
                    });
                    failedTitles.Add(title);
                }
                else
                {
                    showsToAdd.Add(showFromTMDb);
                    addedTitles.Add(showFromTMDb.Title); 
                    existingTitles.Add(NormalizeTitleForComparison(showFromTMDb.Title)); 
                }
            }

            _context.Shows.AddRange(showsToAdd);
            await _context.SaveChangesAsync();
            
            return (addedTitles, skippedTitles, failedTitles);
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Show>>> GetShows()
        {
            return await _context.Shows.ToListAsync();
        }
        
        [HttpPost("top-rated")]
        public async Task<IActionResult> PostTopRatedShows([FromBody] BulkAddRequest request)
        {
            if (request.Count <= 0) return BadRequest("Počet musí být větší než 0.");
            
            var titles = await _tmdbService.GetTopRatedShowsAsync(request.Count + 500); 
            var (addedTitles, skippedTitles, failedTitles) = await AddShowsFromTitlesAsync(titles, request.Count);

            return Ok(new { 
                Message = $"Úspěšně přidáno {addedTitles.Count} nových Top Rated seriálů. Přeskočeno {skippedTitles.Count} existujících. Ignorováno {failedTitles.Count} seriálů.", 
                AddedTitles = addedTitles, 
                SkippedTitles = skippedTitles,
                FailedTitles = failedTitles
            });
        }
        
        [HttpPost("top-czsk")]
        public async Task<IActionResult> PostTopCzSkShows([FromBody] BulkAddRequest request)
        {
            if (request.Count <= 0) return BadRequest("Počet musí být větší než 0.");
            
            var csfdTitles = await _csfdService.GetTopShowTitlesFromCsfdAsync(); 
            var (addedTitles, skippedTitles, failedTitles) = await AddShowsFromTitlesAsync(csfdTitles, request.Count);

            return Ok(new { 
                Message = $"Úspěšně přidáno {addedTitles.Count} CZ/SK seriálů z ČSFD. Přeskočeno {skippedTitles.Count} existujících. Ignorováno {failedTitles.Count} seriálů.", 
                AddedTitles = addedTitles, 
                SkippedTitles = skippedTitles,
                FailedTitles = failedTitles
            });
        }
        
        [HttpGet("{id}")]
        public async Task<ActionResult<Show>> GetShow(int id)
        {
            var show = await _context.Shows
                .Include(s => s.Seasons)
                    .ThenInclude(season => season.Episodes)
                        .ThenInclude(episode => episode.Links)
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
            
            var showFromTMDb = await _tmdbService.CreateShowFromTMDb(request.Title);
            
            if (showFromTMDb == null)
            {
                return NotFound($"Seriál s názvem '{request.Title}' nebyl na TMDb nalezen.");
            }
            
            var existingShow = await _context.Shows.FirstOrDefaultAsync(s => s.TMDbId == showFromTMDb.TMDbId);

            if (existingShow != null)
            {
                 return Conflict($"Seriál s názvem '{showFromTMDb.Title}' (TMDb ID: {showFromTMDb.TMDbId}) již v databázi existuje.");
            }

            if (await _context.Shows.AnyAsync(s => NormalizeTitleForComparison(s.Title!) == NormalizeTitleForComparison(showFromTMDb.Title!)))
            {
                 return Conflict($"Seriál s názvem '{showFromTMDb.Title}' již v databázi existuje (konzistentní shoda).");
            }

            if (await _context.HistoryEntries.AnyAsync(h => h.Title == request.Title && h.MediaType == "Show"))
            {
                return Conflict($"Seriál '{request.Title}' je na blacklistu a byl dříve označen jako nepropojitelný/příliš dlouhý.");
            }

            int totalEpisodes = showFromTMDb.Seasons.Sum(s => s.Episodes.Count);
            if (totalEpisodes > MaxEpisodesForProcessing)
            {
                 _context.HistoryEntries.Add(new HistoryEntry 
                    { 
                        Title = request.Title, 
                        MediaType = "Show", 
                        Reason = $"Příliš dlouhý ({totalEpisodes} epizod).",
                        Timestamp = DateTime.UtcNow
                    });
                await _context.SaveChangesAsync();
                return BadRequest($"Seriál '{showFromTMDb.Title}' nelze přidat, protože má {totalEpisodes} epizod a překračuje maximální povolený limit {MaxEpisodesForProcessing}. Byl přidán na blacklist.");
            }
            
            if ((showFromTMDb.Genres != null && showFromTMDb.Genres.Contains("Animace")) || JapaneseRegex.IsMatch(showFromTMDb.Title))
            {
                 _context.HistoryEntries.Add(new HistoryEntry 
                    { 
                        Title = request.Title, 
                        MediaType = "Show", 
                        Reason = "Ignorováno: Anime/Animace nebo japonské znaky v titulu (při ručním přidání).",
                        Timestamp = DateTime.UtcNow
                    });
                await _context.SaveChangesAsync();
                return BadRequest($"Seriál '{showFromTMDb.Title}' nelze přidat, protože je označen jako Anime/Animace. Byl přidán na blacklist.");
            }

            bool foundAnyLink = false;
            
            foreach (var season in showFromTMDb.Seasons.OrderBy(s => s.SeasonNumber))
            {
                foreach (var episode in season.Episodes)
                {
                    var webshareLinks = await _webshareService.FindLinksAsync(
                        showFromTMDb.Title, 
                        null, 
                        season.SeasonNumber, 
                        episode.EpisodeNumber);
                    
                    if (webshareLinks.Any())
                    {
                        foundAnyLink = true;
                        foreach (var linkDto in webshareLinks.Take(4))
                        {
                            episode.Links.Add(new WebshareLink 
                            { 
                                FileIdent = linkDto.Ident, 
                                Quality = $"{linkDto.SizeGb:F2} GB" 
                            });
                        }
                    }
                }
            }

            if (!foundAnyLink)
            {
                 _context.HistoryEntries.Add(new HistoryEntry 
                    { 
                        Title = request.Title, 
                        MediaType = "Show", 
                        Reason = "Nenalezen žádný Webshare odkaz.",
                        Timestamp = DateTime.UtcNow
                    });
                await _context.SaveChangesAsync();
                return BadRequest($"Seriál '{showFromTMDb.Title}' nelze přidat, protože se nenašel žádný Webshare odkaz. Byl přidán na blacklist.");
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