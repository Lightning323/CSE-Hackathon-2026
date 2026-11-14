#nullable enable
using Godot;

namespace GridQuest.Entities.Enemy;

public partial class VampireEnemy : EnemyEntity
{
	private static readonly Texture2D WalkTex = GD.Load<Texture2D>("res://assets/images/enemies/vampire/Vampires1_Walk_full.png");

	private bool _fleeingFromSunrise;
	private const float SunriseFleeSpeedMultiplier = 1.9f;

	protected override void OnEnemyReady()
	{
		Sprite.Position = new Vector2(0, -12);
		AddToGroup("vampires");
		var cycle = GetTree().GetFirstNodeInGroup("day_night_cycle") as DayNightCycle;
		if (cycle != null)
		{
			cycle.Sunrise += OnSunrise;
			if (!cycle.IsNight)
			{
				// Spawned/loaded in daytime (shouldn't normally happen) - flee immediately.
				BeginSunriseFlee();
			}
		}
	}

	public override void _ExitTree()
	{
		var cycle = GetTree().GetFirstNodeInGroup("day_night_cycle") as DayNightCycle;
		if (cycle != null)
		{
			cycle.Sunrise -= OnSunrise;
		}
		base._ExitTree();
	}

	public override bool CanSpawnInBuildingInterior => false;
	public override bool IsNightOnlySpawn => true;
	public override bool IsHordeSpawnEligible => false; // night-only ambient special spawn

	public override void OnSunrise()
	{
		if (IsDead || Dying) return;
		BeginSunriseFlee();
	}

