#nullable enable
using Godot;
using Godot.Collections;
using GridQuest.Entities.Base;
using System;
using System.Text.Json;

namespace GridQuest.Entities.Npc;

/// <summary>
/// Villager NPC: social/chat/trust/fleeing/gifting behavior with home-village awareness.
/// Extends <see cref="NpcEntity"/> so it can be controlled/read by tools and LLM-facing APIs.
/// </summary>
public partial class VillagerEntity : NpcEntity
{
	[Signal] public delegate void DiedEventHandler(VillagerEntity villager, int villageIndex);

	public enum DialogueEmotion { Idle, Chased, Recovered }

	[Export] public new float Speed { get => base.Speed; set => base.Speed = value; }
	[Export] public float PatrolSpeed { get; set; } = 25.0f;
	[Export] public new int MaxHealth { get => base.MaxHealth; set => base.MaxHealth = value; }
	[Export] public float FleeDetectionRange { get; set; } = 140.0f;
	[Export] public float InteractRange { get; set; } = 42.0f;
	[Export] public float RecoveredStateDuration { get; set; } = 5.0f;
	[Export] public float DialogueDisplayDuration { get; set; } = 4.0f;
	[Export] public float GiftRange { get; set; } = 72.0f;
	[Export] public float GiftCheckInterval { get; set; } = 4.0f;
	[Export] public bool IsTrader { get; set; } = false;

	public int Trust { get; private set; } = 0;
	public bool HasPendingAdviceRequest => _hasPendingAdviceRequest;
	public string PendingAdviceQuestion => _pendingAdviceQuestion ?? "";
	public void DebugAdjustTrust(int delta) => Trust = Mathf.Clamp(Trust + delta, 0, 10);

	private static readonly PackedScene CoinScene = GD.Load<PackedScene>("res://items/coin.tscn");
	private static readonly string[] AdviceQuestions = {
		"I've been feeling stuck lately. What should I do to move forward?",
		"Sometimes I'm afraid of making the wrong choice. Any life advice?",
		"How do you stay hopeful when things get difficult?",
		"I want to be more helpful to others. Where should I start?",
		"Do you have advice for finding courage in scary times?"
	};
	private static readonly string[] TrustGiftDialogue = { "Here's for your troubles.", "For your next upgrade.", "Thanks for listening to me.", "You've been a real help. Take this!", "For all your good advice." };
	private static readonly string[] HighTrustGiftDialogue = { "You've earned this and more!", "For your next upgrade — and the one after!", "I trust you completely. Please, take these!", "Here's a little fortune for a true friend." };
	private static readonly string[] IdleDialogueOptions = { "Lovely day in the village.", "Have you seen anything strange nearby?", "I should check on the others soon." };
	private static readonly string[] ChasedDialogueOptions = { "Help! Something's after me!", "Run! There's a monster!", "Please, keep it away from me!" };
	private static readonly string[] RecoveredDialogueOptions = { "Phew... I think I'm safe now.", "That was too close.", "I need a moment to catch my breath." };
	private static readonly System.Collections.Generic.Dictionary<int, double> VillageSpeechUnlockMsec = new();
	private const double VillageSpeechMinGapMsec = 900.0;

	private bool _hasPendingAdviceRequest;
	private string _pendingAdviceQuestion = "";
	private float _adviceCooldownTimer;
	private float _giftTimer;
	private bool _trustDialogueActive;
	private float _ambientTalkTimer;

	private AnimatedSprite2D _sprite;
	private ProgressBar _healthBar;
	private Node2D _speechBubble;
	private PanelContainer _bubblePanel;
	private Label _dialogueLabel;
	private Control _traderLabelRoot;
	private Label _traderLabel;
	private State _state = State.Idle;
	private string _facing = "down";
	private Vector2 _patrolTarget;
	private float _patrolWaitTimer;
	private float _patrolRepathTimer;
	private float _fleeFacingTimer;
	private float _fleeDialogueCooldownTimer;

	private bool _hasHome;
	private Rect2 _homeWorldRect;
	private Vector2 _homeCenterWorld;
	private int _homeVillageIndex = -1;

	private DialogueEmotion _dialogueEmotion = DialogueEmotion.Idle;
	private bool _wasRecentlyChased;
	private float _recoveryTimer;
	private float _dialogueVisibleTimer;
	private string _currentDialogueText = "";
	private Node2D _lastThreat;
	private float _lastThreatDistance = float.MaxValue;

	private enum State { Idle, Patrol, Flee }

	public DialogueEmotion CurrentDialogueEmotion => _dialogueEmotion;
	public bool IsFleeing => _state == State.Flee;
	public string CurrentDialogueText => _currentDialogueText;

