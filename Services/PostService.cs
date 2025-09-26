//// File: Services/PostService.cs
//using System;
//using System.Collections.Generic;
//using System.IO;
//using System.Linq;
//using System.Threading.Tasks;
//using Microsoft.EntityFrameworkCore;
//using Microsoft.AspNetCore.Hosting;
//using Microsoft.Extensions.Logging;
//using FNF_PROJ.Data;
//using FNF_PROJ.DTOs;

//namespace FNF_PROJ.Services
//{
//    // Interface + implementation in same file
//    public interface IPostService
//    {
//        Task<PostResponseDto> CreatePostAsync(int userId, PostCreateDto dto);
//        Task<PostResponseDto> EditPostAsync(int userId, string role, int deptId, int postId, PostCreateDto dto);
//        Task DeletePostAsync(int userId, int deptId, int postId);
//        Task<PostResponseDto> RepostAsync(int currentUserId, RepostDto dto);
//        Task<List<PostResponseDto>> GetRepostsAsync(int postId);
//        Task<List<PostResponseDto>> GetRepostsByUserAsync(int userId);
//        Task<List<PostResponseDto>> GetAllPostsAsync();
//        Task<PostResponseDto?> GetPostByIdAsync(int postId);
//    }

//    public class PostService : IPostService
//    {
//        private readonly AppDbContext _db;
//        private readonly IWebHostEnvironment _env;
//        private readonly ILogger<PostService>? _logger;

//        public PostService(AppDbContext db, IWebHostEnvironment env, ILogger<PostService>? logger = null)
//        {
//            _db = db;
//            _env = env;
//            _logger = logger;
//        }

//        public async Task<PostResponseDto> CreatePostAsync(int userId, PostCreateDto dto)
//        {
//            var user = await _db.Users.FindAsync(userId) ?? throw new InvalidOperationException("User not found");

//            var post = new Post
//            {
//                Title = dto.Title ?? "",
//                Body = dto.Body ?? "",
//                UserId = user.UserId,
//                DeptId = user.DepartmentId,
//                CreatedAt = DateTime.UtcNow,
//                UpvoteCount = 0,
//                DownvoteCount = 0,
//                IsRepost = false
//            };

//            _db.Posts.Add(post);
//            await _db.SaveChangesAsync();

//            if (dto.Attachments != null && dto.Attachments.Any())
//            {
//                var webRoot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
//                var postsFolder = Path.Combine(webRoot, "posts");
//                if (!Directory.Exists(postsFolder)) Directory.CreateDirectory(postsFolder);

//                foreach (var file in dto.Attachments)
//                {
//                    if (file == null || file.Length == 0) continue;

//                    var clientFileName = Path.GetFileName(file.FileName);
//                    var safeFileName = SanitizeFileName(clientFileName);
//                    var targetPath = Path.Combine(postsFolder, safeFileName);

//                    if (System.IO.File.Exists(targetPath))
//                    {
//                        var ext = Path.GetExtension(safeFileName);
//                        var nameOnly = Path.GetFileNameWithoutExtension(safeFileName);
//                        safeFileName = $"{nameOnly}-{Guid.NewGuid().ToString("n").Substring(0, 8)}{ext}";
//                        targetPath = Path.Combine(postsFolder, safeFileName);
//                    }

//                    using (var stream = new FileStream(targetPath, FileMode.Create))
//                    {
//                        await file.CopyToAsync(stream);
//                    }

//                    var publicPath = $"/posts/{safeFileName}";

//                    var attachment = new Attachment
//                    {
//                        PostId = post.PostId,
//                        FileName = safeFileName,
//                        FilePath = publicPath,
//                        FileType = file.ContentType ?? "application/octet-stream",
//                        UploadedAt = DateTime.UtcNow
//                    };
//                    _db.Attachments.Add(attachment);
//                }
//                await _db.SaveChangesAsync();
//            }

//            // Reload with joins to build DTO
//            try
//            {
//                var savedPost = await _db.Posts
//                    .Include(p => p.User)
//                    .Include(p => p.Dept)
//                    .Include(p => p.PostTags).ThenInclude(pt => pt.Tag)
//                    .Include(p => p.Attachments)
//                    .FirstOrDefaultAsync(p => p.PostId == post.PostId);

//                if (savedPost == null)
//                {
//                    _logger?.LogWarning("Post {PostId} not found after creation", post.PostId);
//                    throw new InvalidOperationException($"Post {post.PostId} was not found after creation.");
//                }

