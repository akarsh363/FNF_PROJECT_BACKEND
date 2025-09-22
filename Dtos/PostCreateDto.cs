namespace FNF_PROJ.DTOs
{
    public class PostCreateDto
    {
        public string Title { get; set; } = null!;
        public string Body { get; set; } = null!;
        public int DeptId { get; set; }
        public List<int>? TagIds { get; set; } // Tags assigned
        public List<IFormFile>? Attachments { get; set; }
    }
}
