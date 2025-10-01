using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FNF_PROJ.Data;
using FNF_PROJ.DTOs;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FNF_PROJ.Services
{
    // KEEP every signature that controllers use
    //public interface IPostService
    //{
    //    Task<PostResponseDto> CreatePostAsync(int userId, PostCreateDto dto);
    //    Task<PostResponseDto> EditPostAsync(int userId, string role, int deptId, int postId, PostCreateDto dto);

    //    // Manager-only delete with reason
    //    Task DeletePostAsync(int userId, int deptId, int postId, string reason);

    //    // Reposts
    //    Task<PostResponseDto> RepostAsync(int currentUserId, RepostDto dto);
    //    Task<List<PostResponseDto>> GetRepostsAsync(int postId);
    //    Task<List<PostResponseDto>> GetRepostsByUserAsync(int userId);

    //    // Reads
    //    Task<List<PostResponseDto>> GetAllPostsAsync();
    //    Task<PostResponseDto?> GetPostByIdAsync(int postId);
    //    Task<List<PostResponseDto>> GetPostsByUserAsync(int userId);

    //    // Back-compat alias
    //    Task<List<PostResponseDto>> GetMyPostsAsync(int userId);
    //}

    //public class PostService : IPostService
    //{
    //    private readonly AppDbContext _db;
    //    private readonly IWebHostEnvironment _env;
    //    private readonly ILogger<PostService>? _logger;

    //    public PostService(AppDbContext db, IWebHostEnvironment env, ILogger<PostService>? logger = null)
    //    {
    //        _db = db;
    //        _env = env;
    //        _logger = logger;
    //    }

    //    // ---------------- CREATE ----------------
    //    public async Task<PostResponseDto> CreatePostAsync(int userId, PostCreateDto dto)
    //    {
    //        var user = await _db.Users.FindAsync(userId) ?? throw new InvalidOperationException("User not found");

    //        var post = new Post
    //        {
    //            Title = dto.Title ?? "",
    //            Body = dto.Body ?? "",
    //            UserId = user.UserId,
    //            DeptId = user.DepartmentId,
    //            CreatedAt = DateTime.UtcNow,
    //            UpvoteCount = 0,
    //            DownvoteCount = 0,
    //            IsRepost = false
    //        };

    //        _db.Posts.Add(post);
    //        await _db.SaveChangesAsync();

    //        return await BuildPostDto(post.PostId);
    //    }

    //    // ---------------- EDIT ----------------
    //    public async Task<PostResponseDto> EditPostAsync(int userId, string role, int deptId, int postId, PostCreateDto dto)
    //    {
    //        var post = await _db.Posts
    //            .Include(p => p.User)
    //            .Include(p => p.Dept)
    //            .Include(p => p.Attachments)
    //            .Include(p => p.PostTags).ThenInclude(pt => pt.Tag)
    //            .FirstOrDefaultAsync(p => p.PostId == postId)
    //            ?? throw new InvalidOperationException("Post not found");

    //        if (string.Equals(role, "Employee", StringComparison.OrdinalIgnoreCase))
    //        {
    //            if (post.UserId != userId) throw new UnauthorizedAccessException();
    //        }
    //        else if (string.Equals(role, "Manager", StringComparison.OrdinalIgnoreCase))
    //        {
    //            if (post.DeptId != deptId) throw new UnauthorizedAccessException();
    //        }
    //        else throw new UnauthorizedAccessException();

    //        if (dto.Title != null) post.Title = dto.Title;
    //        if (dto.Body != null) post.Body = dto.Body;
    //        post.UpdatedAt = DateTime.UtcNow;
    //        await _db.SaveChangesAsync();

    //        return await BuildPostDto(post.PostId);
    //    }

    //    // ---------------- DELETE (Manager only) ----------------
    //    public async Task DeletePostAsync(int userId, int deptId, int postId, string reason)
    //    {
    //        reason ??= "";

    //        var user = await _db.Users.FindAsync(userId) ?? throw new InvalidOperationException("User not found");

    //        if (!string.Equals(user.Role, "Manager", StringComparison.OrdinalIgnoreCase))
    //            throw new UnauthorizedAccessException();

    //        var manager = await _db.Managers.FirstOrDefaultAsync(m => m.UserId == userId)
    //                      ?? throw new UnauthorizedAccessException();

    //        var post = await _db.Posts.FirstOrDefaultAsync(p => p.PostId == postId)
    //                   ?? throw new InvalidOperationException("Post not found");

    //        if (post.DeptId != deptId)
    //            throw new UnauthorizedAccessException();

    //        _db.Commits.Add(new Commit
    //        {
    //            PostId = post.PostId,
    //            ManagerId = manager.ManagerId,
    //            // ✅ Updated message (keeps your intended message behavior)
    //            Message = string.IsNullOrWhiteSpace(reason) ? "Deleted by manager" : reason,
    //            CreatedAt = DateTime.UtcNow
    //        });

    //        await _db.SaveChangesAsync();
    //    }

    //    // ---------------- REPOSTS ----------------
    //    public async Task<PostResponseDto> RepostAsync(int currentUserId, RepostDto dto)
    //    {
    //        if (dto == null || dto.PostId <= 0) throw new InvalidOperationException("Invalid repost payload");

    //        var user = await _db.Users.FindAsync(currentUserId) ?? throw new InvalidOperationException("User not found");

    //        var post = await _db.Posts
    //            .Include(p => p.User)
    //            .Include(p => p.PostTags).ThenInclude(pt => pt.Tag)
    //            .Include(p => p.Dept)
    //            .Include(p => p.Attachments)
    //            .FirstOrDefaultAsync(p => p.PostId == dto.PostId)
    //            ?? throw new InvalidOperationException("Original post not found");

    //        var already = await _db.Reposts.AnyAsync(r => r.PostId == post.PostId && r.UserId == currentUserId);
    //        if (already) throw new InvalidOperationException("Already reposted");

    //        var repost = new Repost
    //        {
    //            PostId = post.PostId,
    //            UserId = user.UserId,
    //            CreatedAt = DateTime.UtcNow
    //        };
    //        _db.Reposts.Add(repost);

    //        post.IsRepost = true;
    //        _db.Posts.Update(post);

    //        await _db.SaveChangesAsync();

    //        return new PostResponseDto
    //        {
    //            PostId = post.PostId,
    //            Title = $"[Repost by {user.FullName}] {post.Title}",
    //            Body = post.Body,
    //            AuthorName = post.User?.FullName ?? "(unknown)",
    //            DepartmentName = post.Dept?.DeptName ?? $"Dept {post.DeptId}",
    //            // DeptId may exist in your DTO — keep consistent with other methods
    //            DeptId = post.DeptId,
    //            UpvoteCount = post.UpvoteCount,
    //            DownvoteCount = post.DownvoteCount,
    //            Tags = post.PostTags?.Where(t => t.Tag != null).Select(t => t.Tag.TagName).ToList(),
    //            Attachments = post.Attachments?.Select(a => a.FilePath).ToList(),
    //            IsRepost = true,
    //            CreatedAt = repost.CreatedAt
    //        };
    //    }

    //    public async Task<List<PostResponseDto>> GetRepostsAsync(int postId)
    //    {
    //        var reposts = await _db.Reposts
    //            .Include(r => r.User)
    //            .Include(r => r.Post).ThenInclude(p => p.User)
    //            .Include(r => r.Post).ThenInclude(p => p.Dept)
    //            .Include(r => r.Post).ThenInclude(p => p.PostTags).ThenInclude(pt => pt.Tag)
    //            .Include(r => r.Post).ThenInclude(p => p.Attachments)
    //            .Where(r => r.PostId == postId)
    //            .OrderByDescending(r => r.CreatedAt)
    //            .ToListAsync();

    //        return reposts.Select(r => new PostResponseDto
    //        {
    //            PostId = r.Post.PostId,
    //            Title = $"[Repost by {r.User.FullName}] {r.Post.Title}",
    //            Body = r.Post.Body,
    //            AuthorName = r.Post.User?.FullName ?? "(unknown)",
    //            DepartmentName = r.Post.Dept?.DeptName ?? $"Dept {r.Post.DeptId}",
    //            DeptId = r.Post.DeptId,
    //            UpvoteCount = r.Post.UpvoteCount,
    //            DownvoteCount = r.Post.DownvoteCount,
    //            Tags = r.Post.PostTags?.Where(t => t.Tag != null).Select(t => t.Tag.TagName).ToList(),
    //            Attachments = r.Post.Attachments?.Select(a => a.FilePath).ToList(),
    //            IsRepost = true,
    //            CreatedAt = r.CreatedAt
    //        }).ToList();
    //    }

    //    public async Task<List<PostResponseDto>> GetRepostsByUserAsync(int userId)
    //    {
    //        var reposts = await _db.Reposts
    //            .Where(r => r.UserId == userId)
    //            .Include(r => r.User)
    //            .Include(r => r.Post).ThenInclude(p => p.User)
    //            .Include(r => r.Post).ThenInclude(p => p.Dept)
    //            .Include(r => r.Post).ThenInclude(p => p.PostTags).ThenInclude(pt => pt.Tag)
    //            .Include(r => r.Post).ThenInclude(p => p.Attachments)
    //            .OrderByDescending(r => r.CreatedAt)
    //            .ToListAsync();

    //        return reposts.Select(r => new PostResponseDto
    //        {
    //            PostId = r.Post.PostId,
    //            Title = $"[Repost by {r.User.FullName}] {r.Post.Title}",
    //            Body = r.Post.Body,
    //            AuthorName = r.Post.User?.FullName ?? "(unknown)",
    //            DepartmentName = r.Post.Dept?.DeptName ?? $"Dept {r.Post.DeptId}",
    //            DeptId = r.Post.DeptId,
    //            UpvoteCount = r.Post.UpvoteCount,
    //            DownvoteCount = r.Post.DownvoteCount,
    //            Tags = r.Post.PostTags?.Where(t => t.Tag != null).Select(t => t.Tag.TagName).ToList(),
    //            Attachments = r.Post.Attachments?.Select(a => a.FilePath).ToList(),
    //            IsRepost = true,
    //            CreatedAt = r.CreatedAt
    //        }).ToList();
    //    }

    //    // ---------------- READS ----------------
    //    public async Task<List<PostResponseDto>> GetAllPostsAsync()
    //    {
    //        var posts = await _db.Posts
    //            .Where(p => !_db.Commits.Any(c => c.PostId == p.PostId))
    //            .Include(p => p.User)
    //            .Include(p => p.Dept)
    //            .Include(p => p.PostTags).ThenInclude(pt => pt.Tag)
    //            .Include(p => p.Attachments)
    //            .ToListAsync();

    //        var originalDtos = posts.Select(p => new PostResponseDto
    //        {
    //            PostId = p.PostId,
    //            Title = p.Title,
    //            Body = p.Body,
    //            AuthorName = p.User?.FullName ?? "(unknown)",
    //            DepartmentName = p.Dept?.DeptName ?? $"Dept {p.DeptId}",
    //            DeptId = p.DeptId,
    //            UpvoteCount = p.UpvoteCount,
    //            DownvoteCount = p.DownvoteCount,
    //            Tags = p.PostTags?.Where(pt => pt.Tag != null).Select(pt => pt.Tag.TagName).ToList() ?? new List<string>(),
    //            Attachments = p.Attachments?.Select(a => a.FilePath).ToList() ?? new List<string>(),
    //            IsRepost = p.IsRepost,
    //            CreatedAt = p.CreatedAt == default ? DateTime.UtcNow : p.CreatedAt
    //        });

    //        var repostRows = await _db.Reposts
    //            .Include(r => r.User)
    //            .Include(r => r.Post).ThenInclude(p => p.User)
    //            .Include(r => r.Post).ThenInclude(p => p.Dept)
    //            .Include(r => r.Post).ThenInclude(p => p.PostTags).ThenInclude(pt => pt.Tag)
    //            .Include(r => r.Post).ThenInclude(p => p.Attachments)
    //            .Where(r => !_db.Commits.Any(c => c.PostId == r.PostId))
    //            .ToListAsync();

    //        var repostDtos = repostRows.Select(r =>
    //        {
    //            var orig = r.Post;
    //            return new PostResponseDto
    //            {
    //                PostId = orig?.PostId ?? 0,
    //                Title = $"[Repost by {r.User?.FullName ?? "(unknown)"}] {orig?.Title ?? ""}",
    //                Body = orig?.Body ?? "",
    //                AuthorName = orig?.User?.FullName ?? "(unknown)",
    //                DepartmentName = orig?.Dept?.DeptName ?? (orig != null ? $"Dept {orig.DeptId}" : "(unknown)"),
    //                DeptId = orig?.DeptId ?? 0,
    //                UpvoteCount = orig?.UpvoteCount ?? 0,
    //                DownvoteCount = orig?.DownvoteCount ?? 0,
    //                Tags = orig?.PostTags?.Where(pt => pt.Tag != null).Select(pt => pt.Tag.TagName).ToList() ?? new List<string>(),
    //                Attachments = orig?.Attachments?.Select(a => a.FilePath).ToList() ?? new List<string>(),
    //                IsRepost = true,
    //                CreatedAt = r.CreatedAt == default ? DateTime.UtcNow : r.CreatedAt
    //            };
    //        });

    //        return originalDtos.Concat(repostDtos)
    //            .OrderByDescending(x => x.CreatedAt)
    //            .ToList();
    //    }

    //    public async Task<PostResponseDto?> GetPostByIdAsync(int postId)
    //    {
    //        var hasCommit = await _db.Commits.AnyAsync(c => c.PostId == postId);
    //        if (hasCommit) return null;

    //        var p = await _db.Posts
    //            .Include(p => p.User).Include(p => p.Dept)
    //            .Include(p => p.PostTags).ThenInclude(pt => pt.Tag)
    //            .Include(p => p.Attachments)
    //            .FirstOrDefaultAsync(p => p.PostId == postId);
    //        if (p == null) return null;

    //        return new PostResponseDto
    //        {
    //            PostId = p.PostId,
    //            Title = p.Title,
    //            Body = p.Body,
    //            AuthorName = p.User?.FullName ?? "(unknown)",
    //            DepartmentName = p.Dept?.DeptName ?? $"Dept {p.DeptId}",
    //            DeptId = p.DeptId,
    //            UpvoteCount = p.UpvoteCount,
    //            DownvoteCount = p.DownvoteCount,
    //            Tags = p.PostTags?.Where(pt => pt.Tag != null).Select(pt => pt.Tag.TagName).ToList() ?? new List<string>(),
    //            Attachments = p.Attachments?.Select(a => a.FilePath).ToList() ?? new List<string>(),
    //            IsRepost = p.IsRepost,
    //            CreatedAt = p.CreatedAt == default ? DateTime.UtcNow : p.CreatedAt
    //        };
    //    }

    //    // ---------------- FIXED METHOD ----------------
    //    public async Task<List<PostResponseDto>> GetPostsByUserAsync(int userId)
    //    {
    //        // original posts by this user (exclude hidden)
    //        var ownPosts = await _db.Posts
    //            .Where(p => p.UserId == userId && !_db.Commits.Any(c => c.PostId == p.PostId))
    //            .Include(p => p.User).Include(p => p.Dept)
    //            .OrderByDescending(p => p.CreatedAt)
    //            .ToListAsync();

    //        var ownDtos = ownPosts.Select(p => new PostResponseDto
    //        {
    //            PostId = p.PostId,
    //            Title = p.Title,
    //            Body = p.Body,
    //            AuthorName = p.User?.FullName ?? "(unknown)",
    //            DepartmentName = p.Dept?.DeptName ?? $"Dept {p.DeptId}",
    //            DeptId = p.DeptId,
    //            UpvoteCount = p.UpvoteCount,
    //            DownvoteCount = p.DownvoteCount,
    //            Tags = new List<string>(),
    //            Attachments = new List<string>(),
    //            IsRepost = p.IsRepost,
    //            CreatedAt = p.CreatedAt == default ? DateTime.UtcNow : p.CreatedAt
    //        });

    //        // reposts made by this user (exclude hidden originals)
    //        var repostRows = await _db.Reposts
    //            .Where(r => r.UserId == userId && !_db.Commits.Any(c => c.PostId == r.PostId))
    //            .Include(r => r.Post).ThenInclude(p => p.User)
    //            .Include(r => r.Post).ThenInclude(p => p.Dept)
    //            .Include(r => r.Post).ThenInclude(p => p.PostTags).ThenInclude(pt => pt.Tag)
    //            .Include(r => r.Post).ThenInclude(p => p.Attachments)
    //            .OrderByDescending(r => r.CreatedAt)
    //            .ToListAsync();

    //        var repostDtos = repostRows.Select(r =>
    //        {
    //            var orig = r.Post;
    //            return new PostResponseDto
    //            {
    //                PostId = orig?.PostId ?? 0,
    //                Title = $"[Repost by {r.User?.FullName ?? "(you)"}] {orig?.Title ?? ""}",
    //                Body = orig?.Body ?? "",
    //                AuthorName = orig?.User?.FullName ?? "(unknown)",
    //                DepartmentName = orig?.Dept?.DeptName ?? (orig != null ? $"Dept {orig.DeptId}" : "(unknown)"),
    //                DeptId = orig?.DeptId ?? 0,
    //                UpvoteCount = orig?.UpvoteCount ?? 0,
    //                DownvoteCount = orig?.DownvoteCount ?? 0,
    //                Tags = orig?.PostTags?.Where(pt => pt.Tag != null).Select(pt => pt.Tag.TagName).ToList() ?? new List<string>(),
    //                Attachments = orig?.Attachments?.Select(a => a.FilePath).ToList() ?? new List<string>(),
    //                IsRepost = true,
    //                CreatedAt = r.CreatedAt == default ? DateTime.UtcNow : r.CreatedAt
    //            };
    //        });

    //        return ownDtos.Concat(repostDtos)
    //            .OrderByDescending(x => x.CreatedAt)
    //            .ToList();
    //    }

    //    public Task<List<PostResponseDto>> GetMyPostsAsync(int userId) => GetPostsByUserAsync(userId);

    //    private async Task<PostResponseDto> BuildPostDto(int postId)
    //    {
    //        var p = await _db.Posts
    //            .Include(p => p.User)
    //            .Include(p => p.Dept)
    //            .Include(p => p.PostTags).ThenInclude(pt => pt.Tag)
    //            .Include(p => p.Attachments)
    //            .FirstOrDefaultAsync(p => p.PostId == postId)
    //            ?? throw new InvalidOperationException("Post not found");

    //        return new PostResponseDto
    //        {
    //            PostId = p.PostId,
    //            Title = p.Title,
    //            Body = p.Body,
    //            AuthorName = p.User?.FullName ?? "(unknown)",
    //            DepartmentName = p.Dept?.DeptName ?? $"Dept {p.DeptId}",
    //            DeptId = p.DeptId,
    //            UpvoteCount = p.UpvoteCount,
    //            DownvoteCount = p.DownvoteCount,
    //            Tags = p.PostTags?.Where(pt => pt.Tag != null).Select(pt => pt.Tag.TagName).ToList() ?? new List<string>(),
    //            Attachments = p.Attachments?.Select(a => a.FilePath).ToList() ?? new List<string>(),
    //            IsRepost = p.IsRepost,
    //            CreatedAt = p.CreatedAt == default ? DateTime.UtcNow : p.CreatedAt
    //        };
    //    }

    //    private static string SanitizeFileName(string filename)
    //    {
    //        var name = Path.GetFileName(filename);
    //        foreach (var c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
    //        return name;
    //}



    public interface IPostService
    {
        Task<PostResponseDto> CreatePostAsync(int userId, PostCreateDto dto);
        Task<PostResponseDto> EditPostAsync(int userId, string role, int deptId, int postId, PostCreateDto dto);

        // Manager-only delete with reason
        Task DeletePostAsync(int userId, int deptId, int postId, string reason);

        // Reposts
        Task<PostResponseDto> RepostAsync(int currentUserId, RepostDto dto);
        Task<List<PostResponseDto>> GetRepostsAsync(int postId);
        Task<List<PostResponseDto>> GetRepostsByUserAsync(int userId);

        // Reads
        Task<List<PostResponseDto>> GetAllPostsAsync();
        Task<PostResponseDto?> GetPostByIdAsync(int postId);
        Task<List<PostResponseDto>> GetPostsByUserAsync(int userId);

        // Back-compat alias
        Task<List<PostResponseDto>> GetMyPostsAsync(int userId);
    }

    public class PostService : IPostService
    {
        private readonly AppDbContext _db;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<PostService>? _logger;

        public PostService(AppDbContext db, IWebHostEnvironment env, ILogger<PostService>? logger = null)
        {
            _db = db;
            _env = env;
            _logger = logger;
        }

        // ---------------- CREATE ----------------
        public async Task<PostResponseDto> CreatePostAsync(int userId, PostCreateDto dto)
        {
            var user = await _db.Users.FindAsync(userId) ?? throw new InvalidOperationException("User not found");

            var post = new Post
            {
                Title = dto.Title ?? "",
                Body = dto.Body ?? "",
                UserId = user.UserId,
                DeptId = user.DepartmentId,
                CreatedAt = DateTime.UtcNow,
                UpvoteCount = 0,
                DownvoteCount = 0,
                IsRepost = false
            };

            _db.Posts.Add(post);
            await _db.SaveChangesAsync();

            // Persist tag relationships if any tag IDs passed
            if (dto.TagIds != null && dto.TagIds.Any())
            {
                var validTagIds = await _db.Tags
                    .Where(t => dto.TagIds.Contains(t.TagId))
                    .Select(t => t.TagId)
                    .ToListAsync();

                var postTags = validTagIds.Select(tagId => new PostTag
                {
                    PostId = post.PostId,
                    TagId = tagId
                });

                _db.PostTags.AddRange(postTags);
                await _db.SaveChangesAsync();
            }

            // Persist attachments: save files to wwwroot/uploads and create Attachment rows
            if (dto.Attachments != null && dto.Attachments.Any())
            {
                var uploadsRoot = Path.Combine(_env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot"), "uploads");
                if (!Directory.Exists(uploadsRoot)) Directory.CreateDirectory(uploadsRoot);

                var attachments = new List<Attachment>();
                foreach (var file in dto.Attachments)
                {
                    if (file == null || file.Length == 0) continue;

                    // Optionally: validate file.ContentType and file.Length here

                    var originalName = SanitizeFileName(file.FileName);
                    var uniqueName = $"{DateTime.UtcNow:yyyyMMddHHmmssfff}_{Guid.NewGuid()}{Path.GetExtension(originalName)}";
                    var filePath = Path.Combine(uploadsRoot, uniqueName);

                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await file.CopyToAsync(stream);
                    }

                    // Store relative path or filename (choose what your frontend expects)
                    var attachment = new Attachment
                    {
                        PostId = post.PostId,
                        FileName = uniqueName,
                        FilePath = $"/uploads/{uniqueName}"
                        // set other fields if Attachment entity has them
                    };
                    attachments.Add(attachment);
                }

                if (attachments.Any())
                {
                    _db.Attachments.AddRange(attachments);
                    await _db.SaveChangesAsync();
                }
            }

            return await BuildPostDto(post.PostId);
        }

        // ---------------- EDIT ----------------
        public async Task<PostResponseDto> EditPostAsync(int userId, string role, int deptId, int postId, PostCreateDto dto)
        {
            var post = await _db.Posts
                .Include(p => p.User)
                .Include(p => p.Dept)
                .Include(p => p.Attachments)
                .Include(p => p.PostTags).ThenInclude(pt => pt.Tag)
                .FirstOrDefaultAsync(p => p.PostId == postId)
                ?? throw new InvalidOperationException("Post not found");

            if (string.Equals(role, "Employee", StringComparison.OrdinalIgnoreCase))
            {
                if (post.UserId != userId) throw new UnauthorizedAccessException();
            }
            else if (string.Equals(role, "Manager", StringComparison.OrdinalIgnoreCase))
            {
                if (post.DeptId != deptId) throw new UnauthorizedAccessException();
            }
            else throw new UnauthorizedAccessException();

            if (dto.Title != null) post.Title = dto.Title;
            if (dto.Body != null) post.Body = dto.Body;
            post.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            // Optionally handle updated TagIds and Attachments in edit:
            if (dto.TagIds != null)
            {
                // Remove existing post tags
                var existing = _db.PostTags.Where(pt => pt.PostId == postId);
                _db.PostTags.RemoveRange(existing);

                var validTagIds = await _db.Tags.Where(t => dto.TagIds.Contains(t.TagId)).Select(t => t.TagId).ToListAsync();
                var postTags = validTagIds.Select(tagId => new PostTag { PostId = post.PostId, TagId = tagId });
                _db.PostTags.AddRange(postTags);
                await _db.SaveChangesAsync();
            }

            // If new attachments present, save them and add Attachment rows (do not delete old ones)
            if (dto.Attachments != null && dto.Attachments.Any())
            {
                var uploadsRoot = Path.Combine(_env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot"), "uploads");
                if (!Directory.Exists(uploadsRoot)) Directory.CreateDirectory(uploadsRoot);

                var attachments = new List<Attachment>();
                foreach (var file in dto.Attachments)
                {
                    if (file == null || file.Length == 0) continue;
                    var originalName = SanitizeFileName(file.FileName);
                    var uniqueName = $"{DateTime.UtcNow:yyyyMMddHHmmssfff}_{Guid.NewGuid()}{Path.GetExtension(originalName)}";
                    var filePath = Path.Combine(uploadsRoot, uniqueName);

                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await file.CopyToAsync(stream);
                    }

                    attachments.Add(new Attachment
                    {
                        PostId = post.PostId,
                        FileName = uniqueName,
                        FilePath = $"/uploads/{uniqueName}"
                    });
                }

                if (attachments.Any())
                {
                    _db.Attachments.AddRange(attachments);
                    await _db.SaveChangesAsync();
                }
            }

            return await BuildPostDto(post.PostId);
        }

        // ---------------- DELETE (Manager only) ----------------
        public async Task DeletePostAsync(int userId, int deptId, int postId, string reason)
        {
            reason ??= "";

            var user = await _db.Users.FindAsync(userId) ?? throw new InvalidOperationException("User not found");

            if (!string.Equals(user.Role, "Manager", StringComparison.OrdinalIgnoreCase))
                throw new UnauthorizedAccessException();

            var manager = await _db.Managers.FirstOrDefaultAsync(m => m.UserId == userId)
                          ?? throw new UnauthorizedAccessException();

            var post = await _db.Posts.FirstOrDefaultAsync(p => p.PostId == postId)
                       ?? throw new InvalidOperationException("Post not found");

            if (post.DeptId != deptId)
                throw new UnauthorizedAccessException();

            _db.Commits.Add(new Commit
            {
                PostId = post.PostId,
                ManagerId = manager.ManagerId,
                Message = string.IsNullOrWhiteSpace(reason) ? "Deleted by manager" : reason,
                CreatedAt = DateTime.UtcNow
            });

            await _db.SaveChangesAsync();
        }

        // ---------------- REPOSTS ----------------
        public async Task<PostResponseDto> RepostAsync(int currentUserId, RepostDto dto)
        {
            if (dto == null || dto.PostId <= 0) throw new InvalidOperationException("Invalid repost payload");

            var user = await _db.Users.FindAsync(currentUserId) ?? throw new InvalidOperationException("User not found");

            var post = await _db.Posts
                .Include(p => p.User)
                .Include(p => p.PostTags).ThenInclude(pt => pt.Tag)
                .Include(p => p.Dept)
                .Include(p => p.Attachments)
                .FirstOrDefaultAsync(p => p.PostId == dto.PostId)
                ?? throw new InvalidOperationException("Original post not found");

            var already = await _db.Reposts.AnyAsync(r => r.PostId == post.PostId && r.UserId == currentUserId);
            if (already) throw new InvalidOperationException("Already reposted");

            var repost = new Repost
            {
                PostId = post.PostId,
                UserId = user.UserId,
                CreatedAt = DateTime.UtcNow
            };
            _db.Reposts.Add(repost);

            post.IsRepost = true;
            _db.Posts.Update(post);

            await _db.SaveChangesAsync();

            return new PostResponseDto
            {
                PostId = post.PostId,
                Title = $"[Repost by {user.FullName}] {post.Title}",
                Body = post.Body,
                AuthorName = post.User?.FullName ?? "(unknown)",
                DepartmentName = post.Dept?.DeptName ?? $"Dept {post.DeptId}",
                DeptId = post.DeptId,
                UpvoteCount = post.UpvoteCount,
                DownvoteCount = post.DownvoteCount,
                Tags = post.PostTags?.Where(t => t.Tag != null).Select(t => t.Tag.TagName).ToList() ?? new List<string>(),
                Attachments = post.Attachments?.Select(a => a.FilePath).ToList() ?? new List<string>(),
                IsRepost = true,
                CreatedAt = repost.CreatedAt
            };
        }

        public async Task<List<PostResponseDto>> GetRepostsAsync(int postId)
        {
            var reposts = await _db.Reposts
                .Include(r => r.User)
                .Include(r => r.Post).ThenInclude(p => p.User)
                .Include(r => r.Post).ThenInclude(p => p.Dept)
                .Include(r => r.Post).ThenInclude(p => p.PostTags).ThenInclude(pt => pt.Tag)
                .Include(r => r.Post).ThenInclude(p => p.Attachments)
                .Where(r => r.PostId == postId)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            return reposts.Select(r => new PostResponseDto
            {
                PostId = r.Post.PostId,
                Title = $"[Repost by {r.User.FullName}] {r.Post.Title}",
                Body = r.Post.Body,
                AuthorName = r.Post.User?.FullName ?? "(unknown)",
                DepartmentName = r.Post.Dept?.DeptName ?? $"Dept {r.Post.DeptId}",
                DeptId = r.Post.DeptId,
                UpvoteCount = r.Post.UpvoteCount,
                DownvoteCount = r.Post.DownvoteCount,
                Tags = r.Post.PostTags?.Where(t => t.Tag != null).Select(t => t.Tag.TagName).ToList() ?? new List<string>(),
                Attachments = r.Post.Attachments?.Select(a => a.FilePath).ToList() ?? new List<string>(),
                IsRepost = true,
                CreatedAt = r.CreatedAt
            }).ToList();
        }

        public async Task<List<PostResponseDto>> GetRepostsByUserAsync(int userId)
        {
            var reposts = await _db.Reposts
                .Where(r => r.UserId == userId)
                .Include(r => r.User)
                .Include(r => r.Post).ThenInclude(p => p.User)
                .Include(r => r.Post).ThenInclude(p => p.Dept)
                .Include(r => r.Post).ThenInclude(p => p.PostTags).ThenInclude(pt => pt.Tag)
                .Include(r => r.Post).ThenInclude(p => p.Attachments)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            return reposts.Select(r => new PostResponseDto
            {
                PostId = r.Post.PostId,
                Title = $"[Repost by {r.User.FullName}] {r.Post.Title}",
                Body = r.Post.Body,
                AuthorName = r.Post.User?.FullName ?? "(unknown)",
                DepartmentName = r.Post.Dept?.DeptName ?? $"Dept {r.Post.DeptId}",
                DeptId = r.Post.DeptId,
                UpvoteCount = r.Post.UpvoteCount,
                DownvoteCount = r.Post.DownvoteCount,
                Tags = r.Post.PostTags?.Where(t => t.Tag != null).Select(t => t.Tag.TagName).ToList() ?? new List<string>(),
                Attachments = r.Post.Attachments?.Select(a => a.FilePath).ToList() ?? new List<string>(),
                IsRepost = true,
                CreatedAt = r.CreatedAt
            }).ToList();
        }

        // ---------------- READS ----------------
        public async Task<List<PostResponseDto>> GetAllPostsAsync()
        {
            var posts = await _db.Posts
                .Where(p => !_db.Commits.Any(c => c.PostId == p.PostId))
                .Include(p => p.User)
                .Include(p => p.Dept)
                .Include(p => p.PostTags).ThenInclude(pt => pt.Tag)
                .Include(p => p.Attachments)
                .ToListAsync();

            var originalDtos = posts.Select(p => new PostResponseDto
            {
                PostId = p.PostId,
                Title = p.Title,
                Body = p.Body,
                AuthorName = p.User?.FullName ?? "(unknown)",
                DepartmentName = p.Dept?.DeptName ?? $"Dept {p.DeptId}",
                DeptId = p.DeptId,
                UpvoteCount = p.UpvoteCount,
                DownvoteCount = p.DownvoteCount,
                Tags = p.PostTags?.Where(pt => pt.Tag != null).Select(pt => pt.Tag.TagName).ToList() ?? new List<string>(),
                Attachments = p.Attachments?.Select(a => a.FilePath).ToList() ?? new List<string>(),
                IsRepost = p.IsRepost,
                CreatedAt = p.CreatedAt == default ? DateTime.UtcNow : p.CreatedAt
            });

            var repostRows = await _db.Reposts
                .Include(r => r.User)
                .Include(r => r.Post).ThenInclude(p => p.User)
                .Include(r => r.Post).ThenInclude(p => p.Dept)
                .Include(r => r.Post).ThenInclude(p => p.PostTags).ThenInclude(pt => pt.Tag)
                .Include(r => r.Post).ThenInclude(p => p.Attachments)
                .Where(r => !_db.Commits.Any(c => c.PostId == r.PostId))
                .ToListAsync();

            var repostDtos = repostRows.Select(r =>
            {
                var orig = r.Post;
                return new PostResponseDto
                {
                    PostId = orig?.PostId ?? 0,
                    Title = $"[Repost by {r.User?.FullName ?? "(unknown)"}] {orig?.Title ?? ""}",
                    Body = orig?.Body ?? "",
                    AuthorName = orig?.User?.FullName ?? "(unknown)",
                    DepartmentName = orig?.Dept?.DeptName ?? (orig != null ? $"Dept {orig.DeptId}" : "(unknown)"),
                    DeptId = orig?.DeptId ?? 0,
                    UpvoteCount = orig?.UpvoteCount ?? 0,
                    DownvoteCount = orig?.DownvoteCount ?? 0,
                    Tags = orig?.PostTags?.Where(pt => pt.Tag != null).Select(pt => pt.Tag.TagName).ToList() ?? new List<string>(),
                    Attachments = orig?.Attachments?.Select(a => a.FilePath).ToList() ?? new List<string>(),
                    IsRepost = true,
                    CreatedAt = r.CreatedAt == default ? DateTime.UtcNow : r.CreatedAt
                };
            });

            return originalDtos.Concat(repostDtos)
                .OrderByDescending(x => x.CreatedAt)
                .ToList();
        }

        public async Task<PostResponseDto?> GetPostByIdAsync(int postId)
        {
            var hasCommit = await _db.Commits.AnyAsync(c => c.PostId == postId);
            if (hasCommit) return null;

            var p = await _db.Posts
                .Include(p => p.User).Include(p => p.Dept)
                .Include(p => p.PostTags).ThenInclude(pt => pt.Tag)
                .Include(p => p.Attachments)
                .FirstOrDefaultAsync(p => p.PostId == postId);
            if (p == null) return null;

            return new PostResponseDto
            {
                PostId = p.PostId,
                Title = p.Title,
                Body = p.Body,
                AuthorName = p.User?.FullName ?? "(unknown)",
                DepartmentName = p.Dept?.DeptName ?? $"Dept {p.DeptId}",
                DeptId = p.DeptId,
                UpvoteCount = p.UpvoteCount,
                DownvoteCount = p.DownvoteCount,
                Tags = p.PostTags?.Where(pt => pt.Tag != null).Select(pt => pt.Tag.TagName).ToList() ?? new List<string>(),
                Attachments = p.Attachments?.Select(a => a.FilePath).ToList() ?? new List<string>(),
                IsRepost = p.IsRepost,
                CreatedAt = p.CreatedAt == default ? DateTime.UtcNow : p.CreatedAt
            };
        }

        // ---------------- FIXED METHOD ----------------
        public async Task<List<PostResponseDto>> GetPostsByUserAsync(int userId)
        {
            // original posts by this user (exclude hidden)
            var ownPosts = await _db.Posts
                .Where(p => p.UserId == userId && !_db.Commits.Any(c => c.PostId == p.PostId))
                .Include(p => p.User).Include(p => p.Dept)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            var ownDtos = ownPosts.Select(p => new PostResponseDto
            {
                PostId = p.PostId,
                Title = p.Title,
                Body = p.Body,
                AuthorName = p.User?.FullName ?? "(unknown)",
                DepartmentName = p.Dept?.DeptName ?? $"Dept {p.DeptId}",
                DeptId = p.DeptId,
                UpvoteCount = p.UpvoteCount,
                DownvoteCount = p.DownvoteCount,
                Tags = p.PostTags?.Where(pt => pt.Tag != null).Select(pt => pt.Tag.TagName).ToList() ?? new List<string>(),
                Attachments = p.Attachments?.Select(a => a.FilePath).ToList() ?? new List<string>(),
                IsRepost = p.IsRepost,
                CreatedAt = p.CreatedAt == default ? DateTime.UtcNow : p.CreatedAt
            });

            // reposts made by this user (exclude hidden originals)
            var repostRows = await _db.Reposts
                .Where(r => r.UserId == userId && !_db.Commits.Any(c => c.PostId == r.PostId))
                .Include(r => r.Post).ThenInclude(p => p.User)
                .Include(r => r.Post).ThenInclude(p => p.Dept)
                .Include(r => r.Post).ThenInclude(p => p.PostTags).ThenInclude(pt => pt.Tag)
                .Include(r => r.Post).ThenInclude(p => p.Attachments)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            var repostDtos = repostRows.Select(r =>
            {
                var orig = r.Post;
                return new PostResponseDto
                {
                    PostId = orig?.PostId ?? 0,
                    Title = $"[Repost by {r.User?.FullName ?? "(you)"}] {orig?.Title ?? ""}",
                    Body = orig?.Body ?? "",
                    AuthorName = orig?.User?.FullName ?? "(unknown)",
                    DepartmentName = orig?.Dept?.DeptName ?? (orig != null ? $"Dept {orig.DeptId}" : "(unknown)"),
                    DeptId = orig?.DeptId ?? 0,
                    UpvoteCount = orig?.UpvoteCount ?? 0,
                    DownvoteCount = orig?.DownvoteCount ?? 0,
                    Tags = orig?.PostTags?.Where(pt => pt.Tag != null).Select(pt => pt.Tag.TagName).ToList() ?? new List<string>(),
                    Attachments = orig?.Attachments?.Select(a => a.FilePath).ToList() ?? new List<string>(),
                    IsRepost = true,
                    CreatedAt = r.CreatedAt == default ? DateTime.UtcNow : r.CreatedAt
                };
            });

            return ownDtos.Concat(repostDtos)
                .OrderByDescending(x => x.CreatedAt)
                .ToList();
        }

        public Task<List<PostResponseDto>> GetMyPostsAsync(int userId) => GetPostsByUserAsync(userId);

        private async Task<PostResponseDto> BuildPostDto(int postId)
        {
            var p = await _db.Posts
                .Include(p => p.User)
                .Include(p => p.Dept)
                .Include(p => p.PostTags).ThenInclude(pt => pt.Tag)
                .Include(p => p.Attachments)
                .FirstOrDefaultAsync(p => p.PostId == postId)
                ?? throw new InvalidOperationException("Post not found");

            return new PostResponseDto
            {
                PostId = p.PostId,
                Title = p.Title,
                Body = p.Body,
                AuthorName = p.User?.FullName ?? "(unknown)",
                DepartmentName = p.Dept?.DeptName ?? $"Dept {p.DeptId}",
                DeptId = p.DeptId,
                UpvoteCount = p.UpvoteCount,
                DownvoteCount = p.DownvoteCount,
                Tags = p.PostTags?.Where(pt => pt.Tag != null).Select(pt => pt.Tag.TagName).ToList() ?? new List<string>(),
                Attachments = p.Attachments?.Select(a => a.FilePath).ToList() ?? new List<string>(),
                IsRepost = p.IsRepost,
                CreatedAt = p.CreatedAt == default ? DateTime.UtcNow : p.CreatedAt
            };
        }

        private static string SanitizeFileName(string filename)
        {
            var name = Path.GetFileName(filename);
            foreach (var c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
            return name;
        }
    }
}

