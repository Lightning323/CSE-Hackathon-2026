using Godot;

public partial class EnemySpawnOnKill : Node
{
	private static readonly PackedScene SkeletonScene = GD.Load<PackedScene>("res://enemy/skeleton_enemy.tscn");
	private static readonly PackedScene EliteScene = GD.Load<PackedScene>("res://enemy/elite_skeleton_enemy.tscn");

	[Export] public float SpawnCheckInterval { get; set; } = 1.2f;
	[Export] public float SpawnChance { get; set; } = 0.95f;
	[Export] public int MaxEnemies { get; set; } = 40;
	[Export] public int MinEnemies { get; set; } = 12;
	[Export] public int MaxSpawnsPerTick { get; set; } = 3;
	[Export] public int InitialSpawnCount { get; set; } = 10;
	[Export] public float EliteChance { get; set; } = 0.12f;

	[Export] public float HordeCheckInterval { get; set; } = 18.0f;
	[Export] public float NightHordeChance { get; set; } = 0.45f;
	[Export] public int HordeMinSize { get; set; } = 4;
	[Export] public int HordeMaxSize { get; set; } = 7;

	private float _timer;
	private float _hordeTimer;

	public override void _Ready()
	{
		_timer = 0.25f;
		_hordeTimer = 8.0f;
		CallDeferred(nameof(SpawnInitialEnemies));
	}

	private void SpawnInitialEnemies()
	{
		DayNightCycle cycle = GetDayNightCycle();
		SurvivalStats stats = GetSurvivalStats();
		int targetInitial = ApplyDifficultyScaling(InitialSpawnCount, cycle, stats, minimum: true);
		int enemyCount = GetTree().GetNodesInGroup("enemies").Count;
		int toSpawn = Mathf.Max(0, targetInitial - enemyCount);
		for (int i = 0; i < toSpawn; i++)
		{
			SpawnEnemyOutsideCamera(force: false);
		}
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;
		_timer -= dt;
		_hordeTimer -= dt;

		if (_hordeTimer <= 0.0f)
		{
			_hordeTimer = HordeCheckInterval;
			TrySpawnHorde();
		}

		if (_timer > 0.0f) return;
		_timer = SpawnCheckInterval;

		DayNightCycle cycle = GetDayNightCycle();
		SurvivalStats stats = GetSurvivalStats();
		int maxEnemies = GetEffectiveMaxEnemies(cycle, stats);
		int minEnemies = GetEffectiveMinEnemies(cycle, stats);
		float spawnChance = GetEffectiveSpawnChance(cycle, stats);
		int maxSpawnsPerTick = GetEffectiveMaxSpawnsPerTick(cycle, stats);

		int enemyCount = GetTree().GetNodesInGroup("enemies").Count;
		if (enemyCount >= maxEnemies)
			return;

		int desiredSpawns = 1;
		if (enemyCount < minEnemies)
			desiredSpawns = Mathf.Clamp(minEnemies - enemyCount, 1, maxSpawnsPerTick);

		for (int i = 0; i < desiredSpawns; i++)
		{
			enemyCount = GetTree().GetNodesInGroup("enemies").Count;
			if (enemyCount >= maxEnemies)
				break;

			if (enemyCount < minEnemies || GD.Randf() <= spawnChance)
			{
				SpawnEnemyOutsideCamera(force: enemyCount < minEnemies);
			}
		}
	}

	private DayNightCycle GetDayNightCycle()
	{
		return GetTree().GetFirstNodeInGroup("day_night_cycle") as DayNightCycle;
	}

	private SurvivalStats GetSurvivalStats()
	{
		return GetTree().GetFirstNodeInGroup("survival_stats") as SurvivalStats;
	}

	private int ApplyDifficultyScaling(int baseValue, DayNightCycle cycle, SurvivalStats stats, bool minimum = false)
	{
		float mult = 1.0f;
		if (cycle != null)
		{
			// Slightly higher base pressure overall, and stronger at night.
			mult *= Mathf.Lerp(minimum ? 1.05f : 1.1f, minimum ? 1.3f : 1.55f, cycle.NightFactor);
		}
		else
		{
			mult *= minimum ? 1.05f : 1.1f;
		}
		if (stats != null)
		{
			float diff = stats.GetDifficultyMultiplier();
			mult *= minimum ? Mathf.Lerp(1.0f, diff, 0.7f) : diff;
		}
		return Mathf.Max(1, Mathf.CeilToInt(baseValue * mult));
	}

	private int GetEffectiveMaxEnemies(DayNightCycle cycle, SurvivalStats stats)
	{
		// Increase enemy counts over time (capped by SurvivalStats), not mob health.
		return Mathf.Max(MaxEnemies, ApplyDifficultyScaling(MaxEnemies, cycle, stats));
	}

	private int GetEffectiveMinEnemies(DayNightCycle cycle, SurvivalStats stats)
	{
		return Mathf.Max(MinEnemies, ApplyDifficultyScaling(MinEnemies, cycle, stats, minimum: true));
	}

	private int GetEffectiveMaxSpawnsPerTick(DayNightCycle cycle, SurvivalStats stats)
	{
		float mult = 1.0f;
		if (cycle != null) mult *= Mathf.Lerp(1.1f, 1.35f, cycle.NightFactor);
		if (stats != null) mult *= Mathf.Lerp(1.0f, stats.GetDifficultyMultiplier(), 0.5f);
		return Mathf.Max(MaxSpawnsPerTick, Mathf.CeilToInt(MaxSpawnsPerTick * mult));
	}

