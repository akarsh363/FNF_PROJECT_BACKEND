using System;
using System.Collections.Generic;

namespace FNF_PROJ.Data;

public partial class User
{
    public int UserId { get; set; }

    public string FullName { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string PasswordHash { get; set; } = null!;

    public string Role { get; set; } = null!;

    public string? ProfilePicture { get; set; }

    public int DepartmentId { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual ICollection<Comment> Comments { get; set; } = new List<Comment>();

    public virtual Department Department { get; set; } = null!;

    public virtual Manager? Manager { get; set; }

    public virtual ICollection<Post> Posts { get; set; } = new List<Post>();

    public virtual ICollection<Repost> Reposts { get; set; } = new List<Repost>();

    public virtual ICollection<Vote> Votes { get; set; } = new List<Vote>();
}
