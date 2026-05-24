using Application.Common;
using Application.CQRS.Auth;
using Domain.Entities;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Tests.Handlers;

public class ConfirmEmailCommandHandlerTests
{
    private readonly Mock<UserManager<ApplicationUser>> _userManagerMock;
    private readonly Mock<ILogger<ConfirmEmailCommandHandler>> _loggerMock;

    private readonly ConfirmEmailCommandHandler _handler;

    public ConfirmEmailCommandHandlerTests()
    {
        var storeMock = new Mock<IUserStore<ApplicationUser>>();
        _userManagerMock = new Mock<UserManager<ApplicationUser>>(
            storeMock.Object, null!, null!, null!, null!, null!, null!, null!, null!);

        _loggerMock = new Mock<ILogger<ConfirmEmailCommandHandler>>();

        _handler = new ConfirmEmailCommandHandler(_userManagerMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_ShouldConfirmEmail_WhenTokenIsValidAndNotAlreadyConfirmed()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new ConfirmEmailCommand(userId, "valid-token");

        var user = new ApplicationUser { Id = userId, Email = "test@autopost.com" };
        // Use reflection to set private set properties if needed, but EmailConfirmed has public set in ASP.NET Core Identity
        user.EmailConfirmed = false;

        _userManagerMock
            .Setup(u => u.FindByIdAsync(userId.ToString()))
            .ReturnsAsync(user);

        _userManagerMock
            .Setup(u => u.ConfirmEmailAsync(user, "valid-token"))
            .ReturnsAsync(IdentityResult.Success);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _userManagerMock.Verify(u => u.ConfirmEmailAsync(user, "valid-token"), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldReturnSuccessImmediately_WhenEmailIsAlreadyConfirmed()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new ConfirmEmailCommand(userId, "any-token");

        var user = new ApplicationUser { Id = userId, Email = "test@autopost.com", EmailConfirmed = true };

        _userManagerMock
            .Setup(u => u.FindByIdAsync(userId.ToString()))
            .ReturnsAsync(user);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _userManagerMock.Verify(u => u.ConfirmEmailAsync(user, It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldFailWithNotFound_WhenUserDoesNotExist()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new ConfirmEmailCommand(userId, "any-token");

        _userManagerMock
            .Setup(u => u.FindByIdAsync(userId.ToString()))
            .ReturnsAsync((ApplicationUser)null!);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ErrorCode.NotFound);
    }

    [Fact]
    public async Task Handle_ShouldFailWithValidation_WhenIdentityConfirmationFails()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new ConfirmEmailCommand(userId, "invalid-token");

        var user = new ApplicationUser { Id = userId, Email = "test@autopost.com", EmailConfirmed = false };

        _userManagerMock
            .Setup(u => u.FindByIdAsync(userId.ToString()))
            .ReturnsAsync(user);

        _userManagerMock
            .Setup(u => u.ConfirmEmailAsync(user, "invalid-token"))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Invalid token." }));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ErrorCode.Validation);
        result.Error.Should().Contain("Invalid token.");
    }
}
