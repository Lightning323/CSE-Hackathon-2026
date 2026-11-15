#nullable enable
using Godot;
using GridQuest.Items;
using System;
using System.Collections.Generic;

namespace GridQuest.Entities.Base;

public abstract partial class Entity : CharacterBody2D
{
	[Signal] public delegate void EntityDiedEventHandler();
	[Signal] public delegate void EntityInteractionRequestedEventHandler(string actorName);
	[Signal] public delegate void EntitySpeechEventHandler(string message);

	public enum IntentMode { Idle, MoveToPoint, MoveToEntity }

	[ExportGroup("Identity")]
	[Export] public string SkinId { get; set; } = "player";
	[Export] public string Faction { get; set; } = "neutral";

	[ExportGroup("Stats")]
	[Export] public float Speed { get; set; } = 50.0f;
	[Export] public int MaxHealth { get; set; } = 3;
	[Export] public int AttackDamage { get; set; } = 1;
	[Export] public float AttackRange { get; set; } = 24.0f;
	[Export] public float AttackCooldown { get; set; } = 0.8f;

	[ExportGroup("Equipment & Inventory")]
	[Export] public string EquippedWeaponId { get; set; } = string.Empty;
	public InventoryContainer Inventory { get; } = new InventoryContainer(16);
	public int Money { get; set; }
	public int Health { get; protected set; }
	public virtual bool IsDead => Health <= 0;
	public Godot.Collections.Dictionary Memory { get; } = new();
	public IntentMode CurrentIntent { get; protected set; } = IntentMode.Idle;
	public Vector2? IntentTargetPosition { get; protected set; }
	public string? IntentTargetEntityId { get; protected set; }
	public string LastActionResult { get; protected set; } = string.Empty;

	private readonly List<Dictionary<string, string>> _chatHistory = new();
	private float _toolAttackCooldownRemaining;

	public override void _Ready()
	{
		base._Ready();
		MotionMode = MotionModeEnum.Floating;
		AddToGroup("entities");
		if (MaxHealth <= 0) MaxHealth = 1;
		if (Health <= 0) Health = MaxHealth;
		Health = Mathf.Clamp(Health, 0, MaxHealth);
	}

	public override void _PhysicsProcess(double delta)
	{
		_toolAttackCooldownRemaining = Mathf.Max(0.0f, _toolAttackCooldownRemaining - (float)delta);
	}

	public virtual int TakeDamageAmount(int amount, Node2D? source = null)
	{
		if (IsDead || amount <= 0) return 0;
		int applied = Mathf.Min(Health, amount);
		Health = Mathf.Max(0, Health - applied);
		OnHealthChanged(applied, source);
		if (Health == 0) OnDeath(source);
		return applied;
	}

	public virtual void TakeDamage(int amount) => TakeDamageAmount(amount);

	public virtual int Heal(int amount)
	{
		if (IsDead || amount <= 0) return 0;
		int before = Health;
		Health = Mathf.Clamp(Health + amount, 0, Mathf.Max(1, MaxHealth));
		int healed = Health - before;
		if (healed > 0) OnHealed(healed);
		return healed;
	}

	protected virtual void OnHealthChanged(int damageApplied, Node2D? source) { }
	protected virtual void OnHealed(int amountHealed) { }

	protected virtual void OnDeath(Node2D? source)
	{
		EmitSignal(SignalName.EntityDied);
	}

	public virtual void AddMoney(int amount)
	{
		if (amount > 0) Money += amount;
	}

	public virtual bool TrySpendMoney(int amount)
	{
		if (amount <= 0) return true;
		if (Money < amount) return false;
		Money -= amount;
		return true;
	}

	public virtual bool AddItem(string itemId, int count = 1)
	{
		if (string.IsNullOrWhiteSpace(itemId) || count <= 0) return false;
		if (itemId == "coin") { AddMoney(count); return true; }
		return Inventory.AddItem(itemId, count);
	}

	public virtual bool GoToPosition(Vector2 position, string reason = "")
	{
		CurrentIntent = IntentMode.MoveToPoint;
		IntentTargetPosition = position;
		IntentTargetEntityId = null;
		RememberState("last_move_reason", reason);
		return true;
	}

	public virtual bool GoToEntity(string entityId, string reason = "")
	{
		if (string.IsNullOrWhiteSpace(entityId)) return false;
		CurrentIntent = IntentMode.MoveToEntity;
		IntentTargetEntityId = entityId;
		IntentTargetPosition = null;
		RememberState("last_move_reason", reason);
		return true;
	}

	public virtual void ClearIntent()
	{
		CurrentIntent = IntentMode.Idle;
		IntentTargetEntityId = null;
		IntentTargetPosition = null;
		Velocity = Vector2.Zero;
	}

