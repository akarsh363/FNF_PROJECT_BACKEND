using FNF_PROJ.DTOs;
using FNF_PROJ.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace FNF_PROJ.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class RepostsController : ControllerBase
    {
        private readonly IPostService _postService;

        public RepostsController(IPostService postService)
        {
            _postService = postService;
        }

        // Extract user id from JWT (NameIdentifier / sub)
        private int GetCurrentUserId()
        {
            string? userIdClaim =
                User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? User.FindFirst("sub")?.Value
                ?? User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;

            return int.TryParse(userIdClaim, out var uid) ? uid : 0;
        }

        // POST: /api/Reposts
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> Repost([FromBody] RepostDto dto)
        {
            if (dto == null) return BadRequest(new { Error = "Invalid payload" });

            var currentUserId = GetCurrentUserId();
            if (currentUserId == 0) return Unauthorized();

            try
            {
                var result = await _postService.RepostAsync(currentUserId, dto);
                return Ok(result);
            }
            catch (System.Exception ex)
            {
                if (ex is UnauthorizedAccessException) return Forbid();
                return BadRequest(new { Error = ex.Message });
            }
        }

        // GET: /api/Reposts/{postId}
        [HttpGet("{postId:int}")]
        [Authorize]
        public async Task<IActionResult> GetReposts(int postId)
        {
            try
            {
                var reposts = await _postService.GetRepostsAsync(postId);
                return Ok(reposts);
            }
            catch (System.Exception ex)
            {
                return BadRequest(new { Error = ex.Message });
            }
        }

        // GET: /api/Reposts/mine
        [HttpGet("mine")]
        [Authorize]
        public async Task<IActionResult> GetMyReposts()
        {
            var currentUserId = GetCurrentUserId();
            if (currentUserId == 0) return Unauthorized();

            try
            {
                var myReposts = await _postService.GetRepostsByUserAsync(currentUserId);
                return Ok(myReposts);
            }
            catch (System.Exception ex)
            {
                return BadRequest(new { Error = ex.Message });
            }
        }
    }
}
