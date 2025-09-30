namespace FNF_PROJ.DTOs
{
  
    public class VoteRequestDto
    {
        public int? PostId { get; set; }
        public int? CommentId { get; set; }
        // "Upvote" or "Downvote" (case-insensitive)
        public string VoteType { get; set; } = null!;
    }

}
