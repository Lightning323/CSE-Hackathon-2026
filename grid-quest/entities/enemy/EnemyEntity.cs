#nullable enable
using Godot;
using GridQuest.Entities.Base;

namespace GridQuest.Entities.Enemy;

/// <summary>
/// Hostile enemy entity implementation.
/// Inherits shared <see cref="NpcEntity"/> (and thus <see cref="Entity"/>) so enemies expose the same core data
/// plus LLM/tool-friendly intent, chat history, and structured state APIs for control/debugging.
/// </summary>
public partial class EnemyEntity : NpcEntity
{
	[Export] public new float Speed { get => base.Speed; set => base.Speed = value; }
	[Export] public new int MaxHealth { get => base.MaxHealth; set => base.MaxHealth = value; }
	[Export] public new int AttackDamage { get => base.AttackDamage; set => base.AttackDamage = value; }
	[Export] public new float AttackRange { get => base.AttackRange; set => base.AttackRange = value; }
	[Export] public new float AttackCooldown { get => base.AttackCooldown; set => base.AttackCooldown = value; }

	[Export] public float AgroRange { get; set; } = 180.0f;
	[Export] public float HordeAgroRange { get; set; } = 1200.0f;
	[Export] public bool ForceChasePlayer { get; set; } = false;
	[Export] public int CoinDropCount { get; set; } = 1;
	[Export(PropertyHint.File, "*.png")] public string TexturePath { get; set; } = "res://assets/images/enemies/skeleton/cute_Skeleton.png";
	[Export] public int IdleFrameCount { get; set; } = 6;
	[Export] public int WalkFrameCount { get; set; } = 6;
	[Export] public int AttackFrameCount { get; set; } = 4;
	[Export] public float IdleFps { get; set; } = 6.0f;
	[Export] public float WalkFps { get; set; } = 10.0f;
	[Export] public float AttackFps { get; set; } = 10.0f;

	private const int FrameSize = 32;
	private static readonly PackedScene CoinScene = GD.Load<PackedScene>("res://items/coin.tscn");

	protected AnimatedSprite2D Sprite = null!;
	protected ProgressBar HealthBarNode = null!;
	protected Node2D? Target;
	protected EnemyState State = EnemyState.Idle;
	protected string Facing = "down";
	protected bool FaceLeft;
	protected float AttackTimer;
	protected Vector2 KnockbackVelocity = Vector2.Zero;
	protected Vector2 WanderTarget;
	protected float WanderWaitTimer;
	protected float WanderRepathTimer;
	protected bool Dying;

	protected enum EnemyState { Idle, Walk, Attack }

	public override void _Ready()
	{
		base.Speed = 50.0f;
		base.MaxHealth = 3;
		base.AttackDamage = 1;
		base.AttackRange = 24.0f;
		base.AttackCooldown = 0.8f;
		base._Ready();
		Health = MaxHealth;

		Sprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
		HealthBarNode = GetNode<ProgressBar>("HealthBar");
		Sprite.SpriteFrames = BuildSpriteFrames();
		Sprite.AnimationFinished += OnAnimationFinished;
		PlayAnimation(GetAnimationName(EnemyState.Idle, Facing));
		AddToGroup("enemies");
		PickNewWanderTarget();
		ApplyHealthBarStyle();
		UpdateHealthBar();
		OnEnemyReady();
	}

	public override void _PhysicsProcess(double delta)
	{
		float dt = (float)delta;
		if (IsDead) { Velocity = Vector2.Zero; MoveAndSlide(); return; }

		AttackTimer = Mathf.Max(0.0f, AttackTimer - dt);
		if (KnockbackVelocity.LengthSquared() > 1.0f)
		{
			Velocity = KnockbackVelocity;
			KnockbackVelocity = KnockbackVelocity.MoveToward(Vector2.Zero, 700.0f * dt);
			MoveAndSlide();
			return;
		}
		KnockbackVelocity = Vector2.Zero;

		if (State == EnemyState.Attack) { Velocity = Vector2.Zero; MoveAndSlide(); return; }

		Target = FindClosestTarget();
		Node2D playerNode = GetTree().GetFirstNodeInGroup("player") as Node2D;
		if (ForceChasePlayer && playerNode != null && IsInstanceValid(playerNode)) Target = playerNode;

		if (Target != null)
		{
			Vector2 toPlayer = Target.GlobalPosition - GlobalPosition;
			float distance = toPlayer.Length();
			float agroRange = (ForceChasePlayer && Target == playerNode) ? Mathf.Max(AgroRange, HordeAgroRange) : AgroRange;
			if (distance <= agroRange && distance > 0.001f)
			{
				Vector2 direction = toPlayer / distance;
				UpdateFacing(direction);

				if (distance <= AttackRange && AttackTimer <= 0.0f) { StartAttack(); MoveAndSlide(); return; }
				if (distance <= AttackRange)
				{
					Velocity = Vector2.Zero; State = EnemyState.Idle; PlayAnimation(GetAnimationName(EnemyState.Idle, Facing)); MoveAndSlide(); return;
				}
				Velocity = direction * Speed; State = EnemyState.Walk; PlayAnimation(GetAnimationName(EnemyState.Walk, Facing)); MoveAndSlide(); return;
			}
		}

		UpdateWander(dt);
		MoveAndSlide();
	}

