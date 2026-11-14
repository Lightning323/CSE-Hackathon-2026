using Godot;
using GridQuest.Entities.Enemy;

public partial class EnemySpawnOnKill : Node
{
	private static readonly PackedScene SkeletonScene = GD.Load<PackedScene>("res://enemy/skeleton_enemy.tscn");
	private static readonly PackedScene EliteScene = GD.Load<PackedScene>("res://enemy/elite_skeleton_enemy.tscn");
	private static readonly PackedScene BloodMonsterScene = GD.Load<PackedScene>("res://enemy/blood_monster_enemy.tscn");
	private static readonly PackedScene GoblinScene = GD.Load<PackedScene>("res://enemy/goblin_enemy.tscn");
	private static readonly PackedScene VampireScene = GD.Load<PackedScene>("res://enemy/vampire_enemy.tscn");

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

	[Export] public int MinDungeonGoblins { get; set; } = 2;
	[Export] public int MaxDungeonGoblins { get; set; } = 5;
	[Export] public float BloodMonsterExtraSpawnChance { get; set; } = 0.55f;

	private float _timer;
	private float _hordeTimer;
	public bool BossFightActive { get; set; } = false;

	public override void _Ready()
	{
		AddToGroup("enemy_spawn_manager");
		_timer = 0.25f;
		_hordeTimer = 8.0f;
		CallDeferred(nameof(SpawnInitialEnemies));
	}

	public void ResetForNewMap()
	{
		_timer = 0.25f;
		_hordeTimer = 8.0f;
		CallDeferred(nameof(SpawnInitialEnemies));
	}

	private void SpawnInitialEnemies()
	{
		EnsureDungeonGoblins(force: true);

		DayNightCycle cycle = GetDayNightCycle();
		SurvivalStats stats = GetSurvivalStats();
		int targetInitial = ApplyDifficultyScaling(InitialSpawnCount, cycle, stats, minimum: true);
		int enemyCount = GetTree().GetNodesInGroup("enemies").Count;
		int toSpawn = Mathf.Max(0, targetInitial - enemyCount);
		for (int i = 0; i < toSpawn; i++)
		{
			SpawnAmbientEnemyOutsideDungeon(force: false);
		}

		// Day 3+ (relative to current world spawn): add blood monsters onto existing spawns.
		TrySpawnBloodMonsterBonus(initialWave: true);
	}

	public override void _Process(double delta)
	{
		if (BossFightActive) return;

		float dt = (float)delta;
		_timer -= dt;
		_hordeTimer -= dt;

		EnsureDungeonGoblins(force: false);

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

		int spawnedThisTick = 0;
		for (int i = 0; i < desiredSpawns; i++)
		{
			enemyCount = GetTree().GetNodesInGroup("enemies").Count;
			if (enemyCount >= maxEnemies)
				break;

			if (enemyCount < minEnemies || GD.Randf() <= spawnChance)
			{
				if (SpawnAmbientEnemyOutsideDungeon(force: enemyCount < minEnemies))
					spawnedThisTick++;
			}
		}

		// Blood monster added onto existing mob spawns on day 3+ of current world spawn.
		if (spawnedThisTick > 0 || (GetTree().GetNodesInGroup("enemies").Count < maxEnemies && GD.Randf() < BloodMonsterExtraSpawnChance * 0.5f))
			TrySpawnBloodMonsterBonus(initialWave: false);
	}

	private DayNightCycle GetDayNightCycle() => GetTree().GetFirstNodeInGroup("day_night_cycle") as DayNightCycle;
	private SurvivalStats GetSurvivalStats() => GetTree().GetFirstNodeInGroup("survival_stats") as SurvivalStats;
	private Map GetMap() => GetTree().GetFirstNodeInGroup("map") as Map ?? GetTree().CurrentScene?.GetNodeOrNull<Map>("Map");

