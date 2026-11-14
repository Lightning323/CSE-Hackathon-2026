using Godot;
using GridQuest.PlayerUpgrades;
using System;
using System.Collections.Generic;

public partial class SkillsUi : Control
{
	private Player _player;
	private Label _coinsLabel;
	private VBoxContainer _list;
	private Label _statusLabel;
	private Label _progressLabel;
	private Button _resetWorldButton;

	private readonly List<string> _currentOfferIds = new();
	private readonly RandomNumberGenerator _rng = new();

	public override void _Ready()
	{
		AddToGroup("skills_ui");
		Visible = false;
		ProcessMode = ProcessModeEnum.Always;
		MouseFilter = MouseFilterEnum.Stop;
		SetAnchorsPreset(LayoutPreset.FullRect);
		_rng.Randomize();
		BuildUi();
		CallDeferred(nameof(ResolvePlayer));
	}

	private void BuildUi()
	{
		foreach (Node child in GetChildren())
			child.QueueFree();

		var dim = new ColorRect { Color = new Color(0, 0, 0, 0.5f) };
		dim.SetAnchorsPreset(LayoutPreset.FullRect);
		dim.MouseFilter = MouseFilterEnum.Stop;
		AddChild(dim);

		var panel = new PanelContainer { CustomMinimumSize = new Vector2(720, 520) };
		panel.SetAnchorsPreset(LayoutPreset.Center);
		panel.GrowHorizontal = GrowDirection.Both;
		panel.GrowVertical = GrowDirection.Both;
		var sb = new StyleBoxFlat { BgColor = new Color(0.12f, 0.12f, 0.16f, 0.97f) };
		sb.SetCornerRadiusAll(8);
		sb.SetContentMarginAll(10);
		panel.AddThemeStyleboxOverride("panel", sb);
		AddChild(panel);

		var root = new VBoxContainer();
		root.SetAnchorsPreset(LayoutPreset.FullRect);
		root.OffsetLeft = 10; root.OffsetTop = 10; root.OffsetRight = -10; root.OffsetBottom = -10;
		root.AddThemeConstantOverride("separation", 8);
		panel.AddChild(root);

		var title = new Label
		{
			Text = "Stone Monument",
			HorizontalAlignment = HorizontalAlignment.Center
		};
		title.AddThemeFontSizeOverride("font_size", 22);
		root.AddChild(title);

		_coinsLabel = new Label { Text = "Coins: 0" };
		root.AddChild(_coinsLabel);

		_progressLabel = new Label { Text = "", AutowrapMode = TextServer.AutowrapMode.Word };
		root.AddChild(_progressLabel);

		var resetRow = new HBoxContainer();
		resetRow.AddThemeConstantOverride("separation", 8);
		root.AddChild(resetRow);
		_resetWorldButton = new Button { Text = "Randomize Map Again", CustomMinimumSize = new Vector2(260, 36) };
		_resetWorldButton.Pressed += OnRandomizeWorldPressed;
		resetRow.AddChild(_resetWorldButton);
		var resetHint = new Label { Text = "Always available. Rerandomizes the world map and advances your world reset progress.", AutowrapMode = TextServer.AutowrapMode.Word, SizeFlagsHorizontal = SizeFlags.ExpandFill };
		resetHint.AddThemeFontSizeOverride("font_size", 12);
		resetRow.AddChild(resetHint);

		_statusLabel = new Label { Text = "Choose 1 of 3 upgrades. A new roll appears after your pick." };
		_statusLabel.AutowrapMode = TextServer.AutowrapMode.Word;
		root.AddChild(_statusLabel);

		var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
		root.AddChild(scroll);
		_list = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		_list.AddThemeConstantOverride("separation", 6);
		scroll.AddChild(_list);

		var hint = new Label { Text = "Pick one upgrade to reveal the next 3. Press F/X to close, Esc/B/Start to back out." };
		hint.AddThemeFontSizeOverride("font_size", 12);
		root.AddChild(hint);
	}

	private void ResolvePlayer()
	{
		if (_player == null || !GodotObject.IsInstanceValid(_player))
			_player = GetTree().GetFirstNodeInGroup("player") as Player;
	}

	public void Open()
	{
		ResolvePlayer();
		Visible = true;
		if (_currentOfferIds.Count == 0)
			RollNewOffers();
		Refresh();
		CallDeferred(nameof(GrabFirstUpgradeFocus));
	}