	public override void TakeDamage(int amount)
	{
		TakeDamageAmount(amount, null);
	}

	public override int TakeDamageAmount(int amount, Node2D? source = null)
	{
		if (amount <= 0 || IsDead || Dying) return 0;
		int applied = base.TakeDamageAmount(amount, source);
		UpdateHealthBar();
		if (Health <= 0)
		{
			Die();
		}
		return applied;
	}

	private void Die()
	{
		if (Dying) return;
		Dying = true;
		Velocity = Vector2.Zero;
		DropCoins();
		PlayDeathSfx();
		QueueFree();
	}

	private void PlayDeathSfx()
	{
		var stream = GD.Load<AudioStream>("res://assets/audio/sfx/forest_enemy-death.ogg");
		if (stream == null) return;

		var sfx = new AudioStreamPlayer2D
		{
			Stream = stream,
			GlobalPosition = GlobalPosition,
			VolumeDb = -4.0f
		};
		var parent = GetParent() ?? GetTree().CurrentScene;
		parent?.AddChild(sfx);
		sfx.Finished += sfx.QueueFree;
		sfx.Play();
	}

	public void ApplyKnockback(Vector2 direction, float strength)
	{
		if (IsDead || strength <= 0.0f) return;
		if (direction.LengthSquared() < 0.0001f) return;
		KnockbackVelocity = direction.Normalized() * strength;
	}

	private void DropCoins()
	{
		int dropCount = GetScaledGoldDropCount(CoinDropCount);
		for (int i = 0; i < dropCount; i++)
		{
			Node coinNode = CoinScene.Instantiate();
			if (coinNode is Node2D coin)
			{
				Vector2 offset = new Vector2((float)GD.RandRange(-8, 8), (float)GD.RandRange(-8, 8));
				coin.GlobalPosition = GlobalPosition + offset;
				GetParent()?.AddChild(coin);
			}
			else coinNode.QueueFree();
		}
	}

	private int GetScaledGoldDropCount(int baseGoldAmount)
	{
		if (baseGoldAmount <= 0) return 0;
		var player = GetTree()?.GetFirstNodeInGroup("player");
		if (player != null && IsInstanceValid(player) && player.HasMethod("GetScaledMobGoldDrop"))
		{
			Variant result = player.Call("GetScaledMobGoldDrop", baseGoldAmount);
			if (result.VariantType == Variant.Type.Int) return Mathf.Max(0, result.AsInt32());
			if (result.VariantType == Variant.Type.Float) return Mathf.Max(0, Mathf.RoundToInt((float)result.AsDouble()));
		}
		return baseGoldAmount;
	}

	private void StartAttack()
	{
		State = EnemyState.Attack; AttackTimer = AttackCooldown; Velocity = Vector2.Zero;
		ApplyFacingFlip();
		PlayAnimation(GetAnimationName(EnemyState.Attack, Facing));
	}

	protected virtual void OnAnimationFinished()
	{
		if (IsDead) return;
		if (State == EnemyState.Attack)
		{
			TryDealAttackDamage();
			State = EnemyState.Idle;
			PlayAnimation(GetAnimationName(EnemyState.Idle, Facing));
		}
	}

	private void TryDealAttackDamage()
	{
		if (Target == null || !IsInstanceValid(Target)) return;
		if (Target.GlobalPosition.DistanceTo(GlobalPosition) <= AttackRange && Target.HasMethod("TakeDamage"))
			Target.Call("TakeDamage", AttackDamage, this);
	}