	private int ApplyDifficultyScaling(int baseValue, DayNightCycle cycle, SurvivalStats stats, bool minimum = false)
	{
		float mult = 1.0f;
		if (cycle != null)
			mult *= Mathf.Lerp(minimum ? 1.05f : 1.1f, minimum ? 1.3f : 1.55f, cycle.NightFactor);
		else
			mult *= minimum ? 1.05f : 1.1f;
		if (stats != null)
		{
			float diff = stats.GetDifficultyMultiplier();
			mult *= minimum ? Mathf.Lerp(1.0f, diff, 0.7f) : diff;
		}
		return Mathf.Max(1, Mathf.CeilToInt(baseValue * mult));
	}

	private int GetEffectiveMaxEnemies(DayNightCycle cycle, SurvivalStats stats)
		=> Mathf.Max(MaxEnemies, ApplyDifficultyScaling(MaxEnemies, cycle, stats));

	private int GetEffectiveMinEnemies(DayNightCycle cycle, SurvivalStats stats)
		=> Mathf.Max(MinEnemies, ApplyDifficultyScaling(MinEnemies, cycle, stats, minimum: true));

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
		if (cycle != null) chance += 0.06f * cycle.NightFactor;
		if (stats != null) chance += 0.05f * (stats.GetDifficultyMultiplier() - 1.0f);
		return Mathf.Clamp(chance, 0.0f, 0.995f);
	}

	private bool IsWorldDayAtLeast(int day) => WorldProgress.CurrentWorldDay >= day;

	private void EnsureDungeonGoblins(bool force)
	{
		Map map = GetMap();
		if (map == null || !map.HasBuildingInteriors || GoblinScene == null)
			return;

		int goblinCount = CountGoblins();
		int desired = Mathf.Clamp(GD.RandRange(MinDungeonGoblins, MaxDungeonGoblins), MinDungeonGoblins, MaxDungeonGoblins);
		if (force) desired = Mathf.Max(desired, MinDungeonGoblins);
		if (goblinCount >= desired)
			return;

		int toSpawn = desired - goblinCount;
		for (int i = 0; i < toSpawn; i++)
		{
			Vector2? pos = FindDungeonSpawnPosition(40);
			if (!pos.HasValue)
				break;
			SpawnEnemyAt(pos.Value, GoblinScene, forceChasePlayer: false);
		}
	}

	private int CountGoblins()
	{
		int count = 0;
		foreach (Node n in GetTree().GetNodesInGroup("enemies"))
		{
			if (n is GoblinEnemy && IsInstanceValid(n)) count++;
		}
		return count;
	}

	private void TrySpawnBloodMonsterBonus(bool initialWave)
	{
		if (!IsWorldDayAtLeast(3) || BloodMonsterScene == null)
			return;

		DayNightCycle cycle = GetDayNightCycle();
		SurvivalStats stats = GetSurvivalStats();
		int maxEnemies = GetEffectiveMaxEnemies(cycle, stats);
		if (GetTree().GetNodesInGroup("enemies").Count >= maxEnemies)
			return;

		int count = 1;
		if (initialWave)
			count += Mathf.Clamp(WorldProgress.CurrentWorldDay - 2, 0, 2);
		else if (GD.Randf() > BloodMonsterExtraSpawnChance)
			return;

		for (int i = 0; i < count; i++)
		{
			if (GetTree().GetNodesInGroup("enemies").Count >= maxEnemies) break;
			Vector2? pos = FindOutsideDungeonSpawnPosition(40);
			if (!pos.HasValue) break;
			SpawnEnemyAt(pos.Value, BloodMonsterScene, forceChasePlayer: false);
		}
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

		Vector2? center = FindOutsideDungeonSpawnPosition(40);
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
			Map map = GetMap();
			if (map != null && map.IsWorldPositionInsideBuilding(pos))
			{
				Vector2? adjusted = FindOutsideDungeonSpawnPosition(20);
				if (!adjusted.HasValue) continue;
				pos = adjusted.Value;
			}
			SpawnEnemyAt(pos, PickAmbientOutdoorScene(), forceChasePlayer: true);
		}

		// Add blood monster(s) onto horde on day 3+.
		TrySpawnBloodMonsterBonus(initialWave: false);
	}

	private bool SpawnAmbientEnemyOutsideDungeon(bool force)
	{
		Vector2? pos = FindOutsideDungeonSpawnPosition(force ? 40 : 24);
		if (!pos.HasValue)
			return false;

		PackedScene scene = PickAmbientOutdoorScene();
		SpawnEnemyAt(pos.Value, scene, forceChasePlayer: false);
		return true;
	}

	private PackedScene PickAmbientOutdoorScene()
	{
		DayNightCycle cycle = GetDayNightCycle();
		bool isNight = cycle != null && cycle.IsNight;

		// Vampires only spawn at night (fairly fast/decent damage handled in scene/script).
		if (isNight && VampireScene != null && GD.Randf() < 0.22f)
			return VampireScene;

		return GD.Randf() < EliteChance ? EliteScene : SkeletonScene;
	}

	private Vector2? FindOutsideDungeonSpawnPosition(int attempts)
	{
		Map map = GetMap();
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
			// Other mobs cannot spawn inside the walls dungeon area (building interiors); only goblins can.
			if (map != null && map.IsWorldPositionInsideBuilding(candidate))
				continue;
			return candidate;
		}

		// Fallback: spawn away from player if visibility checks are difficult.
		if (player != null)
		{
			for (int i = 0; i < 30; i++)
			{
				float angle = (float)GD.RandRange(0.0, Mathf.Tau);
				float dist = (float)GD.RandRange(220.0, 520.0);
				Vector2 candidate = player.GlobalPosition + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * dist;
				if (!mapBounds.HasPoint(candidate))
					continue;
				if (camera != null && visible.HasPoint(candidate))
					continue;
				if (map != null && map.IsWorldPositionInsideBuilding(candidate))
					continue;
				return candidate;
			}
		}

		// Last resort: anywhere outside dungeon/building interiors.
		for (int i = 0; i < 80; i++)
		{
			float x = (float)GD.RandRange(mapBounds.Position.X, mapBounds.End.X);
			float y = (float)GD.RandRange(mapBounds.Position.Y, mapBounds.End.Y);
			Vector2 candidate = new(x, y);
			if (map != null && map.IsWorldPositionInsideBuilding(candidate))
				continue;
			return candidate;
		}

		return null;
	}

	private Vector2? FindDungeonSpawnPosition(int attempts)
	{
		Map map = GetMap();
		if (map == null || !map.HasBuildingInteriors)
			return null;

		for (int i = 0; i < attempts; i++)
		{
			Vector2? candidate = map.GetRandomPointInBuildingInterior();
			if (!candidate.HasValue)
				return null;
			if (!map.IsWorldPositionInsideBuilding(candidate.Value))
				continue;
			return candidate.Value;
		}
		return map.GetRandomPointInBuildingInterior();
	}

	private void SpawnEnemyAt(Vector2 position, PackedScene scene, bool forceChasePlayer)
	{
		if (scene == null) return;
		Node inst = scene.Instantiate();
		if (inst is Node2D enemy)
		{
			enemy.GlobalPosition = position;
			// Ensure special spawn rules are respected even if scene selection changes.
			Map map = GetMap();
			if (map != null && enemy is EnemyEntity ee)
			{
				bool inside = map.IsWorldPositionInsideBuilding(position);
				if (inside && !ee.CanSpawnInBuildingInterior)
				{
					inst.QueueFree();
					return;
				}
				if (!inside && ee.CanSpawnInDungeonOnly)
				{
					inst.QueueFree();
					return;
				}
			}

			if (forceChasePlayer && enemy is EnemyEntity ee2 && ee2.ShouldForceChasePlayerOnHordeSpawn)
				enemy.Set("ForceChasePlayer", true);
			else if (forceChasePlayer)
				enemy.Set("ForceChasePlayer", true);

			GetTree().CurrentScene.AddChild(enemy);
		}
		else
		{
			inst.QueueFree();
		}
	}

	private static Rect2 GetCameraWorldRect(Camera2D camera)
	{
		Vector2 viewportSize = camera.GetViewportRect().Size;
		Vector2 zoom = camera.Zoom;
		if (zoom.X <= 0.001f) zoom.X = 1.0f;
		if (zoom.Y <= 0.001f) zoom.Y = 1.0f;
		Vector2 size = viewportSize / zoom;
		Vector2 center = camera.GetScreenCenterPosition();
		return new Rect2(center - size * 0.5f, size);
	}
}
