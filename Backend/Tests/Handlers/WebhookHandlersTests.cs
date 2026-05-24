using Application.Abstractions.Persistence;
using Application.Abstractions.Repositories;
using Application.Abstractions.Webhooks;
using Application.Common;
using Application.CQRS.Webhooks;
using Application.DTOs.Webhook;
using AutoMapper;
using Domain.Entities;
using Domain.Enums;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Tests.Handlers;

public class WebhookHandlersTests
{
    private readonly Mock<IWebhookEventRepository> _webhookEventRepositoryMock;
    private readonly Mock<IWebhookSignatureVerifier> _signatureVerifierMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<ICurrentUserContext> _currentUserContextMock;
    private readonly Mock<IWorkspaceMemberRepository> _workspaceMemberRepositoryMock;
    private readonly Mock<IMapper> _mapperMock;

    public WebhookHandlersTests()
    {
        _webhookEventRepositoryMock = new Mock<IWebhookEventRepository>();
        _signatureVerifierMock = new Mock<IWebhookSignatureVerifier>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _currentUserContextMock = new Mock<ICurrentUserContext>();
        _workspaceMemberRepositoryMock = new Mock<IWorkspaceMemberRepository>();
        _mapperMock = new Mock<IMapper>();
    }

    [Fact]
    public async Task ReceiveWebhook_ShouldSucceed_WhenSignatureIsVerified()
    {
        // Arrange
        _signatureVerifierMock
            .Setup(s => s.Verify(Platform.Instagram, "raw-payload", "sig-123"))
            .Returns(true);

        var handler = new ReceiveWebhookCommandHandler(
            _webhookEventRepositoryMock.Object,
            _signatureVerifierMock.Object,
            _unitOfWorkMock.Object,
            new Mock<ILogger<ReceiveWebhookCommandHandler>>().Object);

        var command = new ReceiveWebhookCommand("Instagram", "NewComment", "raw-payload", "sig-123");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();

        _webhookEventRepositoryMock.Verify(r => r.AddAsync(It.IsAny<WebhookEvent>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task VerifyWebhookSubscription_ShouldSucceed_WhenTokenIsValid()
    {
        // Arrange
        _signatureVerifierMock
            .Setup(s => s.Verify(Platform.Instagram, "challenge-123", "token-123"))
            .Returns(true);

        var handler = new VerifyWebhookSubscriptionQueryHandler(
            _signatureVerifierMock.Object,
            new Mock<ILogger<VerifyWebhookSubscriptionQueryHandler>>().Object);

        var query = new VerifyWebhookSubscriptionQuery("Instagram", "challenge-123", "token-123");

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("challenge-123");
    }
}
