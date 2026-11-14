
using Godot;

public partial class Player : CharacterBody2D
{
	[Signal]
	public delegate void DiedEventHandler();

	[Export] public float Speed { get; set; } = 90.0f;
	[Export] public float AttackCooldown { get; set; } = 0.45f;
	[Export] public int MaxHealth { get; set; } = 5;
	[Export] public int AttackDamage { get; set; } = 1;
	[Export] public float AttackRange { get; set; } = 32.0f;

	private const int FrameSize = 32;
	private const string PlayerTexturePath = "res://assets/images/characters/cute_Player.png";

	private AnimatedSprite2D _sprite;
	private ProgressBar _healthBar;
	private State _state = State.Idle;
	private string _facing = "down";
	private bool _faceLeft;
	private float _attackTimer;

	// --- TEMPORARY DEBUG (remove when no longer needed): press H to toggle invulnerability ---
	private bool _debugInvulnerable;

	private enum State
	{
		Idle,
		Walk,
		Attack,
		Dead
	}

	public int Health { get; private set; }
	public int Gold { get; private set; }
	public bool IsDead => _state == State.Dead;

	public override void _Ready()
	{
		MotionMode = MotionModeEnum.Floating;
		Health = MaxHealth;
		_sprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
		_healthBar = GetNodeOrNull<ProgressBar>("HealthBar");
		_sprite.SpriteFrames = BuildSpriteFrames();
		_sprite.AnimationFinished += OnAnimationFinished;
		_sprite.Play("idle_down");
		UpdateHealthBar();
	}

	public void AddGold(int amount)
	{
		if (amount <= 0) return;
		Gold += amount;
		GD.Print($"Gold: {Gold}");
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		// --- TEMPORARY DEBUG (remove when no longer needed): press H to toggle invulnerability ---
		if (@event is InputEventKey keyEvent && keyEvent.Pressed && !keyEvent.Echo && keyEvent.Keycode == Key.H)
		{
			_debugInvulnerable = !_debugInvulnerable;
			GD.Print($"[TEMP DEBUG] Player invulnerability: {_debugInvulnerable}");
			GetViewport().SetInputAsHandled();
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_state == State.Dead)
		{
			Velocity = Vector2.Zero;
			MoveAndSlide();
			return;
		}

		_attackTimer = Mathf.Max(0.0f, _attackTimer - (float)delta);

		if (_state != State.Attack && Input.IsKeyPressed(Key.Space) && _attackTimer <= 0.0f)
		{
			StartAttack();
		}

		// WASD and arrow key movement (including during attacks).
		Vector2 input = Vector2.Zero;

		if (Input.IsKeyPressed(Key.A) || Input.IsKeyPressed(Key.Left))
			input.X -= 1;

		if (Input.IsKeyPressed(Key.D) || Input.IsKeyPressed(Key.Right))
			input.X += 1;

		if (Input.IsKeyPressed(Key.W) || Input.IsKeyPressed(Key.Up))
			input.Y -= 1;

		if (Input.IsKeyPressed(Key.S) || Input.IsKeyPressed(Key.Down))
			input.Y += 1;

		input = input.Normalized();

		if (_state == State.Attack)
		{
			Velocity = input * Speed;
			MoveAndSlide();
			return;
		}

		if (input == Vector2.Zero)
		{
			Velocity = Vector2.Zero;
			_state = State.Idle;
			PlayAnimation($"idle_{_facing}");
		}
		else
		{
			UpdateFacing(input);
			Velocity = input * Speed;
			_state = State.Walk;
			PlayAnimation($"walk_{_facing}");
		}

		MoveAndSlide();
	}

	public void TakeDamage(int amount)
	{
		if (_state == State.Dead || amount <= 0)
		{
			return;
		}

		// --- TEMPORARY DEBUG (remove when no longer needed): ignore damage while invulnerable ---
		if (_debugInvulnerable)
		{
			return;
		}

		Health = Mathf.Max(0, Health - amount);
		UpdateHealthBar();
		if (Health == 0)
		{
			Die();
		}
	}

	public void Die()
	{
		if (_state == State.Dead)
		{
			return;
		}

		_state = State.Dead;
		Velocity = Vector2.Zero;
		_sprite.FlipH = _facing == "side" && _faceLeft;
		PlayAnimation("death");
		EmitSignal(SignalName.Died);
	}

	private void StartAttack()
	{
		_state = State.Attack;
		_attackTimer = AttackCooldown;
		_sprite.FlipH = _facing == "side" && _faceLeft;
		PlayAnimation($"attack_{_facing}");
		DamageEnemiesInRange();
	}

	private void DamageEnemiesInRange()
	{
		DamageGroupInRange("enemies");
		DamageGroupInRange("knights");
	}

	private void DamageGroupInRange(string group)
	{
		foreach (Node node in GetTree().GetNodesInGroup(group))
		{
			if (node is Node2D target
				&& target.GlobalPosition.DistanceTo(GlobalPosition) <= AttackRange
				&& target.HasMethod("TakeDamage"))
			{
				target.Call("TakeDamage", AttackDamage);
			}
		}
	}

	private void UpdateFacing(Vector2 input)
	{
		if (Mathf.Abs(input.X) >= Mathf.Abs(input.Y))
		{
			_facing = "side";
			_faceLeft = input.X < 0.0f;
		}
		else
		{
			_facing = input.Y < 0.0f ? "up" : "down";
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

	private void OnAnimationFinished()
	{
		if (_state == State.Dead)
		{
			return;
		}

		if (_state == State.Attack)
		{
			_state = State.Idle;
			PlayAnimation($"idle_{_facing}");
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
		Texture2D texture = GD.Load<Texture2D>(PlayerTexturePath);
		SpriteFrames frames = new();
		frames.RemoveAnimation("default");

		AddAnimation(frames, texture, "idle_down", 0, 6, 6.0f, true);
		AddAnimation(frames, texture, "idle_side", 1, 6, 6.0f, true);
		AddAnimation(frames, texture, "idle_up", 2, 6, 6.0f, true);
		AddAnimation(frames, texture, "walk_down", 3, 6, 10.0f, true);
		AddAnimation(frames, texture, "walk_side", 4, 6, 10.0f, true);
		AddAnimation(frames, texture, "walk_up", 5, 6, 10.0f, true);
		AddAnimation(frames, texture, "attack_down", 6, 4, 12.0f, false);
		AddAnimation(frames, texture, "attack_side", 7, 4, 12.0f, false);
		AddAnimation(frames, texture, "attack_up", 8, 4, 12.0f, false);
		AddAnimation(frames, texture, "death", 9, 4, 8.0f, false);

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
