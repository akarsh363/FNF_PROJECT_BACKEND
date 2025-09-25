namespace FNF_PROJ.DTOs
{
    public class AttachmentDto
    {

        public int AttachmentId { get; set; }
        public string FileName { get; set; } = null!;
        public string FilePath { get; set; } = null!;
        public string FileType { get; set; } = null!;
        public DateTime UploadedAt { get; set; }
    }
}