//                var attachmentsList = savedPost.Attachments?.Where(a => a != null)
//                    .Select(a => a.FilePath).ToList() ?? new List<string>();

//                var tagsList = savedPost.PostTags?
//                    .Where(pt => pt != null && pt.Tag != null)
//                    .Select(pt => pt.Tag.TagName).ToList() ?? new List<string>();

//                return new PostResponseDto
//                {
//                    PostId = savedPost.PostId,
//                    Title = savedPost.Title,
//                    Body = savedPost.Body,
//                    AuthorName = savedPost.User?.FullName ?? "(unknown)",
//                    DepartmentName = savedPost.Dept?.DeptName ?? $"Dept {savedPost.DeptId}",
//                    UpvoteCount = savedPost.UpvoteCount,
//                    DownvoteCount = savedPost.DownvoteCount,
//                    Tags = tagsList,
//                    Attachments = attachmentsList,
//                    IsRepost = savedPost.IsRepost,
//                    CreatedAt = savedPost.CreatedAt
//                };
//            }
//            catch (Exception ex)
//            {
//                _logger?.LogError(ex, "Exception while reloading saved post {PostId}", post.PostId);

//                var attachments = await _db.Attachments.Where(a => a.PostId == post.PostId).ToListAsync();
//                var attachmentsList = attachments?.Select(a => a.FilePath).ToList() ?? new List<string>();

//                return new PostResponseDto
//                {
//                    PostId = post.PostId,
//                    Title = post.Title,
//                    Body = post.Body,
//                    AuthorName = user?.FullName ?? "(unknown)",
//                    DepartmentName = post.Dept?.DeptName ?? $"Dept {post.DeptId}",
//                    UpvoteCount = post.UpvoteCount,
//                    DownvoteCount = post.DownvoteCount,
//                    Tags = new List<string>(),
//                    Attachments = attachmentsList,
//                    IsRepost = post.IsRepost,
//                    CreatedAt = post.CreatedAt
//                };
//            }
//        }

//        public async Task<PostResponseDto> EditPostAsync(int userId, string role, int deptId, int postId, PostCreateDto dto)
//        {
//            var post = await _db.Posts
//                .Include(p => p.User)
//                .Include(p => p.Dept)
//                .Include(p => p.Attachments)
//                .Include(p => p.PostTags).ThenInclude(pt => pt.Tag)
//                .FirstOrDefaultAsync(p => p.PostId == postId) ?? throw new InvalidOperationException("Post not found");

//            if (string.Equals(role, "Employee", StringComparison.OrdinalIgnoreCase))
//            {
//                if (post.UserId != userId) throw new UnauthorizedAccessException();
//            }
//            else if (string.Equals(role, "Manager", StringComparison.OrdinalIgnoreCase))
//            {
//                if (post.DeptId != deptId) throw new UnauthorizedAccessException();
//            }
//            else throw new UnauthorizedAccessException();

//            post.Title = dto.Title ?? post.Title;
//            post.Body = dto.Body ?? post.Body;
//            post.UpdatedAt = DateTime.UtcNow;
//            await _db.SaveChangesAsync();

//            if (dto.Attachments != null && dto.Attachments.Any())
//            {
//                var webRoot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
//                var postsFolder = Path.Combine(webRoot, "posts");
//                if (!Directory.Exists(postsFolder)) Directory.CreateDirectory(postsFolder);

//                foreach (var file in dto.Attachments)
//                {
//                    if (file == null || file.Length == 0) continue;
//                    var clientFileName = Path.GetFileName(file.FileName);
//                    var safeFileName = SanitizeFileName(clientFileName);
//                    var targetPath = Path.Combine(postsFolder, safeFileName);
//                    if (System.IO.File.Exists(targetPath))
//                    {
//                        var ext = Path.GetExtension(safeFileName);
//                        var nameOnly = Path.GetFileNameWithoutExtension(safeFileName);
//                        safeFileName = $"{nameOnly}-{Guid.NewGuid().ToString("n").Substring(0, 8)}{ext}";
//                        targetPath = Path.Combine(postsFolder, safeFileName);
//                    }

//                    using (var stream = new FileStream(targetPath, FileMode.Create))
//                    {
//                        await file.CopyToAsync(stream);
//                    }

