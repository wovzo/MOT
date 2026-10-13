namespace MOT.Application.Gamification;

public class GamificationOptions
{
    public int StudySessionMinMinutes { get; set; } = 5;
    public int StudySessionBaseXp { get; set; } = 10;
    public int StudySessionMinutesPerXp { get; set; } = 2;
    public int StudySessionMaxXp { get; set; } = 70;
    public int TaskCompletionXp { get; set; } = 15;
    public int HabitCompletionXp { get; set; } = 20;
}
