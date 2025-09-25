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
        
        var token = await GetTokenAsync();
        var client = _httpClientFactory.CreateClient();
        
        Console.WriteLine($"\n🔍 HLEDÁM S DOTAZEM: '{query}'");
        var searchData = new FormUrlEncodedContent(new[] { new KeyValuePair<string, string>("what", query), new KeyValuePair<string, string>("category", "video"), new KeyValuePair<string, string>("limit", "500"), new KeyValuePair<string, string>("wst", token ?? "") });
        var searchResponse = await client.PostAsync("https://webshare.cz/api/search/", searchData);
        if (!searchResponse.IsSuccessStatusCode) return new List<WebshareLinkDto>();

        var searchXml = XDocument.Parse(await searchResponse.Content.ReadAsStringAsync());
        var allRawFiles = searchXml.Root?.Elements("file").Select(f => new { Ident = f.Element("ident")?.Value ?? "N/A", Name = f.Element("name")?.Value ?? "Neznámý název", Size = long.TryParse(f.Element("size")?.Value, out var s) ? s : 0 }).ToList();
        
        Console.WriteLine($"\n📦 Webshare vrátil celkem {allRawFiles?.Count ?? 0} souborů.");

        var titleKeywords = title.ToLowerInvariant().Split(new[] { ' ', ':', '-' }, StringSplitOptions.RemoveEmptyEntries);
        var videoExtensions = new[] { ".mkv", ".mp4", ".avi" };

        var files = allRawFiles
            .Where(f => {
                if (string.IsNullOrEmpty(f.Name)) return false;
                
                // --- VYLEPŠENÍ 1: Vše porovnáváme bez diakritiky ---
                var fileNameNoDiacritics = RemoveDiacritics(f.Name.ToLowerInvariant());
                return titleKeywords.All(k => fileNameNoDiacritics.Contains(RemoveDiacritics(k)));
            })
            .Where(f => {
                if (!isSeriesSearch) return true;
                
                var fileNameLower = f.Name.ToLowerInvariant().Replace(".", " ").Replace("_", " ").Replace("-", " ");
                
                // --- VYLEPŠENÍ 2: Přidán nový formát pro seriály ---
                var patterns = new[] { 
                    $"s{season:D2}e{episode:D2}", // s01e01
                    $"{season}x{episode:D2}",     // 1x01
                    $"{season:D2}x{episode:D2}"    // 01x01
                };
                return patterns.Any(p => fileNameLower.Contains(p));
            })
            .Where(f => videoExtensions.Any(ext => f.Name.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(f => f.Size)
            .ToList();

        Console.WriteLine($"\n👍 Po filtraci zbylo {files.Count} relevantních souborů. Zde je prvních 15:");
        foreach (var file in files.Take(15)) { Console.WriteLine($"  -> Název: {file.Name}, Velikost: {Math.Round((decimal)file.Size / (1024 * 1024 * 1024), 2)} GB"); }
        Console.WriteLine("---------------------------------------------");

        List<long> sizeLimitsBytes;
        if (isSeriesSearch) {
            sizeLimitsBytes = new List<double> { 10, 5, 2, 1 }.Select(gb => (long)(gb * Math.Pow(1024, 3))).ToList();
        } else {
            sizeLimitsBytes = new List<double> { 30, 17, 7, 3 }.Select(gb => (long)(gb * Math.Pow(1024, 3))).ToList();
        }

        var selectedLinks = new List<WebshareLinkDto>();
        var availableFiles = new List<dynamic>(files);

        foreach (var limit in sizeLimitsBytes)
        {
            var bestFit = availableFiles.FirstOrDefault(f => f.Size <= limit);
            if (bestFit != null)
            {
                selectedLinks.Add(new WebshareLinkDto { Ident = bestFit.Ident, SizeGb = Math.Round((decimal)bestFit.Size / (1024 * 1024 * 1024), 2) });
                availableFiles.Remove(bestFit);
            }
        }
        
        Console.WriteLine($"\n✅ Finálně vybráno {selectedLinks.Count} odkazů:");
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