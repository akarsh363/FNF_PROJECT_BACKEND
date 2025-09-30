using System;
using System.Collections.Generic;

namespace FNF_PROJ.Data;

public partial class Repost
{
    public int RepostId { get; set; }

    public int PostId { get; set; }

    public int UserId { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Post Post { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
