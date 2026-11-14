using Godot;

public partial class Coin : Area2D
{
	[Export] public float MagnetRange { get; set; } = 90.0f;
	[Export] public float MagnetSpeed { get; set; } = 140.0f;
	[Export] public float IdleTime { get; set; } = 0.8f;
	[Export] public float PopTime { get; set; } = 0.25f;
	[Export] public float PopDistance { get; set; } = 18.0f;

	private AnimatedSprite2D _sprite;
	private float _timer;
	private bool _magnetizing;
	private Vector2 _popStart;
	private Vector2 _popEnd;
	private float _popElapsed;

	public override void _Ready()
	{
		_sprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
		var frames = new SpriteFrames();
		frames.AddAnimation("spin");
		frames.SetAnimationLoop("spin", true);
		frames.SetAnimationSpeed("spin", 10.0);
		frames.AddFrame("spin", GD.Load<Texture2D>("res://assets/images/items/forest_coin-1.png"));
		frames.AddFrame("spin", GD.Load<Texture2D>("res://assets/images/items/forest_coin-2.png"));
		frames.AddFrame("spin", GD.Load<Texture2D>("res://assets/images/items/forest_coin-3.png"));
		frames.AddFrame("spin", GD.Load<Texture2D>("res://assets/images/items/forest_coin-4.png"));
		_sprite.SpriteFrames = frames;
		_sprite.Play("spin");

		_popStart = GlobalPosition;
		float ang = (float)GD.RandRange(0.0, Mathf.Tau);
		_popEnd = _popStart + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * PopDistance;
		_popElapsed = 0.0f;
		_timer = PopTime + IdleTime;
		_magnetizing = false;
		Monitoring = false;
		BodyEntered += OnBodyEntered;
	}

	public override void _PhysicsProcess(double delta)
	{
		float dt = (float)delta;

		if (_popElapsed < PopTime)
		{
			_popElapsed += dt;
			float t = Mathf.Clamp(_popElapsed / PopTime, 0.0f, 1.0f);
			GlobalPosition = _popStart.Lerp(_popEnd, t);
			return;
		}

		if (_timer > PopTime)
		{
			_timer -= dt;
			if (_timer <= PopTime)
			{
				Monitoring = true; // enable pickup after pop+idle
			}
			return;
		}

		Node2D player = GetTree().GetFirstNodeInGroup("player") as Node2D;
		if (player == null || !IsInstanceValid(player))
		{
			return;
		}

		float dist = GlobalPosition.DistanceTo(player.GlobalPosition);
		if (dist <= MagnetRange)
		{
			_magnetizing = true;
		}

		if (_magnetizing)
		{
			Vector2 dir = (player.GlobalPosition - GlobalPosition);
			if (dir.LengthSquared() < 0.01f)
			{
				Collect(player);
				return;
			}
			dir = dir.Normalized();
			GlobalPosition += dir * MagnetSpeed * dt;
			if (GlobalPosition.DistanceTo(player.GlobalPosition) <= 6.0f)
			{
				Collect(player);
			}
		}
	}

	private void OnBodyEntered(Node2D body)
	{
		if (body.IsInGroup("player"))
		{
			Collect(body);
		}
	}

	private void Collect(Node2D player)
	{
		if (player.HasMethod("AddGold"))
		{
			player.Call("AddGold", 1);
		}
		QueueFree();
	}
}
