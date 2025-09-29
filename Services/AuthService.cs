using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using FNF_PROJ.Data;
using FNF_PROJ.DTOs;

namespace FNF_PROJ.Services
{
    public interface IAuthService
    {
        Task<string> RegisterAsync(UserRegisterDto dto);
        Task<string> LoginAsync(LoginDto dto);
    }

    public class AuthService : IAuthService
    {
        private readonly AppDbContext _db;
        private readonly IConfiguration _config;
        private readonly PasswordHasher<User> _pwdHasher;

        public AuthService(AppDbContext db, IConfiguration config)
        {
            _db = db;
            _config = config;
            _pwdHasher = new PasswordHasher<User>();
        }

        public async Task<string> RegisterAsync(UserRegisterDto dto)
        {
            if (await _db.Users.AnyAsync(u => u.Email == dto.Email))
                throw new InvalidOperationException("User already exists");

            string? profilePath = null;

            if (dto.ProfilePicture != null && dto.ProfilePicture.Length > 0)
            {
                var folder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "profiles");
                if (!Directory.Exists(folder))
                    Directory.CreateDirectory(folder);

                var fileName = Guid.NewGuid() + Path.GetExtension(dto.ProfilePicture.FileName);
                var filePath = Path.Combine(folder, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await dto.ProfilePicture.CopyToAsync(stream);
                }

                profilePath = "/profiles/" + fileName;
            }

            var user = new User
            {
                FullName = dto.FullName,
                Email = dto.Email,
                Role = "Employee", // enforce
                DepartmentId = dto.DepartmentId,
                ProfilePicture = profilePath,
                CreatedAt = DateTime.UtcNow
            };

            user.PasswordHash = _pwdHasher.HashPassword(user, dto.Password);

            _db.Users.Add(user);
            await _db.SaveChangesAsync();

            return GenerateJwtToken(user);
        }

        public async Task<string> LoginAsync(LoginDto dto)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == dto.Email);
            if (user == null) throw new InvalidOperationException("Invalid credentials");

            var result = _pwdHasher.VerifyHashedPassword(user, user.PasswordHash, dto.Password);
            if (result == PasswordVerificationResult.Failed)
                throw new InvalidOperationException("Invalid credentials");

            return GenerateJwtToken(user);
        }

        private string GenerateJwtToken(User user)
        {
            var jwt = _config.GetSection("Jwt");

            // ✅ Make sure we pass byte[] to SymmetricSecurityKey
            var keyBytes = Encoding.UTF8.GetBytes(jwt["Key"] ?? "DefaultSecretKey12345");
            var key = new SymmetricSecurityKey(keyBytes);
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            // NOTE: In your model, DepartmentId is an int (not nullable) in existing code,
            // so do NOT use the null-coalescing operator here.
            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.UserId.ToString()),
                new Claim(ClaimTypes.Name, user.FullName),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role),
                // Optional helpful custom claim for clients/services:
                new Claim("departmentId", user.DepartmentId.ToString())
            };

            var expiresMinutes = int.TryParse(jwt["ExpiresMinutes"], out var m) ? m : 60;

            var token = new JwtSecurityToken(
                issuer: jwt["Issuer"],
                audience: jwt["Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(expiresMinutes),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}

