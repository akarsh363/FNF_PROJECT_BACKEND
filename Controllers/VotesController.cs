//// File: Controllers/VotesController.cs
//using Microsoft.AspNetCore.Mvc;
//using Microsoft.AspNetCore.Authorization;
//using Microsoft.EntityFrameworkCore;
//using Microsoft.Extensions.Logging;
//using System;
//using System.Linq;
//using System.Security.Claims;
//using System.Threading.Tasks;
//using FNF_PROJ.Data;     // AppDbContext + Entities
//using FNF_PROJ.DTOs;     // VoteDto

//namespace FNF_PROJ.Controllers
//{
//    [ApiController]
//    [Route("api/[controller]")]
//    public class VotesController : ControllerBase
//    {
//        private readonly AppDbContext _db;
//        private readonly ILogger<VotesController> _logger;

//        public VotesController(AppDbContext db, ILogger<VotesController> logger)
//        {
//            _db = db;
//            _logger = logger;
//        }

//        // helper to extract current userId from token
//        private int GetCurrentUserId()
//        {
//            string? userIdClaim =
//                User.FindFirst(ClaimTypes.NameIdentifier)?.Value
//                ?? User.FindFirst("sub")?.Value
//                ?? User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;

//            return int.TryParse(userIdClaim, out var uid) ? uid : 0;
//        }

//        // Vote on a post
//        [HttpPost("post/{postId:int}")]
//        [Authorize]
//        public async Task<IActionResult> VotePost(int postId, [FromBody] VoteRequestDto dto)
//        {
//            if (dto == null) return BadRequest(new { Error = "Invalid vote data" });

//            var userId = GetCurrentUserId();
//            if (userId == 0) return Unauthorized();

//            dto.PostId = postId;
//            dto.CommentId = null;

//            try
//            {
//                var result = await ProcessVoteAsync(userId, dto);
//                return Ok(result);
//            }
//            catch (Exception ex)
//            {
//                _logger.LogError(ex, "Error voting on post {PostId}", postId);
//                return BadRequest(new { Error = ex.Message });
//            }
//        }

//        // Vote on a comment
//        [HttpPost("comment/{commentId:int}")]
//        [Authorize]
//        public async Task<IActionResult> VoteComment(int commentId, [FromBody] VoteRequestDto dto)
//        {
//            if (dto == null) return BadRequest(new { Error = "Invalid vote data" });

//            var userId = GetCurrentUserId();
//            if (userId == 0) return Unauthorized();

//            dto.CommentId = commentId;
//            dto.PostId = null;

//            try
//            {
//                var result = await ProcessVoteAsync(userId, dto);
//                return Ok(result);
//            }
//            catch (Exception ex)
//            {
//                _logger.LogError(ex, "Error voting on comment {CommentId}", commentId);
//                return BadRequest(new { Error = ex.Message });
//            }
//        }

//        private async Task<object> ProcessVoteAsync(int userId, VoteRequestDto dto)
//        {
//            if (string.IsNullOrWhiteSpace(dto.VoteType))
//                throw new InvalidOperationException("VoteType required");

//            var normalized = dto.VoteType.ToLowerInvariant();
//            if (normalized != "upvote" && normalized != "downvote")
//                throw new InvalidOperationException("VoteType must be 'Upvote' or 'Downvote'");

//            var isUpvote = normalized == "upvote";

//            if (dto.PostId.HasValue)
//            {
//                var post = await _db.Posts.FirstOrDefaultAsync(p => p.PostId == dto.PostId)
//                           ?? throw new InvalidOperationException("Post not found");

//                var existing = await _db.Votes
//                    .FirstOrDefaultAsync(v => v.PostId == dto.PostId && v.UserId == userId);

//                if (existing == null)
//                {
//                    _db.Votes.Add(new Vote
//                    {
//                        PostId = dto.PostId,
//                        UserId = userId,
//                        VoteType = isUpvote ? "Upvote" : "Downvote",
//                        CreatedAt = DateTime.UtcNow
//                    });
//                }
//                else
//                {
//                    if (string.Equals(existing.VoteType, dto.VoteType, StringComparison.OrdinalIgnoreCase))
//                    {
//                        _db.Votes.Remove(existing); // toggle off
//                    }
//                    else
//                    {
//                        existing.VoteType = dto.VoteType;
//                        existing.CreatedAt = DateTime.UtcNow;
//                    }
//                }

//                await _db.SaveChangesAsync();

//                var likeCount = await _db.Votes.CountAsync(v => v.PostId == dto.PostId && v.VoteType == "Upvote");
//                var dislikeCount = await _db.Votes.CountAsync(v => v.PostId == dto.PostId && v.VoteType == "Downvote");

//                return new { PostId = dto.PostId, Likes = likeCount, Dislikes = dislikeCount };
//            }
//            else if (dto.CommentId.HasValue)
//            {
//                var comment = await _db.Comments.FirstOrDefaultAsync(c => c.CommentId == dto.CommentId)
//                              ?? throw new InvalidOperationException("Comment not found");

//                var existing = await _db.Votes
//                    .FirstOrDefaultAsync(v => v.CommentId == dto.CommentId && v.UserId == userId);

//                if (existing == null)
//                {
//                    _db.Votes.Add(new Vote
//                    {
//                        CommentId = dto.CommentId,
//                        UserId = userId,
//                        VoteType = isUpvote ? "Upvote" : "Downvote",
//                        CreatedAt = DateTime.UtcNow
//                    });
//                }
//                else
//                {
//                    if (string.Equals(existing.VoteType, dto.VoteType, StringComparison.OrdinalIgnoreCase))
//                    {
//                        _db.Votes.Remove(existing);
//                    }
//                    else
//                    {
//                        existing.VoteType = dto.VoteType;
//                        existing.CreatedAt = DateTime.UtcNow;
//                    }
//                }

//                await _db.SaveChangesAsync();

//                var likeCount = await _db.Votes.CountAsync(v => v.CommentId == dto.CommentId && v.VoteType == "Upvote");
//                var dislikeCount = await _db.Votes.CountAsync(v => v.CommentId == dto.CommentId && v.VoteType == "Downvote");

//                return new { CommentId = dto.CommentId, Likes = likeCount, Dislikes = dislikeCount };
//            }

//            throw new InvalidOperationException("Either PostId or CommentId must be provided.");
//        }
//    }
//}


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
