using Application.Abstractions.Caching;
using Application.Abstractions.Persistence;
using Application.Abstractions.Repositories;
using Application.Abstractions.Security;
using Application.Common;
using Application.CQRS.Auth;
using Domain.Entities;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Tests.Handlers;

public class LogoutCommandHandlerTests
{
    private readonly Mock<IRefreshTokenRepository> _refreshTokenRepositoryMock;
    private readonly Mock<IRefreshTokenHasher> _refreshTokenHasherMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<ICacheService> _cacheServiceMock;
    private readonly Mock<ILogger<LogoutCommandHandler>> _loggerMock;

    private readonly LogoutCommandHandler _handler;

    public LogoutCommandHandlerTests()
    {
        _refreshTokenRepositoryMock = new Mock<IRefreshTokenRepository>();
        _refreshTokenHasherMock = new Mock<IRefreshTokenHasher>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _cacheServiceMock = new Mock<ICacheService>();
        _loggerMock = new Mock<ILogger<LogoutCommandHandler>>();

        _handler = new LogoutCommandHandler(
            _refreshTokenRepositoryMock.Object,
            _refreshTokenHasherMock.Object,
            _unitOfWorkMock.Object,
            _cacheServiceMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_ShouldRevokeToken_WhenTokenIsActive()
    {
        // Arrange
        var command = new LogoutCommand("active-raw-token");
        var userId = Guid.NewGuid();

        var token = RefreshToken.Issue(
            userId,
            "hashed-token",
            DateTime.UtcNow,
            DateTime.UtcNow.AddDays(7),
            "device",
            "127.0.0.1");

        _refreshTokenHasherMock
            .Setup(h => h.Hash("active-raw-token"))
            .Returns("hashed-token");

        _refreshTokenRepositoryMock
            .Setup(r => r.GetByTokenHashAsync("hashed-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(token);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        token.IsRevoked.Should().BeTrue();
        token.RevokedAt.Should().NotBeNull();

        _refreshTokenRepositoryMock.Verify(r => r.Update(token), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _cacheServiceMock.Verify(c => c.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task Handle_ShouldReturnSuccessIdempotently_WhenTokenIsAlreadyRevoked()
    {
        // Arrange
        var command = new LogoutCommand("revoked-raw-token");
        var userId = Guid.NewGuid();

        var token = RefreshToken.Issue(
            userId,
            "hashed-token",
            DateTime.UtcNow,
            DateTime.UtcNow.AddDays(7),
            "device",
            "127.0.0.1");
        token.Revoke(DateTime.UtcNow);

        _refreshTokenHasherMock
            .Setup(h => h.Hash("revoked-raw-token"))
            .Returns("hashed-token");

        _refreshTokenRepositoryMock
            .Setup(r => r.GetByTokenHashAsync("hashed-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(token);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();

        _refreshTokenRepositoryMock.Verify(r => r.Update(It.IsAny<RefreshToken>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldReturnSuccessIdempotently_WhenTokenNotFound()
    {
        // Arrange
        var command = new LogoutCommand("nonexistent-raw-token");

        _refreshTokenHasherMock
            .Setup(h => h.Hash("nonexistent-raw-token"))
            .Returns("hashed-token");

        _refreshTokenRepositoryMock
            .Setup(r => r.GetByTokenHashAsync("hashed-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync((RefreshToken)null!);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();

        _refreshTokenRepositoryMock.Verify(r => r.Update(It.IsAny<RefreshToken>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
