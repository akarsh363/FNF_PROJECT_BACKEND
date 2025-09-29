
////////////////using System;
////////////////using System.Collections.Generic;
////////////////using System.IO;
////////////////using System.Linq;
////////////////using System.Threading.Tasks;
////////////////using Microsoft.EntityFrameworkCore;
////////////////using Microsoft.AspNetCore.Hosting;
////////////////using Microsoft.Extensions.Logging;
////////////////using FNF_PROJ.Data;
////////////////using FNF_PROJ.DTOs;

////////////////namespace FNF_PROJ.Services
////////////////{
////////////////    // Interface + implementation in same file
////////////////    public interface IPostService
////////////////    {
////////////////        Task<PostResponseDto> CreatePostAsync(int userId, PostCreateDto dto);
////////////////        Task<PostResponseDto> EditPostAsync(int userId, string role, int deptId, int postId, PostCreateDto dto);
////////////////        Task DeletePostAsync(int userId, int deptId, int postId);
////////////////        Task<PostResponseDto> RepostAsync(int currentUserId, RepostDto dto);
////////////////        Task<List<PostResponseDto>> GetRepostsAsync(int postId);
////////////////        Task<List<PostResponseDto>> GetRepostsByUserAsync(int userId);
////////////////        Task<List<PostResponseDto>> GetAllPostsAsync();
////////////////        Task<PostResponseDto?> GetPostByIdAsync(int postId);
////////////////    }

////////////////    public class PostService : IPostService
////////////////    {
////////////////        private readonly AppDbContext _db;
////////////////        private readonly IWebHostEnvironment _env;
////////////////        private readonly ILogger<PostService>? _logger;

////////////////        public PostService(AppDbContext db, IWebHostEnvironment env, ILogger<PostService>? logger = null)
////////////////        {
////////////////            _db = db;
////////////////            _env = env;
////////////////            _logger = logger;
////////////////        }

////////////////        public async Task<PostResponseDto> CreatePostAsync(int userId, PostCreateDto dto)
////////////////        {
////////////////            var user = await _db.Users.FindAsync(userId) ?? throw new InvalidOperationException("User not found");

////////////////            var post = new Post
////////////////            {
////////////////                Title = dto.Title ?? "",
////////////////                Body = dto.Body ?? "",
////////////////                UserId = user.UserId,
////////////////                DeptId = user.DepartmentId,
////////////////                CreatedAt = DateTime.UtcNow,
////////////////                UpvoteCount = 0,
////////////////                DownvoteCount = 0,
////////////////                IsRepost = false
////////////////            };

////////////////            _db.Posts.Add(post);
////////////////            await _db.SaveChangesAsync();

////////////////            if (dto.Attachments != null && dto.Attachments.Any())
////////////////            {
////////////////                var webRoot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
////////////////                var postsFolder = Path.Combine(webRoot, "posts");
////////////////                if (!Directory.Exists(postsFolder)) Directory.CreateDirectory(postsFolder);

////////////////                foreach (var file in dto.Attachments)
////////////////                {
////////////////                    if (file == null || file.Length == 0) continue;

////////////////                    var clientFileName = Path.GetFileName(file.FileName);
////////////////                    var safeFileName = SanitizeFileName(clientFileName);
////////////////                    var targetPath = Path.Combine(postsFolder, safeFileName);

////////////////                    if (System.IO.File.Exists(targetPath))
////////////////                    {
////////////////                        var ext = Path.GetExtension(safeFileName);
////////////////                        var nameOnly = Path.GetFileNameWithoutExtension(safeFileName);
////////////////                        safeFileName = $"{nameOnly}-{Guid.NewGuid().ToString("n").Substring(0, 8)}{ext}";
////////////////                        targetPath = Path.Combine(postsFolder, safeFileName);
////////////////                    }

////////////////                    using (var stream = new FileStream(targetPath, FileMode.Create))
////////////////                    {
////////////////                        await file.CopyToAsync(stream);
////////////////                    }

////////////////                    var publicPath = $"/posts/{safeFileName}";

////////////////                    var attachment = new Attachment
////////////////                    {
////////////////                        PostId = post.PostId,
////////////////                        FileName = safeFileName,
////////////////                        FilePath = publicPath,
////////////////                        FileType = file.ContentType ?? "application/octet-stream",
////////////////                        UploadedAt = DateTime.UtcNow
////////////////                    };
////////////////                    _db.Attachments.Add(attachment);
////////////////                }
////////////////                await _db.SaveChangesAsync();
////////////////            }

////////////////            // --- NEW: Tags: validate against user's department and persist PostTag rows ---
////////////////            if (dto.TagIds != null && dto.TagIds.Any())
////////////////            {
////////////////                var requestedTagIds = dto.TagIds.Distinct().ToList();

////////////////                var validTags = await _db.Tags
////////////////                    .Where(t => requestedTagIds.Contains(t.TagId) && t.DeptId == user.DepartmentId)
////////////////                    .ToListAsync();

////////////////                if (_logger != null && validTags.Count != requestedTagIds.Count)
////////////////                {
////////////////                    var invalid = requestedTagIds.Except(validTags.Select(t => t.TagId)).ToList();
////////////////                    _logger.LogWarning("User {UserId} attempted to attach invalid/out-of-dept tags: {InvalidTagIds}", userId, string.Join(",", invalid));
////////////////                }

////////////////                foreach (var tag in validTags)
////////////////                {
////////////////                    var exists = await _db.PostTags.AnyAsync(pt => pt.PostId == post.PostId && pt.TagId == tag.TagId);
////////////////                    if (!exists)
////////////////                    {
////////////////                        _db.PostTags.Add(new PostTag
////////////////                        {
////////////////                            PostId = post.PostId,
////////////////                            TagId = tag.TagId
////////////////                        });
////////////////                    }
////////////////                }

////////////////                await _db.SaveChangesAsync();
////////////////            }

////////////////            // Reload with joins to build DTO
////////////////            try
////////////////            {
////////////////                var savedPost = await _db.Posts
////////////////                    .Include(p => p.User)
////////////////                    .Include(p => p.Dept)
////////////////                    .Include(p => p.PostTags).ThenInclude(pt => pt.Tag)
////////////////                    .Include(p => p.Attachments)
////////////////                    .FirstOrDefaultAsync(p => p.PostId == post.PostId);

////////////////                if (savedPost == null)
////////////////                {
////////////////                    _logger?.LogWarning("Post {PostId} not found after creation", post.PostId);
////////////////                    throw new InvalidOperationException($"Post {post.PostId} was not found after creation.");
////////////////                }

////////////////                var attachmentsList = savedPost.Attachments?.Where(a => a != null)
////////////////                    .Select(a => a.FilePath).ToList() ?? new List<string>();

////////////////                var tagsList = savedPost.PostTags?
////////////////                    .Where(pt => pt != null && pt.Tag != null)
////////////////                    .Select(pt => pt.Tag.TagName).ToList() ?? new List<string>();

////////////////                return new PostResponseDto
////////////////                {
////////////////                    PostId = savedPost.PostId,
////////////////                    Title = savedPost.Title,
////////////////                    Body = savedPost.Body,
////////////////                    AuthorName = savedPost.User?.FullName ?? "(unknown)",
////////////////                    DepartmentName = savedPost.Dept?.DeptName ?? $"Dept {savedPost.DeptId}",
////////////////                    UpvoteCount = savedPost.UpvoteCount,
////////////////                    DownvoteCount = savedPost.DownvoteCount,
////////////////                    Tags = tagsList,
////////////////                    Attachments = attachmentsList,
////////////////                    IsRepost = savedPost.IsRepost,
////////////////                    CreatedAt = savedPost.CreatedAt
////////////////                };
////////////////            }
////////////////            catch (Exception ex)
////////////////            {
////////////////                _logger?.LogError(ex, "Exception while reloading saved post {PostId}", post.PostId);

////////////////                var attachments = await _db.Attachments.Where(a => a.PostId == post.PostId).ToListAsync();
////////////////                var attachmentsList = attachments?.Select(a => a.FilePath).ToList() ?? new List<string>();

////////////////                return new PostResponseDto
////////////////                {
////////////////                    PostId = post.PostId,
////////////////                    Title = post.Title,
////////////////                    Body = post.Body,
////////////////                    AuthorName = user?.FullName ?? "(unknown)",
////////////////                    DepartmentName = post.Dept?.DeptName ?? $"Dept {post.DeptId}",
////////////////                    UpvoteCount = post.UpvoteCount,
////////////////                    DownvoteCount = post.DownvoteCount,
////////////////                    Tags = new List<string>(),
////////////////                    Attachments = attachmentsList,
////////////////                    IsRepost = post.IsRepost,
////////////////                    CreatedAt = post.CreatedAt
////////////////                };
////////////////            }
////////////////        }

////////////////        public async Task<PostResponseDto> EditPostAsync(int userId, string role, int deptId, int postId, PostCreateDto dto)
////////////////        {
////////////////            var post = await _db.Posts
////////////////                .Include(p => p.User)
////////////////                .Include(p => p.Dept)
////////////////                .Include(p => p.Attachments)
////////////////                .Include(p => p.PostTags).ThenInclude(pt => pt.Tag)
////////////////                .FirstOrDefaultAsync(p => p.PostId == postId) ?? throw new InvalidOperationException("Post not found");

////////////////            if (string.Equals(role, "Employee", StringComparison.OrdinalIgnoreCase))
////////////////            {
////////////////                if (post.UserId != userId) throw new UnauthorizedAccessException();
////////////////            }
////////////////            else if (string.Equals(role, "Manager", StringComparison.OrdinalIgnoreCase))
////////////////            {
////////////////                if (post.DeptId != deptId) throw new UnauthorizedAccessException();
////////////////            }
////////////////            else throw new UnauthorizedAccessException();

////////////////            post.Title = dto.Title ?? post.Title;
////////////////            post.Body = dto.Body ?? post.Body;
////////////////            post.UpdatedAt = DateTime.UtcNow;
////////////////            await _db.SaveChangesAsync();

////////////////            if (dto.Attachments != null && dto.Attachments.Any())
////////////////            {
////////////////                var webRoot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
////////////////                var postsFolder = Path.Combine(webRoot, "posts");
////////////////                if (!Directory.Exists(postsFolder)) Directory.CreateDirectory(postsFolder);

////////////////                foreach (var file in dto.Attachments)
////////////////                {
////////////////                    if (file == null || file.Length == 0) continue;
////////////////                    var clientFileName = Path.GetFileName(file.FileName);
////////////////                    var safeFileName = SanitizeFileName(clientFileName);
////////////////                    var targetPath = Path.Combine(postsFolder, safeFileName);
////////////////                    if (System.IO.File.Exists(targetPath))
////////////////                    {
////////////////                        var ext = Path.GetExtension(safeFileName);
////////////////                        var nameOnly = Path.GetFileNameWithoutExtension(safeFileName);
////////////////                        safeFileName = $"{nameOnly}-{Guid.NewGuid().ToString("n").Substring(0, 8)}{ext}";
////////////////                        targetPath = Path.Combine(postsFolder, safeFileName);
////////////////                    }

////////////////                    using (var stream = new FileStream(targetPath, FileMode.Create))
////////////////                    {
////////////////                        await file.CopyToAsync(stream);
////////////////                    }

////////////////                    var publicPath = $"/posts/{safeFileName}";
////////////////                    var attachment = new Attachment
////////////////                    {
////////////////                        PostId = post.PostId,
////////////////                        FileName = safeFileName,
////////////////                        FilePath = publicPath,
////////////////                        FileType = file.ContentType ?? "application/octet-stream",
////////////////                        UploadedAt = DateTime.UtcNow
////////////////                    };
////////////////                    _db.Attachments.Add(attachment);
////////////////                }
////////////////                await _db.SaveChangesAsync();
////////////////            }

////////////////            var attachmentsReload = await _db.Attachments.Where(a => a.PostId == post.PostId).ToListAsync();

////////////////            return new PostResponseDto
////////////////            {
////////////////                PostId = post.PostId,
////////////////                Title = post.Title,
////////////////                Body = post.Body,
////////////////                AuthorName = post.User.FullName,
////////////////                DepartmentName = post.Dept?.DeptName ?? $"Dept {post.DeptId}",
////////////////                UpvoteCount = post.UpvoteCount,
////////////////                DownvoteCount = post.DownvoteCount,
////////////////                Tags = post.PostTags?.Select(pt => pt.Tag.TagName).ToList(),
////////////////                Attachments = attachmentsReload.Select(a => a.FilePath).ToList(),
////////////////                IsRepost = post.IsRepost,
////////////////                CreatedAt = post.CreatedAt
////////////////            };
////////////////        }

////////////////        public async Task DeletePostAsync(int userId, int deptId, int postId)
////////////////        {
////////////////            var post = await _db.Posts.FirstOrDefaultAsync(p => p.PostId == postId) ?? throw new InvalidOperationException("Post not found");
////////////////            var user = await _db.Users.FindAsync(userId) ?? throw new InvalidOperationException("User not found");
////////////////            if (!string.Equals(user.Role, "Manager", StringComparison.OrdinalIgnoreCase)) throw new UnauthorizedAccessException();
////////////////            if (post.DeptId != deptId) throw new UnauthorizedAccessException();

////////////////            _db.Posts.Remove(post);
////////////////            await _db.SaveChangesAsync();
////////////////        }

////////////////        /// <summary>
////////////////        /// Repost flow:
////////////////        /// - Ensure original post exists
////////////////        /// - Ensure user hasn't already reposted (Reposts table)
////////////////        /// - Create Repost row
////////////////        /// - Mark the original Post.IsRepost = true and save both changes in same SaveChanges call
////////////////        /// - Return a PostResponseDto representing the repost (IsRepost = true)
////////////////        /// </summary>
////////////////        public async Task<PostResponseDto> RepostAsync(int currentUserId, RepostDto dto)
////////////////        {
////////////////            if (dto == null || dto.PostId <= 0) throw new InvalidOperationException("Invalid repost payload");

////////////////            var user = await _db.Users.FindAsync(currentUserId) ?? throw new InvalidOperationException("User not found");

////////////////            var post = await _db.Posts
////////////////                .Include(p => p.User)
////////////////                .Include(p => p.PostTags).ThenInclude(pt => pt.Tag)
////////////////                .Include(p => p.Dept)
////////////////                .Include(p => p.Attachments)
////////////////                .FirstOrDefaultAsync(p => p.PostId == dto.PostId)
////////////////                       ?? throw new InvalidOperationException("Original post not found");

////////////////            // prevent duplicate repost by same user
////////////////            var already = await _db.Reposts.AnyAsync(r => r.PostId == post.PostId && r.UserId == currentUserId);
////////////////            if (already) throw new InvalidOperationException("Already reposted");

////////////////            // create repost record
////////////////            var repost = new Repost
////////////////            {
////////////////                PostId = post.PostId,
////////////////                UserId = user.UserId,
////////////////                CreatedAt = DateTime.UtcNow
////////////////            };
////////////////            _db.Reposts.Add(repost);

////////////////            // Also set the original Post's IsRepost flag to true (persist it)
////////////////            post.IsRepost = true;
////////////////            _db.Posts.Update(post); // optional if tracked; safe to call

////////////////            // Save both repost record and post flag in same transaction
////////////////            await _db.SaveChangesAsync();

////////////////            // Build PostResponseDto to return (representing the repost event)
////////////////            return new PostResponseDto
////////////////            {
////////////////                PostId = post.PostId,
////////////////                Title = $"[Repost by {user.FullName}] {post.Title}",
////////////////                Body = post.Body,
////////////////                AuthorName = post.User.FullName,
////////////////                DepartmentName = post.Dept?.DeptName ?? $"Dept {post.DeptId}",
////////////////                UpvoteCount = post.UpvoteCount,
////////////////                DownvoteCount = post.DownvoteCount,
////////////////                Tags = post.PostTags?.Select(t => t.Tag.TagName).ToList(),
////////////////                Attachments = post.Attachments?.Select(a => a.FilePath).ToList(),
////////////////                IsRepost = true,
////////////////                CreatedAt = repost.CreatedAt
////////////////            };
////////////////        }

////////////////        public async Task<List<PostResponseDto>> GetRepostsAsync(int postId)
////////////////        {
////////////////            var reposts = await _db.Reposts
////////////////                .Include(r => r.User)
////////////////                .Include(r => r.Post).ThenInclude(p => p.User)
////////////////                .Include(r => r.Post).ThenInclude(p => p.Dept)
////////////////                .Include(r => r.Post).ThenInclude(p => p.PostTags).ThenInclude(pt => pt.Tag)
////////////////                .Include(r => r.Post).ThenInclude(p => p.Attachments)
////////////////                .Where(r => r.PostId == postId).OrderByDescending(r => r.CreatedAt).ToListAsync();

////////////////            return reposts.Select(r => new PostResponseDto
////////////////            {
////////////////                PostId = r.Post.PostId,
////////////////                Title = $"[Repost by {r.User.FullName}] {r.Post.Title}",
////////////////                Body = r.Post.Body,
////////////////                AuthorName = r.Post.User.FullName,
////////////////                DepartmentName = r.Post.Dept?.DeptName ?? $"Dept {r.Post.DeptId}",
////////////////                UpvoteCount = r.Post.UpvoteCount,
////////////////                DownvoteCount = r.Post.DownvoteCount,
////////////////                Tags = r.Post.PostTags?.Select(t => t.Tag.TagName).ToList(),
////////////////                Attachments = r.Post.Attachments?.Select(a => a.FilePath).ToList(),
////////////////                IsRepost = true,
////////////////                CreatedAt = r.CreatedAt
////////////////            }).ToList();
////////////////        }

////////////////        public async Task<List<PostResponseDto>> GetRepostsByUserAsync(int userId)
////////////////        {
////////////////            var reposts = await _db.Reposts
////////////////                .Where(r => r.UserId == userId)
////////////////                .Include(r => r.User)
////////////////                .Include(r => r.Post).ThenInclude(p => p.User)
////////////////                .Include(r => r.Post).ThenInclude(p => p.Dept)
////////////////                .Include(r => r.Post).ThenInclude(p => p.PostTags).ThenInclude(pt => pt.Tag)
////////////////                .Include(r => r.Post).ThenInclude(p => p.Attachments)
////////////////                .OrderByDescending(r => r.CreatedAt)
////////////////                .ToListAsync();

