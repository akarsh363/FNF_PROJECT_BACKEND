namespace FNF_PROJ.DTOs
{
    public class CommentResponseDto
    {
        public int CommentId { get; set; }
        public string CommentText { get; set; } = null!;
        public string AuthorName { get; set; } = null!;
        public List<AttachmentDto>? Attachments { get; set; }
        public List<CommentResponseDto>? Replies { get; set; } // Nested comments
        public DateTime CreatedAt { get; set; }
    }
}
