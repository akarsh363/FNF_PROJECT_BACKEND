//////using Microsoft.AspNetCore.Authorization;
//////using Microsoft.AspNetCore.Mvc;
//////using System.Security.Claims;
//////using System.Threading.Tasks;
//////using FNF_PROJ.Services;

//////namespace FNF_PROJ.Controllers
//////{
//////    [ApiController]
//////    [Route("api/[controller]")]
//////    public class TagsController : ControllerBase
//////    {
//////        private readonly ITagService _tagService;

//////        public TagsController(ITagService tagService)
//////        {
//////            _tagService = tagService;
//////        }

//////        // Helper: get logged-in userId from JWT
//////        private int GetCurrentUserId()
//////        {
//////            string? userIdClaim =
//////                User.FindFirst(ClaimTypes.NameIdentifier)?.Value
//////                ?? User.FindFirst("sub")?.Value
//////                ?? User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;

//////            return int.TryParse(userIdClaim, out var id) ? id : 0;
//////        }

//////        // GET: /api/Tags/mine
//////        // Returns tags only for the logged-in user's department
//////        [HttpGet("mine")]
//////        [Authorize]
//////        public async Task<IActionResult> GetMyDeptTags()
//////        {
//////            var uid = GetCurrentUserId();
//////            if (uid == 0) return Unauthorized();

//////            var tags = await _tagService.GetTagsForUserAsync(uid);
//////            return Ok(tags);
//////        }

//////        // (Optional) GET: /api/Tags?deptId=123
//////        // Fetch tags for a given department (can be restricted to managers/admins if needed)
//////        [HttpGet]
//////        [Authorize]
//////        public async Task<IActionResult> GetTags([FromQuery] int deptId)
//////        {
//////            if (deptId <= 0) return BadRequest(new { Error = "Invalid department id" });

//////            var tags = await _tagService.GetTagsForDeptAsync(deptId);
//////            return Ok(tags);
//////        }
//////    }
//////}

////using Microsoft.AspNetCore.Authorization;
////using Microsoft.AspNetCore.Mvc;
////using System.Security.Claims;
////using System.Threading.Tasks;
////using FNF_PROJ.Services;

////namespace FNF_PROJ.Controllers
////{
////    [ApiController]
////    [Route("api/[controller]")]
////    public class TagsController : ControllerBase
////    {
////        private readonly ITagService _tagService;

////        public TagsController(ITagService tagService)
////        {
////            _tagService = tagService;
////        }

////        // Helper: get logged-in userId from JWT (NameIdentifier / sub / jwt sub)
////        private int GetCurrentUserId()
////        {
////            string? userIdClaim =
////                User.FindFirst(ClaimTypes.NameIdentifier)?.Value
////                ?? User.FindFirst("sub")?.Value
////                ?? User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;

////            return int.TryParse(userIdClaim, out var id) ? id : 0;
////        }

////        // GET: /api/Tags/mine
////        // Returns tags only for the logged-in user's department
////        [HttpGet("mine")]
////        [Authorize]
////        public async Task<IActionResult> GetMyDeptTags()
////        {
////            var uid = GetCurrentUserId();
////            if (uid == 0) return Unauthorized();

////            var tags = await _tagService.GetTagsForUserAsync(uid);
////            return Ok(tags);
////        }

////        // Optional: GET /api/Tags?deptId=123
////        // Returns tags for the specified department (can be used by admins)
////        [HttpGet]
////        [Authorize]
////        public async Task<IActionResult> GetTags([FromQuery] int deptId)
////        {
////            if (deptId <= 0) return BadRequest(new { Error = "Invalid department id" });

////            var tags = await _tagService.GetTagsForDeptAsync(deptId);
////            return Ok(tags);
////        }
////    }
////}


//using Microsoft.AspNetCore.Authorization;
//using Microsoft.AspNetCore.Mvc;
//using System.Security.Claims;
//using System.Threading.Tasks;
//using FNF_PROJ.Services;
//using Microsoft.Extensions.Logging;

