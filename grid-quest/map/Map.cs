using Godot;
using System;
using GridQuest.Map.MapFloors;
using GridQuest.Map.MapObjects;
using GridQuest.Utils;

public partial class Map : Node2D
{
	public const int Width = 200;
	public const int Height = 200;


	public override void _Ready()
	{
		YSortEnabled = true;
		GD.Print("Map is ready");
		GenerateMap();
	}

	public void GenerateMap()
	{
		GD.Print("Generating map...");
		AddChild(FloorLayer(Width, Height));
		AddChild(ObjectLayer(Width, Height));
	}

	public static Node2D FloorLayer(int width, int height)
	{
		Node2D floorLayer = new() { Name = "Floor" };

		for (int y = 0; y < height; y++)
		{
			for (int x = 0; x < width; x++)
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
		Node2D objectsRoot = new() { Name = "Objects", YSortEnabled = true };
		Node2D plantsLayer = new() { Name = "Plants", YSortEnabled = false };
		Node2D treesLayer = new() { Name = "Trees", YSortEnabled = true };
		var rng = new RandomNumberGenerator();

		for (int y = 0; y < height; y++)
		{
			

			for (int x = 0; x < width; x++)
			{
				if(x == 0 || y == 0 || x == width - 1 || y == height - 1)
				{
					objectsRoot.AddChild(WallFactory.Create(x, y));
				}
			
				

				if (MiscUtils.isCheckerboard(x, y) && rng.RandiRange(0, 100) < 1)
				{
					treesLayer.AddChild(TreeFactory.Create(x, y));
				}
				if (!MiscUtils.isCheckerboard(x, y) && rng.RandiRange(0, 10) < 3)
				{
					plantsLayer.AddChild(PlantFactory.Create(x, y, rng.RandiRange(1, 3)));
				}
			}
		}
		objectsRoot.AddChild(plantsLayer);
		objectsRoot.AddChild(treesLayer);
		return objectsRoot;
	}
}
