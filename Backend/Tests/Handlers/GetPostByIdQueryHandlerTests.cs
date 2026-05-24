using Application.Abstractions.Caching;
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

public class GetPostByIdQueryHandlerTests
{
    private readonly Mock<ICurrentUserContext> _currentUserContextMock;
    private readonly Mock<IPostRepository> _postRepositoryMock;
    private readonly Mock<IWorkspaceMemberRepository> _workspaceMemberRepositoryMock;
    private readonly Mock<ICacheService> _cacheServiceMock;
    private readonly Mock<IMapper> _mapperMock;
    private readonly Mock<ILogger<GetPostByIdQueryHandler>> _loggerMock;

    private readonly GetPostByIdQueryHandler _handler;

    public GetPostByIdQueryHandlerTests()
    {
        _currentUserContextMock = new Mock<ICurrentUserContext>();
        _postRepositoryMock = new Mock<IPostRepository>();
        _workspaceMemberRepositoryMock = new Mock<IWorkspaceMemberRepository>();
        _cacheServiceMock = new Mock<ICacheService>();
        _mapperMock = new Mock<IMapper>();
        _loggerMock = new Mock<ILogger<GetPostByIdQueryHandler>>();

        _handler = new GetPostByIdQueryHandler(
            _currentUserContextMock.Object,
            _postRepositoryMock.Object,
            _workspaceMemberRepositoryMock.Object,
            _cacheServiceMock.Object,
            _mapperMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_ShouldReturnPostFromCache_WhenCacheIsHit()
    {
        // Arrange
        var postId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var query = new GetPostByIdQuery(postId);

        _currentUserContextMock.Setup(c => c.UserId).Returns(userId);

        var post = new Post();
        typeof(Post).GetProperty("Id")!.SetValue(post, postId);
        typeof(Post).GetProperty("WorkspaceId")!.SetValue(post, workspaceId);

        _postRepositoryMock
            .Setup(r => r.GetByIdWithTargetsAsync(postId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(post);

        var member = WorkspaceMember.CreateOwner(workspaceId, userId, "owner@test.com", DateTime.UtcNow);
        _workspaceMemberRepositoryMock
            .Setup(r => r.GetByUserAndWorkspaceAsync(userId, workspaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(member);

        _cacheServiceMock
            .Setup(c => c.GetAsync<string>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("cache-stamp-123");

        var cachedDto = new PostDetailDto(postId, "Cached Title", "Desc", new List<string>(), "Public", "Draft", DateTime.UtcNow, "UTC", null, null, new List<PostTargetDto>(), DateTime.UtcNow);
        _cacheServiceMock
            .Setup(c => c.GetAsync<PostDetailDto>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(cachedDto);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEquivalentTo(cachedDto);

        _mapperMock.Verify(m => m.Map<PostDetailDto>(It.IsAny<Post>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldLoadPostFromDbAndCache_WhenCacheIsMiss()
    {
        // Arrange
        var postId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var query = new GetPostByIdQuery(postId);

        _currentUserContextMock.Setup(c => c.UserId).Returns(userId);

        var post = new Post();
        typeof(Post).GetProperty("Id")!.SetValue(post, postId);
        typeof(Post).GetProperty("WorkspaceId")!.SetValue(post, workspaceId);

        _postRepositoryMock
            .Setup(r => r.GetByIdWithTargetsAsync(postId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(post);

        var member = WorkspaceMember.CreateOwner(workspaceId, userId, "owner@test.com", DateTime.UtcNow);
        _workspaceMemberRepositoryMock
            .Setup(r => r.GetByUserAndWorkspaceAsync(userId, workspaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(member);

        _cacheServiceMock
            .Setup(c => c.GetAsync<string>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string)null!);

        _cacheServiceMock
            .Setup(c => c.GetAsync<PostDetailDto>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PostDetailDto)null!);

        var dbDto = new PostDetailDto(postId, "Db Title", "Desc", new List<string>(), "Public", "Draft", DateTime.UtcNow, "UTC", null, null, new List<PostTargetDto>(), DateTime.UtcNow);
        _mapperMock
            .Setup(m => m.Map<PostDetailDto>(post))
            .Returns(dbDto);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEquivalentTo(dbDto);

        _cacheServiceMock.Verify(c => c.SetAsync(It.IsAny<string>(), dbDto, TimeSpan.FromMinutes(5), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldFailWithForbidden_WhenUserHasNoReadAccess()
    {
        // Arrange
        var postId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var query = new GetPostByIdQuery(postId);

        _currentUserContextMock.Setup(c => c.UserId).Returns(userId);

        var post = new Post();
        typeof(Post).GetProperty("Id")!.SetValue(post, postId);
        typeof(Post).GetProperty("WorkspaceId")!.SetValue(post, workspaceId);

        _postRepositoryMock
            .Setup(r => r.GetByIdWithTargetsAsync(postId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(post);

        // No membership found -> Forbidden (IDOR prevention)
        _workspaceMemberRepositoryMock
            .Setup(r => r.GetByUserAndWorkspaceAsync(userId, workspaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((WorkspaceMember)null!);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ErrorCode.Forbidden);
    }
}
