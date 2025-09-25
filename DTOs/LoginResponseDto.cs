namespace FNF_PROJ.DTOs
{
    public class LoginResponseDto
    {
        public string Token { get; set; } = null!;
        public string Role { get; set; } = null!;
        public string FullName { get; set; } = null!;
        public int UserId { get; set; }
    }
}