//namespace FNF_PROJ.Controllers
//{
//    [ApiController]
//    [Route("api/[controller]")]
//    [Authorize] // require auth for all actions
//    public class TagsController : ControllerBase
//    {
//        private readonly ITagService _tagService;
//        private readonly ILogger<TagsController> _logger;

//        public TagsController(ITagService tagService, ILogger<TagsController> logger)
//        {
//            _tagService = tagService;
//            _logger = logger;
//        }

//        // Helper: get logged-in userId from JWT (NameIdentifier / sub / jwt sub)
//        private int GetCurrentUserId()
//        {
//            string? userIdClaim =
//                User.FindFirst(ClaimTypes.NameIdentifier)?.Value
//                ?? User.FindFirst("sub")?.Value
//                ?? User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;

//            return int.TryParse(userIdClaim, out var id) ? id : 0;
//        }

//        /// <summary>
//        /// GET /api/Tags/mine
//        /// Returns tags only for the logged-in user's department
//        /// </summary>
//        [HttpGet("mine")]
//        public async Task<IActionResult> GetMyDeptTags()
//        {
//            var uid = GetCurrentUserId();
//            if (uid == 0)
//            {
//                _logger.LogWarning("GetMyDeptTags called but user id could not be determined from token.");
//                return Unauthorized(new { Error = "Invalid user id in token" });
//            }

//            try
//            {
//                var tags = await _tagService.GetTagsForUserAsync(uid);
//                return Ok(tags);
//            }
//            catch (System.Exception ex)
//            {
//                _logger.LogError(ex, "Error fetching tags for user {UserId}", uid);
//                return StatusCode(500, new { Error = "An error occurred while fetching tags." });
//            }
//        }

//        /// <summary>
//        /// GET /api/Tags?deptId=123
//        /// If deptId is provided and > 0, returns tags for that dept.
//        /// If deptId is omitted or <= 0, returns tags for the current user's department.
//        /// </summary>
//        [HttpGet]
//        public async Task<IActionResult> GetTags([FromQuery] int? deptId)
//        {
//            try
//            {
//                if (deptId.HasValue && deptId.Value > 0)
//                {
//                    var tags = await _tagService.GetTagsForDeptAsync(deptId.Value);
//                    return Ok(tags);
//                }

//                // fallback to current user's dept
//                var uid = GetCurrentUserId();
//                if (uid == 0)
//                {
//                    _logger.LogWarning("GetTags called without deptId and user id could not be determined.");
//                    return Unauthorized(new { Error = "Invalid user id in token" });
//                }

//                var tagsForUser = await _tagService.GetTagsForUserAsync(uid);
//                return Ok(tagsForUser);
//            }
//            catch (System.Exception ex)
//            {
//                _logger.LogError(ex, "Error fetching tags (deptId={DeptId})", deptId);
//                return StatusCode(500, new { Error = "An error occurred while fetching tags." });
//            }
//        }

//        /// <summary>
//        /// GET /api/Tags/{id}
//        /// Returns a single tag by id or 404 if not found.
//        /// </summary>
//        [HttpGet("{id:int}")]
//        public async Task<IActionResult> GetTagById(int id)
//        {
//            if (id <= 0) return BadRequest(new { Error = "Invalid tag id" });

//            try
//            {
//                var list = await _tagService.GetTagsForDeptAsync(0); // placeholder - we need a method to fetch single by id
//                // If TagService doesn't have a single-get method, fetch by dept containing the tag.
//                // Attempt to find the tag across departments (minor perf cost).
//                var tag = (await _tagService.GetTagsForDeptAsync(0)).Find(t => t.TagId == id);

//                // If TagService doesn't support deptId==0, you should add a GetTagByIdAsync to ITagService.
//                if (tag == null)
//                    return NotFound(new { Error = "Tag not found" });

//                return Ok(tag);
//            }
//            catch (System.Exception ex)
//            {
//                _logger.LogError(ex, "Error fetching tag {TagId}", id);
//                return StatusCode(500, new { Error = "An error occurred while fetching the tag." });
//            }
//        }
//    }
//}


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
