using Godot;
using GridQuest.Items;
using System;
using System.Collections.Generic;

public partial class TraderUi : Control
{
	private sealed class SellOffer
	{
		public string Source; // "backpack" or "weapon"
		public int SlotIndex;
		public string ItemId;
		public int Count;
		public int PricePerItem;
		public int TotalPrice => PricePerItem * Count;
	}

	private sealed class BuyOffer
	{
		public string ItemId;
		public int Price;
	}

	private global::Villager _trader;
	private Player _player;

	private Label _titleLabel;
	private Label _coinsLabel;
	private Label _statusLabel;
	private VBoxContainer _sellList;
	private VBoxContainer _buyList;
	private Button _sellButton;
	private Button _buyButton;
	private Button _closeButton;
	private SpinBox _sellQtySpin;
	private Button _sellQtyMaxButton;
	private Label _sellQtyLabel;
	private Label _sellTotalLabel;

	private readonly List<SellOffer> _sellOffers = new();
	private readonly List<BuyOffer> _buyOffers = new();
	private int _selectedSellIndex = -1;
	private int _selectedBuyIndex = -1;
	private int _selectedSellQuantity = 1;

	public override void _Ready()
	{
		AddToGroup("trader_ui");
		ProcessMode = ProcessModeEnum.Always;
		Visible = false;
		MouseFilter = MouseFilterEnum.Stop;
		SetAnchorsPreset(LayoutPreset.FullRect);
		BuildUi();
		CallDeferred(nameof(ResolvePlayer));
	}

