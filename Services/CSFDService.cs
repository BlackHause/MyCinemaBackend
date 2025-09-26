// [Services/CSFDService.cs]

using HtmlAgilityPack;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System;
using System.Text.RegularExpressions;

namespace KodiBackend.Services
{
    public class CSFDService
    {
        private readonly HttpClient _httpClient;
        
        // Pevné URL pro žebříček CZ/SK filmů (stávající)
        private const string CsfdTopCzSkUrl1 = "https://www.csfd.cz/zebricky/vlastni-vyber/?filter=rlW0rKOyVwbkYPWipzyanJ4vBwR5AljvM2IhpzHvBygqYPW5MJSlK2Mlo20vBz51oTjfVayyLKWsqT8vBz51oTjfVzSwqT9lVwcoKFjvMTylMJA0o3VvBygqsD";
        private const string CsfdTopCzUrl2 = "https://www.csfd.cz/zebricky/vlastni-vyber/?filter=rlW0rKOyVwbkYPWipzyanJ4vBwRfVzqyoaWyVwcoKFjvrJIupy9zpz9gVwchqJkfYPW5MJSlK3EiVwchqJkfYPWuL3EipvV6J10fVzEcpzIwqT9lVwcoKK0"; 
        
        // Pevné URL pro žebříček CZ/SK seriálů (stávající)
        private const string CsfdTopShowCzSkUrl1 = "https://www.csfd.cz/zebricky/vlastni-vyber/?filter=rlW0rKOyVwbmYPWipzyanJ4vBwRfVzqyoaWyVwcoKFjvrJIupy9zpz9gVwchqJkfYPW5MJSlK3EiVwchqJkfYPWuL3EipvV6J10fVzEcpzIwqT9lVwcoKK0";
        private const string CsfdTopShowCzUrl2 = "https://www.csfd.cz/zebricky/vlastni-vyber/?filter=rlW0rKOyVwbmYPWipzyanJ4vBwR5AljvM2IhpzHvBygqYPW5MJSlK2Mlo20vBz51oTjfVayyLKWsqT8vBz51oTjfVzSwqT9lVwcoKFjvMTylMJA0o3VvBygqsD";
        
        // Pevná URL pro obecné top filmy (stávající)
        private const string CsfdTopGeneralUrl = "https://www.csfd.cz/zebricky/filmy/nejlepsi/"; 
        
        // NOVÁ KONSTANTA: Nejlepší seriály (obecné) z ČSFD
        private const string CsfdTopShowGeneralUrl = "https://www.csfd.cz/zebricky/serialy/nejlepsi/"; 

        public CSFDService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        // PŮVODNÍ ScrapeTitles je jednoduchá (bez stránkování). Ponecháme, ale pro obecné žebříčky vytvoříme novou metodu.
        private async Task<List<string>> ScrapeTitles(string url)
        {
            var movieTitles = new List<string>();
            var response = await _httpClient.GetAsync(url);
            
            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"[CSFD DIAG] Chyba HTTP při stahování {url}: {response.StatusCode}");
                return movieTitles; 
            }

            var html = await response.Content.ReadAsStringAsync();
            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(html);

            var movieNodes = htmlDoc.DocumentNode.SelectNodes("//a[@class='film-title-name']");
            
