#nullable enable
using Godot;
using GridQuest.Entities.Npc;
using System;

namespace GridQuest.Entities.Enemy;

public partial class ExecutionerEnemy : EnemyEntity
{
	[Signal] public delegate void BossHealthChangedEventHandler(int current, int max);

	private bool _introSpawnPlaying = true;
	private bool _combatEnabled;
	private double _attackWindupTimer;
	private bool _isAttackWindup;
	private bool _dashBurstActive;
	private double _dashBurstTimer;
	private Vector2 _dashDirection = Vector2.Right;
	private double _teleportCooldownTimer;
	private bool _retreatActive;
	private double _retreatTimer;
	private Vector2 _retreatDirection = Vector2.Left;
	private readonly RandomNumberGenerator _rng = new();

	private PointLight2D? _attackLight;

	public bool CombatEnabled
	{
		get => _combatEnabled;
		set
		{
			_combatEnabled = value;
			if (!_combatEnabled)
			{
				Velocity = Vector2.Zero;
				_isAttackWindup = false;
				_dashBurstActive = false;
				SetAttackLightActive(false);
			}
		}
	}

	public bool IsIntroSpawnPlaying => _introSpawnPlaying;

	public override void _Ready()
	{
		_rng.Randomize();
		TexturePath = "res://assets/images/enemies/Executioner/idle.png";
		base.Speed = 260.0f; // very fast / almost teleporty bursts
		base.MaxHealth = 220; // tuned as a hard single-target boss (~2x harder than hellhound swarm feel)
		base.AttackDamage = 18; // hits very hard
		base.AttackRange = 46.0f;
		base.AttackCooldown = 0.8f;
		AgroRange = 2000.0f;
		HordeAgroRange = 3000.0f;
		ForceChasePlayer = true;
		CoinDropCount = 0;
		base._Ready();

		EnsureAttackLight();
		SetAttackLightActive(false);

		// Base EnemyEntity already subscribes Sprite.AnimationFinished to virtual OnAnimationFinished().
		// Custom boss flow is handled by overriding OnAnimationFinished().
		State = EnemyState.Idle;
		_introSpawnPlaying = true;
		_combatEnabled = false;
		// Stay idle/hidden until boss manager begins the timed spawn-in animation after blackout.
		PlayAnimation("idle");
		Sprite.Visible = false;
	}

	public void BeginSpawnIntroAnimation()
	{
		_introSpawnPlaying = true;
		_combatEnabled = false;
		Sprite.Visible = true;
		Velocity = Vector2.Zero;
		State = EnemyState.Idle;
		PlayAnimation("spawn");
	}

	public void FinishSpawnIntroAndIdle()
	{
		_introSpawnPlaying = false;
		Velocity = Vector2.Zero;
		State = EnemyState.Idle;
		PlayAnimation("idle");
	}

	public override void _PhysicsProcess(double delta)
	{
		float dt = (float)delta;

		if (IsDead)
		{
			Velocity = Vector2.Zero;
			SetAttackLightActive(false);
			MoveAndSlide();
			return;
		}

		if (_introSpawnPlaying || !_combatEnabled)
		{
			Velocity = Vector2.Zero;
			MoveAndSlide();
			return;
		}

		AttackTimer = Mathf.Max(0.0f, AttackTimer - dt);
		_teleportCooldownTimer = Mathf.Max(0.0f, _teleportCooldownTimer - dt);

		// Retreat jump-back after attacks to stay unpredictable.
		if (_retreatActive)
		{
			_retreatTimer -= dt;
			Velocity = _retreatDirection * (Speed * 3.6f);
			MoveAndSlide();
			if (_retreatTimer <= 0.0)
			{
				_retreatActive = false;
				Velocity = Vector2.Zero;
			}
			return;
		}

		// Brief burst movement to feel like teleport/dash around the player.
		if (_dashBurstActive)
		{
			_dashBurstTimer -= dt;
			Velocity = _dashDirection * (Speed * 3.2f);
			MoveAndSlide();
			if (_dashBurstTimer <= 0.0)
			{
				_dashBurstActive = false;
				Velocity = Vector2.Zero;
			}
			return;
		}

		if (_isAttackWindup)
		{
			_attackWindupTimer -= dt;
			Velocity = Vector2.Zero;
			MoveAndSlide();
			if (_attackWindupTimer <= 0.0)
			{
				ExecuteAttackStrike();
			}
			return;
		}

		Target = FindClosestTarget();
		Node2D playerNode = GetTree().GetFirstNodeInGroup("player") as Node2D;
		if (ForceChasePlayer && playerNode != null && IsInstanceValid(playerNode))
			Target = playerNode;

		if (Target == null || !IsInstanceValid(Target))
		{
			State = EnemyState.Idle;
			Velocity = Vector2.Zero;
			PlayAnimation("idle");
			MoveAndSlide();
			return;
		}

		Vector2 toTarget = Target.GlobalPosition - GlobalPosition;
		float dist = toTarget.Length();
		if (dist <= 0.001f)
		{
			Velocity = Vector2.Zero;
			MoveAndSlide();
			return;
		}

		Vector2 dir = toTarget / dist;
		UpdateFacing(dir);

		if (dist <= AttackRange && AttackTimer <= 0.0f)
		{
			StartBossAttackWindup();
			MoveAndSlide();
			return;
		}

		// "Almost teleport around": close distance in fast dashes with short pauses (unpredictable).
		if (_teleportCooldownTimer <= 0.0)
		{
			if (dist > 70.0f || _rng.Randf() < 0.18f)
			{
				_dashBurstActive = true;
				_dashBurstTimer = _rng.RandfRange(0.07f, 0.18f);
				_teleportCooldownTimer = _rng.RandfRange(0.08f, 0.30f);
				Vector2 burstDir = dir;
				if (_rng.Randf() < 0.35f)
				{
					// Occasional lateral feint for unpredictability.
					burstDir = dir.Rotated(_rng.RandfRange(-1.2f, 1.2f)).Normalized();
				}
				_dashDirection = burstDir;
				PlayAnimation("skill");
				MoveAndSlide();
				return;
			}
		}

		State = EnemyState.Walk;
		Velocity = dir * Speed;
		PlayAnimation(dist < 120.0f ? "walk" : "skill");
		MoveAndSlide();
	}

