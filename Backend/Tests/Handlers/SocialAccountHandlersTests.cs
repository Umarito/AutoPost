using Application.Abstractions.BackgroundJobs;
using Application.Abstractions.Caching;
using Application.Abstractions.Integrations;
using Application.Abstractions.Persistence;
using Application.Abstractions.Repositories;
using Application.Abstractions.Security;
using Application.Common;
using Application.CQRS.SocialAccounts;
using Application.DTOs.SocialAccount;
using AutoMapper;
using Domain.Entities;
using Domain.Enums;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Tests.Handlers;

public class SocialAccountHandlersTests
{
    private readonly Mock<ICurrentUserContext> _currentUserContextMock;
    private readonly Mock<IWorkspaceRepository> _workspaceRepositoryMock;
    private readonly Mock<IWorkspaceMemberRepository> _workspaceMemberRepositoryMock;
    private readonly Mock<ISocialAccountRepository> _socialAccountRepositoryMock;
    private readonly Mock<ISocialAccountInsightRepository> _socialAccountInsightRepositoryMock;
    private readonly Mock<IPlatformIntegrationService> _platformIntegrationServiceMock;
    private readonly Mock<ITokenProtectionService> _tokenProtectionServiceMock;
    private readonly Mock<IPlatformTokenValidationService> _platformTokenValidationServiceMock;
    private readonly Mock<ICacheService> _cacheServiceMock;
    private readonly Mock<IBackgroundJobScheduler> _backgroundJobSchedulerMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IMapper> _mapperMock;

    public SocialAccountHandlersTests()
    {
        _currentUserContextMock = new Mock<ICurrentUserContext>();
        _workspaceRepositoryMock = new Mock<IWorkspaceRepository>();
        _workspaceMemberRepositoryMock = new Mock<IWorkspaceMemberRepository>();
        _socialAccountRepositoryMock = new Mock<ISocialAccountRepository>();
        _socialAccountInsightRepositoryMock = new Mock<ISocialAccountInsightRepository>();
        _platformIntegrationServiceMock = new Mock<IPlatformIntegrationService>();
        _tokenProtectionServiceMock = new Mock<ITokenProtectionService>();
        _platformTokenValidationServiceMock = new Mock<IPlatformTokenValidationService>();
        _cacheServiceMock = new Mock<ICacheService>();
        _backgroundJobSchedulerMock = new Mock<IBackgroundJobScheduler>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _mapperMock = new Mock<IMapper>();
    }

    [Fact]
    public async Task GetOAuthUrl_ShouldSucceed_WhenUserHasManagementAccess()
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

        _tokenProtectionServiceMock
            .Setup(s => s.Protect(It.IsAny<string>(), It.IsAny<string>()))
            .Returns("protected-state");

        _platformIntegrationServiceMock
            .Setup(s => s.BuildAuthorizationUrl(Platform.Instagram, It.IsAny<string>(), It.IsAny<string>()))
            .Returns("https://instagram.auth.url");

        var handler = new GetOAuthUrlQueryHandler(
            _currentUserContextMock.Object,
            _workspaceMemberRepositoryMock.Object,
            _platformIntegrationServiceMock.Object,
            _tokenProtectionServiceMock.Object,
            new Mock<ILogger<GetOAuthUrlQueryHandler>>().Object);

        var query = new GetOAuthUrlQuery(Platform.Instagram, "https://redirect.uri");

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Url.Should().Be("https://instagram.auth.url");
    }

    [Fact]
    public async Task DisconnectSocialAccount_ShouldSucceed_WhenAccountExistsAndUserIsManager()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var accountId = Guid.NewGuid();

        _currentUserContextMock.Setup(c => c.UserId).Returns(userId);

        var account = new SocialAccount();
        typeof(SocialAccount).GetProperty("Id")!.SetValue(account, accountId);
        typeof(SocialAccount).GetProperty("WorkspaceId")!.SetValue(account, workspaceId);
        typeof(SocialAccount).GetProperty("Status")!.SetValue(account, SocialAccountStatus.Active);

        _socialAccountRepositoryMock
            .Setup(r => r.GetByIdAsync(accountId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        var member = WorkspaceMember.CreateOwner(workspaceId, userId, "owner@test.com", DateTime.UtcNow);
        _workspaceMemberRepositoryMock
            .Setup(r => r.GetByUserAndWorkspaceAsync(userId, workspaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(member);

        var handler = new DisconnectSocialAccountCommandHandler(
            _currentUserContextMock.Object,
            _socialAccountRepositoryMock.Object,
            _workspaceMemberRepositoryMock.Object,
            _unitOfWorkMock.Object,
            _cacheServiceMock.Object,
            new Mock<ILogger<DisconnectSocialAccountCommandHandler>>().Object);

        var command = new DisconnectSocialAccountCommand(accountId);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        account.Status.Should().Be(SocialAccountStatus.Disconnected);
        _socialAccountRepositoryMock.Verify(r => r.Update(account), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EnsureTokenValid_ShouldSucceed_WhenTokenIsValid()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var account = new SocialAccount();
        typeof(SocialAccount).GetProperty("Id")!.SetValue(account, accountId);
        typeof(SocialAccount).GetProperty("WorkspaceId")!.SetValue(account, Guid.NewGuid());

        _socialAccountRepositoryMock
            .Setup(r => r.GetByIdAsync(accountId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        _currentUserContextMock.Setup(c => c.UserId).Returns(Guid.Empty); // System context

        var validationResult = new PlatformTokenValidationResult(true, false, DateTime.UtcNow.AddDays(30), null);
        _platformTokenValidationServiceMock
            .Setup(s => s.EnsureValidAsync(account, It.IsAny<CancellationToken>()))
            .ReturnsAsync(validationResult);

        var handler = new EnsureTokenValidCommandHandler(
            _currentUserContextMock.Object,
            _socialAccountRepositoryMock.Object,
            _workspaceMemberRepositoryMock.Object,
            _platformTokenValidationServiceMock.Object,
            _unitOfWorkMock.Object,
            new Mock<ILogger<EnsureTokenValidCommandHandler>>().Object);

        var command = new EnsureTokenValidCommand(accountId);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetSocialAccounts_ShouldReturnFromCache_WhenCached()
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

        var cachedAccounts = new List<SocialAccountDto>
        {
            new SocialAccountDto(Guid.NewGuid(), "Instagram", "ext-123", "Display Name", "username", "http://avatar", "Active", false, 100, new[] { "read" }, DateTime.UtcNow)
        };

        _cacheServiceMock
            .Setup(c => c.GetAsync<IReadOnlyList<SocialAccountDto>>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(cachedAccounts);

        var handler = new GetSocialAccountsQueryHandler(
            _currentUserContextMock.Object,
            _socialAccountRepositoryMock.Object,
            _workspaceMemberRepositoryMock.Object,
            _cacheServiceMock.Object,
            _mapperMock.Object,
            new Mock<ILogger<GetSocialAccountsQueryHandler>>().Object);

        var query = new GetSocialAccountsQuery();

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        _socialAccountRepositoryMock.Verify(r => r.GetByWorkspaceIdAsync(workspaceId, It.IsAny<CancellationToken>()), Times.Never);
    }
}
