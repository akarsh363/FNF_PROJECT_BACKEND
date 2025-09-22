// ======================= PostService.cs =======================
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using FNF_PROJ.Data;
using FNF_PROJ.DTOs;

namespace FNF_PROJ.Services
{
    public interface IPostService
    {
        Task<PostResponseDto> CreatePostAsync(int userId, PostCreateDto dto);
        Task<PostResponseDto> EditPostAsync(int userId, string role, int deptId, int postId, PostCreateDto dto);
        Task DeletePostAsync(int userId, int deptId, int postId);
        Task<PostResponseDto> RepostAsync(int currentUserId, RepostDto dto);
        Task<List<PostResponseDto>> GetRepostsAsync(int postId);
        Task<List<PostResponseDto>> GetAllPostsAsync();
        Task<PostResponseDto?> GetPostByIdAsync(int postId);
    }

    public class PostService : IPostService
    {
        private readonly AppDbContext _db;
        public PostService(AppDbContext db) { _db = db; }

        public async Task<PostResponseDto> CreatePostAsync(int userId, PostCreateDto dto)
        {
            var user = await _db.Users.FindAsync(userId) ?? throw new InvalidOperationException("User not found");
            var post = new Post
            {
                Title = dto.Title,
                Body = dto.Body,
                UserId = user.UserId,
                DeptId = user.DepartmentId,
                CreatedAt = DateTime.UtcNow,
                UpvoteCount = 0,
                DownvoteCount = 0,
                IsRepost = false
            };
            _db.Posts.Add(post);
            await _db.SaveChangesAsync();
            return new PostResponseDto
            {
                PostId = post.PostId,
                Title = post.Title,
                Body = post.Body,
                AuthorName = user.FullName,
                DepartmentName = post.Dept?.DeptName ?? $"Dept {post.DeptId}",
                UpvoteCount = post.UpvoteCount,
                DownvoteCount = post.DownvoteCount,
                Tags = new List<string>(),
                Attachments = new List<string>(),
                IsRepost = false,
                CreatedAt = post.CreatedAt
            };
        }

        public async Task<PostResponseDto> EditPostAsync(int userId, string role, int deptId, int postId, PostCreateDto dto)
        {
            var post = await _db.Posts.Include(p => p.User).Include(p => p.Dept)
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

            post.Title = dto.Title;
            post.Body = dto.Body;
            post.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            return new PostResponseDto
            {
                PostId = post.PostId,
                Title = post.Title,
                Body = post.Body,
                AuthorName = post.User.FullName,
                DepartmentName = post.Dept?.DeptName ?? $"Dept {post.DeptId}",
                UpvoteCount = post.UpvoteCount,
                DownvoteCount = post.DownvoteCount,
                Tags = post.PostTags?.Select(t => t.Tag.TagName).ToList(),
                Attachments = post.Attachments?.Select(a => a.FilePath).ToList(),
                IsRepost = post.IsRepost,
                CreatedAt = post.CreatedAt
            };
        }

        public async Task DeletePostAsync(int userId, int deptId, int postId)
        {
            var post = await _db.Posts.FirstOrDefaultAsync(p => p.PostId == postId)
                       ?? throw new InvalidOperationException("Post not found");
            var user = await _db.Users.FindAsync(userId) ?? throw new InvalidOperationException("User not found");
            if (!string.Equals(user.Role, "Manager", StringComparison.OrdinalIgnoreCase)) throw new UnauthorizedAccessException();
            if (post.DeptId != deptId) throw new UnauthorizedAccessException();
            _db.Posts.Remove(post);
            await _db.SaveChangesAsync();
        }

        public async Task<PostResponseDto> RepostAsync(int currentUserId, RepostDto dto)
        {
            var user = await _db.Users.FindAsync(currentUserId) ?? throw new InvalidOperationException("User not found");
            var post = await _db.Posts.Include(p => p.User)
                                      .Include(p => p.PostTags).ThenInclude(pt => pt.Tag)
                                      .Include(p => p.Dept)
                                      .Include(p => p.Attachments)
                                      .FirstOrDefaultAsync(p => p.PostId == dto.PostId)
                       ?? throw new InvalidOperationException("Original post not found");

            var role = (user.Role ?? "Employee").Trim();
            var isManager = string.Equals(role, "Manager", StringComparison.OrdinalIgnoreCase);
            var isEmployee = string.Equals(role, "Employee", StringComparison.OrdinalIgnoreCase);

            if (isEmployee && post.UserId != currentUserId) throw new UnauthorizedAccessException();
            if (isManager && post.DeptId != user.DepartmentId) throw new UnauthorizedAccessException();

            var already = await _db.Reposts.AnyAsync(r => r.PostId == post.PostId && r.UserId == currentUserId);
            if (already) throw new InvalidOperationException("Already reposted");

            var repost = new Repost { PostId = post.PostId, UserId = user.UserId, CreatedAt = DateTime.UtcNow };
            _db.Reposts.Add(repost);
            await _db.SaveChangesAsync();

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
                IsRepost = false,
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
    }
}