	private void StartBossAttackWindup()
	{
		_isAttackWindup = true;
		_attackWindupTimer = _rng.RandfRange(0.14f, 0.30f);
		State = EnemyState.Attack;
		Velocity = Vector2.Zero;
		SetAttackLightActive(true);
		PlayAttackWarningSfx();
		PlayAnimation("attack");
	}

	private void ExecuteAttackStrike()
	{
		_isAttackWindup = false;
		AttackTimer = AttackCooldown * _rng.RandfRange(0.7f, 1.3f); // unpredictable cadence
		SetAttackLightActive(true);

		if (Target != null && IsInstanceValid(Target))
		{
			float dist = GlobalPosition.DistanceTo(Target.GlobalPosition);
			if (dist <= AttackRange + 10.0f)
			{
				if (Target is global::Player player)
					player.TakeDamageAmount(AttackDamage, this);
				else if (Target is KnightEntity knight)
					knight.TakeDamage(AttackDamage);
				else if (Target is VillagerEntity villager)
					villager.TakeDamage(AttackDamage);
				else if (Target.HasMethod("TakeDamage"))
					Target.Call("TakeDamage", AttackDamage);
			}
		}

		// Jump back after attempting an attack (unpredictable retreat).
		Vector2 away = Vector2.Left;
		if (Target != null && IsInstanceValid(Target))
		{
			Vector2 delta = GlobalPosition - Target.GlobalPosition;
			away = delta.LengthSquared() > 0.001f ? delta.Normalized() : (FaceLeft ? Vector2.Right : Vector2.Left);
		}
		// Add slight randomness to retreat angle/distance feel.
		away = away.Rotated(_rng.RandfRange(-0.5f, 0.5f)).Normalized();
		_retreatDirection = away;
		_retreatActive = true;
		_retreatTimer = _rng.RandfRange(0.12f, 0.22f);
		_dashBurstActive = false;
		State = EnemyState.Walk;
		PlayAnimation("skill");

		// Keep the green light briefly visible through the attack strike.
		GetTree().CreateTimer(0.16f, false).Timeout += () =>
		{
			if (IsInstanceValid(this)) SetAttackLightActive(false);
		};
	}

	public override int TakeDamageAmount(int amount, Node2D? source = null)
	{
		if (_introSpawnPlaying || IsDead || amount <= 0) return 0;
		int applied = base.TakeDamageAmount(amount, source);
		EmitSignal(SignalName.BossHealthChanged, Health, MaxHealth);
		if (IsDead)
		{
			CombatEnabled = false;
			SetAttackLightActive(false);
			Velocity = Vector2.Zero;
			PlayAnimation("death");
		}
		return applied;
	}

	public override void TakeDamage(int amount)
	{
		TakeDamageAmount(amount, null);
	}

