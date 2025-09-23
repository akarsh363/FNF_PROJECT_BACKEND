// File: /mnt/data/Controllers/PostsController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Threading.Tasks;
using FNF_PROJ.Services;
using FNF_PROJ.DTOs;
using System.Linq;
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

        // Create post - accepts multipart/form-data
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> CreatePost([FromForm] PostCreateDto dto)
        {
            try
            {
                // If client sent BodyJson form field, prefer that (raw JSON)
                if (Request.Form.ContainsKey("BodyJson"))
                {
                    dto.Body = Request.Form["BodyJson"].FirstOrDefault() ?? dto.Body;
                }
                // Determine userId from JWT sub or nameidentifier
                string? userIdClaim =
                    User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                    ?? User.FindFirst("sub")?.Value
                    ?? User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;

                if (!int.TryParse(userIdClaim, out var userId))
                {
                    return Unauthorized(new { Error = "Invalid user id in token" });
                }

                var created = await _postService.CreatePostAsync(userId, dto);
                return Ok(created);
            }
            catch (System.Exception ex)
            {
                return BadRequest(new { Error = ex.Message });
            }
        }

        // Get all posts
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> GetAll()
        {
            var posts = await _postService.GetAllPostsAsync();
            return Ok(posts);
        }

        // Get by id
        [HttpGet("{id:int}")]
        [Authorize]
        public async Task<IActionResult> GetById(int id)
        {
            var p = await _postService.GetPostByIdAsync(id);
            if (p == null) return NotFound();
            return Ok(p);
        }
    }
}
