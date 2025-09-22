using AutoMapper;
using FNF_PROJ.Data;   // adjust if your entities are in a different namespace
using FNF_PROJ.DTOs;   // adjust if your DTOs are in a different namespace
using System.Linq;

namespace FNF_PROJ.Mapper
{
    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            // =========================
            // User Mappings
            // =========================
            CreateMap<User, UserResponseDto>()
                .ForMember(dest => dest.ProfilePicture, opt => opt.MapFrom(src => src.ProfilePicture))
                .ForMember(dest => dest.DepartmentName, opt => opt.MapFrom(src => src.Department != null ? src.Department.DeptName : null));

            CreateMap<UserRegisterDto, User>()
                .ForMember(dest => dest.PasswordHash, opt => opt.Ignore()) // hash separately
                .ForMember(dest => dest.ProfilePicture, opt => opt.Ignore()); // handle file separately

            CreateMap<UserUpdateDto, User>()
                .ForMember(dest => dest.PasswordHash, opt => opt.Ignore()) // hash separately if updated
                .ForMember(dest => dest.ProfilePicture, opt => opt.Ignore()); // handle file separately

            // =========================
            // Department Mappings
            // =========================
            CreateMap<Department, DepartmentDto>().ReverseMap();

            // =========================
            // Post Mappings
            // =========================
            CreateMap<Post, PostResponseDto>()
                .ForMember(dest => dest.AuthorName, opt => opt.MapFrom(src => src.User != null ? src.User.FullName : null))
                .ForMember(dest => dest.DepartmentName, opt => opt.MapFrom(src => src.Dept != null ? src.Dept.DeptName : null))
                .ForMember(dest => dest.Tags, opt => opt.MapFrom(src => src.PostTags.Select(pt => pt.Tag.TagName)))
                .ForMember(dest => dest.Attachments, opt => opt.MapFrom(src => src.Attachments.Select(a => a.FilePath)));

            CreateMap<PostCreateDto, Post>();

            // =========================
            // Tag Mappings
            // =========================
            CreateMap<Tag, TagDto>().ReverseMap();

            // =========================
            // Comment Mappings
            // =========================
            CreateMap<Comment, CommentResponseDto>()
                .ForMember(dest => dest.AuthorName, opt => opt.MapFrom(src => src.User != null ? src.User.FullName : null))
                .ForMember(dest => dest.Attachments, opt => opt.MapFrom(src => src.Attachments))
                .ForMember(dest => dest.Replies, opt => opt.MapFrom(src => src.InverseParentComment));

            CreateMap<CommentCreateDto, Comment>();

            // =========================
            // Attachment Mappings
            // =========================
            CreateMap<Attachment, AttachmentDto>();

            // =========================
            // Vote Mappings
            // =========================
            CreateMap<VoteDto, Vote>()
                .ForMember(dest => dest.CreatedAt, opt => opt.Ignore()); // set in service

            // =========================
            // Commit Mappings
            // =========================
            CreateMap<CommitDto, Commit>()
                .ForMember(dest => dest.CreatedAt, opt => opt.Ignore()); // set in service

            // =========================
            // Repost Mappings
            // =========================
            CreateMap<RepostDto, Repost>()
                .ForMember(dest => dest.CreatedAt, opt => opt.Ignore());

            CreateMap<Repost, RepostDto>();
        }
    }
}
