using Godot;
using GridQuest.Entities;

public partial class CheatMenu : Control
{
	private Entity _player;
	private CheckButton _invincibilityToggle;
	private Label _statusLabel;

	public override void _Ready()
	{
		AddToGroup("cheat_ui");
		Visible = false;
		ProcessMode = ProcessModeEnum.Always;
		MouseFilter = MouseFilterEnum.Stop;
		SetAnchorsPreset(LayoutPreset.FullRect);
		BuildUi();
		CallDeferred(nameof(ResolvePlayer));
	}

	private void BuildUi()
	{
		foreach (Node child in GetChildren()) child.QueueFree();
		var dim = new ColorRect { Color = new Color(0, 0, 0, 0.35f), MouseFilter = MouseFilterEnum.Ignore };
		dim.SetAnchorsPreset(LayoutPreset.FullRect);
		AddChild(dim);

		var panel = new PanelContainer { CustomMinimumSize = new Vector2(420, 400) };
		panel.SetAnchorsPreset(LayoutPreset.TopLeft);
		panel.OffsetLeft = 20; panel.OffsetTop = 20; panel.OffsetRight = 440; panel.OffsetBottom = 420;
		var style = new StyleBoxFlat { BgColor = new Color(0.10f, 0.10f, 0.13f, 0.96f) };
		style.SetCornerRadiusAll(8);
		style.SetContentMarginAll(10);
		panel.AddThemeStyleboxOverride("panel", style);
		AddChild(panel);

		var root = new VBoxContainer();
		root.SetAnchorsPreset(LayoutPreset.FullRect);
		root.OffsetLeft = 10; root.OffsetTop = 10; root.OffsetRight = -10; root.OffsetBottom = -10;
		root.AddThemeConstantOverride("separation", 8);
		panel.AddChild(root);
		root.AddChild(new Label { Text = "Debug / Cheat Menu (`)" });
		_invincibilityToggle = new CheckButton { Text = "Invincibility" };
		_invincibilityToggle.Toggled += OnInvincibilityToggled;
		root.AddChild(_invincibilityToggle);

		var coins = new HBoxContainer();
		var add10 = new Button { Text = "+10 Coins" };
		add10.Pressed += () => AddCoins(10);
		var add100 = new Button { Text = "+100 Coins" };
		add100.Pressed += () => AddCoins(100);
		coins.AddChild(add10); coins.AddChild(add100);
		root.AddChild(new Label { Text = "Add Coins:" });
		root.AddChild(coins);

		root.AddChild(new Label { Text = "Add Weapon:" });
		var weapons = new HBoxContainer();
		foreach (string id in new[] { "base_sword", "fire_staff", "scythe", "crossbow" })
		{
			var button = new Button { Text = id };
			button.Pressed += () => AddWeapon(id);
			weapons.AddChild(button);
		}
		root.AddChild(weapons);

		var spawn = new Button { Text = "Spawn AI Entity" };
		spawn.Pressed += SpawnEntity;
		root.AddChild(spawn);

		var timeRow = new HBoxContainer();
		foreach (string time in new[] { "sunrise", "midday", "sunset", "midnight" })
		{
			var button = new Button { Text = time };
			button.Pressed += () => SetTimeOfDay(time);
			timeRow.AddChild(button);
		}
		root.AddChild(new Label { Text = "Set Time of Day:" });
		root.AddChild(timeRow);

		_statusLabel = new Label { Text = "Press ` to close.", AutowrapMode = TextServer.AutowrapMode.Word };
		root.AddChild(_statusLabel);
	}

	private void ResolvePlayer()
	{
		if (_player == null || !IsInstanceValid(_player))
			_player = GetTree().GetFirstNodeInGroup("controlled_entity") as Entity ?? GetTree().GetFirstNodeInGroup("player") as Entity;
		if (_player != null && _invincibilityToggle != null)
			_invincibilityToggle.ButtonPressed = _player.DebugInvulnerable;
	}

	public override void _Input(InputEvent @event)
	{
		var chat = GetTree().GetFirstNodeInGroup("villager_chat_ui") as Control;
		if (chat != null && chat.Visible) return;
		if (@event is InputEventKey key && key.Pressed && !key.Echo && key.Keycode == Key.Quoteleft)
		{
			Visible = !Visible;
			ResolvePlayer();
			GetViewport().SetInputAsHandled();
			return;
		}
		if (Visible && (@event.IsActionPressed("ui_cancel") || @event.IsActionPressed("pause_menu")))
		{
			Visible = false;
			GetViewport().SetInputAsHandled();
		}
	}

	private void OnInvincibilityToggled(bool enabled)
	{
		ResolvePlayer();
		if (_player != null) _player.DebugInvulnerable = enabled;
	}

	private void AddCoins(int amount)
	{
		ResolvePlayer();
		if (_player == null) return;
		_player.AddGold(amount);
		_statusLabel.Text = $"Added {amount} coin(s). Total: {_player.Gold}";
	}

	private void AddWeapon(string itemId)
	{
		ResolvePlayer();
		if (_player == null) return;
		bool added = _player.AddItem(itemId);
		_statusLabel.Text = added ? $"Added {itemId}." : "Inventory is full.";
	}

	private void SpawnEntity()
	{
		ResolvePlayer();
		Vector2 pos = _player?.GlobalPosition ?? new Vector2(160, 160);
		var scene = GD.Load<PackedScene>("res://entities/entity.tscn");
		if (scene == null)
		{
			GD.PushError("Could not load the entity scene.");
			return;
		}
		var entity = scene.Instantiate<Entity>();
		entity.Name = "SpawnedEntity";
		entity.AiControlled = true;
		entity.GlobalPosition = pos + Vector2.Right * 40.0f;
		GetTree().CurrentScene?.AddChild(entity);
	}

	private void SetTimeOfDay(string timeName)
	{
		var cycle = GetTree().GetFirstNodeInGroup("day_night_cycle") as DayNightCycle;
		if (cycle == null) { _statusLabel.Text = "Day/night cycle not found."; return; }
		switch (timeName)
		{
			case "sunrise": cycle.SetToSunrise(); break;
			case "midday": cycle.SetToMidday(); break;
			case "sunset": cycle.SetToSunset(); break;
			case "midnight": cycle.SetToMidnight(); break;
			default: _statusLabel.Text = "Unknown time setting."; return;
		}
		_statusLabel.Text = $"Set time of day to {timeName}.";
	}
}
