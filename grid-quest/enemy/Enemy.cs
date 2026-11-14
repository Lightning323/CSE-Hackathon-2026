using Godot;

public partial class Enemy : CharacterBody2D
{
	[Export] public float Speed { get; set; } = 50.0f;
	[Export] public int MaxHealth { get; set; } = 3;
	[Export] public int AttackDamage { get; set; } = 1;
	[Export] public float AttackRange { get; set; } = 24.0f;
	[Export] public float AttackCooldown { get; set; } = 0.8f;
	[Export(PropertyHint.File, "*.png")] public string TexturePath { get; set; } = "res://assets/images/enemies/skeleton/cute_Skeleton.png";
	[Export] public int IdleFrameCount { get; set; } = 6;
	[Export] public int WalkFrameCount { get; set; } = 6;
	[Export] public int AttackFrameCount { get; set; } = 4;
	[Export] public float IdleFps { get; set; } = 6.0f;
	[Export] public float WalkFps { get; set; } = 10.0f;
	[Export] public float AttackFps { get; set; } = 10.0f;

	private const int FrameSize = 32;

	private AnimatedSprite2D _sprite;
	private ProgressBar _healthBar;
	private Node2D _player;
	private State _state = State.Idle;
	private string _facing = "down";
	private bool _faceLeft;
	private float _attackTimer;

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
		UpdateHealthBar();
	}

	public override void _PhysicsProcess(double delta)
	{
		if (Health <= 0)
		{
			Velocity = Vector2.Zero;
			MoveAndSlide();
			return;
		}

		_attackTimer = Mathf.Max(0.0f, _attackTimer - (float)delta);

		if (_state == State.Attack)
		{
			Velocity = Vector2.Zero;
			MoveAndSlide();
			return;
		}

		_player = FindClosestTarget();

		if (_player == null)
		{
			Velocity = Vector2.Zero;
			_state = State.Idle;
			PlayAnimation($"idle_{_facing}");
			MoveAndSlide();
			return;
		}

		Vector2 toPlayer = _player.GlobalPosition - GlobalPosition;
		float distance = toPlayer.Length();
		if (distance <= 0.001f)
		{
			Velocity = Vector2.Zero;
			_state = State.Idle;
			PlayAnimation($"idle_{_facing}");
			MoveAndSlide();
			return;
		}

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
			QueueFree();
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
			_player.Call("TakeDamage", AttackDamage);
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
