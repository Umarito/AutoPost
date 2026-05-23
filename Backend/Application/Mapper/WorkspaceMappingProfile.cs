using Application.DTOs.Workspace;
using AutoMapper;
using Domain.Entities;

namespace Application.Mappings;

/// <summary>
/// AutoMapper profile for workspace and membership read models.
/// </summary>
public class WorkspaceMappingProfile : Profile
{
    /// <summary>
    /// Configures mappings used by workspace-oriented queries.
    /// </summary>
    public WorkspaceMappingProfile()
    {
        CreateMap<Workspace, WorkspaceDto>()
            .ForMember(destination => destination.Plan, options => options.MapFrom(source => source.Plan.ToString()))
            .ForMember(destination => destination.MaxMembersPerWorkspace, options => options.MapFrom(source => source.MaxTeamMembers))
            .ForMember(destination => destination.MemberCount, options => options.MapFrom(source => source.Members.Count))
            .ForMember(destination => destination.MaxPostsPerMonth, options => options.Ignore());

        CreateMap<WorkspaceMember, MemberDto>()
            .ForMember(destination => destination.DisplayName, options => options.MapFrom(source =>
                source.User != null
                    ? source.User.DisplayName
                    : source.InvitedEmail))
            .ForMember(destination => destination.Email, options => options.MapFrom(source =>
                source.User != null && !string.IsNullOrWhiteSpace(source.User.Email)
                    ? source.User.Email
                    : source.InvitedEmail))
            .ForMember(destination => destination.AvatarUrl, options => options.MapFrom(source =>
                source.User != null
                    ? source.User.AvatarUrl
                    : null))
            .ForMember(destination => destination.Role, options => options.MapFrom(source => source.Role.ToString()))
            .ForMember(destination => destination.Status, options => options.MapFrom(source => source.Status.ToString()));
    }
}
