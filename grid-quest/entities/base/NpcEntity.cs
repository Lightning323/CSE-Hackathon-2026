#nullable enable
using Godot;
using System.Collections.Generic;

namespace GridQuest.Entities.Base;

/// <summary>
/// Base class for non-playable controllable entities (villagers, knights, and future NPC/enemy controllers).
/// Adds LLM/tool-friendly APIs for intent control, navigation requests, chat history, and arbitrary state JSON.
/// </summary>
/// <remarks>
/// This class does not implement a full navigation/pathfinding system; it provides a common interface and state
/// container that subclasses can honor in their AI/update loops. This keeps integration simple while making NPCs
/// easy for external controllers (including LLM tool calls) to read and direct.
/// </remarks>
public abstract partial class NpcEntity : Entity
{
	/// <summary>High-level intent modes for tool/debug/state consumption.</summary>
	public enum NpcIntentMode
	{
		Idle,
		Patrol,
		MoveToPoint,
		MoveToEntity,
		Interact,
		Combat,
		Flee,
		Custom
	}

	/// <summary>Current high-level intent.</summary>
	public NpcIntentMode CurrentIntent { get; protected set; } = NpcIntentMode.Idle;
	/// <summary>Optional world target position for move intents.</summary>
	public Vector2? IntentTargetPosition { get; protected set; }
	/// <summary>Optional target identifier/name/group for entity-directed intents.</summary>
	public string? IntentTargetEntityId { get; protected set; }

	/// <summary>In-memory chat history (oldest->newest) for conversation/tool traces.</summary>
	protected readonly List<Dictionary<string, string>> _chatHistory = new();
	/// <summary>Arbitrary structured NPC state payload (LLM/tool readable/writable).</summary>
	protected readonly Godot.Collections.Dictionary _stateData = new();

	/// <summary>Max chat messages kept in memory.</summary>
	[Export] public int MaxChatHistoryEntries { get; set; } = 40;

	/// <summary>
	/// Requests movement to a world-space position. Subclasses should consume this intent in their update loop.
	/// </summary>
	public virtual bool GoToPosition(Vector2 worldPosition, string reason = "")
	{
		CurrentIntent = NpcIntentMode.MoveToPoint;
		IntentTargetPosition = worldPosition;
		IntentTargetEntityId = null;
		RememberState("last_move_reason", reason ?? string.Empty);
		return true;
	}

	/// <summary>
	/// Requests movement toward a named entity/node/group resolved via <see cref="ResolveEntityTarget"/>.
	/// </summary>
	public virtual bool GoToEntity(string entityIdOrName, string reason = "")
	{
		CurrentIntent = NpcIntentMode.MoveToEntity;
		IntentTargetEntityId = entityIdOrName;
		IntentTargetPosition = null;
		RememberState("last_move_reason", reason ?? string.Empty);
		return true;
	}

	/// <summary>Clears current movement/interaction intent.</summary>
	public virtual void ClearIntent()
	{
		CurrentIntent = NpcIntentMode.Idle;
		IntentTargetEntityId = null;
		IntentTargetPosition = null;
	}

	/// <summary>Stores arbitrary state for this NPC (tool/LLM controllable metadata).</summary>
	public virtual void RememberState(string key, Variant value)
	{
		if (string.IsNullOrWhiteSpace(key)) return;
		_stateData[key] = value;
	}

	/// <summary>Reads arbitrary state previously stored on this NPC.</summary>
	public virtual Variant RecallState(string key, Variant defaultValue = default)
	{
		if (string.IsNullOrWhiteSpace(key)) return defaultValue;
		return _stateData.TryGetValue(key, out var value) ? value : defaultValue;
	}

	/// <summary>Returns a copy of NPC structured state data.</summary>
	public virtual Godot.Collections.Dictionary GetNpcStateData()
	{
		var copy = new Godot.Collections.Dictionary();
		foreach (var kv in _stateData) copy[kv.Key] = kv.Value;
		return copy;
	}

