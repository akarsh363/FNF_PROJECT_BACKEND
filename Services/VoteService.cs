// File: Services/VoteService.cs
using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using FNF_PROJ.Data;
using FNF_PROJ.DTOs;

namespace FNF_PROJ.Services
{
    // Concrete service (no interface)
    public class VoteService
    {
        private readonly AppDbContext _db;

        public VoteService(AppDbContext db)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
        }

        // Core vote logic: toggles, switches, updates Post counters, returns counts + user vote
        public async Task<VoteResponseDto> VoteAsync(int userId, VoteRequestDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.VoteType))
                throw new ArgumentException("VoteType is required", nameof(dto.VoteType));

            var norm = dto.VoteType.Trim().ToLowerInvariant();
            if (norm != "upvote" && norm != "downvote")
                throw new ArgumentException("VoteType must be 'Upvote' or 'Downvote'", nameof(dto.VoteType));

            var isUp = norm == "upvote";

            // Begin a transaction to ensure vote + post counter update are atomic
            await using var tx = await _db.Database.BeginTransactionAsync();

            if (dto.PostId.HasValue)
            {
                var postId = dto.PostId.Value;
                var post = await _db.Posts.FirstOrDefaultAsync(p => p.PostId == postId)
                           ?? throw new InvalidOperationException("Post not found");

                var existing = await _db.Votes.FirstOrDefaultAsync(v => v.PostId == postId && v.UserId == userId);

                int userVoteAfter;
                if (existing == null)
                {
                    _db.Votes.Add(new Vote
                    {
                        PostId = postId,
                        CommentId = null,
                        UserId = userId,
                        VoteType = isUp ? "Upvote" : "Downvote",
                        CreatedAt = DateTime.UtcNow
                    });
                    userVoteAfter = isUp ? 1 : -1;
                }
                else
                {
                    if (string.Equals(existing.VoteType, isUp ? "Upvote" : "Downvote", StringComparison.OrdinalIgnoreCase))
                    {
                        // toggle off
                        _db.Votes.Remove(existing);
                        userVoteAfter = 0;
                    }
                    else
                    {
                        // switch vote
                        existing.VoteType = isUp ? "Upvote" : "Downvote";
                        existing.CreatedAt = DateTime.UtcNow;
                        userVoteAfter = isUp ? 1 : -1;
                    }
                }

                // persist vote change
                await _db.SaveChangesAsync();

                // recompute authoritative counts from Votes table
                var likeCount = await _db.Votes.CountAsync(v => v.PostId == postId && v.VoteType == "Upvote");
                var dislikeCount = await _db.Votes.CountAsync(v => v.PostId == postId && v.VoteType == "Downvote");

                // update Post counters so GET /api/Posts reads correct values
                post.UpvoteCount = likeCount;
                post.DownvoteCount = dislikeCount;
                _db.Posts.Update(post);
                await _db.SaveChangesAsync();

                await tx.CommitAsync();

                return new VoteResponseDto
                {
                    PostId = postId,
                    CommentId = null,
                    LikeCount = likeCount,
                    DislikeCount = dislikeCount,
                    UserVote = userVoteAfter
                };
            }
            else if (dto.CommentId.HasValue)
            {
                var commentId = dto.CommentId.Value;
                var comment = await _db.Comments.FirstOrDefaultAsync(c => c.CommentId == commentId)
                              ?? throw new InvalidOperationException("Comment not found");

                var existing = await _db.Votes.FirstOrDefaultAsync(v => v.CommentId == commentId && v.UserId == userId);

                int userVoteAfter;
                if (existing == null)
                {
                    _db.Votes.Add(new Vote
                    {
                        PostId = null,
                        CommentId = commentId,
                        UserId = userId,
                        VoteType = isUp ? "Upvote" : "Downvote",
                        CreatedAt = DateTime.UtcNow
                    });
                    userVoteAfter = isUp ? 1 : -1;
                }
                else
                {
                    if (string.Equals(existing.VoteType, isUp ? "Upvote" : "Downvote", StringComparison.OrdinalIgnoreCase))
                    {
                        _db.Votes.Remove(existing);
                        userVoteAfter = 0;
                    }
                    else
                    {
                        existing.VoteType = isUp ? "Upvote" : "Downvote";
                        existing.CreatedAt = DateTime.UtcNow;
                        userVoteAfter = isUp ? 1 : -1;
                    }
                }

                // persist vote change
                await _db.SaveChangesAsync();

                // recompute counts for comment votes
                var likeCount = await _db.Votes.CountAsync(v => v.CommentId == commentId && v.VoteType == "Upvote");
                var dislikeCount = await _db.Votes.CountAsync(v => v.CommentId == commentId && v.VoteType == "Downvote");

                await tx.CommitAsync();

                return new VoteResponseDto
                {
                    PostId = null,
                    CommentId = commentId,
                    LikeCount = likeCount,
                    DislikeCount = dislikeCount,
                    UserVote = userVoteAfter
                };
            }

            throw new InvalidOperationException("Either PostId or CommentId must be provided.");
        }
    }
}