	public void Close()
	{
		Visible = false;
	}

	public override void _Input(InputEvent @event)
	{
		var villagerChat = GetTree().GetFirstNodeInGroup("villager_chat_ui") as Control;
		if (villagerChat != null && villagerChat.Visible) return;
		var traderUi = GetTree().GetFirstNodeInGroup("trader_ui") as Control;
		if (traderUi != null && traderUi.Visible) return;
		if (!Visible) return;
		bool closePressed = @event.IsActionPressed("interact") || @event.IsActionPressed("ui_cancel") || @event.IsActionPressed("pause_menu");
		if (!closePressed && @event is InputEventKey key && key.Pressed && !key.Echo && (key.Keycode == Key.F || key.Keycode == Key.Escape))
			closePressed = true;
		if (closePressed)
		{
			Close();
			GetViewport().SetInputAsHandled();
		}
	}

	private void RollNewOffers()
	{
		_currentOfferIds.Clear();
		if (UpgradeCatalog.All == null || UpgradeCatalog.All.Count == 0)
			return;

		var pool = new List<UpgradeDef>(UpgradeCatalog.All);
		int count = Math.Min(3, pool.Count);
		for (int i = 0; i < count; i++)
		{
			int idx = _rng.RandiRange(0, pool.Count - 1);
			_currentOfferIds.Add(pool[idx].Id);
			pool.RemoveAt(idx);
		}
	}

	private void RebuildOfferRows()
	{
		if (_list == null) return;
		_list.ClearChildrenSafe();

		foreach (string upgradeId in _currentOfferIds)
		{
			var def = UpgradeCatalog.Get(upgradeId);
			if (def == null) continue;

			var rowPanel = new PanelContainer();
			var rowStyle = new StyleBoxFlat { BgColor = new Color(0.18f, 0.18f, 0.22f, 1.0f) };
			rowStyle.SetCornerRadiusAll(4);
			rowStyle.SetContentMarginAll(6);
			rowPanel.AddThemeStyleboxOverride("panel", rowStyle);
			rowPanel.SizeFlagsHorizontal = SizeFlags.ExpandFill;

			var h = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
			h.AddThemeConstantOverride("separation", 8);
			rowPanel.AddChild(h);

			var info = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
			info.AddThemeConstantOverride("separation", 2);
			h.AddChild(info);

			var nameLabel = new Label { Name = "NameLabel", Text = def.Name };
			nameLabel.AddThemeFontSizeOverride("font_size", 16);
			info.AddChild(nameLabel);

			var descLabel = new Label { Name = "DescLabel", Text = def.Description };
			descLabel.AutowrapMode = TextServer.AutowrapMode.Word;
			descLabel.AddThemeFontSizeOverride("font_size", 12);
			info.AddChild(descLabel);

			var statsLabel = new Label { Name = "StatsLabel", Text = "" };
			statsLabel.AddThemeFontSizeOverride("font_size", 12);
			info.AddChild(statsLabel);

			var buy = new Button { Name = "BuyButton", Text = "Choose", CustomMinimumSize = new Vector2(140, 40) };
			buy.SizeFlagsVertical = SizeFlags.ShrinkCenter;
			string id = def.Id;
			buy.Pressed += () => OnBuyPressed(id);
			h.AddChild(buy);

			rowPanel.SetMeta("upgrade_id", def.Id);
			_list.AddChild(rowPanel);
		}
	}

	private void OnBuyPressed(string upgradeId)
	{
		ResolvePlayer();
		if (_player == null) return;
		var def = UpgradeCatalog.Get(upgradeId);
		if (def == null) return;

		int cost = _player.GetUpgradeCost(upgradeId);
		if (_player.TryPurchaseUpgrade(upgradeId))
		{
			_statusLabel.Text = $"Purchased {def.Name} for {cost} coin(s). New options rolled.";
			RollNewOffers();
			Refresh();
			CallDeferred(nameof(GrabFirstUpgradeFocus));
		}
		else
		{
			_statusLabel.Text = $"Not enough coins for {def.Name} (need {cost}). Pick another option or come back later.";
			Refresh();
		}
	}