	public Vector2 GetIntentMovementDirection()
	{
		Vector2? destination = IntentTargetPosition;
		if (!string.IsNullOrWhiteSpace(IntentTargetEntityId))
		{
			var target = ResolveEntityTarget(IntentTargetEntityId);
			if (target != null && IsInstanceValid(target))
				destination = target.GlobalPosition;
			else
			{
				ClearIntent();
				return Vector2.Zero;
			}
		}

		if (!destination.HasValue) return Vector2.Zero;
		Vector2 offset = destination.Value - GlobalPosition;
		if (offset.Length() <= 6.0f)
		{
			ClearIntent();
			return Vector2.Zero;
		}
		return offset.Normalized();
	}

	public virtual bool TryAttackEntity(Entity target)
	{
		if (target == null || !IsInstanceValid(target) || target == this || target.IsDead)
			return false;
		if (_toolAttackCooldownRemaining > 0.0f || GlobalPosition.DistanceTo(target.GlobalPosition) > AttackRange)
			return false;
		_toolAttackCooldownRemaining = Mathf.Max(0.0f, AttackCooldown);
		target.TakeDamageAmount(AttackDamage, this);
		return true;
	}

	public virtual bool TryPurchaseUpgrade(string upgradeId) => false;

	public virtual bool TryInteractWith(Node2D target)
	{
		if (target == null || !IsInstanceValid(target) || GlobalPosition.DistanceTo(target.GlobalPosition) > 48.0f)
			return false;
		if (target is Entity entity)
			entity.OnInteractedBy(this);
		else if (target.HasMethod("Interact"))
			target.Call("Interact", this);
		else
			return false;
		return true;
	}

	public virtual void OnInteractedBy(Entity actor)
	{
		AppendChatHistory("system", $"{actor.Name} interacted with {Name}.");
		EmitSignal(SignalName.EntityInteractionRequested, actor.Name.ToString());
	}

	public virtual Node2D? ResolveEntityTarget(string entityIdOrName)
	{
		if (string.IsNullOrWhiteSpace(entityIdOrName)) return null;
		var scene = GetTree().CurrentScene;
		if (scene?.FindChild(entityIdOrName, true, false) is Node2D byName) return byName;
		foreach (Node node in GetTree().GetNodesInGroup("entities"))
			if (node is Node2D entity && IsInstanceValid(entity) && entity.Name == entityIdOrName) return entity;
		foreach (Node node in GetTree().GetNodesInGroup(entityIdOrName))
			if (node is Node2D target && IsInstanceValid(target)) return target;
		return null;
	}

	public virtual void RememberState(string key, Variant value)
	{
		if (!string.IsNullOrWhiteSpace(key)) Memory[key] = value;
	}

	public virtual Variant RecallState(string key, Variant defaultValue = default)
	{
		return !string.IsNullOrWhiteSpace(key) && Memory.TryGetValue(key, out var value) ? value : defaultValue;
	}

	public virtual void AppendChatHistory(string role, string content)
	{
		_chatHistory.Add(new Dictionary<string, string>
		{
			["role"] = string.IsNullOrWhiteSpace(role) ? "system" : role,
			["content"] = content ?? string.Empty,
			["ts"] = DateTime.UtcNow.ToString("o")
		});
		if (_chatHistory.Count > 40) _chatHistory.RemoveRange(0, _chatHistory.Count - 40);
	}

	public virtual Godot.Collections.Array GetChatHistoryState(int maxEntries = 40)
	{
		var result = new Godot.Collections.Array();
		int start = Mathf.Max(0, _chatHistory.Count - Mathf.Max(0, maxEntries));
		for (int i = start; i < _chatHistory.Count; i++)
		{
			var entry = new Godot.Collections.Dictionary();
			foreach (var pair in _chatHistory[i]) entry[pair.Key] = pair.Value;
			result.Add(entry);
		}
		return result;
	}

	public virtual void Speak(string message)
	{
		if (string.IsNullOrWhiteSpace(message)) return;
		AppendChatHistory("assistant", message);
		EmitSignal(SignalName.EntitySpeech, message);
	}

