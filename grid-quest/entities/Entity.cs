#nullable enable
using Godot;
using GridQuest.Entities.Base;
using GridQuest.GameGenerator.Llm;
using GridQuest.Items;
using GridQuest.PlayerUpgrades;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;

namespace GridQuest.Entities;

/// <summary>
/// Unified character entity. Can be controlled by a human (via PlayerController) or by the LLM.
/// </summary>
public partial class Entity : GridQuest.Entities.Base.Entity
{
	[Signal] public delegate void DiedEventHandler();

	[Export] public bool AiControlled { get; set; } = true;
	[Export(PropertyHint.MultilineText)] public string SystemPrompt { get; set; } = "You are an autonomous character in a small fantasy world. Explore, interact, and remember useful things based on what you observe.";
	[Export] public float DecisionIntervalSeconds { get; set; } = 12.0f;
	[Export] public float InteractionRange { get; set; } = 48.0f;

	private const int FrameSize = 32;
	private const string PlayerTexturePath = "res://assets/images/characters/cute_Player.png";
	private static readonly PackedScene FireballScene = GD.Load<PackedScene>("res://items/fireball.tscn");
	private static readonly PackedScene CrossbowBoltScene = GD.Load<PackedScene>("res://items/crossbow_bolt.tscn");

	private AnimatedSprite2D _sprite = null!;
	private ProgressBar _healthBar = null!;
	private PointLight2D? _pointLight;
	private AudioStreamPlayer _hurtSfx = null!;
	private AudioStreamPlayer _slashSfx = null!;
	private State _state = State.Idle;
	private string _facing = "down";
	private bool _faceLeft;
	private float _attackTimer;
	private Vector2 _velocityCurrent = Vector2.Zero;
	private Vector2 _controllerMoveInput = Vector2.Zero;

	private readonly Dictionary<string, int> _upgradeLevels = new();
	private float _damageRegenDelayTimer;
	private float _regenAccumulator;
	private float _damageSpeedBuffTimer;
	private float _killSpeedBuffTimer;
	private float _lifestealAccumulator;
	private float _aoeAuraTickTimer;
	private Node2D _aoeAuraVisual = null!;
	private Line2D _aoeAuraLine = null!;

	private bool _crossbowCharging;
	private float _crossbowChargeTime;
	private const float CrossbowMinChargeTime = 0.18f;
	private const float CrossbowFullChargeTime = 0.85f;
	private string _lastSpriteWeaponKey = "";

	private bool _thinking;
	private bool _decisionRequested = true;
	private float _decisionTimer;
	private float _minimumDecisionDelay;

	public bool DebugInvulnerable { get; set; }
	public bool AttackHeldByController { get; set; }

	private enum State { Idle, Walk, Attack, Dead }

	public int Gold { get => Money; set => Money = value; }
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

	private float WeaponDamageMultiplier => EquippedWeaponId switch
	{
		"scythe" => 1.9f,
		"crossbow" => 1.0f,
		"fire_staff" => 1.0f,
		"base_sword" => 1.0f,
		_ => 1.0f,
	};

	private float WeaponRangeBonus => EquippedWeaponId switch
	{
		"scythe" => 22.0f,
		_ => 0.0f,
	};

	private float WeaponAttackSpeedMultiplier => EquippedWeaponId switch
	{
		"scythe" => 1.2f,
		"crossbow" => 0.85f,
		"fire_staff" => 1.1f,
		"base_sword" => 1.0f,
		_ => 1.0f,
	};

