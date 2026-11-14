#nullable enable
using Godot;
using GridQuest.Entities.Base;

namespace GridQuest.Entities.Npc;

/// <summary>
/// Allied knight NPC: protects village, fights enemies, regenerates out of combat.
/// Extends <see cref="NpcEntity"/> for shared entity stats/inventory/money and LLM/tool control APIs.
/// </summary>
public partial class KnightEntity : NpcEntity
{
	[Signal] public delegate void DiedEventHandler(KnightEntity knight, int villageIndex);

	[Export] public new float Speed { get => base.Speed; set => base.Speed = value; }
	[Export] public new int MaxHealth { get => base.MaxHealth; set => base.MaxHealth = value; }
	[Export] public new int AttackDamage { get => base.AttackDamage; set => base.AttackDamage = value; }
	[Export] public new float AttackRange { get => base.AttackRange; set => base.AttackRange = value; }
	[Export] public new float AttackCooldown { get => base.AttackCooldown; set => base.AttackCooldown = value; }
	[Export] public float RegenPerSecond { get; set; } = 0.3f;
	[Export] public float RegenDelayAfterAttack { get; set; } = 3.0f;
	[Export] public float ProtectRange { get; set; } = 220.0f;

	private const string HeroBasePath = "res://assets/images/characters/";

	private AnimatedSprite2D _sprite;
	private ProgressBar _healthBar;
	private Node2D _targetEnemy;
	private State _state = State.Idle;
	private string _facing = "down";
	private bool _faceLeft;
	private float _attackTimer;
	private float _regenAccumulator;
	private float _timeSinceAttack = 999.0f;
	private Vector2 _patrolTarget;
	private float _patrolWaitTimer;
	private float _patrolRepathTimer;

	private bool _hasHome;
	private Rect2 _homeWorldRect;
	private Vector2 _homeCenterWorld;
	private int _homeVillageIndex = -1;

	private enum State { Idle, Walk, Attack }

	public override void _Ready()
	{
		base.Speed = 55.0f; base.MaxHealth = 8; base.AttackDamage = 1; base.AttackRange = 24.0f; base.AttackCooldown = 1.75f;
		base._Ready(); Health = MaxHealth;
		_sprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
		_healthBar = GetNodeOrNull<ProgressBar>("HealthBar");
		_sprite.SpriteFrames = BuildSpriteFrames();
		_sprite.AnimationFinished += OnAnimationFinished;
		_sprite.Play("idle_down");
		AddToGroup("allies"); AddToGroup("knights"); AddToGroup("village_knights");
		PickNewPatrolTarget(); ApplyHealthBarStyle(); UpdateHealthBar();
	}

	/// <summary>Sets the knight's protected village area/center/index.</summary>
	public void SetHome(Rect2I cellBounds, Vector2I centerCell, int villageIndex)
	{
		const int cellSize = 16;
		_homeWorldRect = new Rect2(cellBounds.Position.X * cellSize, cellBounds.Position.Y * cellSize, cellBounds.Size.X * cellSize, cellBounds.Size.Y * cellSize);
		_homeCenterWorld = new Vector2(centerCell.X * cellSize + cellSize / 2.0f, centerCell.Y * cellSize + cellSize / 2.0f);
		_homeVillageIndex = villageIndex; _hasHome = true; _patrolTarget = _homeCenterWorld;
		RememberState("home_village_index", villageIndex);
		RememberState("home_center_world", _homeCenterWorld);
	}