	public override void _Ready()
	{
		base.Speed = 90.0f;
		base.MaxHealth = 3;
		base._Ready();
		Health = MaxHealth;
		_sprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
		_healthBar = GetNodeOrNull<ProgressBar>("HealthBar");
		_speechBubble = GetNodeOrNull<Node2D>("SpeechBubble");
		_bubblePanel = GetNodeOrNull<PanelContainer>("SpeechBubble/BubblePanel");
		_dialogueLabel = GetNodeOrNull<Label>("SpeechBubble/BubblePanel/MarginContainer/DialogueLabel");
		_traderLabelRoot = GetNodeOrNull<Control>("TraderLabel");
		_traderLabel = GetNodeOrNull<Label>("TraderLabel/Label") ?? GetNodeOrNull<Label>("TraderLabel");
		_sprite.SpriteFrames = BuildSpriteFrames();
		_sprite.Play("idle_down");
		AddToGroup("villagers");
		AddToGroup("civilians");
		AddToGroup("villager_npcs");
		if (IsTrader)
		{
			AddToGroup("traders");
			PatrolSpeed = Mathf.Min(PatrolSpeed, 14.0f);
			base.Speed = Mathf.Min(base.Speed, 55.0f);
		}
		PickNewPatrolTarget();
		ApplyHealthBarStyle();
		UpdateHealthBar();
		ConfigureSpeechBubble();
		SetDialogueForEmotion(DialogueEmotion.Idle, false);
		if (_traderLabelRoot != null)
			_traderLabelRoot.Visible = IsTrader;
		else if (_traderLabel != null)
			_traderLabel.Visible = IsTrader;
		if (_traderLabel != null)
		{
			_traderLabel.Text = "Trader";
			// Brighter blue on dark rounded backdrop (scene provides backdrop styling).
			_traderLabel.Modulate = Colors.White;
			_traderLabel.AddThemeColorOverride("font_color", new Color(0.55f, 0.85f, 1.0f, 1.0f));
		}
		UpdateTraderLabelLayout();
		_adviceCooldownTimer = (float)GD.RandRange(5.0, 12.0);
		_ambientTalkTimer = (float)GD.RandRange(18.0, 45.0);
		_giftTimer = (float)GD.RandRange(12.0, 20.0);
	}

	public override void _Input(InputEvent @event)
	{
		var chatUiControl = GetTree().GetFirstNodeInGroup("villager_chat_ui") as Control;
		if (chatUiControl != null && chatUiControl.Visible) return;
		var traderUiControl = GetTree().GetFirstNodeInGroup("trader_ui") as Control;
		if (traderUiControl != null && traderUiControl.Visible) return;
		if (!IsInteractionInput(@event)) return;
		Node2D player = GetTree().GetFirstNodeInGroup("player") as Node2D;
		if (player == null) return;
		if (GlobalPosition.DistanceTo(player.GlobalPosition) > InteractRange) return;
		if (!IsClosestInteractableVillager(player)) return;
		if (IsPlayerNearChest(player)) return;

		var wrapper = this as global::Villager;
		if (IsTrader)
		{
			if (traderUiControl != null && traderUiControl.HasMethod("Open"))
			{
				if (wrapper != null)
					traderUiControl.Call("Open", wrapper);
				else
					traderUiControl.Call("Open", this);
			}
			GetViewport().SetInputAsHandled();
			return;
		}

		var chatUi = GetTree().GetFirstNodeInGroup("villager_chat_ui") as VillagerChatUi;
		if (chatUi != null)
		{
			// Chat UI currently expects global Villager wrapper type; prefer wrapper instance in scene tree.
			if (wrapper != null) chatUi.Open(wrapper);
			else ShowDialogueForCurrentState(forceRefresh: true, autoHide: true, prioritize: true);
		}
		else
		{
			ShowDialogueForCurrentState(forceRefresh: true, autoHide: true, prioritize: true);
		}
		GetViewport().SetInputAsHandled();
	}

	/// <summary>Returns a human-readable villager name for UI/LLM context.</summary>
	public string DisplayNameForChat()
	{
		if (IsTrader)
			return _hasHome && _homeVillageIndex >= 0 ? $"Trader (Village {_homeVillageIndex + 1})" : "Trader";
		return _hasHome && _homeVillageIndex >= 0 ? $"Villager (Village {_homeVillageIndex + 1})" : "Villager";
	}

	/// <summary>Builds a system prompt for in-character LLM chat.</summary>
	public string BuildChatSystemPrompt()
	{
		string mood = _dialogueEmotion switch { DialogueEmotion.Chased => "frightened and urgent", DialogueEmotion.Recovered => "relieved but shaken", _ => "calm and friendly" };
		string threat = (_lastThreat != null && IsInstanceValid(_lastThreat)) ? "A threat is nearby." : "No immediate threat is visible.";
		string location = _hasHome ? $"home village #{_homeVillageIndex + 1}" : "the wilderness";
		return $"You are a villager NPC in the game Grid Quest. Stay in character and answer the player's question directly. " +
			$"Current mood/state: {mood}. Location/context: {location}. {threat} " +
			$"Health: {Health}/{MaxHealth}. Trust toward player: {Trust}/10. Recent line: \"{_currentDialogueText}\". " +
			"Be responsive to how kindly/helpfully the player speaks. Keep responses concise (1-3 short sentences), helpful, and grounded in what a villager might know.";
	}

	/// <summary>Builds a user prompt with runtime context for LLM chat.</summary>
	public string BuildChatPrompt(string question)
	{
		var ctx = GetDialogueContext();
		string state = ctx.TryGetValue("state", out var s) ? s.AsString() : _state.ToString();
		string emotion = ctx.TryGetValue("emotion", out var e) ? e.AsString() : _dialogueEmotion.ToString();
		return $"Player question: {question}\n\nNPC runtime context: state={state}, emotion={emotion}, fleeing={IsFleeing}, was_recently_chased={_wasRecentlyChased}, health={Health}/{MaxHealth}, has_home={_hasHome}, village_index={_homeVillageIndex}. Respond as the villager.";
	}

