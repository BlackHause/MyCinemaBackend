using System;
using System.Collections.Generic;
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
    private string _token;
    private static readonly SemaphoreSlim _loginSemaphore = new SemaphoreSlim(1, 1);


    public WebshareService(IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
    }

    private async Task<string> GetTokenAsync()
    {
        if (!string.IsNullOrEmpty(_token))
        {
            // Opcionalne by se zde dala pridat validace tokenu
            return _token;
        }

        // Zajistime, ze se prihlaseni spusti pouze jednou i pri vice soubeznych pozadavcich
        await _loginSemaphore.WaitAsync();
        try
        {
            // Znovu zkontrolujeme, jestli token mezitim neziskal jiny thread
            if (!string.IsNullOrEmpty(_token)) return _token;
            
            Console.WriteLine("Logging into Webshare...");
            var client = _httpClientFactory.CreateClient();
            var username = _configuration["Webshare:Username"];
            var password = _configuration["Webshare:Password"];

            // 1. Get Salt
            var saltResponse = await client.PostAsync("https://webshare.cz/api/salt/",
                new FormUrlEncodedContent(new[] { new KeyValuePair<string, string>("username_or_email", username) }));
            
            if (!saltResponse.IsSuccessStatusCode) throw new Exception("Failed to get salt from Webshare.");
            var saltXml = XDocument.Parse(await saltResponse.Content.ReadAsStringAsync());
            var salt = saltXml.Root?.Element("salt")?.Value;
            if (salt == null) throw new Exception("Salt not found in Webshare response.");

            // 2. Encrypt password
            var encryptedPass = GetSha1Hash(Md5Crypt.Crypt(password, salt));
            var digest = GetMd5Hash($"{username}:Webshare:{encryptedPass}");
            
            // 3. Login
            var loginData = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("username_or_email", username),
                new KeyValuePair<string, string>("password", encryptedPass),
                new KeyValuePair<string, string>("digest", digest),
                new KeyValuePair<string, string>("keep_logged_in", "1")
            });

            var loginResponse = await client.PostAsync("https://webshare.cz/api/login/", loginData);
            if (!loginResponse.IsSuccessStatusCode) throw new Exception("Failed to log in to Webshare.");

            var loginXml = XDocument.Parse(await loginResponse.Content.ReadAsStringAsync());
            var status = loginXml.Root?.Element("status")?.Value;
            if (status != "OK")
            {
                var message = loginXml.Root?.Element("message")?.Value;
                throw new Exception($"Webshare login failed: {message}");
            }
            
            _token = loginXml.Root?.Element("token")?.Value;
            Console.WriteLine("Webshare login successful.");
            return _token;
        }
        finally
        {
            _loginSemaphore.Release();
        }
    }

    public async Task<List<WebshareLinkDto>> FindLinksAsync(string title, int? year, int? season, int? episode)
    {
        string query;
        List<long> sizeLimitsBytes;

        if (year.HasValue) // Film
        {
            query = $"{title} {year.Value}";
            sizeLimitsBytes = new List<double> { 30, 17, 7, 3 }.Select(gb => (long)(gb * Math.Pow(1024, 3))).ToList();
        }
        else if (season.HasValue && episode.HasValue) // Seriál
        {
            query = $"{title} S{season:D2}E{episode:D2}";
            sizeLimitsBytes = new List<double> { 10, 5, 2, 1 }.Select(gb => (long)(gb * Math.Pow(1024, 3))).ToList();
        }
        else
        {
            throw new ArgumentException("Either 'year' or both 'season' and 'episode' must be provided.");
        }

        var token = await GetTokenAsync();
        var client = _httpClientFactory.CreateClient();

        var searchData = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("what", query),
            new KeyValuePair<string, string>("category", "video"),
            new KeyValuePair<string, string>("sort", "largest"),
            new KeyValuePair<string, string>("limit", "200"),
            new KeyValuePair<string, string>("wst", token)
        });

        var searchResponse = await client.PostAsync("https://webshare.cz/api/search/", searchData);
        if (!searchResponse.IsSuccessStatusCode) return new List<WebshareLinkDto>();

        var searchXml = XDocument.Parse(await searchResponse.Content.ReadAsStringAsync());
        var videoExtensions = new[] { ".mkv", ".mp4", ".avi" };
        
        var files = searchXml.Root?.Elements("file")
            .Select(f => new {
                Ident = f.Element("ident")?.Value,
                Name = f.Element("name")?.Value,
                Size = long.TryParse(f.Element("size")?.Value, out var s) ? s : 0
            })
            .Where(f => !string.IsNullOrEmpty(f.Ident) && videoExtensions.Any(ext => f.Name.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(f => f.Size)
            .ToList();

        var selectedLinks = new List<WebshareLinkDto>();
        var availableFiles = new List<dynamic>(files);

        foreach (var limit in sizeLimitsBytes)
        {
            var bestFit = availableFiles.FirstOrDefault(f => f.Size <= limit);
            if (bestFit != null)
            {
                selectedLinks.Add(new WebshareLinkDto
                {
                    Ident = bestFit.Ident,
                    SizeGb = Math.Round((decimal)bestFit.Size / (1024 * 1024 * 1024), 2)
                });
                availableFiles.Remove(bestFit);
            }
        }
        
        return selectedLinks;
    }

    private static string GetSha1Hash(string input)
    {
        using (var sha1 = SHA1.Create())
        {
            byte[] hashBytes = sha1.ComputeHash(Encoding.UTF8.GetBytes(input));
            return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
        }
    }
    
    private static string GetMd5Hash(string input)
    {
        using (var md5 = MD5.Create())
        {
            byte[] hashBytes = md5.ComputeHash(Encoding.UTF8.GetBytes(input));
            return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
        }
    }
}