//                    var publicPath = $"/posts/{safeFileName}";
//                    var attachment = new Attachment
//                    {
//                        PostId = post.PostId,
//                        FileName = safeFileName,
//                        FilePath = publicPath,
//                        FileType = file.ContentType ?? "application/octet-stream",
//                        UploadedAt = DateTime.UtcNow
//                    };
//                    _db.Attachments.Add(attachment);
//                }
//                await _db.SaveChangesAsync();
//            }

//            var attachmentsReload = await _db.Attachments.Where(a => a.PostId == post.PostId).ToListAsync();

//            return new PostResponseDto
//            {
//                PostId = post.PostId,
//                Title = post.Title,
//                Body = post.Body,
//                AuthorName = post.User.FullName,
//                DepartmentName = post.Dept?.DeptName ?? $"Dept {post.DeptId}",
//                UpvoteCount = post.UpvoteCount,
//                DownvoteCount = post.DownvoteCount,
//                Tags = post.PostTags?.Select(pt => pt.Tag.TagName).ToList(),
//                Attachments = attachmentsReload.Select(a => a.FilePath).ToList(),
//                IsRepost = post.IsRepost,
//                CreatedAt = post.CreatedAt
//            };
//        }

//        public async Task DeletePostAsync(int userId, int deptId, int postId)
//        {
//            var post = await _db.Posts.FirstOrDefaultAsync(p => p.PostId == postId) ?? throw new InvalidOperationException("Post not found");
//            var user = await _db.Users.FindAsync(userId) ?? throw new InvalidOperationException("User not found");
//            if (!string.Equals(user.Role, "Manager", StringComparison.OrdinalIgnoreCase)) throw new UnauthorizedAccessException();
//            if (post.DeptId != deptId) throw new UnauthorizedAccessException();

//            _db.Posts.Remove(post);
//            await _db.SaveChangesAsync();
//        }

//        /// <summary>
//        /// Repost flow:
//        /// - Ensure original post exists
//        /// - Ensure user hasn't already reposted (Reposts table)
//        /// - Create Repost row
//        /// - Mark the original Post.IsRepost = true and save both changes in same SaveChanges call
//        /// - Return a PostResponseDto representing the repost (IsRepost = true)
//        /// </summary>
//        public async Task<PostResponseDto> RepostAsync(int currentUserId, RepostDto dto)
//        {
//            if (dto == null || dto.PostId <= 0) throw new InvalidOperationException("Invalid repost payload");

//            var user = await _db.Users.FindAsync(currentUserId) ?? throw new InvalidOperationException("User not found");

//            var post = await _db.Posts
//                .Include(p => p.User)
//                .Include(p => p.PostTags).ThenInclude(pt => pt.Tag)
//                .Include(p => p.Dept)
//                .Include(p => p.Attachments)
//                .FirstOrDefaultAsync(p => p.PostId == dto.PostId)
//                       ?? throw new InvalidOperationException("Original post not found");

//            // prevent duplicate repost by same user
//            var already = await _db.Reposts.AnyAsync(r => r.PostId == post.PostId && r.UserId == currentUserId);
//            if (already) throw new InvalidOperationException("Already reposted");

//            // create repost record
//            var repost = new Repost
//            {
//                PostId = post.PostId,
//                UserId = user.UserId,
//                CreatedAt = DateTime.UtcNow
//            };
//            _db.Reposts.Add(repost);

//            // Also set the original Post's IsRepost flag to true (persist it)
//            post.IsRepost = true;
//            _db.Posts.Update(post); // optional if tracked; safe to call

//            // Save both repost record and post flag in same transaction
//            await _db.SaveChangesAsync();

//            // Build PostResponseDto to return (representing the repost event)
//            return new PostResponseDto
//            {
//                PostId = post.PostId,
//                Title = $"[Repost by {user.FullName}] {post.Title}",
//                Body = post.Body,
//                AuthorName = post.User.FullName,
//                DepartmentName = post.Dept?.DeptName ?? $"Dept {post.DeptId}",
//                UpvoteCount = post.UpvoteCount,
//                DownvoteCount = post.DownvoteCount,
//                Tags = post.PostTags?.Select(t => t.Tag.TagName).ToList(),
//                Attachments = post.Attachments?.Select(a => a.FilePath).ToList(),
//                IsRepost = true,
//                CreatedAt = repost.CreatedAt
//            };
//        }

