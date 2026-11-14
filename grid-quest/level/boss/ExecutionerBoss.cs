#nullable enable
using Godot;
using GridQuest.Entities.Enemy;
using System.Threading.Tasks;

public partial class ExecutionerBoss : Node
{
	private static readonly PackedScene ExecutionerScene = GD.Load<PackedScene>("res://enemy/executioner_enemy.tscn");

	private bool _active;
	private bool _triggerQueued;
	private bool _rewardSpawned;
	private int _bossIndex = 2;
	private int _totalBossMaxHealth = 1;
	private ExecutionerEnemy? _executioner;
	private AudioStreamPlayer? _spawnAudioPlayer;
	private Color _previousCycleColor = Colors.White;
	private bool _hasPreviousCycleColor;

	public bool IsActive => _active;

	public override void _Ready()
	{
		AddToGroup("executioner_boss_manager");
	}

	public override void _Process(double delta)
	{
		if (_active)
		{
			UpdateBossUi();
			return;
		}

		if (_triggerQueued) return;

		if (WorldProgress.HasPendingBossForCurrentWarp() && WorldProgress.GetPendingBossIndex() == 2)
		{
			var cycle = GetTree().GetFirstNodeInGroup("day_night_cycle") as DayNightCycle;
			if (cycle == null) return;
			if (IsSunsetWindow(cycle.DaylightFactor))
			{
				_triggerQueued = true;
				_bossIndex = 2;
				// Trigger moment = sunset bossfight trigger: start audio immediately.
				_ = StartExecutionerBossAsync(_bossIndex, playAudioImmediately: true);
			}
		}
	}

	private static bool IsSunsetWindow(float daylight)
	{
		return daylight <= 0.62f && daylight >= 0.28f;
	}

	public async Task TriggerCheatExecutionerBossAsync()
	{
		if (_active || _triggerQueued) return;
		WorldProgress.DebugPrepareExecutionerBossTrigger();
		var cycle = GetTree().GetFirstNodeInGroup("day_night_cycle") as DayNightCycle;
		cycle?.SetToSunset();
		_triggerQueued = true;
		_bossIndex = 2;
		await StartExecutionerBossAsync(_bossIndex, playAudioImmediately: true);
	}

