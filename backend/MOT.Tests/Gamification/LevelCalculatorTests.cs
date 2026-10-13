using System;
using FluentAssertions;
using MOT.Domain.Gamification;
using Xunit;

namespace MOT.Tests.Gamification;

public class LevelCalculatorTests
{
    // =========================================================================
    // 1. Threshold Calculations: Threshold(L) = 50 * L * (L - 1)
    // =========================================================================

    [Theory]
    [InlineData(1, 0L)]
    [InlineData(2, 100L)]
    [InlineData(3, 300L)]
    [InlineData(4, 600L)]
    [InlineData(5, 1000L)]
    [InlineData(6, 1500L)]
    [InlineData(7, 2100L)]
    [InlineData(8, 2800L)]
    [InlineData(9, 3600L)]
    [InlineData(10, 4500L)]
    public void GetThresholdForLevel_CalculatesExpectedFormulaValues(int level, long expectedThreshold)
    {
        var threshold = LevelCalculator.GetThresholdForLevel(level);
        threshold.Should().Be(expectedThreshold);
    }

    [Theory]
    [InlineData(0, 0L)]
    [InlineData(-1, 0L)]
    [InlineData(-100, 0L)]
    public void GetThresholdForLevel_WhenLevelZeroOrNegative_ReturnsZero(int level, long expectedThreshold)
    {
        var threshold = LevelCalculator.GetThresholdForLevel(level);
        threshold.Should().Be(expectedThreshold);
    }

    // =========================================================================
    // 2. XP Required for Level: Delta(L) = 100 * L
    // =========================================================================

    [Theory]
    [InlineData(1, 100)]
    [InlineData(2, 200)]
    [InlineData(3, 300)]
    [InlineData(4, 400)]
    [InlineData(5, 500)]
    [InlineData(10, 1000)]
    public void GetXpRequiredForLevel_ReturnsCorrectDelta(int level, int expectedRequired)
    {
        var required = LevelCalculator.GetXpRequiredForLevel(level);
        required.Should().Be(expectedRequired);
    }

