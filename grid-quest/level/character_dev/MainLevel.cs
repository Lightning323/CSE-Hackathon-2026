using Godot;
using System;

public partial class MainLevel : Node2D
{
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		GD.Print("_Ready");
		YSortEnabled = true;
		CallDeferred(nameof(PlacePlayerAtRandomSpawn));
	}

	private void PlacePlayerAtRandomSpawn()
	{
		var player = GetNodeOrNull<Node2D>("Player");
		var map = GetNodeOrNull<Map>("Map");
		if (player == null || map == null)
			return;

		player.GlobalPosition = map.GetRandomPlayerSpawnPosition();
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}
}