	private async Task StartExecutionerBossAsync(int bossIndex, bool playAudioImmediately)
	{
		if (_active) { _triggerQueued = false; return; }
		_active = true;
		_rewardSpawned = false;
		_bossIndex = Mathf.Max(2, bossIndex);

		var ui = GetTree().GetFirstNodeInGroup("boss_fight_ui") as BossFightUi;
		var spawner = GetTree().GetFirstNodeInGroup("enemy_spawn_manager") as EnemySpawnOnKill;
		var level = GetTree().CurrentScene as MainLevel ?? GetTree().Root.FindChild("Level", true, false) as MainLevel;
		var cycle = GetTree().GetFirstNodeInGroup("day_night_cycle") as DayNightCycle;
		var player = GetTree().GetFirstNodeInGroup("player") as Player;
		var map = GetTree().GetFirstNodeInGroup("map") as Map ?? GetTree().CurrentScene?.GetNodeOrNull<Map>("Map");

		if (spawner != null) spawner.BossFightActive = true;
		level?.StopBackgroundMusicForBoss();
		player?.SetBossMovementLocked(true);

		// Midnight and stay midnight for duration of the bossfight; keep it dark but slightly brighter for visibility.
		if (cycle != null)
		{
			_previousCycleColor = cycle.Color;
			_hasPreviousCycleColor = true;
			cycle.SetToMidnight();
			cycle.FreezeTime();
			cycle.Color = new Color(0.14f, 0.15f, 0.24f, 1.0f);
		}

		double audioClipLength = 0.0;
		if (playAudioImmediately)
		{
			_spawnAudioPlayer = PlaySfxAndReturn("res://assets/audio/sfx/executionersummon.mp3", out audioClipLength);
		}
		// Start boss fight music immediately when the bossfight starts.
		PlayBossMusic("res://assets/audio/sfx/ambientexc_music.mp3");

		// Blackout card for 6 seconds with white "Judge... Jury..." text.
		// During blackout: transform arena, hide monument, clear entities, place player, spawn hidden executioner.
		if (ui != null)
		{
			await ui.ShowExecutionerIntroSequenceAsync(
				onBlackoutStart: null,
				duringBlackFlash: () =>
				{
					ClearArenaForBossFight();
					map?.SetMonumentVisible(false);
					SpawnExecutioner();
				},
				onBlackoutEnd: () =>
				{
					// As soon as blackout ends, begin summonDeath spawn-in animation (8s).
					if (_executioner != null && IsInstanceValid(_executioner))
						_executioner.BeginSpawnIntroAnimation();
				},
				blackoutHoldSeconds: 6.0f);
		}
		else
		{
			ClearArenaForBossFight();
			map?.SetMonumentVisible(false);
			SpawnExecutioner();
			await ToSignal(GetTree().CreateTimer(6.0f, true, false, true), SceneTreeTimer.SignalName.Timeout);
			if (_executioner != null && IsInstanceValid(_executioner))
				_executioner.BeginSpawnIntroAnimation();
		}

		if (_executioner == null || !IsInstanceValid(_executioner))
		{
			EndBossCleanupNoReward();
			return;
		}

		_totalBossMaxHealth = Mathf.Max(1, _executioner.MaxHealth);
		if (ui != null)
		{
			await ui.ShowBossBarAsync("Executioner", _totalBossMaxHealth);
			ui.UpdateBossHealth(_executioner.Health, _totalBossMaxHealth);
		}

		// Timed boss intro events relative to sunset/bossfight trigger (audio start).
		_ = HandleExecutionerTimingAsync(audioClipLength, player);

		WorldProgress.MarkPendingBossCompleted();
		_triggerQueued = false;
	}

	private async Task HandleExecutionerTimingAsync(double audioClipLength, Player? player)
	{
		// 6.5s after trigger: player light goes out and player can move.
		await ToSignal(GetTree().CreateTimer(6.5f, true, false, true), SceneTreeTimer.SignalName.Timeout);
		if (!_active) return;
		player?.SetBossLampDisabled(true);
		player?.SetBossMovementLocked(false);

		// 14s after trigger (audio end / spawn-in completion): finish spawn-in and stop spawn audio at that moment.
		await ToSignal(GetTree().CreateTimer(7.5f, true, false, true), SceneTreeTimer.SignalName.Timeout);
		if (!_active) return;

		if (_spawnAudioPlayer != null && IsInstanceValid(_spawnAudioPlayer) && _spawnAudioPlayer.Playing)
			_spawnAudioPlayer.Stop();

		if (_executioner != null && IsInstanceValid(_executioner))
			_executioner.FinishSpawnIntroAndIdle();

		// Wait at least 1-2 seconds after the audio clip ends before first attack.
		// Audio is timed to end at 14s; if clip is longer, wait for its actual end as well.
		double postEndDelay = GD.RandRange(1.0, 2.0);
		double waitAfterAudioEnd = postEndDelay + Mathf.Max(0.0, audioClipLength - 14.0);
		if (waitAfterAudioEnd > 0.001)
			await ToSignal(GetTree().CreateTimer(waitAfterAudioEnd, true, false, true), SceneTreeTimer.SignalName.Timeout);

		if (!_active) return;
		if (_executioner != null && IsInstanceValid(_executioner))
			_executioner.CombatEnabled = true;
	}

