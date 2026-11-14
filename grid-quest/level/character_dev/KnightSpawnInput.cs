using Godot;

public partial class KnightSpawnInput : Node
{
	private static readonly PackedScene KnightScene = GD.Load<PackedScene>("res://npc/knight_npc.tscn");

	public override void _UnhandledInput(InputEvent @event)
	{
		bool pressed = false;
		if (@event is InputEventKey keyEvent && keyEvent.Pressed && !keyEvent.Echo && keyEvent.Keycode == Key.G) pressed = true;
		if (@event is InputEventJoypadButton jb && jb.Pressed && jb.ButtonIndex == JoyButton.LeftShoulder) pressed = true;
		if (!pressed) return;

		Node2D player = GetTree().GetFirstNodeInGroup("player") as Node2D;
		if (player == null)
			return;

		Node instance = KnightScene.Instantiate();
		if (instance is Node2D knight)
		{
			Vector2 offset = new(GD.Randf() * 40.0f - 20.0f, GD.Randf() * 40.0f - 20.0f);
			knight.GlobalPosition = player.GlobalPosition + offset;
			GetTree().CurrentScene.AddChild(knight);
			GetViewport().SetInputAsHandled();
		}
		else
		{
			instance.QueueFree();
		}
	}
}
