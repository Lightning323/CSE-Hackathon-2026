
using Godot;
using GridQuest.Items;
using GridQuest.PlayerUpgrades;
using System;
using System.Collections.Generic;

public partial class Player : CharacterBody2D
{
	[Signal]
	public delegate void DiedEventHandler();

	[Export] public float Speed { get; set; } = 90.0f;
	[Export] public float AttackCooldown { get; set; } = 0.45f;
	[Export] public int MaxHealth { get; set; } = 5;
	[Export] public int AttackDamage { get; set; } = 1;
	[Export] public float AttackRange { get; set; } = 32.0f;

	private const int FrameSize = 32;
	private const string PlayerTexturePath = "res://assets/images/characters/cute_Player.png";
	private static readonly PackedScene FireballScene = GD.Load<PackedScene>("res://items/fireball.tscn");

	private AnimatedSprite2D _sprite;
	private ProgressBar _healthBar;
	private PointLight2D _pointLight;
	private State _state = State.Idle;
	private string _facing = "down";
	private bool _faceLeft;
	private float _attackTimer;
	private Vector2 _velocityCurrent = Vector2.Zero;

	// Upgrade-driven runtime state
	private readonly Dictionary<string, int> _upgradeLevels = new();
	private float _damageRegenDelayTimer;
	private float _regenAccumulator;
	private float _damageSpeedBuffTimer;
	private float _killSpeedBuffTimer;
	private float _lifestealAccumulator;
	private float _thornsAccumulator;
	private AudioStreamPlayer _hurtSfx;
	private AudioStreamPlayer _slashSfx;

	public bool DebugInvulnerable { get; set; }

	private enum State
	{
		Idle,
		Walk,
		Attack,
		Dead
	}

	public int Health { get; private set; }
	public int Gold { get; private set; }
	public string EquippedWeapon { get; set; } = "sword";
	public InventoryContainer Inventory { get; } = new InventoryContainer(16);
	public string EquippedWeaponId { get; set; } = "base_sword";
	public bool IsDead => _state == State.Dead;

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
	public float CurrentBossBonusMultiplier => 1.0f + GetUpgradeLevel("bonus_damage_to_bosses") * 0.2f;
	public float CurrentArmorReductionPerHit => Mathf.Min(0.75f, GetUpgradeLevel("damage_reduction_armor") * 0.08f);
	public float CurrentRegenPerSecond => GetUpgradeLevel("slow_health_regeneration") * 0.35f;
	public float CurrentRegenDelay => Mathf.Max(0.25f, 3.0f - GetUpgradeLevel("health_regeneration_delay") * 0.25f);
	public float CurrentAcceleration
	{
		get
		{
			int lvl = GetUpgradeLevel("player_acceleration");
			return 900.0f + lvl * 220.0f;
		}
	}

	public float CurrentMoveSpeed
	{
		get
		{
			float mult = 1.0f
				+ GetUpgradeLevel("player_movement_speed") * 0.08f;
			if (_damageSpeedBuffTimer > 0.0f)
				mult += GetUpgradeLevel("movement_speed_after_taking_damage") * 0.12f;
			if (_killSpeedBuffTimer > 0.0f)
				mult += GetUpgradeLevel("movement_speed_after_killing_an_enemy") * 0.12f;
			return BaseSpeed * mult;
		}
	}

	public override void _Ready()
	{
		MotionMode = MotionModeEnum.Floating;
		Health = CurrentMaxHealth;
		MaxHealth = CurrentMaxHealth;
		Speed = CurrentMoveSpeed;
		AttackCooldown = CurrentAttackCooldown;
		AttackDamage = CurrentAttackDamage;
		AttackRange = CurrentAttackRange;
		_sprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
		_healthBar = GetNodeOrNull<ProgressBar>("HealthBar");
		_pointLight = GetNodeOrNull<PointLight2D>("PointLight2D");
		_sprite.SpriteFrames = BuildSpriteFrames();
		_sprite.AnimationFinished += OnAnimationFinished;
		_sprite.Play("idle_down");
		SyncEquippedFromId();
		SetupAudio();
		UpdateHealthBar();
		UpdatePlayerLight();
		CallDeferred(nameof(ApplyRandomSpawnIfNeeded));
	}

	private void ApplyRandomSpawnIfNeeded()
	{
		// Place the player on a random grass tile when spawned at the default/placeholder position.
		if (GlobalPosition.LengthSquared() > 1.0f)
			return;

		var map = GetTree().Root.FindChild("Map", true, false) as Map;
		if (map == null)
			return;

		GlobalPosition = map.GetRandomPlayerSpawnPosition();
	}

