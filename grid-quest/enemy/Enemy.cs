using Godot;

public partial class Enemy : CharacterBody2D
{
	[Export] public float Speed { get; set; } = 50.0f;
	[Export] public int MaxHealth { get; set; } = 3;
	[Export] public int AttackDamage { get; set; } = 1;
	[Export] public float AttackRange { get; set; } = 24.0f;
	[Export] public float AttackCooldown { get; set; } = 0.8f;
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
	private Node2D _player;
	private State _state = State.Idle;
	private string _facing = "down";
	private bool _faceLeft;
	private float _attackTimer;
	private Vector2 _knockbackVelocity = Vector2.Zero;
	private Vector2 _wanderTarget;
	private float _wanderWaitTimer;
	private float _wanderRepathTimer;

	private enum State
	{
		Idle,
		Walk,
		Attack
	}

	public int Health { get; private set; }

	public override void _Ready()
	{
		MotionMode = MotionModeEnum.Floating;
		Health = MaxHealth;
		_sprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
		_healthBar = GetNode<ProgressBar>("HealthBar");
		_sprite.SpriteFrames = BuildSpriteFrames();
		_sprite.AnimationFinished += OnAnimationFinished;
		_sprite.Play("idle_down");
		AddToGroup("enemies");
		_player = GetTree().GetFirstNodeInGroup("player") as Node2D;
		PickNewWanderTarget();
		UpdateHealthBar();
	}

	public override void _PhysicsProcess(double delta)
	{
		float dt = (float)delta;
		if (Health <= 0)
		{
			Velocity = Vector2.Zero;
			MoveAndSlide();
			return;
		}

		_attackTimer = Mathf.Max(0.0f, _attackTimer - dt);
		if (_knockbackVelocity.LengthSquared() > 1.0f)
		{
			Velocity = _knockbackVelocity;
			_knockbackVelocity = _knockbackVelocity.MoveToward(Vector2.Zero, 700.0f * dt);
			MoveAndSlide();
			return;
		}
		_knockbackVelocity = Vector2.Zero;

		if (_state == State.Attack)
		{
			Velocity = Vector2.Zero;
			MoveAndSlide();
			return;
		}

		_player = FindClosestTarget();
		Node2D playerNode = GetTree().GetFirstNodeInGroup("player") as Node2D;
		if (ForceChasePlayer && playerNode != null && IsInstanceValid(playerNode))
			_player = playerNode;

		if (_player != null)
		{
			Vector2 toPlayer = _player.GlobalPosition - GlobalPosition;
			float distance = toPlayer.Length();
			float agroRange = (ForceChasePlayer && _player == playerNode) ? Mathf.Max(AgroRange, HordeAgroRange) : AgroRange;
			if (distance <= agroRange && distance > 0.001f)
			{
				Vector2 direction = toPlayer / distance;
				UpdateFacing(direction);

				if (distance <= AttackRange && _attackTimer <= 0.0f)
				{
					StartAttack();
					MoveAndSlide();
					return;
				}

				if (distance <= AttackRange)
				{
					Velocity = Vector2.Zero;
					_state = State.Idle;
					PlayAnimation($"idle_{_facing}");
					MoveAndSlide();
					return;
				}

				Velocity = direction * Speed;
				_state = State.Walk;
				PlayAnimation($"walk_{_facing}");
				MoveAndSlide();
				return;
			}
		}

		UpdateWander(dt);
		MoveAndSlide();
	}

	public void TakeDamage(int amount)
	{
		if (amount <= 0 || Health <= 0)
		{
			return;
		}

		Health = Mathf.Max(0, Health - amount);
		UpdateHealthBar();
		if (Health == 0)
		{
			DropCoins();
			QueueFree();
		}
	}