//        public async Task<List<PostResponseDto>> GetRepostsAsync(int postId)
//        {
//            var reposts = await _db.Reposts
//                .Include(r => r.User)
//                .Include(r => r.Post).ThenInclude(p => p.User)
//                .Include(r => r.Post).ThenInclude(p => p.Dept)
//                .Include(r => r.Post).ThenInclude(p => p.PostTags).ThenInclude(pt => pt.Tag)
//                .Include(r => r.Post).ThenInclude(p => p.Attachments)
//                .Where(r => r.PostId == postId).OrderByDescending(r => r.CreatedAt).ToListAsync();

//            return reposts.Select(r => new PostResponseDto
//            {
//                PostId = r.Post.PostId,
//                Title = $"[Repost by {r.User.FullName}] {r.Post.Title}",
//                Body = r.Post.Body,
//                AuthorName = r.Post.User.FullName,
//                DepartmentName = r.Post.Dept?.DeptName ?? $"Dept {r.Post.DeptId}",
//                UpvoteCount = r.Post.UpvoteCount,
//                DownvoteCount = r.Post.DownvoteCount,
//                Tags = r.Post.PostTags?.Select(t => t.Tag.TagName).ToList(),
//                Attachments = r.Post.Attachments?.Select(a => a.FilePath).ToList(),
//                IsRepost = true,
//                CreatedAt = r.CreatedAt
//            }).ToList();
//        }

//        public async Task<List<PostResponseDto>> GetRepostsByUserAsync(int userId)
//        {
//            var reposts = await _db.Reposts
//                .Where(r => r.UserId == userId)
//                .Include(r => r.User)
//                .Include(r => r.Post).ThenInclude(p => p.User)
//                .Include(r => r.Post).ThenInclude(p => p.Dept)
//                .Include(r => r.Post).ThenInclude(p => p.PostTags).ThenInclude(pt => pt.Tag)
//                .Include(r => r.Post).ThenInclude(p => p.Attachments)
//                .OrderByDescending(r => r.CreatedAt)
//                .ToListAsync();

//            return reposts.Select(r => new PostResponseDto
//            {
//                PostId = r.Post.PostId,
//                Title = $"[Repost by {r.User.FullName}] {r.Post.Title}",
//                Body = r.Post.Body,
//                AuthorName = r.Post.User.FullName,
//                DepartmentName = r.Post.Dept?.DeptName ?? $"Dept {r.Post.DeptId}",
//                UpvoteCount = r.Post.UpvoteCount,
//                DownvoteCount = r.Post.DownvoteCount,
//                Tags = r.Post.PostTags?.Select(t => t.Tag.TagName).ToList(),
//                Attachments = r.Post.Attachments?.Select(a => a.FilePath).ToList(),
//                IsRepost = true,
//                CreatedAt = r.CreatedAt
//            }).ToList();
//        }

//        public async Task<List<PostResponseDto>> GetAllPostsAsync()
//        {
//            var posts = await _db.Posts
//                .Include(p => p.User).Include(p => p.Dept)
//                .Include(p => p.PostTags).ThenInclude(pt => pt.Tag)
//                .Include(p => p.Attachments).ToListAsync();

//            var reposts = await _db.Reposts
//                .Include(r => r.User)
//                .Include(r => r.Post).ThenInclude(p => p.User)
//                .Include(r => r.Post).ThenInclude(p => p.Dept)
//                .Include(r => r.Post).ThenInclude(p => p.PostTags).ThenInclude(pt => pt.Tag)
//                .Include(r => r.Post).ThenInclude(p => p.Attachments).ToListAsync();

//            var postDtos = posts.Select(p => new PostResponseDto
//            {
//                PostId = p.PostId,
//                Title = p.Title,
//                Body = p.Body,
//                AuthorName = p.User.FullName,
//                DepartmentName = p.Dept?.DeptName ?? $"Dept {p.DeptId}",
//                UpvoteCount = p.UpvoteCount,
//                DownvoteCount = p.DownvoteCount,
//                Tags = p.PostTags?.Select(t => t.Tag.TagName).ToList(),
//                Attachments = p.Attachments?.Select(a => a.FilePath).ToList(),
//                IsRepost = p.IsRepost,
//                CreatedAt = p.CreatedAt
//            });

