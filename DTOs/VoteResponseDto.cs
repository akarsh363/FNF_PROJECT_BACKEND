namespace FNF_PROJ.DTOs
{
    public class VoteResponseDto
    {
        public int? PostId { get; set; }
        public int? CommentId { get; set; }
        public int LikeCount { get; set; }
        public int DislikeCount { get; set; }
        // 1 = Upvoted by current user, -1 = Downvoted, 0 = none
        public int UserVote { get; set; }
    }
}