////////////////            return reposts.Select(r => new PostResponseDto
////////////////            {
////////////////                PostId = r.Post.PostId,
////////////////                Title = $"[Repost by {r.User.FullName}] {r.Post.Title}",
////////////////                Body = r.Post.Body,
////////////////                AuthorName = r.Post.User.FullName,
////////////////                DepartmentName = r.Post.Dept?.DeptName ?? $"Dept {r.Post.DeptId}",
////////////////                UpvoteCount = r.Post.UpvoteCount,
////////////////                DownvoteCount = r.Post.DownvoteCount,
////////////////                Tags = r.Post.PostTags?.Select(t => t.Tag.TagName).ToList(),
////////////////                Attachments = r.Post.Attachments?.Select(a => a.FilePath).ToList(),
////////////////                IsRepost = true,
////////////////                CreatedAt = r.CreatedAt
////////////////            }).ToList();
////////////////        }

////////////////        public async Task<List<PostResponseDto>> GetAllPostsAsync()
////////////////        {
////////////////            var posts = await _db.Posts
////////////////                .Include(p => p.User).Include(p => p.Dept)
////////////////                .Include(p => p.PostTags).ThenInclude(pt => pt.Tag)
////////////////                .Include(p => p.Attachments).ToListAsync();

////////////////            var reposts = await _db.Reposts
////////////////                .Include(r => r.User)
////////////////                .Include(r => r.Post).ThenInclude(p => p.User)
////////////////                .Include(r => r.Post).ThenInclude(p => p.Dept)
////////////////                .Include(r => r.Post).ThenInclude(p => p.PostTags).ThenInclude(pt => pt.Tag)
////////////////                .Include(r => r.Post).ThenInclude(p => p.Attachments).ToListAsync();

////////////////            var postDtos = posts.Select(p => new PostResponseDto
////////////////            {
////////////////                PostId = p.PostId,
////////////////                Title = p.Title,
////////////////                Body = p.Body,
////////////////                AuthorName = p.User.FullName,
////////////////                DepartmentName = p.Dept?.DeptName ?? $"Dept {p.DeptId}",
////////////////                UpvoteCount = p.UpvoteCount,
////////////////                DownvoteCount = p.DownvoteCount,
////////////////                Tags = p.PostTags?.Select(t => t.Tag.TagName).ToList(),
////////////////                Attachments = p.Attachments?.Select(a => a.FilePath).ToList(),
////////////////                IsRepost = p.IsRepost,
////////////////                CreatedAt = p.CreatedAt
////////////////            });

////////////////            var repostDtos = reposts.Select(r => new PostResponseDto
////////////////            {
////////////////                PostId = r.Post.PostId,
////////////////                Title = $"[Repost by {r.User.FullName}] {r.Post.Title}",
////////////////                Body = r.Post.Body,
////////////////                AuthorName = r.Post.User.FullName,
////////////////                DepartmentName = r.Post.Dept?.DeptName ?? $"Dept {r.Post.DeptId}",
////////////////                UpvoteCount = r.Post.UpvoteCount,
////////////////                DownvoteCount = r.Post.DownvoteCount,
////////////////                Tags = r.Post.PostTags?.Select(t => t.Tag.TagName).ToList(),
////////////////                Attachments = r.Post.Attachments?.Select(a => a.FilePath).ToList(),
////////////////                IsRepost = true,
////////////////                CreatedAt = r.CreatedAt
////////////////            });

////////////////            return postDtos.Concat(repostDtos).OrderByDescending(x => x.CreatedAt).ToList();
////////////////        }

////////////////        public async Task<PostResponseDto?> GetPostByIdAsync(int postId)
////////////////        {
////////////////            var p = await _db.Posts
////////////////                .Include(p => p.User).Include(p => p.Dept)
////////////////                .Include(p => p.PostTags).ThenInclude(pt => pt.Tag)
////////////////                .Include(p => p.Attachments)
////////////////                .FirstOrDefaultAsync(p => p.PostId == postId);
////////////////            if (p == null) return null;

////////////////            return new PostResponseDto
////////////////            {
////////////////                PostId = p.PostId,
////////////////                Title = p.Title,
////////////////                Body = p.Body,
////////////////                AuthorName = p.User.FullName,
////////////////                DepartmentName = p.Dept?.DeptName ?? $"Dept {p.DeptId}",
////////////////                UpvoteCount = p.UpvoteCount,
////////////////                DownvoteCount = p.DownvoteCount,
////////////////                Tags = p.PostTags?.Select(t => t.Tag.TagName).ToList(),
////////////////                Attachments = p.Attachments?.Select(a => a.FilePath).ToList(),
////////////////                IsRepost = p.IsRepost,
////////////////                CreatedAt = p.CreatedAt
////////////////            };
////////////////        }

////////////////        private static string SanitizeFileName(string filename)
////////////////        {
////////////////            var name = Path.GetFileName(filename);
////////////////            foreach (var c in Path.GetInvalidFileNameChars())
////////////////            {
////////////////                name = name.Replace(c, '_');
////////////////            }
////////////////            return name;
////////////////        }
////////////////    }
////////////////}



//////////////// File: Services/PostService.cs
//////////////using System;
//////////////using System.Collections.Generic;
//////////////using System.IO;
//////////////using System.Linq;
//////////////using System.Threading.Tasks;
//////////////using Microsoft.AspNetCore.Hosting;
//////////////using Microsoft.EntityFrameworkCore;
//////////////using Microsoft.Extensions.Logging;
//////////////using FNF_PROJ.Data;
//////////////using FNF_PROJ.DTOs;

//////////////namespace FNF_PROJ.Services
//////////////{
//////////////    // -------------------- Interface (same file) --------------------
//////////////    public interface IPostService
//////////////    {
//////////////        Task<PostResponseDto> CreatePostAsync(int userId, PostCreateDto dto);
//////////////        Task<PostResponseDto> EditPostAsync(int userId, string role, int deptId, int postId, PostCreateDto dto);
//////////////        Task DeletePostAsync(int userId, int deptId, int postId, string reason);          // manager-only, with reason
//////////////        Task<PostResponseDto> RepostAsync(int currentUserId, RepostDto dto);
//////////////        Task<List<PostResponseDto>> GetRepostsAsync(int postId);
//////////////        Task<List<PostResponseDto>> GetRepostsByUserAsync(int userId);
//////////////        Task<List<PostResponseDto>> GetAllPostsAsync();
//////////////        Task<PostResponseDto?> GetPostByIdAsync(int postId);
//////////////        Task<List<PostResponseDto>> GetMyPostsAsync(int userId);                           // only my posts
//////////////    }

//////////////    // -------------------- Implementation --------------------
//////////////    public class PostService : IPostService
//////////////    {
//////////////        private readonly AppDbContext _db;
//////////////        private readonly IWebHostEnvironment _env;
//////////////        private readonly ILogger<PostService>? _logger;

//////////////        public PostService(AppDbContext db, IWebHostEnvironment env, ILogger<PostService>? logger = null)
//////////////        {
//////////////            _db = db;
//////////////            _env = env;
//////////////            _logger = logger;
//////////////        }

//////////////        // CREATE
//////////////        public async Task<PostResponseDto> CreatePostAsync(int userId, PostCreateDto dto)
//////////////        {
//////////////            var user = await _db.Users.FindAsync(userId) ?? throw new InvalidOperationException("User not found");

//////////////            var post = new Post
//////////////            {
//////////////                Title = dto.Title ?? "",
//////////////                Body = dto.Body ?? "",
//////////////                UserId = user.UserId,
//////////////                DeptId = user.DepartmentId,
//////////////                CreatedAt = DateTime.UtcNow,
//////////////                UpvoteCount = 0,
//////////////                DownvoteCount = 0,
//////////////                IsRepost = false
//////////////            };

//////////////            _db.Posts.Add(post);
//////////////            await _db.SaveChangesAsync();

//////////////            // Attachments
//////////////            if (dto.Attachments != null && dto.Attachments.Any())
//////////////            {
//////////////                var webRoot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
//////////////                var postsFolder = Path.Combine(webRoot, "posts");
//////////////                if (!Directory.Exists(postsFolder)) Directory.CreateDirectory(postsFolder);

//////////////                foreach (var file in dto.Attachments)
//////////////                {
//////////////                    if (file == null || file.Length == 0) continue;

//////////////                    var clientFileName = Path.GetFileName(file.FileName);
//////////////                    var safeFileName = SanitizeFileName(clientFileName);
//////////////                    var targetPath = Path.Combine(postsFolder, safeFileName);

//////////////                    if (File.Exists(targetPath))
//////////////                    {
//////////////                        var ext = Path.GetExtension(safeFileName);
//////////////                        var nameOnly = Path.GetFileNameWithoutExtension(safeFileName);
//////////////                        safeFileName = $"{nameOnly}-{Guid.NewGuid():N}".Substring(0, nameOnly.Length + 9) + ext;
//////////////                        targetPath = Path.Combine(postsFolder, safeFileName);
//////////////                    }

//////////////                    using var stream = new FileStream(targetPath, FileMode.Create);
//////////////                    await file.CopyToAsync(stream);

//////////////                    var publicPath = $"/posts/{safeFileName}";
//////////////                    _db.Attachments.Add(new Attachment
//////////////                    {
//////////////                        PostId = post.PostId,
//////////////                        FileName = safeFileName,
//////////////                        FilePath = publicPath,
//////////////                        FileType = file.ContentType ?? "application/octet-stream",
//////////////                        UploadedAt = DateTime.UtcNow
//////////////                    });
//////////////                }
//////////////                await _db.SaveChangesAsync();
//////////////            }

//////////////            // Tags (dept-scoped)
//////////////            if (dto.TagIds != null && dto.TagIds.Any())
//////////////            {
//////////////                var requestedTagIds = dto.TagIds.Distinct().ToList();
//////////////                var validTags = await _db.Tags
//////////////                    .Where(t => requestedTagIds.Contains(t.TagId) && t.DeptId == user.DepartmentId)
//////////////                    .ToListAsync();

//////////////                if (_logger != null && validTags.Count != requestedTagIds.Count)
//////////////                {
//////////////                    var invalid = requestedTagIds.Except(validTags.Select(t => t.TagId)).ToList();
//////////////                    _logger.LogWarning("User {UserId} attempted invalid/out-of-dept tags: {InvalidTagIds}", userId, string.Join(",", invalid));
//////////////                }

//////////////                foreach (var tag in validTags)
//////////////                {
//////////////                    if (!await _db.PostTags.AnyAsync(pt => pt.PostId == post.PostId && pt.TagId == tag.TagId))
//////////////                        _db.PostTags.Add(new PostTag { PostId = post.PostId, TagId = tag.TagId });
//////////////                }
//////////////                await _db.SaveChangesAsync();
//////////////            }

//////////////            // Return hydrated DTO
//////////////            var savedPost = await _db.Posts
//////////////                .Include(p => p.User)
//////////////                .Include(p => p.Dept)
//////////////                .Include(p => p.PostTags).ThenInclude(pt => pt.Tag)
//////////////                .Include(p => p.Attachments)
//////////////                .FirstOrDefaultAsync(p => p.PostId == post.PostId);

//////////////            if (savedPost == null) throw new InvalidOperationException("Post not found after creation.");

//////////////            return ToDto(savedPost);
//////////////        }

//////////////        // EDIT (Employee: own post; Manager: same dept)
//////////////        public async Task<PostResponseDto> EditPostAsync(int userId, string role, int deptId, int postId, PostCreateDto dto)
//////////////        {
//////////////            var post = await _db.Posts
//////////////                .Include(p => p.User).Include(p => p.Dept)
//////////////                .Include(p => p.Attachments)
//////////////                .Include(p => p.PostTags).ThenInclude(pt => pt.Tag)
//////////////                .FirstOrDefaultAsync(p => p.PostId == postId)
//////////////                ?? throw new InvalidOperationException("Post not found");

//////////////            var isEmployee = string.Equals(role, "Employee", StringComparison.OrdinalIgnoreCase);
//////////////            var isManager = string.Equals(role, "Manager", StringComparison.OrdinalIgnoreCase);

//////////////            if (isEmployee && post.UserId != userId) throw new UnauthorizedAccessException();
//////////////            if (isManager && post.DeptId != (deptId == 0 ? post.DeptId : deptId)) throw new UnauthorizedAccessException();
//////////////            if (!isEmployee && !isManager) throw new UnauthorizedAccessException();

//////////////            post.Title = dto.Title ?? post.Title;
//////////////            post.Body = dto.Body ?? post.Body;
//////////////            post.UpdatedAt = DateTime.UtcNow;
//////////////            await _db.SaveChangesAsync();

//////////////            // Optional: allow adding new attachments on edit
//////////////            if (dto.Attachments != null && dto.Attachments.Any())
//////////////            {
//////////////                var webRoot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
//////////////                var postsFolder = Path.Combine(webRoot, "posts");
//////////////                if (!Directory.Exists(postsFolder)) Directory.CreateDirectory(postsFolder);

//////////////                foreach (var file in dto.Attachments)
//////////////                {
//////////////                    if (file == null || file.Length == 0) continue;

//////////////                    var clientFileName = Path.GetFileName(file.FileName);
//////////////                    var safeFileName = SanitizeFileName(clientFileName);
//////////////                    var targetPath = Path.Combine(postsFolder, safeFileName);

//////////////                    if (File.Exists(targetPath))
//////////////                    {
//////////////                        var ext = Path.GetExtension(safeFileName);
//////////////                        var nameOnly = Path.GetFileNameWithoutExtension(safeFileName);
//////////////                        safeFileName = $"{nameOnly}-{Guid.NewGuid():N}".Substring(0, nameOnly.Length + 9) + ext;
//////////////                        targetPath = Path.Combine(postsFolder, safeFileName);
//////////////                    }

//////////////                    using var stream = new FileStream(targetPath, FileMode.Create);
//////////////                    await file.CopyToAsync(stream);

//////////////                    var publicPath = $"/posts/{safeFileName}";
//////////////                    _db.Attachments.Add(new Attachment
//////////////                    {
//////////////                        PostId = post.PostId,
//////////////                        FileName = safeFileName,
//////////////                        FilePath = publicPath,
//////////////                        FileType = file.ContentType ?? "application/octet-stream",
//////////////                        UploadedAt = DateTime.UtcNow
//////////////                    });
//////////////                }
//////////////                await _db.SaveChangesAsync();
//////////////            }

//////////////            return await GetPostByIdAsync(post.PostId) ?? ToDto(post);
//////////////        }

//////////////        // DELETE (Manager-only) + commit log (reason required)
//////////////        public async Task DeletePostAsync(int userId, int deptId, int postId, string reason)
//////////////        {
//////////////            if (string.IsNullOrWhiteSpace(reason))
//////////////                throw new InvalidOperationException("Delete reason is required.");

//////////////            var post = await _db.Posts.FirstOrDefaultAsync(p => p.PostId == postId)
//////////////                       ?? throw new InvalidOperationException("Post not found");
//////////////            var manager = await _db.Users.FindAsync(userId)
//////////////                          ?? throw new InvalidOperationException("User not found");

//////////////            if (!string.Equals(manager.Role, "Manager", StringComparison.OrdinalIgnoreCase))
//////////////                throw new UnauthorizedAccessException("Only Managers can delete posts.");

//////////////            var managerDept = manager.DepartmentId;
//////////////            if (post.DeptId != managerDept || (deptId != 0 && deptId != managerDept))
//////////////                throw new UnauthorizedAccessException("Manager can delete only posts from their own department.");

//////////////            // Log commit (moderation)
//////////////            _db.Commits.Add(new Commit
//////////////            {
//////////////                PostId = post.PostId,
//////////////                ManagerId = manager.UserId,
//////////////                Action = "DELETE",
//////////////                Reason = reason.Trim(),
//////////////                CreatedAt = DateTime.UtcNow
//////////////            });

//////////////            _db.Posts.Remove(post);
//////////////            await _db.SaveChangesAsync();
//////////////        }

//////////////        // REPOST
//////////////        public async Task<PostResponseDto> RepostAsync(int currentUserId, RepostDto dto)
//////////////        {
//////////////            if (dto == null || dto.PostId <= 0) throw new InvalidOperationException("Invalid repost payload");

//////////////            var user = await _db.Users.FindAsync(currentUserId) ?? throw new InvalidOperationException("User not found");

//////////////            var post = await _db.Posts
//////////////                .Include(p => p.User)
//////////////                .Include(p => p.PostTags).ThenInclude(pt => pt.Tag)
//////////////                .Include(p => p.Dept)
//////////////                .Include(p => p.Attachments)
//////////////                .FirstOrDefaultAsync(p => p.PostId == dto.PostId)
//////////////                ?? throw new InvalidOperationException("Original post not found");

//////////////            var already = await _db.Reposts.AnyAsync(r => r.PostId == post.PostId && r.UserId == currentUserId);
//////////////            if (already) throw new InvalidOperationException("Already reposted");

//////////////            _db.Reposts.Add(new Repost { PostId = post.PostId, UserId = user.UserId, CreatedAt = DateTime.UtcNow });
//////////////            post.IsRepost = true;
//////////////            _db.Posts.Update(post);

//////////////            await _db.SaveChangesAsync();

//////////////            return new PostResponseDto
//////////////            {
//////////////                PostId = post.PostId,
//////////////                Title = $"[Repost by {user.FullName}] {post.Title}",
//////////////                Body = post.Body,
//////////////                AuthorName = post.User.FullName,
//////////////                DepartmentName = post.Dept?.DeptName ?? $"Dept {post.DeptId}",
//////////////                UpvoteCount = post.UpvoteCount,
//////////////                DownvoteCount = post.DownvoteCount,
//////////////                Tags = post.PostTags?.Select(t => t.Tag.TagName).ToList(),
//////////////                Attachments = post.Attachments?.Select(a => a.FilePath).ToList(),
//////////////                IsRepost = true,
//////////////                CreatedAt = DateTime.UtcNow
//////////////            };
//////////////        }

//////////////        // REPOST lists
//////////////        public async Task<List<PostResponseDto>> GetRepostsAsync(int postId)
//////////////        {
//////////////            var reposts = await _db.Reposts
//////////////                .Include(r => r.User)
//////////////                .Include(r => r.Post).ThenInclude(p => p.User)
//////////////                .Include(r => r.Post).ThenInclude(p => p.Dept)
//////////////                .Include(r => r.Post).ThenInclude(p => p.PostTags).ThenInclude(pt => pt.Tag)
//////////////                .Include(r => r.Post).ThenInclude(p => p.Attachments)
//////////////                .Where(r => r.PostId == postId)
//////////////                .OrderByDescending(r => r.CreatedAt)
//////////////                .ToListAsync();

