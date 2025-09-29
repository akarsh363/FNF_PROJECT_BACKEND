using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using FNF_PROJ.Services;

namespace FNF_PROJ.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TagsController : ControllerBase
    {
        private readonly ITagService _tagService;

        public TagsController(ITagService tagService)
        {
            _tagService = tagService;
        }

        private int GetCurrentUserId()
        {
            string? userIdClaim =
                User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? User.FindFirst("sub")?.Value
                ?? User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;

            return int.TryParse(userIdClaim, out var id) ? id : 0;
        }

        [HttpGet("mine")]
        [Authorize]
        public async Task<IActionResult> GetMyDeptTags()
        {
            var uid = GetCurrentUserId();
            if (uid == 0) return Unauthorized();

            var tags = await _tagService.GetTagsForUserAsync(uid);
            return Ok(tags);
        }

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> GetTags([FromQuery] int deptId)
        {
            if (deptId <= 0) return BadRequest(new { Error = "Invalid department id" });

            var tags = await _tagService.GetTagsForDeptAsync(deptId);
            return Ok(tags);
        }
    }
}
