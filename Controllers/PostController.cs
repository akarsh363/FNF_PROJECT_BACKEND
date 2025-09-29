//////////using Microsoft.AspNetCore.Authorization;
//////////using Microsoft.AspNetCore.Mvc;
//////////using System.Security.Claims;
//////////using System.Threading.Tasks;
//////////using FNF_PROJ.Services;
//////////using FNF_PROJ.DTOs;
//////////using System.Linq;

//////////namespace FNF_PROJ.Controllers
//////////{
//////////    [ApiController]
//////////    [Route("api/[controller]")]
//////////    public class PostsController : ControllerBase
//////////    {
//////////        private readonly IPostService _postService;
//////////        private readonly ILogger<PostsController> _logger;

//////////        public PostsController(IPostService postService, ILogger<PostsController> logger)
//////////        {
//////////            _postService = postService;
//////////            _logger = logger;
//////////        }

//////////        private int GetCurrentUserId()
//////////        {
//////////            string? userIdClaim =
//////////                User.FindFirst(ClaimTypes.NameIdentifier)?.Value
//////////                ?? User.FindFirst("sub")?.Value
//////////                ?? User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;

//////////            return int.TryParse(userIdClaim, out var uid) ? uid : 0;
//////////        }

//////////        [HttpPost]
//////////        [Authorize]
//////////        public async Task<IActionResult> CreatePost([FromForm] PostCreateDto dto)
//////////        {
//////////            try
//////////            {
//////////                // Normalize TagIds if binder failed
//////////                if ((dto.TagIds == null || !dto.TagIds.Any()) && Request.HasFormContentType)
//////////                {
//////////                    var form = Request.Form;
//////////                    var parsed = new List<int>();

//////////                    foreach (var val in form["TagIds"])
//////////                        if (int.TryParse(val, out var id)) parsed.Add(id);

//////////                    foreach (var kv in form.Where(kv => kv.Key.StartsWith("TagIds[")))
//////////                        foreach (var val in kv.Value)
//////////                            if (int.TryParse(val, out var id)) parsed.Add(id);

//////////                    if (form.TryGetValue("TagIds", out var csvVals))
//////////                        foreach (var s in csvVals)
//////////                            foreach (var part in s.Split(',', StringSplitOptions.RemoveEmptyEntries))
//////////                                if (int.TryParse(part.Trim(), out var id)) parsed.Add(id);

//////////                    if (parsed.Any()) dto.TagIds = parsed.Distinct().ToList();
//////////                }

//////////                var uid = GetCurrentUserId();
//////////                if (uid == 0) return Unauthorized(new { Error = "Invalid user id" });

//////////                var created = await _postService.CreatePostAsync(uid, dto);
//////////                return Ok(created);
//////////            }
//////////            catch (Exception ex)
//////////            {
//////////                _logger.LogError(ex, "Error creating post");
//////////                return BadRequest(new { Error = ex.Message });
//////////            }
//////////        }

//////////        [HttpGet]
//////////        [Authorize]
//////////        public async Task<IActionResult> GetAll()
//////////        {
//////////            var posts = await _postService.GetAllPostsAsync();
//////////            return Ok(posts);
//////////        }

//////////        [HttpGet("{id:int}")]
//////////        [Authorize]
//////////        public async Task<IActionResult> GetById(int id)
//////////        {
//////////            var post = await _postService.GetPostByIdAsync(id);
//////////            if (post == null) return NotFound();
//////////            return Ok(post);
//////////        }
//////////    }
//////////}


////////using Microsoft.AspNetCore.Authorization;
////////using Microsoft.AspNetCore.Mvc;
////////using System.Security.Claims;
////////using System.Threading.Tasks;
////////using FNF_PROJ.Services;
////////using FNF_PROJ.DTOs;
////////using System.Linq;

