using Godot;

public partial class Enemy : CharacterBody2D
{
	[Export] public float Speed { get; set; } = 50.0f;
	[Export] public int MaxHealth { get; set; } = 3;

	private const int FrameSize = 32;
	private const string EnemyTexturePath = "res://assets/Cute_Fantasy_Free/Enemies/Skeleton.png";

	private AnimatedSprite2D _sprite;
	private ProgressBar _healthBar;
	private Node2D _player;
	private string _facing = "down";
	private bool _faceLeft;

	public int Health { get; private set; }

	public override void _Ready()
	{
		Health = MaxHealth;
		_sprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
		_healthBar = GetNode<ProgressBar>("HealthBar");
		_sprite.SpriteFrames = BuildSpriteFrames();
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

		if (_player == null || !IsInstanceValid(_player))
		{
			_player = GetTree().GetFirstNodeInGroup("player") as Node2D;
		}

		if (_player == null)
		{
			Velocity = Vector2.Zero;
			PlayAnimation($"idle_{_facing}");
			MoveAndSlide();
			return;
		}

		Vector2 toPlayer = _player.GlobalPosition - GlobalPosition;
		if (toPlayer.LengthSquared() < 1.0f)
		{
			Velocity = Vector2.Zero;
			PlayAnimation($"idle_{_facing}");
			MoveAndSlide();
			return;
		}

		Vector2 direction = toPlayer.Normalized();
		UpdateFacing(direction);
		Velocity = direction * Speed;
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
		Texture2D texture = GD.Load<Texture2D>(EnemyTexturePath);
		SpriteFrames frames = new();
		frames.RemoveAnimation("default");

		AddAnimation(frames, texture, "idle_down", 0, 6, 6.0f, true);
		AddAnimation(frames, texture, "idle_side", 1, 6, 6.0f, true);
		AddAnimation(frames, texture, "idle_up", 2, 6, 6.0f, true);
		AddAnimation(frames, texture, "walk_down", 3, 6, 10.0f, true);
		AddAnimation(frames, texture, "walk_side", 4, 6, 10.0f, true);
		AddAnimation(frames, texture, "walk_up", 5, 6, 10.0f, true);

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
