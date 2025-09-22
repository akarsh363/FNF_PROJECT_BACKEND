namespace FNF_PROJ.DTOs
{
    public class PostResponseDto
    {
        public int PostId { get; set; }
        public string Title { get; set; } = null!;
        public string Body { get; set; } = null!;
        public string AuthorName { get; set; } = null!;
        public string DepartmentName { get; set; } = null!;
        public int UpvoteCount { get; set; }
        public int DownvoteCount { get; set; }
        public List<string>? Tags { get; set; }
        public List<string>? Attachments { get; set; } // File paths
        public bool IsRepost { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
