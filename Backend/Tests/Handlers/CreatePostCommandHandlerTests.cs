using Application.Abstractions.BackgroundJobs;
using Application.Abstractions.Caching;
using Application.Abstractions.Persistence;
using Application.Abstractions.Repositories;
using Application.Common;
using Application.CQRS.Posts;
using Application.DTOs.Post;
using AutoMapper;
using Domain.Entities;
using Domain.Enums;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Tests.Handlers;

public class CreatePostCommandHandlerTests
{
    private readonly Mock<ICurrentUserContext> _currentUserContextMock;
    private readonly Mock<IVideoRepository> _videoRepositoryMock;
    private readonly Mock<ISocialAccountRepository> _socialAccountRepositoryMock;
    private readonly Mock<IPostRepository> _postRepositoryMock;
    private readonly Mock<IPostTargetRepository> _postTargetRepositoryMock;
    private readonly Mock<IWorkspaceMemberRepository> _workspaceMemberRepositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IBackgroundJobScheduler> _backgroundJobSchedulerMock;
    private readonly Mock<ICacheService> _cacheServiceMock;
    private readonly Mock<IMapper> _mapperMock;
    private readonly Mock<ILogger<CreatePostCommandHandler>> _loggerMock;
    private readonly Mock<IUnitOfWorkTransaction> _transactionMock;

    private readonly CreatePostCommandHandler _handler;

    public CreatePostCommandHandlerTests()
    {
        _currentUserContextMock = new Mock<ICurrentUserContext>();
        _videoRepositoryMock = new Mock<IVideoRepository>();
        _socialAccountRepositoryMock = new Mock<ISocialAccountRepository>();
        _postRepositoryMock = new Mock<IPostRepository>();
        _postTargetRepositoryMock = new Mock<IPostTargetRepository>();
        _workspaceMemberRepositoryMock = new Mock<IWorkspaceMemberRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _backgroundJobSchedulerMock = new Mock<IBackgroundJobScheduler>();
        _cacheServiceMock = new Mock<ICacheService>();
        _mapperMock = new Mock<IMapper>();
        _loggerMock = new Mock<ILogger<CreatePostCommandHandler>>();
        _transactionMock = new Mock<IUnitOfWorkTransaction>();

        _unitOfWorkMock
            .Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(_transactionMock.Object);

        _handler = new CreatePostCommandHandler(
            _currentUserContextMock.Object,
            _videoRepositoryMock.Object,
            _socialAccountRepositoryMock.Object,
            _postRepositoryMock.Object,
            _postTargetRepositoryMock.Object,
            _workspaceMemberRepositoryMock.Object,
            _unitOfWorkMock.Object,
            _backgroundJobSchedulerMock.Object,
            _cacheServiceMock.Object,
            _mapperMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_ShouldCreatePost_WhenRequestIsValid()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var videoId = Guid.NewGuid();
        var socialAccountId = Guid.NewGuid();

        _currentUserContextMock.Setup(c => c.UserId).Returns(userId);
        _currentUserContextMock.Setup(c => c.WorkspaceId).Returns(workspaceId);

        var request = new CreatePostRequest(
            videoId,
            "Post Title",
            "Post Description",
            new List<string> { "tag1" },
            Visibility.Public,
            null,
            DateTime.UtcNow.AddHours(2),
            "UTC",
            new List<Guid> { socialAccountId },
            null);
        var command = new CreatePostCommand(request);

        // Mock write access
        var member = WorkspaceMember.CreateOwner(workspaceId, userId, "owner@test.com", DateTime.UtcNow);
        _workspaceMemberRepositoryMock
            .Setup(r => r.GetByUserAndWorkspaceAsync(userId, workspaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(member);

        // Mock Video
        var video = new Video();
        typeof(Video).GetProperty("Id")!.SetValue(video, videoId);
        typeof(Video).GetProperty("WorkspaceId")!.SetValue(video, workspaceId);
        typeof(Video).GetProperty("Status")!.SetValue(video, VideoStatus.Ready);
        _videoRepositoryMock
            .Setup(r => r.GetByIdAsync(videoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(video);

        // Mock Social Account
        var account = new SocialAccount();
        typeof(SocialAccount).GetProperty("Id")!.SetValue(account, socialAccountId);
        typeof(SocialAccount).GetProperty("WorkspaceId")!.SetValue(account, workspaceId);
        typeof(SocialAccount).GetProperty("Status")!.SetValue(account, SocialAccountStatus.Active);
        typeof(SocialAccount).GetProperty("Platform")!.SetValue(account, Platform.Instagram);
        _socialAccountRepositoryMock
            .Setup(r => r.GetByWorkspaceIdAsync(workspaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SocialAccount> { account });

        _backgroundJobSchedulerMock
            .Setup(s => s.Schedule(
                It.IsAny<System.Linq.Expressions.Expression<Func<Application.BackgroundJobs.ContentBackgroundJobDispatcher, Task>>>(),
                It.IsAny<TimeSpan>(),
                "default"))
            .Returns("job-123");

        var post = new Post();
        typeof(Post).GetProperty("Id")!.SetValue(post, Guid.NewGuid());
        _postRepositoryMock
            .Setup(r => r.GetByIdWithTargetsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(post);

        var expectedDto = new PostDetailDto(post.Id, "Post Title", "Post Description", new List<string>(), "Public", "Scheduled", DateTime.UtcNow.AddHours(2), "UTC", null, null, new List<PostTargetDto>(), DateTime.UtcNow);
        _mapperMock
            .Setup(m => m.Map<PostDetailDto>(It.IsAny<Post>()))
            .Returns(expectedDto);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Title.Should().Be("Post Title");

        _postRepositoryMock.Verify(r => r.AddAsync(It.IsAny<Post>(), It.IsAny<CancellationToken>()), Times.Once);
        _postTargetRepositoryMock.Verify(r => r.AddAsync(It.IsAny<PostTarget>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
        _transactionMock.Verify(t => t.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldFailWithForbidden_WhenUserHasNoWriteAccess()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var command = new CreatePostCommand(new CreatePostRequest(Guid.NewGuid(), "Title", "Desc", new(), Visibility.Public, null, DateTime.UtcNow, "UTC", new(), null));

        _currentUserContextMock.Setup(c => c.UserId).Returns(userId);
        _currentUserContextMock.Setup(c => c.WorkspaceId).Returns(workspaceId);

        // No membership found -> Forbidden
        _workspaceMemberRepositoryMock
            .Setup(r => r.GetByUserAndWorkspaceAsync(userId, workspaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((WorkspaceMember)null!);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ErrorCode.Forbidden);
    }
}
