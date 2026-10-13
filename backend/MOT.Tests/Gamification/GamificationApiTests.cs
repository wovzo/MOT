using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using MOT.Api.Controllers;
using MOT.Application.Common.Exceptions;
using MOT.Application.Common.Interfaces;
using MOT.Application.Gamification.DTOs;
using MOT.Application.Gamification.Queries;
using MOT.Application.Validators;
using MOT.Domain.Entities;
using MOT.Domain.Gamification;
using MOT.Tests.Common;
using Xunit;

namespace MOT.Tests.Gamification;

public class GamificationApiTests
{
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _otherUserId = Guid.NewGuid();
    private readonly Mock<ICurrentUserService> _currentUserServiceMock;
    private readonly Mock<IAppDbContext> _dbContextMock;
    private readonly List<User> _users;
    private readonly List<UserAchievement> _userAchievements;
    private readonly List<XpTransaction> _xpTransactions;

    public GamificationApiTests()
    {
        _currentUserServiceMock = new Mock<ICurrentUserService>();
        _currentUserServiceMock.Setup(s => s.UserId).Returns(_userId.ToString());

        _users = new List<User>();
        _userAchievements = new List<UserAchievement>();
        _xpTransactions = new List<XpTransaction>();

        _dbContextMock = new Mock<IAppDbContext>();
        _dbContextMock.Setup(c => c.Users).Returns(MockDbSetHelper.CreateMockDbSet(_users).Object);
        _dbContextMock.Setup(c => c.UserAchievements).Returns(MockDbSetHelper.CreateMockDbSet(_userAchievements).Object);
        _dbContextMock.Setup(c => c.XpTransactions).Returns(MockDbSetHelper.CreateMockDbSet(_xpTransactions).Object);
    }

    private User CreateTestUser(Guid? id = null, int xp = 0, int level = 1)
    {
        var user = new User
        {
            Id = id ?? _userId,
            Email = $"user-{Guid.NewGuid()}@example.com",
            PasswordHash = "hashedPassword",
            DisplayName = "Test User",
            XP = xp,
            Level = level,
            CreatedAt = DateTime.UtcNow
        };
        _users.Add(user);
        return user;
    }

    // =========================================================================
    // 1. Gamification Summary Query Handler Tests
    // =========================================================================

    [Fact]
    public async Task Summary_ForNewUserWithZeroXp_ReturnsLevel1AndZeroProgress()
    {
        CreateTestUser(xp: 0, level: 1);

        var handler = new GetGamificationSummaryQueryHandler(_dbContextMock.Object, _currentUserServiceMock.Object);
        var result = await handler.Handle(new GetGamificationSummaryQuery(), CancellationToken.None);

        result.Level.Should().Be(1);
        result.TotalXp.Should().Be(0);
        result.CurrentLevelThresholdXp.Should().Be(0);
        result.NextLevelThresholdXp.Should().Be(100);
        result.XpEarnedInCurrentLevel.Should().Be(0);
        result.XpRequiredForNextLevel.Should().Be(100);
        result.ProgressFraction.Should().Be(0.0);
        result.ProgressPercentage.Should().Be(0.0);
        result.TotalAchievements.Should().Be(8);
        result.UnlockedAchievements.Should().Be(0);
    }

    [Fact]
    public async Task Summary_AtLevelBoundaries_CalculatesCorrectThresholds()
    {
        // 100 XP is exact boundary for Level 2 (50 * 2 * 1 = 100)
        CreateTestUser(xp: 100, level: 2);

        var handler = new GetGamificationSummaryQueryHandler(_dbContextMock.Object, _currentUserServiceMock.Object);
        var result = await handler.Handle(new GetGamificationSummaryQuery(), CancellationToken.None);

        result.Level.Should().Be(2);
        result.TotalXp.Should().Be(100);
        result.CurrentLevelThresholdXp.Should().Be(100);
        result.NextLevelThresholdXp.Should().Be(300); // 50 * 3 * 2 = 300
        result.XpEarnedInCurrentLevel.Should().Be(0);
        result.XpRequiredForNextLevel.Should().Be(200); // 100 * 2 = 200
        result.ProgressFraction.Should().Be(0.0);
        result.ProgressPercentage.Should().Be(0.0);
    }

