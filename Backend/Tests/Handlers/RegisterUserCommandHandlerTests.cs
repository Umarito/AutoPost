using Application.Abstractions.Notifications;
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

public class RegisterUserCommandHandlerTests
{
    private readonly Mock<UserManager<ApplicationUser>> _userManagerMock;
    private readonly Mock<IWorkspaceRepository> _workspaceRepositoryMock;
    private readonly Mock<IWorkspaceMemberRepository> _workspaceMemberRepositoryMock;
    private readonly Mock<INotificationPreferenceRepository> _notificationPreferenceRepositoryMock;
    private readonly Mock<IRefreshTokenRepository> _refreshTokenRepositoryMock;
    private readonly Mock<IRefreshTokenHasher> _refreshTokenHasherMock;
    private readonly Mock<IJwtTokenGenerator> _jwtTokenGeneratorMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IRedisRateLimitService> _rateLimitServiceMock;
    private readonly Mock<IEmailService> _emailServiceMock;
    private readonly Mock<ILogger<RegisterUserCommandHandler>> _loggerMock;
    private readonly Mock<IUnitOfWorkTransaction> _transactionMock;

    private readonly RegisterUserCommandHandler _handler;

    public RegisterUserCommandHandlerTests()
    {
        var storeMock = new Mock<IUserStore<ApplicationUser>>();
        _userManagerMock = new Mock<UserManager<ApplicationUser>>(
            storeMock.Object, null!, null!, null!, null!, null!, null!, null!, null!);

        _workspaceRepositoryMock = new Mock<IWorkspaceRepository>();
        _workspaceMemberRepositoryMock = new Mock<IWorkspaceMemberRepository>();
        _notificationPreferenceRepositoryMock = new Mock<INotificationPreferenceRepository>();
        _refreshTokenRepositoryMock = new Mock<IRefreshTokenRepository>();
        _refreshTokenHasherMock = new Mock<IRefreshTokenHasher>();
        _jwtTokenGeneratorMock = new Mock<IJwtTokenGenerator>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _rateLimitServiceMock = new Mock<IRedisRateLimitService>();
        _emailServiceMock = new Mock<IEmailService>();
        _loggerMock = new Mock<ILogger<RegisterUserCommandHandler>>();
        _transactionMock = new Mock<IUnitOfWorkTransaction>();

        _unitOfWorkMock
            .Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(_transactionMock.Object);

        _handler = new RegisterUserCommandHandler(
            _userManagerMock.Object,
            _workspaceRepositoryMock.Object,
            _workspaceMemberRepositoryMock.Object,
            _notificationPreferenceRepositoryMock.Object,
            _refreshTokenRepositoryMock.Object,
            _refreshTokenHasherMock.Object,
            _jwtTokenGeneratorMock.Object,
            _unitOfWorkMock.Object,
            _rateLimitServiceMock.Object,
            _emailServiceMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_ShouldRegisterUserAndBootstrapWorkspace_WhenRequestIsValid()
    {
        // Arrange
        var request = new RegisterRequest("test@autopost.com", "SecurePass123!", "Test User");
        var command = new RegisterUserCommand(request);

        _rateLimitServiceMock
            .Setup(r => r.ConsumeAsync(
                "register-email",
                "test@autopost.com",
                5,
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RateLimitDecision(true, 5, TimeSpan.Zero));

        _userManagerMock
            .Setup(u => u.FindByEmailAsync("test@autopost.com"))
            .ReturnsAsync((ApplicationUser)null!);

        _workspaceRepositoryMock
            .Setup(w => w.SlugExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _userManagerMock
            .Setup(u => u.CreateAsync(It.IsAny<ApplicationUser>(), request.Password))
            .ReturnsAsync(IdentityResult.Success);

        _userManagerMock
            .Setup(u => u.GenerateEmailConfirmationTokenAsync(It.IsAny<ApplicationUser>()))
            .ReturnsAsync("confirmation-token-123");

        _refreshTokenHasherMock
            .Setup(h => h.Hash(It.IsAny<string>()))
            .Returns("token-hash");

        _jwtTokenGeneratorMock
            .Setup(j => j.GenerateAccessToken(
                It.IsAny<ApplicationUser>(),
                It.IsAny<Guid>(),
                It.IsAny<string>(),
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
        result.Value.RefreshToken.Should().NotBeNullOrWhiteSpace();

        _userManagerMock.Verify(u => u.CreateAsync(It.IsAny<ApplicationUser>(), request.Password), Times.Once);
        _workspaceRepositoryMock.Verify(w => w.AddAsync(It.IsAny<Workspace>(), It.IsAny<CancellationToken>()), Times.Once);
        _workspaceMemberRepositoryMock.Verify(w => w.AddAsync(It.IsAny<WorkspaceMember>(), It.IsAny<CancellationToken>()), Times.Once);
        _notificationPreferenceRepositoryMock.Verify(n => n.AddAsync(It.IsAny<NotificationPreference>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        _emailServiceMock.Verify(e => e.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()), Times.Once);
        _transactionMock.Verify(t => t.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenRateLimitExceeded()
    {
        // Arrange
        var request = new RegisterRequest("test@autopost.com", "SecurePass123!", "Test User");
        var command = new RegisterUserCommand(request);

        _rateLimitServiceMock
            .Setup(r => r.ConsumeAsync(
                "register-email",
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
        result.Error.Should().Contain("Too many registration attempts");

        _userManagerMock.Verify(u => u.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenEmailAlreadyExists()
    {
        // Arrange
        var request = new RegisterRequest("test@autopost.com", "SecurePass123!", "Test User");
        var command = new RegisterUserCommand(request);

        _rateLimitServiceMock
            .Setup(r => r.ConsumeAsync(
                "register-email",
                "test@autopost.com",
                5,
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RateLimitDecision(true, 5, TimeSpan.Zero));

        var existingUser = new ApplicationUser { Email = "test@autopost.com" };
        _userManagerMock
            .Setup(u => u.FindByEmailAsync("test@autopost.com"))
            .ReturnsAsync(existingUser);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ErrorCode.Conflict);
        result.Error.Should().Contain("already registered");

        _userManagerMock.Verify(u => u.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldRollbackAndFail_WhenIdentityCreationFails()
    {
        // Arrange
        var request = new RegisterRequest("test@autopost.com", "SecurePass123!", "Test User");
        var command = new RegisterUserCommand(request);

        _rateLimitServiceMock
            .Setup(r => r.ConsumeAsync(
                "register-email",
                "test@autopost.com",
                5,
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RateLimitDecision(true, 5, TimeSpan.Zero));

        _userManagerMock
            .Setup(u => u.FindByEmailAsync("test@autopost.com"))
            .ReturnsAsync((ApplicationUser)null!);

        _userManagerMock
            .Setup(u => u.CreateAsync(It.IsAny<ApplicationUser>(), request.Password))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Password too weak." }));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ErrorCode.Validation);
        result.Error.Should().Contain("Password too weak.");

        _transactionMock.Verify(t => t.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        _transactionMock.Verify(t => t.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
