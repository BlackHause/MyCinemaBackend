using Microsoft.AspNetCore.Mvc;
using KodiBackend.Services;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace KodiBackend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TMDbController : ControllerBase
    {
        private readonly TMDbService _tmdbService;

        public TMDbController(TMDbService tmdbService)
        {
            _tmdbService = tmdbService;
        }

        [HttpGet("search")]
        public async Task<ActionResult<IEnumerable<object>>> Search([FromQuery] string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return Ok(new List<object>());
            }
            var results = await _tmdbService.SearchAsync(query);
            return Ok(results);
        }
    }
}