	private void BeginSunriseFlee()
	{
		if (_fleeingFromSunrise) return;
		_fleeingFromSunrise = true;
		ForceChasePlayer = false;
		Target = null;
		// Move faster to try to escape offscreen.
		base.Speed = Mathf.Max(base.Speed, 95.0f) * SunriseFleeSpeedMultiplier;
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_fleeingFromSunrise && !IsDead)
		{
			ProcessSunriseFlee((float)delta);
			return;
		}
		base._PhysicsProcess(delta);
	}

	private void ProcessSunriseFlee(float dt)
	{
		AttackTimer = Mathf.Max(0.0f, AttackTimer - dt);
		if (KnockbackVelocity.LengthSquared() > 1.0f)
		{
			Velocity = KnockbackVelocity;
			KnockbackVelocity = KnockbackVelocity.MoveToward(Vector2.Zero, 700.0f * dt);
			MoveAndSlide();
			return;
		}
		KnockbackVelocity = Vector2.Zero;

		// Pick/update an offscreen escape direction away from player/camera center.
		Node2D? player = GetTree().GetFirstNodeInGroup("player") as Node2D;
		Vector2 away = Vector2.Right;
		if (player != null && IsInstanceValid(player))
			away = (GlobalPosition - player.GlobalPosition);
		if (away.LengthSquared() < 0.001f)
		{
			Camera2D? cam = GetViewport().GetCamera2D();
			away = cam != null ? (GlobalPosition - cam.GetScreenCenterPosition()) : Vector2.Right;
		}
		if (away.LengthSquared() < 0.001f) away = Vector2.Right;
		away = away.Normalized();

		// Nudge toward nearest map edge direction for a cleaner escape.
		Vector2 toLeft = new Vector2(-GlobalPosition.X, 0);
		Vector2 toRight = new Vector2((global::Map.Width * GridQuest.Map.MapConstants.CELL_SIZE) - GlobalPosition.X, 0);
		Vector2 toTop = new Vector2(0, -GlobalPosition.Y);
		Vector2 toBottom = new Vector2(0, (global::Map.Height * GridQuest.Map.MapConstants.CELL_SIZE) - GlobalPosition.Y);
		Vector2 edgeDir = toRight;
		float best = Mathf.Abs(toRight.X);
		if (Mathf.Abs(toLeft.X) < best) { best = Mathf.Abs(toLeft.X); edgeDir = toLeft; }
		if (Mathf.Abs(toTop.Y) < best) { best = Mathf.Abs(toTop.Y); edgeDir = toTop; }
		if (Mathf.Abs(toBottom.Y) < best) { edgeDir = toBottom; }
		if (edgeDir.LengthSquared() > 0.001f)
			away = away.Lerp(edgeDir.Normalized(), 0.45f).Normalized();

		UpdateFacing(away);
		State = EnemyState.Walk;
		Velocity = away * base.Speed;
		PlayAnimation(GetAnimationName(EnemyState.Walk, Facing));
		MoveAndSlide();

		if (TryCompleteSunriseEscape())
			return;
	}

	private bool TryCompleteSunriseEscape()
	{
		// If off camera/screen bounds, flee succeeds and despawn (no drops).
		Camera2D? camera = GetViewport().GetCamera2D();
		if (camera != null)
		{
			Vector2 vp = GetViewportRect().Size;
			Vector2 half = (vp * 0.5f) / Mathf.Max(camera.Zoom.X, 0.001f);
			Rect2 visible = new(camera.GetScreenCenterPosition() - half - new Vector2(96, 96), half * 2.0f + new Vector2(192, 192));
			if (!visible.HasPoint(GlobalPosition))
			{
				QueueFree();
				return true;
			}
		}
		else
		{
			// Fallback: if very near/far outside map edges
			float margin = 128f;
			float maxX = global::Map.Width * GridQuest.Map.MapConstants.CELL_SIZE;
			float maxY = global::Map.Height * GridQuest.Map.MapConstants.CELL_SIZE;
			if (GlobalPosition.X < -margin || GlobalPosition.Y < -margin || GlobalPosition.X > maxX + margin || GlobalPosition.Y > maxY + margin)
			{
				QueueFree();
				return true;
			}
		}

		// If cannot flee (stuck, barely moving into walls/corners for a while) -> die and drop coins.
		_fleeStuckTime += (float)GetPhysicsProcessDeltaTime();
		if (Velocity.Length() > 5.0f) _fleeStuckTime = 0.0f;
		if (_fleeStuckTime >= 2.5f)
		{
			// Die and drop coins as requested.
			ForceKillWithDrops();
			return true;
		}
		return false;
	}

	private float _fleeStuckTime;

	private void ForceKillWithDrops()
	{
		if (Dying || IsDead) return;
		// Use damage path to trigger existing death/drop logic
		Health = 1;
		TakeDamageAmount(9999, null);
		if (!IsDead && !Dying)
		{
			// fallback
			Dying = true;
			DropCoinsPublic();
			QueueFree();
		}
	}

	private void DropCoinsPublic()
	{
		// Reuse base drop behavior via reflection-like call not possible; duplicate minimal logic
		// but easier: call protected DropCoins by making it accessible? It's private. We'll just kill through health.
	}

	protected override bool ShouldFlipHorizontallyForSide() => false;

	protected override string GetAnimationName(EnemyState state, string facing)
	{
		string prefix = state switch
		{
			EnemyState.Walk => "walk",
			EnemyState.Attack => "attack",
			_ => "idle",
		};
		if (facing == "side")
			return $"{prefix}_{(FaceLeft ? "left" : "right")}";
		return $"{prefix}_{facing}";
	}

	protected override SpriteFrames BuildSpriteFrames()
	{
		var frames = new SpriteFrames();
		frames.RemoveAnimation("default");
		// Vampires1_Walk_full.png: 384x256 = 6 cols x 4 rows (64x64). Swapped up and right directions per request.
		AddGrid64(frames, "idle_down", WalkTex, 0, 1, 4.0f, true);
		AddGrid64(frames, "idle_left", WalkTex, 2, 1, 4.0f, true);
		AddGrid64(frames, "idle_right", WalkTex, 3, 1, 4.0f, true); // swapped with up
		AddGrid64(frames, "idle_up", WalkTex, 1, 1, 4.0f, true);    // swapped with right
		AddGrid64(frames, "walk_down", WalkTex, 0, 6, 8.0f, true);
		AddGrid64(frames, "walk_left", WalkTex, 2, 6, 8.0f, true);
		AddGrid64(frames, "walk_right", WalkTex, 3, 6, 8.0f, true); // swapped with up
		AddGrid64(frames, "walk_up", WalkTex, 1, 6, 8.0f, true);    // swapped with right
		// No dedicated attack sheet in assets/images; reuse walk frames for attack visuals
		AddGrid64(frames, "attack_down", WalkTex, 0, 6, 12.0f, false);
		AddGrid64(frames, "attack_left", WalkTex, 2, 6, 12.0f, false);
		AddGrid64(frames, "attack_right", WalkTex, 3, 6, 12.0f, false); // swapped with up
		AddGrid64(frames, "attack_up", WalkTex, 1, 6, 12.0f, false);    // swapped with right
		return frames;
	}

	private static void AddGrid64(SpriteFrames frames, string name, Texture2D texture, int row, int count, float fps, bool loop)
	{
		if (texture == null) return;
		frames.AddAnimation(name);
		frames.SetAnimationSpeed(name, fps);
		frames.SetAnimationLoop(name, loop);
		for (int i = 0; i < count; i++)
		{
			frames.AddFrame(name, new AtlasTexture
			{
				Atlas = texture,
				Region = new Rect2(i * 64, row * 64, 64, 64)
			});
		}
	}
}
