//// File: /mnt/data/Controllers/PostsController.cs
//using Microsoft.AspNetCore.Mvc;
//using Microsoft.AspNetCore.Authorization;
//using System.Threading.Tasks;
//using FNF_PROJ.Services;
//using FNF_PROJ.DTOs;
//using System.Linq;
//using System.Security.Claims;

//namespace FNF_PROJ.Controllers
//{
//    [ApiController]
//    [Route("api/[controller]")]
//    public class PostsController : ControllerBase
//    {
//        private readonly IPostService _postService;

//        public PostsController(IPostService postService)
//        {
//            _postService = postService;
//        }

//        // Create post - accepts multipart/form-data
//        [HttpPost]
//        [Authorize]
//        public async Task<IActionResult> CreatePost([FromForm] PostCreateDto dto)
//        {
//            try
//            {
//                // If client sent BodyJson form field, prefer that (raw JSON)
//                if (Request.Form.ContainsKey("BodyJson"))
//                {
//                    dto.Body = Request.Form["BodyJson"].FirstOrDefault() ?? dto.Body;
//                }
//                // Determine userId from JWT sub or nameidentifier
//                string? userIdClaim =
//                    User.FindFirst(ClaimTypes.NameIdentifier)?.Value
//                    ?? User.FindFirst("sub")?.Value
//                    ?? User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;

//                if (!int.TryParse(userIdClaim, out var userId))
//                {
//                    return Unauthorized(new { Error = "Invalid user id in token" });
//                }

//                var created = await _postService.CreatePostAsync(userId, dto);
//                return Ok(created);
//            }
//            catch (System.Exception ex)
//            {
//                return BadRequest(new { Error = ex.Message });
//            }
//        }

//        // Get all posts
//        [HttpGet]
//        [Authorize]
//        public async Task<IActionResult> GetAll()
//        {
//            var posts = await _postService.GetAllPostsAsync();
//            return Ok(posts);
//        }

//        // Get by id
//        [HttpGet("{id:int}")]
//        [Authorize]
//        public async Task<IActionResult> GetById(int id)
//        {
//            var p = await _postService.GetPostByIdAsync(id);
//            if (p == null) return NotFound();
//            return Ok(p);
//        }
//    }
//}


using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Threading.Tasks;
using FNF_PROJ.Services;
using FNF_PROJ.DTOs;
using System.Linq;

namespace FNF_PROJ.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PostsController : ControllerBase
    {
        private readonly IPostService _postService;
        private readonly ILogger<PostsController> _logger;

        public PostsController(IPostService postService, ILogger<PostsController> logger)
        {
            _postService = postService;
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

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> CreatePost([FromForm] PostCreateDto dto)
        {
            try
            {
                // Normalize TagIds if binder failed
                if ((dto.TagIds == null || !dto.TagIds.Any()) && Request.HasFormContentType)
                {
                    var form = Request.Form;
                    var parsed = new List<int>();

                    foreach (var val in form["TagIds"])
                        if (int.TryParse(val, out var id)) parsed.Add(id);

                    foreach (var kv in form.Where(kv => kv.Key.StartsWith("TagIds[")))
                        foreach (var val in kv.Value)
                            if (int.TryParse(val, out var id)) parsed.Add(id);

                    if (form.TryGetValue("TagIds", out var csvVals))
                        foreach (var s in csvVals)
                            foreach (var part in s.Split(',', StringSplitOptions.RemoveEmptyEntries))
                                if (int.TryParse(part.Trim(), out var id)) parsed.Add(id);

                    if (parsed.Any()) dto.TagIds = parsed.Distinct().ToList();
                }

                var uid = GetCurrentUserId();
                if (uid == 0) return Unauthorized(new { Error = "Invalid user id" });

                var created = await _postService.CreatePostAsync(uid, dto);
                return Ok(created);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating post");
                return BadRequest(new { Error = ex.Message });
            }
        }

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> GetAll()
        {
            var posts = await _postService.GetAllPostsAsync();
            return Ok(posts);
        }

        [HttpGet("{id:int}")]
        [Authorize]
        public async Task<IActionResult> GetById(int id)
        {
            var post = await _postService.GetPostByIdAsync(id);
            if (post == null) return NotFound();
            return Ok(post);
        }
    }
}
