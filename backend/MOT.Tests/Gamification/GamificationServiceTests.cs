using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;
using MOT.Application.Common;
using MOT.Application.Common.Exceptions;
using MOT.Application.Common.Interfaces;
using MOT.Application.Gamification;
using MOT.Application.Habits.Commands;
using MOT.Application.StudySessions;
using MOT.Application.Tasks.Commands;
using MOT.Domain.Entities;
using MOT.Domain.Gamification;
using MOT.Tests.Common;
using Xunit;

namespace MOT.Tests.Gamification;

public class GamificationServiceTests
{
    private readonly Mock<IAppDbContext> _dbContextMock;
    private readonly Mock<IAchievementService> _achievementServiceMock;
    private readonly List<User> _users;
    private readonly List<XpTransaction> _xpTransactions;
    private readonly List<DailyTask> _tasks;
    private readonly List<Habit> _habits;
    private readonly List<HabitCompletion> _habitCompletions;
    private readonly List<StudySession> _studySessions;
    private readonly List<UserAchievement> _userAchievements;
    private readonly GamificationOptions _options;
    private readonly GamificationService _gamificationService;

    public GamificationServiceTests()
    {
        _users = new List<User>();
        _xpTransactions = new List<XpTransaction>();
        _tasks = new List<DailyTask>();
        _habits = new List<Habit>();
        _habitCompletions = new List<HabitCompletion>();
        _studySessions = new List<StudySession>();
        _userAchievements = new List<UserAchievement>();
        _achievementServiceMock = new Mock<IAchievementService>();

        _dbContextMock = new Mock<IAppDbContext>();
        _dbContextMock.Setup(c => c.Users).Returns(MockDbSetHelper.CreateMockDbSet(_users).Object);
        _dbContextMock.Setup(c => c.XpTransactions).Returns(MockDbSetHelper.CreateMockDbSet(_xpTransactions).Object);
        _dbContextMock.Setup(c => c.Tasks).Returns(MockDbSetHelper.CreateMockDbSet(_tasks).Object);
        _dbContextMock.Setup(c => c.Habits).Returns(MockDbSetHelper.CreateMockDbSet(_habits).Object);
        _dbContextMock.Setup(c => c.HabitCompletions).Returns(MockDbSetHelper.CreateMockDbSet(_habitCompletions).Object);
        _dbContextMock.Setup(c => c.StudySessions).Returns(MockDbSetHelper.CreateMockDbSet(_studySessions).Object);
        _dbContextMock.Setup(c => c.UserAchievements).Returns(MockDbSetHelper.CreateMockDbSet(_userAchievements).Object);
        _dbContextMock.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        _options = new GamificationOptions
        {
            TaskCompletionXp = 15,
            HabitCompletionXp = 20,
            StudySessionMinMinutes = 5,
            StudySessionBaseXp = 10,
            StudySessionMinutesPerXp = 2,
            StudySessionMaxXp = 70
        };

        _gamificationService = new GamificationService(_dbContextMock.Object, Options.Create(_options));
    }

