using Application.Abstractions.BackgroundJobs;
using Application.Abstractions.Caching;
using Application.Abstractions.Media;
using Application.Abstractions.Persistence;
using Application.Abstractions.Repositories;
using Application.Abstractions.Storage;
using Application.Common;
using Application.CQRS.Videos;
using Application.DTOs.Video;
using AutoMapper;
using Domain.Entities;
using Domain.Enums;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Tests.Handlers;

public class VideoHandlersTests
{
    private readonly Mock<ICurrentUserContext> _currentUserContextMock;
    private readonly Mock<IVideoRepository> _videoRepositoryMock;
    private readonly Mock<IPostRepository> _postRepositoryMock;
    private readonly Mock<IWorkspaceMemberRepository> _workspaceMemberRepositoryMock;
    private readonly Mock<IApplicationUserRepository> _applicationUserRepositoryMock;
    private readonly Mock<ICacheService> _cacheServiceMock;
    private readonly Mock<IBlobStorageService> _blobStorageServiceMock;
    private readonly Mock<IVideoProcessingService> _videoProcessingServiceMock;
    private readonly Mock<IBackgroundJobScheduler> _backgroundJobSchedulerMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IMapper> _mapperMock;

    public VideoHandlersTests()
    {
        _currentUserContextMock = new Mock<ICurrentUserContext>();
        _videoRepositoryMock = new Mock<IVideoRepository>();
        _postRepositoryMock = new Mock<IPostRepository>();
        _workspaceMemberRepositoryMock = new Mock<IWorkspaceMemberRepository>();
        _applicationUserRepositoryMock = new Mock<IApplicationUserRepository>();
        _cacheServiceMock = new Mock<ICacheService>();
        _blobStorageServiceMock = new Mock<IBlobStorageService>();
        _videoProcessingServiceMock = new Mock<IVideoProcessingService>();
        _backgroundJobSchedulerMock = new Mock<IBackgroundJobScheduler>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _mapperMock = new Mock<IMapper>();
    }

    [Fact]
    public async Task InitVideoUpload_ShouldSucceed_WhenUserHasContentWriteAccess()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        _currentUserContextMock.Setup(c => c.UserId).Returns(userId);
        _currentUserContextMock.Setup(c => c.WorkspaceId).Returns(workspaceId);

        var member = WorkspaceMember.CreateOwner(workspaceId, userId, "owner@test.com", DateTime.UtcNow);
        _workspaceMemberRepositoryMock
            .Setup(r => r.GetByUserAndWorkspaceAsync(userId, workspaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(member);

        var handler = new InitVideoUploadCommandHandler(
            _currentUserContextMock.Object,
            _workspaceMemberRepositoryMock.Object,
            _cacheServiceMock.Object,
            new Mock<ILogger<InitVideoUploadCommandHandler>>().Object);

        var command = new InitVideoUploadCommand(new InitUploadRequest("video.mp4", "video/mp4", 25 * 1024 * 1024));

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.TotalChunks.Should().Be(3); // 25MB / 10MB chunk size
        _cacheServiceMock.Verify(c => c.SetAsync(It.IsAny<string>(), It.IsAny<VideoUploadSessionState>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessVideo_ShouldSucceed_WhenVideoExists()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var videoId = Guid.NewGuid();

        _currentUserContextMock.Setup(c => c.UserId).Returns(userId);

        var video = new Video();
        typeof(Video).GetProperty("Id")!.SetValue(video, videoId);
        typeof(Video).GetProperty("WorkspaceId")!.SetValue(video, workspaceId);

        _videoRepositoryMock
            .Setup(r => r.GetByIdIncludingDeletedAsync(videoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(video);

        var member = WorkspaceMember.CreateOwner(workspaceId, userId, "owner@test.com", DateTime.UtcNow);
        _workspaceMemberRepositoryMock
            .Setup(r => r.GetByUserAndWorkspaceAsync(userId, workspaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(member);

        var handler = new ProcessVideoCommandHandler(
            _currentUserContextMock.Object,
            _videoRepositoryMock.Object,
            _workspaceMemberRepositoryMock.Object,
            _videoProcessingServiceMock.Object,
            _cacheServiceMock.Object,
            new Mock<ILogger<ProcessVideoCommandHandler>>().Object);

        var command = new ProcessVideoCommand(videoId);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _videoProcessingServiceMock.Verify(s => s.ProcessAsync(videoId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteVideo_ShouldSucceed_WhenNoActivePostDependencies()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var videoId = Guid.NewGuid();

        _currentUserContextMock.Setup(c => c.UserId).Returns(userId);

        var video = new Video();
        typeof(Video).GetProperty("Id")!.SetValue(video, videoId);
        typeof(Video).GetProperty("WorkspaceId")!.SetValue(video, workspaceId);

        _videoRepositoryMock
            .Setup(r => r.GetByIdAsync(videoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(video);

        var member = WorkspaceMember.CreateOwner(workspaceId, userId, "owner@test.com", DateTime.UtcNow);
        _workspaceMemberRepositoryMock
            .Setup(r => r.GetByUserAndWorkspaceAsync(userId, workspaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(member);

        _postRepositoryMock
            .Setup(r => r.GetByWorkspaceIdAsync(
                workspaceId,
                It.IsAny<PostStatus?>(),
                It.IsAny<Platform?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Post>()); // No active posts

        var handler = new DeleteVideoCommandHandler(
            _currentUserContextMock.Object,
            _videoRepositoryMock.Object,
            _postRepositoryMock.Object,
            _workspaceMemberRepositoryMock.Object,
            _unitOfWorkMock.Object,
            _cacheServiceMock.Object,
            new Mock<ILogger<DeleteVideoCommandHandler>>().Object);

        var command = new DeleteVideoCommand(videoId);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        video.Status.Should().Be(VideoStatus.Deleted);
        _videoRepositoryMock.Verify(r => r.Update(video), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
