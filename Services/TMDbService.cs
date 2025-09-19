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
        // ZDE JE TVŮJ VLASTNÍ API KLÍČ
        private readonly string _apiKey = "723975edd7760961afc4cf85daa9968a"; 

        #region Pomocné třídy pro JSON
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
        }

        private class TMDbShowSearchResult { [JsonPropertyName("results")] public List<TMDbShowResult> Results { get; set; } = new(); }
        private class TMDbShowResult
        {
            [JsonPropertyName("id")] public int Id { get; set; }
            [JsonPropertyName("name")] public string? Name { get; set; }
            [JsonPropertyName("overview")] public string? Overview { get; set; }
            [JsonPropertyName("poster_path")] public string? PosterPath { get; set; }
            [JsonPropertyName("seasons")] public List<TMDbSeasonInfo> Seasons { get; set; } = new();
        }
        private class TMDbSeasonInfo { [JsonPropertyName("season_number")] public int SeasonNumber { get; set; } }
        private class TMDbSeasonResult { [JsonPropertyName("episodes")] public List<TMDbEpisodeResult> Episodes { get; set; } = new(); }
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
                Runtime = movieResult.Runtime
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
                Seasons = new List<Season>()
            };

            foreach (var seasonDto in showDetails.Seasons)
            {
                var seasonDetailResponse = await _httpClient.GetAsync($"https://api.themoviedb.org/3/tv/{firstShow.Id}/season/{seasonDto.SeasonNumber}?api_key={_apiKey}&language=cs-CZ");
                if (!seasonDetailResponse.IsSuccessStatusCode) continue;

                var seasonDetails = JsonSerializer.Deserialize<TMDbSeasonResult>(await seasonDetailResponse.Content.ReadAsStringAsync());
                if (seasonDetails == null) continue;

                var newSeason = new Season { SeasonNumber = seasonDto.SeasonNumber, Episodes = new List<Episode>() };

                foreach (var episodeDto in seasonDetails.Episodes)
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
    }
}