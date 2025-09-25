using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

// DTO pro prijmuti dat z requestu
public class FindLinksRequest
{
    public string Title { get; set; }
    public int? Year { get; set; }
    public int? Season { get; set; }
    public int? Episode { get; set; }
}

[ApiController]
[Route("api/[controller]")]
public class WebshareController : ControllerBase
{
    private readonly IWebshareService _webshareService;

    public WebshareController(IWebshareService webshareService)
    {
        _webshareService = webshareService;
    }

    [HttpPost("find-links")]
    public async Task<IActionResult> FindLinks([FromBody] FindLinksRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return BadRequest("Title is required.");
        }

        var links = await _webshareService.FindLinksAsync(request.Title, request.Year, request.Season, request.Episode);

        if (links == null || links.Count == 0)
        {
            return NotFound("No suitable links found on Webshare.");
        }

        return Ok(links);
    }
}