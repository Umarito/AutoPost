using Application.Abstractions.Caching;
using Application.Abstractions.Persistence;
using Application.Abstractions.Repositories;
using Application.Common;
using Application.CQRS.Auth;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Tests.Handlers;

public class LogoutAllDevicesCommandHandlerTests
{
    private readonly Mock<ICurrentUserContext> _currentUserContextMock;
    private readonly Mock<IRefreshTokenRepository> _refreshTokenRepositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<ICacheService> _cacheServiceMock;
    private readonly Mock<ILogger<LogoutAllDevicesCommandHandler>> _loggerMock;

    private readonly LogoutAllDevicesCommandHandler _handler;

    public LogoutAllDevicesCommandHandlerTests()
    {
        _currentUserContextMock = new Mock<ICurrentUserContext>();
        _refreshTokenRepositoryMock = new Mock<IRefreshTokenRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _cacheServiceMock = new Mock<ICacheService>();
        _loggerMock = new Mock<ILogger<LogoutAllDevicesCommandHandler>>();

        _handler = new LogoutAllDevicesCommandHandler(
            _currentUserContextMock.Object,
            _refreshTokenRepositoryMock.Object,
            _unitOfWorkMock.Object,
            _cacheServiceMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_ShouldRevokeAllSessions_WhenUserIsAuthenticated()
    {
        // Arrange
        var command = new LogoutAllDevicesCommand();
        var userId = Guid.NewGuid();

        _currentUserContextMock
            .Setup(c => c.UserId)
            .Returns(userId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();

        _refreshTokenRepositoryMock.Verify(r => r.RevokeAllForUserAsync(userId, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _cacheServiceMock.Verify(c => c.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }
}
