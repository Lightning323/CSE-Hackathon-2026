using Godot;

public partial class Villager : CharacterBody2D
{
	[Export] public float Speed { get; set; } = 90.0f;
	[Export] public float PatrolSpeed { get; set; } = 25.0f;
	[Export] public int MaxHealth { get; set; } = 3;
	[Export] public float FleeDetectionRange { get; set; } = 140.0f;

	private AnimatedSprite2D _sprite;
	private ProgressBar _healthBar;
	private State _state = State.Idle;
	private string _facing = "down";
	private Vector2 _patrolTarget;
	private float _patrolWaitTimer;
	private float _patrolRepathTimer;

	private enum State
	{
		Idle,
		Patrol,
		Flee
	}

	public int Health { get; private set; }

	public override void _Ready()
	{
		MotionMode = MotionModeEnum.Floating;
		Health = MaxHealth;
		_sprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
		_healthBar = GetNodeOrNull<ProgressBar>("HealthBar");

		_sprite.SpriteFrames = BuildSpriteFrames();
		_sprite.Play("idle_down");

		AddToGroup("villagers");
		AddToGroup("civilians");
		PickNewPatrolTarget();
		UpdateHealthBar();
	}

	private SpriteFrames BuildSpriteFrames()
	{
		SpriteFrames frames = new SpriteFrames();
		string basePath = "res://assets/images/characters/villagers/";

		frames.AddAnimation("idle_down");
		frames.AddAnimation("idle_left");
		frames.AddAnimation("idle_right");
		frames.AddAnimation("idle_up");
		frames.AddAnimation("walk_down");
		frames.AddAnimation("walk_left");
		frames.AddAnimation("walk_right");
		frames.AddAnimation("walk_up");

		frames.SetAnimationLoop("idle_down", true);
		frames.SetAnimationLoop("idle_left", true);
		frames.SetAnimationLoop("idle_right", true);
		frames.SetAnimationLoop("idle_up", true);
		frames.SetAnimationLoop("walk_down", true);
		frames.SetAnimationLoop("walk_left", true);
		frames.SetAnimationLoop("walk_right", true);
		frames.SetAnimationLoop("walk_up", true);
		frames.SetAnimationSpeed("idle_down", 4.0);
		frames.SetAnimationSpeed("idle_left", 4.0);
		frames.SetAnimationSpeed("idle_right", 4.0);
		frames.SetAnimationSpeed("idle_up", 4.0);
		frames.SetAnimationSpeed("walk_down", 8.0);
		frames.SetAnimationSpeed("walk_left", 8.0);
		frames.SetAnimationSpeed("walk_right", 8.0);
		frames.SetAnimationSpeed("walk_up", 8.0);

		// Use separated dogwalker frames
		AddFrames(frames, "walk_down", basePath + "walkdown1-crop-folk-dogwalker.png", basePath + "walkdown2-crop-folk-dogwalker.png", basePath + "walkdown3-crop-folk-dogwalker.png", basePath + "walkdown4-crop-folk-dogwalker.png");
		AddFrames(frames, "walk_left", basePath + "walkleft1-crop-folk-dogwalker.png", basePath + "walkleft2-crop-folk-dogwalker.png", basePath + "walkleft3-crop-folk-dogwalker.png", basePath + "walkleft4-crop-folk-dogwalker.png");
		AddFrames(frames, "walk_right", basePath + "walkright1-crop-folk-dogwalker.png", basePath + "walkright2-crop-folk-dogwalker.png", basePath + "walkright3-crop-folk-dogwalker.png", basePath + "walkright4-crop-folk-dogwalker.png");
		AddFrames(frames, "walk_up", basePath + "walkup1-crop-folk-dogwalker.png", basePath + "walkup2-crop-folk-dogwalker.png", basePath + "walkup3-crop-folk-dogwalker.png", basePath + "walkup4-crop-folk-dogwalker.png");

		// Idle = first frame of each walk
		frames.AddFrame("idle_down", frames.GetFrameTexture("walk_down", 0));
		frames.AddFrame("idle_left", frames.GetFrameTexture("walk_left", 0));
		frames.AddFrame("idle_right", frames.GetFrameTexture("walk_right", 0));
		frames.AddFrame("idle_up", frames.GetFrameTexture("walk_up", 0));

		return frames;
	}

