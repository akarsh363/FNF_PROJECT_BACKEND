using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Threading.Tasks;
using FNF_PROJ.Services;
using FNF_PROJ.DTOs;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Linq;

namespace FNF_PROJ.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;

        public AuthController(IAuthService authService)
        {
            _authService = authService;
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
            catch (System.Exception ex)
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
            catch (System.Exception ex)
            {
                return Unauthorized(new { Error = ex.Message });
            }
        }

        [HttpGet("me")]
        [Authorize]
        public IActionResult Me()
        {
            // Try common claim types in order of preference
            string? userId =
                User.FindFirst(ClaimTypes.NameIdentifier)?.Value     // "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier"
                ?? User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value // "sub"
                ?? User.FindFirst("sub")?.Value                       // sometimes stored as "sub" plain
                ?? User.FindFirst("id")?.Value                        // some tokens use "id"
                ?? User.FindFirst("userId")?.Value;                   // custom key

            // Name / email / role
            var fullName = User.FindFirst(ClaimTypes.Name)?.Value ?? User.Identity?.Name;
            var email = User.FindFirst(ClaimTypes.Email)?.Value;
            var role = User.FindFirst(ClaimTypes.Role)?.Value;

            // If userId still null, return all claims (helpful for debugging)
            if (string.IsNullOrEmpty(userId))
            {
                var claims = User.Claims.Select(c => new { c.Type, c.Value }).ToList();
                return Ok(new { userId = (string?)null, fullName, email, role, claims });
            }

            return Ok(new { userId, fullName, email, role });
        }
    }
}
