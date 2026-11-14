using Godot;
using Godot.Collections;

public partial class Villager : CharacterBody2D
{
	[Signal] public delegate void DiedEventHandler(Villager villager, int villageIndex);

	public enum DialogueEmotion
	{
		Idle,
		Chased,
		Recovered
	}

	[Export] public float Speed { get; set; } = 90.0f;
	[Export] public float PatrolSpeed { get; set; } = 25.0f;
	[Export] public int MaxHealth { get; set; } = 3;
	[Export] public float FleeDetectionRange { get; set; } = 140.0f;
	[Export] public float InteractRange { get; set; } = 42.0f;
	[Export] public float RecoveredStateDuration { get; set; } = 5.0f;
	[Export] public float DialogueDisplayDuration { get; set; } = 4.0f;

	private static readonly string[] IdleDialogueOptions =
	{
		"Lovely day in the village.",
		"Have you seen anything strange nearby?",
		"I should check on the others soon."
	};

	private static readonly string[] ChasedDialogueOptions =
	{
		"Help! Something's after me!",
		"Run! There's a monster!",
		"Please, keep it away from me!"
	};

	private static readonly string[] RecoveredDialogueOptions =
	{
		"Phew... I think I'm safe now.",
		"That was too close.",
		"I need a moment to catch my breath."
	};

	private AnimatedSprite2D _sprite;
	private ProgressBar _healthBar;
	private Node2D _speechBubble;
	private PanelContainer _bubblePanel;
	private Label _dialogueLabel;
	private State _state = State.Idle;
	private string _facing = "down";
	private Vector2 _patrolTarget;
	private float _patrolWaitTimer;
	private float _patrolRepathTimer;
	private float _fleeFacingTimer;

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
	private bool _fleeDialogueLocked;
	private string _fleeDialogueText = "";

	private enum State
	{
		Idle,
		Patrol,
		Flee
	}

	public int Health { get; private set; }
	public DialogueEmotion CurrentDialogueEmotion => _dialogueEmotion;
	public bool IsFleeing => _state == State.Flee;
	public string CurrentDialogueText => _currentDialogueText;

	public override void _Ready()
	{
		MotionMode = MotionModeEnum.Floating;
		Health = MaxHealth;
		_sprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
		_healthBar = GetNodeOrNull<ProgressBar>("HealthBar");
		_speechBubble = GetNodeOrNull<Node2D>("SpeechBubble");
		_bubblePanel = GetNodeOrNull<PanelContainer>("SpeechBubble/BubblePanel");
		_dialogueLabel = GetNodeOrNull<Label>("SpeechBubble/BubblePanel/MarginContainer/DialogueLabel");

		_sprite.SpriteFrames = BuildSpriteFrames();
		_sprite.Play("idle_down");

		AddToGroup("villagers");
		AddToGroup("civilians");
		AddToGroup("villager_npcs");
		PickNewPatrolTarget();
		UpdateHealthBar();
		ConfigureSpeechBubble();
		SetDialogueForEmotion(DialogueEmotion.Idle, false);
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (!IsInteractionInput(@event))
			return;

		Node2D player = GetTree().GetFirstNodeInGroup("player") as Node2D;
		if (player == null)
			return;

		if (GlobalPosition.DistanceTo(player.GlobalPosition) > InteractRange)
			return;

		if (!IsClosestInteractableVillager(player))
			return;

		ShowDialogueForCurrentState(forceRefresh: true, autoHide: true);
		GetViewport().SetInputAsHandled();
	}

	public void SetHome(Rect2I cellBounds, Vector2I centerCell, int villageIndex)
	{
		const int cellSize = 16;
		_homeWorldRect = new Rect2(
			cellBounds.Position.X * cellSize,
			cellBounds.Position.Y * cellSize,
			cellBounds.Size.X * cellSize,
			cellBounds.Size.Y * cellSize);
		_homeCenterWorld = new Vector2(centerCell.X * cellSize + cellSize / 2.0f, centerCell.Y * cellSize + cellSize / 2.0f);
		_homeVillageIndex = villageIndex;
		_hasHome = true;
		_patrolTarget = _homeCenterWorld;
	}

