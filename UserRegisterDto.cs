namespace FNF_PROJ.DTOs
{
    public class UserRegisterDto
    {
        public string FullName { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string Password { get; set; } = null!;
        public string Role { get; set; } = "Employee"; // default Employee
        public int DepartmentId { get; set; }
        public IFormFile? ProfilePicture { get; set; } // For file upload
    }
}