//////////////            return reposts.Select(r => new PostResponseDto
//////////////            {
//////////////                PostId = r.Post.PostId,
//////////////                Title = $"[Repost by {r.User.FullName}] {r.Post.Title}",
//////////////                Body = r.Post.Body,
//////////////                AuthorName = r.Post.User.FullName,
//////////////                DepartmentName = r.Post.Dept?.DeptName ?? $"Dept {r.Post.DeptId}",
//////////////                UpvoteCount = r.Post.UpvoteCount,
//////////////                DownvoteCount = r.Post.DownvoteCount,
//////////////                Tags = r.Post.PostTags?.Select(t => t.Tag.TagName).ToList(),
//////////////                Attachments = r.Post.Attachments?.Select(a => a.FilePath).ToList(),
//////////////                IsRepost = true,
//////////////                CreatedAt = r.CreatedAt
//////////////            }).ToList();
//////////////        }

//////////////        public async Task<List<PostResponseDto>> GetRepostsByUserAsync(int userId)
//////////////        {
//////////////            var reposts = await _db.Reposts
//////////////                .Where(r => r.UserId == userId)
//////////////                .Include(r => r.User)
//////////////                .Include(r => r.Post).ThenInclude(p => p.User)
//////////////                .Include(r => r.Post).ThenInclude(p => p.Dept)
//////////////                .Include(r => r.Post).ThenInclude(p => p.PostTags).ThenInclude(pt => pt.Tag)
//////////////                .Include(r => r.Post).ThenInclude(p => p.Attachments)
//////////////                .OrderByDescending(r => r.CreatedAt)
//////////////                .ToListAsync();

//////////////            return reposts.Select(r => new PostResponseDto
//////////////            {
//////////////                PostId = r.Post.PostId,
//////////////                Title = $"[Repost by {r.User.FullName}] {r.Post.Title}",
//////////////                Body = r.Post.Body,
//////////////                AuthorName = r.Post.User.FullName,
//////////////                DepartmentName = r.Post.Dept?.DeptName ?? $"Dept {r.Post.DeptId}",
//////////////                UpvoteCount = r.Post.UpvoteCount,
//////////////                DownvoteCount = r.Post.DownvoteCount,
//////////////                Tags = r.Post.PostTags?.Select(t => t.Tag.TagName).ToList(),
//////////////                Attachments = r.Post.Attachments?.Select(a => a.FilePath).ToList(),
//////////////                IsRepost = true,
//////////////                CreatedAt = r.CreatedAt
//////////////            }).ToList();
//////////////        }

//////////////        // LISTS
//////////////        public async Task<List<PostResponseDto>> GetAllPostsAsync()
//////////////        {
//////////////            var posts = await _db.Posts
//////////////                .Include(p => p.User).Include(p => p.Dept)
//////////////                .Include(p => p.PostTags).ThenInclude(pt => pt.Tag)
//////////////                .Include(p => p.Attachments)
//////////////                .ToListAsync();

//////////////            var reposts = await _db.Reposts
//////////////                .Include(r => r.User)
//////////////                .Include(r => r.Post).ThenInclude(p => p.User)
//////////////                .Include(r => r.Post).ThenInclude(p => p.Dept)
//////////////                .Include(r => r.Post).ThenInclude(p => p.PostTags).ThenInclude(pt => pt.Tag)
//////////////                .Include(r => r.Post).ThenInclude(p => p.Attachments)
//////////////                .ToListAsync();

//////////////            var postDtos = posts.Select(ToDto);
//////////////            var repostDtos = reposts.Select(r => new PostResponseDto
//////////////            {
//////////////                PostId = r.Post.PostId,
//////////////                Title = $"[Repost by {r.User.FullName}] {r.Post.Title}",
//////////////                Body = r.Post.Body,
//////////////                AuthorName = r.Post.User.FullName,
//////////////                DepartmentName = r.Post.Dept?.DeptName ?? $"Dept {r.Post.DeptId}",
//////////////                UpvoteCount = r.Post.UpvoteCount,
//////////////                DownvoteCount = r.Post.DownvoteCount,
//////////////                Tags = r.Post.PostTags?.Select(t => t.Tag.TagName).ToList(),
//////////////                Attachments = r.Post.Attachments?.Select(a => a.FilePath).ToList(),
//////////////                IsRepost = true,
//////////////                CreatedAt = r.CreatedAt
//////////////            });

//////////////            return postDtos.Concat(repostDtos).OrderByDescending(x => x.CreatedAt).ToList();
//////////////        }

//////////////        public async Task<PostResponseDto?> GetPostByIdAsync(int postId)
//////////////        {
//////////////            var p = await _db.Posts
//////////////                .Include(p => p.User).Include(p => p.Dept)
//////////////                .Include(p => p.PostTags).ThenInclude(pt => pt.Tag)
//////////////                .Include(p => p.Attachments)
//////////////                .FirstOrDefaultAsync(p => p.PostId == postId);
//////////////            if (p == null) return null;
//////////////            return ToDto(p);
//////////////        }

//////////////        public async Task<List<PostResponseDto>> GetMyPostsAsync(int userId)
//////////////        {
//////////////            var posts = await _db.Posts
//////////////                .Where(p => p.UserId == userId)
//////////////                .Include(p => p.User).Include(p => p.Dept)
//////////////                .Include(p => p.PostTags).ThenInclude(pt => pt.Tag)
//////////////                .Include(p => p.Attachments)
//////////////                .OrderByDescending(p => p.CreatedAt)
//////////////                .ToListAsync();

//////////////            return posts.Select(ToDto).ToList();
//////////////        }

//////////////        // Helpers
//////////////        private static string SanitizeFileName(string filename)
//////////////        {
//////////////            var name = Path.GetFileName(filename);
//////////////            foreach (var c in Path.GetInvalidFileNameChars())
//////////////                name = name.Replace(c, '_');
//////////////            return name;
//////////////        }

//////////////        private static PostResponseDto ToDto(Post p)
//////////////        {
//////////////            return new PostResponseDto
//////////////            {
//////////////                PostId = p.PostId,
//////////////                Title = p.Title,
//////////////                Body = p.Body,
//////////////                AuthorName = p.User?.FullName ?? "(unknown)",
//////////////                DepartmentName = p.Dept?.DeptName ?? $"Dept {p.DeptId}",
//////////////                UpvoteCount = p.UpvoteCount,
//////////////                DownvoteCount = p.DownvoteCount,
//////////////                Tags = p.PostTags?.Select(t => t.Tag.TagName).ToList(),
//////////////                Attachments = p.Attachments?.Select(a => a.FilePath).ToList(),
//////////////                IsRepost = p.IsRepost,
//////////////                CreatedAt = p.CreatedAt
//////////////            };
//////////////        }
//////////////    }
//////////////}


////////////using System;
////////////using System.Collections.Generic;
////////////using System.IO;
////////////using System.Linq;
////////////using System.Threading.Tasks;
////////////using Microsoft.AspNetCore.Hosting;
////////////using Microsoft.EntityFrameworkCore;
////////////using Microsoft.Extensions.Logging;
////////////using FNF_PROJ.Data;
////////////using FNF_PROJ.DTOs;

////////////namespace FNF_PROJ.Services
////////////{
////////////    // -------------------- Interface (same file) --------------------
////////////    public interface IPostService
////////////    {
////////////        Task<PostResponseDto> CreatePostAsync(int userId, PostCreateDto dto);
////////////        Task<PostResponseDto> EditPostAsync(int userId, string role, int deptId, int postId, PostCreateDto dto);
////////////        Task DeletePostAsync(int userId, int deptId, int postId, string reason); // manager-only, with reason
////////////        Task<PostResponseDto> RepostAsync(int currentUserId, RepostDto dto);
////////////        Task<List<PostResponseDto>> GetRepostsAsync(int postId);
////////////        Task<List<PostResponseDto>> GetRepostsByUserAsync(int userId);
////////////        Task<List<PostResponseDto>> GetAllPostsAsync();
////////////        Task<PostResponseDto?> GetPostByIdAsync(int postId);
////////////        Task<List<PostResponseDto>> GetMyPostsAsync(int userId);
////////////    }

////////////    // -------------------- Implementation --------------------
////////////    public class PostService : IPostService
////////////    {
////////////        private readonly AppDbContext _db;
////////////        private readonly IWebHostEnvironment _env;
////////////        private readonly ILogger<PostService>? _logger;

////////////        public PostService(AppDbContext db, IWebHostEnvironment env, ILogger<PostService>? logger = null)
////////////        {
////////////            _db = db;
////////////            _env = env;
////////////            _logger = logger;
////////////        }

////////////        public async Task<PostResponseDto> CreatePostAsync(int userId, PostCreateDto dto)
////////////        {
////////////            var user = await _db.Users.FindAsync(userId) ?? throw new InvalidOperationException("User not found");

////////////            var post = new Post
////////////            {
////////////                Title = dto.Title ?? "",
////////////                Body = dto.Body ?? "",
////////////                UserId = user.UserId,
////////////                DeptId = user.DepartmentId,
////////////                CreatedAt = DateTime.UtcNow,
////////////                UpvoteCount = 0,
////////////                DownvoteCount = 0,
////////////                IsRepost = false
////////////            };

////////////            _db.Posts.Add(post);
////////////            await _db.SaveChangesAsync();

////////////            // Attachments
////////////            if (dto.Attachments != null && dto.Attachments.Any())
////////////            {
////////////                var webRoot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
////////////                var postsFolder = Path.Combine(webRoot, "posts");
////////////                if (!Directory.Exists(postsFolder)) Directory.CreateDirectory(postsFolder);

////////////                foreach (var file in dto.Attachments)
////////////                {
////////////                    if (file == null || file.Length == 0) continue;

////////////                    var clientFileName = Path.GetFileName(file.FileName);
////////////                    var safeFileName = SanitizeFileName(clientFileName);
////////////                    var targetPath = Path.Combine(postsFolder, safeFileName);

////////////                    if (File.Exists(targetPath))
////////////                    {
////////////                        var ext = Path.GetExtension(safeFileName);
////////////                        var nameOnly = Path.GetFileNameWithoutExtension(safeFileName);
////////////                        safeFileName = $"{nameOnly}-{Guid.NewGuid():N}".Substring(0, nameOnly.Length + 9) + ext;
////////////                        targetPath = Path.Combine(postsFolder, safeFileName);
////////////                    }

////////////                    using var stream = new FileStream(targetPath, FileMode.Create);
////////////                    await file.CopyToAsync(stream);

////////////                    var publicPath = $"/posts/{safeFileName}";
////////////                    _db.Attachments.Add(new Attachment
////////////                    {
////////////                        PostId = post.PostId,
////////////                        FileName = safeFileName,
////////////                        FilePath = publicPath,
////////////                        FileType = file.ContentType ?? "application/octet-stream",
////////////                        UploadedAt = DateTime.UtcNow
////////////                    });
////////////                }
////////////                await _db.SaveChangesAsync();
////////////            }

////////////            // Tags (dept-scoped)
////////////            if (dto.TagIds != null && dto.TagIds.Any())
////////////            {
////////////                var requestedTagIds = dto.TagIds.Distinct().ToList();
////////////                var validTags = await _db.Tags
////////////                    .Where(t => requestedTagIds.Contains(t.TagId) && t.DeptId == user.DepartmentId)
////////////                    .ToListAsync();

////////////                if (_logger != null && validTags.Count != requestedTagIds.Count)
////////////                {
////////////                    var invalid = requestedTagIds.Except(validTags.Select(t => t.TagId)).ToList();
////////////                    _logger.LogWarning("User {UserId} attempted invalid/out-of-dept tags: {InvalidTagIds}", userId, string.Join(",", invalid));
////////////                }

////////////                foreach (var tag in validTags)
////////////                {
////////////                    if (!await _db.PostTags.AnyAsync(pt => pt.PostId == post.PostId && pt.TagId == tag.TagId))
////////////                        _db.PostTags.Add(new PostTag { PostId = post.PostId, TagId = tag.TagId });
////////////                }
////////////                await _db.SaveChangesAsync();
////////////            }

////////////            var savedPost = await _db.Posts
////////////                .Include(p => p.User)
////////////                .Include(p => p.Dept)
////////////                .Include(p => p.PostTags).ThenInclude(pt => pt.Tag)
////////////                .Include(p => p.Attachments)
////////////                .FirstOrDefaultAsync(p => p.PostId == post.PostId);

////////////            if (savedPost == null) throw new InvalidOperationException("Post not found after creation.");

////////////            return ToDto(savedPost);
////////////        }

////////////        public async Task<PostResponseDto> EditPostAsync(int userId, string role, int deptId, int postId, PostCreateDto dto)
////////////        {
////////////            var post = await _db.Posts
////////////                .Include(p => p.User).Include(p => p.Dept)
////////////                .Include(p => p.Attachments)
////////////                .Include(p => p.PostTags).ThenInclude(pt => pt.Tag)
////////////                .FirstOrDefaultAsync(p => p.PostId == postId)
////////////                ?? throw new InvalidOperationException("Post not found");

////////////            var isEmployee = string.Equals(role, "Employee", StringComparison.OrdinalIgnoreCase);
////////////            var isManager = string.Equals(role, "Manager", StringComparison.OrdinalIgnoreCase);

////////////            if (isEmployee && post.UserId != userId) throw new UnauthorizedAccessException();
////////////            if (isManager && post.DeptId != (deptId == 0 ? post.DeptId : deptId)) throw new UnauthorizedAccessException();
////////////            if (!isEmployee && !isManager) throw new UnauthorizedAccessException();

////////////            post.Title = dto.Title ?? post.Title;
////////////            post.Body = dto.Body ?? post.Body;
////////////            post.UpdatedAt = DateTime.UtcNow;
////////////            await _db.SaveChangesAsync();

////////////            // Optional: allow adding new attachments on edit
////////////            if (dto.Attachments != null && dto.Attachments.Any())
////////////            {
////////////                var webRoot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
////////////                var postsFolder = Path.Combine(webRoot, "posts");
////////////                if (!Directory.Exists(postsFolder)) Directory.CreateDirectory(postsFolder);

////////////                foreach (var file in dto.Attachments)
////////////                {
////////////                    if (file == null || file.Length == 0) continue;

////////////                    var clientFileName = Path.GetFileName(file.FileName);
////////////                    var safeFileName = SanitizeFileName(clientFileName);
////////////                    var targetPath = Path.Combine(postsFolder, safeFileName);

////////////                    if (File.Exists(targetPath))
////////////                    {
////////////                        var ext = Path.GetExtension(safeFileName);
////////////                        var nameOnly = Path.GetFileNameWithoutExtension(safeFileName);
////////////                        safeFileName = $"{nameOnly}-{Guid.NewGuid():N}".Substring(0, nameOnly.Length + 9) + ext;
////////////                        targetPath = Path.Combine(postsFolder, safeFileName);
////////////                    }

////////////                    using var stream = new FileStream(targetPath, FileMode.Create);
////////////                    await file.CopyToAsync(stream);

////////////                    var publicPath = $"/posts/{safeFileName}";
////////////                    _db.Attachments.Add(new Attachment
////////////                    {
////////////                        PostId = post.PostId,
////////////                        FileName = safeFileName,
////////////                        FilePath = publicPath,
////////////                        FileType = file.ContentType ?? "application/octet-stream",
////////////                        UploadedAt = DateTime.UtcNow
////////////                    });
////////////                }
////////////                await _db.SaveChangesAsync();
////////////            }

////////////            return await GetPostByIdAsync(post.PostId) ?? ToDto(post);
////////////        }

////////////        // Manager-only delete with reason -> creates Commit record
////////////        public async Task DeletePostAsync(int userId, int deptId, int postId, string reason)
////////////        {
////////////            if (string.IsNullOrWhiteSpace(reason))
////////////                throw new InvalidOperationException("Delete reason is required.");

////////////            var post = await _db.Posts.FirstOrDefaultAsync(p => p.PostId == postId)
////////////                       ?? throw new InvalidOperationException("Post not found");
////////////            var manager = await _db.Users.FindAsync(userId)
////////////                          ?? throw new InvalidOperationException("User not found");

////////////            if (!string.Equals(manager.Role, "Manager", StringComparison.OrdinalIgnoreCase))
////////////                throw new UnauthorizedAccessException("Only Managers can delete posts.");

////////////            var managerDept = manager.DepartmentId;
////////////            if (post.DeptId != managerDept || (deptId != 0 && deptId != managerDept))
////////////                throw new UnauthorizedAccessException("Manager can delete only posts from their own department.");

////////////            _db.Commits.Add(new Commit
////////////            {
////////////                PostId = post.PostId,
////////////                ManagerId = manager.UserId,
////////////                Action = "DELETE",
////////////                Reason = reason.Trim(),
////////////                CreatedAt = DateTime.UtcNow
////////////            });

////////////            _db.Posts.Remove(post);
////////////            await _db.SaveChangesAsync();
////////////        }

////////////        public async Task<PostResponseDto> RepostAsync(int currentUserId, RepostDto dto)
////////////        {
////////////            if (dto == null || dto.PostId <= 0) throw new InvalidOperationException("Invalid repost payload");

////////////            var user = await _db.Users.FindAsync(currentUserId) ?? throw new InvalidOperationException("User not found");

////////////            var post = await _db.Posts
////////////                .Include(p => p.User)
////////////                .Include(p => p.PostTags).ThenInclude(pt => pt.Tag)
////////////                .Include(p => p.Dept)
////////////                .Include(p => p.Attachments)
////////////                .FirstOrDefaultAsync(p => p.PostId == dto.PostId)
////////////                ?? throw new InvalidOperationException("Original post not found");

////////////            var already = await _db.Reposts.AnyAsync(r => r.PostId == post.PostId && r.UserId == currentUserId);
////////////            if (already) throw new InvalidOperationException("Already reposted");

////////////            _db.Reposts.Add(new Repost { PostId = post.PostId, UserId = user.UserId, CreatedAt = DateTime.UtcNow });
////////////            post.IsRepost = true;
////////////            _db.Posts.Update(post);