    [Fact]
    public async Task Summary_WithPartialProgress_CalculatesCorrectDeltaAndPercentage()
    {
        // 250 XP is Level 2 with 150/200 progress (75%)
        CreateTestUser(xp: 250, level: 2);

        var handler = new GetGamificationSummaryQueryHandler(_dbContextMock.Object, _currentUserServiceMock.Object);
        var result = await handler.Handle(new GetGamificationSummaryQuery(), CancellationToken.None);

        result.Level.Should().Be(2);
        result.TotalXp.Should().Be(250);
        result.CurrentLevelThresholdXp.Should().Be(100);
        result.NextLevelThresholdXp.Should().Be(300);
        result.XpEarnedInCurrentLevel.Should().Be(150);
        result.XpRequiredForNextLevel.Should().Be(200);
        result.ProgressFraction.Should().Be(0.75);
        result.ProgressPercentage.Should().Be(75.0);
    }

    [Fact]
    public async Task Summary_WithUnlockedAchievements_CountsUserAchievementsAccurately()
    {
        CreateTestUser(xp: 150, level: 2);

        _userAchievements.Add(new UserAchievement { Id = Guid.NewGuid(), UserId = _userId, AchievementCode = AchievementCodes.FirstStep, UnlockedAt = DateTime.UtcNow });
        _userAchievements.Add(new UserAchievement { Id = Guid.NewGuid(), UserId = _userId, AchievementCode = AchievementCodes.FocusInitiate, UnlockedAt = DateTime.UtcNow });
        _userAchievements.Add(new UserAchievement { Id = Guid.NewGuid(), UserId = _userId, AchievementCode = AchievementCodes.HabitStarter, UnlockedAt = DateTime.UtcNow });

        var handler = new GetGamificationSummaryQueryHandler(_dbContextMock.Object, _currentUserServiceMock.Object);
        var result = await handler.Handle(new GetGamificationSummaryQuery(), CancellationToken.None);

        result.TotalAchievements.Should().Be(8);
        result.UnlockedAchievements.Should().Be(3);
    }

    [Fact]
    public async Task Summary_UserScoping_ExcludesOtherUsersAchievements()
    {
        CreateTestUser(_userId, xp: 50, level: 1);
        CreateTestUser(_otherUserId, xp: 500, level: 3);

        _userAchievements.Add(new UserAchievement { Id = Guid.NewGuid(), UserId = _userId, AchievementCode = AchievementCodes.FirstStep, UnlockedAt = DateTime.UtcNow });
        _userAchievements.Add(new UserAchievement { Id = Guid.NewGuid(), UserId = _otherUserId, AchievementCode = AchievementCodes.FirstStep, UnlockedAt = DateTime.UtcNow });
        _userAchievements.Add(new UserAchievement { Id = Guid.NewGuid(), UserId = _otherUserId, AchievementCode = AchievementCodes.DeepDiver, UnlockedAt = DateTime.UtcNow });

        var handler = new GetGamificationSummaryQueryHandler(_dbContextMock.Object, _currentUserServiceMock.Object);
        var result = await handler.Handle(new GetGamificationSummaryQuery(), CancellationToken.None);

        result.UnlockedAchievements.Should().Be(1);
    }

    [Fact]
    public async Task Summary_WhenUserDoesNotExist_ThrowsUserNotFoundException()
    {
        var handler = new GetGamificationSummaryQueryHandler(_dbContextMock.Object, _currentUserServiceMock.Object);
        Func<Task> act = async () => await handler.Handle(new GetGamificationSummaryQuery(), CancellationToken.None);

        await act.Should().ThrowAsync<UserNotFoundException>();
    }

