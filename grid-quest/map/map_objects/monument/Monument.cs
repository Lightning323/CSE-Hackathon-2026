#nullable enable
using Godot;

public partial class Monument : Node2D
{
	[Export] public float InteractRange { get; set; } = 64.0f;
	[Export] public float DiscoveryRange { get; set; } = 96.0f;

	public bool Discovered { get; private set; }

	private Sprite2D? _sprite;
	private bool _playerNear;

	public override void _Ready()
	{
		YSortEnabled = true;
		_sprite = GetNodeOrNull<Sprite2D>("Sprite2D");
		AddToGroup("monument");
		AddToGroup("interactables");
	}

	public override void _Process(double delta)
	{
		var player = GetTree().GetFirstNodeInGroup("player") as Node2D;
		if (player == null) { SetHighlight(false); return; }
		float dist = GlobalPosition.DistanceTo(player.GlobalPosition);
		bool near = dist <= InteractRange;
		if (dist <= DiscoveryRange)
			Discovered = true;
		SetHighlight(near);
		_playerNear = near;
	}

	public override void _Input(InputEvent @event)
	{
		if (!_playerNear) return;
		bool interactPressed = @event.IsActionPressed("interact");
		if (!interactPressed && @event is InputEventKey key && key.Pressed && !key.Echo && key.Keycode == Key.F)
			interactPressed = true;
		if (!interactPressed)
			return;

		var inventoryUi = GetTree().GetFirstNodeInGroup("inventory_ui") as InventoryUi;
		if (inventoryUi != null && inventoryUi.Visible)
			return;
		if (IsPlayerNearChest())
			return;

		Discovered = true;
		var skills = GetTree().GetFirstNodeInGroup("skills_ui") as Control;
		if (skills != null && skills.HasMethod("Open"))
		{
			skills.Call("Open");
			GetViewport().SetInputAsHandled();
		}
	}

	private bool IsPlayerNearChest()
	{
		var player = GetTree().GetFirstNodeInGroup("player") as Node2D;
		if (player == null) return false;
		foreach (var node in GetTree().GetNodesInGroup("chests"))
		{
			if (node is Node2D chest && IsInstanceValid(chest) && player.GlobalPosition.DistanceTo(chest.GlobalPosition) <= 40.0f)
				return true;
		}
		return false;
	}

	private void SetHighlight(bool on)
	{
		if (_sprite == null) return;
		_sprite.Modulate = on ? new Color(1.3f, 1.3f, 1.3f) : Colors.White;
	}
}