////////////            await _db.SaveChangesAsync();

////////////            return new PostResponseDto
////////////            {
////////////                PostId = post.PostId,
////////////                Title = $"[Repost by {user.FullName}] {post.Title}",
////////////                Body = post.Body,
////////////                AuthorName = post.User.FullName,
////////////                DepartmentName = post.Dept?.DeptName ?? $"Dept {post.DeptId}",
////////////                UpvoteCount = post.UpvoteCount,
////////////                DownvoteCount = post.DownvoteCount,
////////////                Tags = post.PostTags?.Select(t => t.Tag.TagName).ToList(),
////////////                Attachments = post.Attachments?.Select(a => a.FilePath).ToList(),
////////////                IsRepost = true,
////////////                CreatedAt = DateTime.UtcNow
////////////            };
////////////        }

////////////        public async Task<List<PostResponseDto>> GetRepostsAsync(int postId)
////////////        {
////////////            var reposts = await _db.Reposts
////////////                .Include(r => r.User)
////////////                .Include(r => r.Post).ThenInclude(p => p.User)
////////////                .Include(r => r.Post).ThenInclude(p => p.Dept)
////////////                .Include(r => r.Post).ThenInclude(p => p.PostTags).ThenInclude(pt => pt.Tag)
////////////                .Include(r => r.Post).ThenInclude(p => p.Attachments)
////////////                .Where(r => r.PostId == postId)
////////////                .OrderByDescending(r => r.CreatedAt)
////////////                .ToListAsync();

////////////            return reposts.Select(r => new PostResponseDto
////////////            {
////////////                PostId = r.Post.PostId,
////////////                Title = $"[Repost by {r.User.FullName}] {r.Post.Title}",
////////////                Body = r.Post.Body,
////////////                AuthorName = r.Post.User.FullName,
////////////                DepartmentName = r.Post.Dept?.DeptName ?? $"Dept {r.Post.DeptId}",
////////////                UpvoteCount = r.Post.UpvoteCount,
////////////                DownvoteCount = r.Post.DownvoteCount,
////////////                Tags = r.Post.PostTags?.Select(t => t.Tag.TagName).ToList(),
////////////                Attachments = r.Post.Attachments?.Select(a => a.FilePath).ToList(),
////////////                IsRepost = true,
////////////                CreatedAt = r.CreatedAt
////////////            }).ToList();
////////////        }

////////////        public async Task<List<PostResponseDto>> GetRepostsByUserAsync(int userId)
////////////        {
////////////            var reposts = await _db.Reposts
////////////                .Where(r => r.UserId == userId)
////////////                .Include(r => r.User)
////////////                .Include(r => r.Post).ThenInclude(p => p.User)
////////////                .Include(r => r.Post).ThenInclude(p => p.Dept)
////////////                .Include(r => r.Post).ThenInclude(p => p.PostTags).ThenInclude(pt => pt.Tag)
////////////                .Include(r => r.Post).ThenInclude(p => p.Attachments)
////////////                .OrderByDescending(r => r.CreatedAt)
////////////                .ToListAsync();

////////////            return reposts.Select(r => new PostResponseDto
////////////            {
////////////                PostId = r.Post.PostId,
////////////                Title = $"[Repost by {r.User.FullName}] {r.Post.Title}",
////////////                Body = r.Post.Body,
////////////                AuthorName = r.Post.User.FullName,
////////////                DepartmentName = r.Post.Dept?.DeptName ?? $"Dept {r.Post.DeptId}",
////////////                UpvoteCount = r.Post.UpvoteCount,
////////////                DownvoteCount = r.Post.DownvoteCount,
////////////                Tags = r.Post.PostTags?.Select(t => t.Tag.TagName).ToList(),
////////////                Attachments = r.Post.Attachments?.Select(a => a.FilePath).ToList(),
////////////                IsRepost = true,
////////////                CreatedAt = r.CreatedAt
////////////            }).ToList();
////////////        }

////////////        public async Task<List<PostResponseDto>> GetAllPostsAsync()
////////////        {
////////////            var posts = await _db.Posts
////////////                .Include(p => p.User).Include(p => p.Dept)
////////////                .Include(p => p.PostTags).ThenInclude(pt => pt.Tag)
////////////                .Include(p => p.Attachments)
////////////                .ToListAsync();

////////////            var reposts = await _db.Reposts
////////////                .Include(r => r.User)
////////////                .Include(r => r.Post).ThenInclude(p => p.User)
////////////                .Include(r => r.Post).ThenInclude(p => p.Dept)
////////////                .Include(r => r.Post).ThenInclude(p => p.PostTags).ThenInclude(pt => pt.Tag)
////////////                .Include(r => r.Post).ThenInclude(p => p.Attachments)
////////////                .ToListAsync();

////////////            var postDtos = posts.Select(ToDto);
////////////            var repostDtos = reposts.Select(r => new PostResponseDto
////////////            {
////////////                PostId = r.Post.PostId,
////////////                Title = $"[Repost by {r.User.FullName}] {r.Post.Title}",
////////////                Body = r.Post.Body,
////////////                AuthorName = r.Post.User.FullName,
////////////                DepartmentName = r.Post.Dept?.DeptName ?? $"Dept {r.Post.DeptId}",
////////////                UpvoteCount = r.Post.UpvoteCount,
////////////                DownvoteCount = r.Post.DownvoteCount,
////////////                Tags = r.Post.PostTags?.Select(t => t.Tag.TagName).ToList(),
////////////                Attachments = r.Post.Attachments?.Select(a => a.FilePath).ToList(),
////////////                IsRepost = true,
////////////                CreatedAt = r.CreatedAt
////////////            });

////////////            return postDtos.Concat(repostDtos).OrderByDescending(x => x.CreatedAt).ToList();
////////////        }

////////////        public async Task<PostResponseDto?> GetPostByIdAsync(int postId)
////////////        {
////////////            var p = await _db.Posts
////////////                .Include(p => p.User).Include(p => p.Dept)
////////////                .Include(p => p.PostTags).ThenInclude(pt => pt.Tag)
////////////                .Include(p => p.Attachments)
////////////                .FirstOrDefaultAsync(p => p.PostId == postId);
////////////            if (p == null) return null;
////////////            return ToDto(p);
////////////        }

////////////        public async Task<List<PostResponseDto>> GetMyPostsAsync(int userId)
////////////        {
////////////            var posts = await _db.Posts
////////////                .Where(p => p.UserId == userId)
////////////                .Include(p => p.User).Include(p => p.Dept)
////////////                .Include(p => p.PostTags).ThenInclude(pt => pt.Tag)
////////////                .Include(p => p.Attachments)
////////////                .OrderByDescending(p => p.CreatedAt)
////////////                .ToListAsync();

////////////            return posts.Select(ToDto).ToList();
////////////        }

////////////        private static string SanitizeFileName(string filename)
////////////        {
////////////            var name = Path.GetFileName(filename);
////////////            foreach (var c in Path.GetInvalidFileNameChars())
////////////                name = name.Replace(c, '_');
////////////            return name;
////////////        }

////////////        private static PostResponseDto ToDto(Post p)
////////////        {
////////////            return new PostResponseDto
////////////            {
////////////                PostId = p.PostId,
////////////                Title = p.Title,
////////////                Body = p.Body,
////////////                AuthorName = p.User?.FullName ?? "(unknown)",
////////////                DepartmentName = p.Dept?.DeptName ?? $"Dept {p.DeptId}",
////////////                UpvoteCount = p.UpvoteCount,
////////////                DownvoteCount = p.DownvoteCount,
////////////                Tags = p.PostTags?.Select(t => t.Tag.TagName).ToList(),
////////////                Attachments = p.Attachments?.Select(a => a.FilePath).ToList(),
////////////                IsRepost = p.IsRepost,
////////////                CreatedAt = p.CreatedAt
////////////            };
////////////        }
////////////    }
////////////}

//////////using System;
//////////using System.Collections.Generic;
//////////using System.IO;
//////////using System.Linq;
//////////using System.Threading.Tasks;
//////////using Microsoft.AspNetCore.Hosting;
//////////using Microsoft.EntityFrameworkCore;
//////////using Microsoft.Extensions.Logging;
//////////using FNF_PROJ.Data;
//////////using FNF_PROJ.DTOs;

//////////namespace FNF_PROJ.Services
//////////{
//////////    public interface IPostService
//////////    {
//////////        Task<PostResponseDto> CreatePostAsync(int userId, PostCreateDto dto);
//////////        Task<PostResponseDto> EditPostAsync(int userId, string role, int deptId, int postId, PostCreateDto dto);
//////////        Task DeletePostAsync(int userId, int deptId, int postId, string reason);
//////////        Task<PostResponseDto> RepostAsync(int currentUserId, RepostDto dto);
//////////        Task<List<PostResponseDto>> GetRepostsAsync(int postId);
//////////        Task<List<PostResponseDto>> GetRepostsByUserAsync(int userId);
//////////        Task<List<PostResponseDto>> GetAllPostsAsync();
//////////        Task<PostResponseDto?> GetPostByIdAsync(int postId);
//////////        Task<List<PostResponseDto>> GetMyPostsAsync(int userId);
//////////    }

//////////    public class PostService : IPostService
//////////    {
//////////        private readonly AppDbContext _db;
//////////        private readonly IWebHostEnvironment _env;
//////////        private readonly ILogger<PostService>? _logger;

//////////        public PostService(AppDbContext db, IWebHostEnvironment env, ILogger<PostService>? logger = null)
//////////        {
//////////            _db = db;
//////////            _env = env;
//////////            _logger = logger;
//////////        }

//////////        public async Task<PostResponseDto> CreatePostAsync(int userId, PostCreateDto dto)
//////////        {
//////////            var user = await _db.Users.FindAsync(userId) ?? throw new InvalidOperationException("User not found");

//////////            var post = new Post
//////////            {
//////////                Title = dto.Title ?? "",
//////////                Body = dto.Body ?? "",
//////////                UserId = user.UserId,
//////////                DeptId = user.DepartmentId,
//////////                CreatedAt = DateTime.UtcNow,
//////////                UpvoteCount = 0,
//////////                DownvoteCount = 0,
//////////                IsRepost = false
//////////            };

//////////            _db.Posts.Add(post);
//////////            await _db.SaveChangesAsync();

//////////            // attachments (unchanged)
//////////            if (dto.Attachments != null && dto.Attachments.Any())
//////////            {
//////////                var webRoot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
//////////                var postsFolder = Path.Combine(webRoot, "posts");
//////////                if (!Directory.Exists(postsFolder)) Directory.CreateDirectory(postsFolder);

//////////                foreach (var file in dto.Attachments)
//////////                {
//////////                    if (file == null || file.Length == 0) continue;

//////////                    var clientFileName = Path.GetFileName(file.FileName);
//////////                    var safeFileName = SanitizeFileName(clientFileName);
//////////                    var targetPath = Path.Combine(postsFolder, safeFileName);

//////////                    if (File.Exists(targetPath))
//////////                    {
//////////                        var ext = Path.GetExtension(safeFileName);
//////////                        var nameOnly = Path.GetFileNameWithoutExtension(safeFileName);
//////////                        safeFileName = $"{nameOnly}-{Guid.NewGuid().ToString("n")[..8]}{ext}";
//////////                        targetPath = Path.Combine(postsFolder, safeFileName);
//////////                    }

//////////                    using var stream = new FileStream(targetPath, FileMode.Create);
//////////                    await file.CopyToAsync(stream);

//////////                    var publicPath = $"/posts/{safeFileName}";
//////////                    _db.Attachments.Add(new Attachment
//////////                    {
//////////                        PostId = post.PostId,
//////////                        FileName = safeFileName,
//////////                        FilePath = publicPath,
//////////                        FileType = file.ContentType ?? "application/octet-stream",
//////////                        UploadedAt = DateTime.UtcNow
//////////                    });
//////////                }
//////////                await _db.SaveChangesAsync();
//////////            }

//////////            // tags (unchanged)
//////////            if (dto.TagIds != null && dto.TagIds.Any())
//////////            {
//////////                var requestedTagIds = dto.TagIds.Distinct().ToList();
//////////                var validTags = await _db.Tags
//////////                    .Where(t => requestedTagIds.Contains(t.TagId) && t.DeptId == user.DepartmentId)
//////////                    .ToListAsync();

//////////                if (_logger != null && validTags.Count != requestedTagIds.Count)
//////////                {
//////////                    var invalid = requestedTagIds.Except(validTags.Select(t => t.TagId)).ToList();
//////////                    _logger.LogWarning("User {UserId} attempted invalid/out-of-dept tags: {InvalidTagIds}", userId, string.Join(",", invalid));
//////////                }

//////////                foreach (var tag in validTags)
//////////                {
//////////                    if (!await _db.PostTags.AnyAsync(pt => pt.PostId == post.PostId && pt.TagId == tag.TagId))
//////////                        _db.PostTags.Add(new PostTag { PostId = post.PostId, TagId = tag.TagId });
//////////                }
//////////                await _db.SaveChangesAsync();
//////////            }

//////////            var savedPost = await _db.Posts
//////////                .Include(p => p.User)
//////////                .Include(p => p.Dept)
//////////                .Include(p => p.PostTags).ThenInclude(pt => pt.Tag)
//////////                .Include(p => p.Attachments)
//////////                .FirstOrDefaultAsync(p => p.PostId == post.PostId);

//////////            if (savedPost == null) throw new InvalidOperationException("Post not found after creation.");

//////////            return ToDto(savedPost);
//////////        }

//////////        public async Task<PostResponseDto> EditPostAsync(int userId, string role, int deptId, int postId, PostCreateDto dto)
//////////        {
//////////            var post = await _db.Posts
//////////                .Include(p => p.User).Include(p => p.Dept)
//////////                .Include(p => p.Attachments)
//////////                .Include(p => p.PostTags).ThenInclude(pt => pt.Tag)
//////////                .FirstOrDefaultAsync(p => p.PostId == postId)
//////////                ?? throw new InvalidOperationException("Post not found");

//////////            var isEmployee = string.Equals(role, "Employee", StringComparison.OrdinalIgnoreCase);
//////////            var isManager = string.Equals(role, "Manager", StringComparison.OrdinalIgnoreCase);

//////////            if (isEmployee && post.UserId != userId) throw new UnauthorizedAccessException();
//////////            if (isManager && post.DeptId != (deptId == 0 ? post.DeptId : deptId)) throw new UnauthorizedAccessException();
//////////            if (!isEmployee && !isManager) throw new UnauthorizedAccessException();

//////////            post.Title = dto.Title ?? post.Title;
//////////            post.Body = dto.Body ?? post.Body;
//////////            post.UpdatedAt = DateTime.UtcNow;
//////////            await _db.SaveChangesAsync();

//////////            if (dto.Attachments != null && dto.Attachments.Any())
//////////            {
//////////                var webRoot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
//////////                var postsFolder = Path.Combine(webRoot, "posts");
//////////                if (!Directory.Exists(postsFolder)) Directory.CreateDirectory(postsFolder);

//////////                foreach (var file in dto.Attachments)
//////////                {
//////////                    if (file == null || file.Length == 0) continue;

//////////                    var clientFileName = Path.GetFileName(file.FileName);
//////////                    var safeFileName = SanitizeFileName(clientFileName);
//////////                    var targetPath = Path.Combine(postsFolder, safeFileName);

//////////                    if (File.Exists(targetPath))
//////////                    {
//////////                        var ext = Path.GetExtension(safeFileName);
//////////                        var nameOnly = Path.GetFileNameWithoutExtension(safeFileName);
//////////                        safeFileName = $"{nameOnly}-{Guid.NewGuid().ToString("n")[..8]}{ext}";
//////////                        targetPath = Path.Combine(postsFolder, safeFileName);
//////////                    }

//////////                    using var stream = new FileStream(targetPath, FileMode.Create);
//////////                    await file.CopyToAsync(stream);

//////////                    var publicPath = $"/posts/{safeFileName}";
//////////                    _db.Attachments.Add(new Attachment
//////////                    {
//////////                        PostId = post.PostId,
//////////                        FileName = safeFileName,
//////////                        FilePath = publicPath,
//////////                        FileType = file.ContentType ?? "application/octet-stream",
//////////                        UploadedAt = DateTime.UtcNow
//////////                    });
//////////                }
//////////                await _db.SaveChangesAsync();
//////////            }

//////////            return await GetPostByIdAsync(post.PostId) ?? ToDto(post);
//////////        }

//////////        // 🔧 FIXED: use Commit.Message instead of Action/Reason
//////////        public async Task DeletePostAsync(int userId, int deptId, int postId, string reason)
//////////        {
//////////            if (string.IsNullOrWhiteSpace(reason))
//////////                throw new InvalidOperationException("Delete reason is required.");

//////////            var post = await _db.Posts.FirstOrDefaultAsync(p => p.PostId == postId)
//////////                       ?? throw new InvalidOperationException("Post not found");
//////////            var manager = await _db.Users.FindAsync(userId)
//////////                          ?? throw new InvalidOperationException("User not found");

//////////            if (!string.Equals(manager.Role, "Manager", StringComparison.OrdinalIgnoreCase))
//////////                throw new UnauthorizedAccessException("Only Managers can delete posts.");

//////////            var managerDept = manager.DepartmentId;
//////////            if (post.DeptId != managerDept || (deptId != 0 && deptId != managerDept))
//////////                throw new UnauthorizedAccessException("Manager can delete only posts from their own department.");

//////////            // 👇 Commit schema uses Message (no Action/Reason)
//////////            _db.Commits.Add(new Commit
//////////            {
//////////                PostId = post.PostId,
//////////                ManagerId = manager.UserId,
//////////                Message = reason.Trim(),
//////////                CreatedAt = DateTime.UtcNow
//////////            });

//////////            _db.Posts.Remove(post);
//////////            await _db.SaveChangesAsync();
//////////        }

//////////        public async Task<PostResponseDto> RepostAsync(int currentUserId, RepostDto dto)
//////////        {
//////////            if (dto == null || dto.PostId <= 0) throw new InvalidOperationException("Invalid repost payload");

//////////            var user = await _db.Users.FindAsync(currentUserId) ?? throw new InvalidOperationException("User not found");

