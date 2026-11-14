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

	private AnimatedSprite2D _sprite;
	private ProgressBar _healthBar;
	private Node2D _target;
	private State _state = State.Idle;
	private string _facing = "down";
	private bool _faceLeft;
	private float _attackTimer;
	private Vector2 _knockbackVelocity = Vector2.Zero;
	private Vector2 _wanderTarget;
	private float _wanderWaitTimer;
	private float _wanderRepathTimer;

	private enum State { Idle, Walk, Attack }

	public override void _Ready()
	{
		base.Speed = 50.0f;
		base.MaxHealth = 3;
		base.AttackDamage = 1;
		base.AttackRange = 24.0f;
		base.AttackCooldown = 0.8f;
		base._Ready();
		Health = MaxHealth;

		_sprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
		_healthBar = GetNode<ProgressBar>("HealthBar");
		_sprite.SpriteFrames = BuildSpriteFrames();
		_sprite.AnimationFinished += OnAnimationFinished;
		_sprite.Play("idle_down");
		AddToGroup("enemies");
		PickNewWanderTarget();
		UpdateHealthBar();
	}

	public override void _PhysicsProcess(double delta)
	{
		float dt = (float)delta;
		if (IsDead) { Velocity = Vector2.Zero; MoveAndSlide(); return; }

		_attackTimer = Mathf.Max(0.0f, _attackTimer - dt);
		if (_knockbackVelocity.LengthSquared() > 1.0f)
		{
			Velocity = _knockbackVelocity;
			_knockbackVelocity = _knockbackVelocity.MoveToward(Vector2.Zero, 700.0f * dt);
			MoveAndSlide();
			return;
		}
		_knockbackVelocity = Vector2.Zero;

		if (_state == State.Attack) { Velocity = Vector2.Zero; MoveAndSlide(); return; }

		_target = FindClosestTarget();
		Node2D playerNode = GetTree().GetFirstNodeInGroup("player") as Node2D;
		if (ForceChasePlayer && playerNode != null && IsInstanceValid(playerNode)) _target = playerNode;

		if (_target != null)
		{
			Vector2 toPlayer = _target.GlobalPosition - GlobalPosition;
			float distance = toPlayer.Length();
			float agroRange = (ForceChasePlayer && _target == playerNode) ? Mathf.Max(AgroRange, HordeAgroRange) : AgroRange;
			if (distance <= agroRange && distance > 0.001f)
			{
				Vector2 direction = toPlayer / distance;
				UpdateFacing(direction);

				if (distance <= AttackRange && _attackTimer <= 0.0f) { StartAttack(); MoveAndSlide(); return; }
				if (distance <= AttackRange)
				{
					Velocity = Vector2.Zero; _state = State.Idle; PlayAnimation($"idle_{_facing}"); MoveAndSlide(); return;
				}
				Velocity = direction * Speed; _state = State.Walk; PlayAnimation($"walk_{_facing}"); MoveAndSlide(); return;
			}
		}

		UpdateWander(dt);
		MoveAndSlide();
	}

	public override void TakeDamage(int amount) => TakeDamageAmount(amount, null);

	public override int TakeDamageAmount(int amount, Node2D? source = null)
	{
		if (amount <= 0 || IsDead) return 0;
		int applied = base.TakeDamageAmount(amount, source);
		UpdateHealthBar();
		if (Health == 0)
		{
			DropCoins();
			QueueFree();
		}
		return applied;
	}

	public void ApplyKnockback(Vector2 direction, float strength)
	{
		if (IsDead || strength <= 0.0f) return;
		if (direction.LengthSquared() < 0.0001f) return;
		_knockbackVelocity = direction.Normalized() * strength;
	}

	private void DropCoins()
	{
		for (int i = 0; i < CoinDropCount; i++)
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

	private void StartAttack()
	{
		_state = State.Attack; _attackTimer = AttackCooldown; Velocity = Vector2.Zero;
		_sprite.FlipH = _facing == "side" && _faceLeft;
		PlayAnimation($"attack_{_facing}");
	}

	private void OnAnimationFinished()
	{
		if (IsDead) return;
		if (_state == State.Attack)
		{
			TryDealAttackDamage();
			_state = State.Idle;
			PlayAnimation($"idle_{_facing}");
		}
	}

	private void TryDealAttackDamage()
	{
		if (_target == null || !IsInstanceValid(_target)) return;
		if (_target.GlobalPosition.DistanceTo(GlobalPosition) <= AttackRange && _target.HasMethod("TakeDamage"))
			_target.Call("TakeDamage", AttackDamage, this);
	}

	private Node2D FindClosestTarget()
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

	private void UpdateFacing(Vector2 direction)
	{
		if (Mathf.Abs(direction.X) >= Mathf.Abs(direction.Y)) { _facing = "side"; _faceLeft = direction.X < 0.0f; }
		else { _facing = direction.Y < 0.0f ? "up" : "down"; _faceLeft = false; }
		_sprite.FlipH = _facing == "side" && _faceLeft;
	}

	private void PlayAnimation(string animation) { if (_sprite.Animation != animation) _sprite.Play(animation); }

	private void UpdateWander(float delta)
	{
		_wanderRepathTimer -= delta;
		if (_wanderWaitTimer > 0.0f)
		{
			_wanderWaitTimer -= delta; _state = State.Idle; Velocity = Vector2.Zero; PlayAnimation($"idle_{_facing}");
			if (_wanderWaitTimer <= 0.0f) PickNewWanderTarget();
			return;
		}
		if (_wanderTarget == Vector2.Zero || _wanderRepathTimer <= 0.0f) PickNewWanderTarget();

		Vector2 toTarget = _wanderTarget - GlobalPosition;
		if (toTarget.Length() <= 6.0f)
		{
			_wanderWaitTimer = (float)GD.RandRange(0.6, 2.0); _state = State.Idle; Velocity = Vector2.Zero; PlayAnimation($"idle_{_facing}"); return;
		}
		Vector2 dir = toTarget.Normalized();
		UpdateFacing(dir); _state = State.Walk; Velocity = dir * Speed * 0.55f; PlayAnimation($"walk_{_facing}");
	}

	private void PickNewWanderTarget()
	{
		_wanderRepathTimer = (float)GD.RandRange(1.5, 4.0);
		float angle = (float)GD.RandRange(0.0, Mathf.Tau);
		float dist = (float)GD.RandRange(16.0, 64.0);
		_wanderTarget = GlobalPosition + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * dist;
	}

	private void UpdateHealthBar() { if (_healthBar == null) return; _healthBar.MaxValue = MaxHealth; _healthBar.Value = Health; }

	private SpriteFrames BuildSpriteFrames()
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

	private static void AddAnimation(SpriteFrames frames, Texture2D texture, string name, int row, int frameCount, float fps, bool loop)
	{
		frames.AddAnimation(name); frames.SetAnimationSpeed(name, fps); frames.SetAnimationLoop(name, loop);
		for (int frame = 0; frame < frameCount; frame++)
			frames.AddFrame(name, new AtlasTexture { Atlas = texture, Region = new Rect2(frame * FrameSize, row * FrameSize, FrameSize, FrameSize) });
	}

	public override Godot.Collections.Dictionary GetEntityState()
	{
		var state = base.GetEntityState();
		state["entity_kind"] = "enemy";
		state["ai_state"] = _state.ToString();
		state["facing"] = _facing;
		state["has_target"] = _target != null && IsInstanceValid(_target);
		state["target_name"] = (_target != null && IsInstanceValid(_target)) ? _target.Name.ToString() : "";
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
			["ai_state"] = _state.ToString(),
			["facing"] = _facing,
			["has_target"] = _target != null && IsInstanceValid(_target),
			["target_name"] = (_target != null && IsInstanceValid(_target)) ? _target.Name.ToString() : "",
			["force_chase_player"] = ForceChasePlayer,
			["agro_range"] = AgroRange,
			["horde_agro_range"] = HordeAgroRange,
			["wander_target"] = _wanderTarget,
		};
		return state;
	}

	/// <summary>Tool-call convenience: set a move-to-point intent (honored as a wander target override when idle/wandering).</summary>
	public bool ToolGoTo(float x, float y, string reason = "")
	{
		bool ok = GoToPosition(new Vector2(x, y), reason);
		_wanderTarget = new Vector2(x, y);
		_wanderRepathTimer = 0.0f;
		return ok;
	}

	/// <summary>Tool-call convenience: request targeting/movement toward a named entity/group.</summary>
	public bool ToolGoToEntity(string entityId, string reason = "")
	{
		bool ok = GoToEntity(entityId, reason);
		var target = ResolveEntityTarget(entityId);
		if (target != null && IsInstanceValid(target))
			_wanderTarget = target.GlobalPosition;
		return ok;
	}

	/// <summary>Tool-call convenience: write arbitrary NPC memory/state.</summary>
	public void ToolRemember(string key, string value) => RememberState(key, value);

	/// <summary>Tool-call convenience: append chat/control history entry for traces.</summary>
	public void ToolAppendChat(string role, string content) => AppendChatHistory(role, content);
}
