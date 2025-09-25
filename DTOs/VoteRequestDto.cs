namespace FNF_PROJ.DTOs
{
    //public class VoteDto
    //{
    //    public int? PostId { get; set; }
    //    public int? CommentId { get; set; }
    //    public string VoteType { get; set; } = null!; // "Upvote" or "Downvote"
    //}
    // VoteDto already exists (you provided it). Add this response DTO:
    public class VoteRequestDto
    {
        public int? PostId { get; set; }
        public int? CommentId { get; set; }
        // "Upvote" or "Downvote" (case-insensitive)
        public string VoteType { get; set; } = null!;
    }

}
