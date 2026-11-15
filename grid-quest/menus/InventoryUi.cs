using Godot;
using GridQuest.Entities.Player;
using GridQuest.Items;
using GridQuest.Map.MapObjects;

public partial class InventoryUi : Control
{
	private PlayerEntity _player;
	private Chest _openChest;

	private Label _titleLabel;
	private Label _goldLabel;
	private Label _hintLabel;
	private GridContainer _playerGrid;
	private InventorySlotUi _weaponSlot;
	private PanelContainer _chestPanel;
	private GridContainer _chestGrid;
	private Button _takeAllButton;
	private Button _closeChestButton;

	private readonly InventorySlotUi[] _playerSlots = new InventorySlotUi[16];
	private readonly InventorySlotUi[] _chestSlots = new InventorySlotUi[12];

	private InventorySlotUi _selected;
	private VBoxContainer _upgradesBox;
	private Label _upgradesHeader;

	public override void _Ready()
	{
		AddToGroup("inventory_ui");
		ProcessMode = ProcessModeEnum.Always;
		Visible = false;
		MouseFilter = MouseFilterEnum.Stop;
		SetAnchorsPreset(LayoutPreset.FullRect);
		BuildUi();
		CallDeferred(nameof(ResolvePlayer));
	}

	private void BuildUi()
	{
		foreach (var child in GetChildren()) child.QueueFree();

		var dim = new ColorRect { Color = new Color(0, 0, 0, 0.45f) };
		dim.SetAnchorsPreset(LayoutPreset.FullRect);
		dim.MouseFilter = MouseFilterEnum.Stop;
		AddChild(dim);

		var root = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
		root.SetAnchorsPreset(LayoutPreset.Center);
		root.GrowHorizontal = GrowDirection.Both;
		root.GrowVertical = GrowDirection.Both;
		root.AddThemeConstantOverride("separation", 12);
		AddChild(root);

		var playerPanel = MakePanel(new Vector2(420, 360));
		root.AddChild(playerPanel);
		var pvb = new VBoxContainer();
		pvb.SetAnchorsPreset(LayoutPreset.FullRect);
		pvb.OffsetLeft = 10; pvb.OffsetTop = 10; pvb.OffsetRight = -10; pvb.OffsetBottom = -10;
		pvb.AddThemeConstantOverride("separation", 6);
		playerPanel.AddChild(pvb);

		_titleLabel = new Label { Text = "Inventory", HorizontalAlignment = HorizontalAlignment.Center };
		pvb.AddChild(_titleLabel);
		_goldLabel = new Label { Text = "Coins: 0" };
		pvb.AddChild(_goldLabel);

		var weaponRow = new HBoxContainer();
		weaponRow.AddChild(new Label { Text = "Weapon:" , VerticalAlignment = VerticalAlignment.Center});
		_weaponSlot = new InventorySlotUi { ContainerId = "weapon", SlotIndex = -1, CustomMinimumSize = new Vector2(52, 52) };
		_weaponSlot.SlotPressed += OnSlotPressed;
		weaponRow.AddChild(_weaponSlot);
		pvb.AddChild(weaponRow);

		pvb.AddChild(new Label { Text = "Backpack:" });
		_playerGrid = new GridContainer { Columns = 8 };
		_playerGrid.AddThemeConstantOverride("h_separation", 4);
		_playerGrid.AddThemeConstantOverride("v_separation", 4);
		pvb.AddChild(_playerGrid);
		for (int i = 0; i < _playerSlots.Length; i++)
		{
			var slot = new InventorySlotUi { ContainerId = "player", SlotIndex = i };
			slot.SlotPressed += OnSlotPressed;
			_playerSlots[i] = slot;
			_playerGrid.AddChild(slot);
		}

		_hintLabel = new Label { Text = "E: Close | Click or drag items to move/swap | Click chest to open", AutowrapMode = TextServer.AutowrapMode.Word };
		_hintLabel.AddThemeFontSizeOverride("font_size", 12);
		pvb.AddChild(_hintLabel);

		_upgradesHeader = new Label { Text = "Upgrades:", Visible = false };
		_upgradesHeader.AddThemeFontSizeOverride("font_size", 13);
		pvb.AddChild(_upgradesHeader);
		_upgradesBox = new VBoxContainer { Visible = false };
		_upgradesBox.AddThemeConstantOverride("separation", 2);
		pvb.AddChild(_upgradesBox);

		_chestPanel = MakePanel(new Vector2(300, 360));
		_chestPanel.Visible = false;
		root.AddChild(_chestPanel);
		var cvb = new VBoxContainer();
		cvb.SetAnchorsPreset(LayoutPreset.FullRect);
		cvb.OffsetLeft = 10; cvb.OffsetTop = 10; cvb.OffsetRight = -10; cvb.OffsetBottom = -10;
		cvb.AddThemeConstantOverride("separation", 6);
		_chestPanel.AddChild(cvb);
		cvb.AddChild(new Label { Text = "Chest", HorizontalAlignment = HorizontalAlignment.Center });
		_chestGrid = new GridContainer { Columns = 4 };
		_chestGrid.AddThemeConstantOverride("h_separation", 4);
		_chestGrid.AddThemeConstantOverride("v_separation", 4);
		cvb.AddChild(_chestGrid);
		for (int i = 0; i < _chestSlots.Length; i++)
		{
			var slot = new InventorySlotUi { ContainerId = "chest", SlotIndex = i };
			slot.SlotPressed += OnSlotPressed;
			_chestSlots[i] = slot;
			_chestGrid.AddChild(slot);
		}
		var chestBtns = new HBoxContainer();
		_takeAllButton = new Button { Text = "Take All" };
		_takeAllButton.Pressed += OnTakeAll;
		_closeChestButton = new Button { Text = "Close Chest" };
		_closeChestButton.Pressed += CloseChest;
		chestBtns.AddChild(_takeAllButton);
		chestBtns.AddChild(_closeChestButton);
		cvb.AddChild(chestBtns);
	}

