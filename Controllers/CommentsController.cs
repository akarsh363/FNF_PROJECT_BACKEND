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
////    public class CommentsController : ControllerBase
////    {
////    }
////}

//using Microsoft.AspNetCore.Authorization;
//using Microsoft.AspNetCore.Mvc;
//using System.Security.Claims;
//using System.Threading.Tasks;
//using FNF_PROJ.DTOs;
//using FNF_PROJ.Services;

//namespace FNF_PROJ.Controllers
//{
//    [ApiController]
//    [Route("api/[controller]")]
//    public class CommentsController : ControllerBase
//    {
//        private readonly CommentService _commentService;

//        public CommentsController(CommentService commentService)
//        {
//            _commentService = commentService;
//        }

//        private int CurrentUserId()
//        {
//            var id = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
//                  ?? User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value
//                  ?? User.FindFirst("sub")?.Value;
//            return int.TryParse(id, out var v) ? v : 0;
//        }

//        // Create comment (form-data to accept attachments)
//        [HttpPost]
//        [Authorize]
//        public async Task<IActionResult> Create([FromForm] CommentCreateDto dto)
//        {
//            var userId = CurrentUserId();
//            if (userId == 0) return Unauthorized();

//            try
//            {
//                var created = await _commentService.CreateAsync(userId, dto);
//                return Ok(created);
//            }
//            catch (InvalidOperationException ex) { return BadRequest(new { Error = ex.Message }); }
//            catch (UnauthorizedAccessException) { return Forbid(); }
//            catch (System.Exception ex) { return StatusCode(500, new { Error = ex.Message }); }
//        }

//        // Get all comments for a post (nested)
//        [HttpGet("post/{postId:int}")]
//        [AllowAnonymous]
//        public async Task<IActionResult> GetForPost(int postId)
//        {
//            var list = await _commentService.GetForPostAsync(postId);
//            return Ok(list);
//        }

//        // Edit comment - reusing CommentCreateDto for body; only CommentText will be used
//        [HttpPut("{commentId:int}")]
//        [Authorize]
//        public async Task<IActionResult> Edit(int commentId, [FromBody] CommentCreateDto dto)
//        {
//            var userId = CurrentUserId();
//            if (userId == 0) return Unauthorized();

//            try
//            {
//                var updated = await _commentService.EditAsync(userId, commentId, dto);
//                return Ok(updated);
//            }
//            catch (UnauthorizedAccessException) { return Forbid(); }
//            catch (InvalidOperationException ex) { return NotFound(new { Error = ex.Message }); }
//            catch (System.Exception ex) { return StatusCode(500, new { Error = ex.Message }); }
//        }

//        // Delete comment (+ all descendants)
//        [HttpDelete("{commentId:int}")]
//        [Authorize]
//        public async Task<IActionResult> Delete(int commentId)
//        {
//            var userId = CurrentUserId();
//            if (userId == 0) return Unauthorized();

//            try
//            {
//                await _commentService.DeleteAsync(userId, commentId);
//                return NoContent();
//            }
//            catch (UnauthorizedAccessException) { return Forbid(); }
//            catch (InvalidOperationException ex) { return NotFound(new { Error = ex.Message }); }
//            catch (System.Exception ex) { return StatusCode(500, new { Error = ex.Message }); }
//        }
//    }
//}
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Threading.Tasks;
using FNF_PROJ.DTOs;
using FNF_PROJ.Services;

namespace FNF_PROJ.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CommentsController : ControllerBase
    {
        private readonly CommentService _commentService;

        public CommentsController(CommentService commentService)
        {
            _commentService = commentService;
        }

        private int CurrentUserId()
        {
            var id = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                  ?? User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value
                  ?? User.FindFirst("sub")?.Value;
            return int.TryParse(id, out var v) ? v : 0;
        }

        // ✅ Create comment (form-data to accept attachments)
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> Create([FromForm] CommentCreateDto dto)
        {
            var userId = CurrentUserId();
            if (userId == 0) return Unauthorized();

            try
            {
                var created = await _commentService.CreateAsync(userId, dto);
                return Ok(created);
            }
            catch (InvalidOperationException ex) { return BadRequest(new { Error = ex.Message }); }
            catch (UnauthorizedAccessException) { return Forbid(); }
            catch (System.Exception ex) { return StatusCode(500, new { Error = ex.Message }); }
        }

        // ✅ Get all comments for a post (nested with votes)
        [HttpGet("post/{postId:int}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetForPost(int postId)
        {
            int userId = 0;
            if (User.Identity?.IsAuthenticated == true)
            {
                userId = CurrentUserId();
            }

            var list = await _commentService.GetForPostAsync(postId, userId);
            return Ok(list);
        }

        // ✅ Edit comment
        [HttpPut("{commentId:int}")]
        [Authorize]
        public async Task<IActionResult> Edit(int commentId, [FromBody] CommentCreateDto dto)
        {
            var userId = CurrentUserId();
            if (userId == 0) return Unauthorized();

            try
            {
                var updated = await _commentService.EditAsync(userId, commentId, dto);
                return Ok(updated);
            }
            catch (UnauthorizedAccessException) { return Forbid(); }
            catch (InvalidOperationException ex) { return NotFound(new { Error = ex.Message }); }
            catch (System.Exception ex) { return StatusCode(500, new { Error = ex.Message }); }
        }

        // ✅ Delete comment (+ all descendants)
        [HttpDelete("{commentId:int}")]
        [Authorize]
        public async Task<IActionResult> Delete(int commentId)
        {
            var userId = CurrentUserId();
            if (userId == 0) return Unauthorized();

            try
            {
                await _commentService.DeleteAsync(userId, commentId);
                return NoContent();
            }
            catch (UnauthorizedAccessException) { return Forbid(); }
            catch (InvalidOperationException ex) { return NotFound(new { Error = ex.Message }); }
            catch (System.Exception ex) { return StatusCode(500, new { Error = ex.Message }); }
        }
    }
}
