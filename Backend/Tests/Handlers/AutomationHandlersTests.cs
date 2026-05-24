using Application.Abstractions.Persistence;
using Application.Abstractions.Repositories;
using Application.Common;
using Application.CQRS.Automation;
using Application.DTOs.Automation;
using Application.DTOs.SocialAccount;
using AutoMapper;
using Domain.Entities;
using Domain.Enums;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Tests.Handlers;

public class AutomationHandlersTests
{
    private readonly Mock<ICurrentUserContext> _currentUserContextMock;
    private readonly Mock<IWorkspaceRepository> _workspaceRepositoryMock;
    private readonly Mock<ISocialAccountRepository> _socialAccountRepositoryMock;
    private readonly Mock<IAutomationRuleRepository> _automationRuleRepositoryMock;
    private readonly Mock<IWorkspaceMemberRepository> _workspaceMemberRepositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IMapper> _mapperMock;

    public AutomationHandlersTests()
    {
        _currentUserContextMock = new Mock<ICurrentUserContext>();
        _workspaceRepositoryMock = new Mock<IWorkspaceRepository>();
        _socialAccountRepositoryMock = new Mock<ISocialAccountRepository>();
        _automationRuleRepositoryMock = new Mock<IAutomationRuleRepository>();
        _workspaceMemberRepositoryMock = new Mock<IWorkspaceMemberRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _mapperMock = new Mock<IMapper>();
    }

    [Fact]
    public async Task CreateAutomationRule_ShouldSucceed_WhenPlanIsBusinessAndAccountExists()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var socialAccountId = Guid.NewGuid();

        _currentUserContextMock.Setup(c => c.UserId).Returns(userId);
        _currentUserContextMock.Setup(c => c.WorkspaceId).Returns(workspaceId);

        var member = WorkspaceMember.CreateOwner(workspaceId, userId, "owner@test.com", DateTime.UtcNow);
        _workspaceMemberRepositoryMock
            .Setup(r => r.GetByUserAndWorkspaceAsync(userId, workspaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(member);

        var workspace = new Workspace();
        typeof(Workspace).GetProperty("Id")!.SetValue(workspace, workspaceId);
        typeof(Workspace).GetProperty("Plan")!.SetValue(workspace, SubscriptionPlan.Business);

        _workspaceRepositoryMock
            .Setup(r => r.GetByIdAsync(workspaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(workspace);

        var socialAccount = new SocialAccount();
        typeof(SocialAccount).GetProperty("Id")!.SetValue(socialAccount, socialAccountId);
        typeof(SocialAccount).GetProperty("WorkspaceId")!.SetValue(socialAccount, workspaceId);

        _socialAccountRepositoryMock
            .Setup(r => r.GetByIdAsync(socialAccountId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(socialAccount);

        var conditions = new List<ConditionRequest>
        {
            new ConditionRequest(ConditionType.CommentText, ConditionOperator.Contains, "promo", false)
        };
        var actions = new List<ActionRequest>
        {
            new ActionRequest(ActionType.SendDirectMessage, 1, 30, "Hello", null)
        };
        var requestDto = new CreateAutomationRuleRequest(
            "Rule 1",
            "Desc",
            socialAccountId,
            AutomationTriggerType.NewComment,
            null,
            5,
            100,
            conditions,
            actions
        );

        var socialAccountDto = new SocialAccountDto(
            socialAccountId,
            "Instagram",
            "ext-123",
            "Display Name",
            "username",
            "http://avatar",
            "Active",
            false,
            100,
            new[] { "read" },
            DateTime.UtcNow
        );

        var expectedDto = new AutomationRuleDetailDto(
            Guid.NewGuid(),
            "Rule 1",
            "Desc",
            "Instagram",
            socialAccountDto,
            "NewComment",
            null,
            5,
            100,
            true,
            new List<ConditionDto>(),
            new List<ActionDto>(),
            DateTime.UtcNow
        );

        _mapperMock
            .Setup(m => m.Map<AutomationRuleDetailDto>(It.IsAny<AutomationRule>()))
            .Returns(expectedDto);

        var handler = new CreateAutomationRuleCommandHandler(
            _currentUserContextMock.Object,
            _workspaceRepositoryMock.Object,
            _socialAccountRepositoryMock.Object,
            _automationRuleRepositoryMock.Object,
            _workspaceMemberRepositoryMock.Object,
            _unitOfWorkMock.Object,
            _mapperMock.Object);

        var command = new CreateAutomationRuleCommand(requestDto);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Name.Should().Be("Rule 1");

        _automationRuleRepositoryMock.Verify(r => r.AddAsync(It.IsAny<AutomationRule>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
