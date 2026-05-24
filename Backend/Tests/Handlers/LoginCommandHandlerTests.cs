using Application.Abstractions.Caching;
using Application.Abstractions.Persistence;
using Application.Abstractions.RateLimiting;
using Application.Abstractions.Repositories;
using Application.Abstractions.Security;
using Application.Common;
using Application.CQRS.Auth;
using Application.DTOs.Auth;
using Domain.Entities;
using Domain.Enums;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Tests.Handlers;

public class LoginCommandHandlerTests
{
    private readonly Mock<UserManager<ApplicationUser>> _userManagerMock;
    private readonly Mock<IWorkspaceMemberRepository> _workspaceMemberRepositoryMock;
    private readonly Mock<IRefreshTokenRepository> _refreshTokenRepositoryMock;
    private readonly Mock<IRefreshTokenHasher> _refreshTokenHasherMock;
    private readonly Mock<IJwtTokenGenerator> _jwtTokenGeneratorMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IRedisRateLimitService> _rateLimitServiceMock;
    private readonly Mock<ICacheService> _cacheServiceMock;
    private readonly Mock<ILogger<LoginCommandHandler>> _loggerMock;

    private readonly LoginCommandHandler _handler;

    public LoginCommandHandlerTests()
    {
        var storeMock = new Mock<IUserStore<ApplicationUser>>();
        _userManagerMock = new Mock<UserManager<ApplicationUser>>(
            storeMock.Object, null!, null!, null!, null!, null!, null!, null!, null!);

        _workspaceMemberRepositoryMock = new Mock<IWorkspaceMemberRepository>();
        _refreshTokenRepositoryMock = new Mock<IRefreshTokenRepository>();
        _refreshTokenHasherMock = new Mock<IRefreshTokenHasher>();
        _jwtTokenGeneratorMock = new Mock<IJwtTokenGenerator>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _rateLimitServiceMock = new Mock<IRedisRateLimitService>();
        _cacheServiceMock = new Mock<ICacheService>();
        _loggerMock = new Mock<ILogger<LoginCommandHandler>>();

        _handler = new LoginCommandHandler(
            _userManagerMock.Object,
            _workspaceMemberRepositoryMock.Object,
            _refreshTokenRepositoryMock.Object,
            _refreshTokenHasherMock.Object,
            _jwtTokenGeneratorMock.Object,
            _unitOfWorkMock.Object,
            _rateLimitServiceMock.Object,
            _cacheServiceMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_ShouldLoginUser_WhenCredentialsAreValid()
    {
        // Arrange
        var request = new LoginRequest("test@autopost.com", "SecurePass123!");
        var command = new LoginCommand(request);

        var userId = Guid.NewGuid();
        var entry = TestEntityFactory.CreateUser(userId, "Test User", "test@autopost.com");

        _rateLimitServiceMock
            .Setup(r => r.ConsumeAsync(
                "login-email",
                "test@autopost.com",
                5,
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RateLimitDecision(true, 5, TimeSpan.Zero));

        _userManagerMock
            .Setup(u => u.FindByEmailAsync("test@autopost.com"))
            .ReturnsAsync(entry);

        _userManagerMock
            .Setup(u => u.CheckPasswordAsync(entry, request.Password))
            .ReturnsAsync(true);

        var workspaceId = Guid.NewGuid();
        var workspaceMember = WorkspaceMember.CreateOwner(workspaceId, userId, "test@autopost.com", DateTime.UtcNow);
        _workspaceMemberRepositoryMock
            .Setup(w => w.GetActiveByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkspaceMember> { workspaceMember });

        _refreshTokenHasherMock
            .Setup(h => h.Hash(It.IsAny<string>()))
            .Returns("token-hash");

        _jwtTokenGeneratorMock
            .Setup(j => j.GenerateAccessToken(
                It.IsAny<ApplicationUser>(),
                workspaceId,
                WorkspaceRole.Owner.ToString(),
                It.IsAny<IEnumerable<System.Security.Claims.Claim>>()))
            .Returns("jwt-access-token");

        _jwtTokenGeneratorMock
            .Setup(j => j.GetAccessTokenExpiresAtUtc())
            .Returns(DateTime.UtcNow.AddMinutes(15));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.AccessToken.Should().Be("jwt-access-token");

        _rateLimitServiceMock.Verify(r => r.ResetAsync("login-email", "test@autopost.com", It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenRateLimitExceeded()
    {
        // Arrange
        var request = new LoginRequest("test@autopost.com", "SecurePass123!");
        var command = new LoginCommand(request);

        _rateLimitServiceMock
            .Setup(r => r.ConsumeAsync(
                "login-email",
                "test@autopost.com",
                5,
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RateLimitDecision(false, 0, TimeSpan.FromSeconds(60)));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ErrorCode.Validation);
        result.Error.Should().Contain("Too many login attempts");

        _userManagerMock.Verify(u => u.FindByEmailAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldFailWithUnauthorized_WhenUserNotFound()
    {
        // Arrange
        var request = new LoginRequest("test@autopost.com", "SecurePass123!");
        var command = new LoginCommand(request);

        _rateLimitServiceMock
            .Setup(r => r.ConsumeAsync(
                "login-email",
                "test@autopost.com",
                5,
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RateLimitDecision(true, 5, TimeSpan.Zero));

        _userManagerMock
            .Setup(u => u.FindByEmailAsync("test@autopost.com"))
            .ReturnsAsync((ApplicationUser)null!);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ErrorCode.Unauthorized);
        result.Error.Should().Be("Invalid credentials.");
    }

    [Fact]
    public async Task Handle_ShouldFailWithUnauthorized_WhenPasswordIsIncorrect()
    {
        // Arrange
        var request = new LoginRequest("test@autopost.com", "WrongPass123!");
        var command = new LoginCommand(request);

        var entry = TestEntityFactory.CreateUser(Guid.NewGuid(), "Test User", "test@autopost.com");

        _rateLimitServiceMock
            .Setup(r => r.ConsumeAsync(
                "login-email",
                "test@autopost.com",
                5,
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RateLimitDecision(true, 5, TimeSpan.Zero));

        _userManagerMock
            .Setup(u => u.FindByEmailAsync("test@autopost.com"))
            .ReturnsAsync(entry);

        _userManagerMock
            .Setup(u => u.CheckPasswordAsync(entry, request.Password))
            .ReturnsAsync(false);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ErrorCode.Unauthorized);
        result.Error.Should().Be("Invalid credentials.");
    }

    [Fact]
    public async Task Handle_ShouldFailWithForbidden_WhenUserHasNoActiveWorkspace()
    {
        // Arrange
        var request = new LoginRequest("test@autopost.com", "SecurePass123!");
        var command = new LoginCommand(request);

        var entry = TestEntityFactory.CreateUser(Guid.NewGuid(), "Test User", "test@autopost.com");

        _rateLimitServiceMock
            .Setup(r => r.ConsumeAsync(
                "login-email",
                "test@autopost.com",
                5,
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RateLimitDecision(true, 5, TimeSpan.Zero));

        _userManagerMock
            .Setup(u => u.FindByEmailAsync("test@autopost.com"))
            .ReturnsAsync(entry);

        _userManagerMock
            .Setup(u => u.CheckPasswordAsync(entry, request.Password))
            .ReturnsAsync(true);

        _workspaceMemberRepositoryMock
            .Setup(w => w.GetActiveByUserIdAsync(entry.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkspaceMember>()); // No active membership

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ErrorCode.Forbidden);
        result.Error.Should().Contain("No active workspace membership");
    }
}
