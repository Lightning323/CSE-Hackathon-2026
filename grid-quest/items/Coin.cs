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
		frames.SetAnimationLoopMode("spin", SpriteFrames.LoopMode.Linear);
		frames.SetAnimationSpeed("spin", 10.0);
		// Use available coin art assets for drop animation.
		foreach (string path in new[]
		{
			"res://assets/images/items/coin/meta_coin1.png",
			"res://assets/images/items/coin/meta_coin2.png",
			"res://assets/images/items/coin/meta_coin3.png",
			"res://assets/images/items/coin/meta_coin4.png",
		})
		{
			var tex = GD.Load<Texture2D>(path);
			if (tex != null)
				frames.AddFrame("spin", tex);
		}
		if (frames.GetFrameCount("spin") == 0)
		{
			var fallback = GD.Load<Texture2D>("res://assets/images/items/coin/meta_coin1.png");
			if (fallback != null) frames.AddFrame("spin", fallback);
		}
		_sprite.SpriteFrames = frames;
		_sprite.Play("spin");

		Monitoring = true;
		Monitorable = true;
		Monitoring = false; // enable for overlap after pop+idle delay
		BodyEntered += OnBodyEntered;
		CallDeferred(nameof(InitializePop));
	}

	private void InitializePop()
	{
		_popStart = GlobalPosition;
		float ang = (float)GD.RandRange(0.0, Mathf.Tau);
		_popEnd = _popStart + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * PopDistance;
		_popElapsed = 0.0f;
		_timer = PopTime + IdleTime;
		_magnetizing = false;
	}

	public override void _PhysicsProcess(double delta)
	{
		float dt = (float)delta;

		if (_popElapsed < PopTime)
		{
			_popElapsed += dt;
			float t = Mathf.Clamp(_popElapsed / PopTime, 0.0f, 1.0f);
			// Ease-out pop so the drop feels snappy then settles before magneting.
			float eased = 1.0f - Mathf.Pow(1.0f - t, 3.0f);
			GlobalPosition = _popStart.Lerp(_popEnd, eased);
			return;
		}

		if (_timer > PopTime)
		{
			_timer -= dt;
			if (_timer <= PopTime)
			{
				Monitoring = true; // enable pickup overlap after pop+idle delay
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
			Vector2 toPlayer = player.GlobalPosition - GlobalPosition;
			float d = toPlayer.Length();
			if (d <= 8.0f)
			{
				Collect(player);
				return;
			}

			Vector2 dir = toPlayer / d;
			// Smooth acceleration into the player for a satisfying magnet feel.
			float speed = MagnetSpeed + Mathf.Clamp((MagnetRange - d) * 1.2f, 0.0f, 120.0f);
			GlobalPosition += dir * speed * dt;
			if (GlobalPosition.DistanceTo(player.GlobalPosition) <= 8.0f)
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

	private bool _collected;

	private void Collect(Node2D player)
	{
		if (_collected) return;
		_collected = true;
		SetDeferred(Area2D.PropertyName.Monitoring, false);
		if (player != null && IsInstanceValid(player) && player.HasMethod("AddGold"))
		{
			player.Call("AddGold", 1);
		}
		PlayPickupSfx();
		QueueFree();
	}

	private void PlayPickupSfx()
	{
		var stream = GD.Load<AudioStream>("res://assets/audio/sfx/forest_item.ogg");
		if (stream == null) return;

		var sfx = new AudioStreamPlayer2D
		{
			Stream = stream,
			GlobalPosition = GlobalPosition,
			VolumeDb = -10.0f
		};
		var parent = GetTree().CurrentScene ?? GetParent();
		parent?.AddChild(sfx);
		sfx.Finished += sfx.QueueFree;
		sfx.Play();
	}
}