	protected Node2D? FindClosestTarget()
	{
		Node2D closest = null;
		float bestDist = float.MaxValue;
		Vector2 origin = GlobalPosition;
		foreach (string group in new[] { "player", "knights", "villagers", "civilians" })
		{
			foreach (Node node in GetTree().GetNodesInGroup(group))
			{
				if (node is not Node2D n2d || !IsInstanceValid(n2d)) continue;
				float d = origin.DistanceSquaredTo(n2d.GlobalPosition);
				if (d < bestDist) { bestDist = d; closest = n2d; }
			}
		}
		return closest;
	}

	protected virtual void UpdateFacing(Vector2 direction)
	{
		if (Mathf.Abs(direction.X) >= Mathf.Abs(direction.Y)) { Facing = "side"; FaceLeft = direction.X < 0.0f; }
		else { Facing = direction.Y < 0.0f ? "up" : "down"; FaceLeft = false; }
		ApplyFacingFlip();
	}

	protected void PlayAnimation(string animation) { if (Sprite.Animation != animation) Sprite.Play(animation); }

	private void UpdateWander(float delta)
	{
		WanderRepathTimer -= delta;
		if (WanderWaitTimer > 0.0f)
		{
			WanderWaitTimer -= delta; State = EnemyState.Idle; Velocity = Vector2.Zero; PlayAnimation(GetAnimationName(EnemyState.Idle, Facing));
			if (WanderWaitTimer <= 0.0f) PickNewWanderTarget();
			return;
		}
		if (WanderTarget == Vector2.Zero || WanderRepathTimer <= 0.0f) PickNewWanderTarget();

		Vector2 toTarget = WanderTarget - GlobalPosition;
		if (toTarget.Length() <= 6.0f)
		{
			WanderWaitTimer = (float)GD.RandRange(0.6, 2.0); State = EnemyState.Idle; Velocity = Vector2.Zero; PlayAnimation(GetAnimationName(EnemyState.Idle, Facing)); return;
		}
		Vector2 dir = toTarget.Normalized();
		UpdateFacing(dir); State = EnemyState.Walk; Velocity = dir * Speed * 0.55f; PlayAnimation(GetAnimationName(EnemyState.Walk, Facing));
	}

	private void PickNewWanderTarget()
	{
		WanderRepathTimer = (float)GD.RandRange(1.5, 4.0);
		float angle = (float)GD.RandRange(0.0, Mathf.Tau);
		float dist = (float)GD.RandRange(16.0, 64.0);
		WanderTarget = GlobalPosition + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * dist;
	}

	private void ApplyHealthBarStyle()
	{
		if (HealthBarNode == null) return;
		var bg = new StyleBoxFlat { BgColor = new Color(0.08f, 0.05f, 0.05f, 0.95f) };
		bg.SetCornerRadiusAll(2);
		var fill = new StyleBoxFlat { BgColor = new Color(1.0f, 0.45f, 0.45f, 1.0f) };
		fill.SetCornerRadiusAll(2);
		HealthBarNode.AddThemeStyleboxOverride("background", bg);
		HealthBarNode.AddThemeStyleboxOverride("fill", fill);
		HealthBarNode.SelfModulate = Colors.White;
	}

	public void SetCurrentHealthForBoss(int value)
	{
		Health = Mathf.Clamp(value, 0, Mathf.Max(1, MaxHealth));
		UpdateHealthBar();
	}

	private void UpdateHealthBar() { if (HealthBarNode == null) return; HealthBarNode.MaxValue = MaxHealth; HealthBarNode.Value = Health; }


	protected virtual void OnEnemyReady() { }


	protected virtual bool ShouldFlipHorizontallyForSide() => true;

	protected void ApplyFacingFlip()
	{
		Sprite.FlipH = Facing == "side" && FaceLeft && ShouldFlipHorizontallyForSide();
	}

	protected virtual string GetAnimationName(EnemyState state, string facing)
	{
		string prefix = state switch
		{
			EnemyState.Walk => "walk",
			EnemyState.Attack => "attack",
			_ => "idle",
		};
		return $"{prefix}_{facing}";
	}

	public virtual bool CanSpawnInBuildingInterior => true;
	public virtual bool CanSpawnInDungeonOnly => false;
	public virtual bool IsNightOnlySpawn => false;
	public virtual bool IsDayThreeOnlySpawn => false;
	public virtual bool IsHordeSpawnEligible => true;
	public virtual bool IsAmbientSpawnEligible => true;
	public virtual bool ShouldForceChasePlayerOnHordeSpawn => true;

