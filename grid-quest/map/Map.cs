using Godot;
using System;
using GridQuest.Map.MapFloors;
using GridQuest.Utils;

public partial class Map : Node2D
{
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		GD.Print("Map is ready");
		generateMap();
	}

	public void generateMap()
	{
		GD.Print("Generating map...");
		AddChild(CreateLayer(100, 100));
	}


	public static Node2D CreateLayer(int width, int height)
	{
		Node2D floorLayer = new() { Name = "Floor" };

		for (int y = 0; y < height; y += 1)
		{
			for (int x = 0; x < width; x += 1)
			{
				if (MiscUtils.isCheckerboard(x, y))
				{
					floorLayer.AddChild(FloorTiles.StoneFloorTile.MakeTile(x, y));
				}
				else
				{
					floorLayer.AddChild(FloorTiles.GrassFloorTile.MakeTile(x, y));
				}
			}
		}

		return floorLayer;
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}
}
