#nullable enable
using Godot;
using GridQuest.Items;

namespace GridQuest.Entities.Base;

/// <summary>
/// Common base class for all living/interactive world actors in Grid Quest.
/// Consolidates shared RPG-like data and APIs: health, combat stats, weapon slot, inventory, and money.
/// </summary>
/// <remarks>
/// Intended hierarchy:
/// - <see cref="Entity"/>: player, enemies, NPC base
/// - <see cref="NpcEntity"/>: AI/LLM-facing non-playable entities (villagers, knights, future NPCs)
///
/// This class provides a stable, tool-friendly surface for reading/writing core state.
/// Derived classes implement movement, AI, animation, and game-specific behavior.
/// </remarks>
public abstract partial class Entity : CharacterBody2D
{
	/// <summary>Emitted when the entity is defeated/dies (health reaches zero).</summary>
	[Signal]
	public delegate void EntityDiedEventHandler();

	[ExportGroup("Stats")]
	[Export] public float Speed { get; set; } = 50.0f;
	[Export] public int MaxHealth { get; set; } = 3;
	[Export] public int AttackDamage { get; set; } = 1;
	[Export] public float AttackRange { get; set; } = 24.0f;
	[Export] public float AttackCooldown { get; set; } = 0.8f;

	[ExportGroup("Equipment & Inventory")]
	/// <summary>Weapon slot identifier (item id / logical weapon id) shared by all entities.</summary>
	[Export] public string EquippedWeaponId { get; set; } = string.Empty;
	/// <summary>Backpack/inventory container.</summary>
	public InventoryContainer Inventory { get; } = new InventoryContainer(16);
	/// <summary>Currency wallet (gold/coins/money).</summary>
	public int Money { get; set; }

	/// <summary>Current health points.</summary>
	public int Health { get; protected set; }
	/// <summary>True when health is depleted.</summary>
	public virtual bool IsDead => Health <= 0;

	public override void _Ready()
	{
		base._Ready();
		MotionMode = MotionModeEnum.Floating;
		InitializeEntityState();
	}

	/// <summary>Initializes shared runtime state. Derived classes should call base first/early when overriding.</summary>
	protected virtual void InitializeEntityState()
	{
		if (MaxHealth <= 0) MaxHealth = 1;
		if (Health <= 0) Health = MaxHealth;
		Health = Mathf.Clamp(Health, 0, MaxHealth);
	}

	/// <summary>
	/// Applies damage and returns the amount actually applied.
	/// Override to add armor, invulnerability, reactions, etc., while preserving death behavior.
	/// </summary>
	public virtual int TakeDamageAmount(int amount, Node2D? source = null)
	{
		if (IsDead || amount <= 0) return 0;
		int applied = Mathf.Min(Health, amount);
		Health = Mathf.Max(0, Health - applied);
		OnHealthChanged(applied, source);
		if (Health <= 0) OnDeath(source);
		return applied;
	}

	/// <summary>Compatibility damage entrypoint for existing systems using <c>TakeDamage(int)</c>.</summary>
	public virtual void TakeDamage(int amount) => TakeDamageAmount(amount, null);

	/// <summary>Heals the entity and returns amount healed.</summary>
	public virtual int Heal(int amount)
	{
		if (IsDead || amount <= 0) return 0;
		int before = Health;
		Health = Mathf.Clamp(Health + amount, 0, Mathf.Max(1, MaxHealth));
		int healed = Health - before;
		if (healed > 0) OnHealed(healed);
		return healed;
	}

	/// <summary>Hook invoked after health decreases.</summary>
	protected virtual void OnHealthChanged(int damageApplied, Node2D? source) { }

	/// <summary>Hook invoked after health increases.</summary>
	protected virtual void OnHealed(int amountHealed) { }

	/// <summary>Hook invoked when entity dies. Default emits <see cref="SignalName.EntityDied"/>.</summary>
	protected virtual void OnDeath(Node2D? source)
	{
		EmitSignal(SignalName.EntityDied);
	}

	/// <summary>Adds money/currency to the entity wallet.</summary>
	public virtual void AddMoney(int amount)
	{
		if (amount <= 0) return;
		Money += amount;
	}

	/// <summary>Attempts to spend money/currency from the entity wallet.</summary>
	public virtual bool TrySpendMoney(int amount)
	{
		if (amount <= 0) return true;
		if (Money < amount) return false;
		Money -= amount;
		return true;
	}

	/// <summary>
	/// Adds an item to inventory. "coin" is treated as money by default for gameplay compatibility.
	/// </summary>
	public virtual bool AddItem(string itemId, int count = 1)
	{
		if (string.IsNullOrWhiteSpace(itemId) || count <= 0) return false;
		if (itemId == "coin") { AddMoney(count); return true; }
		return Inventory.AddItem(itemId, count);
	}

	/// <summary>
	/// Returns a JSON-like state snapshot of shared entity data (identity, transform, stats, health, inventory, money, weapon).
	/// Derived classes should extend this dictionary with domain-specific fields.
	/// </summary>
	public virtual Godot.Collections.Dictionary GetEntityState()
	{
		return new Godot.Collections.Dictionary
		{
			["entity_type"] = GetType().Name,
			["name"] = Name.ToString(),
			["scene_path"] = SceneFilePath,
			["global_position"] = GlobalPosition,
			["health"] = Health,
			["max_health"] = MaxHealth,
			["is_dead"] = IsDead,
			["money"] = Money,
			["equipped_weapon_id"] = EquippedWeaponId ?? string.Empty,
			["inventory"] = ExportInventoryState(),
			["stats"] = new Godot.Collections.Dictionary
			{
				["speed"] = Speed,
				["attack_damage"] = AttackDamage,
				["attack_range"] = AttackRange,
				["attack_cooldown"] = AttackCooldown,
			}
		};
	}

	/// <summary>Exports inventory slots for state/tool APIs.</summary>
	protected virtual Godot.Collections.Array ExportInventoryState()
	{
		var slots = new Godot.Collections.Array();
		foreach (var slot in Inventory.Slots)
		{
			slots.Add(new Godot.Collections.Dictionary
			{
				["item_id"] = slot?.ItemId ?? string.Empty,
				["count"] = slot?.Count ?? 0,
				["is_empty"] = slot == null || slot.IsEmpty
			});
		}
		return slots;
	}

	/// <summary>Returns <see cref="GetEntityState"/> serialized as JSON.</summary>
	public virtual string GetEntityStateJson(bool indented = false)
	{
		return indented ? Json.Stringify(GetEntityState(), "\t") : Json.Stringify(GetEntityState());
	}
}
