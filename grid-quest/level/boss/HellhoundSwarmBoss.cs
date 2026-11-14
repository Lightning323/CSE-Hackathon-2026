using Godot;
using GridQuest.Entities.Enemy;
using System.Collections.Generic;
using System.Threading.Tasks;

public partial class HellhoundSwarmBoss : Node
{
	private static readonly PackedScene HellhoundScene = GD.Load<PackedScene>("res://enemy/hellhound_enemy.tscn");

	[Export] public int InitialBurstCount { get; set; } = 18;
	[Export] public int TargetActiveSwarmCount { get; set; } = 28;
	[Export] public float StreamSpawnIntervalSeconds { get; set; } = 0.18f;
	[Export] public int MaxSpawnsPerTick { get; set; } = 4;

	private bool _active;
	private bool _triggerQueued;
	private bool _fightStarted;
	private bool _rewardSpawned;
	private int _bossIndex = 1;
	private int _totalBossHealthPool;
	private int _remainingSpawnHealthPool;
	private double _spawnAccumulator;
	private readonly HashSet<ulong> _swarmIds = new();

	public bool IsActive => _active;

	public override void _Ready()
	{
		AddToGroup("hellhound_swarm_boss_manager");
	}

	public override void _Process(double delta)
	{
		if (_active)
		{
			UpdateBossStream((float)delta);
			return;
		}

		if (_triggerQueued) return;

		if (WorldProgress.HasPendingBossForCurrentWarp())
		{
			if (WorldProgress.GetPendingBossIndex() != 1) return;
			var cycle = GetTree().GetFirstNodeInGroup("day_night_cycle") as DayNightCycle;
			if (cycle == null) return;
			if (IsSunsetWindow(cycle.DaylightFactor))
			{
				_triggerQueued = true;
				_bossIndex = 1;
				_ = StartHellhoundSwarmAsync(_bossIndex);
			}
		}
	}

	private static bool IsSunsetWindow(float daylight)
	{
		// Trigger once in the sunset window.
		return daylight <= 0.62f && daylight >= 0.28f;
	}

	public async Task TriggerCheatHellhoundSwarmAsync()
	{
		if (_active || _triggerQueued) return;
		WorldProgress.DebugPrepareHellhoundBossTrigger();
		var cycle = GetTree().GetFirstNodeInGroup("day_night_cycle") as DayNightCycle;
		cycle?.SetToSunset();
		_triggerQueued = true;
		_bossIndex = WorldProgress.GetPendingBossIndex();
		await StartHellhoundSwarmAsync(_bossIndex);
	}

	private async Task StartHellhoundSwarmAsync(int bossIndex)
	{
		if (_active) { _triggerQueued = false; return; }
		_active = true;
		_fightStarted = false;
		_rewardSpawned = false;
		_bossIndex = Mathf.Max(1, bossIndex);
		_spawnAccumulator = 0.0;

		var ui = GetTree().GetFirstNodeInGroup("boss_fight_ui") as BossFightUi;
		var spawner = GetTree().GetFirstNodeInGroup("enemy_spawn_manager") as EnemySpawnOnKill;
		var level = GetTree().CurrentScene as MainLevel ?? GetTree().Root.FindChild("Level", true, false) as MainLevel;
		var cycle = GetTree().GetFirstNodeInGroup("day_night_cycle") as DayNightCycle;

		if (spawner != null) spawner.BossFightActive = true;
		level?.StopBackgroundMusicForBoss();
		cycle?.FreezeTime();

		// Play bossfight start audio.
		PlayOptionalSfx("res://assets/audio/sfx/hellhoundsummon.mp3");

		if (ui != null)
		{
			await ui.ShowHellIsComingSequenceAsync(() =>
			{
				ClearArenaForBossFight();
			});
		}
		else
		{
			ClearArenaForBossFight();
		}

		InitializeBossHealthPool();

		// Give the player a moment to get ready in the arena before the horde begins.
		double hordeDelay = GD.RandRange(3.0, 5.0);
		await ToSignal(GetTree().CreateTimer(hordeDelay, true, false, true), SceneTreeTimer.SignalName.Timeout);

		SpawnInitialSwarmBurst();
		_fightStarted = true;

		if (ui != null)
		{
			// As enemies begin coming, fade in a slight red tint.
			_ = ui.FadeRedTintAsync(true, 0.8f);
			await ui.ShowBossBarAsync("Hellhound Swarm", _totalBossHealthPool);
			ui.UpdateBossHealth(GetCurrentBossHealthTotal(), _totalBossHealthPool);
		}

		// Mark milestone as started/consumed so we don't retrigger repeatedly.
		WorldProgress.MarkPendingBossCompleted();
		_triggerQueued = false;
	}

