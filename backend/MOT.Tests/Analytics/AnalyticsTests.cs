using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using MOT.Api.Controllers;
using MOT.Application.Analytics.DTOs;
using MOT.Application.Analytics.Queries;
using MOT.Application.Common.Interfaces;
using MOT.Application.Users.Queries;
using MOT.Application.Validators;
using MOT.Domain.Entities;
using MOT.Domain.Interfaces;
using MOT.Tests.Common;
using Xunit;

namespace MOT.Tests.Analytics
{
    public class AnalyticsTests
    {
        private readonly Guid _userId = Guid.NewGuid();
        private readonly Guid _otherUserId = Guid.NewGuid();
        private readonly Mock<ICurrentUserService> _currentUserServiceMock;
        private readonly Mock<IAppDbContext> _dbContextMock;
        private readonly List<StudySession> _studySessions;
        private readonly List<DailyTask> _tasks;
        private readonly List<Habit> _habits;
        private readonly List<HabitCompletion> _habitCompletions;

        public AnalyticsTests()
        {
            _currentUserServiceMock = new Mock<ICurrentUserService>();
            _currentUserServiceMock.Setup(s => s.UserId).Returns(_userId.ToString());

            _studySessions = new List<StudySession>();
            _tasks = new List<DailyTask>();
            _habits = new List<Habit>();
            _habitCompletions = new List<HabitCompletion>();

            _dbContextMock = new Mock<IAppDbContext>();
            _dbContextMock.Setup(c => c.StudySessions).Returns(MockDbSetHelper.CreateMockDbSet(_studySessions).Object);
            _dbContextMock.Setup(c => c.Tasks).Returns(MockDbSetHelper.CreateMockDbSet(_tasks).Object);
            _dbContextMock.Setup(c => c.Habits).Returns(MockDbSetHelper.CreateMockDbSet(_habits).Object);
            _dbContextMock.Setup(c => c.HabitCompletions).Returns(MockDbSetHelper.CreateMockDbSet(_habitCompletions).Object);
        }

        // =========================================================================
        // 1. Focus Analytics Summary Handler Tests
        // =========================================================================

