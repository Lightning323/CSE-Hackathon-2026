using Godot;

public partial class KnightNpc : CharacterBody2D
{
	[Export] public float Speed { get; set; } = 55.0f;
	[Export] public int MaxHealth { get; set; } = 8;
	[Export] public int AttackDamage { get; set; } = 1;
	[Export] public float AttackRange { get; set; } = 24.0f;
	[Export] public float AttackCooldown { get; set; } = 1.75f;
	[Export] public float RegenPerSecond { get; set; } = 0.3f;
	[Export] public float RegenDelayAfterAttack { get; set; } = 3.0f;

	private const string HeroBasePath = "res://assets/images/characters/";

	private AnimatedSprite2D _sprite;
	private ProgressBar _healthBar;
	private Node2D _targetEnemy;
	private State _state = State.Idle;
	private string _facing = "down";
	private bool _faceLeft;
	private float _attackTimer;
	private float _regenAccumulator;
	private float _timeSinceAttack = 999.0f;

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
		_healthBar = GetNodeOrNull<ProgressBar>("HealthBar");
		_sprite.SpriteFrames = BuildSpriteFrames();
		_sprite.AnimationFinished += OnAnimationFinished;
		_sprite.Play("idle_down");
		AddToGroup("allies");
		AddToGroup("knights");
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

		_timeSinceAttack += dt;
		ApplyRegen(dt);
		_attackTimer = Mathf.Max(0.0f, _attackTimer - dt);

		if (_state == State.Attack)
		{
			Velocity = Vector2.Zero;
			MoveAndSlide();
			return;
		}

		_targetEnemy = FindClosestEnemy();
		if (_targetEnemy == null)
		{
			Velocity = Vector2.Zero;
			_state = State.Idle;
			PlayAnimation($"idle_{_facing}");
			MoveAndSlide();
			return;
		}

		Vector2 toTarget = _targetEnemy.GlobalPosition - GlobalPosition;
		float distance = toTarget.Length();
		if (distance <= 0.001f)
		{
			Velocity = Vector2.Zero;
			_state = State.Idle;
			PlayAnimation($"idle_{_facing}");
			MoveAndSlide();
			return;
		}

		Vector2 direction = toTarget / distance;
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
		if (Health <= 0 || amount <= 0)
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

	private void ApplyRegen(float delta)
	{
		if (Health <= 0 || Health >= MaxHealth || RegenPerSecond <= 0.0f || _timeSinceAttack < RegenDelayAfterAttack)
		{
			if (_timeSinceAttack < RegenDelayAfterAttack || Health <= 0 || Health >= MaxHealth)
			{
				_regenAccumulator = 0.0f;
			}
			return;
		}

		_regenAccumulator += RegenPerSecond * delta;
		int heal = Mathf.FloorToInt(_regenAccumulator);
		if (heal <= 0)
		{
			return;
		}

		_regenAccumulator -= heal;
		Health = Mathf.Min(MaxHealth, Health + heal);
		UpdateHealthBar();
	}

	private Node2D FindClosestEnemy()
	{
		Node2D closest = null;
		float closestDistSq = float.MaxValue;
		Vector2 origin = GlobalPosition;

		foreach (Node node in GetTree().GetNodesInGroup("enemies"))
		{
			if (node is not Node2D enemy || !IsInstanceValid(enemy))
			{
				continue;
			}

			float distSq = origin.DistanceSquaredTo(enemy.GlobalPosition);
			if (distSq < closestDistSq)
			{
				closestDistSq = distSq;
				closest = enemy;
			}
		}

		return closest;
	}

	private void StartAttack()
	{
		_state = State.Attack;
		_attackTimer = AttackCooldown;
		_timeSinceAttack = 0.0f;
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
		if (_targetEnemy == null || !IsInstanceValid(_targetEnemy))
		{
			_targetEnemy = FindClosestEnemy();
		}

		if (_targetEnemy == null || !IsInstanceValid(_targetEnemy))
		{
			return;
		}

		if (_targetEnemy.GlobalPosition.DistanceTo(GlobalPosition) <= AttackRange && _targetEnemy.HasMethod("TakeDamage"))
		{
			_targetEnemy.Call("TakeDamage", AttackDamage);
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

	private static SpriteFrames BuildSpriteFrames()
	{
		SpriteFrames frames = new();
		frames.RemoveAnimation("default");

		AddAnimation(frames, "idle_down", new[]
		{
			"forest_hero-idle-front.png",
			"forest_hero-idle-front-2.png"
		}, 4.0f, true);
		AddAnimation(frames, "idle_side", new[]
		{
			"forest_hero-idle-side.png",
			"forest_hero-idle-side-2.png"
		}, 4.0f, true);
		AddAnimation(frames, "idle_up", new[]
		{
			"forest_hero-idle-back.png",
			"forest_hero-idle-back-2.png"
		}, 4.0f, true);

		AddAnimation(frames, "walk_down", new[]
		{
			"forest_hero-walk-front-1.png",
			"forest_hero-walk-front-2.png",
			"forest_hero-walk-front-3.png",
			"forest_hero-walk-front-4.png",
			"forest_hero-walk-front-5.png",
			"forest_hero-walk-front-6.png"
		}, 10.0f, true);
		AddAnimation(frames, "walk_side", new[]
		{
			"forest_hero-walk-side-1.png",
			"forest_hero-walk-side-2.png",
			"forest_hero-walk-side-3.png",
			"forest_hero-walk-side-4.png",
			"forest_hero-walk-side-5.png",
			"forest_hero-walk-side-6.png"
		}, 10.0f, true);
		AddAnimation(frames, "walk_up", new[]
		{
			"forest_hero-walk-back-1.png",
			"forest_hero-walk-back-2.png",
			"forest_hero-walk-back-3.png",
			"forest_hero-walk-back-4.png",
			"forest_hero-walk-back-5.png",
			"forest_hero-walk-back-6.png"
		}, 10.0f, true);

		AddAnimation(frames, "attack_down", new[]
		{
			"forest_hero-attack-front-1.png",
			"forest_hero-attack-front-2.png",
			"forest_hero-attack-front-3.png"
		}, 10.0f, false);
		AddAnimation(frames, "attack_side", new[]
		{
			"forest_hero-attack-side-1.png",
			"forest_hero-attack-side-2.png",
			"forest_hero-attack-side-3.png"
		}, 10.0f, false);
		AddAnimation(frames, "attack_up", new[]
		{
			"forest_hero-attack-back-1.png",
			"forest_hero-attack-back-2.png",
			"forest_hero-attack-back-3.png"
		}, 10.0f, false);

		return frames;
	}

	private static void AddAnimation(SpriteFrames frames, string name, string[] files, float fps, bool loop)
	{
		frames.AddAnimation(name);
		frames.SetAnimationSpeed(name, fps);
		frames.SetAnimationLoopMode(name, loop ? SpriteFrames.LoopMode.Linear : SpriteFrames.LoopMode.None);

		foreach (string file in files)
		{
			Texture2D texture = GD.Load<Texture2D>(HeroBasePath + file);
			frames.AddFrame(name, texture);
		}
	}
}
