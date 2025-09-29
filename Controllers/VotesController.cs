// File: Controllers/VotesController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Security.Claims;
using System.Threading.Tasks;
using FNF_PROJ.DTOs;
using FNF_PROJ.Services;

namespace FNF_PROJ.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class VotesController : ControllerBase
    {
        private readonly VoteService _voteService;
        private readonly ILogger<VotesController> _logger;

        public VotesController(VoteService voteService, ILogger<VotesController> logger)
        {
            _voteService = voteService;
            _logger = logger;
        }

        private int GetCurrentUserId()
        {
            string? userIdClaim =
                User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? User.FindFirst("sub")?.Value
                ?? User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;

            return int.TryParse(userIdClaim, out var uid) ? uid : 0;
        }

        // POST /api/Votes/post/{postId}
        [HttpPost("post/{postId:int}")]
        [Authorize]
        public async Task<IActionResult> VotePost(int postId, [FromBody] VoteRequestDto dto)
        {
            if (dto == null) return BadRequest(new { Error = "Invalid payload" });

            var userId = GetCurrentUserId();
            if (userId == 0) return Unauthorized();

            dto.PostId = postId;
            dto.CommentId = null;

            try
            {
                var result = await _voteService.VoteAsync(userId, dto);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error voting on post {PostId}", postId);
                return BadRequest(new { Error = ex.Message });
            }
        }

        // POST /api/Votes/comment/{commentId}
        [HttpPost("comment/{commentId:int}")]
        [Authorize]
        public async Task<IActionResult> VoteComment(int commentId, [FromBody] VoteRequestDto dto)
        {
            if (dto == null) return BadRequest(new { Error = "Invalid payload" });

            var userId = GetCurrentUserId();
            if (userId == 0) return Unauthorized();

            dto.CommentId = commentId;
            dto.PostId = null;

            try
            {
                var result = await _voteService.VoteAsync(userId, dto);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error voting on comment {CommentId}", commentId);
                return BadRequest(new { Error = ex.Message });
            }
        }
    }
}
