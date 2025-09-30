using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Threading.Tasks;
using FNF_PROJ.Services;
using FNF_PROJ.DTOs;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Linq;
using System;
using FNF_PROJ.Data; // <-- added

namespace FNF_PROJ.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly AppDbContext _db; // <-- added

        public AuthController(IAuthService authService, AppDbContext db) // <-- inject db
        {
            _authService = authService;
            _db = db;
        }

        [HttpPost("register")]
        [AllowAnonymous]
        public async Task<IActionResult> Register([FromForm] UserRegisterDto dto) // [FromForm] for file upload
        {
            try
            {
                // Service will enforce Role = "Employee"
                var token = await _authService.RegisterAsync(dto);
                return Ok(new { Token = token });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Error = ex.Message });
            }
        }

        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            try
            {
                var token = await _authService.LoginAsync(dto);
                return Ok(new { Token = token });
            }
            catch (Exception ex)
            {
                return Unauthorized(new { Error = ex.Message });
            }
        }

        [HttpGet("me")]
        [Authorize]
        public IActionResult Me()
        {
            // Try common claim types in order of preference
            string? userIdStr =
                User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                ?? User.FindFirst("sub")?.Value
                ?? User.FindFirst("id")?.Value
                ?? User.FindFirst("userId")?.Value;

            var fullName = User.FindFirst(ClaimTypes.Name)?.Value ?? User.Identity?.Name;
            var email = User.FindFirst(ClaimTypes.Email)?.Value;
            var role = User.FindFirst(ClaimTypes.Role)?.Value;

            if (!int.TryParse(userIdStr, out var uid) || uid <= 0)
            {
                // Helpful when token is malformed
                var claims = User.Claims.Select(c => new { c.Type, c.Value }).ToList();
                return Ok(new { userId = (int?)null, fullName, email, role, DepartmentId = (int?)null, claims });
            }

            // Load minimal user shape from DB so we can include DepartmentId and ProfilePicture
            var me = _db.Users
                .Where(u => u.UserId == uid)
                .Select(u => new
                {
                    userId = u.UserId,
                    fullName = u.FullName,
                    email = u.Email,
                    role = u.Role,
                    DepartmentId = u.DepartmentId,
                    ProfilePicture = u.ProfilePicture // include stored filename or path
                })
                .FirstOrDefault();

            if (me == null)
            {
                // Fallback: if somehow not found, at least return identity info
                return Ok(new { userId = uid, fullName, email, role, DepartmentId = (int?)null });
            }

            // Compose a convenient fully-qualified URL if ProfilePicture looks like a filename (not starting with / or http)
            string? profilePictureUrl = null;
            if (!string.IsNullOrWhiteSpace(me.ProfilePicture))
            {
                var pp = me.ProfilePicture.Trim();

                if (pp.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                    pp.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                    pp.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                {
                    profilePictureUrl = pp; // already absolute
                }
                else if (pp.StartsWith("/"))
                {
                    // root relative
                    profilePictureUrl = $"{Request.Scheme}://{Request.Host}{pp}";
                }
                else
                {
                    // Common layout: files stored in wwwroot/profiles/<filename>
                    profilePictureUrl = $"{Request.Scheme}://{Request.Host}/profiles/{pp}";
                }
            }

            return Ok(new
            {
                userId = me.userId,
                fullName = me.fullName,
                email = me.email,
                role = me.role,
                DepartmentId = me.DepartmentId,
                ProfilePicture = me.ProfilePicture,
                ProfilePictureUrl = profilePictureUrl
            });
        }
    }
}