	public void ApplyKnockback(Vector2 direction, float strength)
	{
		if (Health <= 0 || strength <= 0.0f) return;
		Vector2 dir = direction;
		if (dir.LengthSquared() < 0.0001f) return;
		_knockbackVelocity = dir.Normalized() * strength;
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
				GetParent().AddChild(coin);
			}
			else
			{
				coinNode.QueueFree();
			}
		}
	}

	private void StartAttack()
	{
		_state = State.Attack;
		_attackTimer = AttackCooldown;
		Velocity = Vector2.Zero;
		_sprite.FlipH = _facing == "side" && _faceLeft;
		PlayAnimation($"attack_{_facing}");
	}

	private void OnAnimationFinished()
	{
		if (Health <= 0)
		{
			return;
		}

		if (_state == State.Attack)
		{
			TryDealAttackDamage();
			_state = State.Idle;
			PlayAnimation($"idle_{_facing}");
		}
	}

	private void TryDealAttackDamage()
	{
		if (_player == null || !IsInstanceValid(_player))
		{
			return;
		}

		if (_player.GlobalPosition.DistanceTo(GlobalPosition) <= AttackRange && _player.HasMethod("TakeDamage"))
		{
			_player.Call("TakeDamage", AttackDamage, this);
		}
	}

	private void UpdateFacing(Vector2 direction)
	{
		if (Mathf.Abs(direction.X) >= Mathf.Abs(direction.Y))
		{
			_facing = "side";
			_faceLeft = direction.X < 0.0f;
		}
		else
		{
			_facing = direction.Y < 0.0f ? "up" : "down";
			_faceLeft = false;
		}

		_sprite.FlipH = _facing == "side" && _faceLeft;
	}

	private void PlayAnimation(string animation)
	{
		if (_sprite.Animation != animation)
		{
			_sprite.Play(animation);
		}
	}

	private void UpdateWander(float delta)
	{
		_wanderRepathTimer -= delta;

		if (_wanderWaitTimer > 0.0f)
		{
			_wanderWaitTimer -= delta;
			_state = State.Idle;
			Velocity = Vector2.Zero;
			PlayAnimation($"idle_{_facing}");
			if (_wanderWaitTimer <= 0.0f)
				PickNewWanderTarget();
			return;
		}

		if (_wanderTarget == Vector2.Zero || _wanderRepathTimer <= 0.0f)
		{
			PickNewWanderTarget();
		}

		Vector2 toTarget = _wanderTarget - GlobalPosition;
		if (toTarget.Length() <= 6.0f)
		{
			_wanderWaitTimer = (float)GD.RandRange(0.6, 2.0);
			_state = State.Idle;
			Velocity = Vector2.Zero;
			PlayAnimation($"idle_{_facing}");
			return;
		}

		Vector2 direction = toTarget.Normalized();
		UpdateFacing(direction);
		_state = State.Walk;
		Velocity = direction * Mathf.Max(10.0f, Speed * 0.4f);
		PlayAnimation($"walk_{_facing}");
	}

	private void PickNewWanderTarget()
	{
		_wanderRepathTimer = (float)GD.RandRange(2.0, 4.5);
		Vector2 origin = GlobalPosition;

		for (int i = 0; i < 8; i++)
		{
			float angle = (float)GD.RandRange(0.0, Mathf.Tau);
			float dist = (float)GD.RandRange(20.0, 72.0);
			Vector2 candidate = origin + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * dist;
			if (candidate.DistanceTo(origin) >= 12.0f)
			{
				_wanderTarget = candidate;
				return;
			}
		}

		_wanderTarget = origin;
	}

	private void UpdateHealthBar()
	{
		if (_healthBar == null)
		{
			return;
		}

		_healthBar.MaxValue = MaxHealth;
		_healthBar.Value = Health;
	}

	private Node2D FindClosestTarget()
	{
		Node2D closest = null;
		float closestDistSq = float.MaxValue;
		Vector2 origin = GlobalPosition;

		foreach (Node node in GetTree().GetNodesInGroup("player"))
		{
			ConsiderTarget(node, origin, ref closest, ref closestDistSq);
		}

		foreach (Node node in GetTree().GetNodesInGroup("knights"))
		{
			ConsiderTarget(node, origin, ref closest, ref closestDistSq);
		}

		foreach (Node node in GetTree().GetNodesInGroup("villagers"))
		{
			ConsiderTarget(node, origin, ref closest, ref closestDistSq);
		}

		return closest;
	}

	private static void ConsiderTarget(Node node, Vector2 origin, ref Node2D closest, ref float closestDistSq)
	{
		if (node is not Node2D target || !IsInstanceValid(target))
		{
			return;
		}

		float distSq = origin.DistanceSquaredTo(target.GlobalPosition);
		if (distSq < closestDistSq)
		{
			closestDistSq = distSq;
			closest = target;
		}
	}

	private SpriteFrames BuildSpriteFrames()
	{
		Texture2D texture = GD.Load<Texture2D>(TexturePath);
		SpriteFrames frames = new();
		frames.RemoveAnimation("default");

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

	private static void AddAnimation(
		SpriteFrames frames,
		Texture2D texture,
		string name,
		int row,
		int frameCount,
		float fps,
		bool loop)
	{
		frames.AddAnimation(name);
		frames.SetAnimationSpeed(name, fps);
		frames.SetAnimationLoopMode(name, loop ? SpriteFrames.LoopMode.Linear : SpriteFrames.LoopMode.None);

		for (int frame = 0; frame < frameCount; frame++)
		{
			AtlasTexture atlasTexture = new()
			{
				Atlas = texture,
				Region = new Rect2(frame * FrameSize, row * FrameSize, FrameSize, FrameSize)
			};
			frames.AddFrame(name, atlasTexture);
		}
	}
}