////////namespace FNF_PROJ.Controllers
////////{
////////    [ApiController]
////////    [Route("api/[controller]")]
////////    public class PostsController : ControllerBase
////////    {
////////        private readonly IPostService _postService;
////////        private readonly ILogger<PostsController> _logger;

////////        public PostsController(IPostService postService, ILogger<PostsController> logger)
////////        {
////////            _postService = postService;
////////            _logger = logger;
////////        }

////////        private int GetCurrentUserId()
////////        {
////////            string? userIdClaim =
////////                User.FindFirst(ClaimTypes.NameIdentifier)?.Value
////////                ?? User.FindFirst("sub")?.Value
////////                ?? User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;

////////            return int.TryParse(userIdClaim, out var uid) ? uid : 0;
////////        }

////////        [HttpPost]
////////        [Authorize]
////////        public async Task<IActionResult> CreatePost([FromForm] PostCreateDto dto)
////////        {
////////            try
////////            {
////////                // Normalize TagIds if binder failed
////////                if ((dto.TagIds == null || !dto.TagIds.Any()) && Request.HasFormContentType)
////////                {
////////                    var form = Request.Form;
////////                    var parsed = new List<int>();

////////                    foreach (var val in form["TagIds"])
////////                        if (int.TryParse(val, out var id)) parsed.Add(id);

////////                    foreach (var kv in form.Where(kv => kv.Key.StartsWith("TagIds[")))
////////                        foreach (var val in kv.Value)
////////                            if (int.TryParse(val, out var id)) parsed.Add(id);

////////                    if (form.TryGetValue("TagIds", out var csvVals))
////////                        foreach (var s in csvVals)
////////                            foreach (var part in s.Split(',', StringSplitOptions.RemoveEmptyEntries))
////////                                if (int.TryParse(part.Trim(), out var id)) parsed.Add(id);

////////                    if (parsed.Any()) dto.TagIds = parsed.Distinct().ToList();
////////                }

////////                var uid = GetCurrentUserId();
////////                if (uid == 0) return Unauthorized(new { Error = "Invalid user id" });

////////                var created = await _postService.CreatePostAsync(uid, dto);
////////                return Ok(created);
////////            }
////////            catch (Exception ex)
////////            {
////////                _logger.LogError(ex, "Error creating post");
////////                return BadRequest(new { Error = ex.Message });
////////            }
////////        }

////////        [HttpGet]
////////        [Authorize]
////////        public async Task<IActionResult> GetAll()
////////        {
////////            var posts = await _postService.GetAllPostsAsync();
////////            return Ok(posts);
////////        }

////////        [HttpGet("{id:int}")]
////////        [Authorize]
////////        public async Task<IActionResult> GetById(int id)
////////        {
////////            var post = await _postService.GetPostByIdAsync(id);
////////            if (post == null) return NotFound();
////////            return Ok(post);
////////        }

////////        [HttpGet("mine")]
////////        [Authorize]
////////        public async Task<IActionResult> GetMine()
////////        {
////////            var uid = GetCurrentUserId();
////////            if (uid == 0) return Unauthorized();
////////            var posts = await _postService.GetMyPostsAsync(uid);
////////            return Ok(posts);
////////        }

////////        public class DeletePostRequest { public string Reason { get; set; } = ""; }

////////        [HttpPut("{id:int}")]
////////        [Authorize]
////////        public async Task<IActionResult> Edit(int id, [FromForm] PostCreateDto dto)
////////        {
////////            try
////////            {
////////                var uid = GetCurrentUserId();
////////                if (uid == 0) return Unauthorized();

////////                var role = User.FindFirst("role")?.Value ?? User.FindFirst(ClaimTypes.Role)?.Value ?? "Employee";
////////                var deptIdStr = User.FindFirst("deptId")?.Value;
////////                int.TryParse(deptIdStr, out var deptIdClaim);

