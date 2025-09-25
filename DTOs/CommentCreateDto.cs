namespace FNF_PROJ.DTOs
{
    public class CommentCreateDto
    {
        public int PostId { get; set; }
        public int? ParentCommentId { get; set; }
        public string CommentText { get; set; } = null!;
        public List<IFormFile>? Attachments { get; set; }
    }
}
