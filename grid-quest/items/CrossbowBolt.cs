using Godot;
using System.Collections.Generic;

public partial class CrossbowBolt : Area2D
{
	[Export] public float Speed { get; set; } = 420.0f;
	[Export] public int Damage { get; set; } = 4;
	[Export] public int PierceCount { get; set; } = 3; // number of enemies it can pass through

	private Vector2 _dir = Vector2.Right;
	private float _life = 2.5f;
	private float _knockbackStrength = 0.0f;
	private global::GridQuest.Entities.Entity _sourceEntity;
	private readonly HashSet<ulong> _hitTargets = new();

	public override void _Ready()
	{
		AddToGroup("projectiles");
		BodyEntered += OnBodyEntered;
		AreaEntered += OnAreaEntered;
		var sprite = GetNodeOrNull<Sprite2D>("Sprite2D");
		if (sprite != null)
		{
			sprite.Texture = GD.Load<Texture2D>("res://assets/images/fx/crossbow__bolt.png");
		}
	}

	public void Init(Vector2 direction, Vector2 fromPos, float speed = 420.0f, int damage = 4, float knockbackStrength = 0.0f, global::GridQuest.Entities.Entity sourceEntity = null, int pierceCount = 3)
	{
		GlobalPosition = fromPos;
		Speed = speed;
		Damage = damage;
		_knockbackStrength = knockbackStrength;
		_sourceEntity = sourceEntity;
		PierceCount = Mathf.Max(1, pierceCount);
		_dir = direction.LengthSquared() > 0.0001f ? direction.Normalized() : Vector2.Right;
		// Sprite bottom should point in firing direction.
		Rotation = _dir.Angle() + Mathf.Pi / 2.0f;
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

		if (body.IsInGroup("entities") && body != _sourceEntity && body.HasMethod("TakeDamage"))
		{
			ulong id = body.GetInstanceId();
			if (_hitTargets.Contains(id))
				return;
			_hitTargets.Add(id);

			int damage = Damage;
			if (_sourceEntity != null && IsInstanceValid(_sourceEntity))
			{
				damage = _sourceEntity.ComputeDamageForTarget(body, Damage);
			}

			if (body.HasMethod("TakeDamageAmount"))
			{
				if (_sourceEntity != null && IsInstanceValid(_sourceEntity))
					body.Call("TakeDamageAmount", damage, _sourceEntity);
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
			if (_sourceEntity != null && IsInstanceValid(_sourceEntity))
			{
				_sourceEntity.NotifyProjectileHit(body);
			}

			// Heavy piercing: pass through multiple targets, then expire.
			PierceCount--;
			if (PierceCount <= 0)
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
		Node current = body;
		while (current != null)
		{
			if (current.IsInGroup("walls"))
				return true;
			current = current.GetParent();
		}
		return false;
	}

	private void OnAreaEntered(Area2D area) { }
}