////////                var result = await _postService.EditPostAsync(uid, role, deptIdClaim, id, dto);
////////                return Ok(result);
////////            }
////////            catch (UnauthorizedAccessException)
////////            {
////////                return Forbid();
////////            }
////////            catch (Exception ex)
////////            {
////////                _logger.LogError(ex, "Error editing post {Id}", id);
////////                return BadRequest(new { Error = ex.Message });
////////            }
////////        }

////////        [HttpDelete("{id:int}")]
////////        [Authorize]
////////        public async Task<IActionResult> Delete(int id, [FromBody] DeletePostRequest req)
////////        {
////////            try
////////            {
////////                var uid = GetCurrentUserId();
////////                if (uid == 0) return Unauthorized();

////////                var deptIdStr = User.FindFirst("deptId")?.Value;
////////                int.TryParse(deptIdStr, out var deptId);

////////                await _postService.DeletePostAsync(uid, deptId, id, req?.Reason ?? "");
////////                return NoContent();
////////            }
////////            catch (UnauthorizedAccessException)
////////            {
////////                return Forbid();
////////            }
////////            catch (Exception ex)
////////            {
////////                _logger.LogError(ex, "Delete failed for post {Id}", id);
////////                return BadRequest(new { Error = ex.Message });
////////            }
////////        }
////////    }
////////}


//////// ...existing usings...
//////using FNF_PROJ.Data;
//////using FNF_PROJ.Services;
//////using Microsoft.AspNetCore.Authorization;
//////using Microsoft.AspNetCore.Mvc;
//////using System.Security.Claims;

//////namespace FNF_PROJ.Controllers
//////{
//////    [ApiController]
//////    [Route("api/[controller]")]
//////    public class PostsController : ControllerBase
//////    {
//////        private readonly IPostService _postService;
//////        private readonly ILogger<PostsController> _logger;

//////        public PostsController(IPostService postService, ILogger<PostsController> logger)
//////        {
//////            _postService = postService;
//////            _logger = logger;
//////        }

//////        private int GetCurrentUserId()
//////        {
//////            string? userIdClaim =
//////                User.FindFirst(ClaimTypes.NameIdentifier)?.Value
//////                ?? User.FindFirst("sub")?.Value
//////                ?? User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;

//////            return int.TryParse(userIdClaim, out var uid) ? uid : 0;
//////        }

//////        // ... CreatePost, GetAll, GetById, GetMine same as before ...

//////        public class DeletePostRequest { public string Reason { get; set; } = ""; }

//////        [HttpDelete("{id:int}")]
//////        [Authorize]
//////        public async Task<IActionResult> Delete(int id, [FromBody] DeletePostRequest req)
//////        {
//////            try
//////            {
//////                var uid = GetCurrentUserId();
//////                if (uid == 0) return Unauthorized();

//////                var deptIdStr = User.FindFirst("deptId")?.Value;
//////                int.TryParse(deptIdStr, out var deptId);

//////                await _postService.DeletePostAsync(uid, deptId, id, req?.Reason ?? "");
//////                return NoContent();
//////            }
//////            catch (UnauthorizedAccessException)
//////            {
//////                return Forbid();
//////            }
//////            catch (Exception ex)
//////            {
//////                _logger.LogError(ex, "Delete failed for post {Id}", id);
//////                return BadRequest(new { Error = ex.Message });
//////            }
//////        }
//////    }
//////}



////using Microsoft.AspNetCore.Authorization;
////using Microsoft.AspNetCore.Mvc;
////using System.Security.Claims;
////using System.Threading.Tasks;
////using FNF_PROJ.Services;
////using FNF_PROJ.DTOs;
////using System.Linq;

////namespace FNF_PROJ.Controllers
////{
////    [ApiController]
////    [Route("api/[controller]")]
////    public class PostsController : ControllerBase
////    {
////        private readonly IPostService _postService;
////        private readonly ILogger<PostsController> _logger;

////        public PostsController(IPostService postService, ILogger<PostsController> logger)
////        {
////            _postService = postService;
////            _logger = logger;
////        }

