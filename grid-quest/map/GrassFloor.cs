using Godot;

namespace GridQuest.Map;

public static class GrassFloor
{
	public const string TexturePath = "res://assets/images/map_tiles/grass/grass.png";

	public static Node2D CreateLayer(int width, int height, int cellSize)
	{
		Node2D floorLayer = new() { Name = "Floor" };
		Texture2D grass = GD.Load<Texture2D>(TexturePath);
		int texW = grass.GetWidth();
		int texH = grass.GetHeight();
		int mapPixelW = width * cellSize;
		int mapPixelH = height * cellSize;

		for (int y = 0; y < mapPixelH; y += texH)
		{
			for (int x = 0; x < mapPixelW; x += texW)
			{
				Sprite2D tile = new()
				{
					Texture = grass,
					Centered = false,
					Position = new Vector2(x, y),
					TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
					ZIndex = -10,
				};
				floorLayer.AddChild(tile);
			}
		}

		return floorLayer;
	}
}
