using Godot;

namespace GridQuest.Map;

public static class TownPlant2
{
	public const string TexturePath = "res://assets/images/map_tiles/plants/town_plant-2.png";

	public static Node2D Create(Vector2 worldPosition)
	{
		return new Sprite2D
		{
			Name = "TownPlant2",
			Texture = GD.Load<Texture2D>(TexturePath),
			TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
			Position = worldPosition,
			ZIndex = 0,
		};
	}
}