	private static void AddFrames(SpriteFrames frames, string anim, params string[] paths)
	{
		foreach (string p in paths)
		{
			Texture2D tex = GD.Load<Texture2D>(p);
			frames.AddFrame(anim, tex);
		}
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

		Node2D threat = FindNearestThreat();
		if (threat != null)
		{
			FleeFrom(threat.GlobalPosition);
			MoveAndSlide();
			return;
		}

		UpdatePatrol(dt);
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

	private void FleeFrom(Vector2 threatPosition)
	{
		_state = State.Flee;
		_patrolWaitTimer = 0.0f;
		Vector2 away = GlobalPosition - threatPosition;
		if (away.LengthSquared() < 0.001f)
		{
			away = Vector2.Down;
		}

		Vector2 direction = away.Normalized();
		UpdateFacing(direction);
		Velocity = direction * Speed;
		PlayAnim($"walk_{_facing}");
	}

	private void UpdatePatrol(float delta)
	{
		_patrolRepathTimer -= delta;

		if (_patrolWaitTimer > 0.0f)
		{
			_patrolWaitTimer -= delta;
			_state = State.Idle;
			Velocity = Vector2.Zero;
			PlayAnim($"idle_{_facing}");
			if (_patrolWaitTimer <= 0.0f)
			{
				PickNewPatrolTarget();
			}
			return;
		}

		Vector2 toTarget = _patrolTarget - GlobalPosition;
		if (toTarget.Length() <= 6.0f || _patrolRepathTimer <= 0.0f)
		{
			_patrolWaitTimer = (float)GD.RandRange(0.8, 2.2);
			_state = State.Idle;
			Velocity = Vector2.Zero;
			PlayAnim($"idle_{_facing}");
			return;
		}

		Vector2 direction = toTarget.Normalized();
		UpdateFacing(direction);
		_state = State.Patrol;
		Velocity = direction * PatrolSpeed;
		PlayAnim($"walk_{_facing}");
	}

	private void PickNewPatrolTarget()
	{
		_patrolRepathTimer = (float)GD.RandRange(2.5, 5.0);
		Vector2 origin = GlobalPosition;
		Node2D map = GetTree().CurrentScene as Node2D;
		Node grid = map?.GetNodeOrNull("MapGrid");
		if (grid != null && grid.HasMethod("CellToWorldCenter"))
		{
			int width = grid.Get("Width").AsInt32();
			int height = grid.Get("Height").AsInt32();
			if (width > 0 && height > 0)
			{
				for (int i = 0; i < 8; i++)
				{
					int x = GD.RandRange(0, width - 1);
					int y = GD.RandRange(0, height - 1);
					Vector2 candidate = (Vector2)grid.Call("CellToWorldCenter", x, y);
					if (candidate.DistanceTo(origin) >= 24.0f)
					{
						_patrolTarget = candidate;
						return;
					}
				}
			}
		}

		float angle = (float)GD.RandRange(0.0, Mathf.Tau);
		float dist = (float)GD.RandRange(24.0, 80.0);
		_patrolTarget = origin + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * dist;
	}

	private Node2D FindNearestThreat()
	{
		Node2D closest = null;
		float closestDistSq = FleeDetectionRange * FleeDetectionRange;
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

	private void UpdateFacing(Vector2 direction)
	{
		if (Mathf.Abs(direction.X) >= Mathf.Abs(direction.Y))
		{
			_facing = direction.X < 0.0f ? "left" : "right";
		}
		else
		{
			_facing = direction.Y < 0.0f ? "up" : "down";
		}
	}

	private void PlayAnim(string anim)
	{
		if (_sprite.Animation != anim)
		{
			_sprite.Play(anim);
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