	private void BuildUi()
	{
		foreach (Node child in GetChildren()) child.QueueFree();

		var dim = new ColorRect { Color = new Color(0, 0, 0, 0.45f) };
		dim.SetAnchorsPreset(LayoutPreset.FullRect);
		dim.MouseFilter = MouseFilterEnum.Stop;
		AddChild(dim);

		var panel = new PanelContainer { CustomMinimumSize = new Vector2(860, 520) };
		panel.SetAnchorsPreset(LayoutPreset.Center);
		panel.GrowHorizontal = GrowDirection.Both;
		panel.GrowVertical = GrowDirection.Both;
		var sb = new StyleBoxFlat { BgColor = new Color(0.12f, 0.12f, 0.15f, 0.96f) };
		sb.SetCornerRadiusAll(8);
		sb.SetContentMarginAll(10);
		panel.AddThemeStyleboxOverride("panel", sb);
		AddChild(panel);

		var root = new VBoxContainer();
		root.SetAnchorsPreset(LayoutPreset.FullRect);
		root.OffsetLeft = 10; root.OffsetTop = 10; root.OffsetRight = -10; root.OffsetBottom = -10;
		root.AddThemeConstantOverride("separation", 8);
		panel.AddChild(root);

		var header = new HBoxContainer();
		header.AddThemeConstantOverride("separation", 8);
		root.AddChild(header);

		_titleLabel = new Label { Text = "Trader", SizeFlagsHorizontal = SizeFlags.ExpandFill };
		_titleLabel.AddThemeFontSizeOverride("font_size", 22);
		header.AddChild(_titleLabel);

		_closeButton = new Button { Text = "Close", CustomMinimumSize = new Vector2(90, 0) };
		_closeButton.Pressed += Close;
		header.AddChild(_closeButton);

		_coinsLabel = new Label { Text = "Coins: 0" };
		root.AddChild(_coinsLabel);

		_statusLabel = new Label { Text = "Sell looted items/weapons, buy weapons, or pay for a permanent Full Heal service." };
		_statusLabel.AutowrapMode = TextServer.AutowrapMode.Word;
		root.AddChild(_statusLabel);

		var columns = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
		columns.AddThemeConstantOverride("separation", 10);
		root.AddChild(columns);

		var sellPanel = MakeSubPanel("Sell Items");
		sellPanel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		columns.AddChild(sellPanel);
		var sellScroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 320) };
		sellPanel.GetNode<VBoxContainer>("VBox").AddChild(sellScroll);
		_sellList = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		_sellList.AddThemeConstantOverride("separation", 4);
		sellScroll.AddChild(_sellList);

		var qtyRow = new HBoxContainer();
		qtyRow.AddThemeConstantOverride("separation", 6);
		sellPanel.GetNode<VBoxContainer>("VBox").AddChild(qtyRow);
		_sellQtyLabel = new Label { Text = "Quantity:", VerticalAlignment = VerticalAlignment.Center };
		qtyRow.AddChild(_sellQtyLabel);
		_sellQtySpin = new SpinBox { MinValue = 1, MaxValue = 1, Step = 1, Value = 1, CustomMinimumSize = new Vector2(90, 0) };
		_sellQtySpin.ValueChanged += OnSellQuantityChanged;
		qtyRow.AddChild(_sellQtySpin);
		_sellQtyMaxButton = new Button { Text = "Max", CustomMinimumSize = new Vector2(52, 0) };
		_sellQtyMaxButton.Pressed += OnSellMaxPressed;
		qtyRow.AddChild(_sellQtyMaxButton);
		_sellTotalLabel = new Label { Text = "Total: 0c", SizeFlagsHorizontal = SizeFlags.ExpandFill, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
		qtyRow.AddChild(_sellTotalLabel);

		_sellButton = new Button { Text = "Sell Selected", CustomMinimumSize = new Vector2(0, 36) };
		_sellButton.Pressed += OnSellPressed;
		sellPanel.GetNode<VBoxContainer>("VBox").AddChild(_sellButton);

		var buyPanel = MakeSubPanel("Buy Weapons");
		buyPanel.CustomMinimumSize = new Vector2(320, 0);
		columns.AddChild(buyPanel);

		// Permanent service: full heal (separate and visually distinct)
		var serviceBox = new PanelContainer();
		var serviceSb = new StyleBoxFlat { BgColor = new Color(0.10f, 0.28f, 0.18f, 1.0f), BorderColor = new Color(0.35f, 0.95f, 0.55f, 1.0f) };
		serviceSb.SetBorderWidthAll(2);
		serviceSb.SetCornerRadiusAll(6);
		serviceSb.SetContentMarginAll(8);
		serviceBox.AddThemeStyleboxOverride("panel", serviceSb);
		var serviceVBox = new VBoxContainer();
		serviceVBox.AddThemeConstantOverride("separation", 4);
		serviceBox.AddChild(serviceVBox);
		var serviceTitle = new Label { Text = "Service (Permanent)", HorizontalAlignment = HorizontalAlignment.Center };
		serviceTitle.AddThemeColorOverride("font_color", new Color(0.75f, 1.0f, 0.85f, 1.0f));
		serviceTitle.AddThemeFontSizeOverride("font_size", 14);
		serviceVBox.AddChild(serviceTitle);
		var healButton = new Button { Text = "Full Heal (5c)", CustomMinimumSize = new Vector2(0, 34) };
		var healStyle = new StyleBoxFlat { BgColor = new Color(0.18f, 0.55f, 0.30f, 1.0f), BorderColor = new Color(0.55f, 1.0f, 0.70f, 1.0f) };
		healStyle.SetBorderWidthAll(2);
		healStyle.SetCornerRadiusAll(5);
		healButton.AddThemeStyleboxOverride("normal", healStyle);
		healButton.AddThemeColorOverride("font_color", new Color(0.95f, 1.0f, 0.97f, 1.0f));
		healButton.Pressed += OnFullHealPressed;
		serviceVBox.AddChild(healButton);
		buyPanel.GetNode<VBoxContainer>("VBox").AddChild(serviceBox);

		var weaponsLabel = new Label { Text = "Weapons", HorizontalAlignment = HorizontalAlignment.Center };
		weaponsLabel.AddThemeFontSizeOverride("font_size", 13);
		buyPanel.GetNode<VBoxContainer>("VBox").AddChild(weaponsLabel);

		var buyScroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 260) };
		buyPanel.GetNode<VBoxContainer>("VBox").AddChild(buyScroll);
		_buyList = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		_buyList.AddThemeConstantOverride("separation", 4);
		buyScroll.AddChild(_buyList);
		_buyButton = new Button { Text = "Buy Selected", CustomMinimumSize = new Vector2(0, 36) };
		_buyButton.Pressed += OnBuyPressed;
		buyPanel.GetNode<VBoxContainer>("VBox").AddChild(_buyButton);

		var hint = new Label { Text = "Interact/Esc/B/Start: Close | Click items to select then Sell/Buy" };
		hint.AddThemeFontSizeOverride("font_size", 12);
		root.AddChild(hint);
	}

	private static PanelContainer MakeSubPanel(string title)
	{
		var panel = new PanelContainer();
		var sb = new StyleBoxFlat { BgColor = new Color(0.16f, 0.16f, 0.2f, 1.0f) };
		sb.SetCornerRadiusAll(6);
		sb.SetContentMarginAll(8);
		panel.AddThemeStyleboxOverride("panel", sb);
		var vb = new VBoxContainer { Name = "VBox" };
		vb.AddThemeConstantOverride("separation", 6);
		panel.AddChild(vb);
		var label = new Label { Text = title, HorizontalAlignment = HorizontalAlignment.Center };
		label.AddThemeFontSizeOverride("font_size", 16);
		vb.AddChild(label);
		return panel;
	}

	private void ResolvePlayer()
	{
		if (_player == null || !IsInstanceValid(_player))
			_player = GetTree().GetFirstNodeInGroup("player") as Player;
	}

	public void Open(Node traderNode)
	{
		ResolvePlayer();
		if (_player == null) return;

		_trader = traderNode as global::Villager;
		if (_trader == null && traderNode is GridQuest.Entities.Npc.VillagerEntity ve)
		{
			// TraderUi uses wrapper type in some APIs; try to find wrapper in scene if needed.
			_trader = ve as global::Villager;
		}

		Visible = true;
		GetTree().Paused = true;
		_selectedSellIndex = -1;
		_selectedBuyIndex = -1;
		_selectedSellQuantity = 1;
		BuildBuyOffers();
		Refresh();
		if (_closeButton != null && _closeButton.IsVisibleInTree()) _closeButton.GrabFocus();
	}

	public void Close()
	{
		if (!Visible) return;
		Visible = false;
		_trader = null;
		_selectedSellIndex = -1;
		_selectedBuyIndex = -1;
		_selectedSellQuantity = 1;
		if (GetTree() != null) GetTree().Paused = false;
	}

	public override void _Input(InputEvent @event)
	{
		if (!Visible) return;

		bool closePressed = @event.IsActionPressed("ui_cancel") || @event.IsActionPressed("pause_menu") || @event.IsActionPressed("interact");
		if (!closePressed && @event is InputEventKey key && key.Pressed && !key.Echo && (key.Keycode == Key.Escape || key.Keycode == Key.F))
			closePressed = true;

		if (closePressed)
		{
			Close();
			GetViewport().SetInputAsHandled();
		}
	}

	private void BuildBuyOffers()
	{
		_buyOffers.Clear();
		// Weapons for sale (pricey)
		_buyOffers.Add(new BuyOffer { ItemId = "base_sword", Price = 120 });
		_buyOffers.Add(new BuyOffer { ItemId = "fire_staff", Price = 240 });
		_buyOffers.Add(new BuyOffer { ItemId = "scythe", Price = 320 });
		_buyOffers.Add(new BuyOffer { ItemId = "crossbow", Price = 360 });
	}

	private void Refresh()
	{
		ResolvePlayer();
		if (_player == null) return;
		_player.ConvertInventoryCoinsToGold();

		if (_titleLabel != null)
		{
			string name = _trader != null && IsInstanceValid(_trader) ? _trader.DisplayNameForChat() : "Trader";
			_titleLabel.Text = name;
		}
		if (_coinsLabel != null)
			_coinsLabel.Text = $"Coins: {_player.Gold}";

		BuildSellOffers();
		RebuildSellList();
		RebuildBuyList();
		UpdateActionButtons();
	}

	private void BuildSellOffers()
	{
		string prevKey = GetSellSelectionKey(_selectedSellIndex);
		_sellOffers.Clear();
		if (_player == null) return;

		// Equipped weapon
		if (!string.IsNullOrEmpty(_player.EquippedWeaponId))
		{
			string wid = _player.EquippedWeaponId;
			int p = GetSellPricePerItem(wid);
			if (p > 0)
				_sellOffers.Add(new SellOffer { Source = "weapon", SlotIndex = -1, ItemId = wid, Count = 1, PricePerItem = p });
		}

		if (_player.Inventory?.Slots != null)
		{
			for (int i = 0; i < _player.Inventory.Slots.Length; i++)
			{
				var s = _player.Inventory.Slots[i];
				if (s == null || s.IsEmpty) continue;
				if (s.ItemId == "coin") continue; // wallet currency
				int p = GetSellPricePerItem(s.ItemId);
				if (p <= 0) continue;
				_sellOffers.Add(new SellOffer { Source = "backpack", SlotIndex = i, ItemId = s.ItemId, Count = s.Count, PricePerItem = p });
			}
		}

		_selectedSellIndex = -1;
		if (!string.IsNullOrEmpty(prevKey))
		{
			for (int i = 0; i < _sellOffers.Count; i++)
			{
				if (GetSellSelectionKey(i) == prevKey)
				{
					_selectedSellIndex = i;
					break;
				}
			}
		}
		ClampSelectedSellQuantity();
	}

	private string GetSellSelectionKey(int index)
	{
		if (index < 0 || index >= _sellOffers.Count) return "";
		var o = _sellOffers[index];
		return $"{o.Source}:{o.SlotIndex}:{o.ItemId}";
	}

	private void ClampSelectedSellQuantity()
	{
		if (_selectedSellIndex < 0 || _selectedSellIndex >= _sellOffers.Count)
		{
			_selectedSellQuantity = 1;
			return;
		}
		int max = Mathf.Max(1, _sellOffers[_selectedSellIndex].Count);
		_selectedSellQuantity = Mathf.Clamp(_selectedSellQuantity, 1, max);
	}

	private void RebuildSellList()
	{
		if (_sellList == null) return;
		ClearChildren(_sellList);

		if (_sellOffers.Count == 0)
		{
			_sellList.AddChild(new Label { Text = "No sellable items.", AutowrapMode = TextServer.AutowrapMode.Word });
			return;
		}

		for (int i = 0; i < _sellOffers.Count; i++)
		{
			var offer = _sellOffers[i];
			var def = ItemCatalog.Get(offer.ItemId);
			string name = def?.Name ?? offer.ItemId;
			string src = offer.Source == "weapon" ? "Equipped" : $"Bag {offer.SlotIndex + 1}";
			string countText = offer.Count > 1 ? $" x{offer.Count}" : "";
			var btn = new Button
			{
				Text = $"{name}{countText} [{src}]  -  {offer.PricePerItem} coins each",
				Alignment = HorizontalAlignment.Left,
				SizeFlagsHorizontal = SizeFlags.ExpandFill,
				CustomMinimumSize = new Vector2(0, 34),
			};
			int idx = i;
			btn.Pressed += () => { _selectedSellIndex = idx; ClampSelectedSellQuantity(); UpdateSellQuantityUi(); UpdateSellSelectionVisuals(); UpdateActionButtons(); };
			_sellList.AddChild(btn);
		}
		UpdateSellSelectionVisuals();
	}

	private void RebuildBuyList()
	{
		if (_buyList == null) return;
		ClearChildren(_buyList);

		for (int i = 0; i < _buyOffers.Count; i++)
		{
			var offer = _buyOffers[i];
			var def = ItemCatalog.Get(offer.ItemId);
			string name = def?.Name ?? offer.ItemId;
			var btn = new Button
			{
				Text = $"{name}  -  {offer.Price} coins",
				Alignment = HorizontalAlignment.Left,
				SizeFlagsHorizontal = SizeFlags.ExpandFill,
				CustomMinimumSize = new Vector2(0, 40),
			};
			int idx = i;
			btn.Pressed += () => { _selectedBuyIndex = idx; UpdateBuySelectionVisuals(); UpdateActionButtons(); };
			_buyList.AddChild(btn);
		}
		UpdateBuySelectionVisuals();
	}

	private void UpdateSellSelectionVisuals()
	{
		for (int i = 0; i < _sellList.GetChildCount(); i++)
		{
			if (_sellList.GetChild(i) is Button b)
				b.Modulate = (i == _selectedSellIndex) ? new Color(1f, 1f, 0.55f) : Colors.White;
		}
	}

	private void UpdateBuySelectionVisuals()
	{
		for (int i = 0; i < _buyList.GetChildCount(); i++)
		{
			if (_buyList.GetChild(i) is Button b)
				b.Modulate = (i == _selectedBuyIndex) ? new Color(1f, 1f, 0.55f) : Colors.White;
		}
	}

	private void UpdateActionButtons()
	{
		bool canSell = _selectedSellIndex >= 0 && _selectedSellIndex < _sellOffers.Count;
		if (_sellButton != null)
		{
			_sellButton.Disabled = !canSell;
			if (canSell)
			{
				var offer = _sellOffers[_selectedSellIndex];
				int qty = Mathf.Clamp(_selectedSellQuantity, 1, offer.Count);
				int total = offer.PricePerItem * qty;
				_sellButton.Text = $"Sell {qty} for {total}c";
			}
			else
			{
				_sellButton.Text = "Sell Selected";
			}
		}
		if (_buyButton != null)
			_buyButton.Disabled = _selectedBuyIndex < 0 || _selectedBuyIndex >= _buyOffers.Count;

		UpdateSellQuantityUi();
	}

	private void UpdateSellQuantityUi()
	{
		if (_sellQtySpin == null || _sellTotalLabel == null)
			return;

		bool hasSelection = _selectedSellIndex >= 0 && _selectedSellIndex < _sellOffers.Count;
		if (!hasSelection)
		{
			_sellQtySpin.MinValue = 1;
			_sellQtySpin.MaxValue = 1;
			if (Math.Abs(_sellQtySpin.Value - 1.0) > 0.001) _sellQtySpin.Value = 1;
			_sellQtySpin.Editable = false;
			if (_sellQtyMaxButton != null) _sellQtyMaxButton.Disabled = true;
			_sellTotalLabel.Text = "Total: 0c";
			if (_sellQtyLabel != null) _sellQtyLabel.Text = "Quantity:";
			return;
		}

		var offer = _sellOffers[_selectedSellIndex];
		int max = Mathf.Max(1, offer.Count);
		_selectedSellQuantity = Mathf.Clamp(_selectedSellQuantity, 1, max);
		_sellQtySpin.Editable = max > 1;
		if (_sellQtyMaxButton != null) _sellQtyMaxButton.Disabled = max <= 1 || _selectedSellQuantity >= max;
		_sellQtySpin.MinValue = 1;
		_sellQtySpin.MaxValue = max;
		_sellQtySpin.Step = 1;
		if (Math.Abs(_sellQtySpin.Value - _selectedSellQuantity) > 0.001)
			_sellQtySpin.Value = _selectedSellQuantity;
		_sellTotalLabel.Text = $"Total: {offer.PricePerItem * _selectedSellQuantity}c";
		if (_sellQtyLabel != null) _sellQtyLabel.Text = max > 1 ? $"Quantity (1-{max}):" : "Quantity:";
	}

	private void OnSellQuantityChanged(double value)
	{
		_selectedSellQuantity = Mathf.Max(1, Mathf.RoundToInt((float)value));
		ClampSelectedSellQuantity();
		UpdateActionButtons();
	}

	private void OnSellMaxPressed()
	{
		if (_selectedSellIndex < 0 || _selectedSellIndex >= _sellOffers.Count)
			return;
		_selectedSellQuantity = Mathf.Max(1, _sellOffers[_selectedSellIndex].Count);
		ClampSelectedSellQuantity();
		UpdateActionButtons();
	}

	private void OnSellPressed()
	{
		ResolvePlayer();
		if (_player == null) return;
		if (_selectedSellIndex < 0 || _selectedSellIndex >= _sellOffers.Count) return;

		// Rebuild/validate against current state
		BuildSellOffers();
		if (_selectedSellIndex >= _sellOffers.Count) { _selectedSellIndex = -1; _selectedSellQuantity = 1; Refresh(); return; }
		var offer = _sellOffers[_selectedSellIndex];
		int qty = Mathf.Clamp(_selectedSellQuantity, 1, offer.Count);

		bool sold = false;
		int soldQty = 0;
		int earned = 0;
		if (offer.Source == "weapon")
		{
			if (_player.EquippedWeaponId == offer.ItemId)
			{
				_player.EquippedWeaponId = null;
				_player.SyncEquippedFromId();
				soldQty = 1;
				earned = offer.PricePerItem * soldQty;
				_player.AddGold(earned);
				sold = true;
			}
		}
		else if (offer.Source == "backpack")
		{
			var slot = _player.Inventory?.Get(offer.SlotIndex);
			if (slot != null && !slot.IsEmpty && slot.ItemId == offer.ItemId)
			{
				int pricePer = GetSellPricePerItem(offer.ItemId);
				soldQty = Mathf.Clamp(qty, 1, slot.Count);
				if (pricePer > 0 && soldQty > 0)
				{
					slot.Count -= soldQty;
					if (slot.Count <= 0)
					{
						slot.ItemId = null;
						slot.Count = 0;
					}
					earned = pricePer * soldQty;
					_player.AddGold(earned);
					sold = true;
				}
			}
		}

		if (sold)
		{
			_statusLabel.Text = soldQty > 1
				? $"Sold {soldQty}x {ItemName(offer.ItemId)} for {earned} coins."
				: $"Sold {ItemName(offer.ItemId)} for {earned} coins.";
			_selectedSellQuantity = 1;
		}
		else
		{
			_statusLabel.Text = "Could not sell that item.";
		}
		_player.SyncEquippedFromId();
		Refresh();
	}


	private void OnFullHealPressed()
	{
		ResolvePlayer();
		if (_player == null || !IsInstanceValid(_player)) return;
		const int healCost = 5;
		if (_player.Health >= _player.MaxHealth)
		{
			_statusLabel.Text = "You are already at full health.";
			Refresh();
			return;
		}
		if (_player.Gold < healCost)
		{
			_statusLabel.Text = $"Not enough coins for Full Heal ({healCost}c).";
			Refresh();
			return;
		}
		if (!_player.TrySpendCoins(healCost))
		{
			_statusLabel.Text = $"Could not pay for Full Heal ({healCost}c).";
			Refresh();
			return;
		}
		_player.Heal(_player.MaxHealth);
		_statusLabel.Text = $"Fully healed for {healCost} coins.";
		Refresh();
	}

	private void OnBuyPressed()
	{
		ResolvePlayer();
		if (_player == null) return;
		if (_selectedBuyIndex < 0 || _selectedBuyIndex >= _buyOffers.Count) return;

		var offer = _buyOffers[_selectedBuyIndex];
		if (_player.Gold < offer.Price)
		{
			_statusLabel.Text = $"Not enough coins for {ItemName(offer.ItemId)} ({offer.Price}c).";
			Refresh();
			return;
		}

		bool granted = TryGiveBoughtItem(offer.ItemId);
		if (!granted)
		{
			_statusLabel.Text = "No space to carry that weapon.";
			Refresh();
			return;
		}

		_player.TrySpendCoins(offer.Price);
		_player.SyncEquippedFromId();
		_statusLabel.Text = $"Bought {ItemName(offer.ItemId)} for {offer.Price} coins.";
		Refresh();
	}

	private bool TryGiveBoughtItem(string itemId)
	{
		if (_player == null || string.IsNullOrEmpty(itemId)) return false;
		var def = ItemCatalog.Get(itemId);
		if (def == null) return false;

		// Equip weapon immediately if slot is empty.
		if (def.IsWeapon && string.IsNullOrEmpty(_player.EquippedWeaponId))
		{
			_player.EquippedWeaponId = itemId;
			return true;
		}

		return _player.Inventory != null && _player.Inventory.AddItem(itemId, 1);
	}

	private static string ItemName(string itemId)
	{
		var def = ItemCatalog.Get(itemId);
		return def?.Name ?? itemId ?? "Item";
	}

	private static (int min, int max) GetSellPriceRange(string itemId)
	{
		return itemId switch
		{
			"key" => (5, 10),
			"bomb" => (8, 15),
			"blue_orb" => (12, 22),
			"red_orb" => (12, 22),
			"green_orb" => (12, 22),
			"letter" => (3, 7),
			"boots" => (20, 35),
			"base_sword" => (40, 70),
			"fire_staff" => (80, 130),
			"scythe" => (110, 180),
			"crossbow" => (130, 200),
			_ => (0, 0),
		};
	}

	private static string GetSellPriceRangeText(string itemId)
	{
		var (min, max) = GetSellPriceRange(itemId);
		if (max <= 0) return "-";
		return $"{min}-{max}";
	}

	private static int GetSellPricePerItem(string itemId)
	{
		var (min, max) = GetSellPriceRange(itemId);
		if (max <= 0) return 0;
		// Simple midpoint offer (stable, clear). Range shown in UI as "between prices" table/range.
		return Mathf.RoundToInt((min + max) / 2.0f);
	}

	private static void ClearChildren(Node node)
	{
		foreach (Node child in node.GetChildren())
			child.QueueFree();
	}
}
