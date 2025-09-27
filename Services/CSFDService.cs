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

        // NOVÁ KONSTANTA: URL pro žebříček dokumentárních seriálů
        private const string CsfdTopShowDocumentariesUrl = "https://www.csfd.cz/zebricky/vlastni-vyber/?filter=rlW0rKOyVwbmYPWipzyanJ4vBz51oTjfVzqyoaWyVwcoZGAqYPW5MJSlK2Mlo20vBz51oTjfVayyLKWsqT8vBz51oTjfVzSwqT9lVwcoKFjvMTylMJA0o3VvBygqsD";
        
        // NOVÁ KONSTANTA: URL pro žebříček pohádek
        private const string CsfdTopFairyTalesUrl = "https://www.csfd.cz/zebricky/vlastni-vyber/?filter=rlW0rKOyVwbkYPWipzyanJ4vBz51oTjfVzqyoaWyVwcoZmOqYPW5MJSlK2Mlo20vBz51oTjfVayyLKWsqT8vBz51oTjfVzSwqT9lVwcoKFjvMTylMJA0o3VvBygqsD";

        // NOVÁ KONSTANTA: URL pro žebříček hudebních filmů
        private const string CsfdTopMusicalUrl = "https://www.csfd.cz/zebricky/vlastni-vyber/?filter=rlW0rKOyVwbkYPWipzyanJ4vBz51oTjfVzqyoaWyVwcoZwWqYPW5MJSlK2Mlo20vBz51oTjfVayyLKWsqT8vBz51oTjfVzSwqT9lVwcoKFjvMTylMJA0o3VvBygqsD";

        // *** ZAČÁTEK NOVÉ ČÁSTI ***
        // NOVÁ KONSTANTA: URL pro žebříček koncertů
        private const string CsfdTopConcertsUrl = "https://www.csfd.cz/zebricky/vlastni-vyber/?filter=rlW0rKOyVwb2YPWipzyanJ4vBz51oTjfVzqyoaWyVwcoKFjvrJIupy9zpz9gVwchqJkfYPW5MJSlK3EiVwchqJkfYPWuL3EipvV6J10fVzEcpzIwqT9lVwcoKK0";
        // *** KONEC NOVÉ ČÁSTI ***

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
        // POUŽÍVÁ PARAMETR ?from=X (po 100)
        private async Task<List<string>> ScrapeGeneralTitlesWithPagination(string baseUrl, int maxItems = 500)
        {
            var allTitles = new List<string>();
            var addedTitles = new HashSet<string>();
            
            // Loop začíná na 0 a inkrementuje se o 100
            for (int from = 0; allTitles.Count < maxItems; from += 100)
            {
                string url = from == 0 ? baseUrl : $"{baseUrl}?from={from}";
                Console.WriteLine($"[CSFD DIAG] Stahuji: {url}");

                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode) break; 

                var html = await response.Content.ReadAsStringAsync();
                var htmlDoc = new HtmlDocument();
                htmlDoc.LoadHtml(html);

                var movieNodes = htmlDoc.DocumentNode.SelectNodes("//a[@class='film-title-name']");
                
                // Přerušíme, pokud nenajdeme žádné titulky, a nejsme na první stránce (konec žebříčku)
                if (movieNodes == null || movieNodes.Count == 0)
                {
                    if (from > 0) break;
                    // Pokud je to první stránka a nenašlo se nic, pokračujeme, ale asi je něco špatně
                    if (movieNodes == null) break;
                }

                int foundOnPage = 0;
                if (movieNodes != null)
                {
                    foreach (var node in movieNodes)
                    {
                        string? title = node.GetAttributeValue("title", string.Empty);
                        if (!string.IsNullOrWhiteSpace(title) && addedTitles.Add(title.Trim()))
                        {
                            allTitles.Add(title.Trim()); 
                            foundOnPage++;
                            if (allTitles.Count >= maxItems) break; // Zastavíme, pokud dosáhneme maxima
                        }
                    }
                }
                
                // Pokud získáme méně než 100 titulů, ale nejsme na konci požadovaného limitu, znamená to, že žebříček skončil
                if (foundOnPage < 100 && from > 0) break;
            }
            Console.WriteLine($"[CSFD DIAG] Nalezeno celkem {allTitles.Count} unikátních titulů z obecného žebříčku.");
            return allTitles;
        }

        // NOVÁ METODA: ZAVÁDÍ STRÁNKOVÁNÍ PRO FILTROVANÉ ŽEBŘÍČKY (CZ/SK)
        // POUŽÍVÁ PARAMETR ?page=X (po 20)
        private async Task<List<string>> ScrapeFilteredTitlesWithPagination(string baseUrl, int maxPages = 20)
        {
            var allTitles = new List<string>();
            var addedTitles = new HashSet<string>();
            
            for (int page = 1; page <= maxPages; page++)
            {
                string url = page == 1 ? baseUrl : $"{baseUrl}&page={page}";
                Console.WriteLine($"[CSFD DIAG] Stahuji filtrovanou stránku {page}: {url}");

                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode) break; 

                var html = await response.Content.ReadAsStringAsync();
                var htmlDoc = new HtmlDocument();
                htmlDoc.LoadHtml(html);

                var movieNodes = htmlDoc.DocumentNode.SelectNodes("//a[@class='film-title-name']");
                
                // Přerušíme, pokud nenajdeme žádné titulky (konec žebříčku)
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
            Console.WriteLine($"[CSFD DIAG] Nalezeno celkem {allTitles.Count} unikátních titulů z filtrovaného žebříčku.");
            return allTitles;
        }

        // Metoda pro stávající CZ/SK Top filmy
        public async Task<List<string>> GetTopTitlesFromCsfdAsync()
        {
            Console.WriteLine("[CSFD DIAG] Zahajuji scraping 2 ČSFD žebříčků filmů a prokládání...");
            
            // ZMĚNA: Používáme stránkovanou metodu pro CZ/SK žebříčky
            var task1 = ScrapeFilteredTitlesWithPagination(CsfdTopCzSkUrl1, maxPages: 25); // Získá až 25 stránek
            var task2 = ScrapeFilteredTitlesWithPagination(CsfdTopCzUrl2, maxPages: 25); // Získá až 25 stránek

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

            // PŮVODNÍ LIMIT 500 TITULŮ NAVÝŠEN NA 1000
            return interleavedTitles.Take(1000).ToList();
        }
        
        // Stávající metoda pro CZ/SK Top seriály
        public async Task<List<string>> GetTopShowTitlesFromCsfdAsync()
        {
            Console.WriteLine("[CSFD DIAG] Zahajuji scraping 2 ČSFD žebříčků seriálů a prokládání...");
            
            // ZMĚNA: Používáme stránkovanou metodu pro CZ/SK žebříčky
            var task1 = ScrapeFilteredTitlesWithPagination(CsfdTopShowCzSkUrl1, maxPages: 25); // Získá až 25 stránek
            var task2 = ScrapeFilteredTitlesWithPagination(CsfdTopShowCzUrl2, maxPages: 25); // Získá až 25 stránek

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

            // PŮVODNÍ LIMIT 500 TITULŮ NAVÝŠEN NA 1000
            return interleavedTitles.Take(1000).ToList();
        }
        
        // ZMĚNA: Stávající metoda pro obecné top filmy (používá stránkování)
        public async Task<List<string>> GetTopGeneralTitlesFromCsfdAsync()
        {
            Console.WriteLine("[CSFD DIAG] Stahuji obecný žebříček nejlepších filmů z ČSFD...");
            // ZMĚNA: Používáme nový mechanismus stránkování ?from=X, zkusíme stáhnout 500 filmů
            return await ScrapeGeneralTitlesWithPagination(CsfdTopGeneralUrl, maxItems: 500);
        }

        // NOVÁ METODA: Stahuje obecné top seriály z ČSFD (jeden seznam se stránkováním)
        public async Task<List<string>> GetTopShowGeneralTitlesFromCsfdAsync()
        {
            Console.WriteLine("[CSFD DIAG] Stahuji obecný žebříček nejlepších seriálů z ČSFD...");
            // ZMĚNA: Používáme nový mechanismus stránkování ?from=X, zkusíme stáhnout 500 seriálů
            return await ScrapeGeneralTitlesWithPagination(CsfdTopShowGeneralUrl, maxItems: 500);
        }

        // NOVÁ METODA: Stahuje top dokumentární seriály z ČSFD
        public async Task<List<string>> GetTopDocumentaryShowTitlesFromCsfdAsync()
        {
            Console.WriteLine("[CSFD DIAG] Zahajuji scraping ČSFD žebříčku dokumentárních seriálů...");
            
            // Používáme stránkovanou metodu pro filtrované žebříčky (stejně jako u CZ/SK)
            var titles = await ScrapeFilteredTitlesWithPagination(CsfdTopShowDocumentariesUrl, maxPages: 25); 

            Console.WriteLine($"[CSFD DIAG] Úspěšně staženo {titles.Count} unikátních titulů dokumentárních seriálů.");

            return titles.Take(1000).ToList(); // Omezíme na 1000, stejně jako ostatní
        }
        
        // NOVÁ METODA: Stahuje top pohádky z ČSFD
        public async Task<List<string>> GetTopFairyTaleTitlesFromCsfdAsync()
        {
            Console.WriteLine("[CSFD DIAG] Zahajuji scraping ČSFD žebříčku pohádek...");
            
            var titles = await ScrapeFilteredTitlesWithPagination(CsfdTopFairyTalesUrl, maxPages: 25); 

            Console.WriteLine($"[CSFD DIAG] Úspěšně staženo {titles.Count} unikátních titulů pohádek.");

            return titles.Take(1000).ToList();
        }
        
        // NOVÁ METODA: Stahuje top hudební filmy z ČSFD
        public async Task<List<string>> GetTopMusicalTitlesFromCsfdAsync()
        {
            Console.WriteLine("[CSFD DIAG] Zahajuji scraping ČSFD žebříčku hudebních filmů...");
            
            var titles = await ScrapeFilteredTitlesWithPagination(CsfdTopMusicalUrl, maxPages: 25); 

            Console.WriteLine($"[CSFD DIAG] Úspěšně staženo {titles.Count} unikátních titulů hudebních filmů.");

            return titles.Take(1000).ToList();
        }

        // *** ZAČÁTEK NOVÉ ČÁSTI ***
        // NOVÁ METODA: Stahuje top koncerty z ČSFD
        public async Task<List<string>> GetTopConcertTitlesFromCsfdAsync()
        {
            Console.WriteLine("[CSFD DIAG] Zahajuji scraping ČSFD žebříčku koncertů...");
            
            var titles = await ScrapeFilteredTitlesWithPagination(CsfdTopConcertsUrl, maxPages: 25); 

            Console.WriteLine($"[CSFD DIAG] Úspěšně staženo {titles.Count} unikátních titulů koncertů.");

            return titles.Take(1000).ToList();
        }
        // *** KONEC NOVÉ ČÁSTI ***
    }
}