	public override void _PhysicsProcess(double delta)
	{
		float dt = (float)delta;
		if (IsDead) { Velocity = Vector2.Zero; MoveAndSlide(); return; }
		_timeSinceAttack += dt; ApplyRegen(dt); _attackTimer = Mathf.Max(0.0f, _attackTimer - dt);
		if (_state == State.Attack) { CurrentIntent = NpcIntentMode.Combat; Velocity = Vector2.Zero; MoveAndSlide(); return; }

		_targetEnemy = FindClosestEnemy();
		if (_targetEnemy == null)
		{
			if (CurrentIntent == NpcIntentMode.MoveToPoint && IntentTargetPosition.HasValue)
			{
				MoveTowardPoint(IntentTargetPosition.Value, Speed * 0.7f);
				if (GlobalPosition.DistanceTo(IntentTargetPosition.Value) <= 8.0f) ClearIntent();
				MoveAndSlide(); return;
			}
			if (CurrentIntent == NpcIntentMode.MoveToEntity && !string.IsNullOrWhiteSpace(IntentTargetEntityId))
			{
				var target = ResolveEntityTarget(IntentTargetEntityId);
				if (target != null && IsInstanceValid(target))
				{
					MoveTowardPoint(target.GlobalPosition, Speed * 0.7f);
					if (GlobalPosition.DistanceTo(target.GlobalPosition) <= 16.0f) ClearIntent();
					MoveAndSlide(); return;
				}
				ClearIntent();
			}
			UpdatePatrol(dt); MoveAndSlide(); return;
		}

		Vector2 toTarget = _targetEnemy.GlobalPosition - GlobalPosition; float distance = toTarget.Length();
		if (distance <= 0.001f) { Velocity = Vector2.Zero; _state = State.Idle; CurrentIntent = NpcIntentMode.Combat; PlayAnimation($"idle_{_facing}"); MoveAndSlide(); return; }
		Vector2 direction = toTarget / distance; UpdateFacing(direction);

		if (distance <= AttackRange && _attackTimer <= 0.0f) { StartAttack(); MoveAndSlide(); return; }
		if (distance <= AttackRange) { Velocity = Vector2.Zero; _state = State.Idle; CurrentIntent = NpcIntentMode.Combat; PlayAnimation($"idle_{_facing}"); MoveAndSlide(); return; }
		if (_hasHome && _homeCenterWorld.DistanceTo(_targetEnemy.GlobalPosition) > ProtectRange * 1.05f) { _targetEnemy = null; UpdatePatrol(dt); MoveAndSlide(); return; }

		Velocity = direction * Speed; _state = State.Walk; CurrentIntent = NpcIntentMode.Combat; PlayAnimation($"walk_{_facing}"); MoveAndSlide();
	}

	public override void TakeDamage(int amount) => TakeDamageAmount(amount, null);
	public override int TakeDamageAmount(int amount, Node2D? source = null)
	{
		if (IsDead || amount <= 0) return 0;
		int applied = base.TakeDamageAmount(amount, source); UpdateHealthBar();
		if (Health == 0) { EmitSignal(SignalName.Died, this, _homeVillageIndex); QueueFree(); }
		return applied;
	}

	private void ApplyRegen(float delta)
	{
		if (IsDead || Health >= MaxHealth || RegenPerSecond <= 0.0f || _timeSinceAttack < RegenDelayAfterAttack)
		{ if (_timeSinceAttack < RegenDelayAfterAttack || IsDead || Health >= MaxHealth) _regenAccumulator = 0.0f; return; }
		_regenAccumulator += RegenPerSecond * delta; int heal = Mathf.FloorToInt(_regenAccumulator); if (heal <= 0) return;
		_regenAccumulator -= heal; Health = Mathf.Min(MaxHealth, Health + heal); UpdateHealthBar();
	}

	private Node2D FindClosestEnemy()
	{
		Node2D closest = null; float closestDistSq = float.MaxValue; Vector2 origin = GlobalPosition;
		foreach (Node node in GetTree().GetNodesInGroup("enemies"))
		{
			if (node is not Node2D enemy || !IsInstanceValid(enemy)) continue;
			if (_hasHome)
			{
				if (_homeCenterWorld.DistanceTo(enemy.GlobalPosition) > ProtectRange) continue;
				if (!_homeWorldRect.Grow(ProtectRange * 0.35f).HasPoint(GlobalPosition) && _homeCenterWorld.DistanceTo(enemy.GlobalPosition) > ProtectRange * 0.8f) continue;
			}
			float distSq = origin.DistanceSquaredTo(enemy.GlobalPosition);
			if (distSq < closestDistSq) { closestDistSq = distSq; closest = enemy; }
		}
		if (closest == null && _hasHome)
		{
			float nearDistSq = 96.0f * 96.0f;
			foreach (Node node in GetTree().GetNodesInGroup("enemies"))
			{
				if (node is not Node2D enemy || !IsInstanceValid(enemy)) continue;
				float distSq = origin.DistanceSquaredTo(enemy.GlobalPosition);
				if (distSq < nearDistSq) { nearDistSq = distSq; closest = enemy; }
			}
		}
		return closest;
	}

