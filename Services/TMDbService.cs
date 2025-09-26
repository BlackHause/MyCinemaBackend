// [Services/TMDbService.cs]

using KodiBackend.Models;
using System;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;

namespace KodiBackend.Services
{
    public class TMDbService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey = "723975edd7760961afc4cf85daa9968a";

        #region Pomocné třídy pro JSON
		private class TMDbMultiSearchResult { [JsonPropertyName("results")] public List<TMDbMultiResult> Results { get; set; } = new(); }
		private class TMDbMultiResult
        {
            [JsonPropertyName("media_type")] public string? MediaType { get; set; }
            [JsonPropertyName("title")] public string? Title { get; set; }
            [JsonPropertyName("name")] public string? Name { get; set; }
            [JsonPropertyName("release_date")] public string? ReleaseDate { get; set; }
            [JsonPropertyName("first_air_date")] public string? FirstAirDate { get; set; }
        }
        
        private class TMDbGenre { [JsonPropertyName("name")] public string Name { get; set; } = ""; }

        private class TMDbMovieSearchResult { 
            [JsonPropertyName("results")] public List<TMDbMovieResult> Results { get; set; } = new();
            [JsonPropertyName("total_pages")] public int TotalPages { get; set; }
        }
        private class TMDbMovieResult
        {
            [JsonPropertyName("id")] public int Id { get; set; }
            [JsonPropertyName("title")] public string? Title { get; set; }
            [JsonPropertyName("overview")] public string? Overview { get; set; }
            [JsonPropertyName("release_date")] public string? ReleaseDate { get; set; }
            [JsonPropertyName("vote_average")] public double VoteAverage { get; set; }
            [JsonPropertyName("poster_path")] public string? PosterPath { get; set; }
            [JsonPropertyName("runtime")] public int? Runtime { get; set; }
            [JsonPropertyName("genres")] public List<TMDbGenre> Genres { get; set; } = new();
        }

        private class TMDbShowSearchResult { 
            [JsonPropertyName("results")] public List<TMDbShowResult> Results { get; set; } = new(); 
            [JsonPropertyName("total_pages")] public int TotalPages { get; set; }
        }
        private class TMDbShowResult
        {
            [JsonPropertyName("id")] public int Id { get; set; }
            [JsonPropertyName("name")] public string? Name { get; set; }
            [JsonPropertyName("overview")] public string? Overview { get; set; }
            [JsonPropertyName("poster_path")] public string? PosterPath { get; set; }
            [JsonPropertyName("genres")] public List<TMDbGenre> Genres { get; set; } = new();
            [JsonPropertyName("seasons")] public List<TMDbSeasonInfo> Seasons { get; set; } = new();
        }
        
        private class TMDbSeasonInfo
        {
            [JsonPropertyName("season_number")] public int SeasonNumber { get; set; }
            [JsonPropertyName("air_date")] public string? AirDate { get; set; }
        }

        private class TMDbSeasonDetailResult
        {
            [JsonPropertyName("air_date")] public string? AirDate { get; set; } // Opakuje se, ale pro jednoduchost necháváme
            [JsonPropertyName("episodes")] public List<TMDbEpisodeResult> Episodes { get; set; } = new();
        }
        
        private class TMDbEpisodeResult
        {
            [JsonPropertyName("name")] public string? Name { get; set; }
            [JsonPropertyName("overview")] public string? Overview { get; set; }
            [JsonPropertyName("episode_number")] public int EpisodeNumber { get; set; }
            [JsonPropertyName("runtime")] public int? Runtime { get; set; }
        }
        #endregion