	public virtual void OnSunrise() { }

	protected virtual SpriteFrames BuildSpriteFrames()
	{
		Texture2D texture = GD.Load<Texture2D>(TexturePath);
		SpriteFrames frames = new(); frames.RemoveAnimation("default");
		AddAnimation(frames, texture, "idle_down", 0, IdleFrameCount, IdleFps, true);
		AddAnimation(frames, texture, "idle_side", 1, IdleFrameCount, IdleFps, true);
		AddAnimation(frames, texture, "idle_up", 2, IdleFrameCount, IdleFps, true);
		AddAnimation(frames, texture, "walk_down", 3, WalkFrameCount, WalkFps, true);
		AddAnimation(frames, texture, "walk_side", 4, WalkFrameCount, WalkFps, true);
		AddAnimation(frames, texture, "walk_up", 5, WalkFrameCount, WalkFps, true);
		AddAnimation(frames, texture, "attack_down", 6, AttackFrameCount, AttackFps, false);
		AddAnimation(frames, texture, "attack_side", 7, AttackFrameCount, AttackFps, false);
		AddAnimation(frames, texture, "attack_up", 8, AttackFrameCount, AttackFps, false);
		return frames;
	}

	protected static void AddAnimation(SpriteFrames frames, Texture2D texture, string name, int row, int frameCount, float fps, bool loop)
	{
		frames.AddAnimation(name); frames.SetAnimationSpeed(name, fps); frames.SetAnimationLoop(name, loop);
		for (int frame = 0; frame < frameCount; frame++)
			frames.AddFrame(name, new AtlasTexture { Atlas = texture, Region = new Rect2(frame * FrameSize, row * FrameSize, FrameSize, FrameSize) });
	}

	public override Godot.Collections.Dictionary GetEntityState()
	{
		var state = base.GetEntityState();
		state["entity_kind"] = "enemy";
		state["aiState"] = State.ToString();
		state["facing"] = Facing;
		state["hasTarget"] = Target != null && IsInstanceValid(Target);
		state["target_name"] = (Target != null && IsInstanceValid(Target)) ? Target.Name.ToString() : "";
		state["force_chase_player"] = ForceChasePlayer;
		state["agro_range"] = AgroRange;
		state["coin_drop_count"] = CoinDropCount;
		return state;
	}

	/// <summary>LLM/tool-readable NPC control state including enemy AI/targeting details.</summary>
	public override Godot.Collections.Dictionary GetNpcControlState()
	{
		var state = base.GetNpcControlState();
		state["entity_kind"] = "enemy";
		state["enemy_ai"] = new Godot.Collections.Dictionary
		{
			["aiState"] = State.ToString(),
			["facing"] = Facing,
			["hasTarget"] = Target != null && IsInstanceValid(Target),
			["target_name"] = (Target != null && IsInstanceValid(Target)) ? Target.Name.ToString() : "",
			["force_chase_player"] = ForceChasePlayer,
			["agro_range"] = AgroRange,
			["horde_agro_range"] = HordeAgroRange,
			["wanderTarget"] = WanderTarget,
		};
		return state;
	}

	/// <summary>Tool-call convenience: set a move-to-point intent (honored as a wander target override when idle/wandering).</summary>
	public bool ToolGoTo(float x, float y, string reason = "")
	{
		bool ok = GoToPosition(new Vector2(x, y), reason);
		WanderTarget = new Vector2(x, y);
		WanderRepathTimer = 0.0f;
		return ok;
	}

	/// <summary>Tool-call convenience: request targeting/movement toward a named entity/group.</summary>
	public bool ToolGoToEntity(string entityId, string reason = "")
	{
		bool ok = GoToEntity(entityId, reason);
		var target = ResolveEntityTarget(entityId);
		if (target != null && IsInstanceValid(target))
			WanderTarget = target.GlobalPosition;
		return ok;
	}

	/// <summary>Tool-call convenience: write arbitrary NPC memory/state.</summary>
	public void ToolRemember(string key, string value) => RememberState(key, value);

	/// <summary>Tool-call convenience: append chat/control history entry for traces.</summary>
	public void ToolAppendChat(string role, string content) => AppendChatHistory(role, content);
}
