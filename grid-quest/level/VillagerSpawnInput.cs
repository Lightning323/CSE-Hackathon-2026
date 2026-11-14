using Godot;

public partial class VillagerSpawnInput : Node
{
	private static readonly PackedScene VillagerScene = GD.Load<PackedScene>("res://npc/villager/villager.tscn");

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event is not InputEventKey keyEvent || !keyEvent.Pressed || keyEvent.Echo || keyEvent.Keycode != Key.V)
		{
			return;
		}

		Node instance = VillagerScene.Instantiate();
		if (instance is not Node2D villager)
		{
			instance.QueueFree();
			return;
		}

		Node2D parent = GetTree().CurrentScene as Node2D ?? GetParent<Node2D>();
		Node2D player = GetTree().GetFirstNodeInGroup("player") as Node2D;
		Vector2 spawnPos = player?.GlobalPosition ?? Vector2.Zero;

		Node grid = parent.GetNodeOrNull("MapGrid");
		if (grid != null && grid.HasMethod("CellToWorldCenter"))
		{
			int width = grid.Get("Width").AsInt32();
			int height = grid.Get("Height").AsInt32();
			if (width > 0 && height > 0)
			{
				for (int i = 0; i < 12; i++)
				{
					int x = GD.RandRange(0, width - 1);
					int y = GD.RandRange(0, height - 1);
					Vector2 candidate = (Vector2)grid.Call("CellToWorldCenter", x, y);
					if (player == null || candidate.DistanceTo(player.GlobalPosition) >= 32.0f)
					{
						spawnPos = candidate;
						break;
					}
				}
			}
		}
		else if (player != null)
		{
			float angle = (float)GD.RandRange(0.0, Mathf.Tau);
			float dist = (float)GD.RandRange(24.0, 64.0);
			spawnPos = player.GlobalPosition + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * dist;
		}

		villager.GlobalPosition = spawnPos;
		parent.AddChild(villager);
		GetViewport().SetInputAsHandled();
	}
}