	public override void _Process(double delta)
	{
		UpdatePlayerLight();
	}

	private void UpdatePlayerLight()
	{
		if (_pointLight == null || !IsInstanceValid(_pointLight))
			return;

		Map map = GetTree().Root.FindChild("Map", true, false) as Map;
		bool insideBuilding = map != null && map.IsWorldPositionInsideBuilding(GlobalPosition);

		// Keep light contained by wall occluders when indoors.
		_pointLight.RangeItemCullMask = 1;
		_pointLight.ShadowEnabled = insideBuilding;

		bool shouldEnable;
		if (insideBuilding)
		{
			// Turn on inside buildings to explore dark interiors.
			shouldEnable = true;
		}
		else
		{
			DayNightCycle cycle = GetTree().GetFirstNodeInGroup("day_night_cycle") as DayNightCycle;
			if (cycle == null)
			{
				shouldEnable = false;
			}
			else
			{
				// Off during bright day; on around sunset/night and off again around sunrise.
				shouldEnable = cycle.DaylightFactor < 0.45f;
			}
		}

		_pointLight.Enabled = shouldEnable;
		if (!shouldEnable)
		{
			_pointLight.Energy = 0.0f;
			_pointLight.TextureScale = 1.0f;
			_pointLight.Color = Colors.White;
			return;
		}

		if (insideBuilding)
		{
			// Dim indoor light and rely on wall occluders to keep it from leaking outside.
			_pointLight.Energy = 0.95f;
			_pointLight.TextureScale = 1.15f;
			_pointLight.Color = new Color(1.0f, 0.98f, 0.92f, 1.0f);
		}
		else
		{
			_pointLight.Energy = 1.0f;
			_pointLight.TextureScale = 1.0f;
			_pointLight.Color = Colors.White;
		}
	}

	public void AddGold(int amount)
	{
		if (amount <= 0) return;
		Gold += amount;
	}
 
	public bool AddItem(string itemId, int count = 1)
	{
		if (string.IsNullOrEmpty(itemId) || count <= 0) return false;
		if (itemId == "coin")
		{
			AddGold(count);
			return true;
		}
		return Inventory.AddItem(itemId, count);
	}
 
	public void ConvertInventoryCoinsToGold()
	{
		if (Inventory?.Slots == null) return;
		int total = 0;
		foreach (var s in Inventory.Slots)
		{
			if (s == null || s.IsEmpty || s.ItemId != "coin") continue;
			total += s.Count;
			s.ItemId = null;
			s.Count = 0;
		}
		if (total > 0) Gold += total;
	}
 
	public bool TrySpendCoins(int amount)
	{
		if (amount <= 0) return true;
		ConvertInventoryCoinsToGold();
		if (Gold < amount) return false;
		Gold -= amount;
		if (Gold < 0) Gold = 0;
		return true;
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
		if (healOnMaxHealthIncrease && MaxHealth > oldMax)
		{
			Health += (MaxHealth - oldMax);
		}
		Health = Mathf.Clamp(Health, 0, MaxHealth);
		UpdateHealthBar();
	}

	public void SyncEquippedFromId()
	{
		EquippedWeapon = EquippedWeaponId switch
		{
			"base_sword" => "sword",
			"fire_staff" => "fire_staff",
			_ => "none",
		};
	}

	public override void _UnhandledInput(InputEvent @event)
	{
	}

