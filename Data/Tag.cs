using System;
using System.Collections.Generic;

namespace FNF_PROJ.Data;

public partial class Tag
{
    public int TagId { get; set; }

    public string TagName { get; set; } = null!;

    public int DeptId { get; set; }

    public virtual Department Dept { get; set; } = null!;

    public virtual ICollection<PostTag> PostTags { get; set; } = new List<PostTag>();
}