	private void InitializeBossHealthPool()
	{
		_swarmIds.Clear();
		int basePool = 3360; // ~8x previous pool: much longer swarm fight
		int scaled = basePool + Mathf.Clamp((_bossIndex - 1) * 1120, 0, 3360);
		_totalBossHealthPool = Mathf.Max(1, scaled);
		_remainingSpawnHealthPool = _totalBossHealthPool;
	}

	private void SpawnInitialSwarmBurst()
	{
		int initial = InitialBurstCount + Mathf.Clamp((_bossIndex - 1) * 4, 0, 16);
		for (int i = 0; i < initial; i++)
		{
			if (_remainingSpawnHealthPool <= 0) break;
			TrySpawnStreamHellhound();
		}
	}

	private void UpdateBossStream(float delta)
	{
		if (!_active || !_fightStarted) return;
		CleanupInvalidSwarmIds();

		if (_remainingSpawnHealthPool > 0)
		{
			_spawnAccumulator += delta;
			while (_spawnAccumulator >= StreamSpawnIntervalSeconds && _remainingSpawnHealthPool > 0)
			{
				_spawnAccumulator -= StreamSpawnIntervalSeconds;
				int spawnedThisTick = 0;
				while (spawnedThisTick < MaxSpawnsPerTick &&
				       _remainingSpawnHealthPool > 0 &&
				       _swarmIds.Count < TargetActiveSwarmCount + 10)
				{
					if (!TrySpawnStreamHellhound()) break;
					spawnedThisTick++;
				}
			}
		}

		UpdateBossUiHealth();

		if (_remainingSpawnHealthPool <= 0 && _swarmIds.Count <= 0)
		{
			_ = EndHellhoundSwarmAsync();
		}
	}

	private bool TrySpawnStreamHellhound()
	{
		if (_remainingSpawnHealthPool <= 0 || HellhoundScene == null) return false;

		Vector2? pos = FindSwarmSpawnPosition(40);
		if (!pos.HasValue) return false;

		Node inst = HellhoundScene.Instantiate();
		if (inst is not Node2D enemy)
		{
			inst.QueueFree();
			return false;
		}

		enemy.GlobalPosition = pos.Value;
		if (enemy is HellhoundEnemy hellhound)
		{
			hellhound.Variant = GD.RandRange(1, 3);
			hellhound.ForceChasePlayer = true;
			hellhound.CoinDropCount = 0;
			hellhound.AddToGroup("boss_hellhound_swarm");
			hellhound.TreeExiting += () => OnHellhoundTreeExiting(hellhound.GetInstanceId());

			// Each spawn consumes from the boss health pool until depleted.
			int spawnHealth = Mathf.Clamp(hellhound.MaxHealth, 1, _remainingSpawnHealthPool);
			hellhound.MaxHealth = spawnHealth;
			hellhound.SetCurrentHealthForBoss(spawnHealth);
			_remainingSpawnHealthPool -= spawnHealth;
		}
		else
		{
			int fallback = Mathf.Min(8, _remainingSpawnHealthPool);
			_remainingSpawnHealthPool -= fallback;
		}

		GetTree().CurrentScene.AddChild(enemy);
		_swarmIds.Add(enemy.GetInstanceId());
		return true;
	}

	private void OnHellhoundTreeExiting(ulong id)
	{
		if (!_active) return;
		_swarmIds.Remove(id);
		CallDeferred(nameof(CheckSwarmStateDeferred));
	}

	private void CheckSwarmStateDeferred()
	{
		if (!_active || !_fightStarted) return;
		CleanupInvalidSwarmIds();
		UpdateBossUiHealth();
		if (_remainingSpawnHealthPool <= 0 && _swarmIds.Count <= 0)
		{
			_ = EndHellhoundSwarmAsync();
		}
	}

	private void CleanupInvalidSwarmIds()
	{
		_swarmIds.RemoveWhere(id => !GodotObject.IsInstanceIdValid(id));
	}

