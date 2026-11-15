using Godot;

public partial class Fireball : Area2D
{
	[Export] public float Speed { get; set; } = 220.0f;
	[Export] public int Damage { get; set; } = 2;
	private Vector2 _dir = Vector2.Right;
	private float _life = 2.0f;
	private float _knockbackStrength = 0.0f;
	private global::GridQuest.Entities.Player.PlayerEntity _sourcePlayerEntity;

	public override void _Ready()
	{
		AddToGroup("projectiles");
		BodyEntered += OnBodyEntered;
		AreaEntered += OnAreaEntered;
		var sprite = GetNodeOrNull<Sprite2D>("Sprite2D");
		if (sprite != null)
		{
			sprite.Texture = GD.Load<Texture2D>("res://assets/images/fx/meta_fire1.png");
		}
	}

	public void Init(Vector2 direction, Vector2 fromPos, float speed = 220.0f, int damage = 2, float knockbackStrength = 0.0f, global::GridQuest.Entities.Player.PlayerEntity sourcePlayer = null)
	{
		_sourcePlayerEntity = sourcePlayer;
		GlobalPosition = fromPos;
		Speed = speed;
		Damage = damage;
		_knockbackStrength = knockbackStrength;
		_dir = direction.Normalized();
		// Bias toward the nearest entity once when the projectile is created.
		Node2D nearestEntity = null;
		float best = 300.0f * 300.0f;
		foreach (Node n in GetTree().GetNodesInGroup("entities"))
		{
			if (n is Node2D e && IsInstanceValid(e) && e != _sourcePlayerEntity)
			{
				float d2 = fromPos.DistanceSquaredTo(e.GlobalPosition);
				if (d2 < best) { best = d2; nearestEntity = e; }
			}
		}
		if (nearestEntity != null)
		{
			Vector2 toEntity = (nearestEntity.GlobalPosition - fromPos).Normalized();
			_dir = _dir.Lerp(toEntity, 0.3f).Normalized();
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

		if (body.IsInGroup("entities") && body != _sourcePlayerEntity && body.HasMethod("TakeDamage"))
		{
			int damage = Damage;
			if (_sourcePlayerEntity != null && IsInstanceValid(_sourcePlayerEntity))
			{
				damage = _sourcePlayerEntity.ComputeDamageForTarget(body as Node2D, Damage);
			}

			if (body.HasMethod("TakeDamageAmount"))
			{
				if (_sourcePlayerEntity != null && IsInstanceValid(_sourcePlayerEntity))
					body.Call("TakeDamageAmount", damage, _sourcePlayerEntity);
				else
					body.Call("TakeDamageAmount", damage, (Node2D?)null);
			}
			else
			{
				body.Call("TakeDamage", damage);
			}

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