	private float GetEffectiveSpawnChance(DayNightCycle cycle, SurvivalStats stats)
	{
		float chance = SpawnChance;
		if (cycle != null)
			chance += 0.06f * cycle.NightFactor; // night already higher, boosted a bit more
		if (stats != null)
			chance += 0.05f * (stats.GetDifficultyMultiplier() - 1.0f);
		return Mathf.Clamp(chance, 0.0f, 0.995f);
	}

	private void TrySpawnHorde()
	{
		DayNightCycle cycle = GetDayNightCycle();
		SurvivalStats stats = GetSurvivalStats();
		if (cycle == null)
			return;

		// Random hordes, primarily during night.
		float nightFactor = cycle.NightFactor;
		float chance = NightHordeChance * Mathf.Lerp(0.15f, 1.0f, nightFactor);
		if (stats != null)
			chance *= Mathf.Lerp(0.85f, 1.2f, Mathf.Clamp((stats.GetDifficultyMultiplier() - 1.0f) / 0.8f, 0.0f, 1.0f));
		if (GD.Randf() > Mathf.Clamp(chance, 0.0f, 0.95f))
			return;

		Vector2? center = FindOffCameraSpawnPosition(40);
		if (!center.HasValue)
			return;

		int groupSize = GD.RandRange(HordeMinSize, HordeMaxSize);
		if (stats != null)
			groupSize += Mathf.FloorToInt((stats.GetDifficultyMultiplier() - 1.0f) * 2.0f);
		groupSize = Mathf.Clamp(groupSize, HordeMinSize, HordeMaxSize + 3);

		int maxEnemies = GetEffectiveMaxEnemies(cycle, stats);
		int current = GetTree().GetNodesInGroup("enemies").Count;
		int canSpawn = Mathf.Max(0, maxEnemies - current);
		groupSize = Mathf.Min(groupSize, Mathf.Max(1, canSpawn));

		for (int i = 0; i < groupSize; i++)
		{
			Vector2 offset = new((float)GD.RandRange(-28.0, 28.0), (float)GD.RandRange(-28.0, 28.0));
			Vector2 pos = center.Value + offset;
			SpawnEnemyAt(pos, SkeletonScene, forceChasePlayer: true);
		}
	}

	private void SpawnEnemyOutsideCamera(bool force)
	{
		Vector2? pos = FindOffCameraSpawnPosition(force ? 40 : 24);
		if (!pos.HasValue)
			return;

		PackedScene scene = GD.Randf() < EliteChance ? EliteScene : SkeletonScene;
		SpawnEnemyAt(pos.Value, scene, forceChasePlayer: false);
	}

	private void SpawnEnemyAt(Vector2 position, PackedScene scene, bool forceChasePlayer)
	{
		if (scene == null) return;
		Node inst = scene.Instantiate();
		if (inst is Node2D enemy)
		{
			enemy.GlobalPosition = position;
			if (forceChasePlayer && enemy.HasMethod("set_ForceChasePlayer"))
			{
				enemy.Set("ForceChasePlayer", true);
			}
			else if (forceChasePlayer)
			{
				// C# property access via Set works for exported/script properties in Godot.
				enemy.Set("ForceChasePlayer", true);
			}
			GetTree().CurrentScene.AddChild(enemy);
		}
		else
		{
			inst.QueueFree();
		}
	}

	private Vector2? FindOffCameraSpawnPosition(int attempts)
	{
		Camera2D camera = GetViewport().GetCamera2D();
		Rect2 visible = camera != null ? GetCameraWorldRect(camera).Grow(48.0f) : new Rect2(-99999, -99999, 0, 0);
		Rect2 mapBounds = new(32, 32, Map.Width * GridQuest.Map.MapConstants.CELL_SIZE - 64, Map.Height * GridQuest.Map.MapConstants.CELL_SIZE - 64);
		Node2D player = GetTree().GetFirstNodeInGroup("player") as Node2D;

		for (int i = 0; i < attempts; i++)
		{
			float x = (float)GD.RandRange(mapBounds.Position.X, mapBounds.End.X);
			float y = (float)GD.RandRange(mapBounds.Position.Y, mapBounds.End.Y);
			Vector2 candidate = new(x, y);
			if (camera != null && visible.HasPoint(candidate))
				continue;
			if (player != null && candidate.DistanceTo(player.GlobalPosition) < 180.0f)
				continue;
			return candidate;
		}

		// Fallback: spawn away from player if visibility checks are difficult (e.g. camera not ready yet).
		if (player != null)
		{
			for (int i = 0; i < 20; i++)
			{
				float angle = (float)GD.RandRange(0.0, Mathf.Tau);
				float dist = (float)GD.RandRange(220.0, 520.0);
				Vector2 candidate = player.GlobalPosition + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * dist;
				if (!mapBounds.HasPoint(candidate))
					continue;
				if (camera != null && visible.HasPoint(candidate))
					continue;
				return candidate;
			}
		}

		return null;
	}

	private Rect2 GetCameraWorldRect(Camera2D camera)
	{
		Vector2 viewportSize = GetViewport().GetVisibleRect().Size;
		Vector2 zoom = camera.Zoom;
		if (zoom.X == 0 || zoom.Y == 0)
			zoom = Vector2.One;
		Vector2 worldSize = viewportSize / zoom;
		Vector2 topLeft = camera.GetScreenCenterPosition() - worldSize / 2.0f;
		return new Rect2(topLeft, worldSize);
	}
}
