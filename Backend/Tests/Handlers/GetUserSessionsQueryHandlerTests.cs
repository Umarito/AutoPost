using Application.Abstractions.Caching;
using Application.Abstractions.Repositories;
using Application.Common;
using Application.CQRS.Auth;
using Application.DTOs.Auth;
using Domain.Entities;
using Domain.Common;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Tests.Handlers;

public class GetUserSessionsQueryHandlerTests
{
    private readonly Mock<ICurrentUserContext> _currentUserContextMock;
    private readonly Mock<IRefreshTokenRepository> _refreshTokenRepositoryMock;
    private readonly Mock<ICacheService> _cacheServiceMock;
    private readonly Mock<ILogger<GetUserSessionsQueryHandler>> _loggerMock;

    private readonly GetUserSessionsQueryHandler _handler;

    public GetUserSessionsQueryHandlerTests()
    {
        _currentUserContextMock = new Mock<ICurrentUserContext>();
        _refreshTokenRepositoryMock = new Mock<IRefreshTokenRepository>();
        _cacheServiceMock = new Mock<ICacheService>();
        _loggerMock = new Mock<ILogger<GetUserSessionsQueryHandler>>();

        _handler = new GetUserSessionsQueryHandler(
            _currentUserContextMock.Object,
            _refreshTokenRepositoryMock.Object,
            _cacheServiceMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_ShouldReturnSessionsFromCache_WhenCacheIsHit()
    {
        // Arrange
        var query = new GetUserSessionsQuery();
        var userId = Guid.NewGuid();

        _currentUserContextMock.Setup(c => c.UserId).Returns(userId);
        _currentUserContextMock.Setup(c => c.SessionId).Returns(Guid.NewGuid());

        var cachedSessions = new List<UserSessionDto>
        {
            new(Guid.NewGuid(), DateTime.UtcNow, DateTime.UtcNow.AddDays(7), null, "device", "127.0.0.1", true)
        };

        _cacheServiceMock
            .Setup(c => c.GetAsync<List<UserSessionDto>>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(cachedSessions);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEquivalentTo(cachedSessions);

        _refreshTokenRepositoryMock.Verify(r => r.GetByUserIdAsync(It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldLoadSessionsFromRepositoryAndCache_WhenCacheIsMiss()
    {
        // Arrange
        var query = new GetUserSessionsQuery();
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();

        _currentUserContextMock.Setup(c => c.UserId).Returns(userId);
        _currentUserContextMock.Setup(c => c.SessionId).Returns(sessionId);

        _cacheServiceMock
            .Setup(c => c.GetAsync<List<UserSessionDto>>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((List<UserSessionDto>)null!);

        var dbSessions = new List<RefreshToken>
        {
            RefreshToken.Issue(userId, "token-hash-1", DateTime.UtcNow, DateTime.UtcNow.AddDays(7), "device-1", "127.0.0.1")
        };
        // Use reflection to set the private Id field so it can match the session
        typeof(BaseEntity<Guid>).GetProperty("Id")!.SetValue(dbSessions[0], sessionId);

        _refreshTokenRepositoryMock
            .Setup(r => r.GetByUserIdAsync(userId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dbSessions);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value![0].IsCurrent.Should().BeTrue();

        _cacheServiceMock.Verify(c => c.SetAsync(It.IsAny<string>(), It.IsAny<List<UserSessionDto>>(), TimeSpan.FromMinutes(2), It.IsAny<CancellationToken>()), Times.Once);
    }
}
