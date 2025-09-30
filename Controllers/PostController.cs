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
        // Primary DELETE endpoint: reads ?reason=... OR attempts to read JSON/form body fallback.
        [HttpDelete("{id:int}")]
        [Authorize]
        public async Task<IActionResult> DeletePost(int id, [FromQuery] string? reason = null)
        {
            try
            {
                // If reason not provided in query, try to read it from body (JSON or form)
                if (string.IsNullOrWhiteSpace(reason) && Request.ContentLength > 0)
                {
                    try
                    {
                        using var reader = new System.IO.StreamReader(Request.Body);
                        var bodyText = await reader.ReadToEndAsync();
                        if (!string.IsNullOrWhiteSpace(bodyText))
                        {
                            // Try JSON parse first
                            try
                            {
                                using var doc = System.Text.Json.JsonDocument.Parse(bodyText);
                                if (doc.RootElement.TryGetProperty("reason", out var rEl) && rEl.ValueKind == System.Text.Json.JsonValueKind.String)
                                {
                                    reason = rEl.GetString();
                                }
                            }
                            catch
                            {
                                // If not JSON, try simple form-style parsing (e.g., reason=...)
                                if (bodyText.Contains("="))
                                {
                                    var kv = bodyText.Split('&').Select(part =>
                                    {
                                        var i = part.IndexOf('=');
                                        return i > -1 ? new { Key = part.Substring(0, i), Value = part.Substring(i + 1) } : null;
                                    }).Where(x => x != null).ToList();

                                    var found = kv.FirstOrDefault(x => string.Equals(x.Key, "reason", StringComparison.OrdinalIgnoreCase));
                                    if (found != null) reason = System.Net.WebUtility.UrlDecode(found.Value);
                                }
                            }
                        }
                    }
                    catch
                    {
                        // ignore body read errors; proceed with query value (may be null)
                    }
                }

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

        // Alternative endpoint for clients that prefer POST with JSON body
        [HttpPost("{id:int}/delete")]
        [Authorize]
        public async Task<IActionResult> DeletePostViaPost(int id, [FromBody] DeleteRequestDto? body)
        {
            var reason = body?.Reason ?? "";
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
                _logger.LogError(ex, "Error deleting post via POST {PostId}", id);
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