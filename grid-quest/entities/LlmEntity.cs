#nullable enable
using Godot;
using GridQuest.Entities.Base;
using GridQuest.Entities.Player;
using GridQuest.GameGenerator.Llm;
using System;
using System.Text.Json;
using System.Threading.Tasks;

namespace GridQuest.Entities;

public partial class LlmEntity : PlayerEntity
{
	[Export(PropertyHint.MultilineText)] public string SystemPrompt { get; set; } = "You are a curious villager. Explore your surroundings, talk with nearby characters, and remember useful things.";
	[Export] public float DecisionIntervalSeconds { get; set; } = 12.0f;
	[Export] public float InteractionRange { get; set; } = 48.0f;

	private bool _thinking;
	private bool _decisionRequested = true;
	private float _decisionTimer;
	private float _minimumDecisionDelay;
	private AnimatedSprite2D? _entitySprite;

	public override void _Ready()
	{
		UseHumanInput = false;
		base._Ready();
		Faction = "villager";
		AddToGroup("llm_entities");
		AddToGroup("villagers");
		_entitySprite = GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
		if (_entitySprite != null)
		{
			_entitySprite.SpriteFrames = SkinCatalog.BuildSpriteFrames(SkinId);
			_entitySprite.Play("idle_down");
		}
		EntityInteractionRequested += OnInteractionRequested;
	}

	public override Godot.Collections.Dictionary GetEntityState()
	{
		var state = base.GetEntityState();
		state["entity_kind"] = "llm_entity";
		state["is_player"] = false;
		state["system_prompt"] = SystemPrompt;
		state["is_thinking"] = _thinking;
		return state;
	}

	public async Task<string> RespondToPlayerAsync(string message)
	{
		var api = LlmApi.Instance ?? GetNodeOrNull<LlmApi>("/root/LlmApi");
		if (api == null) throw new InvalidOperationException("LlmApi is unavailable.");
		AppendChatHistory("user", message);
		string prompt = $"A nearby character says: {message}\n\nRecent conversation:\n{Json.Stringify(GetChatHistoryState(12))}\n\nYour current observed state is:\n{Json.Stringify(GetWorldObservation(InteractionRange * 5.0f))}\n\nReply in character in 1-3 short sentences.";
		string reply = await api.ChatAsync(prompt, SystemPrompt);
		if (IsInsideTree())
		{
			AppendChatHistory("assistant", reply);
			RequestDecision(0.0f);
		}
		return reply;
	}

	public override void _Process(double delta)
	{
		base._Process(delta);
		_decisionTimer += (float)delta;
		_minimumDecisionDelay = Mathf.Max(0.0f, _minimumDecisionDelay - (float)delta);
		if (_decisionRequested && !_thinking && _minimumDecisionDelay <= 0.0f)
			ThinkAsync();
		else if (!_thinking && _decisionTimer >= Mathf.Max(1.0f, DecisionIntervalSeconds))
			ThinkAsync();
	}

	public override void _Input(InputEvent @event)
	{
		if (!(@event.IsActionPressed("interact") ||
			(@event is InputEventKey key && key.Pressed && !key.Echo && key.Keycode == Key.F)))
			return;
		var chat = GetTree().GetFirstNodeInGroup("villager_chat_ui") as VillagerChatUi;
		if (chat == null || chat.Visible) return;
		var player = GetTree().GetFirstNodeInGroup("player") as PlayerEntity;
		if (player == null || player.GlobalPosition.DistanceTo(GlobalPosition) > InteractionRange) return;
		foreach (Node node in GetTree().GetNodesInGroup("llm_entities"))
		{
			if (node is not LlmEntity other || other == this || !IsInstanceValid(other)) continue;
			float otherDistance = player.GlobalPosition.DistanceTo(other.GlobalPosition);
			if (otherDistance <= other.InteractionRange && otherDistance < player.GlobalPosition.DistanceTo(GlobalPosition))
				return;
		}
		chat.Open(this);
		GetViewport().SetInputAsHandled();
	}

	public override int TakeDamageAmount(int amount, Node2D? source = null)
	{
		int applied = base.TakeDamageAmount(amount, source);
		if (applied > 0) RequestDecision(0.25f);
		return applied;
	}

	public override void OnInteractedBy(Entity actor)
	{
		base.OnInteractedBy(actor);
		AppendChatHistory("user", $"{actor.Name} is nearby and wants to interact.");
		RequestDecision(0.0f);
	}

	public void ReceiveMessage(string speaker, string message)
	{
		AppendChatHistory("user", $"{speaker}: {message}");
		RequestDecision(0.0f);
	}

	private void OnInteractionRequested(string actorName) => RequestDecision(0.0f);

	private void RequestDecision(float delay)
	{
		_decisionRequested = true;
		_minimumDecisionDelay = Mathf.Max(0.0f, delay);
	}

	private async void ThinkAsync()
	{
		if (_thinking || IsDead || !IsInsideTree()) return;
		var api = LlmApi.Instance ?? GetNodeOrNull<LlmApi>("/root/LlmApi");
		if (api == null)
		{
			GD.PushWarning($"LLM entity '{Name}' cannot think because LlmApi is unavailable.");
			_decisionRequested = false;
			_decisionTimer = 0.0f;
			return;
		}

		_thinking = true;
		_decisionRequested = false;
		_decisionTimer = 0.0f;
		try
		{
			var observation = GetWorldObservation(InteractionRange * 5.0f);
			observation["recent_memory"] = Memory.Duplicate(true);
			observation["recent_chat"] = GetChatHistoryState(12);
			string prompt = "Observe the game state and choose exactly one available tool action. " +
				"Return only JSON in this shape: {\"tool\":\"tool_name\",\"arguments\":{...}}. " +
				"Do not invent tools or entity ids.\n\n" + Json.Stringify(observation);
			string response = await api.ChatAsync(prompt, SystemPrompt);
			if (!IsInsideTree()) return;
			ExecuteModelAction(response);
		}
		catch (Exception exception)
		{
			GD.PushWarning($"LLM decision failed for '{Name}': {exception.Message}");
		}
		finally
		{
			_thinking = false;
			_decisionTimer = 0.0f;
		}
	}

	private void ExecuteModelAction(string response)
	{
		using JsonDocument document = JsonDocument.Parse(response);
		JsonElement root = document.RootElement;
		if (root.ValueKind != JsonValueKind.Object ||
			!root.TryGetProperty("tool", out JsonElement toolElement) ||
			toolElement.ValueKind != JsonValueKind.String ||
			!root.TryGetProperty("arguments", out JsonElement argumentsElement) ||
			argumentsElement.ValueKind != JsonValueKind.Object)
			throw new FormatException("The model response must include string 'tool' and object 'arguments' fields.");

		var arguments = new Godot.Collections.Dictionary();
		foreach (JsonProperty property in argumentsElement.EnumerateObject())
			arguments[property.Name] = ToVariant(property.Value);
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
}
