// ======================= PostsController.cs =======================
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using FNF_PROJ.Services;
using FNF_PROJ.DTOs;
using System.Security.Claims;

namespace FNF_PROJ.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PostsController : ControllerBase
    {
        private readonly IPostService _postService;

        public PostsController(IPostService postService)
        {
            _postService = postService;
        }

        private int CurrentUserId()
        {
            var id = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                  ?? User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value
                  ?? User.FindFirst("sub")?.Value;
            return int.TryParse(id, out var v) ? v : 0;
        }

        private string CurrentUserRole()
        {
            return User.FindFirst(ClaimTypes.Role)?.Value ?? "Employee";
        }

        private int CurrentDepartmentId()
        {
            var deptId = User.FindFirst("DepartmentId")?.Value;
            return int.TryParse(deptId, out var d) ? d : 0;
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> Create([FromForm] PostCreateDto dto)
        {
            var userId = CurrentUserId();
            if (userId == 0) return Unauthorized();
            var result = await _postService.CreatePostAsync(userId, dto);
            return Ok(result);
        }

        [HttpPut("{postId:int}")]
        [Authorize]
        public async Task<IActionResult> Edit(int postId, [FromForm] PostCreateDto dto)
        {
            var userId = CurrentUserId();
            if (userId == 0) return Unauthorized();
            var role = CurrentUserRole();
            var deptId = CurrentDepartmentId();

            try
            {
                var result = await _postService.EditPostAsync(userId, role, deptId, postId, dto);
                return Ok(result);
            }
            catch (UnauthorizedAccessException) { return Forbid(); }
            catch (InvalidOperationException ex) { return NotFound(new { error = ex.Message }); }
            catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
        }

        [HttpDelete("{postId:int}")]
        [Authorize(Roles = "Manager")]
        public async Task<IActionResult> Delete(int postId)
        {
            var userId = CurrentUserId();
            if (userId == 0) return Unauthorized();
            var deptId = CurrentDepartmentId();

            try
            {
                await _postService.DeletePostAsync(userId, deptId, postId);
                return NoContent();
            }
            catch (UnauthorizedAccessException) { return Forbid(); }
            catch (InvalidOperationException ex) { return NotFound(new { error = ex.Message }); }
            catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
        }

        [HttpPost("repost")]
        [Authorize]
        public async Task<IActionResult> Repost([FromBody] RepostDto dto)
        {
            if (dto == null || dto.PostId <= 0) return BadRequest("Invalid post id");
            var userId = CurrentUserId();
            if (userId == 0) return Unauthorized();

            try
            {
                var result = await _postService.RepostAsync(userId, dto);
                return Ok(result);
            }
            catch (UnauthorizedAccessException) { return Forbid(); }
            catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
            catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
        }

        [HttpGet("{postId:int}/reposts")]
        [AllowAnonymous]
        public async Task<IActionResult> GetReposts(int postId)
        {
            var reposts = await _postService.GetRepostsAsync(postId);
            return Ok(reposts);
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetAll()
        {
            var posts = await _postService.GetAllPostsAsync();
            return Ok(posts);
        }

        [HttpGet("{postId:int}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetById(int postId)
        {
            var post = await _postService.GetPostByIdAsync(postId);
            if (post == null) return NotFound();
            return Ok(post);
        }
    }
}
