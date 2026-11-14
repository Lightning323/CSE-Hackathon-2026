#nullable enable
using Godot;
using GridQuest.Entities.Base;
using GridQuest.Items;
using GridQuest.PlayerUpgrades;
using System;
using System.Collections.Generic;

namespace GridQuest.Entities.Player;

/// <summary>
/// Player-controlled entity implementation.
/// Inherits shared <see cref="Entity"/> state (health, inventory, money, weapon slot) and adds
/// input/movement, upgrades, combat, lighting, and progression behavior.
/// </summary>
public partial class PlayerEntity : Entity
{
	[Signal]
	public delegate void DiedEventHandler();

	[Export] public new float Speed { get => base.Speed; set => base.Speed = value; }
	[Export] public new float AttackCooldown { get => base.AttackCooldown; set => base.AttackCooldown = value; }
	[Export] public new int MaxHealth { get => base.MaxHealth; set => base.MaxHealth = value; }
	[Export] public new int AttackDamage { get => base.AttackDamage; set => base.AttackDamage = value; }
	[Export] public new float AttackRange { get => base.AttackRange; set => base.AttackRange = value; }

	private const int FrameSize = 32;
	private const string PlayerTexturePath = "res://assets/images/characters/cute_Player.png";
	private static readonly PackedScene FireballScene = GD.Load<PackedScene>("res://items/fireball.tscn");

	private AnimatedSprite2D _sprite;
	private ProgressBar _healthBar;
	private PointLight2D _pointLight;
	private Camera2D _camera;
	private AudioStreamPlayer _hurtSfx;
	private AudioStreamPlayer _slashSfx;
	private State _state = State.Idle;
	private string _facing = "down";
	private bool _faceLeft;
	private float _attackTimer;
	private Vector2 _velocityCurrent = Vector2.Zero;

	private readonly Dictionary<string, int> _upgradeLevels = new();
	private float _damageRegenDelayTimer;
	private float _regenAccumulator;
	private float _damageSpeedBuffTimer;
	private float _killSpeedBuffTimer;
	private float _lifestealAccumulator;
	private float _aoeAuraTickTimer;
	private Node2D _aoeAuraVisual;
	private Line2D _aoeAuraLine;

	public bool DebugInvulnerable { get; set; }

	private enum State { Idle, Walk, Attack, Dead }

	/// <summary>Compatibility alias for wallet currency.</summary>
	public int Gold { get => Money; set => Money = value; }
	/// <summary>Legacy weapon label kept for compatibility with existing code/UI.</summary>
	public string EquippedWeapon { get; set; } = "sword";

	public override bool IsDead => _state == State.Dead || base.IsDead;

	public int GetUpgradeLevel(string id) => _upgradeLevels.TryGetValue(id, out int lvl) ? lvl : 0;
	public int GetUpgradeCost(string id)
	{
		var def = UpgradeCatalog.Get(id);
		return def?.GetCost(GetUpgradeLevel(id)) ?? int.MaxValue;
	}

	private int BaseMaxHealth => 5;
	private float BaseSpeed => 90.0f;
	private float BaseAttackCooldown => 0.45f;
	private int BaseAttackDamage => 1;
	private float BaseAttackRange => 32.0f;
	private float BaseProjectileSpeed => 220.0f;
	private int BaseProjectileDamage => 2;

