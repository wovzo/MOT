using System.Linq;
using FluentAssertions;
using MOT.Domain.Gamification;
using Xunit;

namespace MOT.Tests.Gamification;

public class AchievementCatalogTests
{
    [Fact]
    public void All_ShouldContainExactlyEightAchievements()
    {
        AchievementCatalog.All.Should().HaveCount(8);
    }

    [Theory]
    [InlineData(AchievementCodes.FirstStep, "First Step", "Complete your first daily task", "check_circle", 25)]
    [InlineData(AchievementCodes.FocusInitiate, "Focus Initiate", "Complete your first study session of at least 15 minutes", "timer", 25)]
    [InlineData(AchievementCodes.HabitStarter, "Habit Starter", "Complete your first habit", "repeat", 25)]
    [InlineData(AchievementCodes.CenturyClub, "Century Club", "Reach 100 total minutes of completed focus time", "hourglass_full", 50)]
    [InlineData(AchievementCodes.DeepDiver, "Deep Diver", "Complete a single focus session of at least 60 minutes", "scuba_diving", 50)]
    [InlineData(AchievementCodes.StreakBuilder, "Streak Builder", "Reach a 3-day streak on any habit", "local_fire_department", 50)]
    [InlineData(AchievementCodes.Unstoppable, "Unstoppable", "Reach a 7-day streak on any habit", "whatshot", 100)]
    [InlineData(AchievementCodes.TaskMaster, "Task Master", "Complete 10 distinct daily tasks", "task_alt", 75)]
    public void GetByCode_ShouldReturnExpectedDefinition(string code, string expectedTitle, string expectedDesc, string expectedIcon, int expectedXp)
    {
        var def = AchievementCatalog.GetByCode(code);

        def.Should().NotBeNull();
        def!.Code.Should().Be(code);
        def.Title.Should().Be(expectedTitle);
        def.Description.Should().Be(expectedDesc);
        def.IconName.Should().Be(expectedIcon);
        def.XpReward.Should().Be(expectedXp);
    }

    [Theory]
    [InlineData("first_step")]
    [InlineData("FIRST_STEP")]
    [InlineData("First_Step")]
    public void GetByCode_ShouldBeCaseInsensitive(string code)
    {
        var def = AchievementCatalog.GetByCode(code);
        def.Should().NotBeNull();
        def!.Code.Should().Be(AchievementCodes.FirstStep);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    [InlineData("UNKNOWN_ACHIEVEMENT")]
    public void GetByCode_ShouldReturnNullForInvalidCodes(string? code)
    {
        var def = AchievementCatalog.GetByCode(code!);
        def.Should().BeNull();
    }

    [Theory]
    [InlineData(AchievementCodes.FirstStep, true)]
    [InlineData(AchievementCodes.FocusInitiate, true)]
    [InlineData(AchievementCodes.HabitStarter, true)]
    [InlineData(AchievementCodes.CenturyClub, true)]
    [InlineData(AchievementCodes.DeepDiver, true)]
    [InlineData(AchievementCodes.StreakBuilder, true)]
    [InlineData(AchievementCodes.Unstoppable, true)]
    [InlineData(AchievementCodes.TaskMaster, true)]
    [InlineData("non_existent", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Contains_ShouldValidateCorrectly(string? code, bool expectedResult)
    {
        AchievementCatalog.Contains(code!).Should().Be(expectedResult);
    }

    [Fact]
    public void TotalAchievementXp_ShouldEqual400()
    {
        var totalXp = AchievementCatalog.All.Sum(a => a.XpReward);
        totalXp.Should().Be(400);
    }
}
