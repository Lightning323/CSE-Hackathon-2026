using Godot;

public partial class InventoryUi : Control
{
	private Label _goldLabel;
	private Label _weaponLabel;

	public override void _Ready()
	{
		_goldLabel = GetNode<Label>("Panel/VBox/GoldLabel");
		_weaponLabel = GetNode<Label>("Panel/VBox/WeaponLabel");
		Visible = false;
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event is InputEventKey key && key.Pressed && !key.Echo && key.Keycode == Key.E)
		{
			Visible = !Visible;
			if (Visible)
			{
				UpdateDisplay();
			}
			GetViewport().SetInputAsHandled();
		}
	}

	public void UpdateDisplay()
	{
		var player = GetTree().GetFirstNodeInGroup("player");
		int gold = 0;
		if (player != null && player.HasMethod("get_Gold"))
		{
			gold = (int)player.Get("Gold");
		}
		else if (player is Player p)
		{
			gold = p.Gold;
		}
		_goldLabel.Text = $"Coins: {gold}";
		_weaponLabel.Text = "Weapon Slot: (empty)";
	}
}
