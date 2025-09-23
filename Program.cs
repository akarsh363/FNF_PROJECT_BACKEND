using FNF_PROJ.Mapper;
using FNF_PROJ.Services;
using FNF_PROJ.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace FNF_PROJ
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddDbContext<AppDbContext>(options =>
                options.UseSqlServer(builder.Configuration.GetConnectionString("myCon"))
            );

            builder.Services.AddAutoMapper(cfg =>
            {
                cfg.AddMaps(typeof(MappingProfile).Assembly);
            });

            // Register AuthService & PostService
            builder.Services.AddScoped<IAuthService, AuthService>();
            builder.Services.AddScoped<IPostService, PostService>();

            // JWT configuration
            var jwtSection = builder.Configuration.GetSection("Jwt");
            var key = jwtSection["Key"] ?? "SuperSecretKey_ChangeThis_To_A_Very_Long_Value_123!";

            builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidIssuer = jwtSection["Issuer"],
                        ValidateAudience = true,
                        ValidAudience = jwtSection["Audience"],
                        ValidateLifetime = true,
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
                        ValidateIssuerSigningKey = true,
                        ClockSkew = TimeSpan.FromMinutes(2)
                    };
                });

            builder.Services.AddAuthorization();

            // 🔹 Enable CORS
            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowFrontend",
                    policy => policy.WithOrigins("http://localhost:5175") // React app
                                    .AllowAnyHeader()
                                    .AllowAnyMethod()
                                    .AllowCredentials());
            });

            builder.Services.AddControllers();
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            var app = builder.Build();

            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            // Use CORS BEFORE auth
            app.UseCors("AllowFrontend");

            app.UseAuthentication();
            app.UseAuthorization();

            app.UseStaticFiles();

            app.MapControllers();

            app.Run();
        }
    }
}
