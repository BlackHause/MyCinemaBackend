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

        private class TMDbMovieSearchResult { [JsonPropertyName("results")] public List<TMDbMovieResult> Results { get; set; } = new(); }
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

        private class TMDbShowSearchResult { [JsonPropertyName("results")] public List<TMDbShowResult> Results { get; set; } = new(); }
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

        public async Task<Movie?> GetMovieDetailsAsync(string title)
        {
            var encodedTitle = Uri.EscapeDataString(title);
            var searchResponse = await _httpClient.GetAsync($"https://api.themoviedb.org/3/search/movie?api_key={_apiKey}&query={encodedTitle}&language=cs-CZ");
            if (!searchResponse.IsSuccessStatusCode) return null;

            var searchResult = JsonSerializer.Deserialize<TMDbMovieSearchResult>(await searchResponse.Content.ReadAsStringAsync());
            var firstMovie = searchResult?.Results.FirstOrDefault();
            if (firstMovie == null) return null;

            var detailResponse = await _httpClient.GetAsync($"https://api.themoviedb.org/3/movie/{firstMovie.Id}?api_key={_apiKey}&language=cs-CZ");
            if (!detailResponse.IsSuccessStatusCode) return new Movie { Title = firstMovie.Title };

            var movieResult = JsonSerializer.Deserialize<TMDbMovieResult>(await detailResponse.Content.ReadAsStringAsync());
            if (movieResult == null) return null;

            int.TryParse(movieResult.ReleaseDate?.Split('-').FirstOrDefault(), out int year);

            return new Movie
            {
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

            var detailResponse = await _httpClient.GetAsync($"https://api.themoviedb.org/3/tv/{firstShow.Id}?api_key={_apiKey}&language=cs-CZ");
            if (!detailResponse.IsSuccessStatusCode) return null;

            var showDetails = JsonSerializer.Deserialize<TMDbShowResult>(await detailResponse.Content.ReadAsStringAsync());
            if (showDetails == null) return null;

            var newShow = new Show
            {
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