using Godot;

public partial class InventoryUi : Control
{
	private Label _goldLabel;
	private Button _weaponSlotBtn;
	private TextureRect _weaponIcon;
	private TextureRect[] _icons = new TextureRect[16];
	private Button[] _slotButtons = new Button[16];
	private string _swordLocation = "weapon";
	private string _selectedSource = null; // null or "weapon" or "0".."15"
	private static readonly Texture2D SwordTex = GD.Load<Texture2D>("res://assets/images/items/meta_sword.png");

	public override void _Ready()
	{
		AddToGroup("inventory_ui");
		_goldLabel = GetNode<Label>("Panel/VBox/GoldLabel");
		_weaponSlotBtn = GetNode<Button>("Panel/VBox/WeaponSlot");
		_weaponIcon = GetNode<TextureRect>("Panel/VBox/WeaponSlot/WeaponIcon");
		var grid = GetNode<GridContainer>("Panel/VBox/GridContainer");
		for (int i = 0; i < 16; i++)
		{
			_slotButtons[i] = grid.GetNode<Button>($"Slot{i}");
			_icons[i] = _slotButtons[i].GetNode<TextureRect>($"Icon{i}");
			int idx = i;
			_slotButtons[i].Pressed += () => OnInvSlotPressed(idx);
		}
		_weaponSlotBtn.Pressed += OnWeaponSlotPressed;
		Visible = false;
		RefreshSlots();
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		bool toggle = false;
		if (@event is InputEventKey key && key.Pressed && !key.Echo && key.Keycode == Key.E) toggle = true;
		if (@event is InputEventJoypadButton jb && jb.Pressed && jb.ButtonIndex == JoyButton.Y) toggle = true;
		if (toggle)
		{
			Visible = !Visible;
			if (Visible) { UpdateDisplay(); _selectedSource = null; }
			GetViewport().SetInputAsHandled();
		}
	}

	public void UpdateDisplay()
	{
		var playerNode = GetTree().GetFirstNodeInGroup("player");
		int gold = 0;
		if (playerNode is Player p)
		{
			gold = p.Gold;
			if (!p.HasWeaponEquipped && _swordLocation == "weapon") _swordLocation = "0";
			if (p.HasWeaponEquipped) _swordLocation = "weapon";
		}
		_goldLabel.Text = $"Coins: {gold}";
		RefreshSlots();
	}

	private void OnWeaponSlotPressed()
	{
		HandleSlotClick("weapon");
	}

	private void OnInvSlotPressed(int idx)
	{
		HandleSlotClick(idx.ToString());
	}

	private void HandleSlotClick(string loc)
	{
		if (_selectedSource == null)
		{
			// select if this slot has sword
			if (_swordLocation == loc)
			{
				_selectedSource = loc;
			}
		}
		else
		{
			// move sword to clicked location
			_swordLocation = loc;
			_selectedSource = null;
			ApplyWeaponState();
		}
		RefreshSlots();
	}

	private void ApplyWeaponState()
	{
		var playerNode = GetTree().GetFirstNodeInGroup("player");
		if (playerNode is Player p)
		{
			p.HasWeaponEquipped = (_swordLocation == "weapon");
		}
	}

	private void RefreshSlots()
	{
		// weapon slot
		_weaponIcon.Visible = (_swordLocation == "weapon");
		if (_selectedSource == "weapon")
			_weaponSlotBtn.Modulate = new Color(1f, 1f, 0.4f); // highlighted selected
		else
			_weaponSlotBtn.Modulate = Colors.White;

		for (int i = 0; i < 16; i++)
		{
			bool hasSword = _swordLocation == i.ToString();
			_icons[i].Texture = hasSword ? SwordTex : null;
			_icons[i].Visible = hasSword;
			if (_selectedSource == i.ToString())
				_slotButtons[i].Modulate = new Color(1f, 1f, 0.4f);
			else
				_slotButtons[i].Modulate = Colors.White;
		}
	}
}