    private User CreateTestUser(int initialXp = 0)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = $"user-{Guid.NewGuid()}@example.com",
            PasswordHash = "hash",
            DisplayName = "Test User",
            XP = initialXp,
            Level = LevelCalculator.CalculateLevel(initialXp),
            CreatedAt = DateTime.UtcNow
        };
        _users.Add(user);
        return user;
    }

    // =========================================================================
    // 1. Study Session XP Formula Tests
    // =========================================================================

    [Theory]
    [InlineData(0, 0)]
    [InlineData(-10, 0)]
    [InlineData(4, 0)]
    [InlineData(5, 12)]   // 10 + 5/2 = 12
    [InlineData(25, 22)]  // 10 + 25/2 = 22
    [InlineData(50, 35)]  // 10 + 50/2 = 35
    [InlineData(120, 70)] // 10 + 120/2 = 70 (cap)
    [InlineData(180, 70)] // 10 + 180/2 = 100 -> capped at 70
    public void CalculateStudySessionXp_FollowsAuthoritativeFormulaAndCap(int durationMinutes, int expectedXp)
    {
        var xp = _gamificationService.CalculateStudySessionXp(durationMinutes);
        xp.Should().Be(expectedXp);
    }

    [Fact]
    public async Task AwardStudySessionXp_WhenEligible_StagesLedgerAndUpdatesUserBalance()
    {
        var user = CreateTestUser(initialXp: 50);
        var session = new StudySession
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Title = "Math Study",
            StartTime = DateTime.UtcNow.AddMinutes(-30),
            EndTime = DateTime.UtcNow,
            DurationMinutes = 25,
            IsCompleted = true
        };
        _studySessions.Add(session);

        var result = await _gamificationService.AwardStudySessionXpAsync(user.Id, session);

        result.Awarded.Should().BeTrue();
        result.Amount.Should().Be(22); // 10 + 25/2 = 22
        user.XP.Should().Be(72);
        user.Level.Should().Be(1);
        _xpTransactions.Should().HaveCount(1);
        var tx = _xpTransactions.Single();
        tx.UserId.Should().Be(user.Id);
        tx.SourceType.Should().Be(XpSourceTypes.StudySession);
        tx.SourceId.Should().Be(session.Id.ToString());
        tx.Amount.Should().Be(22);
    }

    [Fact]
    public async Task AwardStudySessionXp_WhenDurationLessThan5Min_ReturnsNotEligibleAndAwards0Xp()
    {
        var user = CreateTestUser(initialXp: 50);
        var session = new StudySession
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Title = "Quick check",
            StartTime = DateTime.UtcNow.AddMinutes(-3),
            EndTime = DateTime.UtcNow,
            DurationMinutes = 3,
            IsCompleted = true
        };

        var result = await _gamificationService.AwardStudySessionXpAsync(user.Id, session);

        result.Awarded.Should().BeFalse();
        result.Amount.Should().Be(0);
        user.XP.Should().Be(50);
        _xpTransactions.Should().BeEmpty();
    }

    [Fact]
    public async Task AwardStudySessionXp_WhenSessionNotCompleted_ReturnsNotEligible()
    {
        var user = CreateTestUser(initialXp: 50);
        var session = new StudySession
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Title = "Ongoing session",
            StartTime = DateTime.UtcNow.AddMinutes(-20),
            DurationMinutes = 20,
            IsCompleted = false
        };

        var result = await _gamificationService.AwardStudySessionXpAsync(user.Id, session);

        result.Awarded.Should().BeFalse();
        result.Amount.Should().Be(0);
        user.XP.Should().Be(50);
        _xpTransactions.Should().BeEmpty();
    }

    [Fact]
    public async Task AwardStudySessionXp_WhenAlreadyAwarded_ReturnsDuplicate()
    {
        var user = CreateTestUser(initialXp: 100);
        var session = new StudySession
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Title = "Physics",
            DurationMinutes = 30,
            IsCompleted = true
        };

        _xpTransactions.Add(new XpTransaction
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            SourceType = XpSourceTypes.StudySession,
            SourceId = session.Id.ToString(),
            Amount = 25,
            CreatedAt = DateTime.UtcNow
        });

        var result = await _gamificationService.AwardStudySessionXpAsync(user.Id, session);

        result.Awarded.Should().BeFalse();
        result.AlreadyAwarded.Should().BeTrue();
        user.XP.Should().Be(100);
        _xpTransactions.Should().HaveCount(1);
    }

    [Fact]
    public async Task AwardStudySessionXp_WhenDifferentUser_ThrowsUnauthorizedAccessException()
    {
        var userA = CreateTestUser();
        var userB = CreateTestUser();
        var session = new StudySession
        {
            Id = Guid.NewGuid(),
            UserId = userB.Id,
            DurationMinutes = 30,
            IsCompleted = true
        };

        Func<Task> act = async () => await _gamificationService.AwardStudySessionXpAsync(userA.Id, session);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    // =========================================================================
    // 2. Daily Task XP Tests
    // =========================================================================

    [Fact]
    public async Task AwardTaskXp_WhenCompleted_Awards15XpAndUpdatesUserLevel()
    {
        var user = CreateTestUser(initialXp: 90);
        var task = new DailyTask
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Title = "Read chapter 1",
            IsCompleted = true
        };

        var result = await _gamificationService.AwardTaskXpAsync(user.Id, task);

        result.Awarded.Should().BeTrue();
        result.Amount.Should().Be(15);
        user.XP.Should().Be(105);
        user.Level.Should().Be(2); // 105 XP crosses 100 threshold into level 2
        _xpTransactions.Should().HaveCount(1);
        _xpTransactions.Single().SourceType.Should().Be(XpSourceTypes.Task);
        _xpTransactions.Single().SourceId.Should().Be(task.Id.ToString());
    }

    [Fact]
    public async Task AwardTaskXp_WhenAlreadyAwarded_ReturnsDuplicateAndDoesNotModifyUserXp()
    {
        var user = CreateTestUser(initialXp: 15);
        var task = new DailyTask
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Title = "Read chapter 1",
            IsCompleted = true
        };

        _xpTransactions.Add(new XpTransaction
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            SourceType = XpSourceTypes.Task,
            SourceId = task.Id.ToString(),
            Amount = 15,
            CreatedAt = DateTime.UtcNow
        });

        var result = await _gamificationService.AwardTaskXpAsync(user.Id, task);

        result.AlreadyAwarded.Should().BeTrue();
        result.Awarded.Should().BeFalse();
        user.XP.Should().Be(15);
        _xpTransactions.Should().HaveCount(1);
    }

    [Fact]
    public async Task AwardTaskXp_WhenTaskIncomplete_ReturnsNotEligible()
    {
        var user = CreateTestUser();
        var task = new DailyTask
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Title = "Unchecked Task",
            IsCompleted = false
        };

        var result = await _gamificationService.AwardTaskXpAsync(user.Id, task);

        result.Awarded.Should().BeFalse();
        result.AlreadyAwarded.Should().BeFalse();
        user.XP.Should().Be(0);
        _xpTransactions.Should().BeEmpty();
    }

    // =========================================================================
    // 3. Habit XP Tests
    // =========================================================================

    [Fact]
    public async Task AwardHabitXp_WhenCompleted_Awards20XpWithDateKey()
    {
        var user = CreateTestUser(initialXp: 0);
        var habit = new Habit
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Title = "Morning Run"
        };
        var date = new DateTime(2026, 10, 13, 8, 0, 0, DateTimeKind.Utc);

        var result = await _gamificationService.AwardHabitXpAsync(user.Id, habit, date);

        result.Awarded.Should().BeTrue();
        result.Amount.Should().Be(20);
        user.XP.Should().Be(20);
        _xpTransactions.Should().HaveCount(1);
        var tx = _xpTransactions.Single();
        tx.SourceType.Should().Be(XpSourceTypes.Habit);
        tx.SourceId.Should().Be($"{habit.Id}:2026-10-13");
        tx.Amount.Should().Be(20);
    }

    [Fact]
    public async Task AwardHabitXp_WhenCompletedAgainOnSameDay_ReturnsDuplicate()
    {
        var user = CreateTestUser(initialXp: 20);
        var habit = new Habit
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Title = "Morning Run"
        };
        var date = new DateTime(2026, 10, 13, 8, 0, 0, DateTimeKind.Utc);

        _xpTransactions.Add(new XpTransaction
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            SourceType = XpSourceTypes.Habit,
            SourceId = $"{habit.Id}:2026-10-13",
            Amount = 20,
            CreatedAt = DateTime.UtcNow
        });

        var result = await _gamificationService.AwardHabitXpAsync(user.Id, habit, date);

        result.AlreadyAwarded.Should().BeTrue();
        result.Awarded.Should().BeFalse();
        user.XP.Should().Be(20);
        _xpTransactions.Should().HaveCount(1);
    }

    [Fact]
    public async Task AwardHabitXp_WhenCompletedOnDifferentDays_AwardsXpForBothDays()
    {
        var user = CreateTestUser(initialXp: 0);
        var habit = new Habit
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Title = "Morning Run"
        };
        var day1 = new DateTime(2026, 10, 13, 8, 0, 0, DateTimeKind.Utc);
        var day2 = new DateTime(2026, 10, 14, 8, 0, 0, DateTimeKind.Utc);

        var result1 = await _gamificationService.AwardHabitXpAsync(user.Id, habit, day1);
        result1.Awarded.Should().BeTrue();

        var result2 = await _gamificationService.AwardHabitXpAsync(user.Id, habit, day2);
        result2.Awarded.Should().BeTrue();

        user.XP.Should().Be(40);
        _xpTransactions.Should().HaveCount(2);
        _xpTransactions.Select(t => t.SourceId).Should().Contain($"{habit.Id}:2026-10-13");
        _xpTransactions.Select(t => t.SourceId).Should().Contain($"{habit.Id}:2026-10-14");
    }

    // =========================================================================
    // 4. Overflow Protection Tests
    // =========================================================================

    [Fact]
    public async Task AwardXp_WhenNearIntMaxValue_ThrowsOverflowExceptionAndPreservesBalance()
    {
        var user = CreateTestUser(initialXp: int.MaxValue - 10);
        var task = new DailyTask
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Title = "Max XP Task",
            IsCompleted = true
        };

        Func<Task> act = async () => await _gamificationService.AwardTaskXpAsync(user.Id, task);

        await act.Should().ThrowAsync<OverflowException>();
        user.XP.Should().Be(int.MaxValue - 10);
        _xpTransactions.Should().BeEmpty();
    }

    // =========================================================================
    // 5. Collision Detection Tests
    // =========================================================================

    [Fact]
    public void IsXpDuplicateCollision_WhenKeyMatchesXpTransactionsUniqueIndex_ReturnsTrue()
    {
        var ex = new DbUpdateException(
            "Duplicate entry for key 'IX_XpTransactions_UserId_SourceType_SourceId'",
            new Exception("1062"));

        _gamificationService.IsXpDuplicateCollision(ex).Should().BeTrue();
    }

    [Fact]
    public void IsXpDuplicateCollision_WhenUnrelatedConstraintViolated_ReturnsFalse()
    {
        var ex = new DbUpdateException(
            "Duplicate entry for key 'IX_HabitCompletions_HabitId_Date'",
            new Exception("1062"));

        _gamificationService.IsXpDuplicateCollision(ex).Should().BeFalse();
    }

    // =========================================================================
    // 6. End-to-End Handler Integration Tests
    // =========================================================================

    [Fact]
    public async Task EndStudySessionCommandHandler_AtomicCompletionAndAward()
    {
        var user = CreateTestUser(initialXp: 10);
        var currentUserServiceMock = new Mock<ICurrentUserService>();
        currentUserServiceMock.Setup(s => s.UserId).Returns(user.Id.ToString());

        var session = new StudySession
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Title = "Chemistry Lab",
            StartTime = DateTime.UtcNow.AddMinutes(-50),
            IsCompleted = false
        };
        _studySessions.Add(session);

        var handler = new EndStudySessionCommandHandler(_dbContextMock.Object, currentUserServiceMock.Object, _gamificationService, _achievementServiceMock.Object);
        var result = await handler.Handle(new EndStudySessionCommand { SessionId = session.Id }, CancellationToken.None);

        result.IsCompleted.Should().BeTrue();
        session.IsCompleted.Should().BeTrue();
        session.DurationMinutes.Should().Be(50);
        user.XP.Should().Be(45); // 10 initial + (10 + 50/2) = 10 + 35 = 45
        _xpTransactions.Should().HaveCount(1);
        _dbContextMock.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EndStudySessionCommandHandler_WhenReEndingCompletedSession_IsSafeNoOp()
    {
        var user = CreateTestUser(initialXp: 45);
        var currentUserServiceMock = new Mock<ICurrentUserService>();
        currentUserServiceMock.Setup(s => s.UserId).Returns(user.Id.ToString());

        var initialEndTime = DateTime.UtcNow.AddMinutes(-10);
        var session = new StudySession
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Title = "Completed Lab",
            StartTime = DateTime.UtcNow.AddMinutes(-60),
            EndTime = initialEndTime,
            DurationMinutes = 50,
            IsCompleted = true
        };
        _studySessions.Add(session);

        var handler = new EndStudySessionCommandHandler(_dbContextMock.Object, currentUserServiceMock.Object, _gamificationService, _achievementServiceMock.Object);
        var result = await handler.Handle(new EndStudySessionCommand { SessionId = session.Id }, CancellationToken.None);

        result.IsCompleted.Should().BeTrue();
        user.XP.Should().Be(45);
        _xpTransactions.Should().BeEmpty();
        _dbContextMock.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ToggleTaskCommandHandler_UncheckingAndRechecking_IdempotentXp()
    {
        var user = CreateTestUser(initialXp: 0);
        var currentUserServiceMock = new Mock<ICurrentUserService>();
        currentUserServiceMock.Setup(s => s.UserId).Returns(user.Id.ToString());

        var task = new DailyTask
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Title = "Biology quiz prep",
            IsCompleted = false
        };
        _tasks.Add(task);

        var handler = new ToggleTaskCommandHandler(_dbContextMock.Object, currentUserServiceMock.Object, _gamificationService, _achievementServiceMock.Object);

        // 1. First completion -> earns 15 XP
        var firstResult = await handler.Handle(new ToggleTaskCommand(task.Id), CancellationToken.None);
        firstResult.Should().BeTrue();
        task.IsCompleted.Should().BeTrue();
        user.XP.Should().Be(15);
        _xpTransactions.Should().HaveCount(1);

        // 2. Uncheck task -> 0 XP awarded, IsCompleted becomes false
        var uncheckResult = await handler.Handle(new ToggleTaskCommand(task.Id), CancellationToken.None);
        uncheckResult.Should().BeFalse();
        task.IsCompleted.Should().BeFalse();
        user.XP.Should().Be(15); // Not deducted
        _xpTransactions.Should().HaveCount(1);

        // 3. Re-check task -> 0 XP awarded (duplicate detected), IsCompleted becomes true
        var recheckResult = await handler.Handle(new ToggleTaskCommand(task.Id), CancellationToken.None);
        recheckResult.Should().BeTrue();
        task.IsCompleted.Should().BeTrue();
        user.XP.Should().Be(15); // Still 15 XP!
        _xpTransactions.Should().HaveCount(1);
    }

    [Fact]
    public async Task ToggleHabitCompletionCommandHandler_UntoggleAndRetoggle_IdempotentXp()
    {
        var user = CreateTestUser(initialXp: 0);
        var currentUserServiceMock = new Mock<ICurrentUserService>();
        currentUserServiceMock.Setup(s => s.UserId).Returns(user.Id.ToString());

        var habit = new Habit
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Title = "Read 10 pages"
        };
        _habits.Add(habit);

        var handler = new ToggleHabitCompletionCommandHandler(_dbContextMock.Object, currentUserServiceMock.Object, _gamificationService, _achievementServiceMock.Object);

        // 1. Toggle on today -> earns 20 XP
        var firstResult = await handler.Handle(new ToggleHabitCompletionCommand { HabitId = habit.Id }, CancellationToken.None);
        firstResult.Should().BeTrue();
        _habitCompletions.Should().HaveCount(1);
        user.XP.Should().Be(20);
        _xpTransactions.Should().HaveCount(1);

        // 2. Untoggle today -> removed completion, 0 XP change
        var untoggleResult = await handler.Handle(new ToggleHabitCompletionCommand { HabitId = habit.Id }, CancellationToken.None);
        untoggleResult.Should().BeFalse();
        _habitCompletions.Should().BeEmpty();
        user.XP.Should().Be(20);
        _xpTransactions.Should().HaveCount(1);

        // 3. Re-toggle today -> completion added, duplicate detected, 0 additional XP
        var retoggleResult = await handler.Handle(new ToggleHabitCompletionCommand { HabitId = habit.Id }, CancellationToken.None);
        retoggleResult.Should().BeTrue();
        _habitCompletions.Should().HaveCount(1);
        user.XP.Should().Be(20);
        _xpTransactions.Should().HaveCount(1);
    }

    // =========================================================================
    // 7. UserConcurrencyLock Tests
    // =========================================================================

    [Fact]
    public async Task UserConcurrencyLock_SerializesConcurrentAccessForSameUser()
    {
        var userId = Guid.NewGuid();
        var sequence = new List<int>();

        var task1 = Task.Run(async () =>
        {
            using (await UserConcurrencyLock.LockAsync(userId))
            {
                sequence.Add(1);
                await Task.Delay(50);
                sequence.Add(2);
            }
        });

        var task2 = Task.Run(async () =>
        {
            await Task.Delay(10); // give task1 time to acquire
            using (await UserConcurrencyLock.LockAsync(userId))
            {
                sequence.Add(3);
                sequence.Add(4);
            }
        });

        await Task.WhenAll(task1, task2);

        // Sequence must be strictly sequential (1, 2, 3, 4) without interleaving
        sequence.Should().Equal(1, 2, 3, 4);
    }

    [Fact]
    public async Task UserConcurrencyLock_AllowsParallelAccessForDifferentUsers()
    {
        var user1 = Guid.NewGuid();
        var user2 = Guid.NewGuid();
        var parallelStarted = false;

        var task1 = Task.Run(async () =>
        {
            using (await UserConcurrencyLock.LockAsync(user1))
            {
                await Task.Delay(100);
            }
        });

        var task2 = Task.Run(async () =>
        {
            await Task.Delay(10);
            using (await UserConcurrencyLock.LockAsync(user2))
            {
                // Should acquire immediately even though user1 is locked
                parallelStarted = true;
            }
        });

        await Task.WhenAll(task1, task2);

        parallelStarted.Should().BeTrue();
    }

    // =========================================================================
    // 8. Direct Award and Validation Tests
    // =========================================================================

    [Fact]
    public async Task AwardXpDirectAsync_WhenValid_PersistsTransactionAndIncrementsBalance()
    {
        var user = CreateTestUser(initialXp: 50);

        var result = await _gamificationService.AwardXpDirectAsync(
            user.Id,
            25,
            XpSourceTypes.Bonus,
            "bonus-1",
            "Special event bonus");

        result.Awarded.Should().BeTrue();
        result.Amount.Should().Be(25);
        result.NewTotalXp.Should().Be(75);
        result.NewLevel.Should().Be(1);
        user.XP.Should().Be(75);
        _xpTransactions.Should().HaveCount(1);
        _dbContextMock.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public async Task AwardXpDirectAsync_WhenAmountZeroOrNegative_ReturnsNotEligibleAndDoesNotPersist(int invalidAmount)
    {
        var user = CreateTestUser(initialXp: 50);

        var result = await _gamificationService.AwardXpDirectAsync(
            user.Id,
            invalidAmount,
            XpSourceTypes.Bonus,
            "bonus-zero",
            "Zero bonus");

        result.Awarded.Should().BeFalse();
        result.Amount.Should().Be(0);
        user.XP.Should().Be(50);
        _xpTransactions.Should().BeEmpty();
        _dbContextMock.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(null, "valid-id")]
    [InlineData("", "valid-id")]
    [InlineData("   ", "valid-id")]
    [InlineData("BONUS", null)]
    [InlineData("BONUS", "")]
    [InlineData("BONUS", "   ")]
    public async Task AwardXpDirectAsync_WhenSourceTypeOrSourceIdMissing_ThrowsArgumentException(string? sourceType, string? sourceId)
    {
        var user = CreateTestUser();

        Func<Task> act = async () => await _gamificationService.AwardXpDirectAsync(
            user.Id,
            20,
            sourceType!,
            sourceId!,
            "Description");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task AwardXpDirectAsync_WhenAlreadyAwarded_ReturnsDuplicateAndDoesNotPersist()
    {
        var user = CreateTestUser(initialXp: 100);
        _xpTransactions.Add(new XpTransaction
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            SourceType = XpSourceTypes.Bonus,
            SourceId = "duplicate-direct",
            Amount = 50,
            CreatedAt = DateTime.UtcNow
        });

        var result = await _gamificationService.AwardXpDirectAsync(
            user.Id,
            50,
            XpSourceTypes.Bonus,
            "duplicate-direct",
            "Duplicate grant");

        result.AlreadyAwarded.Should().BeTrue();
        result.Awarded.Should().BeFalse();
        user.XP.Should().Be(100);
        _dbContextMock.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    // =========================================================================
    // 9. Unrelated Database Error Propagation Tests
    // =========================================================================

    [Theory]
    [InlineData("Cannot add or update a child row: a foreign key constraint fails", "1452")]
    [InlineData("Deadlock found when trying to get lock; try restarting transaction", "1213")]
    [InlineData("You have an error in your SQL syntax", "1064")]
    [InlineData("Duplicate entry 'test@example.com' for key 'IX_Users_Email'", "1062")]
    [InlineData("Duplicate entry 'room-1' for key 'IX_RoomParticipants_RoomName_SeatNumber'", "1062")]
    public void IsXpDuplicateCollision_WhenUnrelatedError_ReturnsFalse(string message, string errNumber)
    {
        var ex = new DbUpdateException(message, new Exception(errNumber));
        _gamificationService.IsXpDuplicateCollision(ex).Should().BeFalse();
    }

    [Fact]
    public async Task EndStudySessionCommandHandler_WhenSaveChangesAsyncThrowsUnrelatedDbUpdateException_PropagatesException()
    {
        var user = CreateTestUser();
        var currentUserServiceMock = new Mock<ICurrentUserService>();
        currentUserServiceMock.Setup(s => s.UserId).Returns(user.Id.ToString());

        var session = new StudySession
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Title = "Session with DB Failure",
            StartTime = DateTime.UtcNow.AddMinutes(-30),
            IsCompleted = false
        };
        _studySessions.Add(session);

        _dbContextMock.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateException("Deadlock detected", new Exception("1213")));

        var handler = new EndStudySessionCommandHandler(_dbContextMock.Object, currentUserServiceMock.Object, _gamificationService, _achievementServiceMock.Object);

        Func<Task> act = async () => await handler.Handle(new EndStudySessionCommand { SessionId = session.Id }, CancellationToken.None);

        await act.Should().ThrowAsync<DbUpdateException>().WithMessage("*Deadlock detected*");
    }

    [Fact]
    public async Task ToggleTaskCommandHandler_WhenSaveChangesAsyncThrowsUnrelatedDbUpdateException_PropagatesException()
    {
        var user = CreateTestUser();
        var currentUserServiceMock = new Mock<ICurrentUserService>();
        currentUserServiceMock.Setup(s => s.UserId).Returns(user.Id.ToString());

        var task = new DailyTask
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Title = "Task with DB Failure",
            IsCompleted = false
        };
        _tasks.Add(task);

        _dbContextMock.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateException("Connection terminated", new Exception("2002")));

        var handler = new ToggleTaskCommandHandler(_dbContextMock.Object, currentUserServiceMock.Object, _gamificationService, _achievementServiceMock.Object);

        Func<Task> act = async () => await handler.Handle(new ToggleTaskCommand(task.Id), CancellationToken.None);

        await act.Should().ThrowAsync<DbUpdateException>().WithMessage("*Connection terminated*");
    }

    [Fact]
    public async Task ToggleHabitCompletionCommandHandler_WhenSaveChangesAsyncThrowsUnrelatedDbUpdateException_PropagatesException()
    {
        var user = CreateTestUser();
        var currentUserServiceMock = new Mock<ICurrentUserService>();
        currentUserServiceMock.Setup(s => s.UserId).Returns(user.Id.ToString());

        var habit = new Habit
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Title = "Habit with DB Failure"
        };
        _habits.Add(habit);

        _dbContextMock.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateException("General failure", new Exception("9999")));

        var handler = new ToggleHabitCompletionCommandHandler(_dbContextMock.Object, currentUserServiceMock.Object, _gamificationService, _achievementServiceMock.Object);

        Func<Task> act = async () => await handler.Handle(new ToggleHabitCompletionCommand { HabitId = habit.Id }, CancellationToken.None);

        await act.Should().ThrowAsync<DbUpdateException>().WithMessage("*General failure*");
    }

    // =========================================================================
    // 10. Failure Before Persistence (Overflow & No SaveChanges)
    // =========================================================================

    [Fact]
    public async Task EndStudySessionCommandHandler_WhenOverflowOccurs_ThrowsAndDoesNotCallSaveChanges()
    {
        var user = CreateTestUser(initialXp: int.MaxValue - 5);
        var currentUserServiceMock = new Mock<ICurrentUserService>();
        currentUserServiceMock.Setup(s => s.UserId).Returns(user.Id.ToString());

        var session = new StudySession
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Title = "Overflow Session",
            StartTime = DateTime.UtcNow.AddMinutes(-20),
            IsCompleted = false
        };
        _studySessions.Add(session);

        var handler = new EndStudySessionCommandHandler(_dbContextMock.Object, currentUserServiceMock.Object, _gamificationService, _achievementServiceMock.Object);

        Func<Task> act = async () => await handler.Handle(new EndStudySessionCommand { SessionId = session.Id }, CancellationToken.None);

        await act.Should().ThrowAsync<OverflowException>();
        _dbContextMock.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        user.XP.Should().Be(int.MaxValue - 5);
    }

    [Fact]
    public async Task ToggleTaskCommandHandler_WhenOverflowOccurs_ThrowsAndDoesNotCallSaveChanges()
    {
        var user = CreateTestUser(initialXp: int.MaxValue - 5);
        var currentUserServiceMock = new Mock<ICurrentUserService>();
        currentUserServiceMock.Setup(s => s.UserId).Returns(user.Id.ToString());

        var task = new DailyTask
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Title = "Overflow Task",
            IsCompleted = false
        };
        _tasks.Add(task);

        var handler = new ToggleTaskCommandHandler(_dbContextMock.Object, currentUserServiceMock.Object, _gamificationService, _achievementServiceMock.Object);

        Func<Task> act = async () => await handler.Handle(new ToggleTaskCommand(task.Id), CancellationToken.None);

        await act.Should().ThrowAsync<OverflowException>();
        _dbContextMock.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        user.XP.Should().Be(int.MaxValue - 5);
    }

    // =========================================================================
    // 11. Multi-Event Concurrent Awards for Same User (No Lost Updates)
    // =========================================================================

    [Fact]
    public async Task ConcurrentAwards_DifferentEventsForSameUser_AccumulatesAllXpWithoutLostUpdates()
    {
        var user = CreateTestUser(initialXp: 0);
        var currentUserServiceMock = new Mock<ICurrentUserService>();
        currentUserServiceMock.Setup(s => s.UserId).Returns(user.Id.ToString());

        // Event 1: Daily Task (+15 XP)
        var task = new DailyTask { Id = Guid.NewGuid(), UserId = user.Id, Title = "Task 1", IsCompleted = false };
        _tasks.Add(task);
        var taskHandler = new ToggleTaskCommandHandler(_dbContextMock.Object, currentUserServiceMock.Object, _gamificationService, _achievementServiceMock.Object);

        // Event 2: Habit (+20 XP)
        var habit = new Habit { Id = Guid.NewGuid(), UserId = user.Id, Title = "Habit 1" };
        _habits.Add(habit);
        var habitHandler = new ToggleHabitCompletionCommandHandler(_dbContextMock.Object, currentUserServiceMock.Object, _gamificationService, _achievementServiceMock.Object);

        // Event 3: Study Session (+22 XP for 25m)
        var session = new StudySession
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Title = "Session 1",
            StartTime = DateTime.UtcNow.AddMinutes(-25),
            IsCompleted = false
        };
        _studySessions.Add(session);
        var sessionHandler = new EndStudySessionCommandHandler(_dbContextMock.Object, currentUserServiceMock.Object, _gamificationService, _achievementServiceMock.Object);

        // Run all three concurrently
        var taskAction = taskHandler.Handle(new ToggleTaskCommand(task.Id), CancellationToken.None);
        var habitAction = habitHandler.Handle(new ToggleHabitCompletionCommand { HabitId = habit.Id }, CancellationToken.None);
        var sessionAction = sessionHandler.Handle(new EndStudySessionCommand { SessionId = session.Id }, CancellationToken.None);

        await Task.WhenAll(taskAction, habitAction, sessionAction);

        // Total: 15 + 20 + 22 = 57 XP
        user.XP.Should().Be(57);
        user.Level.Should().Be(1);
        _xpTransactions.Should().HaveCount(3);
        _xpTransactions.Select(t => t.SourceType).Should().BeEquivalentTo(new[] { XpSourceTypes.Task, XpSourceTypes.Habit, XpSourceTypes.StudySession });
    }

    [Fact]
    public void HandleDuplicateCollision_WhenMockContext_ExecutesSafelyWithoutThrowing()
    {
        var userId = Guid.NewGuid();
        // Should execute safely without throwing even on mocked context
        var act = () => _gamificationService.HandleDuplicateCollision(userId, XpSourceTypes.Task, "any-id");
        act.Should().NotThrow();
    }
}