            if (movieNodes != null)
            {
                foreach (var node in movieNodes)
                {
                    string? title = node.GetAttributeValue("title", string.Empty);
                    if (!string.IsNullOrWhiteSpace(title))
                    {
                        movieTitles.Add(title.Trim()); 
                    }
                }
            }
            Console.WriteLine($"[CSFD DIAG] Nalezeno {movieTitles.Count} titulů na {url}.");
            return movieTitles;
        }

        // NOVÁ METODA: ZAVÁDÍ STRÁNKOVÁNÍ PRO OBECNÉ ŽEBŘÍČKY (FILMY/SERIÁLY)
        private async Task<List<string>> ScrapeGeneralTitlesWithPagination(string baseUrl, int maxPages = 5)
        {
            var allTitles = new List<string>();
            var addedTitles = new HashSet<string>();
            
            for (int page = 1; page <= maxPages; page++)
            {
                // Formátování URL pro stránkování: 1. stránka je bez ?strana=X, další stránky mají ?strana=X
                string url = page == 1 ? baseUrl : $"{baseUrl}?strana={page}";
                Console.WriteLine($"[CSFD DIAG] Stahuji stránku {page}: {url}");

                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode) break; 

                var html = await response.Content.ReadAsStringAsync();
                var htmlDoc = new HtmlDocument();
                htmlDoc.LoadHtml(html);

                var movieNodes = htmlDoc.DocumentNode.SelectNodes("//a[@class='film-title-name']");
                
                // Přerušíme, pokud nenajdeme žádné titulky (to znamená konec žebříčku)
                if (movieNodes == null || movieNodes.Count == 0) break; 

                foreach (var node in movieNodes)
                {
                    string? title = node.GetAttributeValue("title", string.Empty);
                    if (!string.IsNullOrWhiteSpace(title) && addedTitles.Add(title.Trim()))
                    {
                        allTitles.Add(title.Trim()); 
                    }
                }
            }
            Console.WriteLine($"[CSFD DIAG] Nalezeno celkem {allTitles.Count} unikátních titulů z obecného žebříčku.");
            return allTitles;
        }

        // Metoda pro stávající CZ/SK Top filmy
        public async Task<List<string>> GetTopTitlesFromCsfdAsync()
        {
            Console.WriteLine("[CSFD DIAG] Zahajuji scraping 2 ČSFD žebříčků filmů a prokládání...");
            
            var task1 = ScrapeTitles(CsfdTopCzSkUrl1);
            var task2 = ScrapeTitles(CsfdTopCzUrl2);

            await Task.WhenAll(task1, task2);

            var list1 = task1.Result; 
            var list2 = task2.Result;
            var interleavedTitles = new List<string>();
            var addedTitles = new HashSet<string>(); 
            
            int maxCount = Math.Max(list1.Count, list2.Count);
            
            for (int i = 0; i < maxCount; i++)
            {
                if (i < list1.Count)
                {
                    string title1 = list1[i];
                    if (addedTitles.Add(title1)) 
                    {
                        interleavedTitles.Add(title1);
                    }
                }

                if (i < list2.Count)
                {
                    string title2 = list2[i];
                    if (addedTitles.Add(title2)) 
                    {
                        interleavedTitles.Add(title2);
                    }
                }
            }

            Console.WriteLine($"[CSFD DIAG] Úspěšně proloženo a deduplikováno. Celkem {interleavedTitles.Count} unikátních titulů.");

            // PŮVODNÍ LIMIT 500 TITULŮ (NAVIŠUJEME PRO ZAJIŠTĚNÍ DOSTATEČNÉ REZERVY)
            return interleavedTitles.Take(1000).ToList();
        }
        
        // Stávající metoda pro CZ/SK Top seriály
        public async Task<List<string>> GetTopShowTitlesFromCsfdAsync()
        {
            Console.WriteLine("[CSFD DIAG] Zahajuji scraping 2 ČSFD žebříčků seriálů a prokládání...");
            
            var task1 = ScrapeTitles(CsfdTopShowCzSkUrl1);
            var task2 = ScrapeTitles(CsfdTopShowCzUrl2);

            await Task.WhenAll(task1, task2);

            var list1 = task1.Result; 
            var list2 = task2.Result;
            var interleavedTitles = new List<string>();
            var addedTitles = new HashSet<string>(); 
            
            int maxCount = Math.Max(list1.Count, list2.Count);
            
            for (int i = 0; i < maxCount; i++)
            {
                if (i < list1.Count)
                {
                    string title1 = list1[i];
                    if (addedTitles.Add(title1)) 
                    {
                        interleavedTitles.Add(title1);
                    }
                }

                if (i < list2.Count)
                {
                    string title2 = list2[i];
                    if (addedTitles.Add(title2)) 
                    {
                        interleavedTitles.Add(title2);
                    }
                }
            }

            Console.WriteLine($"[CSFD DIAG] Úspěšně proloženo a deduplikováno seriálů. Celkem {interleavedTitles.Count} unikátních titulů.");

            // PŮVODNÍ LIMIT 500 TITULŮ (NAVIŠUJEME PRO ZAJIŠTĚNÍ DOSTATEČNÉ REZERVY)
            return interleavedTitles.Take(1000).ToList();
        }
        
        // ZMĚNA: Stávající metoda pro obecné top filmy (používá stránkování)
        public async Task<List<string>> GetTopGeneralTitlesFromCsfdAsync()
        {
            Console.WriteLine("[CSFD DIAG] Stahuji obecný žebříček nejlepších filmů z ČSFD...");
            // ZMĚNA: Používáme stránkování, zkusíme stáhnout 5 stránek
            return await ScrapeGeneralTitlesWithPagination(CsfdTopGeneralUrl, maxPages: 5);
        }

        // NOVÁ METODA: Stahuje obecné top seriály z ČSFD (jeden seznam se stránkováním)
        public async Task<List<string>> GetTopShowGeneralTitlesFromCsfdAsync()
        {
            Console.WriteLine("[CSFD DIAG] Stahuji obecný žebříček nejlepších seriálů z ČSFD...");
            // ZMĚNA: Používáme stránkování, zkusíme stáhnout 5 stránek
            return await ScrapeGeneralTitlesWithPagination(CsfdTopShowGeneralUrl, maxPages: 5);
        }
    }
}