	private static PanelContainer MakePanel(Vector2 size)
	{
		var p = new PanelContainer { CustomMinimumSize = size };
		var sb = new StyleBoxFlat { BgColor = new Color(0.12f, 0.12f, 0.15f, 0.95f) };
		sb.SetCornerRadiusAll(6);
		sb.SetContentMarginAll(4);
		p.AddThemeStyleboxOverride("panel", sb);
		return p;
	}

	private void ResolvePlayer()
	{
		_player = GetTree().GetFirstNodeInGroup("player") as PlayerEntity;
		Refresh();
	}

	public override void _Input(InputEvent @event)
	{
		if (@event.IsEcho()) return;
		var villagerChat = GetTree().GetFirstNodeInGroup("villager_chat_ui") as Control;
		if (villagerChat != null && villagerChat.Visible)
			return;
		bool togglePressed = @event.IsActionPressed("toggle_inventory");
		bool interactPressed = @event.IsActionPressed("interact");
		bool cancelPressed = @event.IsActionPressed("ui_cancel") || @event.IsActionPressed("pause_menu");

		// Fallbacks for direct device events (and when actions are remapped in-editor)
		if (@event is InputEventKey key && key.Pressed && !key.Echo)
		{
			if (key.Keycode == Key.E || key.Keycode == Key.Tab || key.Keycode == Key.I) togglePressed = true;
			if (key.Keycode == Key.F) interactPressed = true;
			if (key.Keycode == Key.Escape && Visible) cancelPressed = true;
		}
		if (@event is InputEventJoypadButton jb && jb.Pressed && jb.ButtonIndex == JoyButton.Y) togglePressed = true;

		if (Visible && cancelPressed)
		{
			CloseInventory();
			GetViewport().SetInputAsHandled();
			return;
		}

		if (interactPressed)
		{
			if (TryInteractChest())
			{
				GetViewport().SetInputAsHandled();
				return;
			}
			// No chest interaction consumed; allow monument/villager handlers to process it.
		}

		if (togglePressed)
		{
			if (Visible) CloseInventory();
			else OpenInventory();
			GetViewport().SetInputAsHandled();
		}
	}

	public void OpenInventory()
	{
		Visible = true;
		_selected = null;
		Refresh();
		GrabInventoryFocus();
	}

	public void CloseInventory()
	{
		_openChest = null;
		_chestPanel.Visible = false;
		Visible = false;
		_selected = null;
	}

	public void OpenChest(Chest chest)
	{
		_openChest = chest;
		_chestPanel.Visible = chest != null;
		Visible = true;
		_selected = null;
		Refresh();
		GrabInventoryFocus();
	}

	private void GrabInventoryFocus()
	{
		Control target = null;
		if (_chestPanel.Visible && _takeAllButton != null && _takeAllButton.IsVisibleInTree())
			target = _takeAllButton;
		else if (_weaponSlot?.Button != null && _weaponSlot.Button.IsVisibleInTree())
			target = _weaponSlot.Button;
		else if (_playerSlots.Length > 0 && _playerSlots[0]?.Button != null)
			target = _playerSlots[0].Button;
		target?.GrabFocus();
	}

