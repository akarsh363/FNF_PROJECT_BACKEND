using System;
using System.Collections.Generic;

namespace FNF_PROJ.Data;

public partial class Commit
{
    public int CommitId { get; set; }

    public int PostId { get; set; }

    public int ManagerId { get; set; }

    public string Message { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public virtual Manager Manager { get; set; } = null!;

    public virtual Post Post { get; set; } = null!;
}