//            var repostDtos = reposts.Select(r => new PostResponseDto
//            {
//                PostId = r.Post.PostId,
//                Title = $"[Repost by {r.User.FullName}] {r.Post.Title}",
//                Body = r.Post.Body,
//                AuthorName = r.Post.User.FullName,
//                DepartmentName = r.Post.Dept?.DeptName ?? $"Dept {r.Post.DeptId}",
//                UpvoteCount = r.Post.UpvoteCount,
//                DownvoteCount = r.Post.DownvoteCount,
//                Tags = r.Post.PostTags?.Select(t => t.Tag.TagName).ToList(),
//                Attachments = r.Post.Attachments?.Select(a => a.FilePath).ToList(),
//                IsRepost = true,
//                CreatedAt = r.CreatedAt
//            });

//            return postDtos.Concat(repostDtos).OrderByDescending(x => x.CreatedAt).ToList();
//        }

//        public async Task<PostResponseDto?> GetPostByIdAsync(int postId)
//        {
//            var p = await _db.Posts
//                .Include(p => p.User).Include(p => p.Dept)
//                .Include(p => p.PostTags).ThenInclude(pt => pt.Tag)
//                .Include(p => p.Attachments)
//                .FirstOrDefaultAsync(p => p.PostId == postId);
//            if (p == null) return null;

//            return new PostResponseDto
//            {
//                PostId = p.PostId,
//                Title = p.Title,
//                Body = p.Body,
//                AuthorName = p.User.FullName,
//                DepartmentName = p.Dept?.DeptName ?? $"Dept {p.DeptId}",
//                UpvoteCount = p.UpvoteCount,
//                DownvoteCount = p.DownvoteCount,
//                Tags = p.PostTags?.Select(t => t.Tag.TagName).ToList(),
//                Attachments = p.Attachments?.Select(a => a.FilePath).ToList(),
//                IsRepost = p.IsRepost,
//                CreatedAt = p.CreatedAt
//            };
//        }

//        private static string SanitizeFileName(string filename)
//        {
//            var name = Path.GetFileName(filename);
//            foreach (var c in Path.GetInvalidFileNameChars())
//            {
//                name = name.Replace(c, '_');
//            }
//            return name;
//        }
//    }
//}


// File: Services/PostService.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using FNF_PROJ.Data;
using FNF_PROJ.DTOs;

namespace FNF_PROJ.Services
{
    // Interface + implementation in same file
    public interface IPostService
    {
        Task<PostResponseDto> CreatePostAsync(int userId, PostCreateDto dto);
        Task<PostResponseDto> EditPostAsync(int userId, string role, int deptId, int postId, PostCreateDto dto);
        Task DeletePostAsync(int userId, int deptId, int postId);
        Task<PostResponseDto> RepostAsync(int currentUserId, RepostDto dto);
        Task<List<PostResponseDto>> GetRepostsAsync(int postId);
        Task<List<PostResponseDto>> GetRepostsByUserAsync(int userId);
        Task<List<PostResponseDto>> GetAllPostsAsync();
        Task<PostResponseDto?> GetPostByIdAsync(int postId);
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

            if (dto.Attachments != null && dto.Attachments.Any())
            {
                var webRoot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
                var postsFolder = Path.Combine(webRoot, "posts");
                if (!Directory.Exists(postsFolder)) Directory.CreateDirectory(postsFolder);

                foreach (var file in dto.Attachments)
                {
                    if (file == null || file.Length == 0) continue;

                    var clientFileName = Path.GetFileName(file.FileName);
                    var safeFileName = SanitizeFileName(clientFileName);
                    var targetPath = Path.Combine(postsFolder, safeFileName);

                    if (System.IO.File.Exists(targetPath))
                    {
                        var ext = Path.GetExtension(safeFileName);
                        var nameOnly = Path.GetFileNameWithoutExtension(safeFileName);
                        safeFileName = $"{nameOnly}-{Guid.NewGuid().ToString("n").Substring(0, 8)}{ext}";
                        targetPath = Path.Combine(postsFolder, safeFileName);
                    }

                    using (var stream = new FileStream(targetPath, FileMode.Create))
                    {
                        await file.CopyToAsync(stream);
                    }

                    var publicPath = $"/posts/{safeFileName}";

                    var attachment = new Attachment
                    {
                        PostId = post.PostId,
                        FileName = safeFileName,
                        FilePath = publicPath,
                        FileType = file.ContentType ?? "application/octet-stream",
                        UploadedAt = DateTime.UtcNow
                    };
                    _db.Attachments.Add(attachment);
                }
                await _db.SaveChangesAsync();
            }

