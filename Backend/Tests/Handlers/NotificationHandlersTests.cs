using Application.Abstractions.Caching;
using Application.Abstractions.Notifications;
using Application.Abstractions.Persistence;
using Application.Abstractions.Repositories;
using Application.Common;
using Application.CQRS.Notifications;
using Application.DTOs.Notification;
using AutoMapper;
using Domain.Entities;
using Domain.Enums;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Tests.Handlers;

public class NotificationHandlersTests
{
    private readonly Mock<ICurrentUserContext> _currentUserContextMock;
    private readonly Mock<INotificationPreferenceRepository> _notificationPreferenceRepositoryMock;
    private readonly Mock<INotificationHistoryRepository> _notificationHistoryRepositoryMock;
    private readonly Mock<IWorkspaceMemberRepository> _workspaceMemberRepositoryMock;
    private readonly Mock<IRealtimeNotificationService> _realtimeNotificationServiceMock;
    private readonly Mock<IEmailService> _emailServiceMock;
    private readonly Mock<IPushNotificationService> _pushNotificationServiceMock;
    private readonly Mock<IApplicationUserRepository> _applicationUserRepositoryMock;
    private readonly Mock<ICacheService> _cacheServiceMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IMapper> _mapperMock;

    public NotificationHandlersTests()
    {
        _currentUserContextMock = new Mock<ICurrentUserContext>();
        _notificationPreferenceRepositoryMock = new Mock<INotificationPreferenceRepository>();
        _notificationHistoryRepositoryMock = new Mock<INotificationHistoryRepository>();
        _workspaceMemberRepositoryMock = new Mock<IWorkspaceMemberRepository>();
        _realtimeNotificationServiceMock = new Mock<IRealtimeNotificationService>();
        _emailServiceMock = new Mock<IEmailService>();
        _pushNotificationServiceMock = new Mock<IPushNotificationService>();
        _applicationUserRepositoryMock = new Mock<IApplicationUserRepository>();
        _cacheServiceMock = new Mock<ICacheService>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _mapperMock = new Mock<IMapper>();
    }

    [Fact]
    public async Task GetNotificationPreferences_ShouldReturnFromCache_WhenCached()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        _currentUserContextMock.Setup(c => c.UserId).Returns(userId);
        _currentUserContextMock.Setup(c => c.WorkspaceId).Returns(workspaceId);

        var member = WorkspaceMember.CreateOwner(workspaceId, userId, "owner@test.com", DateTime.UtcNow);
        _workspaceMemberRepositoryMock
            .Setup(r => r.GetByUserAndWorkspaceAsync(userId, workspaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(member);

        var cachedPreferences = new List<NotificationPreferenceDto>
        {
            new NotificationPreferenceDto(Guid.NewGuid(), "PostPublished", "Description", true, true, false)
        };

        _cacheServiceMock
            .Setup(c => c.GetAsync<IReadOnlyList<NotificationPreferenceDto>>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(cachedPreferences);

        var handler = new GetNotificationPreferencesQueryHandler(
            _currentUserContextMock.Object,
            _notificationPreferenceRepositoryMock.Object,
            _workspaceMemberRepositoryMock.Object,
            _cacheServiceMock.Object,
            _mapperMock.Object,
            new Mock<ILogger<GetNotificationPreferencesQueryHandler>>().Object);

        var query = new GetNotificationPreferencesQuery();

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        _notificationPreferenceRepositoryMock.Verify(r => r.GetByUserAndWorkspaceAsync(userId, workspaceId, It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateNotificationPreference_ShouldSucceed_WhenRequestIsValid()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        _currentUserContextMock.Setup(c => c.UserId).Returns(userId);
        _currentUserContextMock.Setup(c => c.WorkspaceId).Returns(workspaceId);

        var member = WorkspaceMember.CreateOwner(workspaceId, userId, "owner@test.com", DateTime.UtcNow);
        _workspaceMemberRepositoryMock
            .Setup(r => r.GetByUserAndWorkspaceAsync(userId, workspaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(member);

        var preference = NotificationPreference.Create(userId, workspaceId, NotificationEventType.PostPublished, true, true, false);
        _notificationPreferenceRepositoryMock
            .Setup(r => r.GetByUserWorkspaceAndEventAsync(userId, workspaceId, NotificationEventType.PostPublished, It.IsAny<CancellationToken>()))
            .ReturnsAsync(preference);

        var expectedDto = new NotificationPreferenceDto(preference.Id, "PostPublished", "Description", true, false, true);
        _mapperMock
            .Setup(m => m.Map<NotificationPreferenceDto>(It.IsAny<NotificationPreference>()))
            .Returns(expectedDto);

        var handler = new UpdateNotificationPreferenceCommandHandler(
            _currentUserContextMock.Object,
            _notificationPreferenceRepositoryMock.Object,
            _workspaceMemberRepositoryMock.Object,
            _unitOfWorkMock.Object,
            _cacheServiceMock.Object,
            _mapperMock.Object,
            new Mock<ILogger<UpdateNotificationPreferenceCommandHandler>>().Object);

        var command = new UpdateNotificationPreferenceCommand(new UpdateNotificationPreferenceRequest(NotificationEventType.PostPublished, true, false, true));

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.InAppEnabled.Should().BeTrue();
        result.Value!.EmailEnabled.Should().BeFalse();
        result.Value!.PushEnabled.Should().BeTrue();

        _notificationPreferenceRepositoryMock.Verify(r => r.Update(preference), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendNotification_ShouldDispatchCorrectly_WhenUserExists()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var user = ApplicationUser.Create("recipient@test.com", "Recipient", DateTime.UtcNow);
        user.Id = userId;

        _applicationUserRepositoryMock
            .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var preference = NotificationPreference.Create(userId, workspaceId, NotificationEventType.PostPublished, true, false, false);
        _notificationPreferenceRepositoryMock
            .Setup(r => r.GetByUserWorkspaceAndEventAsync(userId, workspaceId, NotificationEventType.PostPublished, It.IsAny<CancellationToken>()))
            .ReturnsAsync(preference);

        var handler = new SendNotificationCommandHandler(
            _notificationPreferenceRepositoryMock.Object,
            _notificationHistoryRepositoryMock.Object,
            _realtimeNotificationServiceMock.Object,
            _emailServiceMock.Object,
            _pushNotificationServiceMock.Object,
            _applicationUserRepositoryMock.Object,
            _unitOfWorkMock.Object,
            new Mock<ILogger<SendNotificationCommandHandler>>().Object);

        var command = new SendNotificationCommand(userId, workspaceId, NotificationEventType.PostPublished, "Title", "Body", "http://action");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _notificationHistoryRepositoryMock.Verify(r => r.AddAsync(It.IsAny<NotificationHistory>(), It.IsAny<CancellationToken>()), Times.Once);
        _realtimeNotificationServiceMock.Verify(s => s.NotifyUserAsync(userId, "notification.received", It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