////        private int GetCurrentUserId()
////        {
////            string? userIdClaim =
////                User.FindFirst(ClaimTypes.NameIdentifier)?.Value
////                ?? User.FindFirst("sub")?.Value
////                ?? User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;

////            return int.TryParse(userIdClaim, out var uid) ? uid : 0;
////        }

////        [HttpPost]
////        [Authorize]
////        [Consumes("multipart/form-data")]
////        public async Task<IActionResult> CreatePost([FromForm] PostCreateDto dto)
////        {
////            try
////            {
////                if ((dto.TagIds == null || !dto.TagIds.Any()) && Request.HasFormContentType)
////                {
////                    var form = Request.Form;
////                    var parsed = new List<int>();

////                    foreach (var val in form["TagIds"])
////                        if (int.TryParse(val, out var id)) parsed.Add(id);

////                    foreach (var kv in form.Where(kv => kv.Key.StartsWith("TagIds[")))
////                        foreach (var val in kv.Value)
////                            if (int.TryParse(val, out var id)) parsed.Add(id);

////                    if (form.TryGetValue("TagIds", out var csvVals))
////                        foreach (var s in csvVals)
////                            foreach (var part in s.Split(',', StringSplitOptions.RemoveEmptyEntries))
////                                if (int.TryParse(part.Trim(), out var id)) parsed.Add(id);

////                    if (parsed.Any()) dto.TagIds = parsed.Distinct().ToList();
////                }

////                var uid = GetCurrentUserId();
////                if (uid == 0) return Unauthorized(new { Error = "Invalid user id" });

////                var created = await _postService.CreatePostAsync(uid, dto);
////                return Ok(created);
////            }
////            catch (Exception ex)
////            {
////                _logger.LogError(ex, "Error creating post");
////                return BadRequest(new { Error = ex.Message });
////            }
////        }

////        [HttpGet]
////        [Authorize]
////        public async Task<IActionResult> GetAll()
////        {
////            var posts = await _postService.GetAllPostsAsync();
////            return Ok(posts);
////        }

////        [HttpGet("{id:int}")]
////        [Authorize]
////        public async Task<IActionResult> GetById(int id)
////        {
////            var post = await _postService.GetPostByIdAsync(id);
////            if (post == null) return NotFound();
////            return Ok(post);
////        }

////        [HttpGet("mine")]
////        [Authorize]
////        public async Task<IActionResult> GetMine()
////        {
////            var uid = GetCurrentUserId();
////            if (uid == 0) return Unauthorized();
////            var posts = await _postService.GetMyPostsAsync(uid);
////            return Ok(posts);
////        }

////        public class DeletePostRequest { public string Reason { get; set; } = ""; }

////        [HttpPut("{id:int}")]
////        [Authorize]
////        [Consumes("multipart/form-data")]
////        public async Task<IActionResult> Edit(int id, [FromForm] PostCreateDto dto)
////        {
////            try
////            {
////                var uid = GetCurrentUserId();
////                if (uid == 0) return Unauthorized();

////                var role = User.FindFirst("role")?.Value ?? User.FindFirst(ClaimTypes.Role)?.Value ?? "Employee";
////                var deptIdStr = User.FindFirst("deptId")?.Value;
////                int.TryParse(deptIdStr, out var deptIdClaim);

////                var result = await _postService.EditPostAsync(uid, role, deptIdClaim, id, dto);
////                return Ok(result);
////            }
////            catch (UnauthorizedAccessException)
////            {
////                return Forbid();
////            }
////            catch (Exception ex)
////            {
////                _logger.LogError(ex, "Error editing post {Id}", id);
////                return BadRequest(new { Error = ex.Message });
////            }
////        }

