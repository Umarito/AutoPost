using Application.Abstractions.Notifications;
using Application.Abstractions.RateLimiting;
using Application.Common;
using Application.CQRS.Auth;
using Domain.Entities;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Tests.Handlers;

public class ResendEmailConfirmationCommandHandlerTests
{
    private readonly Mock<UserManager<ApplicationUser>> _userManagerMock;
    private readonly Mock<IRedisRateLimitService> _rateLimitServiceMock;
    private readonly Mock<IEmailService> _emailServiceMock;
    private readonly Mock<ILogger<ResendEmailConfirmationCommandHandler>> _loggerMock;

    private readonly ResendEmailConfirmationCommandHandler _handler;

    public ResendEmailConfirmationCommandHandlerTests()
    {
        var storeMock = new Mock<IUserStore<ApplicationUser>>();
        _userManagerMock = new Mock<UserManager<ApplicationUser>>(
            storeMock.Object, null!, null!, null!, null!, null!, null!, null!, null!);

        _rateLimitServiceMock = new Mock<IRedisRateLimitService>();
        _emailServiceMock = new Mock<IEmailService>();
        _loggerMock = new Mock<ILogger<ResendEmailConfirmationCommandHandler>>();

        _handler = new ResendEmailConfirmationCommandHandler(
            _userManagerMock.Object,
            _rateLimitServiceMock.Object,
            _emailServiceMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_ShouldSendEmail_WhenUserIsNotConfirmedAndRateLimitAllows()
    {
        // Arrange
        var command = new ResendEmailConfirmationCommand("test@autopost.com");

        _rateLimitServiceMock
            .Setup(r => r.ConsumeAsync(
                "resend-confirmation-email",
                "test@autopost.com",
                3,
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RateLimitDecision(true, 3, TimeSpan.Zero));

        var user = new ApplicationUser { Id = Guid.NewGuid(), Email = "test@autopost.com", EmailConfirmed = false };
        _userManagerMock
            .Setup(u => u.FindByEmailAsync("test@autopost.com"))
            .ReturnsAsync(user);

        _userManagerMock
            .Setup(u => u.GenerateEmailConfirmationTokenAsync(user))
            .ReturnsAsync("new-token-123");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _emailServiceMock.Verify(e => e.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldReturnSuccessImmediatelyWithoutSending_WhenEmailIsAlreadyConfirmed()
    {
        // Arrange
        var command = new ResendEmailConfirmationCommand("confirmed@autopost.com");

        _rateLimitServiceMock
            .Setup(r => r.ConsumeAsync(
                "resend-confirmation-email",
                "confirmed@autopost.com",
                3,
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RateLimitDecision(true, 3, TimeSpan.Zero));

        var user = new ApplicationUser { Id = Guid.NewGuid(), Email = "confirmed@autopost.com", EmailConfirmed = true };
        _userManagerMock
            .Setup(u => u.FindByEmailAsync("confirmed@autopost.com"))
            .ReturnsAsync(user);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue(); // Safe response
        _emailServiceMock.Verify(e => e.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldReturnSuccessImmediatelyWithoutSending_WhenUserNotFound()
    {
        // Arrange
        var command = new ResendEmailConfirmationCommand("nonexistent@autopost.com");

        _rateLimitServiceMock
            .Setup(r => r.ConsumeAsync(
                "resend-confirmation-email",
                "nonexistent@autopost.com",
                3,
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RateLimitDecision(true, 3, TimeSpan.Zero));

        _userManagerMock
            .Setup(u => u.FindByEmailAsync("nonexistent@autopost.com"))
            .ReturnsAsync((ApplicationUser)null!);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue(); // Safe response to prevent email discovery
        _emailServiceMock.Verify(e => e.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenRateLimitExceeded()
    {
        // Arrange
        var command = new ResendEmailConfirmationCommand("test@autopost.com");

        _rateLimitServiceMock
            .Setup(r => r.ConsumeAsync(
                "resend-confirmation-email",
                "test@autopost.com",
                3,
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RateLimitDecision(false, 0, TimeSpan.FromHours(1)));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ErrorCode.Validation);
        result.Error.Should().Contain("Too many confirmation email requests");
    }
}
