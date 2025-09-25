//namespace FNF_PROJ.DTOs
//{
//    public class CommentResponseDto
//    {
//        public int CommentId { get; set; }
//        public string CommentText { get; set; } = null!;
//        public string AuthorName { get; set; } = null!;
//        public List<AttachmentDto>? Attachments { get; set; }
//        public List<CommentResponseDto>? Replies { get; set; } // Nested comments
//        public DateTime CreatedAt { get; set; }
//    }
//}
using FNF_PROJ.DTOs;

public class CommentResponseDto
{
    public int CommentId { get; set; }
    public string CommentText { get; set; } = null!;
    public string AuthorName { get; set; } = null!;
    public List<AttachmentDto>? Attachments { get; set; }
    public List<CommentResponseDto>? Replies { get; set; }
    public DateTime CreatedAt { get; set; }

    // 👇 New fields
    public int LikeCount { get; set; }
    public int DislikeCount { get; set; }
    public int UserVote { get; set; } // 1 = upvote, -1 = downvote, 0 = none
}
