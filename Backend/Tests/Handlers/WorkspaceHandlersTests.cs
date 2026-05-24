using System.Linq.Expressions;
using Application.Abstractions.Caching;
using Application.Abstractions.Notifications;
using Application.Abstractions.Persistence;
using Application.Abstractions.Repositories;
using Application.Abstractions.Security;
using Application.Common;
using Application.CQRS.Workspace;
using Application.DTOs.Workspace;
using Domain.Entities;
using Domain.Enums;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using WorkspaceEntity = Domain.Entities.Workspace;
using WorkspaceMemberEntity = Domain.Entities.WorkspaceMember;

namespace Tests.Handlers;

public class WorkspaceHandlersTests
{
    private readonly Mock<ICurrentUserContext> _currentUserContextMock;
    private readonly Mock<IApplicationUserRepository> _applicationUserRepositoryMock;
    private readonly Mock<IWorkspaceRepository> _workspaceRepositoryMock;
    private readonly Mock<IWorkspaceMemberRepository> _workspaceMemberRepositoryMock;
    private readonly Mock<INotificationPreferenceRepository> _notificationPreferenceMock;
    private readonly Mock<IRefreshTokenRepository> _refreshTokenRepositoryMock;
    private readonly Mock<ISocialAccountRepository> _socialAccountRepositoryMock;
    private readonly Mock<IPostRepository> _postRepositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IUnitOfWorkTransaction> _transactionMock;
    private readonly Mock<ICacheService> _cacheServiceMock;
    private readonly Mock<IInviteTokenService> _inviteTokenServiceMock;
    private readonly Mock<IEmailService> _emailServiceMock;
    private readonly Mock<UserManager<ApplicationUser>> _userManagerMock;

    public WorkspaceHandlersTests()
    {
        _currentUserContextMock = new Mock<ICurrentUserContext>();
        _applicationUserRepositoryMock = new Mock<IApplicationUserRepository>();
        _workspaceRepositoryMock = new Mock<IWorkspaceRepository>();
        _workspaceMemberRepositoryMock = new Mock<IWorkspaceMemberRepository>();
        _notificationPreferenceMock = new Mock<INotificationPreferenceRepository>();
        _refreshTokenRepositoryMock = new Mock<IRefreshTokenRepository>();
        _socialAccountRepositoryMock = new Mock<ISocialAccountRepository>();
        _postRepositoryMock = new Mock<IPostRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _transactionMock = new Mock<IUnitOfWorkTransaction>();
        _cacheServiceMock = new Mock<ICacheService>();
        _inviteTokenServiceMock = new Mock<IInviteTokenService>();
        _emailServiceMock = new Mock<IEmailService>();

        var store = new Mock<IUserStore<ApplicationUser>>();
        _userManagerMock = new Mock<UserManager<ApplicationUser>>(store.Object, null, null, null, null, null, null, null, null);

        _unitOfWorkMock
            .Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(_transactionMock.Object);
    }

    [Fact]
    public async Task CreateWorkspace_ShouldSucceed_WhenRequestIsValid()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _currentUserContextMock.Setup(c => c.UserId).Returns(userId);

