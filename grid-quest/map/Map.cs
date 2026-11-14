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
		GenerateBuildings();
	}

	public Node2D GenerateBuilding(
		int originX,
		int originY,
		int width,
		int height,
		GridQuest.Map.WallFeature north = GridQuest.Map.WallFeature.Solid,
		GridQuest.Map.WallFeature south = GridQuest.Map.WallFeature.Solid,
		GridQuest.Map.WallFeature east = GridQuest.Map.WallFeature.Solid,
		GridQuest.Map.WallFeature west = GridQuest.Map.WallFeature.Solid,
		bool gatesOpen = false,
		bool includeFloor = true)
	{
		var building = GridQuest.Map.RoomGenerator.Create(originX, originY, width, height, north, south, east, west, gatesOpen, includeFloor);
		building.Name = $"Building_{originX}_{originY}";
		AddChild(building);
		return building;
	}

	private void GenerateBuildings()
	{
		GenerateBuilding(20, 20, 8, 6, north: GridQuest.Map.WallFeature.Solid, south: GridQuest.Map.WallFeature.Gate, east: GridQuest.Map.WallFeature.Solid, west: GridQuest.Map.WallFeature.Solid);
		GenerateBuilding(40, 18, 10, 8, north: GridQuest.Map.WallFeature.Opening, south: GridQuest.Map.WallFeature.Solid, east: GridQuest.Map.WallFeature.Gate, west: GridQuest.Map.WallFeature.Solid);
		GenerateBuilding(25, 40, 12, 7, north: GridQuest.Map.WallFeature.Solid, south: GridQuest.Map.WallFeature.Gate, east: GridQuest.Map.WallFeature.Solid, west: GridQuest.Map.WallFeature.Opening, gatesOpen: true);
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