	/// <summary>Applies external state payload to this NPC (merge or replace).</summary>
	public virtual void ApplyNpcStateData(Godot.Collections.Dictionary data, bool merge = true)
	{
		if (data == null) return;
		if (!merge) _stateData.Clear();
		foreach (var kv in data) _stateData[kv.Key] = kv.Value;
	}

	/// <summary>Adds a chat history record for LLM memory/context traces.</summary>
	public virtual void AppendChatHistory(string role, string content)
	{
		_chatHistory.Add(new Dictionary<string, string>
		{
			["role"] = role ?? "system",
			["content"] = content ?? string.Empty,
			["ts"] = System.DateTime.UtcNow.ToString("o")
		});
		TrimChatHistory();
	}

	/// <summary>Returns chat history entries (optionally last N) as copies.</summary>
	public virtual List<Dictionary<string, string>> GetChatHistory(int? maxEntries = null)
	{
		int take = maxEntries.HasValue ? Mathf.Clamp(maxEntries.Value, 0, _chatHistory.Count) : _chatHistory.Count;
		if (take <= 0) return new List<Dictionary<string, string>>();
		int start = Mathf.Max(0, _chatHistory.Count - take);
		var result = new List<Dictionary<string, string>>(take);
		for (int i = start; i < _chatHistory.Count; i++) result.Add(new Dictionary<string, string>(_chatHistory[i]));
		return result;
	}

	/// <summary>Clears chat history.</summary>
	public virtual void ClearChatHistory() => _chatHistory.Clear();

	protected void TrimChatHistory()
	{
		int max = Mathf.Max(1, MaxChatHistoryEntries);
		if (_chatHistory.Count <= max) return;
		_chatHistory.RemoveRange(0, _chatHistory.Count - max);
	}

	/// <summary>
	/// Resolves an entity target by node name under current scene, then by group name.
	/// Override for custom registries/ids.
	/// </summary>
	public virtual Node2D? ResolveEntityTarget(string entityIdOrName)
	{
		if (string.IsNullOrWhiteSpace(entityIdOrName)) return null;
		var scene = GetTree().CurrentScene;
		if (scene == null) return null;
		if (scene.FindChild(entityIdOrName, true, false) is Node2D byName) return byName;
		foreach (var node in GetTree().GetNodesInGroup(entityIdOrName))
			if (node is Node2D n2d && IsInstanceValid(n2d)) return n2d;
		return null;
	}

	/// <summary>
	/// Returns LLM/tool-friendly control state including base entity state, intent, and arbitrary NPC memory.
	/// Subclasses should append domain-specific details (home village, trust, combat target, dialogue, etc.).
	/// </summary>
	public virtual Godot.Collections.Dictionary GetNpcControlState()
	{
		var state = GetEntityState();
		state["npc_intent"] = CurrentIntent.ToString();
		state["intent_target_position"] = IntentTargetPosition.HasValue ? IntentTargetPosition.Value : default;
		state["has_intent_target_position"] = IntentTargetPosition.HasValue;
		state["intent_target_entity_id"] = IntentTargetEntityId ?? string.Empty;
		state["npc_state_data"] = GetNpcStateData();
		state["chat_history"] = GetChatHistoryState();
		return state;
	}

	/// <summary>Serializes NPC control state as JSON.</summary>
	public virtual string GetNpcControlStateJson(bool indented = false)
	{
		return indented ? Json.Stringify(GetNpcControlState(), "\t") : Json.Stringify(GetNpcControlState());
	}

	/// <summary>Exports chat history as a Godot array for state snapshots/tool responses.</summary>
	public virtual Godot.Collections.Array GetChatHistoryState(int? maxEntries = null)
	{
		var arr = new Godot.Collections.Array();
		foreach (var entry in GetChatHistory(maxEntries))
		{
			var d = new Godot.Collections.Dictionary();
			foreach (var kv in entry) d[kv.Key] = kv.Value;
			arr.Add(d);
		}
		return arr;
	}
}