////        [HttpDelete("{id:int}")]
////        [Authorize]
////        [Consumes("application/json", "application/*+json", "text/json", "application/x-www-form-urlencoded", "multipart/form-data")]
////        public async Task<IActionResult> Delete(int id, [FromBody] DeletePostRequest? req)
////        {
////            try
////            {
////                var uid = GetCurrentUserId();
////                if (uid == 0) return Unauthorized();

////                // gather reason from JSON body or fallback to query/form
////                string? reason = req?.Reason;
////                if (string.IsNullOrWhiteSpace(reason))
////                {
////                    if (Request.Query.TryGetValue("reason", out var qv) && !string.IsNullOrWhiteSpace(qv))
////                        reason = qv.ToString();
////                }
////                if (string.IsNullOrWhiteSpace(reason) && Request.HasFormContentType)
////                {
////                    var form = await Request.ReadFormAsync();
////                    if (form.TryGetValue("reason", out var fv) && !string.IsNullOrWhiteSpace(fv))
////                        reason = fv.ToString();
////                }
////                if (string.IsNullOrWhiteSpace(reason))
////                    return BadRequest(new { Error = "Delete reason is required." });

////                var deptIdStr = User.FindFirst("deptId")?.Value;
////                int.TryParse(deptIdStr, out var deptId);

////                await _postService.DeletePostAsync(uid, deptId, id, reason);
////                return NoContent();
////            }
////            catch (UnauthorizedAccessException)
////            {
////                return Forbid();
////            }
////            catch (Exception ex)
////            {
////                _logger.LogError(ex, "Delete failed for post {Id}", id);
////                return BadRequest(new { Error = ex.Message });
////            }
////        }
////    }
////}


//using Microsoft.AspNetCore.Authorization;
//using Microsoft.AspNetCore.Mvc;
//using System.Security.Claims;
//using FNF_PROJ.Services;
//using FNF_PROJ.DTOs;

//namespace FNF_PROJ.Controllers
//{
//    [ApiController]
//    [Route("api/[controller]")]
//    public class PostsController : ControllerBase
//    {
//        private readonly IPostService _postService;
//        private readonly ILogger<PostsController> _logger;

//        public PostsController(IPostService postService, ILogger<PostsController> logger)
//        {
//            _postService = postService;
//            _logger = logger;
//        }

//        private int GetCurrentUserId()
//        {
//            string? userIdClaim =
//                User.FindFirst(ClaimTypes.NameIdentifier)?.Value
//                ?? User.FindFirst("sub")?.Value
//                ?? User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;

//            return int.TryParse(userIdClaim, out var uid) ? uid : 0;
//        }

//        [HttpPost]
//        [Authorize]
//        [Consumes("multipart/form-data")]
//        public async Task<IActionResult> CreatePost([FromForm] PostCreateDto dto)
//        {
//            try
//            {
//                // normalize TagIds if needed
//                if ((dto.TagIds == null || !dto.TagIds.Any()) && Request.HasFormContentType)
//                {
//                    var form = Request.Form;
//                    var parsed = new List<int>();

//                    foreach (var val in form["TagIds"])
//                        if (int.TryParse(val, out var id)) parsed.Add(id);

//                    foreach (var kv in form.Where(kv => kv.Key.StartsWith("TagIds[")))
//                        foreach (var val in kv.Value)
//                            if (int.TryParse(val, out var id)) parsed.Add(id);

//                    if (form.TryGetValue("TagIds", out var csvVals))
//                        foreach (var s in csvVals)
//                            foreach (var part in s.Split(',', StringSplitOptions.RemoveEmptyEntries))
//                                if (int.TryParse(part.Trim(), out var id)) parsed.Add(id);

//                    if (parsed.Any()) dto.TagIds = parsed.Distinct().ToList();
//                }

//                var uid = GetCurrentUserId();
//                if (uid == 0) return Unauthorized(new { Error = "Invalid user id" });