	/// <summary>Sets the villager's home village bounds/center and index.</summary>
	public void SetHome(Rect2I cellBounds, Vector2I centerCell, int villageIndex)
	{
		const int cellSize = 16;
		_homeWorldRect = new Rect2(cellBounds.Position.X * cellSize, cellBounds.Position.Y * cellSize, cellBounds.Size.X * cellSize, cellBounds.Size.Y * cellSize);
		_homeCenterWorld = new Vector2(centerCell.X * cellSize + cellSize / 2.0f, centerCell.Y * cellSize + cellSize / 2.0f);
		_homeVillageIndex = villageIndex; _hasHome = true; _patrolTarget = _homeCenterWorld;
		RememberState("home_village_index", villageIndex);
		RememberState("home_center_world", _homeCenterWorld);
	}

	/// <summary>Returns villager-specific runtime context for UI/LLM systems.</summary>
	public Dictionary GetDialogueContext()
	{
		Node2D player = GetTree().GetFirstNodeInGroup("player") as Node2D;
		var options = new Array<string>();
		foreach (string line in GetOptionsForEmotion(_dialogueEmotion)) options.Add(line);
		return new Dictionary
		{
			["state"] = _state.ToString(), ["emotion"] = _dialogueEmotion.ToString(), ["is_fleeing"] = IsFleeing,
			["was_recently_chased"] = _wasRecentlyChased, ["recovery_seconds_remaining"] = Mathf.Max(0.0f, _recoveryTimer),
			["has_home"] = _hasHome, ["village_index"] = _homeVillageIndex, ["health"] = Health, ["max_health"] = MaxHealth,
			["threat_nearby"] = _lastThreat != null && IsInstanceValid(_lastThreat),
			["threat_distance"] = _lastThreat != null && IsInstanceValid(_lastThreat) ? _lastThreatDistance : -1.0f,
			["player_nearby"] = player != null && GlobalPosition.DistanceTo(player.GlobalPosition) <= InteractRange,
			["current_dialogue"] = _currentDialogueText, ["placeholder_dialogue_options"] = options,
			["dialogue_visible"] = _speechBubble != null && _speechBubble.Visible, ["trust"] = Trust,
			["has_pending_advice_request"] = _hasPendingAdviceRequest
		};
	}

	private static bool IsInteractionInput(InputEvent @event)
	{
		if (@event.IsActionPressed("interact")) return true;
		if (@event is InputEventKey keyEvent && keyEvent.Pressed && !keyEvent.Echo && keyEvent.Keycode == Key.F) return true;
		return false;
	}

	private bool IsPlayerNearChest(Node2D player)
	{
		foreach (Node node in GetTree().GetNodesInGroup("chests"))
			if (node is Node2D chest && IsInstanceValid(chest) && player.GlobalPosition.DistanceTo(chest.GlobalPosition) <= 40.0f) return true;
		return false;
	}

	private bool IsClosestInteractableVillager(Node2D player)
	{
		float myDist = GlobalPosition.DistanceTo(player.GlobalPosition);
		foreach (Node node in GetTree().GetNodesInGroup("villager_npcs"))
		{
			if (ReferenceEquals(node, this)) continue;
			if (node is not CharacterBody2D other || !IsInstanceValid(other)) continue;
			if (other == (object)this) continue;
			// Prefer checking wrapper/global villager range if available
			float range = 42.0f;
			Variant r = other.Get("InteractRange");
			if (r.VariantType == Variant.Type.Float || r.VariantType == Variant.Type.Int) range = (float)r.AsDouble();
			float otherDist = other.GlobalPosition.DistanceTo(player.GlobalPosition);
			if (otherDist <= range && otherDist < myDist) return false;
		}
		return true;
	}

	private SpriteFrames BuildSpriteFrames()
	{
		SpriteFrames frames = new SpriteFrames();
		string basePath = "res://assets/images/characters/villagers/";
		foreach (string anim in new[] { "idle_down", "idle_left", "idle_right", "idle_up", "walk_down", "walk_left", "walk_right", "walk_up" }) { frames.AddAnimation(anim); frames.SetAnimationLoop(anim, true); }
		frames.SetAnimationSpeed("idle_down", 4.0); frames.SetAnimationSpeed("idle_left", 4.0); frames.SetAnimationSpeed("idle_right", 4.0); frames.SetAnimationSpeed("idle_up", 4.0);
		frames.SetAnimationSpeed("walk_down", 8.0); frames.SetAnimationSpeed("walk_left", 8.0); frames.SetAnimationSpeed("walk_right", 8.0); frames.SetAnimationSpeed("walk_up", 8.0);
		AddFrames(frames, "walk_down", basePath + "walkdown1-crop-folk-dogwalker.png", basePath + "walkdown2-crop-folk-dogwalker.png", basePath + "walkdown3-crop-folk-dogwalker.png", basePath + "walkdown4-crop-folk-dogwalker.png");
		AddFrames(frames, "walk_left", basePath + "walkleft1-crop-folk-dogwalker.png", basePath + "walkleft2-crop-folk-dogwalker.png", basePath + "walkleft3-crop-folk-dogwalker.png", basePath + "walkleft4-crop-folk-dogwalker.png");
		AddFrames(frames, "walk_right", basePath + "walkright1-crop-folk-dogwalker.png", basePath + "walkright2-crop-folk-dogwalker.png", basePath + "walkright3-crop-folk-dogwalker.png", basePath + "walkright4-crop-folk-dogwalker.png");
		AddFrames(frames, "walk_up", basePath + "walkup1-crop-folk-dogwalker.png", basePath + "walkup2-crop-folk-dogwalker.png", basePath + "walkup3-crop-folk-dogwalker.png", basePath + "walkup4-crop-folk-dogwalker.png");
		frames.AddFrame("idle_down", frames.GetFrameTexture("walk_down", 0)); frames.AddFrame("idle_left", frames.GetFrameTexture("walk_left", 0));
		frames.AddFrame("idle_right", frames.GetFrameTexture("walk_right", 0)); frames.AddFrame("idle_up", frames.GetFrameTexture("walk_up", 0));
		return frames;
	}
	private static void AddFrames(SpriteFrames frames, string anim, params string[] paths) { foreach (string p in paths) frames.AddFrame(anim, GD.Load<Texture2D>(p)); }

