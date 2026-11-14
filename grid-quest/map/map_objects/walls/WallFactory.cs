using Godot;

namespace GridQuest.Map.MapObjects;

public partial class WallFactory : MapObjectFactory
{
	public const string ScenePath = "res://map/map_objects/walls/stone_wall.tscn";
	private static readonly PackedScene Scene = GD.Load<PackedScene>(ScenePath);

	public static WallFactory Instantiate(Vector2 worldPosition)
	{
		var wall = Scene.Instantiate<WallFactory>();
		//wall.ZIndex=1;
		wall.Position = worldPosition;
		return wall;
	}

	public static WallFactory Create(int x, int y)
	{
		return Instantiate(new Vector2(x * MapConstants.CELL_SIZE + MapConstants.CELL_SIZE / 2.0f, y * MapConstants.CELL_SIZE + MapConstants.CELL_SIZE));
	}

}
