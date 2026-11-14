using Godot;

public partial class Enemy : CharacterBody2D
{
	[Export] public float Speed { get; set; } = 50.0f;
	[Export] public int MaxHealth { get; set; } = 3;

	private AnimatedSprite2D _sprite;
	private ProgressBar _healthBar;
	private Node2D _player;

	public int Health { get; private set; }

	public override void _Ready()
	{
		Health = MaxHealth;
		_sprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
		_healthBar = GetNode<ProgressBar>("HealthBar");
		_sprite.Play("idle");
		AddToGroup("enemies");
		_player = GetTree().GetFirstNodeInGroup("player") as Node2D;
		UpdateHealthBar();
	}

	public override void _PhysicsProcess(double delta)
	{
		if (Health <= 0)
		{
			Velocity = Vector2.Zero;
			return;
		}

		if (_player == null || !IsInstanceValid(_player))
		{
			_player = GetTree().GetFirstNodeInGroup("player") as Node2D;
		}

		if (_player == null)
		{
			Velocity = Vector2.Zero;
			MoveAndSlide();
			return;
		}

		Vector2 direction = (_player.GlobalPosition - GlobalPosition).Normalized();
		Velocity = direction * Speed;
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

	private void UpdateHealthBar()
	{
		if (_healthBar == null)
		{
			return;
		}

		_healthBar.MaxValue = MaxHealth;
		_healthBar.Value = Health;
	}
}