            // --- NEW: Tags: validate against user's department and persist PostTag rows ---
            if (dto.TagIds != null && dto.TagIds.Any())
            {
                var requestedTagIds = dto.TagIds.Distinct().ToList();

                var validTags = await _db.Tags
                    .Where(t => requestedTagIds.Contains(t.TagId) && t.DeptId == user.DepartmentId)
                    .ToListAsync();

                if (_logger != null && validTags.Count != requestedTagIds.Count)
                {
                    var invalid = requestedTagIds.Except(validTags.Select(t => t.TagId)).ToList();
                    _logger.LogWarning("User {UserId} attempted to attach invalid/out-of-dept tags: {InvalidTagIds}", userId, string.Join(",", invalid));
                }

                foreach (var tag in validTags)
                {
                    var exists = await _db.PostTags.AnyAsync(pt => pt.PostId == post.PostId && pt.TagId == tag.TagId);
                    if (!exists)
                    {
                        _db.PostTags.Add(new PostTag
                        {
                            PostId = post.PostId,
                            TagId = tag.TagId
                        });
                    }
                }

                await _db.SaveChangesAsync();
            }

            // Reload with joins to build DTO
            try
            {
                var savedPost = await _db.Posts
                    .Include(p => p.User)
                    .Include(p => p.Dept)
                    .Include(p => p.PostTags).ThenInclude(pt => pt.Tag)
                    .Include(p => p.Attachments)
                    .FirstOrDefaultAsync(p => p.PostId == post.PostId);

                if (savedPost == null)
                {
                    _logger?.LogWarning("Post {PostId} not found after creation", post.PostId);
                    throw new InvalidOperationException($"Post {post.PostId} was not found after creation.");
                }

                var attachmentsList = savedPost.Attachments?.Where(a => a != null)
                    .Select(a => a.FilePath).ToList() ?? new List<string>();

                var tagsList = savedPost.PostTags?
                    .Where(pt => pt != null && pt.Tag != null)
                    .Select(pt => pt.Tag.TagName).ToList() ?? new List<string>();

                return new PostResponseDto
                {
                    PostId = savedPost.PostId,
                    Title = savedPost.Title,
                    Body = savedPost.Body,
                    AuthorName = savedPost.User?.FullName ?? "(unknown)",
                    DepartmentName = savedPost.Dept?.DeptName ?? $"Dept {savedPost.DeptId}",
                    UpvoteCount = savedPost.UpvoteCount,
                    DownvoteCount = savedPost.DownvoteCount,
                    Tags = tagsList,
                    Attachments = attachmentsList,
                    IsRepost = savedPost.IsRepost,
                    CreatedAt = savedPost.CreatedAt
                };
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Exception while reloading saved post {PostId}", post.PostId);

                var attachments = await _db.Attachments.Where(a => a.PostId == post.PostId).ToListAsync();
                var attachmentsList = attachments?.Select(a => a.FilePath).ToList() ?? new List<string>();

                return new PostResponseDto
                {
                    PostId = post.PostId,
                    Title = post.Title,
                    Body = post.Body,
                    AuthorName = user?.FullName ?? "(unknown)",
                    DepartmentName = post.Dept?.DeptName ?? $"Dept {post.DeptId}",
                    UpvoteCount = post.UpvoteCount,
                    DownvoteCount = post.DownvoteCount,
                    Tags = new List<string>(),
                    Attachments = attachmentsList,
                    IsRepost = post.IsRepost,
                    CreatedAt = post.CreatedAt
                };
            }
        }

