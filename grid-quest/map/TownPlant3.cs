using Godot;

namespace GridQuest.Map;

public static class TownPlant3
{
	public const string TexturePath = "res://assets/images/map_tiles/plants/town_plant-3.png";

	public static Node2D Create(Vector2 worldPosition)
	{
		return new Sprite2D
		{
			Name = "TownPlant3",
			Texture = GD.Load<Texture2D>(TexturePath),
			TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
			Position = worldPosition,
			ZIndex = 0,
		};
	}
}
