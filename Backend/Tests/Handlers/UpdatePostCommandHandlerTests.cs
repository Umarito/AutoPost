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

public class UpdatePostCommandHandlerTests
{
    private readonly Mock<ICurrentUserContext> _currentUserContextMock;
    private readonly Mock<IPostRepository> _postRepositoryMock;
    private readonly Mock<IWorkspaceMemberRepository> _workspaceMemberRepositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IBackgroundJobScheduler> _backgroundJobSchedulerMock;
    private readonly Mock<ICacheService> _cacheServiceMock;
    private readonly Mock<IMapper> _mapperMock;
    private readonly Mock<ILogger<UpdatePostCommandHandler>> _loggerMock;

    private readonly UpdatePostCommandHandler _handler;

    public UpdatePostCommandHandlerTests()
    {
        _currentUserContextMock = new Mock<ICurrentUserContext>();
        _postRepositoryMock = new Mock<IPostRepository>();
        _workspaceMemberRepositoryMock = new Mock<IWorkspaceMemberRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _backgroundJobSchedulerMock = new Mock<IBackgroundJobScheduler>();
        _cacheServiceMock = new Mock<ICacheService>();
        _mapperMock = new Mock<IMapper>();
        _loggerMock = new Mock<ILogger<UpdatePostCommandHandler>>();

        _handler = new UpdatePostCommandHandler(
            _currentUserContextMock.Object,
            _postRepositoryMock.Object,
            _workspaceMemberRepositoryMock.Object,
            _unitOfWorkMock.Object,
            _backgroundJobSchedulerMock.Object,
            _cacheServiceMock.Object,
            _mapperMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_ShouldUpdatePostAndRescheduleJob_WhenPostIsScheduled()
    {
        // Arrange
        var postId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var command = new UpdatePostCommand(postId, new UpdatePostRequest("New Title", "New Desc", new(), Visibility.Public, DateTime.UtcNow.AddHours(3), "UTC"));

        _currentUserContextMock.Setup(c => c.UserId).Returns(userId);

        var post = new Post();
        typeof(Post).GetProperty("Id")!.SetValue(post, postId);
        typeof(Post).GetProperty("WorkspaceId")!.SetValue(post, workspaceId);
        typeof(Post).GetProperty("Status")!.SetValue(post, PostStatus.Scheduled);

        var content = PostContent.Create("Old Title", "Old Desc", new List<string>(), Visibility.Public, null, null);
        typeof(Post).GetProperty("Content")!.SetValue(post, content);

        var schedule = Schedule.Create(DateTime.UtcNow.AddHours(1), "UTC");
        typeof(Schedule).GetProperty("SchedulerJobId")!.SetValue(schedule, "old-job-id");
        typeof(Post).GetProperty("Schedule")!.SetValue(post, schedule);

        _postRepositoryMock
            .Setup(r => r.GetByIdWithTargetsAsync(postId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(post);

        var member = WorkspaceMember.CreateOwner(workspaceId, userId, "owner@test.com", DateTime.UtcNow);
        _workspaceMemberRepositoryMock
            .Setup(r => r.GetByUserAndWorkspaceAsync(userId, workspaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(member);

        _backgroundJobSchedulerMock
            .Setup(s => s.Schedule(
                It.IsAny<System.Linq.Expressions.Expression<Func<Application.BackgroundJobs.ContentBackgroundJobDispatcher, Task>>>(),
                It.IsAny<TimeSpan>(),
                "default"))
            .Returns("new-job-id");

        var expectedDto = new PostDetailDto(postId, "New Title", "New Desc", new(), "Public", "Scheduled", DateTime.UtcNow.AddHours(3), "UTC", null, null, new List<PostTargetDto>(), DateTime.UtcNow);
        _mapperMock
            .Setup(m => m.Map<PostDetailDto>(post))
            .Returns(expectedDto);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Title.Should().Be("New Title");

        _backgroundJobSchedulerMock.Verify(s => s.Delete("old-job-id"), Times.Once);
        _backgroundJobSchedulerMock.Verify(s => s.Schedule(It.IsAny<System.Linq.Expressions.Expression<Func<Application.BackgroundJobs.ContentBackgroundJobDispatcher, Task>>>(), It.IsAny<TimeSpan>(), "default"), Times.Once);
        _postRepositoryMock.Verify(r => r.Update(post), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldFailWithConflict_WhenPostIsAlreadyPublishedOrPublishing()
    {
        // Arrange
        var postId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var command = new UpdatePostCommand(postId, new UpdatePostRequest("New Title", "New Desc", new(), Visibility.Public, null, null));

        _currentUserContextMock.Setup(c => c.UserId).Returns(userId);

        var post = new Post();
        typeof(Post).GetProperty("Id")!.SetValue(post, postId);
        typeof(Post).GetProperty("WorkspaceId")!.SetValue(post, workspaceId);
        typeof(Post).GetProperty("Status")!.SetValue(post, PostStatus.Published); // Already published

        _postRepositoryMock
            .Setup(r => r.GetByIdWithTargetsAsync(postId, It.IsAny<CancellationToken>()))
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
        result.Error.Should().Contain("Only draft or scheduled posts can be updated");
    }
}