//////////            var post = await _db.Posts
//////////                .Include(p => p.User)
//////////                .Include(p => p.PostTags).ThenInclude(pt => pt.Tag)
//////////                .Include(p => p.Dept)
//////////                .Include(p => p.Attachments)
//////////                .FirstOrDefaultAsync(p => p.PostId == dto.PostId)
//////////                ?? throw new InvalidOperationException("Original post not found");

//////////            var already = await _db.Reposts.AnyAsync(r => r.PostId == post.PostId && r.UserId == currentUserId);
//////////            if (already) throw new InvalidOperationException("Already reposted");

//////////            _db.Reposts.Add(new Repost { PostId = post.PostId, UserId = user.UserId, CreatedAt = DateTime.UtcNow });
//////////            post.IsRepost = true;
//////////            _db.Posts.Update(post);

//////////            await _db.SaveChangesAsync();

//////////            return new PostResponseDto
//////////            {
//////////                PostId = post.PostId,
//////////                Title = $"[Repost by {user.FullName}] {post.Title}",
//////////                Body = post.Body,
//////////                AuthorName = post.User?.FullName ?? "(unknown)",
//////////                DepartmentName = post.Dept?.DeptName ?? $"Dept {post.DeptId}",
//////////                UpvoteCount = post.UpvoteCount,
//////////                DownvoteCount = post.DownvoteCount,
//////////                Tags = post.PostTags?.Select(t => t.Tag?.TagName ?? "").Where(s => !string.IsNullOrEmpty(s)).ToList(),
//////////                Attachments = post.Attachments?.Select(a => a.FilePath).ToList(),
//////////                IsRepost = true,
//////////                CreatedAt = DateTime.UtcNow
//////////            };
//////////        }

//////////        public async Task<List<PostResponseDto>> GetRepostsAsync(int postId)
//////////        {
//////////            var reposts = await _db.Reposts
//////////                .Include(r => r.User)
//////////                .Include(r => r.Post).ThenInclude(p => p.User)
//////////                .Include(r => r.Post).ThenInclude(p => p.Dept)
//////////                .Include(r => r.Post).ThenInclude(p => p.PostTags).ThenInclude(pt => pt.Tag)
//////////                .Include(r => r.Post).ThenInclude(p => p.Attachments)
//////////                .Where(r => r.PostId == postId)
//////////                .OrderByDescending(r => r.CreatedAt)
//////////                .ToListAsync();

//////////            return reposts.Select(r => new PostResponseDto
//////////            {
//////////                PostId = r.Post?.PostId ?? 0,
//////////                Title = $"[Repost by {r.User?.FullName ?? "(unknown)"}] {r.Post?.Title ?? ""}",
//////////                Body = r.Post?.Body ?? "",
//////////                AuthorName = r.Post?.User?.FullName ?? "(unknown)",
//////////                DepartmentName = r.Post?.Dept?.DeptName ?? $"Dept {r.Post?.DeptId}",
//////////                UpvoteCount = r.Post?.UpvoteCount ?? 0,
//////////                DownvoteCount = r.Post?.DownvoteCount ?? 0,
//////////                Tags = r.Post?.PostTags?.Select(t => t.Tag?.TagName ?? "").Where(s => !string.IsNullOrEmpty(s)).ToList(),
//////////                Attachments = r.Post?.Attachments?.Select(a => a.FilePath).ToList(),
//////////                IsRepost = true,
//////////                CreatedAt = r.CreatedAt
//////////            }).ToList();
//////////        }

//////////        public async Task<List<PostResponseDto>> GetRepostsByUserAsync(int userId)
//////////        {
//////////            var reposts = await _db.Reposts
//////////                .Where(r => r.UserId == userId)
//////////                .Include(r => r.User)
//////////                .Include(r => r.Post).ThenInclude(p => p.User)
//////////                .Include(r => r.Post).ThenInclude(p => p.Dept)
//////////                .Include(r => r.Post).ThenInclude(p => p.PostTags).ThenInclude(pt => pt.Tag)
//////////                .Include(r => r.Post).ThenInclude(p => p.Attachments)
//////////                .OrderByDescending(r => r.CreatedAt)
//////////                .ToListAsync();

//////////            return reposts.Select(r => new PostResponseDto
//////////            {
//////////                PostId = r.Post?.PostId ?? 0,
//////////                Title = $"[Repost by {r.User?.FullName ?? "(unknown)"}] {r.Post?.Title ?? ""}",
//////////                Body = r.Post?.Body ?? "",
//////////                AuthorName = r.Post?.User?.FullName ?? "(unknown)",
//////////                DepartmentName = r.Post?.Dept?.DeptName ?? $"Dept {r.Post?.DeptId}",
//////////                UpvoteCount = r.Post?.UpvoteCount ?? 0,
//////////                DownvoteCount = r.Post?.DownvoteCount ?? 0,
//////////                Tags = r.Post?.PostTags?.Select(t => t.Tag?.TagName ?? "").Where(s => !string.IsNullOrEmpty(s)).ToList(),
//////////                Attachments = r.Post?.Attachments?.Select(a => a.FilePath).ToList(),
//////////                IsRepost = true,
//////////                CreatedAt = r.CreatedAt
//////////            }).ToList();
//////////        }

//////////        public async Task<List<PostResponseDto>> GetAllPostsAsync()
//////////        {
//////////            var posts = await _db.Posts
//////////                .Include(p => p.User).Include(p => p.Dept)
//////////                .Include(p => p.PostTags).ThenInclude(pt => pt.Tag)
//////////                .Include(p => p.Attachments)
//////////                .ToListAsync();

//////////            var reposts = await _db.Reposts
//////////                .Include(r => r.User)
//////////                .Include(r => r.Post).ThenInclude(p => p.User)
//////////                .Include(r => r.Post).ThenInclude(p => p.Dept)
//////////                .Include(r => r.Post).ThenInclude(p => p.PostTags).ThenInclude(pt => pt.Tag)
//////////                .Include(r => r.Post).ThenInclude(p => p.Attachments)
//////////                .ToListAsync();

//////////            var postDtos = posts.Select(ToDto);
//////////            var repostDtos = reposts.Select(r => new PostResponseDto
//////////            {
//////////                PostId = r.Post?.PostId ?? 0,
//////////                Title = $"[Repost by {r.User?.FullName ?? "(unknown)"}] {r.Post?.Title ?? ""}",
//////////                Body = r.Post?.Body ?? "",
//////////                AuthorName = r.Post?.User?.FullName ?? "(unknown)",
//////////                DepartmentName = r.Post?.Dept?.DeptName ?? $"Dept {r.Post?.DeptId}",
//////////                UpvoteCount = r.Post?.UpvoteCount ?? 0,
//////////                DownvoteCount = r.Post?.DownvoteCount ?? 0,
//////////                Tags = r.Post?.PostTags?.Select(t => t.Tag?.TagName ?? "").Where(s => !string.IsNullOrEmpty(s)).ToList(),
//////////                Attachments = r.Post?.Attachments?.Select(a => a.FilePath).ToList(),
//////////                IsRepost = true,
//////////                CreatedAt = r.CreatedAt
//////////            });

//////////            return postDtos.Concat(repostDtos).OrderByDescending(x => x.CreatedAt).ToList();
//////////        }

//////////        public async Task<PostResponseDto?> GetPostByIdAsync(int postId)
//////////        {
//////////            var p = await _db.Posts
//////////                .Include(p => p.User).Include(p => p.Dept)
//////////                .Include(p => p.PostTags).ThenInclude(pt => pt.Tag)
//////////                .Include(p => p.Attachments)
//////////                .FirstOrDefaultAsync(p => p.PostId == postId);
//////////            if (p == null) return null;
//////////            return ToDto(p);
//////////        }

//////////        public async Task<List<PostResponseDto>> GetMyPostsAsync(int userId)
//////////        {
//////////            var posts = await _db.Posts
//////////                .Where(p => p.UserId == userId)
//////////                .Include(p => p.User).Include(p => p.Dept)
//////////                .Include(p => p.PostTags).ThenInclude(pt => pt.Tag)
//////////                .Include(p => p.Attachments)
//////////                .OrderByDescending(p => p.CreatedAt)
//////////                .ToListAsync();

//////////            return posts.Select(ToDto).ToList();
//////////        }

//////////        private static string SanitizeFileName(string filename)
//////////        {
//////////            var name = Path.GetFileName(filename);
//////////            foreach (var c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
//////////            return name;
//////////        }

//////////        private static PostResponseDto ToDto(Post p)
//////////        {
//////////            return new PostResponseDto
//////////            {
//////////                PostId = p.PostId,
//////////                Title = p.Title,
//////////                Body = p.Body,
//////////                AuthorName = p.User?.FullName ?? "(unknown)",
//////////                DepartmentName = p.Dept?.DeptName ?? $"Dept {p.DeptId}",
//////////                UpvoteCount = p.UpvoteCount,
//////////                DownvoteCount = p.DownvoteCount,
//////////                Tags = p.PostTags?.Select(t => t.Tag?.TagName ?? "").Where(s => !string.IsNullOrEmpty(s)).ToList(),
//////////                Attachments = p.Attachments?.Select(a => a.FilePath).ToList(),
//////////                IsRepost = p.IsRepost,
//////////                CreatedAt = p.CreatedAt
//////////            };
//////////        }
//////////    }
//////////}


////////using System;
////////using System.Collections.Generic;
////////using System.IO;
////////using System.Linq;
////////using System.Threading.Tasks;
////////using Microsoft.AspNetCore.Hosting;
////////using Microsoft.EntityFrameworkCore;
////////using Microsoft.Extensions.Logging;
////////using FNF_PROJ.Data;
////////using FNF_PROJ.DTOs;

////////namespace FNF_PROJ.Services
////////{
////////    // -------- Interface (same file) --------
////////    public interface IPostService
////////    {
////////        Task<PostResponseDto> CreatePostAsync(int userId, PostCreateDto dto);
////////        Task<PostResponseDto> EditPostAsync(int userId, string role, int deptId, int postId, PostCreateDto dto);
////////        Task DeletePostAsync(int userId, int deptId, int postId, string reason);
////////        Task<PostResponseDto> RepostAsync(int currentUserId, RepostDto dto);
////////        Task<List<PostResponseDto>> GetRepostsAsync(int postId);
////////        Task<List<PostResponseDto>> GetRepostsByUserAsync(int userId);
////////        Task<List<PostResponseDto>> GetAllPostsAsync();
////////        Task<PostResponseDto?> GetPostByIdAsync(int postId);
////////        Task<List<PostResponseDto>> GetMyPostsAsync(int userId);
////////    }

////////    // -------- Implementation --------
////////    public class PostService : IPostService
////////    {
////////        private readonly AppDbContext _db;
////////        private readonly IWebHostEnvironment _env;
////////        private readonly ILogger<PostService>? _logger;

////////        public PostService(AppDbContext db, IWebHostEnvironment env, ILogger<PostService>? logger = null)
////////        {
////////            _db = db;
////////            _env = env;
////////            _logger = logger;
////////        }

////////        public async Task<PostResponseDto> CreatePostAsync(int userId, PostCreateDto dto)
////////        {
////////            var user = await _db.Users.FindAsync(userId) ?? throw new InvalidOperationException("User not found");

////////            var post = new Post
////////            {
////////                Title = dto.Title ?? "",
////////                Body = dto.Body ?? "",
////////                UserId = user.UserId,
////////                DeptId = user.DepartmentId,
////////                CreatedAt = DateTime.UtcNow,
////////                UpvoteCount = 0,
////////                DownvoteCount = 0,
////////                IsRepost = false
////////            };

////////            _db.Posts.Add(post);
////////            await _db.SaveChangesAsync();

////////            // attachments
////////            if (dto.Attachments != null && dto.Attachments.Any())
////////            {
////////                var webRoot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
////////                var postsFolder = Path.Combine(webRoot, "posts");
////////                if (!Directory.Exists(postsFolder)) Directory.CreateDirectory(postsFolder);

////////                foreach (var file in dto.Attachments)
////////                {
////////                    if (file == null || file.Length == 0) continue;

////////                    var clientFileName = Path.GetFileName(file.FileName);
////////                    var safeFileName = SanitizeFileName(clientFileName);
////////                    var targetPath = Path.Combine(postsFolder, safeFileName);

////////                    if (File.Exists(targetPath))
////////                    {
////////                        var ext = Path.GetExtension(safeFileName);
////////                        var nameOnly = Path.GetFileNameWithoutExtension(safeFileName);
////////                        safeFileName = $"{nameOnly}-{Guid.NewGuid().ToString("n")[..8]}{ext}";
////////                        targetPath = Path.Combine(postsFolder, safeFileName);
////////                    }

////////                    using var stream = new FileStream(targetPath, FileMode.Create);
////////                    await file.CopyToAsync(stream);

////////                    var publicPath = $"/posts/{safeFileName}";
////////                    _db.Attachments.Add(new Attachment
////////                    {
////////                        PostId = post.PostId,
////////                        FileName = safeFileName,
////////                        FilePath = publicPath,
////////                        FileType = file.ContentType ?? "application/octet-stream",
////////                        UploadedAt = DateTime.UtcNow
////////                    });
////////                }
////////                await _db.SaveChangesAsync();
////////            }

////////            // tags (dept-scoped)
////////            if (dto.TagIds != null && dto.TagIds.Any())
////////            {
////////                var requestedTagIds = dto.TagIds.Distinct().ToList();
////////                var validTags = await _db.Tags
////////                    .Where(t => requestedTagIds.Contains(t.TagId) && t.DeptId == user.DepartmentId)
////////                    .ToListAsync();

////////                if (_logger != null && validTags.Count != requestedTagIds.Count)
////////                {
////////                    var invalid = requestedTagIds.Except(validTags.Select(t => t.TagId)).ToList();
////////                    _logger.LogWarning("User {UserId} attempted invalid/out-of-dept tags: {InvalidTagIds}", userId, string.Join(",", invalid));
////////                }

////////                foreach (var tag in validTags)
////////                {
////////                    if (!await _db.PostTags.AnyAsync(pt => pt.PostId == post.PostId && pt.TagId == tag.TagId))
////////                        _db.PostTags.Add(new PostTag { PostId = post.PostId, TagId = tag.TagId });
////////                }
////////                await _db.SaveChangesAsync();
////////            }

////////            var savedPost = await _db.Posts
////////                .Include(p => p.User)
////////                .Include(p => p.Dept)
////////                .Include(p => p.PostTags).ThenInclude(pt => pt.Tag)
////////                .Include(p => p.Attachments)
////////                .FirstOrDefaultAsync(p => p.PostId == post.PostId);

////////            if (savedPost == null) throw new InvalidOperationException("Post not found after creation.");

////////            return ToDto(savedPost);
////////        }

////////        public async Task<PostResponseDto> EditPostAsync(int userId, string role, int deptId, int postId, PostCreateDto dto)
////////        {
////////            var post = await _db.Posts
////////                .Include(p => p.User).Include(p => p.Dept)
////////                .Include(p => p.Attachments)
////////                .Include(p => p.PostTags).ThenInclude(pt => pt.Tag)
////////                .FirstOrDefaultAsync(p => p.PostId == postId)
////////                ?? throw new InvalidOperationException("Post not found");

////////            var isEmployee = string.Equals(role, "Employee", StringComparison.OrdinalIgnoreCase);
////////            var isManager = string.Equals(role, "Manager", StringComparison.OrdinalIgnoreCase);

////////            if (isEmployee && post.UserId != userId) throw new UnauthorizedAccessException();
////////            if (isManager && post.DeptId != (deptId == 0 ? post.DeptId : deptId)) throw new UnauthorizedAccessException();
////////            if (!isEmployee && !isManager) throw new UnauthorizedAccessException();

////////            post.Title = dto.Title ?? post.Title;
////////            post.Body = dto.Body ?? post.Body;
////////            post.UpdatedAt = DateTime.UtcNow;
////////            await _db.SaveChangesAsync();

////////            if (dto.Attachments != null && dto.Attachments.Any())
////////            {
////////                var webRoot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
////////                var postsFolder = Path.Combine(webRoot, "posts");
////////                if (!Directory.Exists(postsFolder)) Directory.CreateDirectory(postsFolder);

////////                foreach (var file in dto.Attachments)
////////                {
////////                    if (file == null || file.Length == 0) continue;

////////                    var clientFileName = Path.GetFileName(file.FileName);
////////                    var safeFileName = SanitizeFileName(clientFileName);
////////                    var targetPath = Path.Combine(postsFolder, safeFileName);

////////                    if (File.Exists(targetPath))
////////                    {
////////                        var ext = Path.GetExtension(safeFileName);
////////                        var nameOnly = Path.GetFileNameWithoutExtension(safeFileName);
////////                        safeFileName = $"{nameOnly}-{Guid.NewGuid().ToString("n")[..8]}{ext}";
////////                        targetPath = Path.Combine(postsFolder, safeFileName);
////////                    }

////////                    using var stream = new FileStream(targetPath, FileMode.Create);
////////                    await file.CopyToAsync(stream);

////////                    var publicPath = $"/posts/{safeFileName}";
////////                    _db.Attachments.Add(new Attachment
////////                    {
////////                        PostId = post.PostId,
////////                        FileName = safeFileName,
////////                        FilePath = publicPath,
////////                        FileType = file.ContentType ?? "application/octet-stream",
////////                        UploadedAt = DateTime.UtcNow
////////                    });
////////                }
////////                await _db.SaveChangesAsync();
////////            }

////////            return await GetPostByIdAsync(post.PostId) ?? ToDto(post);
////////        }

////////        // Manager-only delete; logs Commit.Message
////////        public async Task DeletePostAsync(int userId, int deptId, int postId, string reason)
////////        {
////////            if (string.IsNullOrWhiteSpace(reason))
////////                throw new InvalidOperationException("Delete reason is required.");

////////            var post = await _db.Posts.FirstOrDefaultAsync(p => p.PostId == postId)
////////                       ?? throw new InvalidOperationException("Post not found");
////////            var manager = await _db.Users.FindAsync(userId)
////////                          ?? throw new InvalidOperationException("User not found");

////////            if (!string.Equals(manager.Role, "Manager", StringComparison.OrdinalIgnoreCase))
////////                throw new UnauthorizedAccessException("Only Managers can delete posts.");

////////            var managerDept = manager.DepartmentId;
////////            if (post.DeptId != managerDept || (deptId != 0 && deptId != managerDept))
////////                throw new UnauthorizedAccessException("Manager can delete only posts from their own department.");

