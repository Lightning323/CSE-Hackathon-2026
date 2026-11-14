using Godot;

public partial class InventoryUi : Control
{
	private Label _goldLabel;
	private Button _weaponSlotBtn;
	private TextureRect _weaponIcon;
	private TextureRect[] _icons = new TextureRect[16];
	private Button[] _slotButtons = new Button[16];
	private string _swordLocation = "weapon";
	private string _fireStaffLocation = "1";
	private string _selectedSource = null;
	private static readonly Texture2D SwordTex = GD.Load<Texture2D>("res://assets/images/items/base_sword.png");
	private static readonly Texture2D StaffTex = GD.Load<Texture2D>("res://assets/images/items/fire-staff.png");

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
		if (playerNode is Player p) gold = p.Gold;
		_goldLabel.Text = $"Coins: {gold}";
		RefreshSlots();
	}

	private void OnWeaponSlotPressed() => HandleSlotClick("weapon");
	private void OnInvSlotPressed(int idx) => HandleSlotClick(idx.ToString());

	private string GetItemAt(string loc)
	{
		if (_swordLocation == loc) return "sword";
		if (_fireStaffLocation == loc) return "staff";
		return null;
	}

	private void HandleSlotClick(string loc)
	{
		if (_selectedSource == null)
		{
			string item = GetItemAt(loc);
			if (item != null) _selectedSource = item + "_" + loc;
		}
		else
		{
			string[] parts = _selectedSource.Split('_');
			string selItem = parts[0];
			string selLoc = string.Join("_", parts[1..]); // in case
			// if clicking same source, deselect
			if (selLoc == loc) { _selectedSource = null; RefreshSlots(); return; }
			// if target occupied by different item, block
			string targetItem = GetItemAt(loc);
			if (targetItem != null && targetItem != selItem) { return; } // blocked
			// move
			if (selItem == "sword") _swordLocation = loc;
			else if (selItem == "staff") _fireStaffLocation = loc;
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
			if (_swordLocation == "weapon") p.EquippedWeapon = "sword";
			else if (_fireStaffLocation == "weapon") p.EquippedWeapon = "fire_staff";
			else p.EquippedWeapon = "none";
		}
	}

	private void RefreshSlots()
	{
		string wItem = GetItemAt("weapon");
		_weaponIcon.Texture = wItem == "sword" ? SwordTex : (wItem == "staff" ? StaffTex : null);
		_weaponIcon.Visible = wItem != null;
		_weaponSlotBtn.Modulate = (_selectedSource != null && _selectedSource.EndsWith("_weapon")) ? new Color(1f, 1f, 0.4f) : Colors.White;

		for (int i = 0; i < 16; i++)
		{
			string item = GetItemAt(i.ToString());
			_icons[i].Texture = item == "sword" ? SwordTex : (item == "staff" ? StaffTex : null);
			_icons[i].Visible = item != null;
			_slotButtons[i].Modulate = (_selectedSource != null && _selectedSource.EndsWith("_" + i)) ? new Color(1f, 1f, 0.4f) : Colors.White;
		}
	}
}