	private void StartAttack()
	{
		_state = State.Attack; CurrentIntent = NpcIntentMode.Combat; _attackTimer = AttackCooldown; _timeSinceAttack = 0.0f; Velocity = Vector2.Zero;
		_sprite.FlipH = _facing == "side" && _faceLeft; PlayAnimation($"attack_{_facing}");
	}
	private void OnAnimationFinished()
	{
		if (IsDead) return;
		if (_state == State.Attack) { TryDealAttackDamage(); _state = State.Idle; PlayAnimation($"idle_{_facing}"); }
	}
	private void TryDealAttackDamage()
	{
		if (_targetEnemy == null || !IsInstanceValid(_targetEnemy)) _targetEnemy = FindClosestEnemy();
		if (_targetEnemy == null || !IsInstanceValid(_targetEnemy)) return;
		if (_targetEnemy.GlobalPosition.DistanceTo(GlobalPosition) <= AttackRange && _targetEnemy.HasMethod("TakeDamage")) _targetEnemy.Call("TakeDamage", AttackDamage);
	}
	private void UpdateFacing(Vector2 direction)
	{
		if (Mathf.Abs(direction.X) >= Mathf.Abs(direction.Y)) { _facing = "side"; _faceLeft = direction.X < 0.0f; }
		else { _facing = direction.Y < 0.0f ? "up" : "down"; _faceLeft = false; }
		_sprite.FlipH = _facing == "side" && _faceLeft;
	}
	private void PlayAnimation(string animation) { if (_sprite.Animation != animation) _sprite.Play(animation); }
	private void ApplyHealthBarStyle()
	{
		if (_healthBar == null) return;
		var bg = new StyleBoxFlat { BgColor = new Color(0.08f, 0.05f, 0.05f, 0.95f) };
		bg.SetCornerRadiusAll(2);
		var fill = new StyleBoxFlat { BgColor = new Color(1.0f, 0.45f, 0.45f, 1.0f) };
		fill.SetCornerRadiusAll(2);
		_healthBar.AddThemeStyleboxOverride("background", bg);
		_healthBar.AddThemeStyleboxOverride("fill", fill);
		_healthBar.SelfModulate = Colors.White;
	}

	private void UpdateHealthBar() { if (_healthBar == null) return; _healthBar.MaxValue = MaxHealth; _healthBar.Value = Health; }
	private bool IsFarOutsideHome() => _hasHome && !_homeWorldRect.Grow(28.0f).HasPoint(GlobalPosition);

	private void UpdatePatrol(float delta)
	{
		_patrolRepathTimer -= delta;
		if (_hasHome && IsFarOutsideHome()) { MoveTowardPoint(_homeCenterWorld, Speed * 0.7f); return; }
		if (_patrolWaitTimer > 0.0f)
		{
			_patrolWaitTimer -= delta; _state = State.Idle; CurrentIntent = NpcIntentMode.Idle; Velocity = Vector2.Zero; PlayAnimation($"idle_{_facing}");
			if (_patrolWaitTimer <= 0.0f) PickNewPatrolTarget(); return;
		}
		if (_patrolTarget == Vector2.Zero || _patrolRepathTimer <= 0.0f) PickNewPatrolTarget();
		Vector2 toTarget = _patrolTarget - GlobalPosition;
		if (toTarget.Length() <= 6.0f)
		{
			_patrolWaitTimer = (float)GD.RandRange(0.8, 2.5); _state = State.Idle; CurrentIntent = NpcIntentMode.Idle; Velocity = Vector2.Zero; PlayAnimation($"idle_{_facing}"); return;
		}
		MoveTowardPoint(_patrolTarget, Speed * 0.5f);
	}
	private void MoveTowardPoint(Vector2 target, float speed)
	{
		Vector2 toTarget = target - GlobalPosition;
		if (toTarget.LengthSquared() < 0.001f) { Velocity = Vector2.Zero; _state = State.Idle; CurrentIntent = NpcIntentMode.Idle; PlayAnimation($"idle_{_facing}"); return; }
		Vector2 direction = toTarget.Normalized(); UpdateFacing(direction); _state = State.Walk; CurrentIntent = NpcIntentMode.Patrol; Velocity = direction * speed; PlayAnimation($"walk_{_facing}");
	}
	private void PickNewPatrolTarget()
	{
		_patrolRepathTimer = (float)GD.RandRange(2.5, 5.0); Vector2 origin = GlobalPosition;
		if (_hasHome)
		{
			Rect2 area = _homeWorldRect.Grow(-8.0f); if (area.Size.X < 16.0f || area.Size.Y < 16.0f) area = _homeWorldRect;
			for (int i = 0; i < 10; i++) { float x = (float)GD.RandRange(area.Position.X, area.End.X); float y = (float)GD.RandRange(area.Position.Y, area.End.Y); Vector2 candidate = new(x, y); if (candidate.DistanceTo(origin) >= 16.0f) { _patrolTarget = candidate; return; } }
			_patrolTarget = _homeCenterWorld; return;
		}
		Node2D map = GetTree().CurrentScene as Node2D; Node grid = map?.GetNodeOrNull<Node>("MapGrid") ?? map?.GetNodeOrNull<Node>("Map");
		if (grid != null && grid.HasMethod("CellToWorldCenter"))
		{
			int width = grid.Get("Width").AsInt32(); if (width <= 0 && grid.Get("MapWidth").VariantType != Variant.Type.Nil) width = grid.Get("MapWidth").AsInt32();
			int height = grid.Get("Height").AsInt32(); if (height <= 0 && grid.Get("MapHeight").VariantType != Variant.Type.Nil) height = grid.Get("MapHeight").AsInt32();
			if (width > 0 && height > 0)
			{
				for (int i = 0; i < 8; i++) { int x = GD.RandRange(0, width - 1); int y = GD.RandRange(0, height - 1); Vector2 candidate = (Vector2)grid.Call("CellToWorldCenter", x, y); if (candidate.DistanceTo(origin) >= 24.0f) { _patrolTarget = candidate; return; } }
			}
		}
		float angle = (float)GD.RandRange(0.0, Mathf.Tau); float dist = (float)GD.RandRange(24.0, 80.0);
		_patrolTarget = origin + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * dist;
	}

