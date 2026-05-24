using Application.Abstractions.Repositories;
using Application.Common;
using Application.CQRS.Auth;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Tests.Handlers;

public class GetUserByIdQueryHandlerTests
{
    private readonly Mock<ICurrentUserContext> _currentUserContextMock;
    private readonly Mock<IApplicationUserRepository> _applicationUserRepositoryMock;
    private readonly Mock<ILogger<GetUserByIdQueryHandler>> _loggerMock;

    private readonly GetUserByIdQueryHandler _handler;

    public GetUserByIdQueryHandlerTests()
    {
        _currentUserContextMock = new Mock<ICurrentUserContext>();
        _applicationUserRepositoryMock = new Mock<IApplicationUserRepository>();
        _loggerMock = new Mock<ILogger<GetUserByIdQueryHandler>>();

        _handler = new GetUserByIdQueryHandler(
            _currentUserContextMock.Object,
            _applicationUserRepositoryMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_ShouldReturnProfile_WhenQueryingSelfAndUserExists()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var query = new GetUserByIdQuery(userId);

        _currentUserContextMock
            .Setup(c => c.UserId)
            .Returns(userId);

        var user = TestEntityFactory.CreateUser(userId, "Test User", "test@autopost.com");

        _applicationUserRepositoryMock
            .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Id.Should().Be(userId);
    }

    [Fact]
    public async Task Handle_ShouldFailWithForbidden_WhenQueryingOtherUserProfile()
    {
        // Arrange
        var currentUserId = Guid.NewGuid();
        var targetUserId = Guid.NewGuid(); // Foreign user ID (IDOR attempt)
        var query = new GetUserByIdQuery(targetUserId);

        _currentUserContextMock
            .Setup(c => c.UserId)
            .Returns(currentUserId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ErrorCode.Forbidden);
        result.Error.Should().Contain("Access to another user's profile is forbidden");

        _applicationUserRepositoryMock.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldFailWithNotFound_WhenUserDoesNotExist()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var query = new GetUserByIdQuery(userId);

        _currentUserContextMock
            .Setup(c => c.UserId)
            .Returns(userId);

        _applicationUserRepositoryMock
            .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Domain.Entities.ApplicationUser)null!);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ErrorCode.NotFound);
    }
}