	private void UpdateBossUiHealth()
	{
		var ui = GetTree().GetFirstNodeInGroup("boss_fight_ui") as BossFightUi;
		if (ui == null) return;
		ui.UpdateBossHealth(GetCurrentBossHealthTotal(), Mathf.Max(1, _totalBossHealthPool));
	}

	private int GetCurrentBossHealthTotal()
	{
		return CalculateAliveSwarmHealth() + Mathf.Max(0, _remainingSpawnHealthPool);
	}

	private int CalculateAliveSwarmHealth()
	{
		int total = 0;
		foreach (Node n in GetTree().GetNodesInGroup("boss_hellhound_swarm"))
		{
			if (!IsInstanceValid(n)) continue;
			Variant hv = n.Get("Health");
			if (hv.VariantType == Variant.Type.Int) total += Mathf.Max(0, hv.AsInt32());
			else if (hv.VariantType == Variant.Type.Float) total += Mathf.Max(0, Mathf.RoundToInt((float)hv.AsDouble()));
		}
		return total;
	}

	private async Task EndHellhoundSwarmAsync()
	{
		if (!_active || !_fightStarted || _rewardSpawned) return;
		_active = false;
		_fightStarted = false;
		_rewardSpawned = true;

		var ui = GetTree().GetFirstNodeInGroup("boss_fight_ui") as BossFightUi;
		var spawner = GetTree().GetFirstNodeInGroup("enemy_spawn_manager") as EnemySpawnOnKill;
		var level = GetTree().CurrentScene as MainLevel ?? GetTree().Root.FindChild("Level", true, false) as MainLevel;
		var cycle = GetTree().GetFirstNodeInGroup("day_night_cycle") as DayNightCycle;

		if (ui != null)
		{
			ui.UpdateBossHealth(0, Mathf.Max(1, _totalBossHealthPool));
			await ui.FadeRedTintAsync(false, 0.8f);
			ui.HideBossBar();
		}

		// Reward spawns only after the final hellhound is defeated (health pool/bar depleted).
		SpawnBossRewardCoins();

		cycle?.ResumeTime();
		level?.ResumeBackgroundMusicAfterBoss();
		if (spawner != null)
		{
			spawner.BossFightActive = false;
			spawner.ResetForNewMap();
		}

		_swarmIds.Clear();
		_totalBossHealthPool = 0;
		_remainingSpawnHealthPool = 0;
		_spawnAccumulator = 0.0;
	}

	private void SpawnBossRewardCoins()
	{
		// First boss reward 150, increase by fair percentage each subsequent boss (every 3rd warp).
		int baseReward = 150;
		double scaled = baseReward * Mathf.Pow(1.35, Mathf.Max(0, _bossIndex - 1));
		int coinCount = Mathf.Max(1, Mathf.RoundToInt((float)scaled));

		Node2D player = GetTree().GetFirstNodeInGroup("player") as Node2D;
		Vector2 center = player != null ? player.GlobalPosition : Vector2.Zero;
		PackedScene coinScene = GD.Load<PackedScene>("res://items/coin.tscn");
		for (int i = 0; i < coinCount; i++)
		{
			Node coinNode = coinScene.Instantiate();
			if (coinNode is not Node2D coin) { coinNode.QueueFree(); continue; }
			float angle = (float)GD.RandRange(0.0, Mathf.Tau);
			float dist = (float)GD.RandRange(8.0, 56.0);
			coin.GlobalPosition = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * dist;
			GetTree().CurrentScene.AddChild(coin);
		}
	}

	private void ClearArenaForBossFight()
	{
		// During black flash: clear map to boss arena, remove NPCs/knights/villagers and other non-essential entities,
		// and place the player in the arena.
		var map = GetTree().GetFirstNodeInGroup("map") as Map ?? GetTree().CurrentScene?.GetNodeOrNull<Map>("Map");
		map?.PrepareBossArena();

		ClearNonPlayerEntitiesForBossFight();
		PlacePlayerForBossFight(map);
	}

