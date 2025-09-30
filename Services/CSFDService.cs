// [Services/CSFDService.cs]

using HtmlAgilityPack;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System;
using System.Text.RegularExpressions;
using System.Net;

namespace KodiBackend.Services
{
    public class CSFDService
    {
        private readonly HttpClient _httpClient;
        
        // Všechny konstanty jsou ponechány beze změny
        private const string CsfdTopCzSkUrl1 = "https://www.csfd.cz/zebricky/vlastni-vyber/?filter=rlW0rKOyVwbkYPWipzyanJ4vBwR5AljvM2IhpzHvBygqYPW5MJSlK2Mlo20vBz51oTjfVayyLKWsqT8vBz51oTjfVzSwqT9lVwcoKFjvMTylMJA0o3VvBygqsD";
        private const string CsfdTopCzUrl2 = "https://www.csfd.cz/zebricky/vlastni-vyber/?filter=rlW0rKOyVwbkYPWipzyanJ4vBwRfVzqyoaWyVwcoKFjvrJIupy9zpz9gVwchqJkfYPW5MJSlK3EiVwchqJkfYPWuL3EipvV6J10fVzEcpzIwqT9lVwcoKK0"; 
        private const string CsfdTopShowCzSkUrl1 = "https://www.csfd.cz/zebricky/vlastni-vyber/?filter=rlW0rKOyVwbmYPWipzyanJ4vBwRfVzqyoaWyVwcoKFjvrJIupy9zpz9gVwchqJkfYPW5MJSlK3EiVwchqJkfYPWuL3EipvV6J10fVzEcpzIwqT9lVwcoKK0";
        private const string CsfdTopShowCzUrl2 = "https://www.csfd.cz/zebricky/vlastni-vyber/?filter=rlW0rKOyVwbmYPWipzyanJ4vBwR5AljvM2IhpzHvBygqYPW5MJSlK2Mlo20vBz51oTjfVayyLKWsqT8vBz51oTjfVzSwqT9lVwcoKFjvMTylMJA0o3VvBygqsD";
        private const string CsfdTopGeneralUrl = "https://www.csfd.cz/zebricky/filmy/nejlepsi/"; 
        private const string CsfdTopShowGeneralUrl = "https://www.csfd.cz/zebricky/serialy/nejlepsi/"; 
        private const string CsfdTopShowDocumentariesUrl = "https://www.csfd.cz/zebricky/vlastni-vyber/?filter=rlW0rKOyVwbmYPWipzyanJ4vBz51oTjfVzqyoaWyVwcoZGAqYPW5MJSlK2Mlo20vBz51oTjfVayyLKWsqT8vBz51oTjfVzSwqT9lVwcoKFjvMTylMJA0o3VvBygqsD";
        private const string CsfdTopFairyTalesUrl = "https://www.csfd.cz/zebricky/vlastni-vyber/?filter=rlW0rKOyVwbkYPWipzyanJ4vBz51oTjfVzqyoaWyVwcoZmOqYPW5MJSlK2Mlo20vBz51oTjfVayyLKWsqT8vBz51oTjfVzSwqT9lVwcoKFjvMTylMJA0o3VvBygqsD";
        private const string CsfdTopMusicalUrl = "https://www.csfd.cz/zebricky/vlastni-vyber/?filter=rlW0rKOyVwbkYPWipzyanJ4vBz51oTjfVzqyoaWyVwcoZwWqYPW5MJSlK2Mlo20vBz51oTjfVayyLKWsqT8vBz51oTjfVzSwqT9lVwcoKFjvMTylMJA0o3VvBygqsD";
        private const string CsfdTopConcertsUrl = "https://www.csfd.cz/zebricky/vlastni-vyber/?filter=rlW0rKOyVwb2YPWipzyanJ4vBz51oTjfVzqyoaWyVwcoKFjvrJIupy9zpz9gVwchqJkfYPW5MJSlK3EiVwchqJkfYPWuL3EipvV6J10fVzEcpzIwqT9lVwcoKK0";

