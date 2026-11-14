using Godot;

public partial class VillagerSpawnInput : Node
{
	private static readonly PackedScene VillagerScene = GD.Load<PackedScene>("res://npc/villager/villager.tscn");

	public override void _UnhandledInput(InputEvent @event)
	{
		bool pressed = false;
		if (@event is InputEventKey keyEvent && keyEvent.Pressed && !keyEvent.Echo && keyEvent.Keycode == Key.V) pressed = true;
		if (@event is InputEventJoypadButton jb && jb.Pressed && jb.ButtonIndex == JoyButton.RightShoulder) pressed = true;
		if (!pressed) return;

		Node instance = VillagerScene.Instantiate();
		if (instance is not Villager villager)
		{
			instance.QueueFree();
			return;
		}

		Node2D parent = GetTree().CurrentScene as Node2D ?? GetParent<Node2D>();
		Node2D player = GetTree().GetFirstNodeInGroup("player") as Node2D;
		Map map = parent.GetNodeOrNull<Map>("Map");
		Vector2 spawnPos = player?.GlobalPosition ?? Vector2.Zero;
		int villageIndex = -1;

		if (map != null && map.Villages.Count > 0)
		{
			villageIndex = (int)(GD.Randi() % (uint)map.Villages.Count);
			spawnPos = map.GetRandomPointInVillage(villageIndex);
		}
		else if (player != null)
		{
			float angle = (float)GD.RandRange(0.0, Mathf.Tau);
			float dist = (float)GD.RandRange(24.0, 64.0);
			spawnPos = player.GlobalPosition + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * dist;
		}

		villager.GlobalPosition = spawnPos;
		if (map != null && villageIndex >= 0 && villageIndex < map.Villages.Count)
		{
			Map.VillageArea village = map.Villages[villageIndex];
			villager.SetHome(village.Bounds, village.Center, villageIndex);
		}

		parent.AddChild(villager);
		GetViewport().SetInputAsHandled();
	}
}
