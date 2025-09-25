////using FNF_PROJ.DTOs;
////using FNF_PROJ.Services;
////using Microsoft.AspNetCore.Authorization;
////using Microsoft.AspNetCore.Http;
////using Microsoft.AspNetCore.Mvc;
////using System.Security.Claims;

////namespace FNF_PROJ.Controllers
////{
////    [Route("api/[controller]")]
////    [ApiController]
////    public class RepostsController : ControllerBase
////    {
////        private readonly IPostService _postService;

////        public RepostsController(IPostService postService)
////        {
////            _postService = postService;
////        }

////        // Extract user id from JWT (NameIdentifier / sub)
////        private int GetCurrentUserId()
////        {
////            string? userIdClaim =
////                User.FindFirst(ClaimTypes.NameIdentifier)?.Value
////                ?? User.FindFirst("sub")?.Value
////                ?? User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;

////            return int.TryParse(userIdClaim, out var uid) ? uid : 0;
////        }

////        // POST: /api/Reposts
////        // Client may send RepostDto with PostId, UserId, CreatedAt fields.
////        // We ignore client-supplied UserId/CreatedAt and use the authenticated user + server time.
////        [HttpPost]
////        [Authorize]
////        public async Task<IActionResult> Repost([FromBody] RepostDto dto)
////        {
////            if (dto == null) return BadRequest(new { Error = "Invalid payload" });

////            var currentUserId = GetCurrentUserId();
////            if (currentUserId == 0) return Unauthorized();

////            try
////            {
////                // Call service with authenticated user id. Service will set CreatedAt and persist.
////                var result = await _postService.RepostAsync(currentUserId, dto);
////                return Ok(result);
////            }
////            catch (System.Exception ex)
////            {
////                return BadRequest(new { Error = ex.Message });
////            }
////        }

////        // GET: /api/Reposts/{postId}
////        // Returns reposts for a given post.
////        [HttpGet("{postId:int}")]
////        [Authorize]
////        public async Task<IActionResult> GetReposts(int postId)
////        {
////            try
////            {
////                var reposts = await _postService.GetRepostsAsync(postId);
////                return Ok(reposts);
////            }
////            catch (System.Exception ex)
////            {
////                return BadRequest(new { Error = ex.Message });
////            }
////        }

////        // GET: /api/Reposts/mine
////        // Optional: returns reposts made by the current user
////        [HttpGet("mine")]
////        [Authorize]
////        public async Task<IActionResult> GetMyReposts()
////        {
////            var currentUserId = GetCurrentUserId();
////            if (currentUserId == 0) return Unauthorized();

////            try
////            {
////                // Re-use GetRepostsAsync by selecting posts where repost.UserId == currentUserId.
////                // If you want this optimized, we can add a dedicated method in IPostService.
////                // For now call into DB via an ad-hoc query inside service — if not present, implement below.
////                var allPostIds = (await _postService.GetAllPostsAsync()).Select(p => p.PostId).ToList();
////                // Naive approach: iterate and collect reposts for current user
////                var myReposts = new System.Collections.Generic.List<PostResponseDto>();
////                foreach (var pid in allPostIds)
////                {
////                    var reposts = await _postService.GetRepostsAsync(pid);
////                    myReposts.AddRange(reposts.Where(r => r.IsRepost && r.AuthorName != null && r.CreatedAt != default && r.Title != null && r.PostId == pid && /* filter by header user later */ true));
////                }

////                // NOTE: above is a fallback. Prefer adding a service method GetRepostsByUserAsync(userId) for efficiency.
////                return Ok(myReposts);
////            }
////            catch (System.Exception ex)
////            {
////                return BadRequest(new { Error = ex.Message });
////            }
////        }
////    }
////}
//// File: Controllers/RepostsController.cs
//using FNF_PROJ.DTOs;
//using FNF_PROJ.Services;
//using Microsoft.AspNetCore.Authorization;
//using Microsoft.AspNetCore.Mvc;
//using System.Security.Claims;

//namespace FNF_PROJ.Controllers
//{
//    [Route("api/[controller]")]
//    [ApiController]
//    public class RepostsController : ControllerBase
//    {
//        private readonly IPostService _postService;

//        public RepostsController(IPostService postService)
//        {
//            _postService = postService;
//        }

//        // Extract user id from JWT (NameIdentifier / sub)
//        private int GetCurrentUserId()
//        {
//            string? userIdClaim =
//                User.FindFirst(ClaimTypes.NameIdentifier)?.Value
//                ?? User.FindFirst("sub")?.Value
//                ?? User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;

//            return int.TryParse(userIdClaim, out var uid) ? uid : 0;
//        }

//        // POST: /api/Reposts
//        [HttpPost]
//        [Authorize]
//        public async Task<IActionResult> Repost([FromBody] RepostDto dto)
//        {
//            if (dto == null) return BadRequest(new { Error = "Invalid payload" });

//            var currentUserId = GetCurrentUserId();
//            if (currentUserId == 0) return Unauthorized();

//            try
//            {
//                var result = await _postService.RepostAsync(currentUserId, dto);
//                return Ok(result);
//            }
//            catch (System.Exception ex)
//            {
//                if (ex is UnauthorizedAccessException) return Forbid();
//                return BadRequest(new { Error = ex.Message });
//            }
//        }

//        // GET: /api/Reposts/{postId}
//        [HttpGet("{postId:int}")]
//        [Authorize]
//        public async Task<IActionResult> GetReposts(int postId)
//        {
//            try
//            {
//                var reposts = await _postService.GetRepostsAsync(postId);
//                return Ok(reposts);
//            }
//            catch (System.Exception ex)
//            {
//                return BadRequest(new { Error = ex.Message });
//            }
//        }

//        // GET: /api/Reposts/mine
//        [HttpGet("mine")]
//        [Authorize]
//        public async Task<IActionResult> GetMyReposts()
//        {
//            var currentUserId = GetCurrentUserId();
//            if (currentUserId == 0) return Unauthorized();

//            try
//            {
//                var myReposts = await _postService.GetRepostsByUserAsync(currentUserId);
//                return Ok(myReposts);
//            }
//            catch (System.Exception ex)
//            {
//                return BadRequest(new { Error = ex.Message });
//            }
//        }
//    }
//}
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