	public virtual Godot.Collections.Array GetAvailableTools()
	{
		var tools = new Godot.Collections.Array();
		tools.Add(Tool("observe", "Observe nearby entities, objects, and your current state.", new Godot.Collections.Dictionary()));
		tools.Add(Tool("move_to", "Move to world-space coordinates.", new Godot.Collections.Dictionary { ["x"] = "number", ["y"] = "number" }));
		tools.Add(Tool("move_to_entity", "Move toward an entity by name.", new Godot.Collections.Dictionary { ["entity_id"] = "string" }));
		tools.Add(Tool("stop", "Stop the current movement request.", new Godot.Collections.Dictionary()));
		tools.Add(Tool("attack", "Attack a nearby entity in range.", new Godot.Collections.Dictionary { ["entity_id"] = "string" }));
		tools.Add(Tool("interact", "Interact with a nearby entity or world object.", new Godot.Collections.Dictionary { ["target_id"] = "string" }));
		tools.Add(Tool("inventory", "Inspect your inventory and equipped weapon.", new Godot.Collections.Dictionary()));
		tools.Add(Tool("equip_weapon", "Equip a weapon you currently carry.", new Godot.Collections.Dictionary { ["item_id"] = "string" }));
		tools.Add(Tool("purchase_upgrade", "Purchase an available character upgrade.", new Godot.Collections.Dictionary { ["upgrade_id"] = "string" }));
		tools.Add(Tool("remember", "Store a value in your persistent entity memory.", new Godot.Collections.Dictionary { ["key"] = "string", ["value"] = "string" }));
		tools.Add(Tool("recall", "Read a value from your entity memory.", new Godot.Collections.Dictionary { ["key"] = "string" }));
		tools.Add(Tool("speak", "Say something that can be shown to nearby characters.", new Godot.Collections.Dictionary { ["message"] = "string" }));
		return tools;
	}

	public virtual Godot.Collections.Dictionary ExecuteTool(string toolName, Godot.Collections.Dictionary arguments)
	{
		bool success;
		string result;
		switch (toolName)
		{
			case "observe":
				return ToolResult(true, Json.Stringify(GetWorldObservation()));
			case "move_to":
				success = TryReadNumber(arguments, "x", out float x) && TryReadNumber(arguments, "y", out float y) &&
					GoToPosition(new Vector2(x, y), "LLM tool");
				result = success ? "Moving to requested position." : "Position must include numeric x and y.";
				break;
			case "move_to_entity":
				string entityId = ReadString(arguments, "entity_id");
				success = GoToEntity(entityId, "LLM tool");
				result = success ? $"Moving toward {entityId}." : "entity_id is required.";
				break;
			case "stop":
				ClearIntent();
				success = true;
				result = "Movement stopped.";
				break;
			case "attack":
				Node2D? attackTarget = ResolveEntityTarget(ReadString(arguments, "entity_id"));
				success = attackTarget is Entity entityTarget && TryAttackEntity(entityTarget);
				result = success ? $"Attacked {attackTarget!.Name}." : "Target is missing, out of range, or cannot be attacked now.";
				break;
			case "interact":
				Node2D? interactionTarget = ResolveEntityTarget(ReadString(arguments, "target_id"));
				success = interactionTarget != null && TryInteractWith(interactionTarget);
				result = success ? $"Interacted with {interactionTarget!.Name}." : "Target is missing, too far away, or cannot be interacted with.";
				break;
			case "inventory":
				return ToolResult(true, Json.Stringify(GetInventoryState()));
			case "equip_weapon":
				string itemId = ReadString(arguments, "item_id");
				success = TryEquipWeapon(itemId);
				result = success ? $"Equipped {itemId}." : "That weapon is not in your inventory.";
				break;
			case "purchase_upgrade":
				string upgradeId = ReadString(arguments, "upgrade_id");
				success = TryPurchaseUpgrade(upgradeId);
				result = success ? $"Purchased upgrade '{upgradeId}'." : $"Could not purchase upgrade '{upgradeId}'.";
				break;
			case "remember":
				string key = ReadString(arguments, "key");
				string value = ReadString(arguments, "value");
				success = !string.IsNullOrWhiteSpace(key);
				if (success) RememberState(key, value);
				result = success ? "Memory updated." : "key is required.";
				break;
			case "recall":
				string memoryKey = ReadString(arguments, "key");
				success = !string.IsNullOrWhiteSpace(memoryKey);
				result = success ? RecallState(memoryKey, "(not remembered)").AsString() : "key is required.";
				break;
			case "speak":
				string message = ReadString(arguments, "message");
				success = !string.IsNullOrWhiteSpace(message);
				if (success) Speak(message);
				result = success ? "Message spoken." : "message is required.";
				break;
			default:
				success = false;
				result = $"Unknown tool '{toolName}'.";
				break;
		}

		LastActionResult = result;
		return ToolResult(success, result);
	}

	public virtual Godot.Collections.Dictionary GetEntityState()
	{
		return new Godot.Collections.Dictionary
		{
			["entity_type"] = GetType().Name,
			["name"] = Name.ToString(),
			["skin_id"] = SkinId,
			["faction"] = Faction,
			["global_position"] = GlobalPosition,
			["health"] = Health,
			["max_health"] = MaxHealth,
			["is_dead"] = IsDead,
			["money"] = Money,
			["equipped_weapon_id"] = EquippedWeaponId,
			["inventory"] = GetInventoryState(),
			["memory"] = Memory.Duplicate(true),
			["intent"] = CurrentIntent.ToString(),
			["intent_target_entity_id"] = IntentTargetEntityId ?? string.Empty,
			["intent_target_position"] = IntentTargetPosition.HasValue ? IntentTargetPosition.Value : default,
			["last_action_result"] = LastActionResult,
			["stats"] = new Godot.Collections.Dictionary
			{
				["speed"] = Speed,
				["attack_damage"] = AttackDamage,
				["attack_range"] = AttackRange,
				["attack_cooldown"] = AttackCooldown,
			}
		};
	}

