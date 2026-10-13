using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using MOT.Application.Common.Exceptions;
using MOT.Application.Common.Interfaces;
using MOT.Application.Gamification;
using MOT.Domain.Entities;
using MOT.Domain.Gamification;
using MOT.Tests.Common;
using Xunit;

namespace MOT.Tests.Gamification;

public class AchievementServiceTests
{
    private readonly Mock<IAppDbContext> _dbContextMock;
    private readonly Mock<IGamificationService> _gamificationServiceMock;
    private readonly List<User> _users;
    private readonly List<StudySession> _studySessions;
    private readonly List<DailyTask> _tasks;
    private readonly List<Habit> _habits;
    private readonly List<HabitCompletion> _habitCompletions;
    private readonly List<XpTransaction> _xpTransactions;
    private readonly List<UserAchievement> _userAchievements;
    private readonly AchievementService _achievementService;

    public AchievementServiceTests()
    {
        _users = new List<User>();
        _studySessions = new List<StudySession>();
        _tasks = new List<DailyTask>();
        _habits = new List<Habit>();
        _habitCompletions = new List<HabitCompletion>();
        _xpTransactions = new List<XpTransaction>();
        _userAchievements = new List<UserAchievement>();

        _dbContextMock = new Mock<IAppDbContext>();
        _dbContextMock.Setup(c => c.Users).Returns(MockDbSetHelper.CreateMockDbSet(_users).Object);
        _dbContextMock.Setup(c => c.StudySessions).Returns(MockDbSetHelper.CreateMockDbSet(_studySessions).Object);
        _dbContextMock.Setup(c => c.Tasks).Returns(MockDbSetHelper.CreateMockDbSet(_tasks).Object);
        _dbContextMock.Setup(c => c.Habits).Returns(MockDbSetHelper.CreateMockDbSet(_habits).Object);
        _dbContextMock.Setup(c => c.HabitCompletions).Returns(MockDbSetHelper.CreateMockDbSet(_habitCompletions).Object);
        _dbContextMock.Setup(c => c.XpTransactions).Returns(MockDbSetHelper.CreateMockDbSet(_xpTransactions).Object);
        _dbContextMock.Setup(c => c.UserAchievements).Returns(MockDbSetHelper.CreateMockDbSet(_userAchievements).Object);

        _gamificationServiceMock = new Mock<IGamificationService>();
        _gamificationServiceMock
            .Setup(g => g.StageAchievementXpAsync(It.IsAny<User>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new XpAwardResult(true, 25, 25, 1, false, null));

        _achievementService = new AchievementService(_dbContextMock.Object, _gamificationServiceMock.Object);
    }

    private User CreateTestUser()
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = $"user-{Guid.NewGuid()}@example.com",
            PasswordHash = "hashedPassword",
            DisplayName = "Test User",
            XP = 0,
            Level = 1,
            CreatedAt = DateTime.UtcNow
        };
        _users.Add(user);
        return user;
    }

    // =========================================================================
    // 1. Study Session Achievements
    // =========================================================================

    [Fact]
    public async Task EvaluateStudySession_WhenUnder15Minutes_DoesNotUnlockFocusInitiate()
    {
        var user = CreateTestUser();
        var session = new StudySession
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Title = "Quick Session",
            StartTime = DateTime.UtcNow.AddMinutes(-14),
            EndTime = DateTime.UtcNow,
            DurationMinutes = 14,
            IsCompleted = true
        };
        _studySessions.Add(session);

        var unlocked = await _achievementService.EvaluateStudySessionAchievementsAsync(user.Id, session);

        unlocked.Should().BeEmpty();
        _userAchievements.Should().BeEmpty();
        _gamificationServiceMock.Verify(g => g.StageAchievementXpAsync(It.IsAny<User>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EvaluateStudySession_When15Minutes_UnlocksFocusInitiateAndStagesXp()
    {
        var user = CreateTestUser();
        var session = new StudySession
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Title = "15m Session",
            StartTime = DateTime.UtcNow.AddMinutes(-15),
            EndTime = DateTime.UtcNow,
            DurationMinutes = 15,
            IsCompleted = true
        };
        _studySessions.Add(session);

        var unlocked = await _achievementService.EvaluateStudySessionAchievementsAsync(user.Id, session);

        unlocked.Should().HaveCount(1);
        unlocked[0].Code.Should().Be(AchievementCodes.FocusInitiate);
        unlocked[0].XpReward.Should().Be(25);

        _userAchievements.Should().HaveCount(1);
        _userAchievements[0].AchievementCode.Should().Be(AchievementCodes.FocusInitiate);
        _userAchievements[0].UserId.Should().Be(user.Id);
        _userAchievements[0].XpAwarded.Should().Be(25);

        _gamificationServiceMock.Verify(g => g.StageAchievementXpAsync(user, AchievementCodes.FocusInitiate, 25, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EvaluateStudySession_When60Minutes_UnlocksBothFocusInitiateAndDeepDiver()
    {
        var user = CreateTestUser();
        var session = new StudySession
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Title = "60m Session",
            StartTime = DateTime.UtcNow.AddMinutes(-60),
            EndTime = DateTime.UtcNow,
            DurationMinutes = 60,
            IsCompleted = true
        };
        _studySessions.Add(session);

        var unlocked = await _achievementService.EvaluateStudySessionAchievementsAsync(user.Id, session);

        unlocked.Select(a => a.Code).Should().BeEquivalentTo(new[] { AchievementCodes.FocusInitiate, AchievementCodes.DeepDiver });
        _userAchievements.Select(a => a.AchievementCode).Should().BeEquivalentTo(new[] { AchievementCodes.FocusInitiate, AchievementCodes.DeepDiver });

        _gamificationServiceMock.Verify(g => g.StageAchievementXpAsync(user, AchievementCodes.FocusInitiate, 25, It.IsAny<CancellationToken>()), Times.Once);
        _gamificationServiceMock.Verify(g => g.StageAchievementXpAsync(user, AchievementCodes.DeepDiver, 50, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EvaluateStudySession_WhenCumulativeReaches100Minutes_UnlocksCenturyClub()
    {
        var user = CreateTestUser();

        // Prior session: 80 min
        var prior = new StudySession
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Title = "Prior Session",
            StartTime = DateTime.UtcNow.AddHours(-3),
            EndTime = DateTime.UtcNow.AddHours(-1).AddMinutes(-40),
            DurationMinutes = 80,
            IsCompleted = true
        };
        _studySessions.Add(prior);

        // New session: 20 min (total reaches 100 min)
        var current = new StudySession
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Title = "Current Session",
            StartTime = DateTime.UtcNow.AddMinutes(-20),
            EndTime = DateTime.UtcNow,
            DurationMinutes = 20,
            IsCompleted = true
        };
        _studySessions.Add(current);

        // User already has FocusInitiate
        _userAchievements.Add(new UserAchievement
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            AchievementCode = AchievementCodes.FocusInitiate,
            UnlockedAt = DateTime.UtcNow.AddHours(-3),
            XpAwarded = 25
        });

        var unlocked = await _achievementService.EvaluateStudySessionAchievementsAsync(user.Id, current);

        unlocked.Should().ContainSingle(a => a.Code == AchievementCodes.CenturyClub);
        unlocked[0].XpReward.Should().Be(50);
        _gamificationServiceMock.Verify(g => g.StageAchievementXpAsync(user, AchievementCodes.CenturyClub, 50, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EvaluateStudySession_WhenCumulativeIs99Minutes_DoesNotUnlockCenturyClub()
    {
        var user = CreateTestUser();

        var prior = new StudySession
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Title = "Prior Session",
            StartTime = DateTime.UtcNow.AddHours(-2),
            EndTime = DateTime.UtcNow.AddHours(-1),
            DurationMinutes = 60,
            IsCompleted = true
        };
        _studySessions.Add(prior);

        var current = new StudySession
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Title = "Current Session",
            StartTime = DateTime.UtcNow.AddMinutes(-39),
            EndTime = DateTime.UtcNow,
            DurationMinutes = 39,
            IsCompleted = true
        };
        _studySessions.Add(current);

        _userAchievements.Add(new UserAchievement
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            AchievementCode = AchievementCodes.FocusInitiate,
            UnlockedAt = DateTime.UtcNow.AddHours(-2),
            XpAwarded = 25
        });
        _userAchievements.Add(new UserAchievement
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            AchievementCode = AchievementCodes.DeepDiver,
            UnlockedAt = DateTime.UtcNow.AddHours(-2),
            XpAwarded = 50
        });

        var unlocked = await _achievementService.EvaluateStudySessionAchievementsAsync(user.Id, current);

        unlocked.Should().BeEmpty();
    }

    [Fact]
    public async Task EvaluateStudySession_WhenUncompletedSession_DoesNotUnlockAnyAchievements()
    {
        var user = CreateTestUser();
        var session = new StudySession
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Title = "Unfinished Session",
            StartTime = DateTime.UtcNow.AddMinutes(-120),
            EndTime = null,
            DurationMinutes = 0,
            IsCompleted = false
        };
        _studySessions.Add(session);

        var unlocked = await _achievementService.EvaluateStudySessionAchievementsAsync(user.Id, session);

        unlocked.Should().BeEmpty();
        _userAchievements.Should().BeEmpty();
    }

    [Fact]
    public async Task EvaluateStudySession_WhenAlreadyUnlocked_DoesNotReUnlock()
    {
        var user = CreateTestUser();
        _userAchievements.Add(new UserAchievement
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            AchievementCode = AchievementCodes.FocusInitiate,
            UnlockedAt = DateTime.UtcNow.AddDays(-1),
            XpAwarded = 25
        });

        var session = new StudySession
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Title = "Second Session",
            StartTime = DateTime.UtcNow.AddMinutes(-20),
            EndTime = DateTime.UtcNow,
            DurationMinutes = 20,
            IsCompleted = true
        };
        _studySessions.Add(session);

        var unlocked = await _achievementService.EvaluateStudySessionAchievementsAsync(user.Id, session);

        unlocked.Should().BeEmpty();
        _userAchievements.Should().HaveCount(1);
    }

    // =========================================================================
    // 2. Task Achievements
    // =========================================================================

    [Fact]
    public async Task EvaluateTask_WhenZeroTasksCompleted_DoesNotUnlockFirstStepOrTaskMaster()
    {
        var user = CreateTestUser();

        var unlocked = await _achievementService.EvaluateTaskAchievementsAsync(user.Id);

        unlocked.Should().BeEmpty();
        _userAchievements.Should().BeEmpty();
    }

    [Fact]
    public async Task EvaluateTask_WhenOneTaskCompleted_UnlocksFirstStepOnly()
    {
        var user = CreateTestUser();
        _xpTransactions.Add(new XpTransaction
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Amount = 15,
            SourceType = XpSourceTypes.Task,
            SourceId = Guid.NewGuid().ToString(),
            CreatedAt = DateTime.UtcNow
        });

        var unlocked = await _achievementService.EvaluateTaskAchievementsAsync(user.Id);

        unlocked.Should().HaveCount(1);
        unlocked[0].Code.Should().Be(AchievementCodes.FirstStep);
        unlocked[0].XpReward.Should().Be(25);

        _userAchievements.Should().HaveCount(1);
        _userAchievements[0].AchievementCode.Should().Be(AchievementCodes.FirstStep);

        _gamificationServiceMock.Verify(g => g.StageAchievementXpAsync(user, AchievementCodes.FirstStep, 25, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EvaluateTask_WhenDuplicateTogglesOfSameTask_CountsAsSingleDistinctTask()
    {
        var user = CreateTestUser();
        var sameTaskId = Guid.NewGuid().ToString();

        // 3 ledger rows for same task (e.g. legacy or retries)
        for (int i = 0; i < 3; i++)
        {
            _xpTransactions.Add(new XpTransaction
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                Amount = 15,
                SourceType = XpSourceTypes.Task,
                SourceId = sameTaskId,
                CreatedAt = DateTime.UtcNow
            });
        }

        var unlocked = await _achievementService.EvaluateTaskAchievementsAsync(user.Id);

        // Only unlocks FIRST_STEP, NOT TASK_MASTER
        unlocked.Should().HaveCount(1);
        unlocked[0].Code.Should().Be(AchievementCodes.FirstStep);
    }

    [Fact]
    public async Task EvaluateTask_WhenNineDistinctTasksCompleted_DoesNotUnlockTaskMaster()
    {
        var user = CreateTestUser();
        _userAchievements.Add(new UserAchievement
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            AchievementCode = AchievementCodes.FirstStep,
            UnlockedAt = DateTime.UtcNow.AddDays(-2),
            XpAwarded = 25
        });

        for (int i = 0; i < 9; i++)
        {
            _xpTransactions.Add(new XpTransaction
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                Amount = 15,
                SourceType = XpSourceTypes.Task,
                SourceId = Guid.NewGuid().ToString(),
                CreatedAt = DateTime.UtcNow
            });
        }

        var unlocked = await _achievementService.EvaluateTaskAchievementsAsync(user.Id);

        unlocked.Should().BeEmpty();
    }

    [Fact]
    public async Task EvaluateTask_WhenTenDistinctTasksCompleted_UnlocksTaskMaster()
    {
        var user = CreateTestUser();
        _userAchievements.Add(new UserAchievement
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            AchievementCode = AchievementCodes.FirstStep,
            UnlockedAt = DateTime.UtcNow.AddDays(-2),
            XpAwarded = 25
        });

        for (int i = 0; i < 10; i++)
        {
            _xpTransactions.Add(new XpTransaction
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                Amount = 15,
                SourceType = XpSourceTypes.Task,
                SourceId = Guid.NewGuid().ToString(),
                CreatedAt = DateTime.UtcNow
            });
        }

        var unlocked = await _achievementService.EvaluateTaskAchievementsAsync(user.Id);

        unlocked.Should().HaveCount(1);
        unlocked[0].Code.Should().Be(AchievementCodes.TaskMaster);
        unlocked[0].XpReward.Should().Be(75);

        _gamificationServiceMock.Verify(g => g.StageAchievementXpAsync(user, AchievementCodes.TaskMaster, 75, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EvaluateTask_WhenAlreadyUnlocked_DoesNotReUnlock()
    {
        var user = CreateTestUser();
        _userAchievements.Add(new UserAchievement
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            AchievementCode = AchievementCodes.FirstStep,
            UnlockedAt = DateTime.UtcNow.AddDays(-5),
            XpAwarded = 25
        });
        _userAchievements.Add(new UserAchievement
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            AchievementCode = AchievementCodes.TaskMaster,
            UnlockedAt = DateTime.UtcNow.AddDays(-1),
            XpAwarded = 75
        });

        for (int i = 0; i < 15; i++)
        {
            _xpTransactions.Add(new XpTransaction
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                Amount = 15,
                SourceType = XpSourceTypes.Task,
                SourceId = Guid.NewGuid().ToString(),
                CreatedAt = DateTime.UtcNow
            });
        }

        var unlocked = await _achievementService.EvaluateTaskAchievementsAsync(user.Id);

        unlocked.Should().BeEmpty();
        _userAchievements.Should().HaveCount(2);
    }

    // =========================================================================
    // 3. Habit Achievements
    // =========================================================================

    [Fact]
    public async Task EvaluateHabit_WhenZeroCompletions_DoesNotUnlockAnyHabitAchievements()
    {
        var user = CreateTestUser();
        var habit = new Habit { Id = Guid.NewGuid(), UserId = user.Id, Title = "Daily Reading" };
        _habits.Add(habit);

        var unlocked = await _achievementService.EvaluateHabitAchievementsAsync(user.Id, habit, DateTime.UtcNow);

        unlocked.Should().BeEmpty();
        _userAchievements.Should().BeEmpty();
    }

    [Fact]
    public async Task EvaluateHabit_WhenFirstCompletion_UnlocksHabitStarter()
    {
        var user = CreateTestUser();
        var habit = new Habit { Id = Guid.NewGuid(), UserId = user.Id, Title = "Daily Meditation" };
        _habits.Add(habit);

        var completion = new HabitCompletion
        {
            Id = Guid.NewGuid(),
            HabitId = habit.Id,
            Date = DateTime.UtcNow.Date
        };
        _habitCompletions.Add(completion);

        var unlocked = await _achievementService.EvaluateHabitAchievementsAsync(user.Id, habit, DateTime.UtcNow);

        unlocked.Should().HaveCount(1);
        unlocked[0].Code.Should().Be(AchievementCodes.HabitStarter);
        unlocked[0].XpReward.Should().Be(25);

        _gamificationServiceMock.Verify(g => g.StageAchievementXpAsync(user, AchievementCodes.HabitStarter, 25, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EvaluateHabit_WhenStreakIs2Days_DoesNotUnlockStreakBuilder()
    {
        var user = CreateTestUser();
        var habit = new Habit { Id = Guid.NewGuid(), UserId = user.Id, Title = "Workout" };
        _habits.Add(habit);

        var today = DateTime.UtcNow.Date;
        _habitCompletions.Add(new HabitCompletion { Id = Guid.NewGuid(), HabitId = habit.Id, Date = today });
        _habitCompletions.Add(new HabitCompletion { Id = Guid.NewGuid(), HabitId = habit.Id, Date = today.AddDays(-1) });

        _userAchievements.Add(new UserAchievement
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            AchievementCode = AchievementCodes.HabitStarter,
            UnlockedAt = DateTime.UtcNow.AddDays(-1),
            XpAwarded = 25
        });

        var unlocked = await _achievementService.EvaluateHabitAchievementsAsync(user.Id, habit, today);

        unlocked.Should().BeEmpty();
    }

    [Fact]
    public async Task EvaluateHabit_WhenStreakIs3Days_UnlocksStreakBuilder()
    {
        var user = CreateTestUser();
        var habit = new Habit { Id = Guid.NewGuid(), UserId = user.Id, Title = "Workout" };
        _habits.Add(habit);

        var today = DateTime.UtcNow.Date;
        _habitCompletions.Add(new HabitCompletion { Id = Guid.NewGuid(), HabitId = habit.Id, Date = today });
        _habitCompletions.Add(new HabitCompletion { Id = Guid.NewGuid(), HabitId = habit.Id, Date = today.AddDays(-1) });
        _habitCompletions.Add(new HabitCompletion { Id = Guid.NewGuid(), HabitId = habit.Id, Date = today.AddDays(-2) });

        _userAchievements.Add(new UserAchievement
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            AchievementCode = AchievementCodes.HabitStarter,
            UnlockedAt = DateTime.UtcNow.AddDays(-2),
            XpAwarded = 25
        });

        var unlocked = await _achievementService.EvaluateHabitAchievementsAsync(user.Id, habit, today);

        unlocked.Should().HaveCount(1);
        unlocked[0].Code.Should().Be(AchievementCodes.StreakBuilder);
        unlocked[0].XpReward.Should().Be(50);

        _gamificationServiceMock.Verify(g => g.StageAchievementXpAsync(user, AchievementCodes.StreakBuilder, 50, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EvaluateHabit_WhenStreakIs6Days_DoesNotUnlockUnstoppable()
    {
        var user = CreateTestUser();
        var habit = new Habit { Id = Guid.NewGuid(), UserId = user.Id, Title = "Drink Water" };
        _habits.Add(habit);

        var today = DateTime.UtcNow.Date;
        for (int i = 0; i < 6; i++)
        {
            _habitCompletions.Add(new HabitCompletion { Id = Guid.NewGuid(), HabitId = habit.Id, Date = today.AddDays(-i) });
        }

        _userAchievements.Add(new UserAchievement
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            AchievementCode = AchievementCodes.HabitStarter,
            UnlockedAt = DateTime.UtcNow.AddDays(-5),
            XpAwarded = 25
        });
        _userAchievements.Add(new UserAchievement
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            AchievementCode = AchievementCodes.StreakBuilder,
            UnlockedAt = DateTime.UtcNow.AddDays(-3),
            XpAwarded = 50
        });

        var unlocked = await _achievementService.EvaluateHabitAchievementsAsync(user.Id, habit, today);

        unlocked.Should().BeEmpty();
    }

    [Fact]
    public async Task EvaluateHabit_WhenStreakIs7Days_UnlocksUnstoppable()
    {
        var user = CreateTestUser();
        var habit = new Habit { Id = Guid.NewGuid(), UserId = user.Id, Title = "Drink Water" };
        _habits.Add(habit);

        var today = DateTime.UtcNow.Date;
        for (int i = 0; i < 7; i++)
        {
            _habitCompletions.Add(new HabitCompletion { Id = Guid.NewGuid(), HabitId = habit.Id, Date = today.AddDays(-i) });
        }

        _userAchievements.Add(new UserAchievement
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            AchievementCode = AchievementCodes.HabitStarter,
            UnlockedAt = DateTime.UtcNow.AddDays(-6),
            XpAwarded = 25
        });
        _userAchievements.Add(new UserAchievement
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            AchievementCode = AchievementCodes.StreakBuilder,
            UnlockedAt = DateTime.UtcNow.AddDays(-4),
            XpAwarded = 50
        });

        var unlocked = await _achievementService.EvaluateHabitAchievementsAsync(user.Id, habit, today);

        unlocked.Should().HaveCount(1);
        unlocked[0].Code.Should().Be(AchievementCodes.Unstoppable);
        unlocked[0].XpReward.Should().Be(100);

        _gamificationServiceMock.Verify(g => g.StageAchievementXpAsync(user, AchievementCodes.Unstoppable, 100, It.IsAny<CancellationToken>()), Times.Once);
    }

    // =========================================================================
    // 4. Exception & Duplicate Collision Handling
    // =========================================================================

    [Fact]
    public async Task EvaluateStudySession_WhenUserDoesNotExist_ThrowsUserNotFoundException()
    {
        var missingUserId = Guid.NewGuid();
        var session = new StudySession { Id = Guid.NewGuid(), UserId = missingUserId, DurationMinutes = 30, IsCompleted = true };

        Func<Task> act = async () => await _achievementService.EvaluateStudySessionAchievementsAsync(missingUserId, session);

        await act.Should().ThrowAsync<UserNotFoundException>();
    }

    [Theory]
    [InlineData("Duplicate entry for key 'IX_UserAchievements_UserId_AchievementCode'", 1062, true)]
    [InlineData("Duplicate entry for key 'UserAchievements.PRIMARY'", 1062, true)]
    [InlineData("Duplicate entry 'test' for key 'IX_Users_Email'", 1062, false)]
    [InlineData("Cannot add or update a child row: a foreign key constraint fails", 1452, false)]
    [InlineData("Deadlock found when trying to get lock", 1213, false)]
    public void IsAchievementDuplicateCollision_CorrectlyFiltersCollisionExceptions(string message, int errorCode, bool expectedResult)
    {
        var innerException = new ExceptionWithNumber(message, errorCode);
        var dbEx = new DbUpdateException(message, innerException);

        var isCollision = _achievementService.IsAchievementDuplicateCollision(dbEx);

        isCollision.Should().Be(expectedResult);
    }

    [Fact]
    public void HandleDuplicateCollision_WhenMockContext_ExecutesSafelyWithoutThrowing()
    {
        var userId = Guid.NewGuid();
        var act = () => _achievementService.HandleDuplicateCollision(userId, new[] { AchievementCodes.FirstStep });

        act.Should().NotThrow();
    }

    private class ExceptionWithNumber : Exception
    {
        public int Number { get; }

        public ExceptionWithNumber(string message, int number) : base(message)
        {
            Number = number;
        }
    }
}