	public void CloseChest()
	{
		// When in chest menu, close the whole inventory/chest view (not just the chest panel).
		CloseInventory();
	}

	private bool TryInteractChest()
	{
		if (_player == null || !IsInstanceValid(_player)) ResolvePlayer();
		if (_player == null) return false;
		Chest nearest = null;
		float best = 40f * 40f;
		foreach (var node in GetTree().GetNodesInGroup("chests"))
		{
			if (node is Chest chest && IsInstanceValid(chest))
			{
				float d2 = _player.GlobalPosition.DistanceSquaredTo(chest.GlobalPosition);
				if (d2 < best) { best = d2; nearest = chest; }
			}
		}
		if (nearest != null)
		{
			if (Visible && _openChest == nearest)
			{
				CloseInventory();
				return true;
			}
			nearest.Interact(_player);
			return true;
		}
		return false;
	}

	private void OnSlotPressed(InventorySlotUi slot)
	{
		if (slot == null) return;
		if (_selected == null)
		{
			if (!GetStack(slot).IsEmpty)
			{
				_selected = slot;
			}
		}
		else
		{
			if (_selected != slot) MoveBetween(_selected, slot);
			_selected = null;
		}
		Refresh();
	}

	public ItemStack GetStack(InventorySlotUi slot)
	{
		if (slot == null) return new ItemStack();
		if (slot.ContainerId == "weapon")
		{
			if (_player == null) return new ItemStack();
			return string.IsNullOrEmpty(_player.EquippedWeaponId) ? new ItemStack() : new ItemStack { ItemId = _player.EquippedWeaponId, Count = 1 };
		}
		if (slot.ContainerId == "player")
		{
			return _player?.Inventory?.Get(slot.SlotIndex) ?? new ItemStack();
		}
		if (slot.ContainerId == "chest")
		{
			return _openChest?.Contents?.Get(slot.SlotIndex) ?? new ItemStack();
		}
		return new ItemStack();
	}

	public InventorySlotUi FindSlot(string containerId, int index)
	{
		if (containerId == "weapon") return _weaponSlot;
		if (containerId == "player" && index >= 0 && index < _playerSlots.Length) return _playerSlots[index];
		if (containerId == "chest" && index >= 0 && index < _chestSlots.Length) return _chestSlots[index];
		return null;
	}

	private void MoveBetween(InventorySlotUi from, InventorySlotUi to)
	{
		if (_player == null || from == null || to == null) return;

		if (from.ContainerId == "weapon" || to.ContainerId == "weapon")
		{
			MoveWithWeapon(from, to);
			_player.SyncEquippedFromId();
			return;
		}

		// Coins should never occupy player backpack slots: convert to wallet immediately.
		if (from.ContainerId == "chest" && to.ContainerId == "player")
		{
			var src = _openChest?.Contents?.Get(from.SlotIndex);
			var dst = _player.Inventory?.Get(to.SlotIndex);
			if (src != null && !src.IsEmpty && src.ItemId == "coin")
			{
				if (dst == null) return;
				if (dst.IsEmpty)
				{
					_player.AddGold(src.Count);
					src.ItemId = null; src.Count = 0;
				}
				else if (dst.ItemId == "coin")
				{
					_player.AddGold(src.Count + dst.Count);
					src.ItemId = null; src.Count = 0;
					dst.ItemId = null; dst.Count = 0;
				}
				// If destination has a non-coin item, keep chest coins in place (no silent swap).
				_player.SyncEquippedFromId();
				return;
			}
		}
		else if (from.ContainerId == "player" && to.ContainerId == "player")
		{
			var src = _player.Inventory?.Get(from.SlotIndex);
			var dst = _player.Inventory?.Get(to.SlotIndex);
			if (src != null && dst != null && !src.IsEmpty && src.ItemId == "coin")
			{
				if (dst.IsEmpty)
				{
					_player.AddGold(src.Count);
					src.ItemId = null; src.Count = 0;
					_player.SyncEquippedFromId();
					return;
				}
				if (dst.ItemId == "coin")
				{
					_player.AddGold(src.Count + dst.Count);
					src.ItemId = null; src.Count = 0;
					dst.ItemId = null; dst.Count = 0;
					_player.SyncEquippedFromId();
					return;
				}
			}
		}

		var a = GetContainer(from.ContainerId);
		var b = GetContainer(to.ContainerId);
		if (a == null || b == null) return;
		InventoryContainer.SwapOrMove(a, from.SlotIndex, b, to.SlotIndex);
		_player.ConvertInventoryCoinsToGold();
		_player.SyncEquippedFromId();
	}