	public Dictionary GetDialogueContext()
	{
		Node2D player = GetTree().GetFirstNodeInGroup("player") as Node2D;
		var options = new Array<string>();
		foreach (string line in GetOptionsForEmotion(_dialogueEmotion))
		{
			options.Add(line);
		}

		return new Dictionary
		{
			["state"] = _state.ToString(),
			["emotion"] = _dialogueEmotion.ToString(),
			["is_fleeing"] = IsFleeing,
			["was_recently_chased"] = _wasRecentlyChased,
			["recovery_seconds_remaining"] = Mathf.Max(0.0f, _recoveryTimer),
			["has_home"] = _hasHome,
			["village_index"] = _homeVillageIndex,
			["health"] = Health,
			["max_health"] = MaxHealth,
			["threat_nearby"] = _lastThreat != null && IsInstanceValid(_lastThreat),
			["threat_distance"] = _lastThreat != null && IsInstanceValid(_lastThreat) ? _lastThreatDistance : -1.0f,
			["player_nearby"] = player != null && GlobalPosition.DistanceTo(player.GlobalPosition) <= InteractRange,
			["current_dialogue"] = _currentDialogueText,
			["placeholder_dialogue_options"] = options,
			["dialogue_visible"] = _speechBubble != null && _speechBubble.Visible
		};
	}

	private static bool IsInteractionInput(InputEvent @event)
	{
		if (@event is InputEventKey keyEvent && keyEvent.Pressed && !keyEvent.Echo && keyEvent.Keycode == Key.F)
			return true;
		return false;
	}

	private bool IsClosestInteractableVillager(Node2D player)
	{
		float myDist = GlobalPosition.DistanceTo(player.GlobalPosition);
		foreach (Node node in GetTree().GetNodesInGroup("villager_npcs"))
		{
			if (node == this || node is not Villager other || !IsInstanceValid(other))
				continue;
			float otherDist = other.GlobalPosition.DistanceTo(player.GlobalPosition);
			if (otherDist <= other.InteractRange && otherDist < myDist)
				return false;
		}
		return true;
	}

	private SpriteFrames BuildSpriteFrames()
	{
		SpriteFrames frames = new SpriteFrames();
		string basePath = "res://assets/images/characters/villagers/";

		frames.AddAnimation("idle_down");
		frames.AddAnimation("idle_left");
		frames.AddAnimation("idle_right");
		frames.AddAnimation("idle_up");
		frames.AddAnimation("walk_down");
		frames.AddAnimation("walk_left");
		frames.AddAnimation("walk_right");
		frames.AddAnimation("walk_up");

		frames.SetAnimationLoop("idle_down", true);
		frames.SetAnimationLoop("idle_left", true);
		frames.SetAnimationLoop("idle_right", true);
		frames.SetAnimationLoop("idle_up", true);
		frames.SetAnimationLoop("walk_down", true);
		frames.SetAnimationLoop("walk_left", true);
		frames.SetAnimationLoop("walk_right", true);
		frames.SetAnimationLoop("walk_up", true);
		frames.SetAnimationSpeed("idle_down", 4.0);
		frames.SetAnimationSpeed("idle_left", 4.0);
		frames.SetAnimationSpeed("idle_right", 4.0);
		frames.SetAnimationSpeed("idle_up", 4.0);
		frames.SetAnimationSpeed("walk_down", 8.0);
		frames.SetAnimationSpeed("walk_left", 8.0);
		frames.SetAnimationSpeed("walk_right", 8.0);
		frames.SetAnimationSpeed("walk_up", 8.0);

		AddFrames(frames, "walk_down", basePath + "walkdown1-crop-folk-dogwalker.png", basePath + "walkdown2-crop-folk-dogwalker.png", basePath + "walkdown3-crop-folk-dogwalker.png", basePath + "walkdown4-crop-folk-dogwalker.png");
		AddFrames(frames, "walk_left", basePath + "walkleft1-crop-folk-dogwalker.png", basePath + "walkleft2-crop-folk-dogwalker.png", basePath + "walkleft3-crop-folk-dogwalker.png", basePath + "walkleft4-crop-folk-dogwalker.png");
		AddFrames(frames, "walk_right", basePath + "walkright1-crop-folk-dogwalker.png", basePath + "walkright2-crop-folk-dogwalker.png", basePath + "walkright3-crop-folk-dogwalker.png", basePath + "walkright4-crop-folk-dogwalker.png");
		AddFrames(frames, "walk_up", basePath + "walkup1-crop-folk-dogwalker.png", basePath + "walkup2-crop-folk-dogwalker.png", basePath + "walkup3-crop-folk-dogwalker.png", basePath + "walkup4-crop-folk-dogwalker.png");

		frames.AddFrame("idle_down", frames.GetFrameTexture("walk_down", 0));
		frames.AddFrame("idle_left", frames.GetFrameTexture("walk_left", 0));
		frames.AddFrame("idle_right", frames.GetFrameTexture("walk_right", 0));
		frames.AddFrame("idle_up", frames.GetFrameTexture("walk_up", 0));

		return frames;
	}