	protected override void OnAnimationFinished()
	{
		if (IsDead) { QueueFree(); return; }
		// Spawn intro is timed by the boss manager (exact 8s window / 14s total cue); ignore natural anim end.
		if (_introSpawnPlaying && Sprite.Animation == "spawn")
			return;
		if (State == EnemyState.Attack && !_isAttackWindup)
		{
			State = EnemyState.Idle;
			PlayAnimation("idle");
			SetAttackLightActive(false);
		}
	}

	protected override bool ShouldFlipHorizontallyForSide() => true;

	protected override void UpdateFacing(Vector2 direction)
	{
		FaceLeft = direction.X < 0.0f;
		Facing = "side";
		ApplyFacingFlip();
	}

	protected override string GetAnimationName(EnemyState state, string facing)
	{
		return state switch
		{
			EnemyState.Walk => "walk",
			EnemyState.Attack => "attack",
			_ => "idle",
		};
	}

	protected override SpriteFrames BuildSpriteFrames()
	{
		SpriteFrames frames = new();
		frames.RemoveAnimation("default");
		string basePath = "res://assets/images/enemies/Executioner/";

		// summonDeath.png (150x100, 50x50 frames = 6 frames) used as Executioner spawn-in; 8s duration.
		AddGridAnimation(frames, basePath + "summonDeath.png", "spawn", 0.75f, false, 50, 50);
		AddGridAnimation(frames, basePath + "idle.png", "idle", 8.0f, true, 100, 100);
		AddGridAnimation(frames, basePath + "idle2.png", "walk", 10.0f, true, 100, 100);
		AddGridAnimation(frames, basePath + "skill1.png", "skill", 14.0f, true, 100, 100);
		AddGridAnimation(frames, basePath + "attacking.png", "attack", 14.0f, false, 100, 100);
		AddGridAnimation(frames, basePath + "death.png", "death", 10.0f, false, 100, 100);
		return frames;
	}

	private static void AddGridAnimation(SpriteFrames frames, string texturePath, string animName, float fps, bool loop, int frameW, int frameH)
	{
		Texture2D tex = GD.Load<Texture2D>(texturePath);
		if (tex == null)
		{
			GD.PushWarning($"Executioner animation texture missing: {texturePath}");
			return;
		}
		int cols = Mathf.Max(1, tex.GetWidth() / frameW);
		int rows = Mathf.Max(1, tex.GetHeight() / frameH);
		frames.AddAnimation(animName);
		frames.SetAnimationSpeed(animName, fps);
		frames.SetAnimationLoop(animName, loop);
		for (int y = 0; y < rows; y++)
		{
			for (int x = 0; x < cols; x++)
			{
				frames.AddFrame(animName, new AtlasTexture
				{
					Atlas = tex,
					Region = new Rect2(x * frameW, y * frameH, frameW, frameH)
				});
			}
		}
	}

	private void EnsureAttackLight()
	{
		_attackLight = GetNodeOrNull<PointLight2D>("AttackLight");
		if (_attackLight != null) return;

		var gradient = new Gradient();
		gradient.Colors = new[] { new Color(1, 1, 1, 1), new Color(1, 1, 1, 0) };
		gradient.Offsets = new[] { 0.0f, 1.0f };
		var tex = new GradientTexture2D
		{
			Gradient = gradient,
			Width = 128,
			Height = 128,
			Fill = GradientTexture2D.FillEnum.Radial,
			FillFrom = new Vector2(0.5f, 0.5f),
			FillTo = new Vector2(1.0f, 0.5f),
		};

		_attackLight = new PointLight2D
		{
			Name = "AttackLight",
			Enabled = false,
			Texture = tex,
			Color = new Color(0.35f, 1.0f, 0.45f, 1.0f),
			Energy = 0.45f,
			TextureScale = 0.7f,
			Position = new Vector2(0, -12),
		};
		AddChild(_attackLight);
	}

	private void SetAttackLightActive(bool active)
	{
		if (_attackLight == null) return;
		_attackLight.Enabled = active;
		if (active)
		{
			_attackLight.Energy = 0.5f;
			_attackLight.Color = new Color(0.35f, 1.0f, 0.45f, 1.0f);
		}
	}

	private void PlayAttackWarningSfx()
	{
		const string path = "res://assets/audio/sfx/forest_item.ogg";
		if (!ResourceLoader.Exists(path)) return;
		var stream = GD.Load<AudioStream>(path);
		if (stream == null) return;
		var p = new AudioStreamPlayer { Stream = stream, VolumeDb = -6.0f };
		AddChild(p);
		p.Finished += p.QueueFree;
		p.Play();
	}
}
