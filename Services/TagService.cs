using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using FNF_PROJ.Data;
using FNF_PROJ.DTOs;

namespace FNF_PROJ.Services
{
    public interface ITagService
    {
        Task<List<TagDto>> GetTagsForDeptAsync(int deptId);
        Task<List<TagDto>> GetTagsForUserAsync(int userId);
    }

    public class TagService : ITagService
    {
        private readonly AppDbContext _db;

        public TagService(AppDbContext db)
        {
            _db = db;
        }

        public async Task<List<TagDto>> GetTagsForDeptAsync(int deptId)
        {
            return await _db.Tags
                .Where(t => t.DeptId == deptId)
                .Select(t => new TagDto
                {
                    TagId = t.TagId,
                    TagName = t.TagName,
                    DeptId = t.DeptId
                })
                .ToListAsync();
        }

        public async Task<List<TagDto>> GetTagsForUserAsync(int userId)
        {
            var user = await _db.Users.FindAsync(userId);
            if (user == null) return new List<TagDto>();
            return await GetTagsForDeptAsync(user.DepartmentId);
        }
    }
}