////////            _db.Commits.Add(new Commit
////////            {
////////                PostId = post.PostId,
////////                ManagerId = manager.UserId,
////////                Message = reason.Trim(),
////////                CreatedAt = DateTime.UtcNow
////////            });

////////            _db.Posts.Remove(post);
////////            await _db.SaveChangesAsync();
////////        }

////////        public async Task<PostResponseDto> RepostAsync(int currentUserId, RepostDto dto)
////////        {
////////            if (dto == null || dto.PostId <= 0) throw new InvalidOperationException("Invalid repost payload");

////////            var user = await _db.Users.FindAsync(currentUserId) ?? throw new InvalidOperationException("User not found");

////////            var post = await _db.Posts
////////                .Include(p => p.User)
////////                .Include(p => p.PostTags).ThenInclude(pt => pt.Tag)
////////                .Include(p => p.Dept)
////////                .Include(p => p.Attachments)
////////                .FirstOrDefaultAsync(p => p.PostId == dto.PostId)
////////                ?? throw new InvalidOperationException("Original post not found");

////////            var already = await _db.Reposts.AnyAsync(r => r.PostId == post.PostId && r.UserId == currentUserId);
////////            if (already) throw new InvalidOperationException("Already reposted");

////////            _db.Reposts.Add(new Repost { PostId = post.PostId, UserId = user.UserId, CreatedAt = DateTime.UtcNow });
////////            post.IsRepost = true;
////////            _db.Posts.Update(post);

////////            await _db.SaveChangesAsync();

////////            return new PostResponseDto
////////            {
////////                PostId = post.PostId,
////////                Title = $"[Repost by {user.FullName}] {post.Title}",
////////                Body = post.Body,
////////                AuthorName = post.User?.FullName ?? "(unknown)",
////////                DepartmentName = post.Dept?.DeptName ?? $"Dept {post.DeptId}",
////////                UpvoteCount = post.UpvoteCount,
////////                DownvoteCount = post.DownvoteCount,
////////                Tags = post.PostTags?.Select(t => t.Tag?.TagName ?? "").Where(s => !string.IsNullOrEmpty(s)).ToList(),
////////                Attachments = post.Attachments?.Select(a => a.FilePath).ToList(),
////////                IsRepost = true,
////////                CreatedAt = DateTime.UtcNow
////////            };
////////        }

////////        public async Task<List<PostResponseDto>> GetRepostsAsync(int postId)
////////        {
////////            var reposts = await _db.Reposts
////////                .Include(r => r.User)
////////                .Include(r => r.Post).ThenInclude(p => p.User)
////////                .Include(r => r.Post).ThenInclude(p => p.Dept)
////////                .Include(r => r.Post).ThenInclude(p => p.PostTags).ThenInclude(pt => pt.Tag)
////////                .Include(r => r.Post).ThenInclude(p => p.Attachments)
////////                .Where(r => r.PostId == postId)
////////                .OrderByDescending(r => r.CreatedAt)
////////                .ToListAsync();

////////            return reposts.Select(r => new PostResponseDto
////////            {
////////                PostId = r.Post?.PostId ?? 0,
////////                Title = $"[Repost by {r.User?.FullName ?? "(unknown)"}] {r.Post?.Title ?? ""}",
////////                Body = r.Post?.Body ?? "",
////////                AuthorName = r.Post?.User?.FullName ?? "(unknown)",
////////                DepartmentName = r.Post?.Dept?.DeptName ?? $"Dept {r.Post?.DeptId}",
////////                UpvoteCount = r.Post?.UpvoteCount ?? 0,
////////                DownvoteCount = r.Post?.DownvoteCount ?? 0,
////////                Tags = r.Post?.PostTags?.Select(t => t.Tag?.TagName ?? "").Where(s => !string.IsNullOrEmpty(s)).ToList(),
////////                Attachments = r.Post?.Attachments?.Select(a => a.FilePath).ToList(),
////////                IsRepost = true,
////////                CreatedAt = r.CreatedAt
////////            }).ToList();
////////        }

////////        public async Task<List<PostResponseDto>> GetRepostsByUserAsync(int userId)
////////        {
////////            var reposts = await _db.Reposts
////////                .Where(r => r.UserId == userId)
////////                .Include(r => r.User)
////////                .Include(r => r.Post).ThenInclude(p => p.User)
////////                .Include(r => r.Post).ThenInclude(p => p.Dept)
////////                .Include(r => r.Post).ThenInclude(p => p.PostTags).ThenInclude(pt => pt.Tag)
////////                .Include(r => r.Post).ThenInclude(p => p.Attachments)
////////                .OrderByDescending(r => r.CreatedAt)
////////                .ToListAsync();

////////            return reposts.Select(r => new PostResponseDto
////////            {
////////                PostId = r.Post?.PostId ?? 0,
////////                Title = $"[Repost by {r.User?.FullName ?? "(unknown)"}] {r.Post?.Title ?? ""}",
////////                Body = r.Post?.Body ?? "",
////////                AuthorName = r.Post?.User?.FullName ?? "(unknown)",
////////                DepartmentName = r.Post?.Dept?.DeptName ?? $"Dept {r.Post?.DeptId}",
////////                UpvoteCount = r.Post?.UpvoteCount ?? 0,
////////                DownvoteCount = r.Post?.DownvoteCount ?? 0,
////////                Tags = r.Post?.PostTags?.Select(t => t.Tag?.TagName ?? "").Where(s => !string.IsNullOrEmpty(s)).ToList(),
////////                Attachments = r.Post?.Attachments?.Select(a => a.FilePath).ToList(),
////////                IsRepost = true,
////////                CreatedAt = r.CreatedAt
////////            }).ToList();
////////        }

////////        public async Task<List<PostResponseDto>> GetAllPostsAsync()
////////        {
////////            var posts = await _db.Posts
////////                .Include(p => p.User).Include(p => p.Dept)
////////                .Include(p => p.PostTags).ThenInclude(pt => pt.Tag)
////////                .Include(p => p.Attachments)
////////                .ToListAsync();

////////            var reposts = await _db.Reposts
////////                .Include(r => r.User)
////////                .Include(r => r.Post).ThenInclude(p => p.User)
////////                .Include(r => r.Post).ThenInclude(p => p.Dept)
////////                .Include(r => r.Post).ThenInclude(p => p.PostTags).ThenInclude(pt => pt.Tag)
////////                .Include(r => r.Post).ThenInclude(p => p.Attachments)
////////                .ToListAsync();

////////            var postDtos = posts.Select(ToDto);
////////            var repostDtos = reposts.Select(r => new PostResponseDto
////////            {
////////                PostId = r.Post?.PostId ?? 0,
////////                Title = $"[Repost by {r.User?.FullName ?? "(unknown)"}] {r.Post?.Title ?? ""}",
////////                Body = r.Post?.Body ?? "",
////////                AuthorName = r.Post?.User?.FullName ?? "(unknown)",
////////                DepartmentName = r.Post?.Dept?.DeptName ?? $"Dept {r.Post?.DeptId}",
////////                UpvoteCount = r.Post?.UpvoteCount ?? 0,
////////                DownvoteCount = r.Post?.DownvoteCount ?? 0,
////////                Tags = r.Post?.PostTags?.Select(t => t.Tag?.TagName ?? "").Where(s => !string.IsNullOrEmpty(s)).ToList(),
////////                Attachments = r.Post?.Attachments?.Select(a => a.FilePath).ToList(),
////////                IsRepost = true,
////////                CreatedAt = r.CreatedAt
////////            });

////////            return postDtos.Concat(repostDtos).OrderByDescending(x => x.CreatedAt).ToList();
////////        }

////////        public async Task<PostResponseDto?> GetPostByIdAsync(int postId)
////////        {
////////            var p = await _db.Posts
////////                .Include(p => p.User).Include(p => p.Dept)
////////                .Include(p => p.PostTags).ThenInclude(pt => pt.Tag)
////////                .Include(p => p.Attachments)
////////                .FirstOrDefaultAsync(p => p.PostId == postId);
////////            if (p == null) return null;
////////            return ToDto(p);
////////        }

////////        public async Task<List<PostResponseDto>> GetMyPostsAsync(int userId)
////////        {
////////            var posts = await _db.Posts
////////                .Where(p => p.UserId == userId)
////////                .Include(p => p.User).Include(p => p.Dept)
////////                .Include(p => p.PostTags).ThenInclude(pt => pt.Tag)
////////                .Include(p => p.Attachments)
////////                .OrderByDescending(p => p.CreatedAt)
////////                .ToListAsync();

////////            return posts.Select(ToDto).ToList();
////////        }

////////        private static string SanitizeFileName(string filename)
////////        {
////////            var name = Path.GetFileName(filename);
////////            foreach (var c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
////////            return name;
////////        }

////////        private static PostResponseDto ToDto(Post p)
////////        {
////////            return new PostResponseDto
////////            {
////////                PostId = p.PostId,
////////                Title = p.Title,
////////                Body = p.Body,
////////                AuthorName = p.User?.FullName ?? "(unknown)",
////////                DepartmentName = p.Dept?.DeptName ?? $"Dept {p.DeptId}",
////////                UpvoteCount = p.UpvoteCount,
////////                DownvoteCount = p.DownvoteCount,
////////                Tags = p.PostTags?.Select(t => t.Tag?.TagName ?? "").Where(s => !string.IsNullOrEmpty(s)).ToList(),
////////                Attachments = p.Attachments?.Select(a => a.FilePath).ToList(),
////////                IsRepost = p.IsRepost,
////////                CreatedAt = p.CreatedAt
////////            };
////////        }
////////    }
////////}


//////using System;
//////using System.Collections.Generic;
//////using System.IO;
//////using System.Linq;
//////using System.Threading.Tasks;
//////using Microsoft.AspNetCore.Hosting;
//////using Microsoft.EntityFrameworkCore;
//////using Microsoft.Extensions.Logging;
//////using FNF_PROJ.Data;
//////using FNF_PROJ.DTOs;

//////namespace FNF_PROJ.Services
//////{
//////    // -------- Interface (same file) --------
//////    public interface IPostService
//////    {
//////        Task<PostResponseDto> CreatePostAsync(int userId, PostCreateDto dto);
//////        Task<PostResponseDto> EditPostAsync(int userId, string role, int deptId, int postId, PostCreateDto dto);
//////        Task DeletePostAsync(int userId, int deptId, int postId, string reason); // logical delete (no schema change)
//////        Task<PostResponseDto> RepostAsync(int currentUserId, RepostDto dto);
//////        Task<List<PostResponseDto>> GetRepostsAsync(int postId);
//////        Task<List<PostResponseDto>> GetRepostsByUserAsync(int userId);
//////        Task<List<PostResponseDto>> GetAllPostsAsync();
//////        Task<PostResponseDto?> GetPostByIdAsync(int postId);
//////        Task<List<PostResponseDto>> GetMyPostsAsync(int userId);
//////    }

//////    // -------- Implementation --------
//////    public class PostService : IPostService
//////    {
//////        private readonly AppDbContext _db;
//////        private readonly IWebHostEnvironment _env;
//////        private readonly ILogger<PostService>? _logger;

//////        public PostService(AppDbContext db, IWebHostEnvironment env, ILogger<PostService>? logger = null)
//////        {
//////            _db = db;
//////            _env = env;
//////            _logger = logger;
//////        }

//////        // ---------- helpers ----------
//////        private static string SanitizeFileName(string filename)
//////        {
//////            var name = Path.GetFileName(filename);
//////            foreach (var c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
//////            return name;
//////        }

//////        private static PostResponseDto ToDto(Post p)
//////        {
//////            return new PostResponseDto
//////            {
//////                PostId = p.PostId,
//////                Title = p.Title,
//////                Body = p.Body,
//////                AuthorName = p.User?.FullName ?? "(unknown)",
//////                DepartmentName = p.Dept?.DeptName ?? $"Dept {p.DeptId}",
//////                UpvoteCount = p.UpvoteCount,
//////                DownvoteCount = p.DownvoteCount,
//////                Tags = p.PostTags?.Select(t => t.Tag?.TagName ?? "").Where(s => !string.IsNullOrEmpty(s)).ToList(),
//////                Attachments = p.Attachments?.Select(a => a.FilePath).ToList(),
//////                IsRepost = p.IsRepost,
//////                CreatedAt = p.CreatedAt
//////            };
//////        }

//////        // post is considered "deleted" if there exists a commit with message starting "DELETE:"
//////        private IQueryable<Post> ExcludeDeleted(IQueryable<Post> q)
//////        {
//////            return q.Where(p => !_db.Commits.Any(c => c.PostId == p.PostId && c.Message.StartsWith("DELETE:")));
//////        }

//////        private async Task<bool> IsLogicallyDeletedAsync(int postId)
//////        {
//////            return await _db.Commits.AnyAsync(c => c.PostId == postId && c.Message.StartsWith("DELETE:"));
//////        }

//////        // ---------- create ----------
//////        public async Task<PostResponseDto> CreatePostAsync(int userId, PostCreateDto dto)
//////        {
//////            var user = await _db.Users.FindAsync(userId) ?? throw new InvalidOperationException("User not found");

//////            var post = new Post
//////            {
//////                Title = dto.Title ?? "",
//////                Body = dto.Body ?? "",
//////                UserId = user.UserId,
//////                DeptId = user.DepartmentId,
//////                CreatedAt = DateTime.UtcNow,
//////                UpvoteCount = 0,
//////                DownvoteCount = 0,
//////                IsRepost = false
//////            };

//////            _db.Posts.Add(post);
//////            await _db.SaveChangesAsync();

//////            if (dto.Attachments != null && dto.Attachments.Any())
//////            {
//////                var webRoot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
//////                var postsFolder = Path.Combine(webRoot, "posts");
//////                if (!Directory.Exists(postsFolder)) Directory.CreateDirectory(postsFolder);

//////                foreach (var file in dto.Attachments)
//////                {
//////                    if (file == null || file.Length == 0) continue;

//////                    var clientFileName = Path.GetFileName(file.FileName);
//////                    var safeFileName = SanitizeFileName(clientFileName);
//////                    var targetPath = Path.Combine(postsFolder, safeFileName);

//////                    if (File.Exists(targetPath))
//////                    {
//////                        var ext = Path.GetExtension(safeFileName);
//////                        var nameOnly = Path.GetFileNameWithoutExtension(safeFileName);
//////                        safeFileName = $"{nameOnly}-{Guid.NewGuid().ToString("n")[..8]}{ext}";
//////                        targetPath = Path.Combine(postsFolder, safeFileName);
//////                    }

//////                    using var stream = new FileStream(targetPath, FileMode.Create);
//////                    await file.CopyToAsync(stream);

//////                    var publicPath = $"/posts/{safeFileName}";
//////                    _db.Attachments.Add(new Attachment
//////                    {
//////                        PostId = post.PostId,
//////                        FileName = safeFileName,
//////                        FilePath = publicPath,
//////                        FileType = file.ContentType ?? "application/octet-stream",
//////                        UploadedAt = DateTime.UtcNow
//////                    });
//////                }
//////                await _db.SaveChangesAsync();
//////            }

//////            if (dto.TagIds != null && dto.TagIds.Any())
//////            {
//////                var requestedTagIds = dto.TagIds.Distinct().ToList();
//////                var validTags = await _db.Tags
//////                    .Where(t => requestedTagIds.Contains(t.TagId) && t.DeptId == user.DepartmentId)
//////                    .ToListAsync();

//////                if (_logger != null && validTags.Count != requestedTagIds.Count)
//////                {
//////                    var invalid = requestedTagIds.Except(validTags.Select(t => t.TagId)).ToList();
//////                    _logger.LogWarning("User {UserId} attempted invalid/out-of-dept tags: {InvalidTagIds}", userId, string.Join(",", invalid));
//////                }

//////                foreach (var tag in validTags)
//////                {
//////                    if (!await _db.PostTags.AnyAsync(pt => pt.PostId == post.PostId && pt.TagId == tag.TagId))
//////                        _db.PostTags.Add(new PostTag { PostId = post.PostId, TagId = tag.TagId });
//////                }
//////                await _db.SaveChangesAsync();
//////            }

//////            var savedPost = await _db.Posts
//////                .Include(p => p.User)
//////                .Include(p => p.Dept)
//////                .Include(p => p.PostTags).ThenInclude(pt => pt.Tag)
//////                .Include(p => p.Attachments)
//////                .FirstOrDefaultAsync(p => p.PostId == post.PostId);

//////            if (savedPost == null) throw new InvalidOperationException("Post not found after creation.");

//////            return ToDto(savedPost);
//////        }

//////        // ---------- edit ----------
//////        public async Task<PostResponseDto> EditPostAsync(int userId, string role, int deptId, int postId, PostCreateDto dto)
//////        {
//////            // prevent editing a logically deleted post
//////            if (await IsLogicallyDeletedAsync(postId)) throw new InvalidOperationException("Post has been deleted.");

//////            var post = await _db.Posts
//////                .Include(p => p.User).Include(p => p.Dept)
//////                .Include(p => p.Attachments)
//////                .Include(p => p.PostTags).ThenInclude(pt => pt.Tag)
//////                .FirstOrDefaultAsync(p => p.PostId == postId)
//////                ?? throw new InvalidOperationException("Post not found");

//////            var isEmployee = string.Equals(role, "Employee", StringComparison.OrdinalIgnoreCase);
//////            var isManager = string.Equals(role, "Manager", StringComparison.OrdinalIgnoreCase);

//////            if (isEmployee && post.UserId != userId) throw new UnauthorizedAccessException();
//////            if (isManager && post.DeptId != (deptId == 0 ? post.DeptId : deptId)) throw new UnauthorizedAccessException();
//////            if (!isEmployee && !isManager) throw new UnauthorizedAccessException();

//////            post.Title = dto.Title ?? post.Title;
//////            post.Body = dto.Body ?? post.Body;
//////            post.UpdatedAt = DateTime.UtcNow;
//////            await _db.SaveChangesAsync();

//////            if (dto.Attachments != null && dto.Attachments.Any())
//////            {
//////                var webRoot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
//////                var postsFolder = Path.Combine(webRoot, "posts");
//////                if (!Directory.Exists(postsFolder)) Directory.CreateDirectory(postsFolder);

//////                foreach (var file in dto.Attachments)
//////                {
//////                    if (file == null || file.Length == 0) continue;