	public override void _PhysicsProcess(double delta)
	{
		float dt = (float)delta;
		_fleeFacingTimer -= dt; _fleeDialogueCooldownTimer = Mathf.Max(0.0f, _fleeDialogueCooldownTimer - dt);
		UpdateDialogueTimers(dt); UpdateTrustAndGifts(dt);
		if (IsDead) { Velocity = Vector2.Zero; MoveAndSlide(); return; }

		Node2D threat = FindNearestThreat();
		if (threat != null)
		{
			_lastThreat = threat; _lastThreatDistance = GlobalPosition.DistanceTo(threat.GlobalPosition);
			_wasRecentlyChased = true; _recoveryTimer = 0.0f; UpdateFleeDialogue();
			SetDialogueEmotionState(DialogueEmotion.Chased, forceText: false);
			FleeFrom(threat.GlobalPosition); CurrentIntent = NpcIntentMode.Flee; MoveAndSlide(); return;
		}

		_lastThreat = null; _lastThreatDistance = float.MaxValue;
		if (_wasRecentlyChased)
		{
			_recoveryTimer += dt;
			if (_recoveryTimer < RecoveredStateDuration) SetDialogueEmotionState(DialogueEmotion.Recovered, forceText: _dialogueEmotion != DialogueEmotion.Recovered);
			else { _wasRecentlyChased = false; _recoveryTimer = 0.0f; _fleeDialogueCooldownTimer = 0.0f; SetDialogueEmotionState(DialogueEmotion.Idle, forceText: _dialogueEmotion != DialogueEmotion.Idle); }
		}
		else SetDialogueEmotionState(DialogueEmotion.Idle, forceText: false);

		if (CurrentIntent == NpcIntentMode.MoveToPoint && IntentTargetPosition.HasValue)
		{
			MoveTowardPoint(IntentTargetPosition.Value, PatrolSpeed);
			if (GlobalPosition.DistanceTo(IntentTargetPosition.Value) <= 8.0f) ClearIntent();
			MoveAndSlide(); return;
		}
		if (CurrentIntent == NpcIntentMode.MoveToEntity && !string.IsNullOrWhiteSpace(IntentTargetEntityId))
		{
			var target = ResolveEntityTarget(IntentTargetEntityId);
			if (target != null && IsInstanceValid(target))
			{
				MoveTowardPoint(target.GlobalPosition, PatrolSpeed);
				if (GlobalPosition.DistanceTo(target.GlobalPosition) <= 16.0f) ClearIntent();
				MoveAndSlide(); return;
			}
			ClearIntent();
		}
		UpdatePatrol(dt); MoveAndSlide();
	}

	private void UpdateFleeDialogue() { }
	public override void TakeDamage(int amount) => TakeDamageAmount(amount, null);
	public override int TakeDamageAmount(int amount, Node2D? source = null)
	{
		if (IsDead || amount <= 0) return 0;
		int applied = base.TakeDamageAmount(amount, source);
		UpdateHealthBar();
		if (Health == 0) { EmitSignal(SignalName.Died, this, _homeVillageIndex); QueueFree(); }
		return applied;
	}

	private void FleeFrom(Vector2 threatPosition)
	{
		_state = State.Flee; _patrolWaitTimer = 0.0f;
		Vector2 away = GlobalPosition - threatPosition; if (away.LengthSquared() < 0.001f) away = Vector2.Down;
		Vector2 direction = away.Normalized();
		if (_fleeFacingTimer <= 0.0f) { UpdateFacing(direction); _fleeFacingTimer = 0.15f; }
		Velocity = direction * Speed; PlayAnim($"walk_{_facing}");
	}

	private void UpdatePatrol(float delta)
	{
		CurrentIntent = (_state == State.Patrol) ? NpcIntentMode.Patrol : NpcIntentMode.Idle;
		_patrolRepathTimer -= delta;
		if (_hasHome && IsFarOutsideHome()) { MoveTowardPoint(_homeCenterWorld, PatrolSpeed * 1.15f); return; }
		if (_patrolWaitTimer > 0.0f)
		{
			_patrolWaitTimer -= delta; _state = State.Idle; CurrentIntent = NpcIntentMode.Idle; Velocity = Vector2.Zero; PlayAnim($"idle_{_facing}");
			if (_patrolWaitTimer <= 0.0f) PickNewPatrolTarget(); return;
		}
		Vector2 toTarget = _patrolTarget - GlobalPosition;
		if (toTarget.Length() <= 6.0f || _patrolRepathTimer <= 0.0f)
		{
			_patrolWaitTimer = (float)GD.RandRange(0.8, 2.2); _state = State.Idle; CurrentIntent = NpcIntentMode.Idle; Velocity = Vector2.Zero; PlayAnim($"idle_{_facing}"); return;
		}
		MoveTowardPoint(_patrolTarget, PatrolSpeed);
	}

	private void MoveTowardPoint(Vector2 target, float speed)
	{
		Vector2 toTarget = target - GlobalPosition;
		if (toTarget.LengthSquared() < 0.001f) { Velocity = Vector2.Zero; _state = State.Idle; PlayAnim($"idle_{_facing}"); return; }
		Vector2 direction = toTarget.Normalized(); UpdateFacing(direction); _state = State.Patrol; Velocity = direction * speed; PlayAnim($"walk_{_facing}");
	}