	public int CurrentMaxHealth => BaseMaxHealth + GetUpgradeLevel("maximum_health");
	public int CurrentAttackDamage => BaseAttackDamage + GetUpgradeLevel("attack_damage");
	public float CurrentAttackRange => BaseAttackRange + GetUpgradeLevel("melee_range") * 4.0f;
	public float CurrentAttackCooldown => Mathf.Max(0.08f, BaseAttackCooldown / (1.0f + GetUpgradeLevel("attack_speed") * 0.12f));
	public float CurrentProjectileSpeed => BaseProjectileSpeed * (1.0f + GetUpgradeLevel("projectile_speed") * 0.12f);
	public int CurrentProjectileDamage => BaseProjectileDamage + GetUpgradeLevel("projectile_damage");
	public float CurrentKnockbackStrength => 40.0f * GetUpgradeLevel("knockback_strength");
	public float CurrentLifestealPerHit => GetUpgradeLevel("lifesteal") * 0.25f;
	public int CurrentThornsDamage => GetUpgradeLevel("thorns_damage");
	public float CurrentArmorReductionPerHit => Mathf.Min(0.75f, GetUpgradeLevel("damage_reduction_armor") * 0.08f);
	public float CurrentRegenPerSecond => GetUpgradeLevel("slow_health_regeneration") * 0.35f;
	public float CurrentRegenDelay => Mathf.Max(0.25f, 3.0f - GetUpgradeLevel("health_regeneration_delay") * 0.25f);
	public float CurrentAcceleration => 900.0f + GetUpgradeLevel("player_acceleration") * 220.0f;
	public float CurrentMoveSpeed
	{
		get
		{
			float mult = 1.0f + GetUpgradeLevel("player_movement_speed") * 0.08f;
			if (_damageSpeedBuffTimer > 0.0f) mult += GetUpgradeLevel("movement_speed_after_taking_damage") * 0.12f;
			if (_killSpeedBuffTimer > 0.0f) mult += GetUpgradeLevel("movement_speed_after_killing_an_enemy") * 0.12f;
			return BaseSpeed * mult;
		}
	}

	public float CurrentAoeAuraRadius
	{
		get
		{
			int level = GetUpgradeLevel("aoe_aura");
			if (level <= 0) return 0.0f;
			return 28.0f + (level - 1) * 4.0f; // slow range scaling
		}
	}

	public int CurrentAoeAuraDamage
	{
		get
		{
			int level = GetUpgradeLevel("aoe_aura");
			if (level <= 0) return 0;
			return 1 + (level - 1) / 3; // slow damage scaling
		}
	}

	public float CurrentAoeAuraTickInterval => 0.75f;

	public float CurrentGoldDropMultiplier => 1.0f + GetUpgradeLevel("midas_touch") * 0.15f;

	public int GetScaledMobGoldDrop(int baseGoldAmount)
	{
		if (baseGoldAmount <= 0) return 0;
		return Mathf.Max(baseGoldAmount, Mathf.RoundToInt(baseGoldAmount * CurrentGoldDropMultiplier));
	}

	public override void _Ready()
	{
		base.MaxHealth = BaseMaxHealth;
		base.AttackDamage = BaseAttackDamage;
		base.AttackRange = BaseAttackRange;
		base.AttackCooldown = BaseAttackCooldown;
		base.Speed = BaseSpeed;
		EquippedWeaponId = "base_sword";
		base._Ready();

		Health = CurrentMaxHealth;
		MaxHealth = CurrentMaxHealth;
		Speed = CurrentMoveSpeed;
		AttackCooldown = CurrentAttackCooldown;
		AttackDamage = CurrentAttackDamage;
		AttackRange = CurrentAttackRange;

		_sprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
		_healthBar = GetNodeOrNull<ProgressBar>("HealthBar");
		_pointLight = GetNodeOrNull<PointLight2D>("PointLight2D");
		_camera = GetNodeOrNull<Camera2D>("Camera2D");
		_sprite.SpriteFrames = BuildSpriteFrames();
		_sprite.AnimationFinished += OnAnimationFinished;
		_sprite.Play("idle_down");
		SyncEquippedFromId();
		SetupAudio();
		SetupAoeAuraVisual();
		UpdateAoeAuraVisual();
		ApplyHealthBarStyle();
		UpdateHealthBar();
		UpdatePlayerLight();
		CallDeferred(nameof(ApplyRandomSpawnIfNeeded));
	}

	private void ApplyRandomSpawnIfNeeded()
	{
		if (GlobalPosition.LengthSquared() > 1.0f) return;
		var map = GetTree().Root.FindChild("Map", true, false) as global::Map;
		if (map == null) return;
		GlobalPosition = map.GetRandomPlayerSpawnPosition();
	}

	public override void _Process(double delta)
	{
		UpdatePlayerLight();
		UpdateAoeAuraVisual();
	}

