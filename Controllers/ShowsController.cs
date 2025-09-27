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

                // *** ZAČÁTEK NOVÉ LOGIKY PRO KONTROLU 1. SÉRIE ***
                var seasonOne = showFromTMDb.Seasons.FirstOrDefault(s => s.SeasonNumber == 1);
                bool seasonOneHasLinks = false;
                
                if (skipReason == null)
                {
                    // 1. Zjistit, jestli existuje 1. série a má nějaké odkazy.
                    if (seasonOne != null)
                    {
                        foreach (var episode in seasonOne.Episodes.OrderBy(e => e.EpisodeNumber))
                        {
                            var webshareLinks = await _webshareService.FindLinksAsync(
                                showFromTMDb.Title, 
                                null, 
                                seasonOne.SeasonNumber, 
                                episode.EpisodeNumber);
                            
                            if (webshareLinks.Any())
                            {
                                seasonOneHasLinks = true;
                                break; // Stačí jeden odkaz v S1, abychom prošli kontrolou.
                            }
                        }

                        if (!seasonOneHasLinks)
                        {
                            skipReason = "Nenalezen žádný Webshare odkaz pro 1. Sérii.";
                        }
                    }
                    // Pozn.: Pokud Season 1 neexistuje, přeskočíme tuto kontrolu a spoléháme na obecné pravidlo níže.
                }

                // 2. Pokud 1. série prošla (nebo neexistuje), prohledáme VŠECHNY série pro přidání odkazů.
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
                    
                    if (!foundAnyLink) { skipReason = "Nenalezen žádný Webshare odkaz (ani v ostatních sériích)."; }
                }
                // *** KONEC NOVÉ LOGIKY PRO KONTROLU 1. SÉRIE ***

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
        
        // Endpoint Top-Rated (PŘEPNUTO NA ČSFD)
        [HttpPost("top-rated")]
        public async Task<IActionResult> PostTopRatedShows([FromBody] BulkAddRequest request)
        {
            if (request.Count <= 0) return BadRequest("Počet musí být větší než 0.");
            
            // 1. ZÍSKÁNÍ TITULŮ Z ČSFD (OBECNÝ TOP)
            var titles = await _csfdService.GetTopShowGeneralTitlesFromCsfdAsync(); 
            
            // 2. FILTROVÁNÍ A PŘIDÁNÍ PŘES TMDB DETAILY
            var (addedTitles, skippedTitles, failedTitles) = await AddShowsFromTitlesAsync(titles, request.Count);

            return Ok(new { 
                Message = $"Úspěšně přidáno {addedTitles.Count} Top Rated seriálů (ČSFD). Přeskočeno {skippedTitles.Count} existujících. Ignorováno {failedTitles.Count} seriálů.", 
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
        
        // *** ZAČÁTEK NOVÉ ČÁSTI ***
        // NOVÝ ENDPOINT: Přidává top dokumentární seriály z ČSFD
        [HttpPost("top-documents")]
        public async Task<IActionResult> PostTopDocumentaryShows([FromBody] BulkAddRequest request)
        {
            if (request.Count <= 0) return BadRequest("Počet musí být větší než 0.");
            
            // Voláme novou metodu v CSFDService
            var csfdTitles = await _csfdService.GetTopDocumentaryShowTitlesFromCsfdAsync(); 
            var (addedTitles, skippedTitles, failedTitles) = await AddShowsFromTitlesAsync(csfdTitles, request.Count);

            return Ok(new { 
                Message = $"Úspěšně přidáno {addedTitles.Count} dokumentárních seriálů z ČSFD. Přeskočeno {skippedTitles.Count} existujících. Ignorováno {failedTitles.Count} seriálů.", 
                AddedTitles = addedTitles, 
                SkippedTitles = skippedTitles,
                FailedTitles = failedTitles
            });
        }
        // *** KONEC NOVÉ ČÁSTI ***
        
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
        public async Task<ActionResult<object>> PostShow(ShowCreateRequest request)
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
            
            // --- Duplicity (Duplicity check MUST REMAIN) ---
            var existingShow = await _context.Shows.FirstOrDefaultAsync(s => s.TMDbId == showFromTMDb.TMDbId);

            if (existingShow != null)
            {
                 return Conflict($"Seriál s názvem '{showFromTMDb.Title}' (TMDb ID: {showFromTMDb.TMDbId}) již v databázi existuje.");
            }

            // Opravená duplicity kontrola s načtením do paměti (client evaluation)
            var normalizedTitleFromRequest = NormalizeTitleForComparison(showFromTMDb.Title!);
            var allExistingNormalizedTitles = new HashSet<string>(await _context.Shows
                .Where(s => s.Title != null)
                .Select(s => s.Title!)
                .ToListAsync()
                .ContinueWith(t => t.Result.Select(NormalizeTitleForComparison)));

            if (allExistingNormalizedTitles.Contains(normalizedTitleFromRequest))
            {
                 return Conflict($"Seriál s názvem '{showFromTMDb.Title}' již v databázi existuje (konzistentní shoda).");
            }
            // --- Konec Duplicity ---
            
            // ZMĚNA: PŘESKOČENÍ VŠECH FILTRŮ A HLEDÁNÍ ODKAZŮ PRO RYCHLÉ MANUÁLNÍ PŘIDÁNÍ
            
            // --- PŮVODNÍ KÓD HLEDÁNÍ ODKAZŮ A OMEZENÍ DÉLKY JE PŘESKOČEN ---
            /*
            int totalEpisodes = showFromTMDb.Seasons.Sum(s => s.Episodes.Count);
            if (totalEpisodes > MaxEpisodesForProcessing) { ... }
            if (await _context.HistoryEntries.AnyAsync(...)) { ... }
            if (Genres.Contains("Animace")) { ... }
            
            bool foundAnyLink = false;
            foreach (var season in showFromTMDb.Seasons.OrderBy(s => s.SeasonNumber)) { ... }
            if (!foundAnyLink) { ... }
            */

            // --- JEDINÝ PŘÍKAZ K PROVEDENÍ ---
            _context.Shows.Add(showFromTMDb);
            await _context.SaveChangesAsync();

            // Návrat s přidaným objektem
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