	private InventoryContainer GetContainer(string id)
	{
		if (id == "player") return _player?.Inventory;
		if (id == "chest") return _openChest?.Contents;
		return null;
	}

	private void MoveWithWeapon(InventorySlotUi from, InventorySlotUi to)
	{
		if (from.ContainerId == "weapon" && to.ContainerId == "player")
		{
			var dst = _player.Inventory.Get(to.SlotIndex);
			if (dst == null) return;
			string wid = _player.EquippedWeaponId;
			if (string.IsNullOrEmpty(wid)) return;
			if (dst.IsEmpty)
			{
				dst.ItemId = wid; dst.Count = 1;
				_player.EquippedWeaponId = null;
			}
			else
			{
				var def = ItemCatalog.Get(dst.ItemId);
				if (def != null && def.IsWeapon)
				{
					string tmp = dst.ItemId;
					dst.ItemId = wid; dst.Count = 1;
					_player.EquippedWeaponId = tmp;
				}
			}
		}
		else if (from.ContainerId == "player" && to.ContainerId == "weapon")
		{
			var src = _player.Inventory.Get(from.SlotIndex);
			if (src == null || src.IsEmpty) return;
			var def = ItemCatalog.Get(src.ItemId);
			if (def == null || !def.IsWeapon) return;
			string wid = _player.EquippedWeaponId;
			if (string.IsNullOrEmpty(wid))
			{
				_player.EquippedWeaponId = src.ItemId;
				src.ItemId = null; src.Count = 0;
			}
			else
			{
				src.ItemId = wid; src.Count = 1;
				_player.EquippedWeaponId = def.Id;
			}
		}
		else if (from.ContainerId == "weapon" && to.ContainerId == "chest" && _openChest != null)
		{
			var dst = _openChest.Contents.Get(to.SlotIndex);
			string wid = _player.EquippedWeaponId;
			if (dst == null || string.IsNullOrEmpty(wid)) return;
			if (dst.IsEmpty)
			{
				dst.ItemId = wid; dst.Count = 1; _player.EquippedWeaponId = null;
			}
			else
			{
				var def = ItemCatalog.Get(dst.ItemId);
				if (def != null && def.IsWeapon)
				{
					string tmp = dst.ItemId;
					dst.ItemId = wid; dst.Count = 1; _player.EquippedWeaponId = tmp;
				}
			}
		}
		else if (from.ContainerId == "chest" && to.ContainerId == "weapon" && _openChest != null)
		{
			var src = _openChest.Contents.Get(from.SlotIndex);
			if (src == null || src.IsEmpty) return;
			var def = ItemCatalog.Get(src.ItemId);
			if (def == null || !def.IsWeapon) return;
			string wid = _player.EquippedWeaponId;
			if (string.IsNullOrEmpty(wid))
			{
				_player.EquippedWeaponId = src.ItemId;
				src.ItemId = null; src.Count = 0;
			}
			else
			{
				src.ItemId = wid; src.Count = 1; _player.EquippedWeaponId = def.Id;
			}
		}
	}

	private void OnTakeAll()
	{
		if (_player?.Inventory == null || _openChest?.Contents == null) return;
		var chest = _openChest.Contents;

		// Coins go directly to wallet.
		for (int i = 0; i < chest.Slots.Length; i++)
		{
			var s = chest.Slots[i];
			if (s.IsEmpty || s.ItemId != "coin") continue;
			_player.AddGold(s.Count);
			s.ItemId = null; s.Count = 0;
		}

		// First pass for non-coin items.
		for (int i = 0; i < chest.Slots.Length; i++)
		{
			var s = chest.Slots[i];
			if (s.IsEmpty) continue;
			if (_player.Inventory.AddItem(s.ItemId, s.Count))
			{
				s.ItemId = null; s.Count = 0;
			}
		}
		// Second pass to move remaining partial/non-stacked items where possible.
		CompactTakeAll();
		_player.ConvertInventoryCoinsToGold();
		Refresh();
	}

	private void CompactTakeAll()
	{
		if (_player?.Inventory == null || _openChest?.Contents == null) return;
		var chest = _openChest.Contents;
		for (int i = 0; i < chest.Slots.Length; i++)
		{
			var s = chest.Slots[i];
			if (s.IsEmpty) continue;
			TryMoveAllFromChestSlot(i);
		}
	}