        public async Task<PostResponseDto> EditPostAsync(int userId, string role, int deptId, int postId, PostCreateDto dto)
        {
            var post = await _db.Posts
                .Include(p => p.User)
                .Include(p => p.Dept)
                .Include(p => p.Attachments)
                .Include(p => p.PostTags).ThenInclude(pt => pt.Tag)
                .FirstOrDefaultAsync(p => p.PostId == postId) ?? throw new InvalidOperationException("Post not found");

            if (string.Equals(role, "Employee", StringComparison.OrdinalIgnoreCase))
            {
                if (post.UserId != userId) throw new UnauthorizedAccessException();
            }
            else if (string.Equals(role, "Manager", StringComparison.OrdinalIgnoreCase))
            {
                if (post.DeptId != deptId) throw new UnauthorizedAccessException();
            }
            else throw new UnauthorizedAccessException();

            post.Title = dto.Title ?? post.Title;
            post.Body = dto.Body ?? post.Body;
            post.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            if (dto.Attachments != null && dto.Attachments.Any())
            {
                var webRoot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
                var postsFolder = Path.Combine(webRoot, "posts");
                if (!Directory.Exists(postsFolder)) Directory.CreateDirectory(postsFolder);

                foreach (var file in dto.Attachments)
                {
                    if (file == null || file.Length == 0) continue;
                    var clientFileName = Path.GetFileName(file.FileName);
                    var safeFileName = SanitizeFileName(clientFileName);
                    var targetPath = Path.Combine(postsFolder, safeFileName);
                    if (System.IO.File.Exists(targetPath))
                    {
                        var ext = Path.GetExtension(safeFileName);
                        var nameOnly = Path.GetFileNameWithoutExtension(safeFileName);
                        safeFileName = $"{nameOnly}-{Guid.NewGuid().ToString("n").Substring(0, 8)}{ext}";
                        targetPath = Path.Combine(postsFolder, safeFileName);
                    }

                    using (var stream = new FileStream(targetPath, FileMode.Create))
                    {
                        await file.CopyToAsync(stream);
                    }

                    var publicPath = $"/posts/{safeFileName}";
                    var attachment = new Attachment
                    {
                        PostId = post.PostId,
                        FileName = safeFileName,
                        FilePath = publicPath,
                        FileType = file.ContentType ?? "application/octet-stream",
                        UploadedAt = DateTime.UtcNow
                    };
                    _db.Attachments.Add(attachment);
                }
                await _db.SaveChangesAsync();
            }

            var attachmentsReload = await _db.Attachments.Where(a => a.PostId == post.PostId).ToListAsync();

            return new PostResponseDto
            {
                PostId = post.PostId,
                Title = post.Title,
                Body = post.Body,
                AuthorName = post.User.FullName,
                DepartmentName = post.Dept?.DeptName ?? $"Dept {post.DeptId}",
                UpvoteCount = post.UpvoteCount,
                DownvoteCount = post.DownvoteCount,
                Tags = post.PostTags?.Select(pt => pt.Tag.TagName).ToList(),
                Attachments = attachmentsReload.Select(a => a.FilePath).ToList(),
                IsRepost = post.IsRepost,
                CreatedAt = post.CreatedAt
            };
        }

        public async Task DeletePostAsync(int userId, int deptId, int postId)
        {
            var post = await _db.Posts.FirstOrDefaultAsync(p => p.PostId == postId) ?? throw new InvalidOperationException("Post not found");
            var user = await _db.Users.FindAsync(userId) ?? throw new InvalidOperationException("User not found");
            if (!string.Equals(user.Role, "Manager", StringComparison.OrdinalIgnoreCase)) throw new UnauthorizedAccessException();
            if (post.DeptId != deptId) throw new UnauthorizedAccessException();

            _db.Posts.Remove(post);
            await _db.SaveChangesAsync();
        }

        /// <summary>
        /// Repost flow:
        /// - Ensure original post exists
        /// - Ensure user hasn't already reposted (Reposts table)
        /// - Create Repost row
        /// - Mark the original Post.IsRepost = true and save both changes in same SaveChanges call
        /// - Return a PostResponseDto representing the repost (IsRepost = true)
        /// </summary>
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

            // prevent duplicate repost by same user
            var already = await _db.Reposts.AnyAsync(r => r.PostId == post.PostId && r.UserId == currentUserId);
            if (already) throw new InvalidOperationException("Already reposted");

            // create repost record
            var repost = new Repost
            {
                PostId = post.PostId,
                UserId = user.UserId,
                CreatedAt = DateTime.UtcNow
            };
            _db.Reposts.Add(repost);

            // Also set the original Post's IsRepost flag to true (persist it)
            post.IsRepost = true;
            _db.Posts.Update(post); // optional if tracked; safe to call

            // Save both repost record and post flag in same transaction
            await _db.SaveChangesAsync();

