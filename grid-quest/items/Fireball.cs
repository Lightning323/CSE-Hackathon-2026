using Godot;

public partial class Fireball : Area2D
{
	[Export] public float Speed { get; set; } = 220.0f;
	[Export] public int Damage { get; set; } = 2;
	private Vector2 _dir = Vector2.Right;
	private float _life = 2.0f;
	private float _knockbackStrength = 0.0f;
	private Player _sourcePlayer;
	private global::GridQuest.Entities.Player.PlayerEntity _sourcePlayerEntity;

	public override void _Ready()
	{
		BodyEntered += OnBodyEntered;
		AreaEntered += OnAreaEntered;
		var sprite = GetNodeOrNull<Sprite2D>("Sprite2D");
		if (sprite != null)
		{
			sprite.Texture = GD.Load<Texture2D>("res://assets/images/fx/meta_fire1.png");
		}
	}

	public void Init(Vector2 direction, Vector2 fromPos, float speed = 220.0f, int damage = 2, float knockbackStrength = 0.0f, Player sourcePlayer = null)
	{
		_sourcePlayerEntity = sourcePlayer;

		GlobalPosition = fromPos;
		Speed = speed;
		Damage = damage;
		_knockbackStrength = knockbackStrength;
		_sourcePlayer = sourcePlayer;
		_sourcePlayerEntity = sourcePlayer;
		_dir = direction.Normalized();
		// bias toward nearest enemy (one-time)
		Node2D nearest = null;
		float best = 300.0f * 300.0f;
		foreach (Node n in GetTree().GetNodesInGroup("enemies"))
		{
			if (n is Node2D e && IsInstanceValid(e))
			{
				float d2 = fromPos.DistanceSquaredTo(e.GlobalPosition);
				if (d2 < best) { best = d2; nearest = e; }
			}
		}
		if (nearest != null)
		{
			Vector2 toEnemy = (nearest.GlobalPosition - fromPos).Normalized();
			_dir = _dir.Lerp(toEnemy, 0.3f).Normalized(); // small sway/bias, can overshoot
		}
		Rotation = _dir.Angle();
	}

	public override void _PhysicsProcess(double delta)
	{
		_life -= (float)delta;
		if (_life <= 0) { QueueFree(); return; }
		GlobalPosition += _dir * Speed * (float)delta;
	}

	private void OnBodyEntered(Node2D body)
	{
		if (body == null || !IsInstanceValid(body))
			return;

		if (body.IsInGroup("enemies") && body.HasMethod("TakeDamage"))
		{
			int damage = Damage;
			if (_sourcePlayerEntity != null && IsInstanceValid(_sourcePlayerEntity))
			{
				damage = _sourcePlayerEntity.ComputeDamageForTarget(body as Node2D, Damage);
			}
			body.Call("TakeDamage", damage);
			if (_knockbackStrength > 0.0f && body.HasMethod("ApplyKnockback"))
			{
				body.Call("ApplyKnockback", _dir, _knockbackStrength);
			}
			if (_sourcePlayerEntity != null && IsInstanceValid(_sourcePlayerEntity))
			{
				_sourcePlayerEntity.NotifyProjectileHit(body as Node2D);
			}
			QueueFree();
			return;
		}

		if (IsWallBody(body))
		{
			QueueFree();
		}
	}

	private static bool IsWallBody(Node body)
	{
		// Stop on wall objects (border/building walls), but allow trees/props and other bodies.
		Node current = body;
		while (current != null)
		{
			if (current.IsInGroup("walls"))
				return true;
			current = current.GetParent();
		}
		return false;
	}

	private void OnAreaEntered(Area2D area)
	{
		// ignore
	}
}
