// [Controllers/MoviesController.cs]

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
    // DTO Třídy
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

    public class BulkAddRequest
    {
        public int Count { get; set; }
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
        private readonly IWebshareService _webshareService;
        // NOVÁ DEPENDENCE
        private readonly CSFDService _csfdService; 
        
        // POUŽITÍ KOREKTNÍHO UNICODE BLOKU
        private static readonly Regex JapaneseRegex = new Regex(@"[\p{IsHiragana}\p{IsKatakana}\p{IsCJKUnifiedIdeographs}]", RegexOptions.Compiled); 

        // UPRAVENÝ KONSTRUKTOR S CSFDService
        public MoviesController(ApplicationDbContext context, TMDbService tmdbService, IWebshareService webshareService, CSFDService csfdService)
        {
            _context = context;
            _tmdbService = tmdbService;
            _webshareService = webshareService;
            _csfdService = csfdService; // NOVÁ INJEKCE
        }

        // NOVÁ POMOCNÁ METODA PRO EXTRÉMNĚ ROBUSTNÍ NORMALIZACI TITULŮ (STATICKÁ)
        private static string NormalizeTitleForComparison(string? title)
        {
            if (string.IsNullOrWhiteSpace(title)) return string.Empty;
            
            // 1. Odebrání diakritiky a převedení na malá písmena
            string normalized = RemoveDiacritics(title).ToLowerInvariant();
            
            // 2. Agresivní odstranění podtitulů (za : nebo za mezerou s číslem/velkým písmenem)
            normalized = Regex.Replace(normalized, @"\s*[:\-\—\/].+$", ""); // Po dvojtečce, pomlčce, lomítku
            normalized = Regex.Replace(normalized, @"\s*\d{4}\s*$", ""); // Odstranění roku
            
            // 3. ODSTRANĚNÍ BĚŽNÝCH PŘÍPON (čísla, římské číslice, částice jako 'z')
            // Cílem je z 'apokalypsaz' udělat 'apokalypsa'
            normalized = Regex.Replace(normalized, @"\s+z\s*$", "");
            normalized = Regex.Replace(normalized, @"\s+ii+\s*$", "");
            normalized = Regex.Replace(normalized, @"\s*\d+$", ""); // Odstranění číslice na konci
            
            // 4. Odstranění všech mezer a interpunkce
            return new string(normalized
                .Where(c => !char.IsPunctuation(c) && !char.IsWhiteSpace(c))
                .ToArray());
        }

        // Pomocná metoda pro odstranění diakritiky (STATICKÁ)
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
        
        // OPRAVENÁ METODA PRO AUTOMATICKÉ PŘIDÁVÁNÍ (ZABRAŇUJE CHYBĚ UNIQUE CONSTRAINT)
        private async Task<(List<string> added, List<string> skipped, List<string> failed)> AddMoviesFromTitlesAsync(List<string> allTitles, int targetCount)
        {
            var addedTitles = new List<string>();
            var skippedTitles = new List<string>();
            var failedTitles = new List<string>();
            
            // KLÍČOVÝ KROK 1: Načteme existující tituly JEDNOU do paměti a VŽDY je NORMALIZUJEME.
            var existingTitles = new HashSet<string>(await _context.Movies
                .Where(m => m.Title != null)
                .Select(m => NormalizeTitleForComparison(m.Title!))
                .ToListAsync());
            
            // KLÍČOVÝ KROK 2: Načteme tituly na permanentním blacklistu a také je NORMALIZUJEME.
            var blacklistedTitlesNormalized = new HashSet<string>(await _context.HistoryEntries
                .Where(h => h.MediaType == "Movie" && h.Reason != null && h.Title != null)
                .Select(h => NormalizeTitleForComparison(h.Title!)) // NORMALIZACE BLACKLISTU
                .ToListAsync());

            // *** NOVÁ KLÍČOVÁ OPRAVA ***: Načteme existující FileIdent do paměti, abychom se vyhnuli DB chybě.
            var existingFileIdents = new HashSet<string>(await _context.WebshareLinks
                .Where(l => l.FileIdent != null)
                .Select(l => l.FileIdent!)
                .ToListAsync());

            // Vytvoříme seznam pro hromadné uložení
            var moviesToAdd = new List<Movie>();

            foreach (var title in allTitles)
            {
                // TOTO JE ŘÁDEK, KTERÝ RESPEKTUJE VÁŠ JSON VSTUP Count
                if (addedTitles.Count >= targetCount) break;
                
                if (string.IsNullOrWhiteSpace(title)) continue;
                string normalizedTitle = NormalizeTitleForComparison(title);

                // 1. RYCHLÁ KONTROLA DUPLICITY NÁZVU (V PAMĚTI)
                if (existingTitles.Contains(normalizedTitle)) 
                {
                    skippedTitles.Add(title);
                    continue; 
                }

                // 2. RYCHLÁ KONTROLA BLACKLISTU (V PAMĚTI)
                 if (blacklistedTitlesNormalized.Contains(normalizedTitle)) 
                {
                    failedTitles.Add(title) ;
                    continue; 
                }

                // 3. Získání detailů z TMDB (získáváme i TMDbId)
                var movieFromTMDb = await _tmdbService.GetMovieDetailsAsync(title);
                if (movieFromTMDb == null || movieFromTMDb.Title == null || movieFromTMDb.TMDbId == null)
                {
                     continue;
                }
                
                // 4. *** KLÍČOVÁ KONTROLA TMDb ID V DATABÁZI ***
                if (await _context.Movies.AnyAsync(m => m.TMDbId == movieFromTMDb.TMDbId))
                {
                    skippedTitles.Add(movieFromTMDb.Title);
                    continue;
                }
                
                string? skipReason = null;

                // KONTROLA: FILTRACE ANIME/ANIMACE A JAPONSKÝCH ZNAKŮ
                string genresLower = movieFromTMDb.Genres?.ToLowerInvariant() ?? "";
                
                if (genresLower.Contains("animace") || genresLower.Contains("anime") || 
                    JapaneseRegex.IsMatch(movieFromTMDb.Title))
                {
                    skipReason = "Ignorováno: Anime/Animace nebo japonské znaky v titulu.";
                }

                // 5. Hledání odkazů na Webshare (JEN POKUD NEBYL ZATÍM PŘESKOČEN)
                if (skipReason == null)
                {
                    // Použijeme Title a ReleaseYear Z TMDB DETAILU
                    var webshareLinks = await _webshareService.FindLinksAsync(movieFromTMDb.Title, movieFromTMDb.ReleaseYear, null, null);
                    
                    // FILTR: Pokud nejsou odkazy
                    if (!webshareLinks.Any())
                    {
                        skipReason = "Nenalezen žádný Webshare odkaz.";
                    }
                    
                    // 6. Přidání odkazů (jen v případě, že jsou odkazy)
                    if (skipReason == null)
                    {
                        foreach (var linkDto in webshareLinks.Take(4))
                        {
                            // *** KLÍČOVÁ OPRAVA: Kontrola FileIdent před přidáním ***
                            if (!existingFileIdents.Contains(linkDto.Ident))
                            {
                                // Automatické přidání: IsManuallyVerified = false (default)
                                movieFromTMDb.Links.Add(new WebshareLink { FileIdent = linkDto.Ident, Quality = $"{linkDto.SizeGb:F2} GB" });
                                // Přidáme ident do HashSetu pro kontrolu v rámci stejného běhu
                                existingFileIdents.Add(linkDto.Ident); 
                            }
                        }
                        
                        // Přidání k seznamu pro hromadné uložení
                        moviesToAdd.Add(movieFromTMDb);
                        addedTitles.Add(movieFromTMDb.Title);
                        // Aktualizujeme Hashset, aby budoucí filmy ve stejne smyčce nebyly znova hledány
                        existingTitles.Add(NormalizeTitleForComparison(movieFromTMDb.Title)); 
                    }
                }
                
                // 7. BLACKLIST: Pokud se má přeskočit (Anime/žádný odkaz)
                if (skipReason != null)
                {
                     // Uložit do blacklistu pro trvalé ignorování
                    _context.HistoryEntries.Add(new HistoryEntry 
                    { 
                        Title = title, 
                        MediaType = "Movie", 
                        Reason = skipReason,
                        Timestamp = DateTime.UtcNow
                    });
                    
                    failedTitles.Add(title);
                    // await _context.SaveChangesAsync(); // Uložíme HistoryEntries najednou na konci
                    continue; 
                }
            }

            // *** Provedeme hromadné uložení VŠECH NOVÝCH filmů a HistoryEntries ***
            _context.Movies.AddRange(moviesToAdd);
            await _context.SaveChangesAsync();
            
            return (addedTitles, skippedTitles, failedTitles);
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Movie>>> GetMovies() => await _context.Movies.Include(m => m.Links).ToListAsync();

        // UMOŽŇUJE RUČNÍ PŘIDÁNÍ BEZ KONTROLY BLACKLISTU A NASTAVÍ VLAKU
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
            
            // --- NOVÁ KLÍČOVÁ KONTROLA DUPLICITY PŘED ZÁPISEM ---
            // Zabráníme chybě databáze 'UNIQUE constraint failed' kontrolou TMDbId.
            var existingMovieByTMDbId = await _context.Movies
                .FirstOrDefaultAsync(m => m.TMDbId == movieFromTMDb.TMDbId);

            if (existingMovieByTMDbId != null)
            {
                 return Conflict($"Film s názvem '{movieFromTMDb.Title}' (TMDb ID: {movieFromTMDb.TMDbId}) již v databázi existuje.");
            }
            // --- KONEC KLÍČOVÉ KONTROLY ---
            
            // Původní kontrola duplicity s normalizací (ponecháno)
            var existingTitles = new HashSet<string>(await _context.Movies.Where(m => m.Title != null).Select(m => NormalizeTitleForComparison(m.Title!)).ToListAsync());
            string normalizedTitle = NormalizeTitleForComparison(movieFromTMDb.Title);
            
            if (existingTitles.Contains(normalizedTitle))
            {
                 return Conflict($"Film s názvem '{movieFromTMDb.Title}' již v databázi existuje (konzistentní shoda).");
            }
            
            // --- BLACKLIST KONTROLA ZDE JE ODSTRANĚNA (dle Tvého požadavku) ---

            foreach (var linkDto in request.Links)
            {
                if (!string.IsNullOrWhiteSpace(linkDto.FileIdent))
                {
                    // ZMĚNA: Ručně přidané odkazy se označují jako IsManuallyVerified = true
                    movieFromTMDb.Links.Add(new WebshareLink { FileIdent = linkDto.FileIdent, Quality = linkDto.Quality, IsManuallyVerified = true });
                }
            }
            
            // KONTROLA: Pokud se nepřidal žádný odkaz, neukládat a přidat do blacklistu pro automatické přeskočení
            if (!movieFromTMDb.Links.Any())
            {
                 _context.HistoryEntries.Add(new HistoryEntry 
                    { 
                        Title = request.Title, 
                        MediaType = "Movie", 
                        Reason = "Ruční přidání selhalo (žádné odkazy nebyly zadány/nalezeny).",
                        Timestamp = DateTime.UtcNow
                    });
                await _context.SaveChangesAsync();
                return BadRequest($"Film '{movieFromTMDb.Title}' nelze přidat, protože nebyly zadány žádné Webshare odkazy.");
            }

            _context.Movies.Add(movieFromTMDb);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(GetMovie), new { id = movieFromTMDb.Id }, movieFromTMDb);
        }

        // Endpoint Top-Rated (Nyní bere data z ČSFD)
        [HttpPost("top-rated")]
        public async Task<IActionResult> PostTopRatedMovies([FromBody] BulkAddRequest request)
        {
            if (request.Count <= 0) return BadRequest("Počet musí být větší než 0.");
            
            // 1. ZÍSKÁNÍ TITULŮ Z ČSFD (OBECNÝ TOP)
            var titles = await _csfdService.GetTopGeneralTitlesFromCsfdAsync(); 
            
            // 2. FILTROVÁNÍ A PŘIDÁNÍ PŘES TMDB DETAILY
            // Použijeme seznam jako vstup pro AddMoviesFromTitlesAsync
            var (added, skipped, failed) = await AddMoviesFromTitlesAsync(titles, request.Count);

            return Ok(new { 
                Message = $"Úspěšně přidáno {added.Count} Top Rated filmů (ČSFD). Přeskočeno {skipped.Count} existujících. Ignorováno {failed.Count} filmů.", 
                AddedTitles = added, 
                SkippedTitles = skipped,
                FailedTitles = failed
            });
        }

        // Endpoint CZ/SK Top-Rated (PŮVODNÍ FUNKCE)
        [HttpPost("top-czsk")]
        public async Task<IActionResult> PostTopCzSkMovies([FromBody] BulkAddRequest request)
        {
            if (request.Count <= 0) return BadRequest("Počet musí být větší než 0.");
            
            // 1. ZÍSKÁNÍ TITULŮ Z ČSFD SCRAPINGEM (PROKLÁDANÉ CZ/SK)
            var csfdTitles = await _csfdService.GetTopTitlesFromCsfdAsync(); 
            
            // 2. FILTROVÁNÍ A PŘIDÁNÍ PŘES TMDB
            var (addedTitles, skippedTitles, failedTitles) = await AddMoviesFromTitlesAsync(csfdTitles, request.Count);

            return Ok(new { 
                Message = $"Úspěšně přidáno {addedTitles.Count} CZ/SK filmů z ČSFD. Přeskočeno {skippedTitles.Count} existujících. Ignorováno {failedTitles.Count} filmů.", 
                AddedTitles = addedTitles, 
                SkippedTitles = skippedTitles,
                FailedTitles = failedTitles
            });
        }
        
        // NOVÝ ENDPOINT: Přidává top pohádky z ČSFD
        [HttpPost("top-pohadky")]
        public async Task<IActionResult> PostTopFairyTaleMovies([FromBody] BulkAddRequest request)
        {
            if (request.Count <= 0) return BadRequest("Počet musí být větší než 0.");
            
            var csfdTitles = await _csfdService.GetTopFairyTaleTitlesFromCsfdAsync(); 
            var (addedTitles, skippedTitles, failedTitles) = await AddMoviesFromTitlesAsync(csfdTitles, request.Count);

            return Ok(new { 
                Message = $"Úspěšně přidáno {addedTitles.Count} pohádek z ČSFD. Přeskočeno {skippedTitles.Count} existujících. Ignorováno {failedTitles.Count} filmů.", 
                AddedTitles = addedTitles, 
                SkippedTitles = skippedTitles,
                FailedTitles = failedTitles
            });
        }
        
        // NOVÝ ENDPOINT: Přidává top hudební filmy z ČSFD
        [HttpPost("top-hudebni")]
        public async Task<IActionResult> PostTopMusicalMovies([FromBody] BulkAddRequest request)
        {
            if (request.Count <= 0) return BadRequest("Počet musí být větší než 0.");
            
            var csfdTitles = await _csfdService.GetTopMusicalTitlesFromCsfdAsync(); 
            var (addedTitles, skippedTitles, failedTitles) = await AddMoviesFromTitlesAsync(csfdTitles, request.Count);

            return Ok(new { 
                Message = $"Úspěšně přidáno {addedTitles.Count} hudebních filmů z ČSFD. Přeskočeno {skippedTitles.Count} existujících. Ignorováno {failedTitles.Count} filmů.", 
                AddedTitles = addedTitles, 
                SkippedTitles = skippedTitles,
                FailedTitles = failedTitles
            });
        }
        
        // *** ZAČÁTEK NOVÉ ČÁSTI ***
        // NOVÝ ENDPOINT: Přidává top koncerty z ČSFD
        [HttpPost("top-koncerty")]
        public async Task<IActionResult> PostTopConcertMovies([FromBody] BulkAddRequest request)
        {
            if (request.Count <= 0) return BadRequest("Počet musí být větší než 0.");
            
            var csfdTitles = await _csfdService.GetTopConcertTitlesFromCsfdAsync(); 
            var (addedTitles, skippedTitles, failedTitles) = await AddMoviesFromTitlesAsync(csfdTitles, request.Count);

            return Ok(new { 
                Message = $"Úspěšně přidáno {addedTitles.Count} koncertů z ČSFD. Přeskočeno {skippedTitles.Count} existujících. Ignorováno {failedTitles.Count} filmů.", 
                AddedTitles = addedTitles, 
                SkippedTitles = skippedTitles,
                FailedTitles = failedTitles
            });
        }
        // *** KONEC NOVÉ ČÁSTI ***
        
        // Endpoint New-Releases
        [HttpPost("new-releases")]
        public async Task<IActionResult> PostNewMovies([FromBody] BulkAddRequest request)
        {
            if (request.Count <= 0) return BadRequest("Počet musí být větší než 0.");
            
            // ZMĚNA: Navýšena rezerva z 500 na 2000
            var titles = await _tmdbService.GetNewMoviesAsync(request.Count + 2000);
            var (addedTitles, skippedTitles, failedTitles) = await AddMoviesFromTitlesAsync(titles, request.Count);

            return Ok(new { 
                Message = $"Úspěšně přidáno {addedTitles.Count} nových filmů. Přeskočeno {skippedTitles.Count} existujících. Ignorováno {failedTitles.Count} filmů.", 
                AddedTitles = addedTitles, 
                SkippedTitles = skippedTitles,
                FailedTitles = failedTitles
            });
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
                    // PŘIDÁNO: Ručně aktualizované odkazy jsou také ověřené
                    movieToUpdate.Links.Add(new WebshareLink { FileIdent = linkDto.FileIdent, Quality = linkDto.Quality, IsManuallyVerified = true });
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