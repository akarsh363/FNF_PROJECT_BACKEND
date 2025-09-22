namespace FNF_PROJ.DTOs
{
    public class CommitDto
    {
        public int PostId { get; set; }
        public int ManagerId { get; set; }
        public string Message { get; set; } = null!; // Reason for deletion
    }
}