	private void GrabFirstUpgradeFocus()
	{
		if (_list == null) return;
		foreach (Node child in _list.GetChildren())
		{
			Button buy = FindButton(child, "BuyButton");
			if (buy != null && buy.IsVisibleInTree())
			{
				buy.GrabFocus();
				return;
			}
		}
	}

	private bool IsAnyBossFightActive()
	{
		if (GetTree().GetFirstNodeInGroup("hellhound_swarm_boss_manager") is HellhoundSwarmBoss h && h.IsActive) return true;
		if (GetTree().GetFirstNodeInGroup("executioner_boss_manager") is ExecutionerBoss e && e.IsActive) return true;
		return false;
	}

	private void OnRandomizeWorldPressed()
	{
		ResolvePlayer();
		int cost = WorldProgress.GetWorldResetCost();
		if (_player == null)
		{
			_statusLabel.Text = "Player not found.";
			return;
		}
		if (IsAnyBossFightActive())
		{
			_statusLabel.Text = "Cannot randomize/reset the world during an active boss fight.";
			Refresh();
			return;
		}
		if (_player.Gold < cost)
		{
			_statusLabel.Text = $"Not enough coins to randomize the map (need {cost}).";
			Refresh();
			return;
		}

		var resetManager = GetTree().GetFirstNodeInGroup("world_reset_manager") as WorldResetManager
			?? GetTree().CurrentScene?.GetNodeOrNull<WorldResetManager>("WorldResetManager");
		if (resetManager == null || !resetManager.TryRandomizeWorld())
		{
			_statusLabel.Text = "Could not randomize the map right now.";
			Refresh();
			return;
		}

		Close();
	}

	public void Refresh()
	{
		if (_coinsLabel == null || _list == null) return;
		ResolvePlayer();
		int coins = _player?.Gold ?? 0;
		_coinsLabel.Text = $"Coins: {coins}";
		int resetCost = WorldProgress.GetWorldResetCost();
		if (_progressLabel != null)
		{
			_progressLabel.Text =
				$"World Progress: Resets completed {WorldProgress.WorldResets} | Current world day {WorldProgress.CurrentWorldDay} | " +
				$"High score (world reached) {WorldProgress.WorldResetHighScore} | Next randomize cost {resetCost} coins (+50 each reset)";
		}
		if (_resetWorldButton != null)
		{
			bool bossActive = IsAnyBossFightActive();
			_resetWorldButton.Text = $"Randomize Map Again ({resetCost}c)";
			_resetWorldButton.Disabled = bossActive || _player == null || _player.Gold < resetCost;
			if (bossActive)
				_resetWorldButton.TooltipText = "Unavailable during an active boss fight.";
			else
				_resetWorldButton.TooltipText = "";
		}

		if (_currentOfferIds.Count == 0)
			RollNewOffers();

		RebuildOfferRows();

		foreach (Node child in _list.GetChildren())
		{
			if (child is not PanelContainer row || !row.HasMeta("upgrade_id")) continue;
			string id = row.GetMeta("upgrade_id").AsString();
			var def = UpgradeCatalog.Get(id);
			if (def == null) continue;
			int level = _player?.GetUpgradeLevel(id) ?? 0;
			int cost = def.GetCost(level);

			Label statsLabel = FindLabel(row, "StatsLabel");
			Button buy = FindButton(row, "BuyButton");
			if (statsLabel != null)
			{
				statsLabel.Text = $"Level: {level}    Cost: {cost} coin(s)    (No level cap)";
			}
			if (buy != null)
			{
				buy.Text = $"Choose ({cost})";
				buy.Disabled = _player == null || _player.Gold < cost;
			}
		}
	}

	private static Label FindLabel(Node root, string name)
	{
		foreach (Node n in root.GetChildren())
		{
			if (n is Label l && n.Name == name) return l;
			var found = FindLabel(n, name);
			if (found != null) return found;
		}
		return null;
	}

	private static Button FindButton(Node root, string name)
	{
		foreach (Node n in root.GetChildren())
		{
			if (n is Button b && n.Name == name) return b;
			var found = FindButton(n, name);
			if (found != null) return found;
		}
		return null;
	}
}

internal static class NodeUiExtensions
{
	public static void ClearChildrenSafe(this Node node)
	{
		foreach (Node child in node.GetChildren())
			child.QueueFree();
	}
}