	private void SpawnExecutioner()
	{
		if (ExecutionerScene == null) return;
		Node inst = ExecutionerScene.Instantiate();
		if (inst is not ExecutionerEnemy boss)
		{
			inst.QueueFree();
			return;
		}

		Map map = GetTree().GetFirstNodeInGroup("map") as Map ?? GetTree().CurrentScene?.GetNodeOrNull<Map>("Map");
		Node2D player = GetTree().GetFirstNodeInGroup("player") as Node2D;
		Vector2 center = map != null ? map.MonumentWorldPosition : (player?.GlobalPosition ?? Vector2.Zero);
		// Center of the stone/monument area so the spawn-in animation is easy to see.
		Vector2 spawnPos = center + new Vector2(0, -8);
		boss.GlobalPosition = spawnPos;
		boss.ForceChasePlayer = true;
		boss.CoinDropCount = 0;
		boss.CombatEnabled = false;
		boss.AddToGroup("executioner_boss");
		boss.TreeExiting += OnExecutionerTreeExiting;
		boss.Connect(ExecutionerEnemy.SignalName.BossHealthChanged, Callable.From<int, int>(OnExecutionerHealthChanged));

		GetTree().CurrentScene.AddChild(boss);
		_executioner = boss;
	}

	private void OnExecutionerHealthChanged(int current, int max)
	{
		_totalBossMaxHealth = Mathf.Max(1, max);
		UpdateBossUi();
	}

	private void OnExecutionerTreeExiting()
	{
		if (!_active) return;
		CallDeferred(nameof(CheckExecutionerDefeatDeferred));
	}

	private void CheckExecutionerDefeatDeferred()
	{
		if (!_active) return;
		if (_executioner == null || !IsInstanceValid(_executioner))
		{
			_ = EndExecutionerBossAsync();
		}
	}

	private void UpdateBossUi()
	{
		if (!_active) return;
		var ui = GetTree().GetFirstNodeInGroup("boss_fight_ui") as BossFightUi;
		if (ui == null) return;
		if (_executioner != null && IsInstanceValid(_executioner))
			ui.UpdateBossHealth(_executioner.Health, Mathf.Max(1, _executioner.MaxHealth));
		else
			ui.UpdateBossHealth(0, Mathf.Max(1, _totalBossMaxHealth));
	}

	private async Task EndExecutionerBossAsync()
	{
		if (!_active || _rewardSpawned) return;
		_active = false;
		_rewardSpawned = true;

		var ui = GetTree().GetFirstNodeInGroup("boss_fight_ui") as BossFightUi;
		var spawner = GetTree().GetFirstNodeInGroup("enemy_spawn_manager") as EnemySpawnOnKill;
		var level = GetTree().CurrentScene as MainLevel ?? GetTree().Root.FindChild("Level", true, false) as MainLevel;
		var cycle = GetTree().GetFirstNodeInGroup("day_night_cycle") as DayNightCycle;
		var player = GetTree().GetFirstNodeInGroup("player") as Player;
		var map = GetTree().GetFirstNodeInGroup("map") as Map ?? GetTree().CurrentScene?.GetNodeOrNull<Map>("Map");

		if (ui != null)
		{
			ui.UpdateBossHealth(0, Mathf.Max(1, _totalBossMaxHealth));
			ui.HideBossBar();
		}

		map?.SetMonumentVisible(true);

		if (player != null)
		{
			player.SetBossMovementLocked(false);
			// Lamp flickers back on after beating the boss.
			await player.FlickerBossLampOnAsync(1.0f);
			player.SetBossLampDisabled(false);
		}

		SpawnBossRewardCoins();

		StopBossMusic();
		if (cycle != null)
		{
			cycle.ResumeTime();
			// Let cycle recompute color on next process from current frozen time.
		}
		level?.ResumeBackgroundMusicAfterBoss();
		if (spawner != null)
		{
			spawner.BossFightActive = false;
			spawner.ResetForNewMap();
		}

		_executioner = null;
		_totalBossMaxHealth = 1;
		_hasPreviousCycleColor = false;
	}

