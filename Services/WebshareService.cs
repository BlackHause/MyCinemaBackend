using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.Extensions.Configuration;
using System.Text.RegularExpressions; 

public class WebshareService : IWebshareService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private string? _token;
    private static readonly SemaphoreSlim _loginSemaphore = new SemaphoreSlim(1, 1);

    public WebshareService(IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
    }

    private async Task<string?> GetTokenAsync()
    {
        if (!string.IsNullOrEmpty(_token)) return _token;
        await _loginSemaphore.WaitAsync();
        try
        {
            if (!string.IsNullOrEmpty(_token)) return _token;
            Console.WriteLine("Logging into Webshare...");
            var client = _httpClientFactory.CreateClient();
            var username = _configuration["Webshare:Username"];
            var password = _configuration["Webshare:Password"];
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password)) throw new InvalidOperationException("Webshare credentials not configured.");
            var saltResponse = await client.PostAsync("https://webshare.cz/api/salt/", new FormUrlEncodedContent(new[] { new KeyValuePair<string, string>("username_or_email", username) }));
            if (!saltResponse.IsSuccessStatusCode) throw new Exception("Failed to get salt from Webshare.");
            var saltXml = XDocument.Parse(await saltResponse.Content.ReadAsStringAsync());
            var salt = saltXml.Root?.Element("salt")?.Value;
            if (salt == null) throw new Exception("Salt not found in Webshare response.");
            var encryptedPass = GetSha1Hash(Md5Crypt.Crypt(password, salt));
            var digest = GetMd5Hash($"{username}:Webshare:{encryptedPass}");
            var loginData = new FormUrlEncodedContent(new[] { new KeyValuePair<string, string>("username_or_email", username), new KeyValuePair<string, string>("password", encryptedPass), new KeyValuePair<string, string>("digest", digest), new KeyValuePair<string, string>("keep_logged_in", "1") });
            var loginResponse = await client.PostAsync("https://webshare.cz/api/login/", loginData);
            if (!loginResponse.IsSuccessStatusCode) throw new Exception("Failed to log in to Webshare.");
            var loginXml = XDocument.Parse(await loginResponse.Content.ReadAsStringAsync());
            var status = loginXml.Root?.Element("status")?.Value;
            if (status != "OK") { var message = loginXml.Root?.Element("message")?.Value; throw new Exception($"Webshare login failed: {message}"); }
            _token = loginXml.Root?.Element("token")?.Value;
            Console.WriteLine("Webshare login successful.");
            return _token;
        }
        finally { _loginSemaphore.Release(); }
    }
    
    public async Task<List<WebshareLinkDto>> FindLinksAsync(string title, int? year, int? season, int? episode)
    {
        bool isSeriesSearch = season.HasValue && season > 0 && episode.HasValue;
        
        string query = title;
        if (year.HasValue && year > 0)
        {
            query = $"{title} {year.Value}";
        }
        
        // --- Přesnější query pro seriály ---
        if (isSeriesSearch)
        {
            // Oprava chyby CS8629: Operátor ! je bezpečný, protože isSeriesSearch je true.
            string s_d2 = season!.Value.ToString("D2"); 
            string e_d2 = episode!.Value.ToString("D2");
            query = $"{title} S{s_d2}E{e_d2}"; 
        }
        // ------------------------------------------------
        
        var token = await GetTokenAsync();
        var client = _httpClientFactory.CreateClient();
        
        // --- LOG: HLEDANÝ DOTAZ ---
        Console.WriteLine($"\n🔍 HLEDÁM S DOTAZEM: '{query}'");
        
        var searchData = new FormUrlEncodedContent(new[] { new KeyValuePair<string, string>("what", query), new KeyValuePair<string, string>("category", "video"), new KeyValuePair<string, string>("limit", "500"), new KeyValuePair<string, string>("wst", token ?? "") });
        var searchResponse = await client.PostAsync("https://webshare.cz/api/search/", searchData);
        if (!searchResponse.IsSuccessStatusCode) return new List<WebshareLinkDto>();

        var searchXml = XDocument.Parse(await searchResponse.Content.ReadAsStringAsync());
        var allRawFiles = searchXml.Root?.Elements("file").Select(f => new { Ident = f.Element("ident")?.Value ?? "N/A", Name = f.Element("name")?.Value ?? "Neznámý název", Size = long.TryParse(f.Element("size")?.Value, out var s) ? s : 0 }).ToList();
        
        Console.WriteLine($"\n📦 Webshare vrátil celkem {allRawFiles?.Count ?? 0} souborů.");

        var titleKeywords = title.ToLowerInvariant().Split(new[] { ' ', ':', '-' }, StringSplitOptions.RemoveEmptyEntries);
        var videoExtensions = new[] { ".mkv", ".mp4", ".avi" };
        
        // --- LOG: PODMÍNKY FILTRACE ---
        string requiredPatterns = "";
        if (isSeriesSearch)
        {
            requiredPatterns = $"s{season!.Value:D2}e{episode!.Value:D2}, {season.Value}x{episode.Value:D2}, {season.Value:D2}x{episode.Value:D2}";
        }
        Console.WriteLine($"\n⭐ PODMÍNKY FILTRACE:");
        Console.WriteLine($"  - Povinné (AND): Název ('{string.Join(", ", titleKeywords)}')");
        if (isSeriesSearch)
        {
             Console.WriteLine($"  - Povinné (AND): Formát S/E (jeden z: {requiredPatterns})");
             // OPRAVA CHYBY CS0019: Převod int? na string před ??
             Console.WriteLine($"  - Pomocné/Priorita: Přesná shoda S{season!.Value:D2}E{episode!.Value:D2} OR Název epizody OR Rok vydání ({year?.ToString() ?? "N/A"}) + Velikost");
        }
        else if (year.HasValue)
        {
             Console.WriteLine($"  - Pomocné/Priorita: Rok vydání ({year.Value}) + Velikost");
        }
        else
        {
            Console.WriteLine($"  - Pomocné/Priorita: Velikost");
        }
        Console.WriteLine($"---------------------------------------------");
        // ------------------------------------

        var files = allRawFiles
            .Where(f => {
                if (string.IsNullOrEmpty(f.Name)) return false;
                
                // --- VYLEPŠENÍ 1: Vše porovnáváme bez diakritiky ---
                var fileNameNoDiacritics = RemoveDiacritics(f.Name.ToLowerInvariant());
                // POVINNÁ PODMÍNKA 1: Shoda na název seriálu
                return titleKeywords.All(k => fileNameNoDiacritics.Contains(RemoveDiacritics(k)));
            })
            .Where(f => {
                if (!isSeriesSearch) return true;
                
                var fileNameLower = f.Name.ToLowerInvariant().Replace(".", " ").Replace("_", " ").Replace("-", " ");
                
                // POVINNÁ PODMÍNKA 2: Shoda na formát SxxEyy, 1x01, atd.
                var patterns = new[] { 
                    $"s{season!.Value:D2}e{episode!.Value:D2}", // s01e01
                    $"{season.Value}x{episode.Value:D2}",     // 1x01
                    $"{season.Value:D2}x{episode.Value:D2}"    // 01x01
                };
                return patterns.Any(p => fileNameLower.Contains(p));
            })
            .Where(f => videoExtensions.Any(ext => f.Name.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
            .ToList(); 
            
        // --- ŘAZENÍ (Pomocná podmínka: Čistý SxxEyy formát A ROK VYDÁNÍ) ---
        if (isSeriesSearch)
        {
            // Přesný vzor S01E01 pro prioritizaci souborů s čistým označením (simulace názvu epizody).
            var fullMatchPattern = $"s{season!.Value:D2}e{episode!.Value:D2}";
            
            // Vzor pro rok vydání (pokud je k dispozici)
            string yearPattern = year.HasValue ? year.Value.ToString() : string.Empty;


            // Vytvoříme anonymní typ pro řazení
            var rankedFiles = files.Select(f => new 
            {
                f.Ident,
                f.Name,
                f.Size,
                // Pomocná podmínka 1: Přesná shoda SxxEyy (simuluje Název epizody)
                HasExactSeasonEpisodeFormat = f.Name.ToLowerInvariant().Replace(".", "").Contains(fullMatchPattern),
                // Pomocná podmínka 2: Rok vydání
                HasYearMatch = !string.IsNullOrEmpty(yearPattern) && f.Name.ToLowerInvariant().Contains(yearPattern) 
            })
            .OrderByDescending(f => f.HasExactSeasonEpisodeFormat) // Priorita 1: Přesná SxxEyy shoda (simuluje Název epizody)
            .ThenByDescending(f => f.HasYearMatch)                // Priorita 2: Rok vydání
            .ThenByDescending(f => f.Size)                         // Priorita 3: Velikost
            .ToList();

            // Převedeme zpět na původní dynamický typ, aby zbytek kódu fungoval
            files = rankedFiles.Select(f => new { f.Ident, f.Name, f.Size }).ToList();
        }
        else 
        {
            files = files.OrderByDescending(f => f.Size).ToList();
        }
        // --- Konec Řazení ---

        // --- LOG: TOP 15 SOUBORŮ ---
        Console.WriteLine($"\n👍 Po filtraci a seřazení zbylo {files.Count} relevantních souborů. TOP 15 názvů:");
        for (int i = 0; i < Math.Min(15, files.Count); i++)
        {
             Console.WriteLine($"  [{i + 1:D2}] -> {files[i].Name} ({Math.Round((decimal)files[i].Size / (1024 * 1024 * 1024), 2)} GB)");
        }
        Console.WriteLine("---------------------------------------------");

        List<long> sizeLimitsBytes;
        if (isSeriesSearch) {
            sizeLimitsBytes = new List<double> { 10, 5, 2, 1 }.Select(gb => (long)(gb * Math.Pow(1024, 3))).ToList();
        } else {
            sizeLimitsBytes = new List<double> { 30, 17, 7, 3 }.Select(gb => (long)(gb * Math.Pow(1024, 3))).ToList();
        }

        var selectedLinks = new List<WebshareLinkDto>();
        var availableFiles = new List<dynamic>(files);
        
        // Pomocný seznam pro logování názvů TOP 4 souborů
        var top4FileNames = new List<string>();

        foreach (var limit in sizeLimitsBytes)
        {
            var bestFit = availableFiles.FirstOrDefault(f => f.Size <= limit);
            if (bestFit != null)
            {
                selectedLinks.Add(new WebshareLinkDto { Ident = bestFit.Ident, SizeGb = Math.Round((decimal)bestFit.Size / (1024 * 1024 * 1024), 2) });
                top4FileNames.Add($"{bestFit.Name} ({Math.Round((decimal)bestFit.Size / (1024 * 1024 * 1024), 2)} GB)");
                availableFiles.Remove(bestFit);
            }
        }
        
        // --- LOG: FINÁLNÍ VÝBĚR A NÁZVY ---
        Console.WriteLine($"\n✅ Finálně vybráno {selectedLinks.Count} odkazů.");
        Console.WriteLine("TOP 4 NÁZVY SOUBORŮ:");
        for (int i = 0; i < Math.Min(4, top4FileNames.Count); i++)
        {
             Console.WriteLine($"  [{i + 1}] -> {top4FileNames[i]}");
        }
        Console.WriteLine("---------------------------------------------");
        
        Console.WriteLine("DETAILY (Ident/Velikost):");
        foreach (var link in selectedLinks) { Console.WriteLine($"  -> Ident: {link.Ident}, Velikost: {link.SizeGb} GB"); }
        Console.WriteLine("=============================================\n");
        
        return selectedLinks;
    }

    static string RemoveDiacritics(string text)
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

    private static string GetSha1Hash(string input) { using (var sha1 = SHA1.Create()) { byte[] hashBytes = sha1.ComputeHash(Encoding.UTF8.GetBytes(input)); return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant(); } }
    private static string GetMd5Hash(string input) { using (var md5 = MD5.Create()) { byte[] hashBytes = md5.ComputeHash(Encoding.UTF8.GetBytes(input)); return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant(); } }
}