        [Fact]
        public async Task Summary_WhenUserHasSessionsTasksHabits_ReturnsAccurateMetrics()
        {
            var baseDate = new DateTime(2026, 10, 13, 12, 0, 0, DateTimeKind.Utc); // Tuesday

            // Completed session today
            _studySessions.Add(new StudySession
            {
                Id = Guid.NewGuid(),
                UserId = _userId,
                Title = "Math Focus",
                StartTime = baseDate.Date.AddHours(9),
                EndTime = baseDate.Date.AddHours(10),
                DurationMinutes = 60,
                IsCompleted = true
            });

            // Completed session 3 days ago (this week)
            _studySessions.Add(new StudySession
            {
                Id = Guid.NewGuid(),
                UserId = _userId,
                Title = "Physics Focus",
                StartTime = baseDate.Date.AddDays(-3).AddHours(14),
                EndTime = baseDate.Date.AddDays(-3).AddHours(15).AddMinutes(30),
                DurationMinutes = 90,
                IsCompleted = true
            });

            // Completed session 10 days ago (past week window)
            _studySessions.Add(new StudySession
            {
                Id = Guid.NewGuid(),
                UserId = _userId,
                Title = "Chemistry Focus",
                StartTime = baseDate.Date.AddDays(-10).AddHours(8),
                EndTime = baseDate.Date.AddDays(-10).AddHours(9),
                DurationMinutes = 60,
                IsCompleted = true
            });

            // Active/incomplete session today
            _studySessions.Add(new StudySession
            {
                Id = Guid.NewGuid(),
                UserId = _userId,
                Title = "Ongoing Focus",
                StartTime = baseDate.Date.AddHours(11),
                EndTime = null,
                DurationMinutes = 0,
                IsCompleted = false
            });

            // Tasks
            _tasks.Add(new DailyTask { Id = Guid.NewGuid(), UserId = _userId, Title = "Task 1", IsCompleted = true });
            _tasks.Add(new DailyTask { Id = Guid.NewGuid(), UserId = _userId, Title = "Task 2", IsCompleted = true });
            _tasks.Add(new DailyTask { Id = Guid.NewGuid(), UserId = _userId, Title = "Task 3", IsCompleted = false });

            // Habits
            var habit1 = new Habit { Id = Guid.NewGuid(), UserId = _userId, Title = "Read" };
            var habit2 = new Habit { Id = Guid.NewGuid(), UserId = _userId, Title = "Meditate" };
            habit1.Completions.Add(new HabitCompletion { Id = Guid.NewGuid(), HabitId = habit1.Id, Date = baseDate.Date });
            habit1.Completions.Add(new HabitCompletion { Id = Guid.NewGuid(), HabitId = habit1.Id, Date = baseDate.Date.AddDays(-1) });
            habit2.Completions.Add(new HabitCompletion { Id = Guid.NewGuid(), HabitId = habit2.Id, Date = baseDate.Date.AddDays(-2) });

            _habits.Add(habit1);
            _habits.Add(habit2);

            var handler = new GetFocusAnalyticsSummaryQueryHandler(_dbContextMock.Object, _currentUserServiceMock.Object);
            var result = await handler.Handle(new GetFocusAnalyticsSummaryQuery { TodayOverride = baseDate.Date }, CancellationToken.None);

            // Assertions
            result.TotalFocusMinutes.Should().Be(210); // 60 + 90 + 60
            result.TodayFocusMinutes.Should().Be(60);
            result.ThisWeekFocusMinutes.Should().Be(150); // Today (60) + 3 days ago (90)
            result.TotalSessionsCount.Should().Be(4); // 3 completed + 1 active
            result.CompletedSessionsCount.Should().Be(3);
            result.IncompleteSessionsCount.Should().Be(1);
            result.AverageSessionMinutes.Should().Be(70.0); // 210 / 3

            result.Tasks.TotalTasks.Should().Be(3);
            result.Tasks.CompletedTasks.Should().Be(2);
            result.Tasks.PendingTasks.Should().Be(1);
            result.Tasks.CompletionRatePercentage.Should().Be(66.7);

            result.Habits.TotalHabits.Should().Be(2);
            result.Habits.TotalCompletions.Should().Be(3);
            result.Habits.CompletionsToday.Should().Be(1);
            result.Habits.CompletionsThisWeek.Should().Be(3);
            result.Habits.ActiveStreaksCount.Should().Be(1); // habit1 has streak 2; habit2 missed yesterday & today
        }

        [Fact]
        public async Task Summary_ExcludesIncompleteSessionsFromCompletedTotals()
        {
            var baseDate = new DateTime(2026, 10, 13, 0, 0, 0, DateTimeKind.Utc);

            _studySessions.Add(new StudySession
            {
                Id = Guid.NewGuid(),
                UserId = _userId,
                Title = "Active Session",
                StartTime = baseDate.AddMinutes(-30),
                EndTime = null,
                DurationMinutes = 0,
                IsCompleted = false
            });

            var handler = new GetFocusAnalyticsSummaryQueryHandler(_dbContextMock.Object, _currentUserServiceMock.Object);
            var result = await handler.Handle(new GetFocusAnalyticsSummaryQuery { TodayOverride = baseDate }, CancellationToken.None);

            result.TotalFocusMinutes.Should().Be(0);
            result.TodayFocusMinutes.Should().Be(0);
            result.ThisWeekFocusMinutes.Should().Be(0);
            result.CompletedSessionsCount.Should().Be(0);
            result.IncompleteSessionsCount.Should().Be(1);
            result.TotalSessionsCount.Should().Be(1);
            result.AverageSessionMinutes.Should().Be(0.0);
        }

