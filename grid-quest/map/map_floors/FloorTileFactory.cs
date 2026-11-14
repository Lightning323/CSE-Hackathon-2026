using Godot;
using GridQuest.Map;

namespace GridQuest.Map.MapFloors;


public class FloorTileFactory
{
	

	private Texture2D floorTexture;

	public FloorTileFactory(string floorTexturePath)
	{
		this.floorTexture = GD.Load<Texture2D>(floorTexturePath);
		int texW = floorTexture.GetWidth();
		int texH = floorTexture.GetHeight();
	}

	public Node2D MakeTile(int x, int y)
	{
		Sprite2D tile = new()
		{
			Texture = floorTexture,
			Centered = false,
			Position = new Vector2(x * MapConstants.CELL_SIZE, y * MapConstants.CELL_SIZE),
			TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
			ZIndex = -10,
		};
		return tile;
	}
}