        var user = ApplicationUser.Create("owner@test.com", "Owner User", DateTime.UtcNow);
        user.Id = userId;
        _applicationUserRepositoryMock
            .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _workspaceRepositoryMock
            .Setup(r => r.SlugExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var handler = new CreateWorkspaceCommandHandler(
            _currentUserContextMock.Object,
            _applicationUserRepositoryMock.Object,
            _workspaceRepositoryMock.Object,
            _workspaceMemberRepositoryMock.Object,
            _notificationPreferenceMock.Object,
            _unitOfWorkMock.Object,
            _cacheServiceMock.Object,
            new Mock<ILogger<CreateWorkspaceCommandHandler>>().Object);

        var command = new CreateWorkspaceCommand("New Workspace", "new-workspace");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Name.Should().Be("New Workspace");

        _workspaceRepositoryMock.Verify(r => r.AddAsync(It.IsAny<WorkspaceEntity>(), It.IsAny<CancellationToken>()), Times.Once);
        _workspaceMemberRepositoryMock.Verify(r => r.AddAsync(It.IsAny<WorkspaceMemberEntity>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _transactionMock.Verify(t => t.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetWorkspace_ShouldReturnFromCache_WhenCached()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        _currentUserContextMock.Setup(c => c.UserId).Returns(userId);

        var member = WorkspaceMemberEntity.CreateOwner(workspaceId, userId, "owner@test.com", DateTime.UtcNow);
        _workspaceMemberRepositoryMock
            .Setup(r => r.GetByUserAndWorkspaceAsync(userId, workspaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(member);

        var cachedDto = new WorkspaceDto(workspaceId, "Cached Workspace", "cached", null, "Free", 5, 10, 100, true, 1, DateTime.UtcNow);
        _cacheServiceMock
            .Setup(c => c.GetAsync<WorkspaceDto>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(cachedDto);

        var handler = new GetWorkspaceQueryHandler(
            _currentUserContextMock.Object,
            _workspaceRepositoryMock.Object,
            _workspaceMemberRepositoryMock.Object,
            _cacheServiceMock.Object,
            new Mock<ILogger<GetWorkspaceQueryHandler>>().Object);

        var query = new GetWorkspaceQuery(workspaceId);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Name.Should().Be("Cached Workspace");
        _workspaceRepositoryMock.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateWorkspace_ShouldSucceed_WhenUserIsManager()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        _currentUserContextMock.Setup(c => c.UserId).Returns(userId);

        var member = WorkspaceMemberEntity.CreateOwner(workspaceId, userId, "owner@test.com", DateTime.UtcNow);
        _workspaceMemberRepositoryMock
            .Setup(r => r.GetByUserAndWorkspaceAsync(userId, workspaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(member);

        var workspace = WorkspaceEntity.Create("Old Name", "old-slug", DateTime.UtcNow);
        typeof(WorkspaceEntity).GetProperty("Id")!.SetValue(workspace, workspaceId);

        _workspaceRepositoryMock
            .Setup(r => r.GetByIdAsync(workspaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(workspace);

        var handler = new UpdateWorkspaceCommandHandler(
            _currentUserContextMock.Object,
            _workspaceRepositoryMock.Object,
            _workspaceMemberRepositoryMock.Object,
            _unitOfWorkMock.Object,
            _cacheServiceMock.Object,
            new Mock<ILogger<UpdateWorkspaceCommandHandler>>().Object);

        var command = new UpdateWorkspaceCommand(workspaceId, new UpdateWorkspaceRequest("New Name", "logo.png"));

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Name.Should().Be("New Name");
        result.Value.LogoUrl.Should().Be("logo.png");

        _workspaceRepositoryMock.Verify(r => r.Update(workspace), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task InviteMember_ShouldSucceed_WhenRoleIsAllowedAndLimitNotReached()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        _currentUserContextMock.Setup(c => c.UserId).Returns(userId);

        var member = WorkspaceMemberEntity.CreateOwner(workspaceId, userId, "owner@test.com", DateTime.UtcNow);
        _workspaceMemberRepositoryMock
            .Setup(r => r.GetByUserAndWorkspaceAsync(userId, workspaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(member);

        var workspace = WorkspaceEntity.Create("Workspace", "slug", DateTime.UtcNow, SubscriptionPlan.Pro);
        typeof(WorkspaceEntity).GetProperty("Id")!.SetValue(workspace, workspaceId);
        _workspaceRepositoryMock
            .Setup(r => r.GetByIdAsync(workspaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(workspace);

        _workspaceMemberRepositoryMock
            .Setup(r => r.CountByWorkspaceAsync(workspaceId, MemberStatus.Active, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        _userManagerMock
            .Setup(m => m.FindByEmailAsync(It.IsAny<string>()))
            .ReturnsAsync((ApplicationUser?)null);

        _inviteTokenServiceMock
            .Setup(s => s.Generate(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>()))
            .Returns("token-123");

        var handler = new InviteMemberCommandHandler(
            _currentUserContextMock.Object,
            _workspaceRepositoryMock.Object,
            _workspaceMemberRepositoryMock.Object,
            _userManagerMock.Object,
            _inviteTokenServiceMock.Object,
            _emailServiceMock.Object,
            _unitOfWorkMock.Object,
            _cacheServiceMock.Object,
            new Mock<ILogger<InviteMemberCommandHandler>>().Object);

        var command = new InviteMemberCommand(workspaceId, new InviteMemberRequest("invited@test.com", WorkspaceRole.Editor));

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Role.Should().Be("Editor");
        result.Value.Status.Should().Be("Invited");

        _workspaceMemberRepositoryMock.Verify(r => r.AddAsync(It.IsAny<WorkspaceMemberEntity>(), It.IsAny<CancellationToken>()), Times.Once);
        _emailServiceMock.Verify(s => s.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AcceptInvite_ShouldSucceed_WhenTokenIsValid()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        _currentUserContextMock.Setup(c => c.UserId).Returns(userId);

        var user = ApplicationUser.Create("invited@test.com", "Invited User", DateTime.UtcNow);
        user.Id = userId;
        _applicationUserRepositoryMock
            .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var payload = new InviteTokenPayload(workspaceId, "invited@test.com", "Editor", DateTime.UtcNow.AddHours(2));
        _inviteTokenServiceMock
            .Setup(s => s.Validate("token-123"))
            .Returns(payload);

        var workspace = WorkspaceEntity.Create("Workspace", "slug", DateTime.UtcNow);
        typeof(WorkspaceEntity).GetProperty("Id")!.SetValue(workspace, workspaceId);
        _workspaceRepositoryMock
            .Setup(r => r.GetByIdAsync(workspaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(workspace);

        _workspaceMemberRepositoryMock
            .Setup(r => r.ExistsAsync(userId, workspaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var invitation = WorkspaceMemberEntity.CreateInvitation(workspaceId, "invited@test.com", WorkspaceRole.Editor, Guid.NewGuid(), DateTime.UtcNow, null);
        _workspaceMemberRepositoryMock
            .Setup(r => r.GetByInvitedEmailAsync(workspaceId, "invited@test.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(invitation);

        _notificationPreferenceMock
            .Setup(r => r.GetByUserAndWorkspaceAsync(userId, workspaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<NotificationPreference>());

        var handler = new AcceptInviteCommandHandler(
            _currentUserContextMock.Object,
            _applicationUserRepositoryMock.Object,
            _workspaceRepositoryMock.Object,
            _workspaceMemberRepositoryMock.Object,
            _notificationPreferenceMock.Object,
            _inviteTokenServiceMock.Object,
            _unitOfWorkMock.Object,
            _cacheServiceMock.Object,
            new Mock<ILogger<AcceptInviteCommandHandler>>().Object);

        var command = new AcceptInviteCommand("token-123");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be("Active");
        _workspaceMemberRepositoryMock.Verify(r => r.Update(invitation), Times.Once);
    }
}