        public TMDbService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }
        
        // UPRAVENÁ POMOCNÁ METODA PRO ZÍSKÁNÍ SEZNAMŮ Z TMDB
        private async Task<List<string>> GetTitlesFromTmdbListAsync(string endpoint, int count)
        {
            var allTitles = new List<string>();
            int currentPage = 1;
            int totalPages = 1;

            // Opakujeme, dokud nezískáme požadovaný počet a nepřekročíme max 50 stránek
            while (allTitles.Count < count && currentPage <= totalPages && currentPage <= 50) 
            {
                string url = $"https://api.themoviedb.org/3/{endpoint}?api_key={_apiKey}&language=cs-CZ&page={currentPage}";
                
                // DIAGNOSTIKA: ZALOGUJEME VOLANOU URL
                Console.WriteLine($"[TMDb DIAG] Volám URL: {url}");

                var response = await _httpClient.GetAsync(url);
                
                // DIAGNOSTIKA: ZALOGUJEME KÓD ODPOVĚDI
                Console.WriteLine($"[TMDb DIAG] Odpověď Status: {response.StatusCode}");

                if (!response.IsSuccessStatusCode) break;
                
                var content = await response.Content.ReadAsStringAsync();
                
                if (endpoint.Contains("movie") || endpoint.Contains("discover"))
                {
                    var searchResult = JsonSerializer.Deserialize<TMDbMovieSearchResult>(content);
                    
                    if (searchResult == null)
                    {
                         Console.WriteLine("[TMDb DIAG] Chyba deserializace na TMDbMovieSearchResult.");
                         break;
                    }
                    
                    totalPages = searchResult.TotalPages;

                    // DIAGNOSTIKA: ZALOGUJEME POČET VÝSLEDKŮ A VÝPIS PRVNÍHO NÁZVU
                    Console.WriteLine($"[TMDb DIAG] Stránka {currentPage}: Nalezeno {searchResult.Results.Count} výsledků. První: '{searchResult.Results.FirstOrDefault()?.Title ?? "N/A"}'");
                    
                    allTitles.AddRange(searchResult.Results
                        .Where(r => !string.IsNullOrEmpty(r.Title))
                        .Select(r => r.Title!));
                }
                else // TV show
                {
                    var searchResult = JsonSerializer.Deserialize<TMDbShowSearchResult>(content);
                    if (searchResult == null) break;
                    
                    totalPages = searchResult.TotalPages;
                    
                    allTitles.AddRange(searchResult.Results
                        .Where(r => !string.IsNullOrEmpty(r.Name))
                        .Select(r => r.Name!));
                }

                currentPage++;
            }

            // Vrátíme pouze požadovaný počet, aby se v controlleru neprocházelo víc, než je nutné
            return allTitles.Take(count).ToList();
        }

        // METODA 1: Nejlépe hodnocené FILMY
        public async Task<List<string>> GetTopRatedMoviesAsync(int count)
        {
            return await GetTitlesFromTmdbListAsync("movie/top_rated", count);
        }

        // METODA 2: Novinky FILMY
        public async Task<List<string>> GetNewMoviesAsync(int count)
        {
            return await GetTitlesFromTmdbListAsync("movie/now_playing", count);
        }

        // METODA PRO CZ/SK FILMY ZRUŠENA, PŘESUNUTO DO CONTROLLERU S CSFD SCRAPINGEM
        
        // METODA 4: Nejlépe hodnocené SERIÁLY
        public async Task<List<string>> GetTopRatedShowsAsync(int count)
        {
            return await GetTitlesFromTmdbListAsync("tv/top_rated", count);
        }
        
        // ZBYTEK PŮVODNÍCH METOD ZŮSTÁVÁ BEZE ZMĚN
        public async Task<Movie?> GetMovieDetailsAsync(string title)
        {
            var encodedTitle = Uri.EscapeDataString(title);
            var searchResponse = await _httpClient.GetAsync($"https://api.themoviedb.org/3/search/movie?api_key={_apiKey}&query={encodedTitle}&language=cs-CZ");
            if (!searchResponse.IsSuccessStatusCode) return null;

            var searchResult = JsonSerializer.Deserialize<TMDbMovieSearchResult>(await searchResponse.Content.ReadAsStringAsync());
            var firstMovie = searchResult?.Results.FirstOrDefault();
            if (firstMovie == null) return null;

            var detailResponse = await _httpClient.GetAsync($"https://api.themoviedb.org/3/movie/{firstMovie.Id}?api_key={_apiKey}&language=cs-CZ");
            
            // Nyní vracíme null, pokud nelze načíst detaily.
            if (!detailResponse.IsSuccessStatusCode) return null; 

            var movieResult = JsonSerializer.Deserialize<TMDbMovieResult>(await detailResponse.Content.ReadAsStringAsync());
            if (movieResult == null) return null;

            int.TryParse(movieResult.ReleaseDate?.Split('-').FirstOrDefault(), out int year);

            return new Movie
            {
                // ZÁSADNÍ OPRAVA: Ukládáme TMDb ID, které použijeme pro kontrolu duplicit v databázi.
                TMDbId = firstMovie.Id,
                Title = movieResult.Title,
                Overview = movieResult.Overview,
                ReleaseYear = year == 0 ? null : year,
                VoteAverage = movieResult.VoteAverage,
                PosterPath = movieResult.PosterPath,
                Runtime = movieResult.Runtime,
                Genres = string.Join(" / ", movieResult.Genres.Select(g => g.Name))
            };
        }

        public async Task<Show?> CreateShowFromTMDb(string title)
        {
            var encodedTitle = Uri.EscapeDataString(title);
            var searchResponse = await _httpClient.GetAsync($"https://api.themoviedb.org/3/search/tv?api_key={_apiKey}&query={encodedTitle}&language=cs-CZ");
            if (!searchResponse.IsSuccessStatusCode) return null;

            var searchResult = JsonSerializer.Deserialize<TMDbShowSearchResult>(await searchResponse.Content.ReadAsStringAsync());
            var firstShow = searchResult?.Results.FirstOrDefault();
            if (firstShow == null) return null;

            // OPRAVENÁ CHYBA KOMPILACE A CHYBA LOGIKY (odstraněna reference na seasonDto, použito firstShow.Id)
            var detailResponse = await _httpClient.GetAsync($"https://api.themoviedb.org/3/tv/{firstShow.Id}?api_key={_apiKey}&language=cs-CZ");
            if (!detailResponse.IsSuccessStatusCode) return null;

            var showDetails = JsonSerializer.Deserialize<TMDbShowResult>(await detailResponse.Content.ReadAsStringAsync());
            if (showDetails == null) return null;

            var newShow = new Show
            {
                // Zde se přiřazovalo TMDb ID, které nyní díky úpravě Show.cs kompilaci nebrání.
                TMDbId = firstShow.Id, 
                Title = showDetails.Name,
                Overview = showDetails.Overview,
                PosterPath = showDetails.PosterPath,
                Genres = string.Join(" / ", showDetails.Genres.Select(g => g.Name)),
                Seasons = new List<Season>()
            };

            foreach (var seasonDto in showDetails.Seasons)
            {
                var seasonDetailResponse = await _httpClient.GetAsync($"https://api.themoviedb.org/3/tv/{firstShow.Id}/season/{seasonDto.SeasonNumber}?api_key={_apiKey}&language=cs-CZ");
                if (!seasonDetailResponse.IsSuccessStatusCode) continue;

                var seasonDetailsResult = JsonSerializer.Deserialize<TMDbSeasonDetailResult>(await seasonDetailResponse.Content.ReadAsStringAsync());
                if (seasonDetailsResult == null) continue;

                int.TryParse(seasonDto.AirDate?.Split('-').FirstOrDefault() ?? seasonDetailsResult.AirDate?.Split('-').FirstOrDefault(), out int year);

                var newSeason = new Season
                {
                    SeasonNumber = seasonDto.SeasonNumber,
                    ReleaseYear = year == 0 ? null : year,
                    Episodes = new List<Episode>()
                };

                foreach (var episodeDto in seasonDetailsResult.Episodes)
                {
                    newSeason.Episodes.Add(new Episode
                    {
                        EpisodeNumber = episodeDto.EpisodeNumber,
                        Title = episodeDto.Name,
                        Overview = episodeDto.Overview,
                        Runtime = episodeDto.Runtime
                    });
                }
                if (newSeason.Episodes.Any()) { newShow.Seasons.Add(newSeason); }
            }
            return newShow;
        }
		public async Task<IEnumerable<object>> SearchAsync(string query)
{
    var encodedQuery = Uri.EscapeDataString(query);
    var searchResponse = await _httpClient.GetAsync($"https://api.themoviedb.org/3/search/multi?api_key={_apiKey}&query={encodedQuery}&language=cs-CZ");
    if (!searchResponse.IsSuccessStatusCode) return new List<object>();

    var searchResult = JsonSerializer.Deserialize<TMDbMultiSearchResult>(await searchResponse.Content.ReadAsStringAsync());
    if (searchResult == null) return new List<object>();

    // Zpracujeme výsledky a vybereme jen to, co potřebujeme
    var results = searchResult.Results
        .Where(r => r.MediaType == "movie" || r.MediaType == "tv")
        .Select(r =>
        {
            if (r.MediaType == "movie")
            {
                return new {
                    title = r.Title,
                    year = string.IsNullOrEmpty(r.ReleaseDate) ? "" : r.ReleaseDate.Substring(0, 4),
                    type = "Film"
                };
            }
            else // tv
            {
                return new {
                    title = r.Name,
                    year = string.IsNullOrEmpty(r.FirstAirDate) ? "" : r.FirstAirDate.Substring(0, 4),
                    type = "Seriál"
                };
            }
        })
        .Take(7) // Vrátíme maximálně 7 výsledků
        .ToList();

    return results;
}
    }
}