    // =========================================================================
    // 3. Zero and Negative XP Handling
    // =========================================================================

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-50)]
    [InlineData(-1000)]
    [InlineData(int.MinValue)]
    public void CalculateLevel_WhenZeroOrNegative_ReturnsLevelOne(int totalXp)
    {
        var level = LevelCalculator.CalculateLevel(totalXp);
        level.Should().Be(1);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void GetProgress_WhenZeroOrNegative_ClampsToZeroAndReturnsLevelOneProgress(int totalXp)
    {
        var progress = LevelCalculator.GetProgress(totalXp);

        progress.Level.Should().Be(1);
        progress.TotalXp.Should().Be(0);
        progress.CurrentLevelThresholdXp.Should().Be(0L);
        progress.NextLevelThresholdXp.Should().Be(100L);
        progress.XpEarnedInCurrentLevel.Should().Be(0);
        progress.XpRequiredForNextLevel.Should().Be(100);
        progress.ProgressFraction.Should().Be(0.0);
    }

    // =========================================================================
    // 4. Exact Boundary Transitions and Adjacency Tests
    // =========================================================================

    [Theory]
    // Level 1 boundary (0 to 99)
    [InlineData(0, 1)]
    [InlineData(1, 1)]
    [InlineData(50, 1)]
    [InlineData(99, 1)]
    // Level 2 boundary (100 to 299)
    [InlineData(100, 2)]
    [InlineData(101, 2)]
    [InlineData(200, 2)]
    [InlineData(299, 2)]
    // Level 3 boundary (300 to 599)
    [InlineData(300, 3)]
    [InlineData(301, 3)]
    [InlineData(450, 3)]
    [InlineData(599, 3)]
    // Level 4 boundary (600 to 999)
    [InlineData(600, 4)]
    [InlineData(601, 4)]
    [InlineData(800, 4)]
    [InlineData(999, 4)]
    // Level 5 boundary (1000 to 1499)
    [InlineData(1000, 5)]
    [InlineData(1001, 5)]
    [InlineData(1499, 5)]
    // Level 6 boundary (1500 to 2099)
    [InlineData(1500, 6)]
    [InlineData(1501, 6)]
    [InlineData(2099, 6)]
    public void CalculateLevel_ExactBoundaries_ReturnsExpectedLevel(int totalXp, int expectedLevel)
    {
        var level = LevelCalculator.CalculateLevel(totalXp);
        level.Should().Be(expectedLevel);
    }

    // =========================================================================
    // 5. Progress Fraction and Earned XP Tests (0%, 50%, 100% boundaries)
    // =========================================================================

    [Fact]
    public void GetProgress_LevelOne_FiftyPercent_ReturnsExpectedFraction()
    {
        var progress = LevelCalculator.GetProgress(50);

        progress.Level.Should().Be(1);
        progress.TotalXp.Should().Be(50);
        progress.CurrentLevelThresholdXp.Should().Be(0L);
        progress.NextLevelThresholdXp.Should().Be(100L);
        progress.XpEarnedInCurrentLevel.Should().Be(50);
        progress.XpRequiredForNextLevel.Should().Be(100);
        progress.ProgressFraction.Should().Be(0.5);
    }

    [Fact]
    public void GetProgress_LevelTwo_FiftyPercent_ReturnsExpectedFraction()
    {
        // Level 2 range: 100 to 300, delta = 200. Midpoint = 100 + 100 = 200 XP
        var progress = LevelCalculator.GetProgress(200);

        progress.Level.Should().Be(2);
        progress.TotalXp.Should().Be(200);
        progress.CurrentLevelThresholdXp.Should().Be(100L);
        progress.NextLevelThresholdXp.Should().Be(300L);
        progress.XpEarnedInCurrentLevel.Should().Be(100);
        progress.XpRequiredForNextLevel.Should().Be(200);
        progress.ProgressFraction.Should().Be(0.5);
    }

    [Fact]
    public void GetProgress_ImmediatelyBeforeLevelUp_ReturnsFractionCloseToOne()
    {
        // 99 XP is 99% of Level 1
        var p1 = LevelCalculator.GetProgress(99);
        p1.Level.Should().Be(1);
        p1.XpEarnedInCurrentLevel.Should().Be(99);
        p1.ProgressFraction.Should().Be(0.99);

        // 299 XP is 199/200 = 0.995 of Level 2
        var p2 = LevelCalculator.GetProgress(299);
        p2.Level.Should().Be(2);
        p2.XpEarnedInCurrentLevel.Should().Be(199);
        p2.ProgressFraction.Should().Be(0.995);

        // 599 XP is 299/300 of Level 3
        var p3 = LevelCalculator.GetProgress(599);
        p3.Level.Should().Be(3);
        p3.XpEarnedInCurrentLevel.Should().Be(299);
        p3.ProgressFraction.Should().BeApproximately(0.996666, 0.0001);
    }

    [Fact]
    public void GetProgress_ExactlyAtLevelThreshold_ResetsFractionToZero()
    {
        var p2 = LevelCalculator.GetProgress(100);
        p2.Level.Should().Be(2);
        p2.XpEarnedInCurrentLevel.Should().Be(0);
        p2.ProgressFraction.Should().Be(0.0);

        var p3 = LevelCalculator.GetProgress(300);
        p3.Level.Should().Be(3);
        p3.XpEarnedInCurrentLevel.Should().Be(0);
        p3.ProgressFraction.Should().Be(0.0);

        var p4 = LevelCalculator.GetProgress(600);
        p4.Level.Should().Be(4);
        p4.XpEarnedInCurrentLevel.Should().Be(0);
        p4.ProgressFraction.Should().Be(0.0);
    }

    // =========================================================================
    // 6. Large XP Values and Overflow Protection
    // =========================================================================

    [Theory]
    [InlineData(10000, 14)]          // Threshold(14) = 50 * 14 * 13 = 9,100 <= 10,000 < Threshold(15) = 10,500 -> Level 14
    [InlineData(100000, 45)]        // Threshold(45) = 50 * 45 * 44 = 99,000 <= 100,000 < Threshold(46) = 103,500 -> Level 45
    [InlineData(1000000, 141)]      // Threshold(141) = 50 * 141 * 140 = 987,000 <= 1M < Threshold(142) = 1,001,100 -> Level 141
    public void CalculateLevel_LargeValues_ComputesDeterministically(int totalXp, int expectedLevel)
    {
        var level = LevelCalculator.CalculateLevel(totalXp);
        level.Should().Be(expectedLevel);

        var thresholdCurrent = LevelCalculator.GetThresholdForLevel(level);
        var thresholdNext = LevelCalculator.GetThresholdForLevel(level + 1);

        totalXp.Should().BeGreaterThanOrEqualTo((int)thresholdCurrent);
        totalXp.Should().BeLessThan((int)thresholdNext);
    }

    [Fact]
    public void CalculateLevel_AtIntMaxValue_DoesNotOverflowAndReturnsCorrectLevel()
    {
        int maxInt = int.MaxValue; // 2,147,483,647

        var level = LevelCalculator.CalculateLevel(maxInt);
        level.Should().Be(6554);

        long threshold6554 = LevelCalculator.GetThresholdForLevel(6554);
        long threshold6555 = LevelCalculator.GetThresholdForLevel(6555);

        // Threshold(6554) = 50 * 6554 * 6553 = 2,147,418,100 <= maxInt
        threshold6554.Should().Be(2147418100L);
        threshold6554.Should().BeLessThanOrEqualTo(maxInt);

        // Threshold(6555) = 50 * 6555 * 6554 = 2,148,073,500 > maxInt
        threshold6555.Should().Be(2148073500L);
        threshold6555.Should().BeGreaterThan(maxInt);

        // Verify GetProgress at int.MaxValue
        var progress = LevelCalculator.GetProgress(maxInt);
        progress.Level.Should().Be(6554);
        progress.TotalXp.Should().Be(maxInt);
        progress.XpEarnedInCurrentLevel.Should().Be((int)(maxInt - threshold6554));
        progress.ProgressFraction.Should().BeInRange(0.0, 1.0);
    }
}