	private bool IsFarOutsideHome() => _hasHome && !_homeWorldRect.Grow(24.0f).HasPoint(GlobalPosition);

	private void PickNewPatrolTarget()
	{
		_patrolRepathTimer = (float)GD.RandRange(2.5, 5.0);
		Vector2 origin = GlobalPosition;
		if (_hasHome)
		{
			Rect2 area = _homeWorldRect.Grow(-8.0f); if (area.Size.X < 16.0f || area.Size.Y < 16.0f) area = _homeWorldRect;
			for (int i = 0; i < 10; i++)
			{
				float x = (float)GD.RandRange(area.Position.X, area.End.X); float y = (float)GD.RandRange(area.Position.Y, area.End.Y);
				Vector2 candidate = new(x, y); if (candidate.DistanceTo(origin) >= 16.0f) { _patrolTarget = candidate; return; }
			}
			_patrolTarget = _homeCenterWorld; return;
		}
		Node2D map = GetTree().CurrentScene as Node2D; Node grid = map?.GetNodeOrNull<Node>("MapGrid") ?? map?.GetNodeOrNull<Node>("Map");
		if (grid != null && grid.HasMethod("CellToWorldCenter"))
		{
			int width = grid.Get("Width").AsInt32(); if (width <= 0 && grid.Get("MapWidth").VariantType != Variant.Type.Nil) width = grid.Get("MapWidth").AsInt32();
			int height = grid.Get("Height").AsInt32(); if (height <= 0 && grid.Get("MapHeight").VariantType != Variant.Type.Nil) height = grid.Get("MapHeight").AsInt32();
			if (width > 0 && height > 0)
			{
				for (int i = 0; i < 8; i++)
				{
					int x = GD.RandRange(0, width - 1); int y = GD.RandRange(0, height - 1);
					Vector2 candidate = (Vector2)grid.Call("CellToWorldCenter", x, y);
					if (candidate.DistanceTo(origin) >= 24.0f) { _patrolTarget = candidate; return; }
				}
			}
		}
		float angle = (float)GD.RandRange(0.0, Mathf.Tau); float dist = (float)GD.RandRange(24.0, 80.0);
		_patrolTarget = origin + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * dist;
	}

	private Node2D FindNearestThreat()
	{
		Node2D closest = null; float closestDistSq = FleeDetectionRange * FleeDetectionRange; Vector2 origin = GlobalPosition;
		foreach (Node node in GetTree().GetNodesInGroup("enemies"))
		{
			if (node is not Node2D enemy || !IsInstanceValid(enemy)) continue;
			float distSq = origin.DistanceSquaredTo(enemy.GlobalPosition);
			if (distSq < closestDistSq) { closestDistSq = distSq; closest = enemy; }
		}
		return closest;
	}

	private bool CanReserveVillageSpeech(bool prioritize = false)
	{
		if (!_hasHome || _homeVillageIndex < 0) return true;
		double now = Time.GetTicksMsec();
		if (prioritize) return true;
		return !VillageSpeechUnlockMsec.TryGetValue(_homeVillageIndex, out double unlockMsec) || now >= unlockMsec;
	}

	private void ReserveVillageSpeech(float durationSeconds, bool prioritize = false)
	{
		if (!_hasHome || _homeVillageIndex < 0) return;
		double now = Time.GetTicksMsec();
		double until = now + Mathf.Max(0.5f, durationSeconds) * 1000.0 + VillageSpeechMinGapMsec;
		if (VillageSpeechUnlockMsec.TryGetValue(_homeVillageIndex, out double existing))
			until = Math.Max(until, prioritize ? until : Math.Max(existing, until));
		VillageSpeechUnlockMsec[_homeVillageIndex] = until;
	}

	private void SetDialogueEmotionState(DialogueEmotion emotion, bool forceText)
	{
		bool changed = _dialogueEmotion != emotion; _dialogueEmotion = emotion;
		if (emotion == DialogueEmotion.Chased) { _dialogueVisibleTimer = 0.0f; SetBubbleVisible(false); return; }
		if (changed || forceText || string.IsNullOrEmpty(_currentDialogueText)) SetDialogueForEmotion(emotion, show: changed);
	}
	private void SetDialogueForEmotion(DialogueEmotion emotion, bool show)
	{
		_currentDialogueText = PickDialogueLine(emotion);
		if (_dialogueLabel != null) _dialogueLabel.Text = _currentDialogueText;
		UpdateBubbleLayout();
		if (show)
		{
			// Keep village speech staggered/natural; only one bubble at a time per village for ambient changes.
			if (!CanReserveVillageSpeech(prioritize: false)) return;
			ReserveVillageSpeech(DialogueDisplayDuration, prioritize: false);
			SetBubbleVisible(true);
			_dialogueVisibleTimer = DialogueDisplayDuration;
		}
	}
	private void ShowDialogueForCurrentState(bool forceRefresh, bool autoHide, bool prioritize = false)
	{
		if (forceRefresh || string.IsNullOrEmpty(_currentDialogueText))
		{
			_currentDialogueText = PickDialogueLine(_dialogueEmotion);
			if (_dialogueLabel != null) _dialogueLabel.Text = _currentDialogueText;
			UpdateBubbleLayout();
		}
		if (!prioritize && !CanReserveVillageSpeech(prioritize: false)) return;
		ReserveVillageSpeech(DialogueDisplayDuration, prioritize);
		SetBubbleVisible(true);
		_dialogueVisibleTimer = DialogueDisplayDuration;
	}
	private string PickDialogueLine(DialogueEmotion emotion) { string[] options = GetOptionsForEmotion(emotion); if (options.Length == 0) return "..."; return options[(int)(GD.Randi() % (uint)options.Length)]; }
	private static string[] GetOptionsForEmotion(DialogueEmotion emotion) => emotion switch { DialogueEmotion.Chased => ChasedDialogueOptions, DialogueEmotion.Recovered => RecoveredDialogueOptions, _ => IdleDialogueOptions };
	private void UpdateTraderLabelLayout()
	{
		if (_traderLabelRoot == null || !IsInstanceValid(_traderLabelRoot))
			return;
		// Fit backdrop to text and keep centered above the villager.
		_traderLabelRoot.ResetSize();
		Vector2 size = _traderLabelRoot.GetCombinedMinimumSize();
		_traderLabelRoot.Size = size;
		_traderLabelRoot.Position = new Vector2(-size.X / 2.0f, -31.0f);
	}