        public CSFDService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        // ### ZAČÁTEK NOVÉ ČÁSTI: Pomocná metoda jen pro paralelní stahování ###
        private async Task<List<string>> ScrapeSinglePageAsync(string url)
        {
            var titles = new List<string>();
            try
            {
                Console.WriteLine($"[CSFD DIAG] Stahuji stránku: {url}");
                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"[CSFD DIAG] Chyba při stahování {url}: Status {response.StatusCode}");
                    return titles;
                }
                
                var html = await response.Content.ReadAsStringAsync();
                var htmlDoc = new HtmlDocument();
                htmlDoc.LoadHtml(html);
                var movieNodes = htmlDoc.DocumentNode.SelectNodes("//a[@class='film-title-name']");
                
                if (movieNodes != null)
                {
                    foreach (var node in movieNodes)
                    {
                        string? rawTitle = node.GetAttributeValue("title", string.Empty);
                        // Dekódování HTML entit jako &apos; atd.
                        string? title = WebUtility.HtmlDecode(rawTitle);
                        if (!string.IsNullOrWhiteSpace(title))
                        {
                            titles.Add(title.Trim());
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CSFD DIAG] Výjimka při stahování {url}: {ex.Message}");
            }
            return titles;
        }
        // ### KONEC NOVÉ ČÁSTI ###


        // ### ZAČÁTEK JEDINÉ ZMĚNY: Přepracovaná metoda pro TOP RATED FILMY ###
        public async Task<List<string>> GetTopGeneralTitlesFromCsfdAsync()
        {
            Console.WriteLine("[CSFD DIAG] Stahuji TOP RATED filmy z ČSFD z předem definovaných stránek (paralelně)...");

            // Tvůj nápad: Natvrdo definujeme všechny stránky, které chceme stáhnout
            var urls = new List<string>
            {
                "https://www.csfd.cz/zebricky/filmy/nejlepsi/",
                "https://www.csfd.cz/zebricky/filmy/nejlepsi/?from=100",
                "https://www.csfd.cz/zebricky/filmy/nejlepsi/?from=200",
                "https://www.csfd.cz/zebricky/filmy/nejlepsi/?from=300",
                "https://www.csfd.cz/zebricky/filmy/nejlepsi/?from=400",
                "https://www.csfd.cz/zebricky/filmy/nejlepsi/?from=500"
            };

            // Vytvoříme seznam úkolů (Task) pro stažení každé stránky
            var tasks = urls.Select(url => ScrapeSinglePageAsync(url)).ToList();

            // Počkáme, až se všechny stránky stáhnou najednou
            var results = await Task.WhenAll(tasks);

            // Zkombinujeme výsledky ze všech stránek do jednoho seznamu a odstraníme duplicity
            var allTitles = new List<string>();
            var addedTitles = new HashSet<string>();
            foreach (var titleList in results)
            {
                foreach (var title in titleList)
                {
                    if (addedTitles.Add(title)) // Pokud se titul podaří přidat (ještě tam není)
                    {
                        allTitles.Add(title);
                    }
                }
            }

            Console.WriteLine($"[CSFD DIAG] Úspěšně staženo {allTitles.Count} unikátních titulů z {urls.Count} stránek.");
            return allTitles;
        }
        // ### KONEC JEDINÉ ZMĚNY ###

        #region Ostatní metody (beze změny, ponechány z tvé zálohy)

        // Tyto pomocné metody zůstávají pro ostatní funkce, které je využívají
        private async Task<List<string>> ScrapeGeneralTitlesWithPagination(string baseUrl, int maxItems = 500)
        {
            var allTitles = new List<string>();
            var addedTitles = new HashSet<string>();
            
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
                
                if (movieNodes == null || movieNodes.Count == 0)
                {
                    if (from > 0) break;
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
                            if (allTitles.Count >= maxItems) break;
                        }
                    }
                }
                