	private void UpdatePlayerLight()
	{
		if (_pointLight == null || !IsInstanceValid(_pointLight)) return;
		global::Map map = GetTree().Root.FindChild("Map", true, false) as global::Map;
		bool insideBuilding = map != null && map.IsWorldPositionInsideBuilding(GlobalPosition);
		_pointLight.RangeItemCullMask = 1;
		_pointLight.ShadowEnabled = insideBuilding;
		bool shouldEnable = insideBuilding;
		if (!insideBuilding)
		{
			DayNightCycle cycle = GetTree().GetFirstNodeInGroup("day_night_cycle") as DayNightCycle;
			shouldEnable = cycle != null && cycle.DaylightFactor < 0.45f;
		}
		_pointLight.Enabled = shouldEnable;
		if (!shouldEnable)
		{
			_pointLight.Energy = 0.0f; _pointLight.TextureScale = 1.0f; _pointLight.Color = Colors.White; return;
		}
		if (insideBuilding)
		{
			_pointLight.Energy = 0.95f; _pointLight.TextureScale = 1.15f; _pointLight.Color = new Color(1.0f, 0.98f, 0.92f, 1.0f);
		}
		else
		{
			_pointLight.Energy = 1.0f; _pointLight.TextureScale = 1.0f; _pointLight.Color = Colors.White;
		}
	}

	public void AddGold(int amount) => AddMoney(amount);

	public override bool AddItem(string itemId, int count = 1)
	{
		if (string.IsNullOrEmpty(itemId) || count <= 0) return false;
		if (itemId == "coin") { AddGold(count); return true; }
		return Inventory.AddItem(itemId, count);
	}

	public void ConvertInventoryCoinsToGold()
	{
		if (Inventory?.Slots == null) return;
		int total = 0;
		foreach (var s in Inventory.Slots)
		{
			if (s == null || s.IsEmpty || s.ItemId != "coin") continue;
			total += s.Count; s.ItemId = null; s.Count = 0;
		}
		if (total > 0) Gold += total;
	}

	public bool TrySpendCoins(int amount)
	{
		if (amount <= 0) return true;
		ConvertInventoryCoinsToGold();
		return TrySpendMoney(amount);
	}

	public bool TryPurchaseUpgrade(string upgradeId)
	{
		var def = UpgradeCatalog.Get(upgradeId);
		if (def == null) return false;
		int cost = def.GetCost(GetUpgradeLevel(upgradeId));
		if (!TrySpendCoins(cost)) return false;
		_upgradeLevels[upgradeId] = GetUpgradeLevel(upgradeId) + 1;
		ApplyDerivedStats(healOnMaxHealthIncrease: true);
		return true;
	}

	private void ApplyDerivedStats(bool healOnMaxHealthIncrease)
	{
		int oldMax = MaxHealth;
		MaxHealth = CurrentMaxHealth;
		Speed = CurrentMoveSpeed;
		AttackCooldown = CurrentAttackCooldown;
		AttackDamage = CurrentAttackDamage;
		AttackRange = CurrentAttackRange;
		if (healOnMaxHealthIncrease && MaxHealth > oldMax) Health += (MaxHealth - oldMax);
		Health = Mathf.Clamp(Health, 0, MaxHealth);
		UpdateHealthBar();
	}

	public void SyncEquippedFromId()
	{
		EquippedWeapon = EquippedWeaponId switch { "base_sword" => "sword", "fire_staff" => "fire_staff", _ => "none" };
	}

