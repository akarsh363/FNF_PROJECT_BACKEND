using System;
using System.Collections.Generic;

namespace FNF_PROJ.Data;

public partial class Post
{
    public int PostId { get; set; }

    public int UserId { get; set; }

    public int DeptId { get; set; }

    public string Title { get; set; } = null!;

    public string Body { get; set; } = null!;

    public int UpvoteCount { get; set; }

    public int DownvoteCount { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public bool IsRepost { get; set; }

    public virtual ICollection<Attachment> Attachments { get; set; } = new List<Attachment>();

    public virtual ICollection<Comment> Comments { get; set; } = new List<Comment>();

    public virtual ICollection<Commit> Commits { get; set; } = new List<Commit>();

    public virtual Department Dept { get; set; } = null!;

    public virtual ICollection<PostTag> PostTags { get; set; } = new List<PostTag>();

    public virtual ICollection<Repost> Reposts { get; set; } = new List<Repost>();

    public virtual User User { get; set; } = null!;

    public virtual ICollection<Vote> Votes { get; set; } = new List<Vote>();
}
