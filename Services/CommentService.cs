using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using FNF_PROJ.Data;
using FNF_PROJ.DTOs;

namespace FNF_PROJ.Services
{
    public class CommentService
    {
        private readonly AppDbContext _db;
        private readonly IWebHostEnvironment _env;

        public CommentService(AppDbContext db, IWebHostEnvironment env)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _env = env ?? throw new ArgumentNullException(nameof(env));
        }

        private static string SanitizeFileName(string filename)
        {
            var name = Path.GetFileName(filename);
            foreach (var c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name;
        }

        private async Task<string> SaveCommentFileAsync(IFormFile file)
        {
            var webRoot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            var folder = Path.Combine(webRoot, "comments");
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

            var safe = SanitizeFileName(file.FileName);
            var fileName = $"{Path.GetFileNameWithoutExtension(safe)}-{Guid.NewGuid():N}{Path.GetExtension(safe)}";
            var filePath = Path.Combine(folder, fileName);

            using var stream = new FileStream(filePath, FileMode.Create);
            await file.CopyToAsync(stream);

            return $"/comments/{fileName}";
        }

        // ✅ Create comment
        public async Task<CommentResponseDto> CreateAsync(int userId, CommentCreateDto dto)
        {
            if (dto == null) throw new ArgumentNullException(nameof(dto));
            if (string.IsNullOrWhiteSpace(dto.CommentText)) dto.CommentText = "";

            var post = await _db.Posts.FirstOrDefaultAsync(p => p.PostId == dto.PostId)
                       ?? throw new InvalidOperationException("Post not found");

            if (dto.ParentCommentId.HasValue)
            {
                var parent = await _db.Comments.FirstOrDefaultAsync(c => c.CommentId == dto.ParentCommentId.Value)
                             ?? throw new InvalidOperationException("Parent comment not found");
                if (parent.PostId != dto.PostId)
                    throw new InvalidOperationException("Parent comment does not belong to the same post");
            }

            var comment = new Comment
            {
                PostId = dto.PostId,
                UserId = userId,
                ParentCommentId = dto.ParentCommentId,
                CommentText = dto.CommentText.Trim(),
                CreatedAt = DateTime.UtcNow
            };

            await using var tx = await _db.Database.BeginTransactionAsync();

            _db.Comments.Add(comment);
            await _db.SaveChangesAsync();

            var attachments = new List<Attachment>();
            if (dto.Attachments != null && dto.Attachments.Any())
            {
                foreach (var file in dto.Attachments)
                {
                    if (file == null || file.Length == 0) continue;
                    var publicPath = await SaveCommentFileAsync(file);
                    var a = new Attachment
                    {
                        CommentId = comment.CommentId,
                        FileName = Path.GetFileName(publicPath),
                        FilePath = publicPath,
                        FileType = file.ContentType ?? "application/octet-stream",
                        UploadedAt = DateTime.UtcNow
                    };
                    attachments.Add(a);
                    _db.Attachments.Add(a);
                }
                await _db.SaveChangesAsync();
            }

            await tx.CommitAsync();

            var user = await _db.Users.FindAsync(userId);
            return new CommentResponseDto
            {
                CommentId = comment.CommentId,
                CommentText = comment.CommentText,
                AuthorName = user?.FullName ?? user?.Email ?? "(unknown)",
                CreatedAt = comment.CreatedAt,
                Attachments = attachments.Select(a => new AttachmentDto { FileName = a.FileName, FilePath = a.FilePath }).ToList(),
                Replies = new List<CommentResponseDto>(),
                LikeCount = 0,
                DislikeCount = 0,
                UserVote = 0
            };
        }

        // ✅ Edit comment
        public async Task<CommentResponseDto> EditAsync(int userId, int commentId, CommentCreateDto dto)
        {
            var comment = await _db.Comments
                .Include(c => c.User)
                .Include(c => c.Attachments)
                .FirstOrDefaultAsync(c => c.CommentId == commentId)
                ?? throw new InvalidOperationException("Comment not found");

            if (comment.UserId != userId) throw new UnauthorizedAccessException();

            comment.CommentText = dto?.CommentText ?? comment.CommentText;
            comment.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            return new CommentResponseDto
            {
                CommentId = comment.CommentId,
                CommentText = comment.CommentText,
                AuthorName = comment.User?.FullName ?? "(unknown)",
                CreatedAt = comment.CreatedAt,
                Attachments = comment.Attachments?.Select(a => new AttachmentDto { FileName = a.FileName, FilePath = a.FilePath }).ToList(),
                Replies = new List<CommentResponseDto>(),
                LikeCount = comment.Votes?.Count(v => v.VoteType == "Upvote") ?? 0,
                DislikeCount = comment.Votes?.Count(v => v.VoteType == "Downvote") ?? 0,
                UserVote = 0
            };
        }

        // ✅ Delete comment
        public async Task DeleteAsync(int userId, int commentId)
        {
            var root = await _db.Comments.FirstOrDefaultAsync(c => c.CommentId == commentId)
                       ?? throw new InvalidOperationException("Comment not found");

            var post = await _db.Posts.FirstOrDefaultAsync(p => p.PostId == root.PostId)
                       ?? throw new InvalidOperationException("Post not found");

            if (root.UserId != userId && post.UserId != userId)
                throw new UnauthorizedAccessException();

            var toDelete = new List<int>();
            var stack = new Stack<int>();
            stack.Push(root.CommentId);

            while (stack.Count > 0)
            {
                var cid = stack.Pop();
                toDelete.Add(cid);
                var children = await _db.Comments.Where(c => c.ParentCommentId == cid).Select(c => c.CommentId).ToListAsync();
                foreach (var ch in children) stack.Push(ch);
            }

            await using var tx = await _db.Database.BeginTransactionAsync();

            var attachments = await _db.Attachments.Where(a => a.CommentId != null && toDelete.Contains(a.CommentId.Value)).ToListAsync();
            foreach (var a in attachments)
            {
                try
                {
                    var webRoot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
                    var path = Path.Combine(webRoot, a.FilePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                    if (File.Exists(path)) File.Delete(path);
                }
                catch { }
                _db.Attachments.Remove(a);
            }

            var votes = await _db.Votes.Where(v => v.CommentId != null && toDelete.Contains(v.CommentId.Value)).ToListAsync();
            if (votes.Any()) _db.Votes.RemoveRange(votes);

            var comments = await _db.Comments.Where(c => toDelete.Contains(c.CommentId)).ToListAsync();
            if (comments.Any()) _db.Comments.RemoveRange(comments);

            await _db.SaveChangesAsync();
            await tx.CommitAsync();
        }

        // ✅ Get comments with votes
        public async Task<List<CommentResponseDto>> GetForPostAsync(int postId, int currentUserId = 0)
        {
            var all = await _db.Comments
                .Where(c => c.PostId == postId)
                .Include(c => c.User)
                .Include(c => c.Attachments)
                .Include(c => c.Votes)
                .OrderBy(c => c.CreatedAt)
                .ToListAsync();

            var lookup = all.ToDictionary(c => c.CommentId, c => new CommentResponseDto
            {
                CommentId = c.CommentId,
                CommentText = c.CommentText,
                AuthorName = c.User?.FullName ?? c.User?.Email ?? "(unknown)",
                CreatedAt = c.CreatedAt,
                Attachments = c.Attachments?.Select(a => new AttachmentDto { FileName = a.FileName, FilePath = a.FilePath }).ToList(),
                Replies = new List<CommentResponseDto>(),

                LikeCount = c.Votes.Count(v => v.VoteType == "Upvote"),
                DislikeCount = c.Votes.Count(v => v.VoteType == "Downvote"),
                UserVote = currentUserId == 0
                    ? 0
                    : c.Votes.Where(v => v.UserId == currentUserId)
                             .Select(v => v.VoteType == "Upvote" ? 1 : -1)
                             .FirstOrDefault()
            });

            var roots = new List<CommentResponseDto>();
            foreach (var c in all)
            {
                if (c.ParentCommentId.HasValue && lookup.TryGetValue(c.ParentCommentId.Value, out var parentDto))
                {
                    parentDto.Replies.Add(lookup[c.CommentId]);
                }
                else
                {
                    roots.Add(lookup[c.CommentId]);
                }
            }

            return roots;
        }
    }
}