	private void ConfigureSpeechBubble()
	{
		if (_dialogueLabel != null)
		{
			_dialogueLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
			_dialogueLabel.CustomMinimumSize = new Vector2(48, 0);
			_dialogueLabel.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
		}
		if (_bubblePanel != null) _bubblePanel.CustomMinimumSize = new Vector2(0, 0);
		SetBubbleVisible(false);
	}
	private void UpdateBubbleLayout()
	{
		if (_bubblePanel == null || _dialogueLabel == null) return;
		// Compact speech bubble popup sizing/layout (smaller than original, tuned for readability).
		float maxTextWidth = 140.0f;
		float targetWidth = Mathf.Clamp(32.0f + _currentDialogueText.Length * 3.0f, 56.0f, maxTextWidth);
		_dialogueLabel.CustomMinimumSize = new Vector2(targetWidth, 0);
		_bubblePanel.ResetSize();
		_dialogueLabel.ResetSize();
		Vector2 minSize = _bubblePanel.GetCombinedMinimumSize();
		minSize.X = Mathf.Clamp(minSize.X, 56.0f, maxTextWidth + 16.0f);
		_bubblePanel.Size = minSize;
		_bubblePanel.Position = new Vector2(-minSize.X / 2.0f, -minSize.Y);
	}
	private void SetBubbleVisible(bool visible) { if (_speechBubble != null) _speechBubble.Visible = visible; }
	private void UpdateFacing(Vector2 direction)
	{
		float ax = Mathf.Abs(direction.X); float ay = Mathf.Abs(direction.Y);
		if (ax > ay * 1.2f) _facing = direction.X < 0.0f ? "left" : "right"; else if (ay > ax * 1.2f) _facing = direction.Y < 0.0f ? "up" : "down";
	}
	private void PlayAnim(string anim) { if (_sprite.Animation != anim) _sprite.Play(anim); }
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

	private void UpdateTrustAndGifts(float delta)
	{
		_adviceCooldownTimer = Mathf.Max(0.0f, _adviceCooldownTimer - delta); _giftTimer -= delta; _ambientTalkTimer -= delta;
		if (IsTrader)
		{
			_hasPendingAdviceRequest = false;
			return;
		}
		bool calm = !IsFleeing && !_wasRecentlyChased && _dialogueEmotion != DialogueEmotion.Chased;
		if (!_hasPendingAdviceRequest && calm && _adviceCooldownTimer <= 0.0f)
		{
			_hasPendingAdviceRequest = true; _pendingAdviceQuestion = AdviceQuestions[(int)(GD.Randi() % (uint)AdviceQuestions.Length)];
			_adviceCooldownTimer = (float)GD.RandRange(12.0, 22.0);
		}
		UpdateAmbientDialogue(calm); TryGiftCoins();
	}
	private void UpdateAmbientDialogue(bool calm)
	{
		if (!calm || _ambientTalkTimer > 0.0f) return;
		if (_speechBubble != null && _speechBubble.Visible) { _ambientTalkTimer = (float)GD.RandRange(12.0, 24.0); return; }
		// Don't start ambient chatter if another villager in the same village is speaking.
		if (!CanReserveVillageSpeech(prioritize: false)) { _ambientTalkTimer = (float)GD.RandRange(4.0, 10.0); return; }
		string line = IdleDialogueOptions[(int)(GD.Randi() % (uint)IdleDialogueOptions.Length)];
		if (_hasPendingAdviceRequest && GD.Randf() < 0.45f) line = "Could I ask for your advice sometime?";
		else if (Trust >= 6 && GD.Randf() < 0.35f) line = Trust >= 9 ? "I really trust your judgment." : "I appreciate our talks.";
		float duration = (float)GD.RandRange(3.0, 4.5);
		ShowTemporaryDialogue(line, duration, prioritize: false);
		_ambientTalkTimer = (float)GD.RandRange(25.0, 55.0);
	}
	private void TryGiftCoins()
	{
		if (Trust < 4 || CoinScene == null || IsFleeing || _dialogueEmotion == DialogueEmotion.Chased) return;
		if (_giftTimer > 0.0f) return;
		Node2D player = GetTree().GetFirstNodeInGroup("player") as Node2D;
		if (player == null || !IsInstanceValid(player) || GlobalPosition.DistanceTo(player.GlobalPosition) > GiftRange) return;
		float trust01 = Mathf.Clamp(Trust / 10.0f, 0.0f, 1.0f); float chance = Mathf.Lerp(0.20f, 0.55f, trust01);
		if (GD.Randf() > chance) { _giftTimer = (float)GD.RandRange(10.0f, 18.0f); return; }
		int minCoins = Trust >= 9 ? 3 : Trust >= 7 ? 2 : 1; int maxCoins = Trust >= 10 ? 10 : Trust >= 8 ? 7 : Trust >= 6 ? 4 : 2;
		DropGiftCoins(GD.RandRange(minCoins, maxCoins), player.GlobalPosition); ShowGiftDialogue();
		_giftTimer = Mathf.Lerp(30.0f, 14.0f, trust01) + (float)GD.RandRange(0.0f, 6.0f); _adviceCooldownTimer = Mathf.Max(_adviceCooldownTimer, 6.0f);
	}
	private void DropGiftCoins(int count, Vector2 towardPosition)
	{
		Vector2 baseDir = towardPosition - GlobalPosition; if (baseDir.LengthSquared() < 0.001f) baseDir = Vector2.Down; baseDir = baseDir.Normalized();
		for (int i = 0; i < count; i++)
		{
			Node coinNode = CoinScene.Instantiate(); if (coinNode is not Node2D coin) { coinNode.QueueFree(); continue; }
			Vector2 dir = baseDir.Rotated((float)GD.RandRange(-0.9f, 0.9f)); float dist = (float)GD.RandRange(8.0f, 28.0f);
			coin.GlobalPosition = GlobalPosition + dir * dist + new Vector2((float)GD.RandRange(-4, 4), (float)GD.RandRange(-4, 4));
			GetParent()?.AddChild(coin);
		}
	}
	private void ShowGiftDialogue() { string[] pool = Trust >= 8 ? HighTrustGiftDialogue : TrustGiftDialogue; ShowTemporaryDialogue(pool[(int)(GD.Randi() % (uint)pool.Length)], 3.5f, prioritize: false); }
	private void ShowTemporaryDialogue(string text, float durationSeconds, bool prioritize = false)
	{
		if (string.IsNullOrWhiteSpace(text)) return;
		if (!prioritize && !CanReserveVillageSpeech(prioritize: false)) return;
		float duration = Mathf.Max(durationSeconds, 0.5f);
		_currentDialogueText = text;
		if (_dialogueLabel != null) _dialogueLabel.Text = _currentDialogueText;
		UpdateBubbleLayout();
		ReserveVillageSpeech(duration, prioritize);
		SetBubbleVisible(true);
		_dialogueVisibleTimer = duration;
		_trustDialogueActive = true;
	}