        [Fact]
        public async Task Summary_TotalFocusMinutes_RegressionMatchesProfileCalculation()
        {
            var baseDate = DateTime.UtcNow.Date;

            _studySessions.Add(new StudySession
            {
                Id = Guid.NewGuid(),
                UserId = _userId,
                Title = "Completed Session",
                StartTime = baseDate.AddHours(2),
                EndTime = baseDate.AddHours(3),
                DurationMinutes = 60,
                IsCompleted = true
            });

            var analyticsHandler = new GetFocusAnalyticsSummaryQueryHandler(_dbContextMock.Object, _currentUserServiceMock.Object);
            var analyticsResult = await analyticsHandler.Handle(new GetFocusAnalyticsSummaryQuery(), CancellationToken.None);

            var authRepoMock = new Mock<IAuthRepository>();
            authRepoMock.Setup(r => r.GetUserByIdAsync(_userId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new User { Id = _userId, Email = "u@test.com", DisplayName = "User" });

            var profileHandler = new GetCurrentUserQueryHandler(authRepoMock.Object, _currentUserServiceMock.Object, _dbContextMock.Object);
            var profileResult = await profileHandler.Handle(new GetCurrentUserQuery(), CancellationToken.None);

            analyticsResult.TotalFocusMinutes.Should().Be(profileResult.TotalFocusMinutes);
        }

        [Fact]
        public async Task Summary_CrossUserIsolation_ExcludesOtherUsersData()
        {
            var baseDate = DateTime.UtcNow.Date;

            // Current User
            _studySessions.Add(new StudySession
            {
                Id = Guid.NewGuid(),
                UserId = _userId,
                Title = "User Focus",
                StartTime = baseDate,
                EndTime = baseDate.AddMinutes(30),
                DurationMinutes = 30,
                IsCompleted = true
            });
            _tasks.Add(new DailyTask { Id = Guid.NewGuid(), UserId = _userId, Title = "My Task", IsCompleted = true });
            var myHabit = new Habit { Id = Guid.NewGuid(), UserId = _userId, Title = "My Habit" };
            myHabit.Completions.Add(new HabitCompletion { Id = Guid.NewGuid(), HabitId = myHabit.Id, Date = baseDate });
            _habits.Add(myHabit);

            // Other User
            _studySessions.Add(new StudySession
            {
                Id = Guid.NewGuid(),
                UserId = _otherUserId,
                Title = "Other Focus",
                StartTime = baseDate,
                EndTime = baseDate.AddMinutes(500),
                DurationMinutes = 500,
                IsCompleted = true
            });
            _tasks.Add(new DailyTask { Id = Guid.NewGuid(), UserId = _otherUserId, Title = "Other Task", IsCompleted = true });
            var otherHabit = new Habit { Id = Guid.NewGuid(), UserId = _otherUserId, Title = "Other Habit" };
            otherHabit.Completions.Add(new HabitCompletion { Id = Guid.NewGuid(), HabitId = otherHabit.Id, Date = baseDate });
            _habits.Add(otherHabit);

            var handler = new GetFocusAnalyticsSummaryQueryHandler(_dbContextMock.Object, _currentUserServiceMock.Object);
            var result = await handler.Handle(new GetFocusAnalyticsSummaryQuery { TodayOverride = baseDate }, CancellationToken.None);

            result.TotalFocusMinutes.Should().Be(30);
            result.Tasks.TotalTasks.Should().Be(1);
            result.Habits.TotalHabits.Should().Be(1);
            result.Habits.TotalCompletions.Should().Be(1);
        }

        [Fact]
        public async Task Summary_WhenNoData_ReturnsAllZeroMetricsWithoutException()
        {
            var handler = new GetFocusAnalyticsSummaryQueryHandler(_dbContextMock.Object, _currentUserServiceMock.Object);
            var result = await handler.Handle(new GetFocusAnalyticsSummaryQuery(), CancellationToken.None);

            result.TotalFocusMinutes.Should().Be(0);
            result.TodayFocusMinutes.Should().Be(0);
            result.ThisWeekFocusMinutes.Should().Be(0);
            result.TotalSessionsCount.Should().Be(0);
            result.CompletedSessionsCount.Should().Be(0);
            result.IncompleteSessionsCount.Should().Be(0);
            result.AverageSessionMinutes.Should().Be(0.0);
            result.Tasks.TotalTasks.Should().Be(0);
            result.Tasks.CompletionRatePercentage.Should().Be(0.0);
            result.Habits.TotalHabits.Should().Be(0);
            result.Habits.ActiveStreaksCount.Should().Be(0);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("invalid-guid")]
        public async Task Summary_WhenUnauthenticated_ThrowsUnauthorizedAccessException(string? invalidUserId)
        {
            _currentUserServiceMock.Setup(s => s.UserId).Returns(invalidUserId);
            var handler = new GetFocusAnalyticsSummaryQueryHandler(_dbContextMock.Object, _currentUserServiceMock.Object);

            Func<Task> act = async () => await handler.Handle(new GetFocusAnalyticsSummaryQuery(), CancellationToken.None);
            await act.Should().ThrowAsync<UnauthorizedAccessException>();
        }

        // =========================================================================
        // 2. Daily Analytics Handler & Validator Tests
        // =========================================================================

        [Fact]
        public async Task Daily_ReturnsContiguousDailySeriesWithAccurateTotals()
        {
            var start = new DateTime(2026, 10, 10, 0, 0, 0, DateTimeKind.Utc);
            var end = new DateTime(2026, 10, 12, 0, 0, 0, DateTimeKind.Utc); // 3 days

            // Day 1 (Oct 10): 45 mins focus
            _studySessions.Add(new StudySession
            {
                Id = Guid.NewGuid(),
                UserId = _userId,
                StartTime = start.AddHours(10),
                EndTime = start.AddHours(10).AddMinutes(45),
                DurationMinutes = 45,
                IsCompleted = true
            });

            // Day 2 (Oct 11): 0 mins focus, 1 habit completion
            var habit = new Habit { Id = Guid.NewGuid(), UserId = _userId, Title = "Hydrate" };
            _habits.Add(habit);
            _habitCompletions.Add(new HabitCompletion { Id = Guid.NewGuid(), Habit = habit, HabitId = habit.Id, Date = new DateTime(2026, 10, 11, 0, 0, 0, DateTimeKind.Utc) });

            // Day 3 (Oct 12): 60 mins focus, 1 habit completion
            _studySessions.Add(new StudySession
            {
                Id = Guid.NewGuid(),
                UserId = _userId,
                StartTime = end.AddHours(14),
                EndTime = end.AddHours(15),
                DurationMinutes = 60,
                IsCompleted = true
            });
            _habitCompletions.Add(new HabitCompletion { Id = Guid.NewGuid(), Habit = habit, HabitId = habit.Id, Date = end });

            var handler = new GetDailyAnalyticsQueryHandler(_dbContextMock.Object, _currentUserServiceMock.Object);
            var result = await handler.Handle(new GetDailyAnalyticsQuery { StartDate = start, EndDate = end }, CancellationToken.None);

            result.StartDate.Should().Be("2026-10-10");
            result.EndDate.Should().Be("2026-10-12");
            result.TotalFocusMinutes.Should().Be(105);
            result.TotalCompletedSessions.Should().Be(2);
            result.TotalHabitCompletions.Should().Be(2);

            result.DailyBreakdown.Should().HaveCount(3);
            result.DailyBreakdown[0].Date.Should().Be("2026-10-10");
            result.DailyBreakdown[0].FocusMinutes.Should().Be(45);
            result.DailyBreakdown[0].HabitCompletionsCount.Should().Be(0);

            result.DailyBreakdown[1].Date.Should().Be("2026-10-11");
            result.DailyBreakdown[1].FocusMinutes.Should().Be(0);
            result.DailyBreakdown[1].HabitCompletionsCount.Should().Be(1);

            result.DailyBreakdown[2].Date.Should().Be("2026-10-12");
            result.DailyBreakdown[2].FocusMinutes.Should().Be(60);
            result.DailyBreakdown[2].HabitCompletionsCount.Should().Be(1);
        }

        [Fact]
        public async Task Daily_UtcBoundaryHandling_CorrectlyBucketsSessionsNearMidnight()
        {
            var date = new DateTime(2026, 10, 13, 0, 0, 0, DateTimeKind.Utc);

            // Session started at 23:59:00 on Oct 12
            _studySessions.Add(new StudySession
            {
                Id = Guid.NewGuid(),
                UserId = _userId,
                StartTime = date.AddSeconds(-60), // 2026-10-12 23:59:00 UTC
                EndTime = date.AddMinutes(29),
                DurationMinutes = 30,
                IsCompleted = true
            });

            // Session started at 00:00:01 on Oct 13
            _studySessions.Add(new StudySession
            {
                Id = Guid.NewGuid(),
                UserId = _userId,
                StartTime = date.AddSeconds(1), // 2026-10-13 00:00:01 UTC
                EndTime = date.AddMinutes(30),
                DurationMinutes = 30,
                IsCompleted = true
            });

            var handler = new GetDailyAnalyticsQueryHandler(_dbContextMock.Object, _currentUserServiceMock.Object);
            var result = await handler.Handle(new GetDailyAnalyticsQuery { StartDate = date.AddDays(-1), EndDate = date }, CancellationToken.None);

            result.DailyBreakdown.Should().HaveCount(2);
            result.DailyBreakdown[0].Date.Should().Be("2026-10-12");
            result.DailyBreakdown[0].FocusMinutes.Should().Be(30);

            result.DailyBreakdown[1].Date.Should().Be("2026-10-13");
            result.DailyBreakdown[1].FocusMinutes.Should().Be(30);
        }

        [Fact]
        public async Task Daily_ExcludesIncompleteSessionsAndOtherUsers()
        {
            var start = DateTime.UtcNow.Date.AddDays(-2);
            var end = DateTime.UtcNow.Date;

            // Incomplete session for current user
            _studySessions.Add(new StudySession
            {
                Id = Guid.NewGuid(),
                UserId = _userId,
                StartTime = start.AddHours(2),
                EndTime = null,
                DurationMinutes = 0,
                IsCompleted = false
            });

            // Other user session
            _studySessions.Add(new StudySession
            {
                Id = Guid.NewGuid(),
                UserId = _otherUserId,
                StartTime = start.AddHours(3),
                EndTime = start.AddHours(4),
                DurationMinutes = 60,
                IsCompleted = true
            });

            var handler = new GetDailyAnalyticsQueryHandler(_dbContextMock.Object, _currentUserServiceMock.Object);
            var result = await handler.Handle(new GetDailyAnalyticsQuery { StartDate = start, EndDate = end }, CancellationToken.None);

            result.TotalFocusMinutes.Should().Be(0);
            result.TotalCompletedSessions.Should().Be(0);
        }

        [Fact]
        public async Task Daily_WhenNoActivityInRange_ReturnsAllZeroPointsForEveryDay()
        {
            var start = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
            var end = new DateTime(2026, 10, 5, 0, 0, 0, DateTimeKind.Utc); // 5 days

            var handler = new GetDailyAnalyticsQueryHandler(_dbContextMock.Object, _currentUserServiceMock.Object);
            var result = await handler.Handle(new GetDailyAnalyticsQuery { StartDate = start, EndDate = end }, CancellationToken.None);

            result.DailyBreakdown.Should().HaveCount(5);
            result.TotalFocusMinutes.Should().Be(0);
            result.TotalCompletedSessions.Should().Be(0);
            result.TotalHabitCompletions.Should().Be(0);
            result.DailyBreakdown.All(d => d.FocusMinutes == 0 && d.CompletedSessionsCount == 0 && d.HabitCompletionsCount == 0).Should().BeTrue();
        }

        [Fact]
        public async Task Daily_WhenNegativeSessionDurationExists_FloorsMinutesAtZero()
        {
            var start = new DateTime(2026, 10, 10, 0, 0, 0, DateTimeKind.Utc);
            var end = new DateTime(2026, 10, 10, 0, 0, 0, DateTimeKind.Utc);

            _studySessions.Add(new StudySession
            {
                Id = Guid.NewGuid(),
                UserId = _userId,
                StartTime = start.AddHours(10),
                EndTime = start.AddHours(10),
                DurationMinutes = -15, // Clock skew/corrupted duration
                IsCompleted = true
            });

            var handler = new GetDailyAnalyticsQueryHandler(_dbContextMock.Object, _currentUserServiceMock.Object);
            var result = await handler.Handle(new GetDailyAnalyticsQuery { StartDate = start, EndDate = end }, CancellationToken.None);

            result.TotalFocusMinutes.Should().Be(0);
            result.DailyBreakdown.Single().FocusMinutes.Should().Be(0);
            result.TotalCompletedSessions.Should().Be(1);
        }

        [Fact]
        public void DailyValidator_EnforcesDateOrderAndMaxRangeRules()
        {
            var validator = new GetDailyAnalyticsQueryValidator();

            // Start > End -> invalid
            var invalidOrder = new GetDailyAnalyticsQuery
            {
                StartDate = DateTime.UtcNow.Date,
                EndDate = DateTime.UtcNow.Date.AddDays(-1)
            };
            validator.Validate(invalidOrder).IsValid.Should().BeFalse();

            // Range > 90 days -> invalid
            var rangeTooLong = new GetDailyAnalyticsQuery
            {
                StartDate = DateTime.UtcNow.Date.AddDays(-95),
                EndDate = DateTime.UtcNow.Date
            };
            validator.Validate(rangeTooLong).IsValid.Should().BeFalse();

            // Valid range <= 90 days
            var valid = new GetDailyAnalyticsQuery
            {
                StartDate = DateTime.UtcNow.Date.AddDays(-30),
                EndDate = DateTime.UtcNow.Date
            };
            validator.Validate(valid).IsValid.Should().BeTrue();
        }

        // =========================================================================
        // 3. Weekly Analytics Handler & Validator Tests
        // =========================================================================

        [Fact]
        public async Task Weekly_CalculatesChronologicalBucketsWithAccurateTotals()
        {
            var baseEnd = new DateTime(2026, 10, 13, 0, 0, 0, DateTimeKind.Utc); // Tuesday

            // Session in Week 4 (current week: Oct 07 - Oct 13)
            _studySessions.Add(new StudySession
            {
                Id = Guid.NewGuid(),
                UserId = _userId,
                StartTime = baseEnd.AddDays(-1).AddHours(10), // Oct 12
                EndTime = baseEnd.AddDays(-1).AddHours(11),
                DurationMinutes = 60,
                IsCompleted = true
            });

            // Session in Week 3 (previous week: Sep 30 - Oct 06)
            _studySessions.Add(new StudySession
            {
                Id = Guid.NewGuid(),
                UserId = _userId,
                StartTime = baseEnd.AddDays(-9).AddHours(14), // Oct 04
                EndTime = baseEnd.AddDays(-9).AddHours(15),
                DurationMinutes = 60,
                IsCompleted = true
            });

            var handler = new GetWeeklyAnalyticsQueryHandler(_dbContextMock.Object, _currentUserServiceMock.Object);
            var result = await handler.Handle(new GetWeeklyAnalyticsQuery { Weeks = 4, EndDate = baseEnd }, CancellationToken.None);

            result.Weeks.Should().HaveCount(4);
            result.TotalFocusMinutes.Should().Be(120);
            result.TotalCompletedSessions.Should().Be(2);

            // Weeks should be chronological: oldest week (Week 1) to newest week (Week 4)
            result.Weeks[3].FocusMinutes.Should().Be(60); // Current week
            result.Weeks[2].FocusMinutes.Should().Be(60); // Previous week
            result.Weeks[1].FocusMinutes.Should().Be(0);
            result.Weeks[0].FocusMinutes.Should().Be(0);
        }

        [Fact]
        public async Task Weekly_WhenNegativeSessionDurationExists_FloorsMinutesAtZero()
        {
            var baseEnd = new DateTime(2026, 10, 13, 0, 0, 0, DateTimeKind.Utc);

            _studySessions.Add(new StudySession
            {
                Id = Guid.NewGuid(),
                UserId = _userId,
                StartTime = baseEnd.AddDays(-2),
                EndTime = baseEnd.AddDays(-2),
                DurationMinutes = -30,
                IsCompleted = true
            });

            var handler = new GetWeeklyAnalyticsQueryHandler(_dbContextMock.Object, _currentUserServiceMock.Object);
            var result = await handler.Handle(new GetWeeklyAnalyticsQuery { Weeks = 1, EndDate = baseEnd }, CancellationToken.None);

            result.Weeks.Should().HaveCount(1);
            result.TotalFocusMinutes.Should().Be(0);
            result.Weeks[0].FocusMinutes.Should().Be(0);
            result.TotalCompletedSessions.Should().Be(1);
        }

        [Fact]
        public void WeeklyValidator_RejectsOutOfRangeWeeks()
        {
            var validator = new GetWeeklyAnalyticsQueryValidator();

            validator.Validate(new GetWeeklyAnalyticsQuery { Weeks = 0 }).IsValid.Should().BeFalse();
            validator.Validate(new GetWeeklyAnalyticsQuery { Weeks = 13 }).IsValid.Should().BeFalse();
            validator.Validate(new GetWeeklyAnalyticsQuery { Weeks = 1 }).IsValid.Should().BeTrue();
            validator.Validate(new GetWeeklyAnalyticsQuery { Weeks = 12 }).IsValid.Should().BeTrue();
        }

        // =========================================================================
        // 4. Controller HTTP Semantics Tests
        // =========================================================================

        [Fact]
        public async Task Controller_GetSummary_Success_Returns200WithDto()
        {
            var mediatorMock = new Mock<IMediator>();
            var loggerMock = new Mock<ILogger<AnalyticsController>>();
            var expected = new FocusAnalyticsSummaryDto { TotalFocusMinutes = 120 };

            mediatorMock.Setup(m => m.Send(It.IsAny<GetFocusAnalyticsSummaryQuery>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expected);

            var controller = new AnalyticsController(mediatorMock.Object, loggerMock.Object);
            var result = await controller.GetSummary();

            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            okResult.StatusCode.Should().Be(200);
            okResult.Value.Should().Be(expected);
        }

        [Fact]
        public async Task Controller_GetSummary_Unauthenticated_Returns401()
        {
            var mediatorMock = new Mock<IMediator>();
            var loggerMock = new Mock<ILogger<AnalyticsController>>();

            mediatorMock.Setup(m => m.Send(It.IsAny<GetFocusAnalyticsSummaryQuery>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new UnauthorizedAccessException());

            var controller = new AnalyticsController(mediatorMock.Object, loggerMock.Object);
            var result = await controller.GetSummary();

            var unauthResult = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
            unauthResult.StatusCode.Should().Be(401);
        }

        [Fact]
        public async Task Controller_GetDaily_ValidationFailure_Returns400()
        {
            var mediatorMock = new Mock<IMediator>();
            var loggerMock = new Mock<ILogger<AnalyticsController>>();

            mediatorMock.Setup(m => m.Send(It.IsAny<GetDailyAnalyticsQuery>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new ValidationException(new[] { new ValidationFailure("StartDate", "StartDate cannot be after EndDate.") }));

            var controller = new AnalyticsController(mediatorMock.Object, loggerMock.Object);
            var result = await controller.GetDaily(DateTime.UtcNow, DateTime.UtcNow.AddDays(-1));

            var badResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
            badResult.StatusCode.Should().Be(400);
        }

        [Fact]
        public async Task Controller_GetWeekly_Success_Returns200WithDto()
        {
            var mediatorMock = new Mock<IMediator>();
            var loggerMock = new Mock<ILogger<AnalyticsController>>();
            var expected = new WeeklyAnalyticsDto { TotalFocusMinutes = 240 };

            mediatorMock.Setup(m => m.Send(It.IsAny<GetWeeklyAnalyticsQuery>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expected);

            var controller = new AnalyticsController(mediatorMock.Object, loggerMock.Object);
            var result = await controller.GetWeekly(4);

            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            okResult.StatusCode.Should().Be(200);
            okResult.Value.Should().Be(expected);
        }
    }
}