	public override void _PhysicsProcess(double delta)
	{
		float dt = (float)delta;
		if (_state == State.Dead)
		{
			Velocity = Vector2.Zero;
			MoveAndSlide();
			return;
		}

		// freeze if modal UI open
		var inv = GetTree().GetFirstNodeInGroup("inventory_ui") as Control;
		var skills = GetTree().GetFirstNodeInGroup("skills_ui") as Control;
		var cheat = GetTree().GetFirstNodeInGroup("cheat_ui") as Control;
		if ((inv != null && inv.Visible) || (skills != null && skills.Visible) || (cheat != null && cheat.Visible))
		{
			Velocity = Vector2.Zero;
			_velocityCurrent = Vector2.Zero;
			if (_state != State.Attack)
			{
				_state = State.Idle;
				PlayAnimation($"idle_{_facing}");
			}
			MoveAndSlide();
			return;
		}

		ApplyDerivedStats(healOnMaxHealthIncrease: false);
		_attackTimer = Mathf.Max(0.0f, _attackTimer - dt);
		_damageRegenDelayTimer = Mathf.Max(0.0f, _damageRegenDelayTimer - dt);
		_damageSpeedBuffTimer = Mathf.Max(0.0f, _damageSpeedBuffTimer - dt);
		_killSpeedBuffTimer = Mathf.Max(0.0f, _killSpeedBuffTimer - dt);
		ApplyHealthRegen(dt);

		if (_state != State.Attack && EquippedWeapon != "none" && Input.IsActionPressed("attack") && _attackTimer <= 0.0f)
		{
			StartAttack();
		}

		Vector2 input = Input.GetVector("move_left", "move_right", "move_up", "move_down");
		if (input.LengthSquared() < 0.0001f)
			input = Input.GetVector("ui_left", "ui_right", "ui_up", "ui_down");
		if (input.Length() > 1.0f) input = input.Normalized();

		Vector2 desiredVelocity = input * CurrentMoveSpeed;
		float accel = CurrentAcceleration;
		_velocityCurrent = _velocityCurrent.MoveToward(desiredVelocity, accel * dt);

		if (_state == State.Attack)
		{
			Velocity = _velocityCurrent;
			MoveAndSlide();
			return;
		}

		if (input == Vector2.Zero)
		{
			_state = State.Idle;
			Velocity = _velocityCurrent;
			PlayAnimation($"idle_{_facing}");
		}
		else
		{
			UpdateFacing(input);
			_state = State.Walk;
			Velocity = _velocityCurrent;
			PlayAnimation($"walk_{_facing}");
		}

		MoveAndSlide();
	}

	public void TakeDamage(int amount)
	{
		TakeDamage(amount, null);
	}

	public void TakeDamage(int amount, Node2D attacker)
	{
		if (_state == State.Dead || amount <= 0)
		{
			return;
		}

		if (DebugInvulnerable)
		{
			return;
		}

		int reduced = Mathf.Max(1, Mathf.CeilToInt(amount * (1.0f - CurrentArmorReductionPerHit)));
		Health = Mathf.Max(0, Health - reduced);
		_damageRegenDelayTimer = CurrentRegenDelay;
		if (GetUpgradeLevel("movement_speed_after_taking_damage") > 0)
		{
			_damageSpeedBuffTimer = Mathf.Max(_damageSpeedBuffTimer, 2.5f);
		}

		PlayHurtSfx();
		ApplyThorns(attacker);
		UpdateHealthBar();
		if (Health == 0)
		{
			Die();
		}
	}

	private void ApplyThorns(Node2D attacker)
	{
		int thorns = CurrentThornsDamage;
		if (thorns <= 0 || attacker == null || !IsInstanceValid(attacker))
			return;
		if (attacker.HasMethod("TakeDamage"))
		{
			attacker.Call("TakeDamage", thorns);
		}
	}

	private void ApplyHealthRegen(float delta)
	{
		if (_state == State.Dead || Health <= 0 || Health >= MaxHealth) { _regenAccumulator = 0.0f; return; }
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
		if (_state == State.Dead)
		{
			return;
		}

		_state = State.Dead;
		Velocity = Vector2.Zero;
		_velocityCurrent = Vector2.Zero;
		_sprite.FlipH = _facing == "side" && _faceLeft;
		PlayAnimation("death");
		EmitSignal(SignalName.Died);
	}

	private void ShootFireball()
	{
		if (FireballScene == null)
		{
			GD.PushError("Fireball scene could not be loaded.");
			return;
		}

		var fireball = FireballScene.Instantiate<Fireball>();

		Vector2 direction = _facing switch
		{
			"up" => Vector2.Up,
			"down" => Vector2.Down,
			"side" => _faceLeft ? Vector2.Left : Vector2.Right,
			_ => Vector2.Down
		};

		GetTree().CurrentScene.AddChild(fireball);
		int damage = ComputeDamageForTarget(null, CurrentProjectileDamage);
		fireball.Init(direction, GlobalPosition + direction * 16.0f, CurrentProjectileSpeed, damage, CurrentKnockbackStrength, this);
	}