	public override void _PhysicsProcess(double delta)
	{
		float dt = (float)delta;
		if (IsDead) { Velocity = Vector2.Zero; MoveAndSlide(); return; }

		var inv = GetTree().GetFirstNodeInGroup("inventory_ui") as Control;
		var skills = GetTree().GetFirstNodeInGroup("skills_ui") as Control;
		var cheat = GetTree().GetFirstNodeInGroup("cheat_ui") as Control;
		var trader = GetTree().GetFirstNodeInGroup("trader_ui") as Control;
		if ((inv != null && inv.Visible) || (skills != null && skills.Visible) || (cheat != null && cheat.Visible) || (trader != null && trader.Visible))
		{
			Velocity = Vector2.Zero; _velocityCurrent = Vector2.Zero;
			if (_state != State.Attack) { _state = State.Idle; PlayAnimation($"idle_{_facing}"); }
			MoveAndSlide(); return;
		}

		ApplyDerivedStats(healOnMaxHealthIncrease: false);
		_attackTimer = Mathf.Max(0.0f, _attackTimer - dt);
		_damageRegenDelayTimer = Mathf.Max(0.0f, _damageRegenDelayTimer - dt);
		_damageSpeedBuffTimer = Mathf.Max(0.0f, _damageSpeedBuffTimer - dt);
		_killSpeedBuffTimer = Mathf.Max(0.0f, _killSpeedBuffTimer - dt);
		_aoeAuraTickTimer = Mathf.Max(0.0f, _aoeAuraTickTimer - dt);
		ApplyHealthRegen(dt);
		UpdateAoeAura(dt);

		SyncEquippedFromId();
		if (_state != State.Attack && EquippedWeapon != "none" && Input.IsActionPressed("attack") && _attackTimer <= 0.0f)
			StartAttack();

		Vector2 input = Input.GetVector("move_left", "move_right", "move_up", "move_down");
		if (input.LengthSquared() < 0.0001f) input = Input.GetVector("ui_left", "ui_right", "ui_up", "ui_down");
		if (input.Length() > 1.0f) input = input.Normalized();

		Vector2 desiredVelocity = input * CurrentMoveSpeed;
		_velocityCurrent = _velocityCurrent.MoveToward(desiredVelocity, CurrentAcceleration * dt);

		if (_state == State.Attack) { Velocity = _velocityCurrent; MoveAndSlide(); return; }

		if (input == Vector2.Zero) { _state = State.Idle; Velocity = _velocityCurrent; PlayAnimation($"idle_{_facing}"); }
		else { UpdateFacing(input); _state = State.Walk; Velocity = _velocityCurrent; PlayAnimation($"walk_{_facing}"); }
		MoveAndSlide();
	}

	public override void TakeDamage(int amount) => TakeDamage(amount, null);

	public void TakeDamage(int amount, Node2D attacker)
	{
		if (IsDead || amount <= 0) return;
		if (DebugInvulnerable) return;
		int reduced = Mathf.Max(1, Mathf.CeilToInt(amount * (1.0f - CurrentArmorReductionPerHit)));
		Health = Mathf.Max(0, Health - reduced);
		_damageRegenDelayTimer = CurrentRegenDelay;
		if (GetUpgradeLevel("movement_speed_after_taking_damage") > 0) _damageSpeedBuffTimer = Mathf.Max(_damageSpeedBuffTimer, 2.5f);
		PlayHurtSfx();
		ApplyThorns(attacker);
		UpdateHealthBar();
		if (Health == 0) Die();
	}

	public override int TakeDamageAmount(int amount, Node2D? source = null)
	{
		int before = Health;
		TakeDamage(amount, source);
		return Mathf.Max(0, before - Health);
	}

	private void ApplyThorns(Node2D attacker)
	{
		int thorns = CurrentThornsDamage;
		if (thorns <= 0 || attacker == null || !IsInstanceValid(attacker)) return;
		if (attacker.HasMethod("TakeDamage")) attacker.Call("TakeDamage", thorns);
	}

	private void ApplyHealthRegen(float delta)
	{
		if (IsDead || Health <= 0 || Health >= MaxHealth) { _regenAccumulator = 0.0f; return; }
		if (_damageRegenDelayTimer > 0.0f || CurrentRegenPerSecond <= 0.0f) return;
		_regenAccumulator += CurrentRegenPerSecond * delta;
		int heal = Mathf.FloorToInt(_regenAccumulator);
		if (heal <= 0) return;
		_regenAccumulator -= heal;
		Health = Mathf.Min(MaxHealth, Health + heal);
		UpdateHealthBar();
	}

	public void Die()
	{
		if (_state == State.Dead) return;
		_state = State.Dead; Health = 0; Velocity = Vector2.Zero; _velocityCurrent = Vector2.Zero;
		_sprite.FlipH = _facing == "side" && _faceLeft;
		PlayAnimation("death");
		UpdateHealthBar();
	}

	private void ShootFireball()
	{
		if (FireballScene == null) { GD.PushError("Fireball scene could not be loaded."); return; }
		var fireball = FireballScene.Instantiate<Fireball>();
		Vector2 direction = _facing switch { "up" => Vector2.Up, "down" => Vector2.Down, "side" => _faceLeft ? Vector2.Left : Vector2.Right, _ => Vector2.Down };
		GetTree().CurrentScene.AddChild(fireball);
		int damage = ComputeDamageForTarget(null, CurrentProjectileDamage);
		fireball.Init(direction, GlobalPosition + direction * 16.0f, CurrentProjectileSpeed, damage, CurrentKnockbackStrength, this as global::Player);
	}