//////                    var clientFileName = Path.GetFileName(file.FileName);
//////                    var safeFileName = SanitizeFileName(clientFileName);
//////                    var targetPath = Path.Combine(postsFolder, safeFileName);

//////                    if (File.Exists(targetPath))
//////                    {
//////                        var ext = Path.GetExtension(safeFileName);
//////                        var nameOnly = Path.GetFileNameWithoutExtension(safeFileName);
//////                        safeFileName = $"{nameOnly}-{Guid.NewGuid().ToString("n")[..8]}{ext}";
//////                        targetPath = Path.Combine(postsFolder, safeFileName);
//////                    }

//////                    using var stream = new FileStream(targetPath, FileMode.Create);
//////                    await file.CopyToAsync(stream);

//////                    var publicPath = $"/posts/{safeFileName}";
//////                    _db.Attachments.Add(new Attachment
//////                    {
//////                        PostId = post.PostId,
//////                        FileName = safeFileName,
//////                        FilePath = publicPath,
//////                        FileType = file.ContentType ?? "application/octet-stream",
//////                        UploadedAt = DateTime.UtcNow
//////                    });
//////                }
//////                await _db.SaveChangesAsync();
//////            }

//////            return await GetPostByIdAsync(post.PostId) ?? ToDto(post);
//////        }

//////        // ---------- logical delete via Commit (no entity/DTO changes) ----------
//////        //public async Task DeletePostAsync(int userId, int deptId, int postId, string reason)
//////        //{
//////        //    if (string.IsNullOrWhiteSpace(reason))
//////        //        throw new InvalidOperationException("Delete reason is required.");

//////        //    var post = await _db.Posts.FirstOrDefaultAsync(p => p.PostId == postId)
//////        //               ?? throw new InvalidOperationException("Post not found");
//////        //    var manager = await _db.Users.FindAsync(userId)
//////        //                  ?? throw new InvalidOperationException("User not found");

//////        //    if (!string.Equals(manager.Role, "Manager", StringComparison.OrdinalIgnoreCase))
//////        //        throw new UnauthorizedAccessException("Only Managers can delete posts.");

//////        //    var managerDept = manager.DepartmentId;
//////        //    if (post.DeptId != managerDept || (deptId != 0 && deptId != managerDept))
//////        //        throw new UnauthorizedAccessException("Manager can delete only posts from their own department.");

//////        //    // log the moderation action (convention: "DELETE: <reason>")
//////        //    _db.Commits.Add(new Commit
//////        //    {
//////        //        PostId = post.PostId,
//////        //        ManagerId = manager.UserId,
//////        //        Message = $"DELETE: {reason.Trim()}",
//////        //        CreatedAt = DateTime.UtcNow
//////        //    });

//////        // do NOT remove the post (avoid FK error; hide it in queries instead)
//////        //    post.UpdatedAt = DateTime.UtcNow;
//////        //    await _db.SaveChangesAsync();
//////        //}


//////        public async Task DeletePostAsync(int userId, int deptId, int postId, string reason)
//////        {
//////            if (string.IsNullOrWhiteSpace(reason))
//////                throw new InvalidOperationException("Delete reason is required.");

//////            var post = await _db.Posts.FirstOrDefaultAsync(p => p.PostId == postId)
//////                       ?? throw new InvalidOperationException("Post not found");
//////            var managerUser = await _db.Users.FindAsync(userId)
//////                              ?? throw new InvalidOperationException("User not found");

//////            if (!string.Equals(managerUser.Role, "Manager", StringComparison.OrdinalIgnoreCase))
//////                throw new UnauthorizedAccessException("Only Managers can delete posts.");

//////            // ✅ get the matching Manager row
//////            var managerRow = await _db.Managers
//////                                      .FirstOrDefaultAsync(m => m.UserId == managerUser.UserId)
//////                              ?? throw new InvalidOperationException("Manager record not found for this user");

//////            var managerDept = managerUser.DepartmentId;
//////            if (post.DeptId != managerDept || (deptId != 0 && deptId != managerDept))
//////                throw new UnauthorizedAccessException("Manager can delete only posts from their own department.");

//////            // ✅ use ManagerId from Managers table
//////            _db.Commits.Add(new Commit
//////            {
//////                PostId = post.PostId,
//////                ManagerId = managerRow.ManagerId,        // << fixed
//////                Message = $"DELETE: {reason.Trim()}",
//////                CreatedAt = DateTime.UtcNow
//////            });

//////            post.UpdatedAt = DateTime.UtcNow;
//////            await _db.SaveChangesAsync();
//////        }


//////        // ---------- repost ----------
//////        public async Task<PostResponseDto> RepostAsync(int currentUserId, RepostDto dto)
//////        {
//////            if (dto == null || dto.PostId <= 0) throw new InvalidOperationException("Invalid repost payload");

//////            // avoid reposting deleted posts
//////            if (await IsLogicallyDeletedAsync(dto.PostId))
//////                throw new InvalidOperationException("Cannot repost a deleted post.");

//////            var user = await _db.Users.FindAsync(currentUserId) ?? throw new InvalidOperationException("User not found");

//////            var post = await _db.Posts
//////                .Include(p => p.User)
//////                .Include(p => p.PostTags).ThenInclude(pt => pt.Tag)
//////                .Include(p => p.Dept)
//////                .Include(p => p.Attachments)
//////                .FirstOrDefaultAsync(p => p.PostId == dto.PostId)
//////                ?? throw new InvalidOperationException("Original post not found");

//////            var already = await _db.Reposts.AnyAsync(r => r.PostId == post.PostId && r.UserId == currentUserId);
//////            if (already) throw new InvalidOperationException("Already reposted");

//////            _db.Reposts.Add(new Repost { PostId = post.PostId, UserId = user.UserId, CreatedAt = DateTime.UtcNow });
//////            post.IsRepost = true;
//////            _db.Posts.Update(post);

//////            await _db.SaveChangesAsync();

//////            return new PostResponseDto
//////            {
//////                PostId = post.PostId,
//////                Title = $"[Repost by {user.FullName}] {post.Title}",
//////                Body = post.Body,
//////                AuthorName = post.User?.FullName ?? "(unknown)",
//////                DepartmentName = post.Dept?.DeptName ?? $"Dept {post.DeptId}",
//////                UpvoteCount = post.UpvoteCount,
//////                DownvoteCount = post.DownvoteCount,
//////                Tags = post.PostTags?.Select(t => t.Tag?.TagName ?? "").Where(s => !string.IsNullOrEmpty(s)).ToList(),
//////                Attachments = post.Attachments?.Select(a => a.FilePath).ToList(),
//////                IsRepost = true,
//////                CreatedAt = DateTime.UtcNow
//////            };
//////        }

//////        // ---------- lists ----------
//////        public async Task<List<PostResponseDto>> GetAllPostsAsync()
//////        {
//////            var posts = await ExcludeDeleted(
//////                _db.Posts
//////                  .Include(p => p.User).Include(p => p.Dept)
//////                  .Include(p => p.PostTags).ThenInclude(pt => pt.Tag)
//////                  .Include(p => p.Attachments))
//////                .ToListAsync();

//////            var reposts = await _db.Reposts
//////                .Include(r => r.User)
//////                .Include(r => r.Post).ThenInclude(p => p.User)
//////                .Include(r => r.Post).ThenInclude(p => p.Dept)
//////                .Include(r => r.Post).ThenInclude(p => p.PostTags).ThenInclude(pt => pt.Tag)
//////                .Include(r => r.Post).ThenInclude(p => p.Attachments)
//////                .Where(r => !_db.Commits.Any(c => c.PostId == r.PostId && c.Message.StartsWith("DELETE:")))
//////                .ToListAsync();

//////            var postDtos = posts.Select(ToDto);
//////            var repostDtos = reposts.Select(r => new PostResponseDto
//////            {
//////                PostId = r.Post?.PostId ?? 0,
//////                Title = $"[Repost by {r.User?.FullName ?? "(unknown)"}] {r.Post?.Title ?? ""}",
//////                Body = r.Post?.Body ?? "",
//////                AuthorName = r.Post?.User?.FullName ?? "(unknown)",
//////                DepartmentName = r.Post?.Dept?.DeptName ?? $"Dept {r.Post?.DeptId}",
//////                UpvoteCount = r.Post?.UpvoteCount ?? 0,
//////                DownvoteCount = r.Post?.DownvoteCount ?? 0,
//////                Tags = r.Post?.PostTags?.Select(t => t.Tag?.TagName ?? "").Where(s => !string.IsNullOrEmpty(s)).ToList(),
//////                Attachments = r.Post?.Attachments?.Select(a => a.FilePath).ToList(),
//////                IsRepost = true,
//////                CreatedAt = r.CreatedAt
//////            });

//////            return postDtos.Concat(repostDtos).OrderByDescending(x => x.CreatedAt).ToList();
//////        }

//////        public async Task<PostResponseDto?> GetPostByIdAsync(int postId)
//////        {
//////            if (await IsLogicallyDeletedAsync(postId)) return null;

//////            var p = await _db.Posts
//////                .Include(p => p.User).Include(p => p.Dept)
//////                .Include(p => p.PostTags).ThenInclude(pt => pt.Tag)
//////                .Include(p => p.Attachments)
//////                .FirstOrDefaultAsync(p => p.PostId == postId);
//////            if (p == null) return null;
//////            return ToDto(p);
//////        }

//////        public async Task<List<PostResponseDto>> GetMyPostsAsync(int userId)
//////        {
//////            var posts = await ExcludeDeleted(
//////                _db.Posts
//////                  .Where(p => p.UserId == userId)
//////                  .Include(p => p.User).Include(p => p.Dept)
//////                  .Include(p => p.PostTags).ThenInclude(pt => pt.Tag)
//////                  .Include(p => p.Attachments))
//////                .OrderByDescending(p => p.CreatedAt)
//////                .ToListAsync();

//////            return posts.Select(ToDto).ToList();
//////        }

//////        public async Task<List<PostResponseDto>> GetRepostsAsync(int postId)
//////        {
//////            if (await IsLogicallyDeletedAsync(postId))
//////                return new List<PostResponseDto>();

//////            var reposts = await _db.Reposts
//////                .Include(r => r.User)
//////                .Include(r => r.Post).ThenInclude(p => p.User)
//////                .Include(r => r.Post).ThenInclude(p => p.Dept)
//////                .Include(r => r.Post).ThenInclude(p => p.PostTags).ThenInclude(pt => pt.Tag)
//////                .Include(r => r.Post).ThenInclude(p => p.Attachments)
//////                .Where(r => r.PostId == postId)
//////                .OrderByDescending(r => r.CreatedAt)
//////                .ToListAsync();

//////            return reposts.Select(r => new PostResponseDto
//////            {
//////                PostId = r.Post?.PostId ?? 0,
//////                Title = $"[Repost by {r.User?.FullName ?? "(unknown)"}] {r.Post?.Title ?? ""}",
//////                Body = r.Post?.Body ?? "",
//////                AuthorName = r.Post?.User?.FullName ?? "(unknown)",
//////                DepartmentName = r.Post?.Dept?.DeptName ?? $"Dept {r.Post?.DeptId}",
//////                UpvoteCount = r.Post?.UpvoteCount ?? 0,
//////                DownvoteCount = r.Post?.DownvoteCount ?? 0,
//////                Tags = r.Post?.PostTags?.Select(t => t.Tag?.TagName ?? "").Where(s => !string.IsNullOrEmpty(s)).ToList(),
//////                Attachments = r.Post?.Attachments?.Select(a => a.FilePath).ToList(),
//////                IsRepost = true,
//////                CreatedAt = r.CreatedAt
//////            }).ToList();
//////        }

//////        public async Task<List<PostResponseDto>> GetRepostsByUserAsync(int userId)
//////        {
//////            var reposts = await _db.Reposts
//////                .Where(r => r.UserId == userId &&
//////                            !_db.Commits.Any(c => c.PostId == r.PostId && c.Message.StartsWith("DELETE:")))
//////                .Include(r => r.User)
//////                .Include(r => r.Post).ThenInclude(p => p.User)
//////                .Include(r => r.Post).ThenInclude(p => p.Dept)
//////                .Include(r => r.Post).ThenInclude(p => p.PostTags).ThenInclude(pt => pt.Tag)
//////                .Include(r => r.Post).ThenInclude(p => p.Attachments)
//////                .OrderByDescending(r => r.CreatedAt)
//////                .ToListAsync();

//////            return reposts.Select(r => new PostResponseDto
//////            {
//////                PostId = r.Post?.PostId ?? 0,
//////                Title = $"[Repost by {r.User?.FullName ?? "(unknown)"}] {r.Post?.Title ?? ""}",
//////                Body = r.Post?.Body ?? "",
//////                AuthorName = r.Post?.User?.FullName ?? "(unknown)",
//////                DepartmentName = r.Post?.Dept?.DeptName ?? $"Dept {r.Post?.DeptId}",
//////                UpvoteCount = r.Post?.UpvoteCount ?? 0,
//////                DownvoteCount = r.Post?.DownvoteCount ?? 0,
//////                Tags = r.Post?.PostTags?.Select(t => t.Tag?.TagName ?? "").Where(s => !string.IsNullOrEmpty(s)).ToList(),
//////                Attachments = r.Post?.Attachments?.Select(a => a.FilePath).ToList(),
//////                IsRepost = true,
//////                CreatedAt = r.CreatedAt
//////            }).ToList();
//////        }
//////    }
//////}


////using Microsoft.AspNetCore.Authorization;
////using Microsoft.AspNetCore.Mvc;
////using System.Security.Claims;
////using FNF_PROJ.Services;
////using FNF_PROJ.DTOs;
////using Microsoft.Extensions.Logging;
////using System.Linq;
////using System;
////using System.Collections.Generic;

////namespace FNF_PROJ.Controllers
////{
////    [ApiController]
////    [Route("api/[controller]")]
////    public class PostsController : ControllerBase
////    {
////        private readonly IPostService _postService;
////        private readonly ILogger<PostsController> _logger;
////        private readonly FNF_PROJ.Data.AppDbContext _db;

////        public PostsController(IPostService postService, ILogger<PostsController> logger, FNF_PROJ.Data.AppDbContext db)
////        {
////            _postService = postService;
////            _logger = logger;
////            _db = db;
////        }

////        private int GetCurrentUserId()
////        {
////            string? userIdClaim =
////                User.FindFirst(ClaimTypes.NameIdentifier)?.Value
////                ?? User.FindFirst("sub")?.Value
////                ?? User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;

////            return int.TryParse(userIdClaim, out var uid) ? uid : 0;
////        }

////        // -------------------- CREATE --------------------
////        [HttpPost]
////        [Authorize]
////        [Consumes("multipart/form-data")]
////        public async Task<IActionResult> CreatePost([FromForm] PostCreateDto dto)
////        {
////            try
////            {
////                NormalizeTagIdsFromForm(dto);

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

////        // -------------------- EDIT (UPDATE TAGS HERE) --------------------
////        [HttpPut("{id:int}")]
////        [Authorize]
////        [Consumes("multipart/form-data")]
////        public async Task<IActionResult> EditPost(int id, [FromForm] PostCreateDto dto)
////        {
////            try
////            {
////                NormalizeTagIdsFromForm(dto);

////                var uid = GetCurrentUserId();
////                if (uid == 0) return Unauthorized(new { Error = "Invalid user id" });

////                var me = await _db.Users.FindAsync(uid);
////                if (me == null) return Unauthorized(new { Error = "User not found" });

////                var updated = await _postService.EditPostAsync(
////                    uid,
////                    me.Role ?? "Employee",
////                    me.DepartmentId,
////                    id,
////                    dto
////                );

////                return Ok(updated);
////            }
////            catch (Exception ex)
////            {
////                _logger.LogError(ex, "Error editing post {PostId}", id);
////                if (ex is UnauthorizedAccessException) return Forbid();
////                return BadRequest(new { Error = ex.Message });
////            }
////        }

////        // -------------------- READ --------------------
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

////        // (optional) used by frontend MyPosts
////        [HttpGet("mine")]
////        [Authorize]
////        public async Task<IActionResult> GetMine()
////        {
////            var uid = GetCurrentUserId();
////            if (uid == 0) return Unauthorized();
////            var list = await _postService.GetPostsByUserAsync(uid);
////            return Ok(list);
////        }

////        // -------------------- helpers --------------------
////        private void NormalizeTagIdsFromForm(PostCreateDto dto)
////        {
////            // If MVC model binder didn’t bind TagIds correctly from multipart
////            if ((dto.TagIds == null || !dto.TagIds.Any()) && Request.HasFormContentType)
////            {
////                var form = Request.Form;
////                var parsed = new List<int>();

////                foreach (var val in form["TagIds"])
////                    if (int.TryParse(val, out var id)) parsed.Add(id);

////                foreach (var kv in form.Where(kv => kv.Key.StartsWith("TagIds[")))
////                    foreach (var val in kv.Value)
////                        if (int.TryParse(val, out var id)) parsed.Add(id);

////                if (form.TryGetValue("TagIds", out var csvVals))
////                    foreach (var s in csvVals)
////                        foreach (var part in s.Split(',', StringSplitOptions.RemoveEmptyEntries))
////                            if (int.TryParse(part.Trim(), out var id)) parsed.Add(id);

////                if (parsed.Any()) dto.TagIds = parsed.Distinct().ToList();
////            }
////        }
////    }
////}

//using System;
//using System.Collections.Generic;
//using System.IO;
//using System.Linq;
//using System.Threading.Tasks;
//using FNF_PROJ.Data;
//using FNF_PROJ.DTOs;
//using Microsoft.AspNetCore.Hosting;
//using Microsoft.EntityFrameworkCore;
//using Microsoft.Extensions.Logging;

//namespace FNF_PROJ.Services
//{
//    // Interface + implementation in the same file
//    public interface IPostService
//    {
//        Task<PostResponseDto> CreatePostAsync(int userId, PostCreateDto dto);
//        Task<PostResponseDto> EditPostAsync(int userId, string role, int deptId, int postId, PostCreateDto dto);
//        Task<List<PostResponseDto>> GetAllPostsAsync();
//        Task<PostResponseDto?> GetPostByIdAsync(int postId);
//        Task<List<PostResponseDto>> GetPostsByUserAsync(int userId);
//        // (other methods you already have: Delete/Repost, etc.)
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

