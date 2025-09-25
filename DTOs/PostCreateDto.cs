public class PostCreateDto
{
    public string Title { get; set; } = "";
    // This will hold the raw JSON string provided by client (BodyJson)
    public string Body { get; set; } = "";
    public int DeptId { get; set; }
    public List<int>? TagIds { get; set; }
    public List<IFormFile>? Attachments { get; set; }
}
