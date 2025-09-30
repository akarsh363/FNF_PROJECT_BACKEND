//using Microsoft.AspNetCore.Authorization;
//using Microsoft.AspNetCore.Mvc;
//using Microsoft.EntityFrameworkCore;
//using System.Security.Claims;
//using FNF_PROJ.Data;

//namespace FNF_PROJ.Controllers
//{
//    [ApiController]
//    [Route("api/[controller]")]
//    public class CommitsController : ControllerBase
//    {
//        private readonly AppDbContext _db;

//        public CommitsController(AppDbContext db)
//        {
//            _db = db;
//        }

//        private int GetCurrentUserId()
//        {
//            string? userIdClaim =
//                User.FindFirst(ClaimTypes.NameIdentifier)?.Value
//                ?? User.FindFirst("sub")?.Value
//                ?? User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;

//            return int.TryParse(userIdClaim, out var uid) ? uid : 0;
//        }

//        // returns commits (including delete logs) that affect MY posts
//        [HttpGet("mine")]
//        [Authorize]
//        public async Task<IActionResult> GetMine()
//        {
//            var uid = GetCurrentUserId();
//            if (uid == 0) return Unauthorized();

//            var q =
//                from c in _db.Commits
//                join p in _db.Posts on c.PostId equals p.PostId
//                join m in _db.Users on c.ManagerId equals m.UserId
//                where p.UserId == uid
//                orderby c.CreatedAt descending
//                select new
//                {
//                    c.CommitId,
//                    c.PostId,
//                    PostTitle = p.Title,
//                    ManagerName = m.FullName,
//                    Action = c.Message.StartsWith("DELETE:") ? "DELETE" : "NOTE",
//                    Message = c.Message.StartsWith("DELETE:") ? c.Message.Substring("DELETE:".Length).TrimStart() : c.Message,
//                    c.CreatedAt
//                };

//            var list = await q.ToListAsync();
//            return Ok(list);
//        }
//    }
//}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using FNF_PROJ.Data;

namespace FNF_PROJ.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CommitsController : ControllerBase
    {
        private readonly AppDbContext _db;

        public CommitsController(AppDbContext db)
        {
            _db = db;
        }

        private int GetCurrentUserId()
        {
            string? userIdClaim =
                User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? User.FindFirst("sub")?.Value
                ?? User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;

            return int.TryParse(userIdClaim, out var id) ? id : 0;
        }

        // returns commits (including delete logs) that affect MY posts
        [HttpGet("mine")]
        [Authorize]
        public async Task<IActionResult> GetMine()
        {
            var uid = GetCurrentUserId();
            if (uid == 0) return Unauthorized();

            // Correct join path:
            // Commits.ManagerId is Manager.ManagerId (not Users.UserId).
            // So join Commits -> Managers -> Users to resolve manager's FullName.
            var q =
                from c in _db.Commits
                join p in _db.Posts on c.PostId equals p.PostId
                join mgr in _db.Managers on c.ManagerId equals mgr.ManagerId
                join mu in _db.Users on mgr.UserId equals mu.UserId
                where p.UserId == uid
                orderby c.CreatedAt descending
                select new
                {
                    c.CommitId,
                    c.PostId,
                    PostTitle = p.Title,
                    ManagerName = mu.FullName,
                    Action = c.Message.StartsWith("DELETE:") ? "DELETE" : "NOTE",
                    Message = c.Message.StartsWith("DELETE:") ? c.Message.Substring("DELETE:".Length).TrimStart() : c.Message,
                    c.CreatedAt
                };

            var list = await q.ToListAsync();
            return Ok(list);
        }
    }
}
