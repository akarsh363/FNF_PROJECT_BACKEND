namespace FNF_PROJ.DTOs
{
    public class UserUpdateDto
    {
        public string FullName { get; set; } = null!;
        public IFormFile? ProfilePicture { get; set; }
        public int DepartmentId { get; set; }
    }
}
