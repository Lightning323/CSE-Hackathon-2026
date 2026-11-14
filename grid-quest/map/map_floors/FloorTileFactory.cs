using Godot;

namespace GridQuest.Map.MapFloors;

public enum FloorTileType
{
	Grass,
	Stone,
}

public static class FloorTileMapFactory
{
	private static TileSet _tileSet;
	private static int _grassSourceId = -1;
	private static int _stoneSourceId = -1;

	public static TileSet GetOrCreateTileSet()
	{
		if (_tileSet != null)
			return _tileSet;

		_tileSet = new TileSet
		{
			TileSize = new Vector2I(MapConstants.CELL_SIZE, MapConstants.CELL_SIZE),
		};

		_grassSourceId = AddSource(_tileSet, "res://assets/images/map_tiles/floor/grass.png");
		_stoneSourceId = AddSource(_tileSet, "res://assets/images/map_tiles/floor/stone.png");
		return _tileSet;
	}

	private static int AddSource(TileSet tileSet, string texturePath)
	{
		var texture = GD.Load<Texture2D>(texturePath);
		var source = new TileSetAtlasSource
		{
			Texture = texture,
			TextureRegionSize = new Vector2I(MapConstants.CELL_SIZE, MapConstants.CELL_SIZE),
		};
		int sourceId = tileSet.AddSource(source);
		source.CreateTile(Vector2I.Zero);
		return sourceId;
	}

	public static TileMapLayer CreateFloorLayer(string name = "Floor")
	{
		return new TileMapLayer
		{
			Name = name,
			TileSet = GetOrCreateTileSet(),
			TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
			ZIndex = -10,
			YSortEnabled = false,
		};
	}

	public static void SetFloorCell(TileMapLayer layer, int x, int y, FloorTileType type)
	{
		if (layer == null)
			return;
		GetOrCreateTileSet();
		int sourceId = type == FloorTileType.Stone ? _stoneSourceId : _grassSourceId;
		layer.SetCell(new Vector2I(x, y), sourceId, Vector2I.Zero);
	}
}
