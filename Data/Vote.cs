using System;
using System.Collections.Generic;

namespace FNF_PROJ.Data;

public partial class Vote
{
    public int VoteId { get; set; }

    public int? PostId { get; set; }

    public int? CommentId { get; set; }

    public int UserId { get; set; }

    public string VoteType { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public virtual Comment? Comment { get; set; }

    public virtual Post? Post { get; set; }

    public virtual User User { get; set; } = null!;
}
