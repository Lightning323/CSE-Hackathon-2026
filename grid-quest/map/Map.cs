using Godot;
using System;
using GridQuest.Map.MapFloors;
using GridQuest.Map.MapObjects;
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
		AddChild(FloorLayer(100, 100));
		AddChild(ObjectLayer(100, 100));
	}


	public static Node2D FloorLayer(int width, int height)
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

	private static Node2D ObjectLayer(int width, int height)
	{
		Node2D itemsLayer = new() { Name = "Objects" };


		var rng = new RandomNumberGenerator();

		for (int y = 0; y < height; y++)
		{
			for (int x = 0; x < width; x++)
			{


				if (MiscUtils.isCheckerboard(x, y) && rng.RandiRange(0, 100) < 1)
				{
					Node2D tree = MapObjects.TreeFactory.Create(x, y);
					itemsLayer.AddChild(tree);
				}
				else if (!MiscUtils.isCheckerboard(x, y) && rng.RandiRange(0, 10) < 5)
				{
					Node2D plant = MapObjects.PlantFactory.CreateVariant(x, y, rng.RandiRange(1, 3));
					itemsLayer.AddChild(plant);
				}
			}
		}
		return itemsLayer;
	}


	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}
}
