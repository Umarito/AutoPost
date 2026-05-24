using Application.Abstractions.Caching;
using Application.Abstractions.Persistence;
using Application.Abstractions.Repositories;
using Application.Abstractions.Security;
using Application.Common;
using Application.CQRS.Auth;
using Domain.Entities;
using Domain.Enums;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Tests.Handlers;

public class RefreshTokenCommandHandlerTests
{
    private readonly Mock<IRefreshTokenRepository> _refreshTokenRepositoryMock;
    private readonly Mock<IRefreshTokenHasher> _refreshTokenHasherMock;
    private readonly Mock<IWorkspaceMemberRepository> _workspaceMemberRepositoryMock;
    private readonly Mock<IJwtTokenGenerator> _jwtTokenGeneratorMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<ICacheService> _cacheServiceMock;
    private readonly Mock<ILogger<RefreshTokenCommandHandler>> _loggerMock;

    private readonly RefreshTokenCommandHandler _handler;

    public RefreshTokenCommandHandlerTests()
    {
        _refreshTokenRepositoryMock = new Mock<IRefreshTokenRepository>();
        _refreshTokenHasherMock = new Mock<IRefreshTokenHasher>();
        _workspaceMemberRepositoryMock = new Mock<IWorkspaceMemberRepository>();
        _jwtTokenGeneratorMock = new Mock<IJwtTokenGenerator>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _cacheServiceMock = new Mock<ICacheService>();
        _loggerMock = new Mock<ILogger<RefreshTokenCommandHandler>>();

        _handler = new RefreshTokenCommandHandler(
            _refreshTokenRepositoryMock.Object,
            _refreshTokenHasherMock.Object,
            _workspaceMemberRepositoryMock.Object,
            _jwtTokenGeneratorMock.Object,
            _unitOfWorkMock.Object,
            _cacheServiceMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_ShouldRotateToken_WhenTokenIsValid()
    {
        // Arrange
        var command = new RefreshTokenCommand("valid-raw-refresh-token");
        var userId = Guid.NewGuid();
        var user = new ApplicationUser { Id = userId, Email = "test@autopost.com" };

        var token = RefreshToken.Issue(
            userId,
            "hashed-token",
            DateTime.UtcNow.AddDays(-1),
            DateTime.UtcNow.AddDays(7),
            "device",
            "127.0.0.1");

        // Use reflection to set User on RefreshToken
        typeof(RefreshToken).GetProperty("User")!.SetValue(token, user);

        _refreshTokenHasherMock
            .Setup(h => h.Hash("valid-raw-refresh-token"))
            .Returns("hashed-token");

        // IssueTokensAsync generates a NEW raw refresh token and hashes it during rotation.
        _refreshTokenHasherMock
            .Setup(h => h.Hash(It.Is<string>(s => s != "valid-raw-refresh-token")))
            .Returns("new-hashed-token");

        _refreshTokenRepositoryMock
            .Setup(r => r.AddAsync(It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((RefreshToken r, CancellationToken _) => r);

        _refreshTokenRepositoryMock
            .Setup(r => r.GetByTokenHashAsync("hashed-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(token);

        var workspaceId = Guid.NewGuid();
        var workspaceMember = WorkspaceMember.CreateOwner(workspaceId, userId, "test@autopost.com", DateTime.UtcNow);
        _workspaceMemberRepositoryMock
            .Setup(w => w.GetActiveByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkspaceMember> { workspaceMember });

        _jwtTokenGeneratorMock
            .Setup(j => j.GenerateAccessToken(
                It.IsAny<ApplicationUser>(),
                workspaceId,
                WorkspaceRole.Owner.ToString(),
                It.IsAny<IEnumerable<System.Security.Claims.Claim>>()))
            .Returns("new-access-token");

        _jwtTokenGeneratorMock
            .Setup(j => j.GetAccessTokenExpiresAtUtc())
            .Returns(DateTime.UtcNow.AddMinutes(15));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.AccessToken.Should().Be("new-access-token");
        token.IsUsed.Should().BeTrue();

        _refreshTokenRepositoryMock.Verify(r => r.Update(token), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldDetectTokenReplayAndRevokeAll_WhenTokenIsAlreadyUsed()
    {
        // Arrange
        var command = new RefreshTokenCommand("reused-raw-token");
        var userId = Guid.NewGuid();
        var user = new ApplicationUser { Id = userId, Email = "test@autopost.com" };

        var token = RefreshToken.Issue(
            userId,
            "hashed-token",
            DateTime.UtcNow.AddDays(-1),
            DateTime.UtcNow.AddDays(7),
            "device",
            "127.0.0.1");

        // Mark as used
        token.MarkUsed();
        typeof(RefreshToken).GetProperty("User")!.SetValue(token, user);

        _refreshTokenHasherMock
            .Setup(h => h.Hash("reused-raw-token"))
            .Returns("hashed-token");

        _refreshTokenRepositoryMock
            .Setup(r => r.GetByTokenHashAsync("hashed-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(token);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ErrorCode.Unauthorized);
        result.Error.Should().Contain("no longer valid");

        // Verify Token Replay protection: all active tokens for this user must be revoked
        _refreshTokenRepositoryMock.Verify(r => r.RevokeAllForUserAsync(userId, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _cacheServiceMock.Verify(c => c.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenTokenIsExpired()
    {
        // Arrange
        var command = new RefreshTokenCommand("expired-raw-token");
        var userId = Guid.NewGuid();
        var user = new ApplicationUser { Id = userId, Email = "test@autopost.com" };

        var token = RefreshToken.Issue(
            userId,
            "hashed-token",
            DateTime.UtcNow.AddDays(-10),
            DateTime.UtcNow.AddDays(-1), // Expired
            "device",
            "127.0.0.1");

        typeof(RefreshToken).GetProperty("User")!.SetValue(token, user);

        _refreshTokenHasherMock
            .Setup(h => h.Hash("expired-raw-token"))
            .Returns("hashed-token");

        _refreshTokenRepositoryMock
            .Setup(r => r.GetByTokenHashAsync("hashed-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(token);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ErrorCode.Unauthorized);
        result.Error.Should().Contain("expired");
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenTokenIsNotFound()
    {
        // Arrange
        var command = new RefreshTokenCommand("nonexistent-raw-token");

        _refreshTokenHasherMock
            .Setup(h => h.Hash("nonexistent-raw-token"))
            .Returns("hashed-token");

        _refreshTokenRepositoryMock
            .Setup(r => r.GetByTokenHashAsync("hashed-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync((RefreshToken)null!);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ErrorCode.Unauthorized);
    }
}
