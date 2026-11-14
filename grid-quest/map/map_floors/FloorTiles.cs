
using Godot;

namespace GridQuest.Map.MapFloors;

public static class FloorTiles
{
	public static TileMapLayer CreateLayer(string name = "Floor") => FloorTileMapFactory.CreateFloorLayer(name);

	public static void SetGrass(TileMapLayer layer, int x, int y) => FloorTileMapFactory.SetFloorCell(layer, x, y, FloorTileType.Grass);

	public static void SetStone(TileMapLayer layer, int x, int y) => FloorTileMapFactory.SetFloorCell(layer, x, y, FloorTileType.Stone);

	public static void SetPath(TileMapLayer layer, int x, int y) => SetStone(layer, x, y);
}
