using Application.Abstractions.Persistence;
using Application.Abstractions.Repositories;
using Application.Common;
using Application.CQRS.Auth;
using Application.DTOs.Auth;
using Domain.Entities;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Tests.Handlers;

public class UpdateUserProfileCommandHandlerTests
{
    private readonly Mock<ICurrentUserContext> _currentUserContextMock;
    private readonly Mock<IApplicationUserRepository> _applicationUserRepositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<ILogger<UpdateUserProfileCommandHandler>> _loggerMock;

    private readonly UpdateUserProfileCommandHandler _handler;

    public UpdateUserProfileCommandHandlerTests()
    {
        _currentUserContextMock = new Mock<ICurrentUserContext>();
        _applicationUserRepositoryMock = new Mock<IApplicationUserRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _loggerMock = new Mock<ILogger<UpdateUserProfileCommandHandler>>();

        _handler = new UpdateUserProfileCommandHandler(
            _currentUserContextMock.Object,
            _applicationUserRepositoryMock.Object,
            _unitOfWorkMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_ShouldUpdateProfile_WhenUserExists()
    {
        // Arrange
        var request = new UpdateProfileRequest("New Name", "avatar.jpg", "Asia/Tokyo", "ja");
        var command = new UpdateUserProfileCommand(request);

        var userId = Guid.NewGuid();
        _currentUserContextMock
            .Setup(c => c.UserId)
            .Returns(userId);

        var user = TestEntityFactory.CreateUser(userId, "Old Name", "test@autopost.com");

        _applicationUserRepositoryMock
            .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.DisplayName.Should().Be("New Name");
        result.Value.AvatarUrl.Should().Be("avatar.jpg");
        result.Value.TimeZoneId.Should().Be("Asia/Tokyo");
        result.Value.Locale.Should().Be("ja");

        _applicationUserRepositoryMock.Verify(r => r.Update(user), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldFailWithUnauthorized_WhenUserDoesNotExist()
    {
        // Arrange
        var request = new UpdateProfileRequest("New Name", "avatar.jpg", "Asia/Tokyo", "ja");
        var command = new UpdateUserProfileCommand(request);

        var userId = Guid.NewGuid();
        _currentUserContextMock
            .Setup(c => c.UserId)
            .Returns(userId);

        _applicationUserRepositoryMock
            .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ApplicationUser)null!);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ErrorCode.Unauthorized);
    }
}