            // Build PostResponseDto to return (representing the repost event)
            return new PostResponseDto
            {
                PostId = post.PostId,
                Title = $"[Repost by {user.FullName}] {post.Title}",
                Body = post.Body,
                AuthorName = post.User.FullName,
                DepartmentName = post.Dept?.DeptName ?? $"Dept {post.DeptId}",
                UpvoteCount = post.UpvoteCount,
                DownvoteCount = post.DownvoteCount,
                Tags = post.PostTags?.Select(t => t.Tag.TagName).ToList(),
                Attachments = post.Attachments?.Select(a => a.FilePath).ToList(),
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
                .Where(r => r.PostId == postId).OrderByDescending(r => r.CreatedAt).ToListAsync();

            return reposts.Select(r => new PostResponseDto
            {
                PostId = r.Post.PostId,
                Title = $"[Repost by {r.User.FullName}] {r.Post.Title}",
                Body = r.Post.Body,
                AuthorName = r.Post.User.FullName,
                DepartmentName = r.Post.Dept?.DeptName ?? $"Dept {r.Post.DeptId}",
                UpvoteCount = r.Post.UpvoteCount,
                DownvoteCount = r.Post.DownvoteCount,
                Tags = r.Post.PostTags?.Select(t => t.Tag.TagName).ToList(),
                Attachments = r.Post.Attachments?.Select(a => a.FilePath).ToList(),
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
                AuthorName = r.Post.User.FullName,
                DepartmentName = r.Post.Dept?.DeptName ?? $"Dept {r.Post.DeptId}",
                UpvoteCount = r.Post.UpvoteCount,
                DownvoteCount = r.Post.DownvoteCount,
                Tags = r.Post.PostTags?.Select(t => t.Tag.TagName).ToList(),
                Attachments = r.Post.Attachments?.Select(a => a.FilePath).ToList(),
                IsRepost = true,
                CreatedAt = r.CreatedAt
            }).ToList();
        }

        public async Task<List<PostResponseDto>> GetAllPostsAsync()
        {
            var posts = await _db.Posts
                .Include(p => p.User).Include(p => p.Dept)
                .Include(p => p.PostTags).ThenInclude(pt => pt.Tag)
                .Include(p => p.Attachments).ToListAsync();

            var reposts = await _db.Reposts
                .Include(r => r.User)
                .Include(r => r.Post).ThenInclude(p => p.User)
                .Include(r => r.Post).ThenInclude(p => p.Dept)
                .Include(r => r.Post).ThenInclude(p => p.PostTags).ThenInclude(pt => pt.Tag)
                .Include(r => r.Post).ThenInclude(p => p.Attachments).ToListAsync();

            var postDtos = posts.Select(p => new PostResponseDto
            {
                PostId = p.PostId,
                Title = p.Title,
                Body = p.Body,
                AuthorName = p.User.FullName,
                DepartmentName = p.Dept?.DeptName ?? $"Dept {p.DeptId}",
                UpvoteCount = p.UpvoteCount,
                DownvoteCount = p.DownvoteCount,
                Tags = p.PostTags?.Select(t => t.Tag.TagName).ToList(),
                Attachments = p.Attachments?.Select(a => a.FilePath).ToList(),
                IsRepost = p.IsRepost,
                CreatedAt = p.CreatedAt
            });

            var repostDtos = reposts.Select(r => new PostResponseDto
            {
                PostId = r.Post.PostId,
                Title = $"[Repost by {r.User.FullName}] {r.Post.Title}",
                Body = r.Post.Body,
                AuthorName = r.Post.User.FullName,
                DepartmentName = r.Post.Dept?.DeptName ?? $"Dept {r.Post.DeptId}",
                UpvoteCount = r.Post.UpvoteCount,
                DownvoteCount = r.Post.DownvoteCount,
                Tags = r.Post.PostTags?.Select(t => t.Tag.TagName).ToList(),
                Attachments = r.Post.Attachments?.Select(a => a.FilePath).ToList(),
                IsRepost = true,
                CreatedAt = r.CreatedAt
            });

            return postDtos.Concat(repostDtos).OrderByDescending(x => x.CreatedAt).ToList();
        }

        public async Task<PostResponseDto?> GetPostByIdAsync(int postId)
        {
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
                AuthorName = p.User.FullName,
                DepartmentName = p.Dept?.DeptName ?? $"Dept {p.DeptId}",
                UpvoteCount = p.UpvoteCount,
                DownvoteCount = p.DownvoteCount,
                Tags = p.PostTags?.Select(t => t.Tag.TagName).ToList(),
                Attachments = p.Attachments?.Select(a => a.FilePath).ToList(),
                IsRepost = p.IsRepost,
                CreatedAt = p.CreatedAt
            };
        }

        private static string SanitizeFileName(string filename)
        {
            var name = Path.GetFileName(filename);
            foreach (var c in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(c, '_');
            }
            return name;
        }
    }
}
