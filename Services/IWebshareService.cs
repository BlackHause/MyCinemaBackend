using System.Collections.Generic;
using System.Threading.Tasks;

public class WebshareLinkDto
{
    public string Ident { get; set; }
    public decimal SizeGb { get; set; }
}

public interface IWebshareService
{
    Task<List<WebshareLinkDto>> FindLinksAsync(string title, int? year, int? season, int? episode);
}