	private static SpriteFrames BuildSpriteFrames()
	{
		SpriteFrames frames = new(); frames.RemoveAnimation("default");
		AddAnimation(frames, "idle_down", new[] { "forest_hero-idle-front.png", "forest_hero-idle-front-2.png" }, 4.0f, true);
		AddAnimation(frames, "idle_side", new[] { "forest_hero-idle-side.png", "forest_hero-idle-side-2.png" }, 4.0f, true);
		AddAnimation(frames, "idle_up", new[] { "forest_hero-idle-back.png", "forest_hero-idle-back-2.png" }, 4.0f, true);
		AddAnimation(frames, "walk_down", new[] { "forest_hero-walk-front-1.png", "forest_hero-walk-front-2.png", "forest_hero-walk-front-3.png", "forest_hero-walk-front-4.png", "forest_hero-walk-front-5.png", "forest_hero-walk-front-6.png" }, 10.0f, true);
		AddAnimation(frames, "walk_side", new[] { "forest_hero-walk-side-1.png", "forest_hero-walk-side-2.png", "forest_hero-walk-side-3.png", "forest_hero-walk-side-4.png", "forest_hero-walk-side-5.png", "forest_hero-walk-side-6.png" }, 10.0f, true);
		AddAnimation(frames, "walk_up", new[] { "forest_hero-walk-back-1.png", "forest_hero-walk-back-2.png", "forest_hero-walk-back-3.png", "forest_hero-walk-back-4.png", "forest_hero-walk-back-5.png", "forest_hero-walk-back-6.png" }, 10.0f, true);
		AddAnimation(frames, "attack_down", new[] { "forest_hero-attack-front-1.png", "forest_hero-attack-front-2.png", "forest_hero-attack-front-3.png" }, 10.0f, false);
		AddAnimation(frames, "attack_side", new[] { "forest_hero-attack-side-1.png", "forest_hero-attack-side-2.png", "forest_hero-attack-side-3.png" }, 10.0f, false);
		AddAnimation(frames, "attack_up", new[] { "forest_hero-attack-back-1.png", "forest_hero-attack-back-2.png", "forest_hero-attack-back-3.png" }, 10.0f, false);
		return frames;
	}
	private static void AddAnimation(SpriteFrames frames, string name, string[] files, float fps, bool loop)
	{
		frames.AddAnimation(name); frames.SetAnimationSpeed(name, fps); frames.SetAnimationLoop(name, loop);
		foreach (string file in files) frames.AddFrame(name, GD.Load<Texture2D>(HeroBasePath + file));
	}

	/// <summary>LLM/tool-readable control state for knight AI and village defense context.</summary>
	public override Godot.Collections.Dictionary GetNpcControlState()
	{
		var state = base.GetNpcControlState();
		state["entity_kind"] = "knight_npc";
		state["is_allied"] = true;
		state["home_village_index"] = _homeVillageIndex;
		state["has_target_enemy"] = _targetEnemy != null && IsInstanceValid(_targetEnemy);
		state["target_enemy_name"] = (_targetEnemy != null && IsInstanceValid(_targetEnemy)) ? _targetEnemy.Name.ToString() : "";
		state["protect_range"] = ProtectRange;
		state["regen_per_second"] = RegenPerSecond;
		return state;
	}

	/// <summary>Tool-call convenience: move to world coordinates.</summary>
	public bool ToolGoTo(float x, float y, string reason = "") => GoToPosition(new Vector2(x, y), reason);
	/// <summary>Tool-call convenience: move to entity by name/group.</summary>
	public bool ToolGoToEntity(string entityId, string reason = "") => GoToEntity(entityId, reason);
	/// <summary>Tool-call convenience: set arbitrary NPC memory/state.</summary>
	public void ToolRemember(string key, string value) => RememberState(key, value);
}
