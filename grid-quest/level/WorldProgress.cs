using Godot;
using System;
using System.IO;
using System.Text.Json;

public static class WorldProgress
{
	private sealed class ProgressData
	{
		public int WorldResetHighScore { get; set; }
	}

	private static ProgressData _data = Load();
	private static int _runWorldResets;
	private static int _runCurrentWorldDay = 1;
	private static int _runLastBossWarpMilestone; // 3,6,9... completed/triggered for this run

	public static int WorldResets => _runWorldResets; // run-local (not historical)
	public static int WorldResetHighScore => _data.WorldResetHighScore;
	public static int CurrentWorldDay => Math.Max(1, _runCurrentWorldDay);

	public static int GetWorldResetCost() => 50 + WorldResets * 50;

	public static void RegisterWorldDay(int dayNumber)
	{
		int clamped = Math.Max(1, dayNumber);
		if (clamped <= _runCurrentWorldDay) return;
		_runCurrentWorldDay = clamped;
	}

	public static void RegisterResetHighScoreProgress()
	{
		// "How far you could get" / "number you get to even if you die on it"
		// Count the world/reset number being reached (current + 1 when advancing).
		int reached = WorldResets + 1;
		if (reached <= _data.WorldResetHighScore) return;
		_data.WorldResetHighScore = reached;
		Save();
	}

	public static int GetNextBossWarpMilestone() => ((_runWorldResets / 3) + 1) * 3;

	public static bool HasPendingBossForCurrentWarp()
	{
		// Bosses occur every 3rd warp after (3,6,9...) on the sunset of that warp/world spawn.
		return _runWorldResets >= 3 && _runWorldResets % 3 == 0 && _runLastBossWarpMilestone < _runWorldResets;
	}

	public static int GetPendingBossIndex()
	{
		return HasPendingBossForCurrentWarp() ? (_runWorldResets / 3) : 0; // 1-based boss index
	}

	public static void MarkPendingBossCompleted()
	{
		if (HasPendingBossForCurrentWarp())
			_runLastBossWarpMilestone = _runWorldResets;
	}

	public static void DebugPrepareHellhoundBossTrigger()
	{
		DebugPrepareBossTrigger(1);
	}

	public static void DebugPrepareExecutionerBossTrigger()
	{
		DebugPrepareBossTrigger(2);
	}

	public static void DebugPrepareBossTrigger(int bossIndex)
	{
		int warp = Mathf.Max(3, bossIndex * 3);
		_runWorldResets = warp;
		_runCurrentWorldDay = 1;
		_runLastBossWarpMilestone = warp - 3;
	}

	public static void CompleteWorldReset()
	{
		RegisterResetHighScoreProgress();
		_runWorldResets++;
		_runCurrentWorldDay = 1;
	}

	public static void ResetRunProgressForNewWorldSpawn()
	{
		_runCurrentWorldDay = 1;
	}

	public static void ResetCurrentRunForFreshStart()
	{
		// World reset count is per-run, not historical. Day resets for a clean start.
		_runWorldResets = 0;
		_runCurrentWorldDay = 1;
		_runLastBossWarpMilestone = 0;
	}

	public static void ClearHighScore()
	{
		_data.WorldResetHighScore = 0;
		Save();
	}

	public static float GetChestLootMultiplier(int? worldDayOverride = null)
	{
		int day = Math.Max(1, worldDayOverride ?? CurrentWorldDay);
		// Increase base loot overall (handled in Chest), but fall off rapidly to nothing after 3rd day per spawn.
		return day switch
		{
			1 => 1.0f,
			2 => 0.7f,
			3 => 0.3f,
			_ => 0.0f,
		};
	}

	private static string GetPath() => Path.Combine(OS.GetUserDataDir(), "world_progress.json");

	private static ProgressData Load()
	{
		try
		{
			string path = GetPath();
			if (!File.Exists(path))
				return new ProgressData();
			string json = File.ReadAllText(path);
			return JsonSerializer.Deserialize<ProgressData>(json) ?? new ProgressData();
		}
		catch (Exception e)
		{
			GD.PushWarning($"Failed to load world progress: {e.Message}");
			return new ProgressData();
		}
	}

	public static void Save()
	{
		try
		{
			string path = GetPath();
			Directory.CreateDirectory(Path.GetDirectoryName(path) ?? OS.GetUserDataDir());
			File.WriteAllText(path, JsonSerializer.Serialize(_data, new JsonSerializerOptions { WriteIndented = true }));
		}
		catch (Exception e)
		{
			GD.PushWarning($"Failed to save world progress: {e.Message}");
		}
	}
}