    [Fact]
    public async Task Summary_WhenUnauthenticated_ThrowsUnauthorizedAccessException()
    {
        _currentUserServiceMock.Setup(s => s.UserId).Returns((string?)null);

        var handler = new GetGamificationSummaryQueryHandler(_dbContextMock.Object, _currentUserServiceMock.Object);
        Func<Task> act = async () => await handler.Handle(new GetGamificationSummaryQuery(), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task Summary_ReadRequest_NeverMutatesStateOrCallsSaveChanges()
    {
        var user = CreateTestUser(xp: 75, level: 1);

        var handler = new GetGamificationSummaryQueryHandler(_dbContextMock.Object, _currentUserServiceMock.Object);
        await handler.Handle(new GetGamificationSummaryQuery(), CancellationToken.None);

        _dbContextMock.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        user.XP.Should().Be(75);
        user.Level.Should().Be(1);
    }

    // =========================================================================
    // 2. Achievements Query Handler Tests
    // =========================================================================

    [Fact]
    public async Task Achievements_ReturnsCompleteCatalog_IncludingLockedAndUnlocked()
    {
        CreateTestUser();
        var unlockTime = DateTime.UtcNow.AddHours(-2);

        _userAchievements.Add(new UserAchievement
        {
            Id = Guid.NewGuid(),
            UserId = _userId,
            AchievementCode = AchievementCodes.FirstStep,
            UnlockedAt = unlockTime,
            XpAwarded = 25
        });

        var handler = new GetAchievementsQueryHandler(_dbContextMock.Object, _currentUserServiceMock.Object);
        var result = await handler.Handle(new GetAchievementsQuery(), CancellationToken.None);

        result.Should().HaveCount(8);

        var firstStep = result.Single(a => a.Code == AchievementCodes.FirstStep);
        firstStep.IsUnlocked.Should().BeTrue();
        firstStep.UnlockedAt.Should().Be(unlockTime);
        firstStep.Title.Should().Be("First Step");
        firstStep.XpReward.Should().Be(25);

        var focusInitiate = result.Single(a => a.Code == AchievementCodes.FocusInitiate);
        focusInitiate.IsUnlocked.Should().BeFalse();
        focusInitiate.UnlockedAt.Should().BeNull();
        focusInitiate.Title.Should().Be("Focus Initiate");
        focusInitiate.XpReward.Should().Be(25);
    }

    [Fact]
    public async Task Achievements_ForNewUserWithNoUnlocks_ReturnsAllAchievementsAsLocked()
    {
        CreateTestUser();

        var handler = new GetAchievementsQueryHandler(_dbContextMock.Object, _currentUserServiceMock.Object);
        var result = await handler.Handle(new GetAchievementsQuery(), CancellationToken.None);

        result.Should().HaveCount(8);
        result.All(a => !a.IsUnlocked && a.UnlockedAt == null).Should().BeTrue();
    }

    [Fact]
    public async Task Achievements_UserScoping_DoesNotIncludeOtherUsersUnlocks()
    {
        CreateTestUser(_userId);
        CreateTestUser(_otherUserId);

        _userAchievements.Add(new UserAchievement
        {
            Id = Guid.NewGuid(),
            UserId = _otherUserId,
            AchievementCode = AchievementCodes.CenturyClub,
            UnlockedAt = DateTime.UtcNow,
            XpAwarded = 50
        });

        var handler = new GetAchievementsQueryHandler(_dbContextMock.Object, _currentUserServiceMock.Object);
        var result = await handler.Handle(new GetAchievementsQuery(), CancellationToken.None);

        var centuryClub = result.Single(a => a.Code == AchievementCodes.CenturyClub);
        centuryClub.IsUnlocked.Should().BeFalse();
        centuryClub.UnlockedAt.Should().BeNull();
    }

    [Fact]
    public async Task Achievements_WhenUserNotFound_ThrowsUserNotFoundException()
    {
        var handler = new GetAchievementsQueryHandler(_dbContextMock.Object, _currentUserServiceMock.Object);
        Func<Task> act = async () => await handler.Handle(new GetAchievementsQuery(), CancellationToken.None);

        await act.Should().ThrowAsync<UserNotFoundException>();
    }

    [Fact]
    public async Task Achievements_WhenUnauthenticated_ThrowsUnauthorizedAccessException()
    {
        _currentUserServiceMock.Setup(s => s.UserId).Returns((string?)null);

        var handler = new GetAchievementsQueryHandler(_dbContextMock.Object, _currentUserServiceMock.Object);
        Func<Task> act = async () => await handler.Handle(new GetAchievementsQuery(), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task Achievements_ReadRequest_NeverMutatesStateOrCallsSaveChanges()
    {
        CreateTestUser();

        var handler = new GetAchievementsQueryHandler(_dbContextMock.Object, _currentUserServiceMock.Object);
        await handler.Handle(new GetAchievementsQuery(), CancellationToken.None);

        _dbContextMock.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _userAchievements.Should().BeEmpty();
    }

    // =========================================================================
    // 3. XP History Query Handler Tests
    // =========================================================================

    [Fact]
    public async Task History_WhenEmpty_ReturnsEmptyList()
    {
        CreateTestUser();

        var handler = new GetXpHistoryQueryHandler(_dbContextMock.Object, _currentUserServiceMock.Object);
        var result = await handler.Handle(new GetXpHistoryQuery { Limit = 20 }, CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task History_Ordering_ReturnsNewestFirst()
    {
        CreateTestUser();
        var now = DateTime.UtcNow;

        _xpTransactions.Add(new XpTransaction { Id = Guid.NewGuid(), UserId = _userId, Amount = 10, SourceType = XpSourceTypes.Task, Description = "Oldest", CreatedAt = now.AddMinutes(-30) });
        _xpTransactions.Add(new XpTransaction { Id = Guid.NewGuid(), UserId = _userId, Amount = 20, SourceType = XpSourceTypes.Habit, Description = "Middle", CreatedAt = now.AddMinutes(-15) });
        _xpTransactions.Add(new XpTransaction { Id = Guid.NewGuid(), UserId = _userId, Amount = 35, SourceType = XpSourceTypes.StudySession, Description = "Newest", CreatedAt = now });

        var handler = new GetXpHistoryQueryHandler(_dbContextMock.Object, _currentUserServiceMock.Object);
        var result = await handler.Handle(new GetXpHistoryQuery { Limit = 20 }, CancellationToken.None);

        result.Should().HaveCount(3);
        result[0].Description.Should().Be("Newest");
        result[1].Description.Should().Be("Middle");
        result[2].Description.Should().Be("Oldest");
    }

    [Fact]
    public async Task History_Limit_RespectsRequestedLimit()
    {
        CreateTestUser();

        for (int i = 0; i < 10; i++)
        {
            _xpTransactions.Add(new XpTransaction
            {
                Id = Guid.NewGuid(),
                UserId = _userId,
                Amount = 15,
                SourceType = XpSourceTypes.Task,
                Description = $"Task {i}",
                CreatedAt = DateTime.UtcNow.AddMinutes(i)
            });
        }

        var handler = new GetXpHistoryQueryHandler(_dbContextMock.Object, _currentUserServiceMock.Object);
        var result = await handler.Handle(new GetXpHistoryQuery { Limit = 5 }, CancellationToken.None);

        result.Should().HaveCount(5);
        result[0].Description.Should().Be("Task 9");
    }

    [Fact]
    public void History_DefaultLimit_IsTwenty()
    {
        var query = new GetXpHistoryQuery();
        query.Limit.Should().Be(20);
    }

    [Fact]
    public async Task History_UserScoping_ExcludesOtherUsersTransactions()
    {
        CreateTestUser(_userId);
        CreateTestUser(_otherUserId);

        _xpTransactions.Add(new XpTransaction { Id = Guid.NewGuid(), UserId = _userId, Amount = 15, SourceType = XpSourceTypes.Task, Description = "User 1 Task", CreatedAt = DateTime.UtcNow });
        _xpTransactions.Add(new XpTransaction { Id = Guid.NewGuid(), UserId = _otherUserId, Amount = 50, SourceType = XpSourceTypes.StudySession, Description = "User 2 Session", CreatedAt = DateTime.UtcNow });

        var handler = new GetXpHistoryQueryHandler(_dbContextMock.Object, _currentUserServiceMock.Object);
        var result = await handler.Handle(new GetXpHistoryQuery { Limit = 20 }, CancellationToken.None);

        result.Should().HaveCount(1);
        result.Single().Description.Should().Be("User 1 Task");
    }

    [Fact]
    public async Task History_WhenUserNotFound_ThrowsUserNotFoundException()
    {
        var handler = new GetXpHistoryQueryHandler(_dbContextMock.Object, _currentUserServiceMock.Object);
        Func<Task> act = async () => await handler.Handle(new GetXpHistoryQuery(), CancellationToken.None);

        await act.Should().ThrowAsync<UserNotFoundException>();
    }

    [Fact]
    public async Task History_WhenUnauthenticated_ThrowsUnauthorizedAccessException()
    {
        _currentUserServiceMock.Setup(s => s.UserId).Returns((string?)null);

        var handler = new GetXpHistoryQueryHandler(_dbContextMock.Object, _currentUserServiceMock.Object);
        Func<Task> act = async () => await handler.Handle(new GetXpHistoryQuery(), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task History_ReadRequest_NeverCallsSaveChanges()
    {
        CreateTestUser();

        var handler = new GetXpHistoryQueryHandler(_dbContextMock.Object, _currentUserServiceMock.Object);
        await handler.Handle(new GetXpHistoryQuery { Limit = 20 }, CancellationToken.None);

        _dbContextMock.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    // =========================================================================
    // 4. Validator Tests
    // =========================================================================

    [Theory]
    [InlineData(0, false)]
    [InlineData(-1, false)]
    [InlineData(-50, false)]
    [InlineData(101, false)]
    [InlineData(200, false)]
    [InlineData(1, true)]
    [InlineData(20, true)]
    [InlineData(50, true)]
    [InlineData(100, true)]
    public void HistoryValidator_ValidatesLimitsCorrectly(int limit, bool expectedIsValid)
    {
        var validator = new GetXpHistoryQueryValidator();
        var query = new GetXpHistoryQuery { Limit = limit };

        var validationResult = validator.Validate(query);

        validationResult.IsValid.Should().Be(expectedIsValid);
        if (!expectedIsValid)
        {
            validationResult.Errors.Should().Contain(e => e.PropertyName == "Limit");
        }
    }

    // =========================================================================
    // 5. Controller HTTP Semantics Tests
    // =========================================================================

    [Fact]
    public async Task Controller_GetSummary_Success_Returns200WithDto()
    {
        var mediatorMock = new Mock<IMediator>();
        var loggerMock = new Mock<ILogger<GamificationController>>();
        var expected = new GamificationSummaryDto(2, 250, 100, 300, 150, 200, 0.75, 75.0, 8, 2);

        mediatorMock.Setup(m => m.Send(It.IsAny<GetGamificationSummaryQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var controller = new GamificationController(mediatorMock.Object, loggerMock.Object);
        var result = await controller.GetSummary(CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(200);
        okResult.Value.Should().Be(expected);
    }

    [Fact]
    public async Task Controller_GetSummary_UserNotFound_Returns404()
    {
        var mediatorMock = new Mock<IMediator>();
        var loggerMock = new Mock<ILogger<GamificationController>>();

        mediatorMock.Setup(m => m.Send(It.IsAny<GetGamificationSummaryQuery>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UserNotFoundException(_userId));

        var controller = new GamificationController(mediatorMock.Object, loggerMock.Object);
        var result = await controller.GetSummary(CancellationToken.None);

        var notFoundResult = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        notFoundResult.StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task Controller_GetSummary_Unauthenticated_Returns401()
    {
        var mediatorMock = new Mock<IMediator>();
        var loggerMock = new Mock<ILogger<GamificationController>>();

        mediatorMock.Setup(m => m.Send(It.IsAny<GetGamificationSummaryQuery>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UnauthorizedAccessException());

        var controller = new GamificationController(mediatorMock.Object, loggerMock.Object);
        var result = await controller.GetSummary(CancellationToken.None);

        var unauthResult = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        unauthResult.StatusCode.Should().Be(401);
    }

    [Fact]
    public async Task Controller_GetSummary_UnhandledException_Returns500()
    {
        var mediatorMock = new Mock<IMediator>();
        var loggerMock = new Mock<ILogger<GamificationController>>();

        mediatorMock.Setup(m => m.Send(It.IsAny<GetGamificationSummaryQuery>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database connection failure"));

        var controller = new GamificationController(mediatorMock.Object, loggerMock.Object);
        var result = await controller.GetSummary(CancellationToken.None);

        var serverErrorResult = result.Should().BeOfType<ObjectResult>().Subject;
        serverErrorResult.StatusCode.Should().Be(500);
    }

    [Fact]
    public async Task Controller_GetAchievements_Success_Returns200WithList()
    {
        var mediatorMock = new Mock<IMediator>();
        var loggerMock = new Mock<ILogger<GamificationController>>();
        var expected = new List<AchievementDto>
        {
            new AchievementDto(AchievementCodes.FirstStep, "First Step", "Complete first task", "check", 25, true, DateTime.UtcNow)
        };

        mediatorMock.Setup(m => m.Send(It.IsAny<GetAchievementsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var controller = new GamificationController(mediatorMock.Object, loggerMock.Object);
        var result = await controller.GetAchievements(CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(200);
        okResult.Value.Should().Be(expected);
    }

    [Fact]
    public async Task Controller_GetAchievements_UserNotFound_Returns404()
    {
        var mediatorMock = new Mock<IMediator>();
        var loggerMock = new Mock<ILogger<GamificationController>>();

        mediatorMock.Setup(m => m.Send(It.IsAny<GetAchievementsQuery>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UserNotFoundException(_userId));

        var controller = new GamificationController(mediatorMock.Object, loggerMock.Object);
        var result = await controller.GetAchievements(CancellationToken.None);

        var notFoundResult = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        notFoundResult.StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task Controller_GetHistory_Success_Returns200WithList()
    {
        var mediatorMock = new Mock<IMediator>();
        var loggerMock = new Mock<ILogger<GamificationController>>();
        var expected = new List<XpHistoryDto>
        {
            new XpHistoryDto(Guid.NewGuid(), 25, XpSourceTypes.Task, "Completed task", DateTime.UtcNow)
        };

        mediatorMock.Setup(m => m.Send(It.Is<GetXpHistoryQuery>(q => q.Limit == 10), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var controller = new GamificationController(mediatorMock.Object, loggerMock.Object);
        var result = await controller.GetHistory(limit: 10, CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(200);
        okResult.Value.Should().Be(expected);
    }

    [Fact]
    public async Task Controller_GetHistory_ValidationError_Returns400()
    {
        var mediatorMock = new Mock<IMediator>();
        var loggerMock = new Mock<ILogger<GamificationController>>();

        mediatorMock.Setup(m => m.Send(It.IsAny<GetXpHistoryQuery>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ValidationException(new[] { new ValidationFailure("Limit", "Limit must be between 1 and 100.") }));

        var controller = new GamificationController(mediatorMock.Object, loggerMock.Object);
        var result = await controller.GetHistory(limit: 0, CancellationToken.None);

        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.StatusCode.Should().Be(400);
    }

    [Fact]
    public void Controller_HasAuthorizeAndRouteAttributes_ConfiguredCorrectly()
    {
        var controllerType = typeof(GamificationController);

        var routeAttr = controllerType.GetCustomAttribute<RouteAttribute>();
        routeAttr.Should().NotBeNull();
        routeAttr!.Template.Should().Be("api/[controller]");

        var authAttr = controllerType.GetCustomAttribute<AuthorizeAttribute>();
        authAttr.Should().NotBeNull();
    }
}