	private void StartAttack()
	{
		_attackTimer = CurrentAttackCooldown;
		_sprite.FlipH = _facing == "side" && _faceLeft;
		if (EquippedWeapon == "fire_staff") { ShootFireball(); }
		else { _state = State.Attack; PlayAnimation($"attack_{_facing}"); PlaySlashSfx(); DamageEnemiesInRange(); }
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event is InputEventMouseButton mb && mb.Pressed)
		{
			if (_camera == null || !IsInstanceValid(_camera))
				_camera = GetNodeOrNull<Camera2D>("Camera2D");
			if (_camera == null) return;

			float step = 0.15f;
			Vector2 zoom = _camera.Zoom;
			if (mb.ButtonIndex == MouseButton.WheelUp)
				zoom += new Vector2(step, step);
			else if (mb.ButtonIndex == MouseButton.WheelDown)
				zoom -= new Vector2(step, step);
			else
				return;

			zoom.X = Mathf.Clamp(zoom.X, 0.75f, 4.0f);
			zoom.Y = Mathf.Clamp(zoom.Y, 0.75f, 4.0f);
			_camera.Zoom = zoom;
			GetViewport().SetInputAsHandled();
		}
	}

	private void SetupAudio()
	{
		_hurtSfx = GetNodeOrNull<AudioStreamPlayer>("HurtSfx") ?? new AudioStreamPlayer { Name = "HurtSfx" };
		if (_hurtSfx.GetParent() == null)
			AddChild(_hurtSfx);
		_hurtSfx.Stream = GD.Load<AudioStream>("res://assets/audio/sfx/forest_hurt.ogg");
		_hurtSfx.VolumeDb = -6.0f;

		_slashSfx = GetNodeOrNull<AudioStreamPlayer>("SlashSfx") ?? new AudioStreamPlayer { Name = "SlashSfx" };
		if (_slashSfx.GetParent() == null)
			AddChild(_slashSfx);
		_slashSfx.Stream = GD.Load<AudioStream>("res://assets/audio/sfx/forest_slash.ogg");
		_slashSfx.VolumeDb = -8.0f;
	}

	private void PlayHurtSfx()
	{
		if (_hurtSfx == null || _hurtSfx.Stream == null) return;
		if (_hurtSfx.Playing) return;
		_hurtSfx.Play();
	}

	private void PlaySlashSfx()
	{
		if (_slashSfx == null || _slashSfx.Stream == null) return;
		_slashSfx.Play();
	}

	private void SetupAoeAuraVisual()
	{
		if (_aoeAuraVisual != null && IsInstanceValid(_aoeAuraVisual)) return;
		_aoeAuraVisual = new Node2D { Name = "AoeAuraVisual", ZIndex = 5 };
		_aoeAuraLine = new Line2D
		{
			Name = "Circle",
			Width = 2.0f,
			DefaultColor = new Color(0.45f, 0.9f, 1.0f, 0.55f),
			Closed = true,
			Antialiased = true,
		};
		_aoeAuraVisual.AddChild(_aoeAuraLine);
		AddChild(_aoeAuraVisual);
		_aoeAuraVisual.Visible = false;
	}

	private void UpdateAoeAuraVisual()
	{
		if (_aoeAuraVisual == null || !IsInstanceValid(_aoeAuraVisual) || _aoeAuraLine == null) return;
		float radius = CurrentAoeAuraRadius;
		bool visible = radius > 0.0f && !IsDead;
		_aoeAuraVisual.Visible = visible;
		if (!visible) return;

		const int segments = 32;
		var pts = new Vector2[segments];
		for (int i = 0; i < segments; i++)
		{
			float t = (Mathf.Tau * i) / segments;
			pts[i] = new Vector2(Mathf.Cos(t), Mathf.Sin(t)) * radius;
		}
		_aoeAuraLine.Points = pts;
		float pulse = 0.45f + 0.1f * Mathf.Sin((float)Time.GetTicksMsec() / 180.0f);
		_aoeAuraLine.DefaultColor = new Color(0.45f, 0.95f, 1.0f, pulse);
	}

	private void UpdateAoeAura(float delta)
	{
		int damage = CurrentAoeAuraDamage;
		float radius = CurrentAoeAuraRadius;
		if (damage <= 0 || radius <= 0.0f || IsDead) return;
		if (_aoeAuraTickTimer > 0.0f) return;
		_aoeAuraTickTimer = CurrentAoeAuraTickInterval;

		ApplyAoeAuraDamageToGroup("enemies", radius, damage);
		// Knights are damageable by player attacks in this game, but aura should focus hostile targets.
	}

	private void ApplyAoeAuraDamageToGroup(string group, float radius, int damage)
	{
		float radiusSq = radius * radius;
		foreach (Node node in GetTree().GetNodesInGroup(group))
		{
			if (node is not Node2D target || !IsInstanceValid(target)) continue;
			if (GlobalPosition.DistanceSquaredTo(target.GlobalPosition) > radiusSq) continue;
			DealDamageToTarget(target, damage, applyKnockback: false);
		}
	}

	private void DealDamageToTarget(Node2D target, int baseDamage, bool applyKnockback)
	{
		if (target == null || !IsInstanceValid(target) || !target.HasMethod("TakeDamage")) return;
		int damage = ComputeDamageForTarget(target, baseDamage);
		if (target.HasMethod("TakeDamageAmount"))
			target.Call("TakeDamageAmount", damage, this);
		else
			target.Call("TakeDamage", damage);

		if (applyKnockback && CurrentKnockbackStrength > 0.0f && target.HasMethod("ApplyKnockback"))
		{
			Vector2 dir = target.GlobalPosition - GlobalPosition;
			if (dir.LengthSquared() < 0.001f) dir = _facing switch { "up" => Vector2.Up, "down" => Vector2.Down, "side" => _faceLeft ? Vector2.Left : Vector2.Right, _ => Vector2.Down };
			target.Call("ApplyKnockback", dir.Normalized(), CurrentKnockbackStrength);
		}
		ApplyOnHitEffects(target);
	}

	private void DamageEnemiesInRange() { DamageGroupInRange("enemies"); DamageGroupInRange("knights"); }

	public int ComputeDamageForTarget(Node2D target, int baseDamage)
	{
		_ = target;
		return Mathf.Max(1, baseDamage);
	}

	private void ApplyOnHitEffects(Node2D target)
	{
		if (target == null || !IsInstanceValid(target)) return;
		if (GetUpgradeLevel("movement_speed_after_killing_an_enemy") > 0)
		{
			Variant hv = target.Get("Health");
			if (hv.VariantType == Variant.Type.Int && hv.AsInt32() <= 0) _killSpeedBuffTimer = Mathf.Max(_killSpeedBuffTimer, 2.5f);
		}
		if (CurrentLifestealPerHit > 0.0f && Health < MaxHealth)
		{
			_lifestealAccumulator += CurrentLifestealPerHit;
			int heal = Mathf.FloorToInt(_lifestealAccumulator);
			if (heal > 0) { _lifestealAccumulator -= heal; Health = Mathf.Min(MaxHealth, Health + heal); UpdateHealthBar(); }
		}
	}

	private void DamageGroupInRange(string group)
	{
		foreach (Node node in GetTree().GetNodesInGroup(group))
		{
			if (node is not Node2D target || !IsInstanceValid(target)) continue;
			if (target.GlobalPosition.DistanceTo(GlobalPosition) > CurrentAttackRange || !target.HasMethod("TakeDamage")) continue;
			DealDamageToTarget(target, CurrentAttackDamage, applyKnockback: true);
		}
	}

	public void NotifyProjectileHit(Node2D target) => ApplyOnHitEffects(target);

	private void UpdateFacing(Vector2 input)
	{
		if (Mathf.Abs(input.X) >= Mathf.Abs(input.Y)) { _facing = "side"; _faceLeft = input.X < 0.0f; }
		else { _facing = input.Y < 0.0f ? "up" : "down"; _faceLeft = false; }
		_sprite.FlipH = _facing == "side" && _faceLeft;
	}

	private void PlayAnimation(string animation) { if (_sprite.Animation != animation) _sprite.Play(animation); }

	private void OnAnimationFinished()
	{
		if (_state == State.Dead)
		{
			EmitSignal(SignalName.Died);
			EmitSignal(SignalName.EntityDied);
			return;
		}
		if (_state == State.Attack) { _state = State.Idle; PlayAnimation($"idle_{_facing}"); }
	}

	private void ApplyHealthBarStyle()
	{
		if (_healthBar == null) return;
		var bg = new StyleBoxFlat { BgColor = new Color(0.08f, 0.05f, 0.05f, 0.95f) };
		bg.SetCornerRadiusAll(2);
		var fill = new StyleBoxFlat { BgColor = new Color(1.0f, 0.45f, 0.45f, 1.0f) };
		fill.SetCornerRadiusAll(2);
		_healthBar.AddThemeStyleboxOverride("background", bg);
		_healthBar.AddThemeStyleboxOverride("fill", fill);
		_healthBar.SelfModulate = new Color(1, 1, 1, 1);
	}

	private void UpdateHealthBar() { if (_healthBar == null) return; ApplyHealthBarStyleIfNeeded(); _healthBar.MaxValue = MaxHealth; _healthBar.Value = Health; }

	private bool _healthBarStyled;
	private void ApplyHealthBarStyleIfNeeded()
	{
		if (_healthBarStyled || _healthBar == null) return;
		ApplyHealthBarStyle();
		_healthBarStyled = true;
	}

	private static SpriteFrames BuildSpriteFrames()
	{
		Texture2D texture = GD.Load<Texture2D>(PlayerTexturePath);
		SpriteFrames frames = new(); frames.RemoveAnimation("default");
		AddAnimation(frames, texture, "idle_down", 0, 6, 6.0f, true);
		AddAnimation(frames, texture, "idle_side", 1, 6, 6.0f, true);
		AddAnimation(frames, texture, "idle_up", 2, 6, 6.0f, true);
		AddAnimation(frames, texture, "walk_down", 3, 6, 10.0f, true);
		AddAnimation(frames, texture, "walk_side", 4, 6, 10.0f, true);
		AddAnimation(frames, texture, "walk_up", 5, 6, 10.0f, true);
		AddAnimation(frames, texture, "attack_down", 6, 4, 12.0f, false);
		AddAnimation(frames, texture, "attack_side", 7, 4, 12.0f, false);
		AddAnimation(frames, texture, "attack_up", 8, 4, 12.0f, false);
		AddAnimation(frames, texture, "death", 9, 4, 8.0f, false);
		return frames;
	}

	private static void AddAnimation(SpriteFrames frames, Texture2D texture, string name, int row, int frameCount, float fps, bool loop)
	{
		frames.AddAnimation(name); frames.SetAnimationSpeed(name, fps); frames.SetAnimationLoop(name, loop);
		for (int frame = 0; frame < frameCount; frame++)
			frames.AddFrame(name, new AtlasTexture { Atlas = texture, Region = new Rect2(frame * FrameSize, row * FrameSize, FrameSize, FrameSize) });
	}

	public override Godot.Collections.Dictionary GetEntityState()
	{
		var state = base.GetEntityState();
		state["entity_kind"] = "player";
		state["is_player"] = true;
		state["gold"] = Gold;
		state["equipped_weapon"] = EquippedWeapon;
		state["upgrades"] = ExportUpgradeLevels();
		state["derived_stats"] = new Godot.Collections.Dictionary
		{
			["current_move_speed"] = CurrentMoveSpeed,
			["current_attack_damage"] = CurrentAttackDamage,
			["current_attack_range"] = CurrentAttackRange,
			["current_attack_cooldown"] = CurrentAttackCooldown,
			["current_projectile_speed"] = CurrentProjectileSpeed,
			["current_projectile_damage"] = CurrentProjectileDamage,
			["current_max_health"] = CurrentMaxHealth,
		};
		return state;
	}

	private Godot.Collections.Dictionary ExportUpgradeLevels()
	{
		var d = new Godot.Collections.Dictionary();
		foreach (var kv in _upgradeLevels) d[kv.Key] = kv.Value;
		return d;
	}
}
