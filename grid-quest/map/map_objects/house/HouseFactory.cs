using Godot;

namespace GridQuest.Map.MapObjects;

public partial class HouseFactory : MapObjectFactory
{
	public const string ScenePath = "res://map/map_objects/house/house.tscn";
	private static readonly PackedScene Scene = GD.Load<PackedScene>(ScenePath);

	public static HouseFactory Instantiate(Vector2 worldPosition)
	{
		var house = Scene.Instantiate<HouseFactory>();
		house.Position = worldPosition;
		return house;
	}

	public static HouseFactory Create(int x, int y)
	{
		return Instantiate(new Vector2(x * MapConstants.CELL_SIZE + MapConstants.CELL_SIZE / 2.0f, y * MapConstants.CELL_SIZE + MapConstants.CELL_SIZE));
	}
}