	private static void AddFrames(SpriteFrames frames, string anim, params string[] paths)
	{
		foreach (string p in paths)
		{
			Texture2D tex = GD.Load<Texture2D>(p);
			frames.AddFrame(anim, tex);
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		float dt = (float)delta;
		_fleeFacingTimer -= dt;
		UpdateDialogueTimers(dt);

		if (Health <= 0)
		{
			Velocity = Vector2.Zero;
			MoveAndSlide();
			return;
		}

		Node2D threat = FindNearestThreat();
		if (threat != null)
		{
			_lastThreat = threat;
			_lastThreatDistance = GlobalPosition.DistanceTo(threat.GlobalPosition);
			if (!_wasRecentlyChased && !_fleeDialogueLocked)
			{
				// Start a new flee episode with one dialogue line for the whole flee.
				_fleeDialogueLocked = true;
				_fleeDialogueText = PickDialogueLine(DialogueEmotion.Chased);
			}
			_wasRecentlyChased = true;
			_recoveryTimer = 0.0f;
			SetDialogueEmotionState(DialogueEmotion.Chased, forceText: _dialogueEmotion != DialogueEmotion.Chased);
			FleeFrom(threat.GlobalPosition);
			MoveAndSlide();
			return;
		}

		_lastThreat = null;
		_lastThreatDistance = float.MaxValue;

		if (_wasRecentlyChased)
		{
			_recoveryTimer += dt;
			if (_recoveryTimer < RecoveredStateDuration)
			{
				SetDialogueEmotionState(DialogueEmotion.Recovered, forceText: _dialogueEmotion != DialogueEmotion.Recovered);
			}
			else
			{
				_wasRecentlyChased = false;
				_recoveryTimer = 0.0f;
				_fleeDialogueLocked = false;
				_fleeDialogueText = "";
				SetDialogueEmotionState(DialogueEmotion.Idle, forceText: _dialogueEmotion != DialogueEmotion.Idle);
			}
		}
		else
		{
			_fleeDialogueLocked = false;
			_fleeDialogueText = "";
			SetDialogueEmotionState(DialogueEmotion.Idle, forceText: false);
		}

		UpdatePatrol(dt);
		MoveAndSlide();
	}

	public void TakeDamage(int amount)
	{
		if (Health <= 0 || amount <= 0)
		{
			return;
		}

		Health = Mathf.Max(0, Health - amount);
		UpdateHealthBar();
		if (Health == 0)
		{
			EmitSignal(SignalName.Died, this, _homeVillageIndex);
			QueueFree();
		}
	}

	private void FleeFrom(Vector2 threatPosition)
	{
		_state = State.Flee;
		_patrolWaitTimer = 0.0f;
		Vector2 away = GlobalPosition - threatPosition;
		if (away.LengthSquared() < 0.001f)
		{
			away = Vector2.Down;
		}

		Vector2 direction = away.Normalized();
		if (_fleeFacingTimer <= 0.0f)
		{
			UpdateFacing(direction);
			_fleeFacingTimer = 0.15f;
		}
		Velocity = direction * Speed;
		PlayAnim($"walk_{_facing}");
	}

	private void UpdatePatrol(float delta)
	{
		_patrolRepathTimer -= delta;

		if (_hasHome && IsFarOutsideHome())
		{
			MoveTowardPoint(_homeCenterWorld, PatrolSpeed * 1.15f);
			return;
		}

		if (_patrolWaitTimer > 0.0f)
		{
			_patrolWaitTimer -= delta;
			_state = State.Idle;
			Velocity = Vector2.Zero;
			PlayAnim($"idle_{_facing}");
			if (_patrolWaitTimer <= 0.0f)
			{
				PickNewPatrolTarget();
			}
			return;
		}

		Vector2 toTarget = _patrolTarget - GlobalPosition;
		if (toTarget.Length() <= 6.0f || _patrolRepathTimer <= 0.0f)
		{
			_patrolWaitTimer = (float)GD.RandRange(0.8, 2.2);
			_state = State.Idle;
			Velocity = Vector2.Zero;
			PlayAnim($"idle_{_facing}");
			return;
		}

		MoveTowardPoint(_patrolTarget, PatrolSpeed);
	}

	private void MoveTowardPoint(Vector2 target, float speed)
	{
		Vector2 toTarget = target - GlobalPosition;
		if (toTarget.LengthSquared() < 0.001f)
		{
			Velocity = Vector2.Zero;
			_state = State.Idle;
			PlayAnim($"idle_{_facing}");
			return;
		}

		Vector2 direction = toTarget.Normalized();
		UpdateFacing(direction);
		_state = State.Patrol;
		Velocity = direction * speed;
		PlayAnim($"walk_{_facing}");
	}

	private bool IsFarOutsideHome()
	{
		if (!_hasHome)
			return false;
		Rect2 expanded = _homeWorldRect.Grow(24.0f);
		return !expanded.HasPoint(GlobalPosition);
	}

	private void PickNewPatrolTarget()
	{
		_patrolRepathTimer = (float)GD.RandRange(2.5, 5.0);
		Vector2 origin = GlobalPosition;

		if (_hasHome)
		{
			Rect2 area = _homeWorldRect.Grow(-8.0f);
			if (area.Size.X < 16.0f || area.Size.Y < 16.0f)
				area = _homeWorldRect;

			for (int i = 0; i < 10; i++)
			{
				float x = (float)GD.RandRange(area.Position.X, area.End.X);
				float y = (float)GD.RandRange(area.Position.Y, area.End.Y);
				Vector2 candidate = new(x, y);
				if (candidate.DistanceTo(origin) >= 16.0f)
				{
					_patrolTarget = candidate;
					return;
				}
			}

			_patrolTarget = _homeCenterWorld;
			return;
		}

		Node2D map = GetTree().CurrentScene as Node2D;
		Node grid = map?.GetNodeOrNull<Node>("MapGrid") ?? map?.GetNodeOrNull<Node>("Map");
		if (grid != null && grid.HasMethod("CellToWorldCenter"))
		{
			int width = grid.Get("Width").AsInt32();
			if (width <= 0 && grid.Get("MapWidth").VariantType != Variant.Type.Nil)
				width = grid.Get("MapWidth").AsInt32();
			int height = grid.Get("Height").AsInt32();
			if (height <= 0 && grid.Get("MapHeight").VariantType != Variant.Type.Nil)
				height = grid.Get("MapHeight").AsInt32();
			if (width > 0 && height > 0)
			{
				for (int i = 0; i < 8; i++)
				{
					int x = GD.RandRange(0, width - 1);
					int y = GD.RandRange(0, height - 1);
					Vector2 candidate = (Vector2)grid.Call("CellToWorldCenter", x, y);
					if (candidate.DistanceTo(origin) >= 24.0f)
					{
						_patrolTarget = candidate;
						return;
					}
				}
			}
		}

		float angle = (float)GD.RandRange(0.0, Mathf.Tau);
		float dist = (float)GD.RandRange(24.0, 80.0);
		_patrolTarget = origin + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * dist;
	}

	private Node2D FindNearestThreat()
	{
		Node2D closest = null;
		float closestDistSq = FleeDetectionRange * FleeDetectionRange;
		Vector2 origin = GlobalPosition;

		foreach (Node node in GetTree().GetNodesInGroup("enemies"))
		{
			if (node is not Node2D enemy || !IsInstanceValid(enemy))
			{
				continue;
			}

			float distSq = origin.DistanceSquaredTo(enemy.GlobalPosition);
			if (distSq < closestDistSq)
			{
				closestDistSq = distSq;
				closest = enemy;
			}
		}

		return closest;
	}

	private void SetDialogueEmotionState(DialogueEmotion emotion, bool forceText)
	{
		bool changed = _dialogueEmotion != emotion;
		_dialogueEmotion = emotion;

		if (emotion == DialogueEmotion.Chased)
		{
			// Keep one chased line for the entire flee episode.
			string desired = _fleeDialogueLocked && !string.IsNullOrEmpty(_fleeDialogueText)
				? _fleeDialogueText
				: _currentDialogueText;
			if (string.IsNullOrEmpty(desired))
			{
				desired = PickDialogueLine(DialogueEmotion.Chased);
				_fleeDialogueLocked = true;
				_fleeDialogueText = desired;
			}

			if (changed || _currentDialogueText != desired)
			{
				_currentDialogueText = desired;
				if (_dialogueLabel != null)
					_dialogueLabel.Text = _currentDialogueText;
				UpdateBubbleLayout();
			}

			_dialogueVisibleTimer = 9999.0f;
			SetBubbleVisible(true);
			return;
		}

		if (changed || forceText || string.IsNullOrEmpty(_currentDialogueText))
		{
			SetDialogueForEmotion(emotion, show: changed);
		}
	}

	private void SetDialogueForEmotion(DialogueEmotion emotion, bool show)
	{
		_currentDialogueText = PickDialogueLine(emotion);
		if (_dialogueLabel != null)
		{
			_dialogueLabel.Text = _currentDialogueText;
		}
		UpdateBubbleLayout();

		if (show)
		{
			SetBubbleVisible(true);
			_dialogueVisibleTimer = emotion == DialogueEmotion.Chased ? 9999.0f : DialogueDisplayDuration;
		}
	}

	private void ShowDialogueForCurrentState(bool forceRefresh, bool autoHide)
	{
		if (forceRefresh || string.IsNullOrEmpty(_currentDialogueText))
		{
			_currentDialogueText = PickDialogueLine(_dialogueEmotion);
			if (_dialogueLabel != null)
				_dialogueLabel.Text = _currentDialogueText;
			UpdateBubbleLayout();
		}

		SetBubbleVisible(true);
		_dialogueVisibleTimer = _dialogueEmotion == DialogueEmotion.Chased ? 9999.0f : DialogueDisplayDuration;
		if (!autoHide && _dialogueEmotion != DialogueEmotion.Chased)
		{
			_dialogueVisibleTimer = DialogueDisplayDuration;
		}
	}

	private void UpdateDialogueTimers(float delta)
	{
		if (_speechBubble == null || !_speechBubble.Visible)
			return;
		if (_dialogueEmotion == DialogueEmotion.Chased)
			return;

		_dialogueVisibleTimer -= delta;
		if (_dialogueVisibleTimer <= 0.0f)
		{
			SetBubbleVisible(false);
		}
	}

	private string PickDialogueLine(DialogueEmotion emotion)
	{
		string[] options = GetOptionsForEmotion(emotion);
		if (options.Length == 0)
			return "...";
		int idx = (int)(GD.Randi() % (uint)options.Length);
		return options[idx];
	}

	private static string[] GetOptionsForEmotion(DialogueEmotion emotion)
	{
		return emotion switch
		{
			DialogueEmotion.Chased => ChasedDialogueOptions,
			DialogueEmotion.Recovered => RecoveredDialogueOptions,
			_ => IdleDialogueOptions
		};
	}

	private void ConfigureSpeechBubble()
	{
		if (_dialogueLabel != null)
		{
			_dialogueLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
			_dialogueLabel.CustomMinimumSize = new Vector2(60, 0);
			_dialogueLabel.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
		}
		if (_bubblePanel != null)
		{
			_bubblePanel.CustomMinimumSize = new Vector2(0, 0);
		}
		SetBubbleVisible(false);
	}

	private void UpdateBubbleLayout()
	{
		if (_bubblePanel == null || _dialogueLabel == null)
			return;

		float maxTextWidth = 180.0f;
		float targetWidth = Mathf.Clamp(40.0f + _currentDialogueText.Length * 4.0f, 72.0f, maxTextWidth);
		_dialogueLabel.CustomMinimumSize = new Vector2(targetWidth, 0);
		_bubblePanel.ResetSize();
		_dialogueLabel.ResetSize();

		Vector2 minSize = _bubblePanel.GetCombinedMinimumSize();
		minSize.X = Mathf.Clamp(minSize.X, 72.0f, maxTextWidth + 20.0f);
		_bubblePanel.Size = minSize;
		_bubblePanel.Position = new Vector2(-minSize.X / 2.0f, -minSize.Y);
	}

	private void SetBubbleVisible(bool visible)
	{
		if (_speechBubble != null)
		{
			_speechBubble.Visible = visible;
		}
	}

	private void UpdateFacing(Vector2 direction)
	{
		float ax = Mathf.Abs(direction.X);
		float ay = Mathf.Abs(direction.Y);
		if (ax > ay * 1.2f)
		{
			_facing = direction.X < 0.0f ? "left" : "right";
		}
		else if (ay > ax * 1.2f)
		{
			_facing = direction.Y < 0.0f ? "up" : "down";
		}
	}

	private void PlayAnim(string anim)
	{
		if (_sprite.Animation != anim)
		{
			_sprite.Play(anim);
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
}