//                var created = await _postService.CreatePostAsync(uid, dto);
//                return Ok(created);
//            }
//            catch (Exception ex)
//            {
//                _logger.LogError(ex, "Error creating post");
//                return BadRequest(new { Error = ex.Message });
//            }
//        }

//        [HttpGet]
//        [Authorize]
//        public async Task<IActionResult> GetAll()
//        {
//            var posts = await _postService.GetAllPostsAsync();
//            return Ok(posts);
//        }

//        [HttpGet("{id:int}")]
//        [Authorize]
//        public async Task<IActionResult> GetById(int id)
//        {
//            var post = await _postService.GetPostByIdAsync(id);
//            if (post == null) return NotFound();
//            return Ok(post);
//        }

//        [HttpGet("mine")]
//        [Authorize]
//        public async Task<IActionResult> GetMine()
//        {
//            var uid = GetCurrentUserId();
//            if (uid == 0) return Unauthorized();
//            var posts = await _postService.GetPostByIdAsync(uid);
//            return Ok(posts);
//        }

//        public class DeletePostRequest { public string Reason { get; set; } = ""; }

//        [HttpPut("{id:int}")]
//        [Authorize]
//        [Consumes("multipart/form-data")]
//        public async Task<IActionResult> Edit(int id, [FromForm] PostCreateDto dto)
//        {
//            try
//            {
//                var uid = GetCurrentUserId();
//                if (uid == 0) return Unauthorized();

//                var role = User.FindFirst("role")?.Value ?? User.FindFirst(ClaimTypes.Role)?.Value ?? "Employee";
//                var deptIdStr = User.FindFirst("deptId")?.Value;
//                int.TryParse(deptIdStr, out var deptIdClaim);

//                var result = await _postService.EditPostAsync(uid, role, deptIdClaim, id, dto);
//                return Ok(result);
//            }
//            catch (UnauthorizedAccessException)
//            {
//                return Forbid();
//            }
//            catch (Exception ex)
//            {
//                _logger.LogError(ex, "Error editing post {Id}", id);
//                return BadRequest(new { Error = ex.Message });
//            }
//        }

//        [HttpDelete("{id:int}")]
//        [Authorize]
//        [Consumes("application/json", "application/*+json", "text/json", "application/x-www-form-urlencoded", "multipart/form-data")]
//        public async Task<IActionResult> Delete(int id, [FromBody] DeletePostRequest? req)
//        {
//            try
//            {
//                var uid = GetCurrentUserId();
//                if (uid == 0) return Unauthorized();

//                // try to read reason from JSON, then query, then form
//                string? reason = req?.Reason;
//                if (string.IsNullOrWhiteSpace(reason) && Request.Query.TryGetValue("reason", out var qv) && !string.IsNullOrWhiteSpace(qv))
//                    reason = qv.ToString();

//                if (string.IsNullOrWhiteSpace(reason) && Request.HasFormContentType)
//                {
//                    var form = await Request.ReadFormAsync();
//                    if (form.TryGetValue("reason", out var fv) && !string.IsNullOrWhiteSpace(fv))
//                        reason = fv.ToString();
//                }

//                if (string.IsNullOrWhiteSpace(reason))
//                    return BadRequest(new { Error = "Delete reason is required." });    

//                var deptIdStr = User.FindFirst("deptId")?.Value;
//                int.TryParse(deptIdStr, out var deptId);

//                await _postService.DeletePostAsync(uid, deptId, id, reason);
//                return NoContent();
//            }
//            catch (UnauthorizedAccessException)
//            {
//                return Forbid();
//            }
//            catch (Exception ex)
//            {
//                _logger.LogError(ex, "Delete failed for post {Id}", id);
//                return BadRequest(new { Error = ex.Message });
//            }
//        }
//    }
//}


using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FNF_PROJ.DTOs;
using FNF_PROJ.Services;