	private void ClearNonPlayerEntitiesForBossFight()
	{
		string[] groups = {
			"enemies", "villager_npcs", "villagers", "traders", "knights",
			"coin_pickups", "projectiles", "chests"
		};
		foreach (string group in groups)
		{
			foreach (Node n in GetTree().GetNodesInGroup(group))
			{
				if (!IsInstanceValid(n)) continue;
				if (n.IsInGroup("player")) continue;
				if (n.IsInGroup("boss_hellhound_swarm")) continue;
				n.QueueFree();
			}
		}

		// Also clear common world entities that may not be grouped consistently.
		var scene = GetTree().CurrentScene;
		if (scene == null) return;
		foreach (Node child in scene.GetChildren())
		{
			if (!IsInstanceValid(child)) continue;
			if (child == scene) continue;
			if (child is Player || child is Map || child is CanvasLayer || child is CanvasModulate || child is MainLevel) continue;
			string name = child.Name.ToString();
			if (name.Contains("Coin", System.StringComparison.OrdinalIgnoreCase) ||
				name.Contains("Fireball", System.StringComparison.OrdinalIgnoreCase) ||
				name.Contains("Bolt", System.StringComparison.OrdinalIgnoreCase) ||
				name.Contains("Villager", System.StringComparison.OrdinalIgnoreCase) ||
				name.Contains("Knight", System.StringComparison.OrdinalIgnoreCase))
			{
				child.QueueFree();
			}
		}
	}

	private void PlacePlayerForBossFight(Map map)
	{
		var player = GetTree().GetFirstNodeInGroup("player") as Node2D;
		if (player == null || !IsInstanceValid(player)) return;

		Vector2 pos;
		if (map != null)
		{
			// Place near the monument on the open boss arena.
			Vector2 monument = map.MonumentWorldPosition;
			pos = monument + new Vector2(0, 40);
		}
		else
		{
			pos = player.GlobalPosition;
		}
		player.GlobalPosition = pos;
	}

	private void PlayOptionalSfx(string resPath)
	{
		if (!ResourceLoader.Exists(resPath)) return;
		var stream = GD.Load<AudioStream>(resPath);
		if (stream == null) return;
		var p = new AudioStreamPlayer { Stream = stream, ProcessMode = ProcessModeEnum.Always, VolumeDb = -4.0f };
		AddChild(p);
		p.Finished += p.QueueFree;
		p.Play();
	}

	private Vector2? FindSwarmSpawnPosition(int attempts)
	{
		Camera2D camera = GetViewport().GetCamera2D();
		Node2D player = GetTree().GetFirstNodeInGroup("player") as Node2D;
		Rect2 visible = camera != null ? GetCameraWorldRect(camera).Grow(24.0f) : new Rect2(-99999, -99999, 0, 0);
		Rect2 mapBounds = new(32, 32, Map.Width * GridQuest.Map.MapConstants.CELL_SIZE - 64, Map.Height * GridQuest.Map.MapConstants.CELL_SIZE - 64);
		Map map = GetTree().GetFirstNodeInGroup("map") as Map ?? GetTree().CurrentScene?.GetNodeOrNull<Map>("Map");

		// Prefer spawning in a ring around the player (far enough to give room to run, can be on-screen if far).
		if (player != null)
		{
			for (int i = 0; i < attempts; i++)
			{
				float angle = (float)GD.RandRange(0.0, Mathf.Tau);
				float dist = (float)GD.RandRange(320.0, 520.0);
				Vector2 candidate = player.GlobalPosition + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * dist;
				if (!mapBounds.HasPoint(candidate)) continue;
				if (candidate.DistanceTo(player.GlobalPosition) < 260.0f) continue;
				if (map != null && map.IsWorldPositionInsideBuilding(candidate)) continue;
				return candidate;
			}

			// Fallback ring if tight arena/camera checks reject initial attempts.
			for (int i = 0; i < attempts; i++)
			{
				float angle = (float)GD.RandRange(0.0, Mathf.Tau);
				float dist = (float)GD.RandRange(260.0, 640.0);
				Vector2 candidate = player.GlobalPosition + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * dist;
				if (!mapBounds.HasPoint(candidate)) continue;
				if (map != null && map.IsWorldPositionInsideBuilding(candidate)) continue;
				return candidate;
			}
		}

		// Last-resort fallback: random valid point, avoiding visible camera area only when no player is found.
		for (int i = 0; i < attempts; i++)
		{
			float x = (float)GD.RandRange(mapBounds.Position.X, mapBounds.End.X);
			float y = (float)GD.RandRange(mapBounds.Position.Y, mapBounds.End.Y);
			Vector2 candidate = new(x, y);
			if (player == null && camera != null && visible.HasPoint(candidate)) continue;
			if (player != null && candidate.DistanceTo(player.GlobalPosition) < 90.0f) continue;
			if (map != null && map.IsWorldPositionInsideBuilding(candidate)) continue;
			return candidate;
		}
		return null;
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