	public void EnsureAdviceRequestReady()
	{
		if (IsFleeing || _dialogueEmotion == DialogueEmotion.Chased) return;
		if (_hasPendingAdviceRequest && !string.IsNullOrWhiteSpace(_pendingAdviceQuestion)) return;
		if (_adviceCooldownTimer > 0.0f && GD.Randf() < 0.5f) _adviceCooldownTimer = 0.0f;
		if (_adviceCooldownTimer > 0.0f) return;
		_hasPendingAdviceRequest = true; _pendingAdviceQuestion = AdviceQuestions[(int)(GD.Randi() % (uint)AdviceQuestions.Length)];
	}
	public string ConsumeAdviceQuestion() { EnsureAdviceRequestReady(); if (!_hasPendingAdviceRequest) return ""; _hasPendingAdviceRequest = false; _adviceCooldownTimer = (float)GD.RandRange(10.0, 20.0); return _pendingAdviceQuestion ?? ""; }
	public string BuildAdviceEvaluationSystemPrompt(string adviceQuestion) => "You are a villager NPC in Grid Quest evaluating the player's response to your request for life advice. Be warm and in-character. Judge helpfulness, empathy, clarity, and relevance. Return ONLY valid JSON (no markdown) with keys: \"reply\" (string, 1-3 short sentences as the villager), \"score\" (integer 0-5), \"reason\" (short string). Advice question asked: " + adviceQuestion;
	public string BuildAdviceEvaluationPrompt(string adviceQuestion, string playerAdvice) => $"Villager context: trust={Trust}, mood={_dialogueEmotion}, fleeing={IsFleeing}, village={_homeVillageIndex}.\nAdvice question: {adviceQuestion}\nPlayer advice: {playerAdvice}\nRate how well the player communicated and helped.";
	public string ApplyAdviceEvaluation(string llmJsonOrText, string fallbackAdvice)
	{
		int score = EstimateAdviceScoreFallback(fallbackAdvice); string reply = "Thank you for sharing that with me.";
		if (!string.IsNullOrWhiteSpace(llmJsonOrText))
		{
			if (TryParseAdviceEvaluation(llmJsonOrText, out int parsedScore, out string parsedReply)) { score = parsedScore; if (!string.IsNullOrWhiteSpace(parsedReply)) reply = parsedReply.Trim(); }
			else { reply = llmJsonOrText.Trim(); if (reply.Length > 220) reply = reply.Substring(0, 220).TrimEnd() + "..."; }
		}
		score = Mathf.Clamp(score, 0, 5);
		// Trust revamp (0-10): one good life-help response grants full high trust (10), others adjust by +/-2.
		if (score >= 4)
			Trust = 10;
		else if (score >= 2)
			Trust = Mathf.Clamp(Trust + 2, 0, 10);
		else
			Trust = Mathf.Clamp(Trust - 2, 0, 10);
		if (Trust >= 8) _giftTimer = Mathf.Min(_giftTimer <= 0 ? 6.0f : _giftTimer, 6.0f);
		ShowTemporaryDialogue(reply, 4.5f, prioritize: true); return reply;
	}
	public void ApplyConversationTrust(string playerMessage, string villagerReply) { int delta = EstimateConversationTrustDelta(playerMessage, villagerReply); if (delta == 0) return; Trust = Mathf.Clamp(Trust + delta, 0, 10); }
	private static int EstimateConversationTrustDelta(string playerMessage, string villagerReply)
	{
		string p = (playerMessage ?? "").Trim().ToLowerInvariant(); string r = (villagerReply ?? "").Trim().ToLowerInvariant(); if (string.IsNullOrEmpty(p)) return 0;
		string[] rude = { "shut up", "stupid", "idiot", "hate", "useless", "dumb", "kill you", "annoying", "worthless", "loser", "ugly" };
		foreach (string w in rude) if (p.Contains(w)) return -2;
		string[] dismissive = { "whatever", "idc", "don't care", "dont care", "boring", "go away", "leave me alone" };
		foreach (string w in dismissive) if (p.Contains(w)) return -2;
		string[] positive = { "thank", "thanks", "please", "help", "sorry", "hope", "friend", "kind", "care", "appreciate", "good", "great", "love", "safe", "glad", "understand", "how are you", "are you ok", "you can", "we can", "together" };
		foreach (string w in positive) if (p.Contains(w)) return 2;
		if (p.Contains("?")) return 2;
		if (p.Length >= 12) return 2;
		if (p.Length <= 3) return 0;
		return 2; // normal interaction
	}
	private static bool TryParseAdviceEvaluation(string text, out int score, out string reply)
	{
		score = 0; reply = ""; if (string.IsNullOrWhiteSpace(text)) return false; string json = ExtractJsonObject(text); if (string.IsNullOrWhiteSpace(json)) return false;
		try { using JsonDocument doc = JsonDocument.Parse(json); JsonElement root = doc.RootElement; if (root.ValueKind != JsonValueKind.Object) return false;
			if (root.TryGetProperty("score", out JsonElement scoreEl)) { if (scoreEl.ValueKind == JsonValueKind.Number && scoreEl.TryGetInt32(out int i)) score = i; else if (scoreEl.ValueKind == JsonValueKind.String && int.TryParse(scoreEl.GetString(), out int si)) score = si; }
			if (root.TryGetProperty("reply", out JsonElement replyEl) && replyEl.ValueKind == JsonValueKind.String) reply = replyEl.GetString() ?? "";
			else if (root.TryGetProperty("villager_reply", out JsonElement vr) && vr.ValueKind == JsonValueKind.String) reply = vr.GetString() ?? ""; return true; } catch { return false; }
	}
	private static string ExtractJsonObject(string text) { int start = text.IndexOf('{'); int end = text.LastIndexOf('}'); if (start < 0 || end <= start) return ""; return text.Substring(start, end - start + 1); }
	private static int EstimateAdviceScoreFallback(string advice)
	{
		if (string.IsNullOrWhiteSpace(advice)) return 0; string t = advice.Trim().ToLowerInvariant(); int score = 1;
		if (t.Length >= 20) score++; if (t.Length >= 60) score++; if (t.Length >= 120) score++;
		if (t.Contains("you") || t.Contains("try") || t.Contains("could") || t.Contains("can") || t.Contains("maybe")) score++;
		if (t.Contains("sorry") || t.Contains("understand") || t.Contains("hope") || t.Contains("help") || t.Contains("step") || t.Contains("breathe") || t.Contains("talk")) score++;
		if (t.Contains("idk") || t.Contains("dunno") || t == "ok" || t == "no" || t == "whatever") score = 0;
		if (t.Contains("stupid") || t.Contains("idiot") || t.Contains("hate") || t.Contains("shut up")) score = 0; return Mathf.Clamp(score, 0, 5);
	}
	private void UpdateDialogueTimers(float delta)
	{
		if (_speechBubble == null || !_speechBubble.Visible) return; if (_dialogueEmotion == DialogueEmotion.Chased) return;
		_dialogueVisibleTimer -= delta; if (_dialogueVisibleTimer <= 0.0f)
		{
			_trustDialogueActive = false;
			if (_dialogueEmotion == DialogueEmotion.Recovered) SetDialogueForEmotion(DialogueEmotion.Recovered, show: false); else SetDialogueForEmotion(DialogueEmotion.Idle, show: false);
			SetBubbleVisible(false);
		}
	}

	/// <summary>LLM/tool-readable control state with villager dialogue, trust, and home context.</summary>
	public override Godot.Collections.Dictionary GetNpcControlState()
	{
		var state = base.GetNpcControlState();
		state["entity_kind"] = IsTrader ? "trader_npc" : "villager_npc";
		state["is_trader"] = IsTrader;
		state["dialogue"] = GetDialogueContext();
		state["chat_system_prompt_hint"] = BuildChatSystemPrompt();
		state["home_village_index"] = _homeVillageIndex;
		state["home_center_world"] = _homeCenterWorld;
		return state;
	}

	/// <summary>Tool-call convenience: move to world coordinates.</summary>
	public bool ToolGoTo(float x, float y, string reason = "") => GoToPosition(new Vector2(x, y), reason);
	/// <summary>Tool-call convenience: move to entity by name/group.</summary>
	public bool ToolGoToEntity(string entityId, string reason = "") => GoToEntity(entityId, reason);
	/// <summary>Tool-call convenience: set a state key/value in NPC memory.</summary>
	public void ToolRemember(string key, string value) => RememberState(key, value);
	/// <summary>Tool-call convenience: append chat history entry.</summary>
	public void ToolAppendChat(string role, string content) => AppendChatHistory(role, content);
}
