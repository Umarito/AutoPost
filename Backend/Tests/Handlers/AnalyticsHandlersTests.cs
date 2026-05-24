using Application.Abstractions.Caching;
using Application.Abstractions.Integrations;
using Application.Abstractions.Persistence;
using Application.Abstractions.Repositories;
using Application.Common;
using Application.CQRS.Analytics;
using Application.DTOs.Analytics;
using AutoMapper;
using Domain.Entities;
using Domain.Enums;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Tests.Handlers;

public class AnalyticsHandlersTests
{
    private readonly Mock<ICurrentUserContext> _currentUserContextMock;
    private readonly Mock<IPostTargetRepository> _postTargetRepositoryMock;
    private readonly Mock<IPostRepository> _postRepositoryMock;
    private readonly Mock<IWorkspaceMemberRepository> _workspaceMemberRepositoryMock;
    private readonly Mock<IPlatformIntegrationService> _platformIntegrationServiceMock;
    private readonly Mock<IPostAnalyticsSnapshotRepository> _postAnalyticsSnapshotRepositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<ICacheService> _cacheServiceMock;
    private readonly Mock<IMapper> _mapperMock;

    public AnalyticsHandlersTests()
    {
        _currentUserContextMock = new Mock<ICurrentUserContext>();
        _postTargetRepositoryMock = new Mock<IPostTargetRepository>();
        _postRepositoryMock = new Mock<IPostRepository>();
        _workspaceMemberRepositoryMock = new Mock<IWorkspaceMemberRepository>();
        _platformIntegrationServiceMock = new Mock<IPlatformIntegrationService>();
        _postAnalyticsSnapshotRepositoryMock = new Mock<IPostAnalyticsSnapshotRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _cacheServiceMock = new Mock<ICacheService>();
        _mapperMock = new Mock<IMapper>();
    }

    [Fact]
    public async Task CollectPostSnapshot_ShouldSucceed_WhenTargetIsPublished()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var postTargetId = Guid.NewGuid();
        var postId = Guid.NewGuid();
        var socialAccountId = Guid.NewGuid();

        _currentUserContextMock.Setup(c => c.UserId).Returns(userId);

        var socialAccount = new SocialAccount();
        typeof(SocialAccount).GetProperty("Id")!.SetValue(socialAccount, socialAccountId);

        var target = PostTarget.Create(postId, socialAccountId, Platform.Instagram);
        typeof(PostTarget).GetProperty("Id")!.SetValue(target, postTargetId);
        typeof(PostTarget).GetProperty("SocialAccount")!.SetValue(target, socialAccount);
        target.MarkPublished("ext-post-123", "http://external", DateTime.UtcNow);

        _postTargetRepositoryMock
            .Setup(r => r.GetByIdAsync(postTargetId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(target);

        var post = new Post();
        typeof(Post).GetProperty("Id")!.SetValue(post, postId);
        typeof(Post).GetProperty("WorkspaceId")!.SetValue(post, workspaceId);

        _postRepositoryMock
            .Setup(r => r.GetByIdAsync(postId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(post);

        var member = WorkspaceMember.CreateOwner(workspaceId, userId, "owner@test.com", DateTime.UtcNow);
        _workspaceMemberRepositoryMock
            .Setup(r => r.GetByUserAndWorkspaceAsync(userId, workspaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(member);

        var snapshotDto = new PlatformPostAnalyticsSnapshot(DateTime.UtcNow, 100, 50, 10, 5, 2, 80, 120, 15.0, 0.75);
        _platformIntegrationServiceMock
            .Setup(s => s.GetPostAnalyticsAsync(It.IsAny<SocialAccount>(), "ext-post-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshotDto);

        var handler = new CollectPostSnapshotCommandHandler(
            _currentUserContextMock.Object,
            _postTargetRepositoryMock.Object,
            _postRepositoryMock.Object,
            _workspaceMemberRepositoryMock.Object,
            _platformIntegrationServiceMock.Object,
            _postAnalyticsSnapshotRepositoryMock.Object,
            _unitOfWorkMock.Object,
            _cacheServiceMock.Object,
            new Mock<ILogger<CollectPostSnapshotCommandHandler>>().Object);

        var command = new CollectPostSnapshotCommand(postTargetId);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _postAnalyticsSnapshotRepositoryMock.Verify(r => r.AddAsync(It.IsAny<PostAnalyticsSnapshot>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
