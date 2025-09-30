using FNF_PROJ.Mapper;
using FNF_PROJ.Services;
using FNF_PROJ.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.AspNetCore.ResponseCompression;
using System.IO.Compression;

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
            builder.Services.AddScoped<CommentService>();
            builder.Services.AddScoped<ITagService, TagService>();

            // ✅ Register concrete VoteService (no interface)
            builder.Services.AddScoped<VoteService>();

            // --- Response compression (Brotli + Gzip) ---
            builder.Services.AddResponseCompression(options =>
            {
                options.EnableForHttps = true;
                options.Providers.Add<BrotliCompressionProvider>();
                options.Providers.Add<GzipCompressionProvider>();
            });

            builder.Services.Configure<BrotliCompressionProviderOptions>(opts =>
            {
                opts.Level = CompressionLevel.Fastest;
            });
            builder.Services.Configure<GzipCompressionProviderOptions>(opts =>
            {
                opts.Level = CompressionLevel.Fastest;
            });
            // --- end compression config ---

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

            // 🔹 CORS (adjust port to your Vite dev server)
            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowFrontend",
                    policy => policy.WithOrigins("http://localhost:5173")
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

            // Enable response compression before static files so static assets and API responses are compressed
            app.UseResponseCompression();

            app.UseStaticFiles();

            app.MapControllers();

            app.Run();
        }
    }
}
