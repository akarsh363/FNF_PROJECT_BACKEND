public class PostResponseDto
{
    public int PostId { get; set; }
    public string Title { get; set; } = "";
    public string Body { get; set; } = "";
    public string AuthorName { get; set; } = "";
    public string DepartmentName { get; set; } = "";
    public int DeptId { get; set; }   // ✅ Added for manager delete check
    public int UpvoteCount { get; set; }
    public int DownvoteCount { get; set; }
    public List<string>? Tags { get; set; }
    public List<string>? Attachments { get; set; }
    public bool IsRepost { get; set; }
    public DateTime CreatedAt { get; set; }
}