//            // attachments
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
//                        safeFileName = $"{nameOnly}-{Guid.NewGuid():N}".Substring(0, nameOnly.Length > 0 ? nameOnly.Length : 8) + ext;
//                        targetPath = Path.Combine(postsFolder, safeFileName);
//                    }

//                    using (var stream = new FileStream(targetPath, FileMode.Create))
//                        await file.CopyToAsync(stream);

//                    var publicPath = $"/posts/{safeFileName}";

//                    _db.Attachments.Add(new Attachment
//                    {
//                        PostId = post.PostId,
//                        FileName = safeFileName,
//                        FilePath = publicPath,
//                        FileType = file.ContentType ?? "application/octet-stream",
//                        UploadedAt = DateTime.UtcNow
//                    });
//                }
//                await _db.SaveChangesAsync();
//            }

//            // tags (validate by user's dept)
//            if (dto.TagIds != null && dto.TagIds.Any())
//            {
//                var requested = dto.TagIds.Distinct().ToList();
//                var validTags = await _db.Tags
//                    .Where(t => requested.Contains(t.TagId) && t.DeptId == user.DepartmentId)
//                    .Select(t => t.TagId)
//                    .ToListAsync();

//                foreach (var tid in validTags)
//                    _db.PostTags.Add(new PostTag { PostId = post.PostId, TagId = tid });

//                await _db.SaveChangesAsync();
//            }

//            // build dto
//            var saved = await _db.Posts
//                .Include(p => p.User)
//                .Include(p => p.Dept)
//                .Include(p => p.PostTags).ThenInclude(pt => pt.Tag)
//                .Include(p => p.Attachments)
//                .FirstOrDefaultAsync(p => p.PostId == post.PostId);

//            var tags = saved?.PostTags?.Where(x => x.Tag != null).Select(x => x.Tag.TagName).ToList() ?? new List<string>();
//            var files = saved?.Attachments?.Select(a => a.FilePath).ToList() ?? new List<string>();

//            return new PostResponseDto
//            {
//                PostId = post.PostId,
//                Title = post.Title,
//                Body = post.Body,
//                AuthorName = saved?.User?.FullName ?? "(unknown)",
//                DepartmentName = saved?.Dept?.DeptName ?? $"Dept {post.DeptId}",
//                UpvoteCount = post.UpvoteCount,
//                DownvoteCount = post.DownvoteCount,
//                Tags = tags,
//                Attachments = files,
//                IsRepost = post.IsRepost,
//                CreatedAt = post.CreatedAt
//            };
//        }

//        // ✅ EDIT (including Tags replace)
//        public async Task<PostResponseDto> EditPostAsync(int userId, string role, int deptId, int postId, PostCreateDto dto)
//        {
//            var post = await _db.Posts
//                .Include(p => p.User)
//                .Include(p => p.Dept)
//                .Include(p => p.Attachments)
//                .Include(p => p.PostTags).ThenInclude(pt => pt.Tag)
//                .FirstOrDefaultAsync(p => p.PostId == postId)
//                ?? throw new InvalidOperationException("Post not found");

//            // Authorization
//            if (string.Equals(role, "Employee", StringComparison.OrdinalIgnoreCase))
//            {
//                if (post.UserId != userId) throw new UnauthorizedAccessException();
//            }
//            else if (string.Equals(role, "Manager", StringComparison.OrdinalIgnoreCase))
//            {
//                if (post.DeptId != deptId) throw new UnauthorizedAccessException();
//            }
//            else throw new UnauthorizedAccessException();

//            // Update basic fields
//            if (dto.Title != null) post.Title = dto.Title;
//            if (dto.Body != null) post.Body = dto.Body;
//            post.UpdatedAt = DateTime.UtcNow;
//            await _db.SaveChangesAsync();

//            // New attachments (append-only)
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
//                        safeFileName = $"{nameOnly}-{Guid.NewGuid():N}".Substring(0, nameOnly.Length > 0 ? nameOnly.Length : 8) + ext;
//                        targetPath = Path.Combine(postsFolder, safeFileName);
//                    }

//                    using (var stream = new FileStream(targetPath, FileMode.Create))
//                        await file.CopyToAsync(stream);

//                    var publicPath = $"/posts/{safeFileName}";

//                    _db.Attachments.Add(new Attachment
//                    {
//                        PostId = post.PostId,
//                        FileName = safeFileName,
//                        FilePath = publicPath,
//                        FileType = file.ContentType ?? "application/octet-stream",
//                        UploadedAt = DateTime.UtcNow
//                    });
//                }
//                await _db.SaveChangesAsync();
//            }

//            // ✅ Replace Tags (validate by post.DeptId)
//            if (dto.TagIds != null)
//            {
//                var requested = dto.TagIds.Distinct().ToList();

//                var validTagIds = await _db.Tags
//                    .Where(t => requested.Contains(t.TagId) && t.DeptId == post.DeptId)
//                    .Select(t => t.TagId)
//                    .ToListAsync();

//                var existing = await _db.PostTags.Where(pt => pt.PostId == post.PostId).ToListAsync();
//                if (existing.Count > 0)
//                {
//                    _db.PostTags.RemoveRange(existing);
//                    await _db.SaveChangesAsync();
//                }

//                foreach (var tid in validTagIds)
//                    _db.PostTags.Add(new PostTag { PostId = post.PostId, TagId = tid });

//                await _db.SaveChangesAsync();
//            }

//            // Build response
//            await _db.Entry(post).ReloadAsync();
//            var attachments = await _db.Attachments.Where(a => a.PostId == post.PostId).ToListAsync();
//            var tagNames = await _db.PostTags.Where(pt => pt.PostId == post.PostId)
//                .Include(pt => pt.Tag)
//                .Select(pt => pt.Tag.TagName)
//                .ToListAsync();

//            return new PostResponseDto
//            {
//                PostId = post.PostId,
//                Title = post.Title,
//                Body = post.Body,
//                AuthorName = post.User?.FullName ?? "(unknown)",
//                DepartmentName = post.Dept?.DeptName ?? $"Dept {post.DeptId}",
//                UpvoteCount = post.UpvoteCount,
//                DownvoteCount = post.DownvoteCount,
//                Tags = tagNames,
//                Attachments = attachments.Select(a => a.FilePath).ToList(),
//                IsRepost = post.IsRepost,
//                CreatedAt = post.CreatedAt
//            };
//        }

//        public async Task<List<PostResponseDto>> GetAllPostsAsync()
//        {
//            var posts = await _db.Posts
//                .Include(p => p.User).Include(p => p.Dept)
//                .Include(p => p.PostTags).ThenInclude(pt => pt.Tag)
//                .Include(p => p.Attachments).ToListAsync();

//            return posts.Select(p => new PostResponseDto
//            {
//                PostId = p.PostId,
//                Title = p.Title,
//                Body = p.Body,
//                AuthorName = p.User?.FullName ?? "(unknown)",
//                DepartmentName = p.Dept?.DeptName ?? $"Dept {p.DeptId}",
//                UpvoteCount = p.UpvoteCount,
//                DownvoteCount = p.DownvoteCount,
//                Tags = p.PostTags?.Where(pt => pt.Tag != null).Select(pt => pt.Tag.TagName).ToList() ?? new List<string>(),
//                Attachments = p.Attachments?.Select(a => a.FilePath).ToList() ?? new List<string>(),
//                IsRepost = p.IsRepost,
//                CreatedAt = p.CreatedAt
//            }).OrderByDescending(x => x.CreatedAt).ToList();
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
//                AuthorName = p.User?.FullName ?? "(unknown)",
//                DepartmentName = p.Dept?.DeptName ?? $"Dept {p.DeptId}",
//                UpvoteCount = p.UpvoteCount,
//                DownvoteCount = p.DownvoteCount,
//                Tags = p.PostTags?.Where(pt => pt.Tag != null).Select(pt => pt.Tag.TagName).ToList() ?? new List<string>(),
//                Attachments = p.Attachments?.Select(a => a.FilePath).ToList() ?? new List<string>(),
//                IsRepost = p.IsRepost,
//                CreatedAt = p.CreatedAt
//            };
//        }

//        public async Task<List<PostResponseDto>> GetPostsByUserAsync(int userId)
//        {
//            var posts = await _db.Posts
//                .Where(p => p.UserId == userId)
//                .Include(p => p.User).Include(p => p.Dept)
//                .OrderByDescending(p => p.CreatedAt)
//                .ToListAsync();

//            // lightweight list (title + meta); body/attachments can be fetched via GetPostById on UI if needed
//            return posts.Select(p => new PostResponseDto
//            {
//                PostId = p.PostId,
//                Title = p.Title,
//                Body = p.Body, // keep included – you can ignore on UI if you prefer
//                AuthorName = p.User?.FullName ?? "(unknown)",
//                DepartmentName = p.Dept?.DeptName ?? $"Dept {p.DeptId}",
//                UpvoteCount = p.UpvoteCount,
//                DownvoteCount = p.DownvoteCount,
//                Tags = new List<string>(),
//                Attachments = new List<string>(),
//                IsRepost = p.IsRepost,
//                CreatedAt = p.CreatedAt
//            }).ToList();
//        }

//        private static string SanitizeFileName(string filename)
//        {
//            var name = Path.GetFileName(filename);
//            foreach (var c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
//            return name;
//        }
//    }
//}


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

            // Attachments
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
                        safeFileName = $"{nameOnly}-{Guid.NewGuid():N}".Substring(0, 8) + ext;
                        targetPath = Path.Combine(postsFolder, safeFileName);
                    }

                    using (var stream = new FileStream(targetPath, FileMode.Create))
                        await file.CopyToAsync(stream);

                    var publicPath = $"/posts/{safeFileName}";

                    _db.Attachments.Add(new Attachment
                    {
                        PostId = post.PostId,
                        FileName = safeFileName,
                        FilePath = publicPath,
                        FileType = file.ContentType ?? "application/octet-stream",
                        UploadedAt = DateTime.UtcNow
                    });
                }

                await _db.SaveChangesAsync();
            }

            // Tags (validate by user's dept)
            if (dto.TagIds != null && dto.TagIds.Any())
            {
                var requested = dto.TagIds.Distinct().ToList();
                var validTags = await _db.Tags
                    .Where(t => requested.Contains(t.TagId) && t.DeptId == user.DepartmentId)
                    .Select(t => t.TagId)
                    .ToListAsync();

                foreach (var tid in validTags)
                    _db.PostTags.Add(new PostTag { PostId = post.PostId, TagId = tid });

                await _db.SaveChangesAsync();
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

            // Authorization
            if (string.Equals(role, "Employee", StringComparison.OrdinalIgnoreCase))
            {
                if (post.UserId != userId) throw new UnauthorizedAccessException();
            }
            else if (string.Equals(role, "Manager", StringComparison.OrdinalIgnoreCase))
            {
                if (post.DeptId != deptId) throw new UnauthorizedAccessException();
            }
            else throw new UnauthorizedAccessException();

            // Update text/body
            if (dto.Title != null) post.Title = dto.Title;
            if (dto.Body != null) post.Body = dto.Body;
            post.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            // Append attachments
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
                        safeFileName = $"{nameOnly}-{Guid.NewGuid():N}".Substring(0, 8) + ext;
                        targetPath = Path.Combine(postsFolder, safeFileName);
                    }

                    using (var stream = new FileStream(targetPath, FileMode.Create))
                        await file.CopyToAsync(stream);

                    var publicPath = $"/posts/{safeFileName}";
                    _db.Attachments.Add(new Attachment
                    {
                        PostId = post.PostId,
                        FileName = safeFileName,
                        FilePath = publicPath,
                        FileType = file.ContentType ?? "application/octet-stream",
                        UploadedAt = DateTime.UtcNow
                    });
                }
                await _db.SaveChangesAsync();
            }

            // Replace tags (validate by post dept)
            if (dto.TagIds != null)
            {
                var requested = dto.TagIds.Distinct().ToList();
                var validTagIds = await _db.Tags
                    .Where(t => requested.Contains(t.TagId) && t.DeptId == post.DeptId)
                    .Select(t => t.TagId)
                    .ToListAsync();

                var existing = await _db.PostTags.Where(pt => pt.PostId == post.PostId).ToListAsync();
                if (existing.Count > 0)
                {
                    _db.PostTags.RemoveRange(existing);
                    await _db.SaveChangesAsync();
                }

                foreach (var tid in validTagIds)
                    _db.PostTags.Add(new PostTag { PostId = post.PostId, TagId = tid });

                await _db.SaveChangesAsync();
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
                          ?? throw new UnauthorizedAccessException(); // must exist in Managers table

            var post = await _db.Posts.FirstOrDefaultAsync(p => p.PostId == postId)
                       ?? throw new InvalidOperationException("Post not found");

            if (post.DeptId != deptId)
                throw new UnauthorizedAccessException();

            // record a Commit entry with reason (no entity changes assumed)
            _db.Commits.Add(new Commit
            {
                PostId = post.PostId,
                ManagerId = manager.ManagerId,
                Message = string.IsNullOrWhiteSpace(reason) ? "Deleted by manager" : reason,
                CreatedAt = DateTime.UtcNow
            });
            await _db.SaveChangesAsync();

            // remove dependents explicitly to avoid FK/cascade issues
            var attachments = await _db.Attachments.Where(a => a.PostId == post.PostId).ToListAsync();
            if (attachments.Count > 0) _db.Attachments.RemoveRange(attachments);

            var postTags = await _db.PostTags.Where(pt => pt.PostId == post.PostId).ToListAsync();
            if (postTags.Count > 0) _db.PostTags.RemoveRange(postTags);

            var reposts = await _db.Reposts.Where(r => r.PostId == post.PostId).ToListAsync();
            if (reposts.Count > 0) _db.Reposts.RemoveRange(reposts);

            // (optional) remove votes/comments if you have them:
            // var votes = await _db.Votes.Where(v => v.PostId == post.PostId).ToListAsync();
            // if (votes.Count > 0) _db.Votes.RemoveRange(votes);
            // var comments = await _db.Comments.Where(c => c.PostId == post.PostId).ToListAsync();
            // if (comments.Count > 0) _db.Comments.RemoveRange(comments);

            await _db.SaveChangesAsync();

            _db.Posts.Remove(post);
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

            // mark original as having at least one repost (if you use IsRepost this way)
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
                UpvoteCount = post.UpvoteCount,
                DownvoteCount = post.DownvoteCount,
                Tags = post.PostTags?.Where(t => t.Tag != null).Select(t => t.Tag.TagName).ToList(),
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
                UpvoteCount = r.Post.UpvoteCount,
                DownvoteCount = r.Post.DownvoteCount,
                Tags = r.Post.PostTags?.Where(t => t.Tag != null).Select(t => t.Tag.TagName).ToList(),
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
                AuthorName = r.Post.User?.FullName ?? "(unknown)",
                DepartmentName = r.Post.Dept?.DeptName ?? $"Dept {r.Post.DeptId}",
                UpvoteCount = r.Post.UpvoteCount,
                DownvoteCount = r.Post.DownvoteCount,
                Tags = r.Post.PostTags?.Where(t => t.Tag != null).Select(t => t.Tag.TagName).ToList(),
                Attachments = r.Post.Attachments?.Select(a => a.FilePath).ToList(),
                IsRepost = true,
                CreatedAt = r.CreatedAt
            }).ToList();
        }

        // ---------------- READS ----------------
        public async Task<List<PostResponseDto>> GetAllPostsAsync()
        {
            var posts = await _db.Posts
                .Include(p => p.User).Include(p => p.Dept)
                .Include(p => p.PostTags).ThenInclude(pt => pt.Tag)
                .Include(p => p.Attachments)
                .ToListAsync();

            return posts.Select(p => new PostResponseDto
            {
                PostId = p.PostId,
                Title = p.Title,
                Body = p.Body,
                AuthorName = p.User?.FullName ?? "(unknown)",
                DepartmentName = p.Dept?.DeptName ?? $"Dept {p.DeptId}",
                UpvoteCount = p.UpvoteCount,
                DownvoteCount = p.DownvoteCount,
                Tags = p.PostTags?.Where(pt => pt.Tag != null).Select(pt => pt.Tag.TagName).ToList() ?? new List<string>(),
                Attachments = p.Attachments?.Select(a => a.FilePath).ToList() ?? new List<string>(),
                IsRepost = p.IsRepost,
                CreatedAt = p.CreatedAt
            }).OrderByDescending(x => x.CreatedAt).ToList();
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
                AuthorName = p.User?.FullName ?? "(unknown)",
                DepartmentName = p.Dept?.DeptName ?? $"Dept {p.DeptId}",
                UpvoteCount = p.UpvoteCount,
                DownvoteCount = p.DownvoteCount,
                Tags = p.PostTags?.Where(pt => pt.Tag != null).Select(pt => pt.Tag.TagName).ToList() ?? new List<string>(),
                Attachments = p.Attachments?.Select(a => a.FilePath).ToList() ?? new List<string>(),
                IsRepost = p.IsRepost,
                CreatedAt = p.CreatedAt
            };
        }

        public async Task<List<PostResponseDto>> GetPostsByUserAsync(int userId)
        {
            var posts = await _db.Posts
                .Where(p => p.UserId == userId)
                .Include(p => p.User).Include(p => p.Dept)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            // Lightweight; UI can hydrate with GetPostById if needed
            return posts.Select(p => new PostResponseDto
            {
                PostId = p.PostId,
                Title = p.Title,
                Body = p.Body,
                AuthorName = p.User?.FullName ?? "(unknown)",
                DepartmentName = p.Dept?.DeptName ?? $"Dept {p.DeptId}",
                UpvoteCount = p.UpvoteCount,
                DownvoteCount = p.DownvoteCount,
                Tags = new List<string>(),
                Attachments = new List<string>(),
                IsRepost = p.IsRepost,
                CreatedAt = p.CreatedAt
            }).ToList();
        }

        public Task<List<PostResponseDto>> GetMyPostsAsync(int userId) => GetPostsByUserAsync(userId);

        // ---------------- helpers ----------------
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
                UpvoteCount = p.UpvoteCount,
                DownvoteCount = p.DownvoteCount,
                Tags = p.PostTags?.Where(pt => pt.Tag != null).Select(pt => pt.Tag.TagName).ToList() ?? new List<string>(),
                Attachments = p.Attachments?.Select(a => a.FilePath).ToList() ?? new List<string>(),
                IsRepost = p.IsRepost,
                CreatedAt = p.CreatedAt
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