namespace FNF_PROJ.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PostsController : ControllerBase
    {
        private readonly IPostService _postService;
        private readonly ILogger<PostsController> _logger;
        private readonly FNF_PROJ.Data.AppDbContext _db;

        public PostsController(
            IPostService postService,
            ILogger<PostsController> logger,
            FNF_PROJ.Data.AppDbContext db)
        {
            _postService = postService;
            _logger = logger;
            _db = db;
        }

        private int GetCurrentUserId()
        {
            string? userIdClaim =
                User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? User.FindFirst("sub")?.Value
                ?? User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;

            return int.TryParse(userIdClaim, out var uid) ? uid : 0;
        }

        // -------------------- CREATE --------------------
        [HttpPost]
        [Authorize]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> CreatePost([FromForm] PostCreateDto dto)
        {
            try
            {
                NormalizeTagIdsFromForm(dto);

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

        // -------------------- EDIT (incl. tag updates) --------------------
        [HttpPut("{id:int}")]
        [Authorize]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> EditPost(int id, [FromForm] PostCreateDto dto)
        {
            try
            {
                NormalizeTagIdsFromForm(dto);

                var uid = GetCurrentUserId();
                if (uid == 0) return Unauthorized(new { Error = "Invalid user id" });

                var me = await _db.Users.FindAsync(uid);
                if (me == null) return Unauthorized(new { Error = "User not found" });

                var updated = await _postService.EditPostAsync(
                    uid,
                    me.Role ?? "Employee",
                    me.DepartmentId,
                    id,
                    dto
                );

                return Ok(updated);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error editing post {PostId}", id);
                if (ex is UnauthorizedAccessException) return Forbid();
                return BadRequest(new { Error = ex.Message });
            }
        }

        // -------------------- LIST/GET --------------------
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

        [HttpGet("mine")]
        [Authorize]
        public async Task<IActionResult> GetMine()
        {
            var uid = GetCurrentUserId();
            if (uid == 0) return Unauthorized();
            var list = await _postService.GetPostsByUserAsync(uid);
            return Ok(list);
        }

        // -------------------- DELETE (Manager only, with reason) --------------------
        // Send reason as query (?reason=...) or body { "reason": "..." } — we read query first.
        [HttpDelete("{id:int}")]
        [Authorize]
        public async Task<IActionResult> DeletePost(int id, [FromQuery] string? reason = null)
        {
            try
            {
                var uid = GetCurrentUserId();
                if (uid == 0) return Unauthorized(new { Error = "Invalid user id" });

                var me = await _db.Users.FindAsync(uid);
                if (me == null) return Unauthorized(new { Error = "User not found" });

                await _postService.DeletePostAsync(uid, me.DepartmentId, id, reason ?? "");
                return Ok(new { Deleted = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting post {PostId}", id);
                if (ex is UnauthorizedAccessException) return Forbid();
                return BadRequest(new { Error = ex.Message });
            }
        }

        // -------------------- Helpers --------------------
        private void NormalizeTagIdsFromForm(PostCreateDto dto)
        {
            // If MVC model binder didn’t bind TagIds correctly from multipart, parse manually.
            if ((dto.TagIds == null || !dto.TagIds.Any()) && Request.HasFormContentType)
            {
                var form = Request.Form;
                var parsed = new List<int>();

                foreach (var val in form["TagIds"])
                    if (int.TryParse(val, out var tid)) parsed.Add(tid);

                foreach (var kv in form.Where(kv => kv.Key.StartsWith("TagIds[")))
                    foreach (var val in kv.Value)
                        if (int.TryParse(val, out var tid)) parsed.Add(tid);

                if (form.TryGetValue("TagIds", out var csvVals))
                    foreach (var s in csvVals)
                        foreach (var part in s.Split(',', StringSplitOptions.RemoveEmptyEntries))
                            if (int.TryParse(part.Trim(), out var tid)) parsed.Add(tid);

                if (parsed.Any()) dto.TagIds = parsed.Distinct().ToList();
            }
        }
    }
}

