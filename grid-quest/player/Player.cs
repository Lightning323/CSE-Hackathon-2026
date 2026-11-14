
using Godot;

public partial class Player : CharacterBody2D
{
	[Signal]
	public delegate void DiedEventHandler();

	[Export] public float Speed { get; set; } = 90.0f;
	[Export] public float AttackCooldown { get; set; } = 0.45f;
	[Export] public int MaxHealth { get; set; } = 3;
	[Export] public int AttackDamage { get; set; } = 1;
	[Export] public float AttackRange { get; set; } = 24.0f;

	private const int FrameSize = 32;
	private const string PlayerTexturePath = "res://assets/Cute_Fantasy_Free/Player/Player.png";

	private AnimatedSprite2D _sprite;
	private State _state = State.Idle;
	private string _facing = "down";
	private bool _faceLeft;
	private float _attackTimer;

	private enum State
	{
		Idle,
		Walk,
		Attack,
		Dead
	}

	public int Health { get; private set; }
	public bool IsDead => _state == State.Dead;

	public override void _Ready()
	{
		Health = MaxHealth;
		_sprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
		_sprite.SpriteFrames = BuildSpriteFrames();
		_sprite.AnimationFinished += OnAnimationFinished;
		_sprite.Play("idle_down");
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

		if (_state == State.Attack)
		{
			Velocity = Vector2.Zero;
			MoveAndSlide();
			return;
		}

		// Spacebar attacks
		if (Input.IsKeyPressed(Key.Space) && _attackTimer <= 0.0f)
		{
			StartAttack();
			return;
		}

		// WASD and arrow key movement
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

		Health = Mathf.Max(0, Health - amount);
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
		Velocity = Vector2.Zero;
		_sprite.FlipH = _facing == "side" && _faceLeft;
		PlayAnimation($"attack_{_facing}");
		DamageEnemiesInRange();
	}

	private void DamageEnemiesInRange()
	{
		foreach (Node node in GetTree().GetNodesInGroup("enemies"))
		{
			if (node is Node2D enemy
				&& enemy.GlobalPosition.DistanceTo(GlobalPosition) <= AttackRange
				&& enemy.HasMethod("TakeDamage"))
			{
				enemy.Call("TakeDamage", AttackDamage);
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