	private void EndBossCleanupNoReward()
	{
		if (!_active) return;
		_active = false;
		var spawner = GetTree().GetFirstNodeInGroup("enemy_spawn_manager") as EnemySpawnOnKill;
		var level = GetTree().CurrentScene as MainLevel ?? GetTree().Root.FindChild("Level", true, false) as MainLevel;
		var cycle = GetTree().GetFirstNodeInGroup("day_night_cycle") as DayNightCycle;
		var player = GetTree().GetFirstNodeInGroup("player") as Player;
		var map = GetTree().GetFirstNodeInGroup("map") as Map ?? GetTree().CurrentScene?.GetNodeOrNull<Map>("Map");
		player?.SetBossMovementLocked(false);
		player?.SetBossLampDisabled(false);
		map?.SetMonumentVisible(true);
		StopBossMusic();
		cycle?.ResumeTime();
		level?.ResumeBackgroundMusicAfterBoss();
		if (spawner != null) { spawner.BossFightActive = false; spawner.ResetForNewMap(); }
		_executioner = null;
		_triggerQueued = false;
		_hasPreviousCycleColor = false;
	}

	private void SpawnBossRewardCoins()
	{
		// 6th world reset (boss index 2): scale rewards accordingly (significantly higher than first boss).
		int baseReward = _bossIndex <= 1 ? 150 : 500;
		double scaled = baseReward * Mathf.Pow(1.35, Mathf.Max(0, _bossIndex - 2));
		int coinCount = Mathf.Max(1, Mathf.RoundToInt((float)scaled));

		Node2D playerNode = GetTree().GetFirstNodeInGroup("player") as Node2D;
		Vector2 center = playerNode != null ? playerNode.GlobalPosition : Vector2.Zero;
		PackedScene coinScene = GD.Load<PackedScene>("res://items/coin.tscn");
		for (int i = 0; i < coinCount; i++)
		{
			Node coinNode = coinScene.Instantiate();
			if (coinNode is not Node2D coin) { coinNode.QueueFree(); continue; }
			float angle = (float)GD.RandRange(0.0, Mathf.Tau);
			float dist = (float)GD.RandRange(8.0, 64.0);
			coin.GlobalPosition = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * dist;
			GetTree().CurrentScene.AddChild(coin);
		}
	}

	private void ClearArenaForBossFight()
	{
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
				if (n.IsInGroup("executioner_boss")) continue;
				n.QueueFree();
			}
		}

		var scene = GetTree().CurrentScene;
		if (scene == null) return;
		foreach (Node child in scene.GetChildren())
		{
			if (!IsInstanceValid(child)) continue;
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
		Vector2 pos = map != null ? map.MonumentWorldPosition + new Vector2(0, 40) : player.GlobalPosition;
		player.GlobalPosition = pos;
	}

	private AudioStreamPlayer? _bossMusicPlayer;

	private AudioStreamPlayer? PlaySfxAndReturn(string resPath, out double lengthSeconds)
	{
		lengthSeconds = 0.0;
		if (!ResourceLoader.Exists(resPath)) return null;
		var stream = GD.Load<AudioStream>(resPath);
		if (stream == null) return null;
		lengthSeconds = stream.GetLength();
		var p = new AudioStreamPlayer { Stream = stream, ProcessMode = ProcessModeEnum.Always, VolumeDb = -2.0f };
		AddChild(p);
		p.Finished += p.QueueFree;
		p.Play();
		return p;
	}

	private void PlayBossMusic(string resPath)
	{
		if (_bossMusicPlayer != null && IsInstanceValid(_bossMusicPlayer))
		{
			_bossMusicPlayer.Stop();
			_bossMusicPlayer.QueueFree();
			_bossMusicPlayer = null;
		}
		if (!ResourceLoader.Exists(resPath)) return;
		var stream = GD.Load<AudioStream>(resPath);
		if (stream == null) return;
		if (stream is AudioStreamOggVorbis ogg) ogg.Loop = true;
		else stream.Set("loop", true);
		_bossMusicPlayer = new AudioStreamPlayer { Stream = stream, ProcessMode = ProcessModeEnum.Always, VolumeDb = -6.0f };
		AddChild(_bossMusicPlayer);
		_bossMusicPlayer.Play();
	}

	private void StopBossMusic()
	{
		if (_bossMusicPlayer == null || !IsInstanceValid(_bossMusicPlayer)) return;
		_bossMusicPlayer.Stop();
		_bossMusicPlayer.QueueFree();
		_bossMusicPlayer = null;
	}
}