	public int CurrentMaxHealth => BaseMaxHealth + GetUpgradeLevel("maximum_health");
	public int CurrentAttackDamage => Mathf.Max(1, Mathf.RoundToInt((BaseAttackDamage + GetUpgradeLevel("attack_damage")) * WeaponDamageMultiplier));
	public float CurrentAttackRange => BaseAttackRange + WeaponRangeBonus + GetUpgradeLevel("melee_range") * 4.0f;
	public float CurrentAttackCooldown => Mathf.Max(0.08f, (BaseAttackCooldown / (1.0f + GetUpgradeLevel("attack_speed") * 0.12f)) / Mathf.Max(0.1f, WeaponAttackSpeedMultiplier));
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
			if (_crossbowCharging) mult *= 0.55f;
			return BaseSpeed * mult;
		}
	}

	public float CurrentAoeAuraRadius
	{
		get
		{
			int level = GetUpgradeLevel("aoe_aura");
			return level <= 0 ? 0.0f : 28.0f + (level - 1) * 4.0f;
		}
	}

	public int CurrentAoeAuraDamage
	{
		get
		{
			int level = GetUpgradeLevel("aoe_aura");
			return level <= 0 ? 0 : 1 + (level - 1) / 3;
		}
	}

	public float CurrentAoeAuraTickInterval => 0.75f;
	public float CurrentGoldDropMultiplier => 1.0f + GetUpgradeLevel("midas_touch") * 0.15f;

	public int GetScaledEntityGoldDrop(int baseGoldAmount) =>
		baseGoldAmount <= 0 ? 0 : Mathf.Max(baseGoldAmount, Mathf.RoundToInt(baseGoldAmount * CurrentGoldDropMultiplier));

	public override void _Ready()
	{
		if (string.IsNullOrWhiteSpace(Name) || Name.ToString().StartsWith("Entity", StringComparison.OrdinalIgnoreCase) || Name == "Player")
			Name = EntityNameGenerator.GenerateUnique();
		SkinId = string.IsNullOrWhiteSpace(SkinId) ? "player" : SkinId;
		base.MaxHealth = BaseMaxHealth;
		base.AttackDamage = BaseAttackDamage;
		base.AttackRange = BaseAttackRange;
		base.AttackCooldown = BaseAttackCooldown;
		base.Speed = BaseSpeed;
		if (string.IsNullOrEmpty(EquippedWeaponId)) EquippedWeaponId = "base_sword";
		base._Ready();

		Health = CurrentMaxHealth;
		MaxHealth = CurrentMaxHealth;
		Speed = CurrentMoveSpeed;
		AttackCooldown = CurrentAttackCooldown;
		AttackDamage = CurrentAttackDamage;
		AttackRange = CurrentAttackRange;

		_sprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
		_healthBar = GetNodeOrNull<ProgressBar>("HealthBar") ?? new ProgressBar();
		_pointLight = GetNodeOrNull<PointLight2D>("PointLight2D");
		SyncEquippedFromId();
		EnsureWeaponSpriteFrames(force: true);
		_sprite.AnimationFinished += OnAnimationFinished;
		_sprite.Play(GetIdleAnimName());
		SetupAudio();
		SetupAoeAuraVisual();
		UpdateAoeAuraVisual();
		ApplyHealthBarStyle();
		UpdateHealthBar();
		UpdateEntityLight();
		EntityInteractionRequested += _ => RequestDecision(0.0f);
		CallDeferred(nameof(ApplyRandomSpawnIfNeeded));
	}

	private void ApplyRandomSpawnIfNeeded()
	{
		if (GlobalPosition.LengthSquared() > 1.0f) return;
		var map = GetTree().Root.FindChild("Map", true, false) as global::Map;
		if (map == null) return;
		GlobalPosition = map.GetRandomPlayerSpawnPosition();
	}

	public void SetControlled(bool controlled)
	{
		bool wasControlled = !AiControlled;
		AiControlled = !controlled;
		AttackHeldByController = false;
		_controllerMoveInput = Vector2.Zero;
		CancelCrossbowCharge(resetAnim: true);
		ClearIntent();
		_velocityCurrent = Vector2.Zero;
		Velocity = Vector2.Zero;
		if (controlled)
		{
			AddToGroup("controlled_entity");
			AddToGroup("player");
		}
		else
		{
			RemoveFromGroup("controlled_entity");
			RemoveFromGroup("player");
		}
		if (AiControlled)
		{
			AddToGroup("villagers");
			RequestDecision(0.0f);
		}
		else
		{
			RemoveFromGroup("villagers");
		}
		if (wasControlled != controlled)
			GD.Print($"Entity '{Name}' controlled={controlled}");
	}

	public bool IsControlled => !AiControlled;

	public override void _Process(double delta)
	{
		UpdateEntityLight();
		UpdateAoeAuraVisual();
		if (AiControlled && !IsDead)
		{
			_decisionTimer += (float)delta;
			_minimumDecisionDelay = Mathf.Max(0.0f, _minimumDecisionDelay - (float)delta);
			if (_decisionRequested && !_thinking && _minimumDecisionDelay <= 0.0f)
				ThinkAsync();
			else if (!_thinking && _decisionTimer >= Mathf.Max(1.0f, DecisionIntervalSeconds))
				ThinkAsync();
		}
	}

	private void UpdateEntityLight()
	{
		if (_pointLight == null || !IsInstanceValid(_pointLight)) return;
		global::Map map = GetTree().Root.FindChild("Map", true, false) as global::Map;
		bool insideBuilding = map != null && map.IsWorldPositionInsideBuilding(GlobalPosition);
		_pointLight.RangeItemCullMask = 1;
		_pointLight.ShadowEnabled = insideBuilding;
		bool shouldEnable = IsControlled;
		if (shouldEnable && !insideBuilding)
		{
			DayNightCycle cycle = GetTree().GetFirstNodeInGroup("day_night_cycle") as DayNightCycle;
			shouldEnable = cycle != null && cycle.DaylightFactor < 0.45f;
		}
		else if (shouldEnable && insideBuilding)
		{
			shouldEnable = true;
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

	public override bool TryPurchaseUpgrade(string upgradeId)
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
		EquippedWeapon = EquippedWeaponId switch
		{
			"base_sword" => "sword",
			"fire_staff" => "fire_staff",
			"scythe" => "scythe",
			"crossbow" => "crossbow",
			_ => "none"
		};
		EnsureWeaponSpriteFrames(force: false);
	}

	private string GetWeaponSpriteKey() => EquippedWeaponId switch { "scythe" => "scythe", "crossbow" => "crossbow", _ => "default" };

	private void EnsureWeaponSpriteFrames(bool force)
	{
		if (_sprite == null) return;
		string key = GetWeaponSpriteKey();
		if (!force && key == _lastSpriteWeaponKey && _sprite.SpriteFrames != null) return;
		_lastSpriteWeaponKey = key;
		_sprite.SpriteFrames = BuildSpriteFramesForWeapon(key);
		string anim = _crossbowCharging ? GetChargeAnimName() : (_state == State.Attack ? GetAttackAnimName() : (_state == State.Walk ? GetWalkAnimName() : GetIdleAnimName()));
		if (!_sprite.SpriteFrames.HasAnimation(anim))
			anim = _sprite.SpriteFrames.HasAnimation(GetIdleAnimName()) ? GetIdleAnimName() : "idle_down";
		_sprite.Play(anim);
	}

	public void SetControllerMoveInput(Vector2 input)
	{
		if (input.LengthSquared() < 0.0001f) _controllerMoveInput = Vector2.Zero;
		else _controllerMoveInput = input.Length() > 1.0f ? input.Normalized() : input;
	}

	public override void _PhysicsProcess(double delta)
	{
		base._PhysicsProcess(delta);
		float dt = (float)delta;
		if (IsDead) { Velocity = Vector2.Zero; MoveAndSlide(); return; }

		var inv = GetTree().GetFirstNodeInGroup("inventory_ui") as Control;
		var skills = GetTree().GetFirstNodeInGroup("skills_ui") as Control;
		var cheat = GetTree().GetFirstNodeInGroup("cheat_ui") as Control;
		bool uiBlocking = (inv != null && inv.Visible) || (skills != null && skills.Visible) || (cheat != null && cheat.Visible);
		if (uiBlocking)
		{
			CancelCrossbowCharge(resetAnim: true);
			Velocity = Vector2.Zero; _velocityCurrent = Vector2.Zero;
			if (_state != State.Attack) { _state = State.Idle; PlayAnimation(GetIdleAnimName()); }
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

		if (!AiControlled)
			HandleWeaponInput(dt);

		Vector2 input = AiControlled ? GetIntentMovementDirection() : _controllerMoveInput;
		Vector2 desiredVelocity = input * CurrentMoveSpeed;
		_velocityCurrent = _velocityCurrent.MoveToward(desiredVelocity, CurrentAcceleration * dt);

		if (_state == State.Attack)
		{
			Velocity = _velocityCurrent;
			MoveAndSlide();
			return;
		}

		if (_crossbowCharging)
		{
			if (input != Vector2.Zero) UpdateFacing(input);
			_sprite.FlipH = _facing == "side" && _faceLeft;
			_state = input == Vector2.Zero ? State.Idle : State.Walk;
			Velocity = _velocityCurrent;
			PlayAnimation(input == Vector2.Zero ? GetChargeAnimName() : GetWalkAnimName());
			MoveAndSlide();
			return;
		}

		if (input == Vector2.Zero) { _state = State.Idle; Velocity = _velocityCurrent; PlayAnimation(GetIdleAnimName()); }
		else { UpdateFacing(input); _state = State.Walk; Velocity = _velocityCurrent; PlayAnimation(GetWalkAnimName()); }
		MoveAndSlide();
	}

	private void HandleWeaponInput(float dt)
	{
		if (EquippedWeapon == "none") { CancelCrossbowCharge(resetAnim: true); return; }
		if (EquippedWeaponId == "crossbow") { HandleCrossbowInput(dt); return; }
		CancelCrossbowCharge(resetAnim: false);
		if (_state != State.Attack && AttackHeldByController && _attackTimer <= 0.0f)
			StartAttack();
	}

	private void HandleCrossbowInput(float dt)
	{
		if (_attackTimer > 0.0f) { CancelCrossbowCharge(resetAnim: false); return; }
		bool pressed = AttackHeldByController;
		if (_crossbowCharging)
		{
			_crossbowChargeTime += dt;
			if (!pressed) ReleaseCrossbowShot();
			return;
		}
		if (pressed && _state != State.Attack)
		{
			_crossbowCharging = true;
			_crossbowChargeTime = 0.0f;
			_sprite.FlipH = _facing == "side" && _faceLeft;
			_sprite.Frame = 0;
			PlayAnimation(GetChargeAnimName());
		}
	}

	private void ReleaseCrossbowShot()
	{
		float charge = _crossbowChargeTime;
		_crossbowCharging = false;
		_crossbowChargeTime = 0.0f;
		if (charge < CrossbowMinChargeTime)
		{
			_state = State.Idle;
			PlayAnimation(GetIdleAnimName());
			return;
		}
		ShootCrossbowBolt(charge);
		_attackTimer = CurrentAttackCooldown;
		_state = State.Attack;
		_sprite.FlipH = _facing == "side" && _faceLeft;
		_sprite.Frame = 0;
		PlayAnimation(GetAttackAnimName());
	}

	private void CancelCrossbowCharge(bool resetAnim)
	{
		if (!_crossbowCharging) return;
		_crossbowCharging = false;
		_crossbowChargeTime = 0.0f;
		if (resetAnim && _sprite != null) PlayAnimation(GetIdleAnimName());
	}

	public override void TakeDamage(int amount) => TakeDamage(amount, null);

	public void TakeDamage(int amount, Node2D? attacker)
	{
		if (IsDead || amount <= 0) return;
		if (DebugInvulnerable) return;
		CancelCrossbowCharge(resetAnim: false);
		int reduced = Mathf.Max(1, Mathf.CeilToInt(amount * (1.0f - CurrentArmorReductionPerHit)));
		Health = Mathf.Max(0, Health - reduced);
		_damageRegenDelayTimer = CurrentRegenDelay;
		if (GetUpgradeLevel("movement_speed_after_taking_damage") > 0) _damageSpeedBuffTimer = Mathf.Max(_damageSpeedBuffTimer, 2.5f);
		PlayHurtSfx();
		ApplyThorns(attacker);
		UpdateHealthBar();
		if (Health == 0) Die();
		else if (AiControlled) RequestDecision(0.25f);
	}

	public override int TakeDamageAmount(int amount, Node2D? source = null)
	{
		int before = Health;
		TakeDamage(amount, source);
		return Mathf.Max(0, before - Health);
	}

	private void ApplyThorns(Node2D? attacker)
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
		CancelCrossbowCharge(resetAnim: false);
		_state = State.Dead; Health = 0; Velocity = Vector2.Zero; _velocityCurrent = Vector2.Zero;
		_sprite.FlipH = _facing == "side" && _faceLeft;
		PlayAnimation("death");
		UpdateHealthBar();
	}

	private void ShootFireball()
	{
		if (FireballScene == null) { GD.PushError("Fireball scene could not be loaded."); return; }
		var fireball = FireballScene.Instantiate<Fireball>();
		Vector2 direction = GetFacingDirection();
		GetTree().CurrentScene.AddChild(fireball);
		int damage = ComputeDamageForTarget(null, CurrentProjectileDamage);
		fireball.Init(direction, GlobalPosition + direction * 16.0f, CurrentProjectileSpeed, damage, CurrentKnockbackStrength, this);
	}

	private void ShootCrossbowBolt(float chargeTime)
	{
		if (CrossbowBoltScene == null) { GD.PushError("Crossbow bolt scene could not be loaded."); return; }
		var bolt = CrossbowBoltScene.Instantiate<CrossbowBolt>();
		Vector2 direction = GetFacingDirection();
		GetTree().CurrentScene.AddChild(bolt);
		float t = Mathf.Clamp((chargeTime - CrossbowMinChargeTime) / Mathf.Max(0.01f, CrossbowFullChargeTime - CrossbowMinChargeTime), 0.0f, 1.0f);
		float speed = 440.0f + 180.0f * t + GetUpgradeLevel("projectile_speed") * 20.0f;
		int baseDamage = 5 + Mathf.RoundToInt(4 * t) + GetUpgradeLevel("projectile_damage");
		int damage = ComputeDamageForTarget(null, baseDamage);
		int pierce = 2 + Mathf.RoundToInt(3 * t);
		float knockback = CurrentKnockbackStrength + 20.0f + 30.0f * t;
		bolt.Init(direction, GlobalPosition + direction * 16.0f, speed, damage, knockback, this, pierce);
		PlaySlashSfx();
	}

	private Vector2 GetFacingDirection() => _facing switch { "up" => Vector2.Up, "down" => Vector2.Down, "side" => _faceLeft ? Vector2.Left : Vector2.Right, _ => Vector2.Down };

	public override bool TryAttackEntity(GridQuest.Entities.Base.Entity target)
	{
		if (target == null || !IsInstanceValid(target) || target == this || target.IsDead ||
			GlobalPosition.DistanceTo(target.GlobalPosition) > AttackRange || _attackTimer > 0.0f)
			return false;
		Vector2 direction = target.GlobalPosition - GlobalPosition;
		if (direction.LengthSquared() > 0.001f) UpdateFacing(direction.Normalized());
		if (EquippedWeaponId == "crossbow")
		{
			ShootCrossbowBolt(CrossbowFullChargeTime);
			_attackTimer = CurrentAttackCooldown;
			return true;
		}
		StartAttack();
		return true;
	}

	private void StartAttack()
	{
		_attackTimer = CurrentAttackCooldown;
		_sprite.FlipH = _facing == "side" && _faceLeft;
		if (EquippedWeaponId == "fire_staff") { ShootFireball(); return; }
		if (EquippedWeaponId == "crossbow") return;
		_state = State.Attack;
		PlayAnimation(GetAttackAnimName());
		PlaySlashSfx();
		if (EquippedWeaponId == "scythe") DamageEntitiesAroundPlayer360();
		else DamageEntitiesInRange();
	}

	private void SetupAudio()
	{
		_hurtSfx = GetNodeOrNull<AudioStreamPlayer>("HurtSfx") ?? new AudioStreamPlayer { Name = "HurtSfx" };
		if (_hurtSfx.GetParent() == null) AddChild(_hurtSfx);
		_hurtSfx.Stream = GD.Load<AudioStream>("res://assets/audio/sfx/forest_hurt.ogg");
		_hurtSfx.VolumeDb = -6.0f;
		_slashSfx = GetNodeOrNull<AudioStreamPlayer>("SlashSfx") ?? new AudioStreamPlayer { Name = "SlashSfx" };
		if (_slashSfx.GetParent() == null) AddChild(_slashSfx);
		_slashSfx.Stream = GD.Load<AudioStream>("res://assets/audio/sfx/forest_slash.ogg");
		_slashSfx.VolumeDb = -8.0f;
	}

	private void PlayHurtSfx() { if (_hurtSfx?.Stream != null && !_hurtSfx.Playing) _hurtSfx.Play(); }
	private void PlaySlashSfx() { if (_slashSfx?.Stream != null) _slashSfx.Play(); }

	private void SetupAoeAuraVisual()
	{
		if (_aoeAuraVisual != null && IsInstanceValid(_aoeAuraVisual)) return;
		_aoeAuraVisual = new Node2D { Name = "AoeAuraVisual", ZIndex = 5 };
		_aoeAuraLine = new Line2D { Name = "Circle", Width = 2.0f, DefaultColor = new Color(0.45f, 0.9f, 1.0f, 0.55f), Closed = true, Antialiased = true };
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
		for (int i = 0; i < segments; i++) { float t = (Mathf.Tau * i) / segments; pts[i] = new Vector2(Mathf.Cos(t), Mathf.Sin(t)) * radius; }
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
		ApplyAoeAuraDamageToGroup("entities", radius, damage);
	}

	private void ApplyAoeAuraDamageToGroup(string group, float radius, int damage)
	{
		float radiusSq = radius * radius;
		foreach (Node node in GetTree().GetNodesInGroup(group))
		{
			if (node is not Node2D target || !IsInstanceValid(target) || target == this) continue;
			if (GlobalPosition.DistanceSquaredTo(target.GlobalPosition) > radiusSq) continue;
			DealDamageToTarget(target, damage, applyKnockback: false);
		}
	}

	private void DealDamageToTarget(Node2D target, int baseDamage, bool applyKnockback)
	{
		if (target == null || !IsInstanceValid(target) || !target.HasMethod("TakeDamage")) return;
		int damage = ComputeDamageForTarget(target, baseDamage);
		if (target.HasMethod("TakeDamageAmount")) target.Call("TakeDamageAmount", damage, this);
		else target.Call("TakeDamage", damage);
		if (applyKnockback && CurrentKnockbackStrength > 0.0f && target.HasMethod("ApplyKnockback"))
		{
			Vector2 dir = target.GlobalPosition - GlobalPosition;
			if (dir.LengthSquared() < 0.001f) dir = GetFacingDirection();
			target.Call("ApplyKnockback", dir.Normalized(), CurrentKnockbackStrength);
		}
		ApplyOnHitEffects(target);
	}

	private void DamageEntitiesInRange() => DamageGroupInRange("entities");
	private void DamageEntitiesAroundPlayer360() => DamageGroupInRange360("entities");

	private void DamageGroupInRange360(string group)
	{
		float range = CurrentAttackRange;
		float rangeSq = range * range;
		foreach (Node node in GetTree().GetNodesInGroup(group))
		{
			if (node is not Node2D target || !IsInstanceValid(target) || target == this) continue;
			if (GlobalPosition.DistanceSquaredTo(target.GlobalPosition) > rangeSq || !target.HasMethod("TakeDamage")) continue;
			DealDamageToTarget(target, CurrentAttackDamage, applyKnockback: true);
		}
	}

	public int ComputeDamageForTarget(Node2D? target, int baseDamage) { _ = target; return Mathf.Max(1, baseDamage); }

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
			if (node is not Node2D target || !IsInstanceValid(target) || target == this) continue;
			if (GlobalPosition.DistanceTo(target.GlobalPosition) > CurrentAttackRange || !target.HasMethod("TakeDamage")) continue;
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

	private string GetIdleAnimName() => $"idle_{_facing}";
	private string GetWalkAnimName() => $"walk_{_facing}";
	private string GetAttackAnimName() => $"attack_{_facing}";
	private string GetChargeAnimName() => HasAnim($"charge_{_facing}") ? $"charge_{_facing}" : GetAttackAnimName();
	private bool HasAnim(string name) => _sprite?.SpriteFrames != null && _sprite.SpriteFrames.HasAnimation(name);
	private void PlayAnimation(string animation) { if (_sprite == null || !HasAnim(animation)) return; if (_sprite.Animation != animation) _sprite.Play(animation); }

	private void OnAnimationFinished()
	{
		if (_state == State.Dead)
		{
			EmitSignal(SignalName.Died);
			EmitSignal(SignalName.EntityDied);
			return;
		}
		if (_state == State.Attack) { _state = State.Idle; PlayAnimation(GetIdleAnimName()); }
	}

	private void ApplyHealthBarStyle()
	{
		if (_healthBar == null) return;
		var bg = new StyleBoxFlat { BgColor = new Color(0.08f, 0.05f, 0.05f, 0.95f) }; bg.SetCornerRadiusAll(2);
		var fill = new StyleBoxFlat { BgColor = new Color(1.0f, 0.45f, 0.45f, 1.0f) }; fill.SetCornerRadiusAll(2);
		_healthBar.AddThemeStyleboxOverride("background", bg);
		_healthBar.AddThemeStyleboxOverride("fill", fill);
	}
	private bool _healthBarStyled;
	private void ApplyHealthBarStyleIfNeeded() { if (_healthBarStyled || _healthBar == null) return; ApplyHealthBarStyle(); _healthBarStyled = true; }
	private void UpdateHealthBar() { if (_healthBar == null) return; ApplyHealthBarStyleIfNeeded(); _healthBar.MaxValue = MaxHealth; _healthBar.Value = Health; }

	private SpriteFrames BuildSpriteFramesForWeapon(string weaponKey) => weaponKey switch { "scythe" => BuildScytheSpriteFrames(), "crossbow" => BuildCrossbowSpriteFrames(), _ => BuildDefaultSpriteFrames() };

	private static SpriteFrames BuildDefaultSpriteFrames()
	{
		Texture2D texture = GD.Load<Texture2D>(PlayerTexturePath);
		SpriteFrames frames = new(); frames.RemoveAnimation("default");
		AddGridAnimation(frames, texture, "idle_down", 0, 6, 6.0f, true, FrameSize, FrameSize);
		AddGridAnimation(frames, texture, "idle_side", 1, 6, 6.0f, true, FrameSize, FrameSize);
		AddGridAnimation(frames, texture, "idle_up", 2, 6, 6.0f, true, FrameSize, FrameSize);
		AddGridAnimation(frames, texture, "walk_down", 3, 6, 10.0f, true, FrameSize, FrameSize);
		AddGridAnimation(frames, texture, "walk_side", 4, 6, 10.0f, true, FrameSize, FrameSize);
		AddGridAnimation(frames, texture, "walk_up", 5, 6, 10.0f, true, FrameSize, FrameSize);
		AddGridAnimation(frames, texture, "attack_down", 6, 4, 12.0f, false, FrameSize, FrameSize);
		AddGridAnimation(frames, texture, "attack_side", 7, 4, 12.0f, false, FrameSize, FrameSize);
		AddGridAnimation(frames, texture, "attack_up", 8, 4, 12.0f, false, FrameSize, FrameSize);
		AddGridAnimation(frames, texture, "death", 9, 4, 8.0f, false, FrameSize, FrameSize);
		return frames;
	}

	private static SpriteFrames BuildScytheSpriteFrames()
	{
		SpriteFrames frames = BuildDefaultSpriteFrames();
		Texture2D texture = GD.Load<Texture2D>("res://assets/images/characters/cute_Player_Scythe.png");
		Rect2[] down = { new(11, 6, 16, 32), new(36, 6, 35, 32), new(78, 6, 21, 32), new(110, 6, 13, 32) };
		Rect2[] side = { new(16, 43, 21, 25), new(39, 43, 30, 25), new(72, 43, 20, 25), new(111, 43, 13, 25) };
		Rect2[] up   = { new(14, 75, 18, 25), new(37, 75, 32, 25), new(71, 75, 20, 25), new(110, 75, 13, 25) };
		AddRectAnimation(frames, texture, "attack_down", down, 14.0f, false);
		AddRectAnimation(frames, texture, "attack_side", side, 14.0f, false);
		AddRectAnimation(frames, texture, "attack_up", up, 14.0f, false);
		return frames;
	}

	private static SpriteFrames BuildCrossbowSpriteFrames()
	{
		SpriteFrames frames = BuildDefaultSpriteFrames();
		Texture2D texture = GD.Load<Texture2D>("res://assets/images/characters/cute_Player_Crossbow.png");
		Rect2[] down = { new(14, 14, 13, 23), new(46, 14, 13, 23), new(77, 14, 13, 23), new(110, 14, 13, 23) };
		Rect2[] side = { new(9, 45, 20, 18), new(40, 45, 20, 18), new(72, 45, 20, 18), new(104, 45, 20, 18) };
		Rect2[] up   = { new(13, 73, 14, 22), new(45, 73, 14, 22), new(77, 73, 14, 22), new(110, 73, 14, 22) };
		AddRectAnimation(frames, texture, "charge_down", new[]{down[0],down[1],down[2]}, 10.0f, false);
		AddRectAnimation(frames, texture, "charge_side", new[]{side[0],side[1],side[2]}, 10.0f, false);
		AddRectAnimation(frames, texture, "charge_up", new[]{up[0],up[1],up[2]}, 10.0f, false);
		AddRectAnimation(frames, texture, "attack_down", new[]{down[3]}, 12.0f, false);
		AddRectAnimation(frames, texture, "attack_side", new[]{side[3]}, 12.0f, false);
		AddRectAnimation(frames, texture, "attack_up", new[]{up[3]}, 12.0f, false);
		return frames;
	}

	private static void AddRectAnimation(SpriteFrames frames, Texture2D texture, string name, Rect2[] rects, float fps, bool loop)
	{
		if (texture == null || rects == null || rects.Length == 0) return;
		if (frames.HasAnimation(name)) frames.RemoveAnimation(name);
		frames.AddAnimation(name);
		frames.SetAnimationSpeed(name, fps);
		frames.SetAnimationLoopMode(name, loop ? SpriteFrames.LoopMode.Linear : SpriteFrames.LoopMode.None);
		foreach (Rect2 rect in rects) frames.AddFrame(name, new AtlasTexture { Atlas = texture, Region = rect });
	}

	private static void AddGridAnimation(SpriteFrames frames, Texture2D texture, string name, int row, int frameCount, float fps, bool loop, int frameW, int frameH, int maxCols = int.MaxValue, int startFrame = 0)
	{
		if (texture == null) return;
		if (frames.HasAnimation(name)) frames.RemoveAnimation(name);
		frames.AddAnimation(name);
		frames.SetAnimationSpeed(name, fps);
		frames.SetAnimationLoopMode(name, loop ? SpriteFrames.LoopMode.Linear : SpriteFrames.LoopMode.None);
		int cols = Math.Min(frameCount, maxCols);
		for (int i = 0; i < cols; i++)
		{
			int col = startFrame + i;
			frames.AddFrame(name, new AtlasTexture { Atlas = texture, Region = new Rect2(col * frameW, row * frameH, frameW, frameH) });
		}
	}

	public override Godot.Collections.Dictionary GetEntityState()
	{
		var state = base.GetEntityState();
		state["entity_kind"] = "entity";
		state["ai_controlled"] = AiControlled;
		state["is_controlled"] = IsControlled;
		state["system_prompt"] = SystemPrompt;
		state["is_thinking"] = _thinking;
		state["gold"] = Gold;
		state["equipped_weapon"] = EquippedWeapon;
		state["upgrades"] = ExportUpgradeLevels();
		return state;
	}

	private Godot.Collections.Dictionary ExportUpgradeLevels()
	{
		var d = new Godot.Collections.Dictionary();
		foreach (var kv in _upgradeLevels) d[kv.Key] = kv.Value;
		return d;
	}

	public async Task<string> RespondToPlayerAsync(string message)
	{
		var api = LlmApi.Instance ?? GetNodeOrNull<LlmApi>("/root/LlmApi");
		if (api == null) throw new InvalidOperationException("LlmApi is unavailable.");
		AppendChatHistory("user", message);
		string prompt = $"A nearby character says: {message}\n\nRecent conversation:\n{Json.Stringify(GetChatHistoryState(12))}\n\nYour current observed state is:\n{Json.Stringify(GetWorldObservation(InteractionRange * 5.0f))}\n\nReply in character in 1-3 short sentences.";
		string reply = await api.ChatAsync(prompt, SystemPrompt);
		if (IsInsideTree()) { AppendChatHistory("assistant", reply); RequestDecision(0.0f); }
		return reply;
	}

	public void ReceiveMessage(string speaker, string message)
	{
		AppendChatHistory("user", $"{speaker}: {message}");
		RequestDecision(0.0f);
	}

	public void RequestDecision(float delay)
	{
		_decisionRequested = true;
		_minimumDecisionDelay = Mathf.Max(0.0f, delay);
	}

	private async void ThinkAsync()
	{
		if (_thinking || IsDead || !IsInsideTree() || !AiControlled) return;
		var api = LlmApi.Instance ?? GetNodeOrNull<LlmApi>("/root/LlmApi");
		if (api == null) { _decisionRequested = false; _decisionTimer = 0.0f; return; }
		_thinking = true;
		_decisionRequested = false;
		_decisionTimer = 0.0f;
		try
		{
			var observation = GetWorldObservation(InteractionRange * 5.0f);
			observation["recent_memory"] = Memory.Duplicate(true);
			observation["recent_chat"] = GetChatHistoryState(12);
			string prompt = "Observe the game state and choose exactly one available tool action. " +
				"Return only JSON in this shape: {\\\"tool\\\":\\\"tool_name\\\",\\\"arguments\\\":{...}}. " +
				"Do not invent tools or entity ids.\n\n" + Json.Stringify(observation);
			string response = await api.ChatAsync(prompt, SystemPrompt);
			if (!IsInsideTree()) return;
			ExecuteModelAction(response);
		}
		catch (Exception exception) { GD.PushWarning($"LLM decision failed for '{Name}': {exception.Message}"); }
		finally { _thinking = false; _decisionTimer = 0.0f; }
	}

	private void ExecuteModelAction(string response)
	{
		using JsonDocument document = JsonDocument.Parse(response);
		JsonElement root = document.RootElement;
		if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("tool", out JsonElement toolElement) ||
			toolElement.ValueKind != JsonValueKind.String || !root.TryGetProperty("arguments", out JsonElement argumentsElement) ||
			argumentsElement.ValueKind != JsonValueKind.Object)
			throw new FormatException("The model response must include string 'tool' and object 'arguments' fields.");
		var arguments = new Godot.Collections.Dictionary();
		foreach (JsonProperty property in argumentsElement.EnumerateObject()) arguments[property.Name] = ToVariant(property.Value);
		var result = ExecuteTool(toolElement.GetString()!, arguments);
		AppendChatHistory("tool", $"{toolElement.GetString()}: {result["result"]}");
	}

	private static Variant ToVariant(JsonElement element)
	{
		return element.ValueKind switch
		{
			JsonValueKind.String => Variant.From(element.GetString() ?? string.Empty),
			JsonValueKind.Number when element.TryGetInt64(out long integer) => Variant.From(integer),
			JsonValueKind.Number => Variant.From(element.GetDouble()),
			JsonValueKind.True => Variant.From(true),
			JsonValueKind.False => Variant.From(false),
			JsonValueKind.Null => default,
			_ => Variant.From(element.GetRawText())
		};
	}

	public override void OnInteractedBy(GridQuest.Entities.Base.Entity actor)
	{
		base.OnInteractedBy(actor);
		AppendChatHistory("user", $"{actor.Name} is nearby and wants to interact.");
		RequestDecision(0.0f);
	}

	public bool IsPlayerNearbyForChat(out Node2D? playerNode)
	{
		playerNode = GetTree().GetFirstNodeInGroup("controlled_entity") as Node2D ?? GetTree().GetFirstNodeInGroup("player") as Node2D;
		return playerNode != null && IsInstanceValid(playerNode) && playerNode.GlobalPosition.DistanceTo(GlobalPosition) <= InteractionRange;
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (!AiControlled || IsDead) return;
		bool interactPressed = @event.IsActionPressed("interact");
		if (!interactPressed && @event is InputEventKey key && key.Pressed && !key.Echo && key.Keycode == Key.F)
			interactPressed = true;
		if (!interactPressed) return;

		var chat = GetTree().GetFirstNodeInGroup("villager_chat_ui") as VillagerChatUi;
		if (chat == null || chat.Visible) return;
		if (!IsPlayerNearbyForChat(out var playerNode)) return;
		foreach (Node node in GetTree().GetNodesInGroup("entities"))
		{
			if (node is not Entity other || other == this || !IsInstanceValid(other) || !other.AiControlled) continue;
			float od = playerNode!.GlobalPosition.DistanceTo(other.GlobalPosition);
			if (od <= other.InteractionRange && od < playerNode.GlobalPosition.DistanceTo(GlobalPosition))
				return;
		}
		chat.Open(this);
		GetViewport().SetInputAsHandled();
	}
}
