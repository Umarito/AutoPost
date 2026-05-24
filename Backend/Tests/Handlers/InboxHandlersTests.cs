using Application.Abstractions.Persistence;
using Application.Abstractions.Repositories;
using Application.Common;
using Application.CQRS.Inbox;
using Application.DTOs.Inbox;
using AutoMapper;
using Domain.Entities;
using Domain.Enums;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Tests.Handlers;

public class InboxHandlersTests
{
    private readonly Mock<ICurrentUserContext> _currentUserContextMock;
    private readonly Mock<ISocialAccountRepository> _socialAccountRepositoryMock;
    private readonly Mock<IInboxConversationRepository> _inboxConversationRepositoryMock;
    private readonly Mock<IConversationAssignmentRepository> _conversationAssignmentRepositoryMock;
    private readonly Mock<IWorkspaceMemberRepository> _workspaceMemberRepositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IMapper> _mapperMock;

    public InboxHandlersTests()
    {
        _currentUserContextMock = new Mock<ICurrentUserContext>();
        _socialAccountRepositoryMock = new Mock<ISocialAccountRepository>();
        _inboxConversationRepositoryMock = new Mock<IInboxConversationRepository>();
        _conversationAssignmentRepositoryMock = new Mock<IConversationAssignmentRepository>();
        _workspaceMemberRepositoryMock = new Mock<IWorkspaceMemberRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _mapperMock = new Mock<IMapper>();
    }

    [Fact]
    public async Task CreateOrUpdateConversation_ShouldSucceed_WhenSocialAccountExists()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var socialAccountId = Guid.NewGuid();

        _currentUserContextMock.Setup(c => c.UserId).Returns(userId);

        var socialAccount = new SocialAccount();
        typeof(SocialAccount).GetProperty("Id")!.SetValue(socialAccount, socialAccountId);
        typeof(SocialAccount).GetProperty("WorkspaceId")!.SetValue(socialAccount, workspaceId);

        _socialAccountRepositoryMock
            .Setup(r => r.GetByIdWithWorkspaceAsync(socialAccountId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(socialAccount);

        var member = WorkspaceMember.CreateOwner(workspaceId, userId, "owner@test.com", DateTime.UtcNow);
        _workspaceMemberRepositoryMock
            .Setup(r => r.GetByUserAndWorkspaceAsync(userId, workspaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(member);

        _inboxConversationRepositoryMock
            .Setup(r => r.GetByExternalIdAsync(socialAccountId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((InboxConversation?)null);

        var conversation = InboxConversation.Create(workspaceId, socialAccountId, ConversationType.DirectMessage, "ext-conv-123", "ext-user-123", "External User", null, DateTime.UtcNow);
        var expectedDto = new ConversationDetailDto(conversation.Id, "Instagram", "DirectMessage", "ext-user-123", "External User", null, false, "Open", 0, null, new List<MessageDto>());

        _mapperMock
            .Setup(m => m.Map<ConversationDetailDto>(It.IsAny<InboxConversation>()))
            .Returns(expectedDto);

        var handler = new CreateOrUpdateConversationCommandHandler(
            _currentUserContextMock.Object,
            _socialAccountRepositoryMock.Object,
            _inboxConversationRepositoryMock.Object,
            _workspaceMemberRepositoryMock.Object,
            _unitOfWorkMock.Object,
            _mapperMock.Object);

        var command = new CreateOrUpdateConversationCommand(socialAccountId, "ext-conv-123", "Instagram", "ext-user-123", "External User", "DirectMessage");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Platform.Should().Be("Instagram");

        _inboxConversationRepositoryMock.Verify(r => r.AddAsync(It.IsAny<InboxConversation>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ChangeConversationStatus_ShouldSucceed_WhenConversationExists()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var conversationId = Guid.NewGuid();

        _currentUserContextMock.Setup(c => c.UserId).Returns(userId);

        var conversation = InboxConversation.Create(workspaceId, Guid.NewGuid(), ConversationType.DirectMessage, "ext-conv-123", "ext-user-123", "External User", null, DateTime.UtcNow);
        typeof(InboxConversation).GetProperty("Id")!.SetValue(conversation, conversationId);

        _inboxConversationRepositoryMock
            .Setup(r => r.GetByIdAsync(conversationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(conversation);

        var member = WorkspaceMember.CreateOwner(workspaceId, userId, "owner@test.com", DateTime.UtcNow);
        _workspaceMemberRepositoryMock
            .Setup(r => r.GetByUserAndWorkspaceAsync(userId, workspaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(member);

        var handler = new ChangeConversationStatusCommandHandler(
            _currentUserContextMock.Object,
            _inboxConversationRepositoryMock.Object,
            _workspaceMemberRepositoryMock.Object,
            _unitOfWorkMock.Object);

        var command = new ChangeConversationStatusCommand(conversationId, ConversationStatus.Resolved);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        conversation.Status.Should().Be(ConversationStatus.Resolved);
        _inboxConversationRepositoryMock.Verify(r => r.Update(conversation), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