	public virtual Godot.Collections.Dictionary GetWorldObservation(float radius = 240.0f)
	{
		var nearby = new Godot.Collections.Array();
		foreach (Node node in GetTree().GetNodesInGroup("entities"))
		{
			if (node is not Entity entity || entity == this || !IsInstanceValid(entity)) continue;
			if (GlobalPosition.DistanceTo(entity.GlobalPosition) <= radius)
				nearby.Add(new Godot.Collections.Dictionary
				{
					["name"] = entity.Name.ToString(),
					["skin_id"] = entity.SkinId,
					["faction"] = entity.Faction,
					["global_position"] = entity.GlobalPosition,
					["health"] = entity.Health,
					["max_health"] = entity.MaxHealth,
					["is_dead"] = entity.IsDead
				});
		}
		var cycle = GetTree().GetFirstNodeInGroup("day_night_cycle") as DayNightCycle;
		return new Godot.Collections.Dictionary
		{
			["self"] = GetEntityState(),
			["nearby_entities"] = nearby,
			["nearby_objects"] = GetNearbyObjects(radius),
			["world_day"] = WorldProgress.CurrentWorldDay,
			["daylight_factor"] = cycle?.DaylightFactor ?? 1.0f,
			["available_tools"] = GetAvailableTools()
		};
	}

	public virtual string GetEntityStateJson(bool indented = false) =>
		indented ? Json.Stringify(GetEntityState(), "\t") : Json.Stringify(GetEntityState());

	private Godot.Collections.Dictionary GetInventoryState()
	{
		var slots = new Godot.Collections.Array();
		foreach (var slot in Inventory.Slots)
			slots.Add(new Godot.Collections.Dictionary
			{
				["item_id"] = slot?.ItemId ?? string.Empty,
				["count"] = slot?.Count ?? 0,
				["is_empty"] = slot == null || slot.IsEmpty
			});
		return new Godot.Collections.Dictionary
		{
			["money"] = Money,
			["equipped_weapon_id"] = EquippedWeaponId,
			["slots"] = slots
		};
	}

	private Godot.Collections.Array GetNearbyObjects(float radius)
	{
		var objects = new Godot.Collections.Array();
		foreach (Node node in GetTree().GetNodesInGroup("interactables"))
		{
			if (node is not Node2D target || !IsInstanceValid(target) || GlobalPosition.DistanceTo(target.GlobalPosition) > radius) continue;
			objects.Add(new Godot.Collections.Dictionary
			{
				["name"] = target.Name.ToString(),
				["type"] = target.GetType().Name,
				["global_position"] = target.GlobalPosition
			});
		}
		return objects;
	}

	private bool TryEquipWeapon(string itemId)
	{
		var definition = ItemCatalog.Get(itemId);
		if (definition == null || !definition.IsWeapon) return false;
		if (EquippedWeaponId == itemId) return true;
		foreach (var slot in Inventory.Slots)
		{
			if (slot != null && !slot.IsEmpty && slot.ItemId == itemId)
			{
				EquippedWeaponId = itemId;
				return true;
			}
		}
		return false;
	}

	private static Godot.Collections.Dictionary Tool(string name, string description, Godot.Collections.Dictionary parameters)
	{
		var properties = new Godot.Collections.Dictionary();
		foreach (var parameter in parameters)
			properties[parameter.Key] = new Godot.Collections.Dictionary { ["type"] = parameter.Value };
		return new Godot.Collections.Dictionary
		{
			["name"] = name,
			["description"] = description,
			["parameters"] = new Godot.Collections.Dictionary
			{
				["type"] = "object",
				["properties"] = properties
			}
		};
	}

	private static Godot.Collections.Dictionary ToolResult(bool success, string result) =>
		new() { ["success"] = success, ["result"] = result };

	private static string ReadString(Godot.Collections.Dictionary dictionary, string key) =>
		dictionary.TryGetValue(key, out Variant value) ? value.AsString() : string.Empty;

	private static bool TryReadNumber(Godot.Collections.Dictionary dictionary, string key, out float value)
	{
		value = 0.0f;
		if (!dictionary.TryGetValue(key, out Variant variant)) return false;
		if (variant.VariantType != Variant.Type.Float && variant.VariantType != Variant.Type.Int) return false;
		value = (float)variant.AsDouble();
		return true;
	}
}