                if (foundOnPage < 100 && from > 0) break;
            }
            Console.WriteLine($"[CSFD DIAG] Nalezeno celkem {allTitles.Count} unikátních titulů z obecného žebříčku.");
            return allTitles;
        }

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

        public async Task<List<string>> GetTopTitlesFromCsfdAsync()
        {
            Console.WriteLine("[CSFD DIAG] Zahajuji scraping 2 ČSFD žebříčků filmů a prokládání...");
            
            var task1 = ScrapeFilteredTitlesWithPagination(CsfdTopCzSkUrl1, maxPages: 25);
            var task2 = ScrapeFilteredTitlesWithPagination(CsfdTopCzUrl2, maxPages: 25);

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
            return interleavedTitles.Take(1000).ToList();
        }
        
        public async Task<List<string>> GetTopShowGeneralTitlesFromCsfdAsync()
        {
            // Tato funkce pro seriály zůstává na starém principu, protože funguje
            Console.WriteLine("[CSFD DIAG] Stahuji obecný žebříček nejlepších seriálů z ČSFD...");
            return await ScrapeGeneralTitlesWithPagination(CsfdTopShowGeneralUrl, maxItems: 500);
        }
        
        public async Task<List<string>> GetTopShowTitlesFromCsfdAsync()
        {
            Console.WriteLine("[CSFD DIAG] Zahajuji scraping 2 ČSFD žebříčků seriálů a prokládání...");
            
            var task1 = ScrapeFilteredTitlesWithPagination(CsfdTopShowCzSkUrl1, maxPages: 25);
            var task2 = ScrapeFilteredTitlesWithPagination(CsfdTopShowCzUrl2, maxPages: 25);

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
            return interleavedTitles.Take(1000).ToList();
        }
        
        public async Task<List<string>> GetTopDocumentaryShowTitlesFromCsfdAsync()
        {
            Console.WriteLine("[CSFD DIAG] Zahajuji scraping ČSFD žebříčku dokumentárních seriálů...");
            var titles = await ScrapeFilteredTitlesWithPagination(CsfdTopShowDocumentariesUrl, maxPages: 25); 
            Console.WriteLine($"[CSFD DIAG] Úspěšně staženo {titles.Count} unikátních titulů dokumentárních seriálů.");
            return titles.Take(1000).ToList();
        }
        
        public async Task<List<string>> GetTopFairyTaleTitlesFromCsfdAsync()
        {
            Console.WriteLine("[CSFD DIAG] Zahajuji scraping ČSFD žebříčku pohádek...");
            var titles = await ScrapeFilteredTitlesWithPagination(CsfdTopFairyTalesUrl, maxPages: 25); 
            Console.WriteLine($"[CSFD DIAG] Úspěšně staženo {titles.Count} unikátních titulů pohádek.");
            return titles.Take(1000).ToList();
        }
        
        public async Task<List<string>> GetTopMusicalTitlesFromCsfdAsync()
        {
            Console.WriteLine("[CSFD DIAG] Zahajuji scraping ČSFD žebříčku hudebních filmů...");
            var titles = await ScrapeFilteredTitlesWithPagination(CsfdTopMusicalUrl, maxPages: 25); 
            Console.WriteLine($"[CSFD DIAG] Úspěšně staženo {titles.Count} unikátních titulů hudebních filmů.");
            return titles.Take(1000).ToList();
        }

        public async Task<List<string>> GetTopConcertTitlesFromCsfdAsync()
        {
            Console.WriteLine("[CSFD DIAG] Zahajuji scraping ČSFD žebříčku koncertů...");
            var titles = await ScrapeFilteredTitlesWithPagination(CsfdTopConcertsUrl, maxPages: 25); 
            Console.WriteLine($"[CSFD DIAG] Úspěšně staženo {titles.Count} unikátních titulů koncertů.");
            return titles.Take(1000).ToList();
        }

        #endregion
    }
}