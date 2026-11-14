using Godot;

public partial class SurvivalStats : Node
{
	[Export] public float MaxDifficultyMultiplier { get; set; } = 1.8f;
	[Export] public float SecondsToReachMaxDifficulty { get; set; } = 900.0f; // ~15 minutes

	public double ElapsedSeconds { get; private set; }

	public override void _Ready()
	{
		AddToGroup("survival_stats");
	}

	public override void _Process(double delta)
	{
		ElapsedSeconds += delta;
	}

	public float GetDifficultyMultiplier()
	{
		if (SecondsToReachMaxDifficulty <= 0.0f)
			return 1.0f;
		float t = Mathf.Clamp((float)(ElapsedSeconds / SecondsToReachMaxDifficulty), 0.0f, 1.0f);
		// Ease in slowly, then approach cap.
		float eased = t * t * (3.0f - 2.0f * t);
		return Mathf.Lerp(1.0f, MaxDifficultyMultiplier, eased);
	}

	public string GetFormattedElapsedTime()
	{
		int total = Mathf.Max(0, (int)ElapsedSeconds);
		int hours = total / 3600;
		int minutes = (total % 3600) / 60;
		int seconds = total % 60;

		if (hours > 0)
			return $"{hours}:{minutes:D2}:{seconds:D2}";
		if (minutes > 0)
			return $"{minutes}:{seconds:D2}";
		return $"{seconds}s";
	}
}
