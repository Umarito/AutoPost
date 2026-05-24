using Application.Abstractions.BackgroundJobs;
using Application.Abstractions.Caching;
using Application.Abstractions.Persistence;
using Application.Abstractions.Repositories;
using Application.Common;
using Application.CQRS.Posts;
using Domain.Entities;
using Domain.Enums;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Tests.Handlers;

public class DeletePostCommandHandlerTests
{
    private readonly Mock<ICurrentUserContext> _currentUserContextMock;
    private readonly Mock<IPostRepository> _postRepositoryMock;
    private readonly Mock<IWorkspaceMemberRepository> _workspaceMemberRepositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IBackgroundJobScheduler> _backgroundJobSchedulerMock;
    private readonly Mock<ICacheService> _cacheServiceMock;
    private readonly Mock<ILogger<DeletePostCommandHandler>> _loggerMock;

    private readonly DeletePostCommandHandler _handler;

    public DeletePostCommandHandlerTests()
    {
        _currentUserContextMock = new Mock<ICurrentUserContext>();
        _postRepositoryMock = new Mock<IPostRepository>();
        _workspaceMemberRepositoryMock = new Mock<IWorkspaceMemberRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _backgroundJobSchedulerMock = new Mock<IBackgroundJobScheduler>();
        _cacheServiceMock = new Mock<ICacheService>();
        _loggerMock = new Mock<ILogger<DeletePostCommandHandler>>();

        _handler = new DeletePostCommandHandler(
            _currentUserContextMock.Object,
            _postRepositoryMock.Object,
            _workspaceMemberRepositoryMock.Object,
            _unitOfWorkMock.Object,
            _backgroundJobSchedulerMock.Object,
            _cacheServiceMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_ShouldDeletePostAndCancelJob_WhenPostIsScheduled()
    {
        // Arrange
        var postId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var command = new DeletePostCommand(postId);

        _currentUserContextMock.Setup(c => c.UserId).Returns(userId);

        var post = new Post();
        typeof(Post).GetProperty("Id")!.SetValue(post, postId);
        typeof(Post).GetProperty("WorkspaceId")!.SetValue(post, workspaceId);
        typeof(Post).GetProperty("Status")!.SetValue(post, PostStatus.Scheduled);

        var schedule = Schedule.Create(DateTime.UtcNow.AddHours(1), "UTC");
        typeof(Schedule).GetProperty("SchedulerJobId")!.SetValue(schedule, "job-to-cancel");
        typeof(Post).GetProperty("Schedule")!.SetValue(post, schedule);

        _postRepositoryMock
            .Setup(r => r.GetByIdAsync(postId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(post);

        var member = WorkspaceMember.CreateOwner(workspaceId, userId, "owner@test.com", DateTime.UtcNow);
        _workspaceMemberRepositoryMock
            .Setup(r => r.GetByUserAndWorkspaceAsync(userId, workspaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(member);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();

        _backgroundJobSchedulerMock.Verify(s => s.Delete("job-to-cancel"), Times.Once);
        _postRepositoryMock.Verify(r => r.Remove(post), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldFailWithConflict_WhenPostIsPublished()
    {
        // Arrange
        var postId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var command = new DeletePostCommand(postId);

        _currentUserContextMock.Setup(c => c.UserId).Returns(userId);

        var post = new Post();
        typeof(Post).GetProperty("Id")!.SetValue(post, postId);
        typeof(Post).GetProperty("WorkspaceId")!.SetValue(post, workspaceId);
        typeof(Post).GetProperty("Status")!.SetValue(post, PostStatus.Published); // Already published

        _postRepositoryMock
            .Setup(r => r.GetByIdAsync(postId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(post);

        var member = WorkspaceMember.CreateOwner(workspaceId, userId, "owner@test.com", DateTime.UtcNow);
        _workspaceMemberRepositoryMock
            .Setup(r => r.GetByUserAndWorkspaceAsync(userId, workspaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(member);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ErrorCode.Conflict);
        result.Error.Should().Contain("Published or in-flight posts cannot be deleted");

        _postRepositoryMock.Verify(r => r.Remove(It.IsAny<Post>()), Times.Never);
    }
}