	private void StartAttack()
	{
		_attackTimer = CurrentAttackCooldown;
		_sprite.FlipH = _facing == "side" && _faceLeft;
		if (EquippedWeapon == "fire_staff")
		{
			ShootFireball();
			// keep current walk/idle anim, don't play attack anim
		}
		else
		{
			_state = State.Attack;
			PlayAnimation($"attack_{_facing}");
			PlaySlashSfx();
			DamageEnemiesInRange();
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
		// Do not overlap hurt sound if it's already playing.
		if (_hurtSfx.Playing) return;
		_hurtSfx.Play();
	}

	private void PlaySlashSfx()
	{
		if (_slashSfx == null || _slashSfx.Stream == null) return;
		_slashSfx.Play();
	}

	private void DamageEnemiesInRange()
	{
		DamageGroupInRange("enemies");
		DamageGroupInRange("knights");
	}

	private bool IsBossTarget(Node2D target)
	{
		if (target == null) return false;
		if (target.IsInGroup("boss") || target.IsInGroup("bosses")) return true;
		Variant v = target.Get("IsBoss");
		return v.VariantType == Variant.Type.Bool && v.AsBool();
	}

	public int ComputeDamageForTarget(Node2D target, int baseDamage)
	{
		float dmg = baseDamage;
		if (IsBossTarget(target))
			dmg *= CurrentBossBonusMultiplier;
		return Mathf.Max(1, Mathf.RoundToInt(dmg));
	}

	private void ApplyOnHitEffects(Node2D target)
	{
		if (target == null || !IsInstanceValid(target)) return;
		if (GetUpgradeLevel("movement_speed_after_killing_an_enemy") > 0)
		{
			// if this hit killed, the enemy will be freed shortly; check health-like property best-effort
			Variant hv = target.Get("Health");
			if (hv.VariantType == Variant.Type.Int && hv.AsInt32() <= 0)
			{
				_killSpeedBuffTimer = Mathf.Max(_killSpeedBuffTimer, 2.5f);
			}
		}

		if (CurrentLifestealPerHit > 0.0f && Health < MaxHealth)
		{
			_lifestealAccumulator += CurrentLifestealPerHit;
			int heal = Mathf.FloorToInt(_lifestealAccumulator);
			if (heal > 0)
			{
				_lifestealAccumulator -= heal;
				Health = Mathf.Min(MaxHealth, Health + heal);
				UpdateHealthBar();
			}
		}
	}

	private void DamageGroupInRange(string group)
	{
		foreach (Node node in GetTree().GetNodesInGroup(group))
		{
			if (node is not Node2D target || !IsInstanceValid(target))
				continue;
			if (target.GlobalPosition.DistanceTo(GlobalPosition) > CurrentAttackRange || !target.HasMethod("TakeDamage"))
				continue;

			int damage = ComputeDamageForTarget(target, CurrentAttackDamage);
			target.Call("TakeDamage", damage);
			if (CurrentKnockbackStrength > 0.0f && target.HasMethod("ApplyKnockback"))
			{
				Vector2 dir = (target.GlobalPosition - GlobalPosition);
				if (dir.LengthSquared() < 0.001f) dir = _facing switch { "up" => Vector2.Up, "down" => Vector2.Down, "side" => _faceLeft ? Vector2.Left : Vector2.Right, _ => Vector2.Down };
				target.Call("ApplyKnockback", dir.Normalized(), CurrentKnockbackStrength);
			}
			ApplyOnHitEffects(target);
		}
	}

	public void NotifyProjectileHit(Node2D target)
	{
		ApplyOnHitEffects(target);
	}

	private void UpdateFacing(Vector2 input)
	{
		if (Mathf.Abs(input.X) >= Mathf.Abs(input.Y))
		{
			_facing = "side";
			_faceLeft = input.X < 0.0f;
		}
		else
		{
			_facing = input.Y < 0.0f ? "up" : "down";
			_faceLeft = false;
		}

		_sprite.FlipH = _facing == "side" && _faceLeft;
	}

	private void PlayAnimation(string animation)
	{
		if (_sprite.Animation != animation)
		{
			_sprite.Play(animation);
		}
	}

	private void OnAnimationFinished()
	{
		if (_state == State.Dead)
		{
			return;
		}

		if (_state == State.Attack)
		{
			_state = State.Idle;
			PlayAnimation($"idle_{_facing}");
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

	private static SpriteFrames BuildSpriteFrames()
	{
		Texture2D texture = GD.Load<Texture2D>(PlayerTexturePath);
		SpriteFrames frames = new();
		frames.RemoveAnimation("default");

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

	private static void AddAnimation(
		SpriteFrames frames,
		Texture2D texture,
		string name,
		int row,
		int frameCount,
		float fps,
		bool loop)
	{
		frames.AddAnimation(name);
		frames.SetAnimationSpeed(name, fps);
		frames.SetAnimationLoopMode(name, loop ? SpriteFrames.LoopMode.Linear : SpriteFrames.LoopMode.None);

		for (int frame = 0; frame < frameCount; frame++)
		{
			AtlasTexture atlasTexture = new()
			{
				Atlas = texture,
				Region = new Rect2(frame * FrameSize, row * FrameSize, FrameSize, FrameSize)
			};
			frames.AddFrame(name, atlasTexture);
		}
	}
}
