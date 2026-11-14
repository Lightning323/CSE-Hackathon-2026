using Godot;

public partial class EnemySpawnOnKill : Node
{
	private static readonly PackedScene SkeletonScene = GD.Load<PackedScene>("res://enemy/skeleton_enemy.tscn");
	private static readonly PackedScene EliteScene = GD.Load<PackedScene>("res://enemy/elite_skeleton_enemy.tscn");

	[Export] public float SpawnCheckInterval { get; set; } = 1.5f;
	[Export] public float SpawnChance { get; set; } = 0.9f;
	[Export] public int MaxEnemies { get; set; } = 40;
	[Export] public int MinEnemies { get; set; } = 12;
	[Export] public int MaxSpawnsPerTick { get; set; } = 3;
	[Export] public int InitialSpawnCount { get; set; } = 10;
	[Export] public float EliteChance { get; set; } = 0.12f;

	private float _timer;

	public override void _Ready()
	{
		_timer = 0.25f;
		CallDeferred(nameof(SpawnInitialEnemies));
	}

	private void SpawnInitialEnemies()
	{
		DayNightCycle cycle = GetDayNightCycle();
		int targetInitial = cycle != null ? cycle.GetAdjustedEnemyCount(InitialSpawnCount) : InitialSpawnCount;
		int enemyCount = GetTree().GetNodesInGroup("enemies").Count;
		int toSpawn = Mathf.Max(0, targetInitial - enemyCount);
		for (int i = 0; i < toSpawn; i++)
		{
			SpawnEnemyOutsideCamera(force: false);
		}
	}

	public override void _Process(double delta)
	{
		_timer -= (float)delta;
		if (_timer > 0.0f) return;
		_timer = SpawnCheckInterval;

		DayNightCycle cycle = GetDayNightCycle();
		int maxEnemies = GetEffectiveMaxEnemies(cycle);
		int minEnemies = GetEffectiveMinEnemies(cycle);
		float spawnChance = GetEffectiveSpawnChance(cycle);
		int maxSpawnsPerTick = GetEffectiveMaxSpawnsPerTick(cycle);

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

	private int GetEffectiveMaxEnemies(DayNightCycle cycle)
	{
		if (cycle == null) return MaxEnemies;
		return Mathf.Max(MaxEnemies, cycle.GetAdjustedEnemyCount(MaxEnemies));
	}

	private int GetEffectiveMinEnemies(DayNightCycle cycle)
	{
		if (cycle == null) return MinEnemies;
		return Mathf.Max(MinEnemies, cycle.GetAdjustedEnemyCount(MinEnemies, minimum: true));
	}

	private int GetEffectiveMaxSpawnsPerTick(DayNightCycle cycle)
	{
		if (cycle == null) return MaxSpawnsPerTick;
		return Mathf.Max(MaxSpawnsPerTick, Mathf.CeilToInt(MaxSpawnsPerTick * Mathf.Lerp(1.0f, 1.2f, cycle.NightFactor)));
	}

	private float GetEffectiveSpawnChance(DayNightCycle cycle)
	{
		if (cycle == null) return SpawnChance;
		return Mathf.Clamp(SpawnChance + 0.05f * cycle.NightFactor, 0.0f, 0.99f);
	}

	private void SpawnEnemyOutsideCamera(bool force)
	{
		Vector2? pos = FindOffCameraSpawnPosition(force ? 40 : 24);
		if (!pos.HasValue)
			return;

		PackedScene scene = GD.Randf() < EliteChance ? EliteScene : SkeletonScene;
		Node inst = scene.Instantiate();
		if (inst is Node2D enemy)
		{
			enemy.GlobalPosition = pos.Value;
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
