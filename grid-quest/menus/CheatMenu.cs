using Godot;

public partial class CheatMenu : Control
{
	private Player _player;
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
		foreach (Node child in GetChildren())
			child.QueueFree();

		var dim = new ColorRect { Color = new Color(0, 0, 0, 0.35f) };
		dim.SetAnchorsPreset(LayoutPreset.FullRect);
		dim.MouseFilter = MouseFilterEnum.Ignore;
		AddChild(dim);

		var panel = new PanelContainer { CustomMinimumSize = new Vector2(360, 300) };
		panel.SetAnchorsPreset(LayoutPreset.TopLeft);
		panel.OffsetLeft = 20;
		panel.OffsetTop = 20;
		panel.OffsetRight = 380;
		panel.OffsetBottom = 320;
		var sb = new StyleBoxFlat { BgColor = new Color(0.10f, 0.10f, 0.13f, 0.96f) };
		sb.SetCornerRadiusAll(8);
		sb.SetContentMarginAll(10);
		panel.AddThemeStyleboxOverride("panel", sb);
		AddChild(panel);

		var root = new VBoxContainer();
		root.SetAnchorsPreset(LayoutPreset.FullRect);
		root.OffsetLeft = 10; root.OffsetTop = 10; root.OffsetRight = -10; root.OffsetBottom = -10;
		root.AddThemeConstantOverride("separation", 8);
		panel.AddChild(root);

		var title = new Label { Text = "Debug / Cheat Menu (`)" };
		title.AddThemeFontSizeOverride("font_size", 18);
		root.AddChild(title);

		_invincibilityToggle = new CheckButton { Text = "Invincibility" };
		_invincibilityToggle.Toggled += OnInvincibilityToggled;
		root.AddChild(_invincibilityToggle);

		var coinsRow = new HBoxContainer();
		coinsRow.AddThemeConstantOverride("separation", 6);
		var add10 = new Button { Text = "+10 Coins" };
		add10.Pressed += () => AddCoins(10);
		var add100 = new Button { Text = "+100 Coins" };
		add100.Pressed += () => AddCoins(100);
		coinsRow.AddChild(add10);
		coinsRow.AddChild(add100);
		root.AddChild(new Label { Text = "Add Coins:" });
		root.AddChild(coinsRow);

		root.AddChild(new Label { Text = "Add Weapon to Inventory:" });
		var weaponRow = new HBoxContainer();
		weaponRow.AddThemeConstantOverride("separation", 6);
		var sword = new Button { Text = "Sword" };
		sword.Pressed += () => AddWeapon("base_sword");
		var staff = new Button { Text = "Fire Staff" };
		staff.Pressed += () => AddWeapon("fire_staff");
		weaponRow.AddChild(sword);
		weaponRow.AddChild(staff);
		root.AddChild(weaponRow);

		root.AddChild(new Label { Text = "Villager Trust (debug):" });
		var trustRow = new HBoxContainer();
		trustRow.AddThemeConstantOverride("separation", 6);
		var trustNear = new Button { Text = "+25 Nearest" };
		trustNear.Pressed += () => AdjustNearestVillagerTrust(25);
		var trustAll = new Button { Text = "+25 All Nearby" };
		trustAll.Pressed += () => AdjustNearbyVillagersTrust(25, 180.0f);
		trustRow.AddChild(trustNear);
		trustRow.AddChild(trustAll);
		root.AddChild(trustRow);

		_statusLabel = new Label { Text = "Press ` to close." };
		_statusLabel.AutowrapMode = TextServer.AutowrapMode.Word;
		root.AddChild(_statusLabel);
	}

	private void ResolvePlayer()
	{
		if (_player == null || !IsInstanceValid(_player))
			_player = GetTree().GetFirstNodeInGroup("player") as Player;
		SyncFromPlayer();
	}

	public override void _Input(InputEvent @event)
	{
		var villagerChat = GetTree().GetFirstNodeInGroup("villager_chat_ui") as Control;
		if (villagerChat != null && villagerChat.Visible)
			return;
		if (@event is InputEventKey key && key.Pressed && !key.Echo &&
			(key.Keycode == Key.Quoteleft || key.PhysicalKeycode == Key.Quoteleft))
		{
			ToggleMenu();
			GetViewport().SetInputAsHandled();
			return;
		}

		if (!Visible) return;

		bool closePressed = @event.IsActionPressed("ui_cancel") || @event.IsActionPressed("pause_menu");
		if (!closePressed && @event is InputEventKey esc && esc.Pressed && !esc.Echo && esc.Keycode == Key.Escape)
			closePressed = true;
		if (closePressed)
		{
			Visible = false;
			GetViewport().SetInputAsHandled();
		}
	}

	private void ToggleMenu()
	{
		Visible = !Visible;
		if (Visible)
		{
			ResolvePlayer();
			_statusLabel.Text = "Cheats enabled. Press ` or Esc/B to close.";
			_invincibilityToggle?.GrabFocus();
		}
	}

	private void SyncFromPlayer()
	{
		if (_player != null && IsInstanceValid(_player) && _invincibilityToggle != null)
		{
			_invincibilityToggle.ButtonPressed = _player.DebugInvulnerable;
		}
	}

	private void OnInvincibilityToggled(bool enabled)
	{
		ResolvePlayer();
		if (_player == null) return;
		_player.DebugInvulnerable = enabled;
		_statusLabel.Text = enabled ? "Invincibility: ON" : "Invincibility: OFF";
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
		bool added = _player.AddItem(itemId, 1);
		_statusLabel.Text = added ? $"Added weapon: {itemId}" : $"Could not add {itemId} (inventory full?)";
	}

	private void AdjustNearestVillagerTrust(int delta)
	{
		ResolvePlayer();
		if (_player == null) return;
		Villager nearest = null;
		float best = float.MaxValue;
		foreach (Node node in GetTree().GetNodesInGroup("villager_npcs"))
		{
			if (node is not Villager villager || !IsInstanceValid(villager))
				continue;
			float d = _player.GlobalPosition.DistanceTo(villager.GlobalPosition);
			if (d < best)
			{
				best = d;
				nearest = villager;
			}
		}
		if (nearest == null)
		{
			_statusLabel.Text = "No villager found.";
			return;
		}
		nearest.DebugAdjustTrust(delta);
		_statusLabel.Text = $"Nearest villager trust: {nearest.Trust}/100";
	}

	private void AdjustNearbyVillagersTrust(int delta, float radius)
	{
		ResolvePlayer();
		if (_player == null) return;
		int count = 0;
		foreach (Node node in GetTree().GetNodesInGroup("villager_npcs"))
		{
			if (node is not Villager villager || !IsInstanceValid(villager))
				continue;
			if (_player.GlobalPosition.DistanceTo(villager.GlobalPosition) > radius)
				continue;
			villager.DebugAdjustTrust(delta);
			count++;
		}
		_statusLabel.Text = count > 0 ? $"Raised trust for {count} nearby villager(s)." : "No nearby villagers.";
	}
}