	private void TryMoveAllFromChestSlot(int idx)
	{
		var chest = _openChest.Contents;
		var s = chest.Slots[idx];
		if (s.IsEmpty) return;
		if (s.ItemId == "coin")
		{
			_player.AddGold(s.Count);
			s.ItemId = null; s.Count = 0;
			return;
		}
		var def = ItemCatalog.Get(s.ItemId);
		if (def == null) return;

		if (def.Stackable)
		{
			// merge into existing stacks
			for (int i = 0; i < _player.Inventory.Slots.Length && !s.IsEmpty; i++)
			{
				var ps = _player.Inventory.Slots[i];
				if (!ps.IsEmpty && ps.ItemId == s.ItemId && ps.Count < def.MaxStack)
				{
					int move = System.Math.Min(s.Count, def.MaxStack - ps.Count);
					ps.Count += move; s.Count -= move;
					if (s.Count <= 0) { s.ItemId = null; s.Count = 0; }
				}
			}
		}
		// move to empty slots
		for (int i = 0; i < _player.Inventory.Slots.Length && !s.IsEmpty; i++)
		{
			var ps = _player.Inventory.Slots[i];
			if (ps.IsEmpty)
			{
				int put = def.Stackable ? System.Math.Min(s.Count, def.MaxStack) : 1;
				ps.ItemId = s.ItemId; ps.Count = put;
				s.Count -= put;
				if (s.Count <= 0) { s.ItemId = null; s.Count = 0; }
				if (!def.Stackable) break;
			}
		}
	}

	public void Refresh()
	{
		if (_player == null || !IsInstanceValid(_player)) _player = GetTree().GetFirstNodeInGroup("player") as PlayerEntity;
		if (_player != null)
		{
			_player.ConvertInventoryCoinsToGold();
			_goldLabel.Text = $"Coins: {_player.Gold}";
			var w = string.IsNullOrEmpty(_player.EquippedWeaponId) ? new ItemStack() : new ItemStack { ItemId = _player.EquippedWeaponId, Count = 1 };
			_weaponSlot.SetStack(w);
			for (int i = 0; i < _playerSlots.Length; i++) _playerSlots[i].SetStack(_player.Inventory?.Get(i));
		}

		_chestPanel.Visible = _openChest != null && IsInstanceValid(_openChest);
		if (_openChest != null && IsInstanceValid(_openChest))
		{
			for (int i = 0; i < _chestSlots.Length; i++) _chestSlots[i].SetStack(_openChest.Contents.Get(i));
		}

		RefreshUpgrades();
		HighlightSelected();
		_hintLabel.Text = _openChest != null
			? "Inventory/Cancel: Close | Interact: Close chest | D-pad/Stick + Accept to select/move, Take All for loot"
			: "Inventory: Open/Close | Interact: Open chest | D-pad/Stick + Accept to select/move/swap";
	}

	private void RefreshUpgrades()
	{
		if (_upgradesBox == null || _upgradesHeader == null) return;
		foreach (Node child in _upgradesBox.GetChildren())
			child.QueueFree();

		if (_player == null || !IsInstanceValid(_player))
		{
			_upgradesHeader.Visible = false;
			_upgradesBox.Visible = false;
			return;
		}

		bool any = false;
		foreach (var def in GridQuest.PlayerUpgrades.UpgradeCatalog.All)
		{
			int lvl = _player.GetUpgradeLevel(def.Id);
			if (lvl <= 0) continue;
			any = true;
			var lbl = new Label { Text = $"{def.Name}: Lv. {lvl}" };
			lbl.AddThemeFontSizeOverride("font_size", 12);
			_upgradesBox.AddChild(lbl);
		}
		_upgradesHeader.Visible = any;
		_upgradesBox.Visible = any;
	}

	private void HighlightSelected()
	{
		_weaponSlot.SetHighlight(_selected == _weaponSlot);
		foreach (var s in _playerSlots) s.SetHighlight(_selected == s);
		foreach (var s in _chestSlots) s.SetHighlight(_selected == s);
	}

	public override bool _CanDropData(Vector2 atPosition, Variant data)
	{
		return data.VariantType == Variant.Type.Dictionary;
	}

	public override void _DropData(Vector2 atPosition, Variant data)
	{
		// handled by slots via script? fallback not needed; selection system used
	}

	public void NotifySlotDragDrop(InventorySlotUi from, InventorySlotUi to)
	{
		if (from != null && to != null) MoveBetween(from, to);
		_selected = null;
		Refresh();
	}
}
