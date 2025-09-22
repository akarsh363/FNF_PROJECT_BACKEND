namespace FNF_PROJ.DTOs
{
    public class VoteDto
    {
        public int? PostId { get; set; }
        public int? CommentId { get; set; }
        public string VoteType { get; set; } = null!; // "Upvote" or "Downvote"
    }
}
