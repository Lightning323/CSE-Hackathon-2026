using Godot;

namespace GridQuest.Map.MapObjects;

public partial class TownPlant : MapObject
{
	public const string Plant1TexturePath = "res://assets/images/map_tiles/plants/town_plant-1.png";
	public const string Plant2TexturePath = "res://assets/images/map_tiles/plants/town_plant-2.png";
	public const string Plant3TexturePath = "res://assets/images/map_tiles/plants/town_plant-3.png";

	public static TownPlant Create(Vector2 worldPosition, int cellSize, string texturePath, string name)
	{
		Texture2D texture = GD.Load<Texture2D>(texturePath);
		var plant = new TownPlant
		{
			Name = name,
			Position = worldPosition,
			BlocksMovement = false,
		};

		var sprite = new Sprite2D
		{
			Name = "Sprite",
			Texture = texture,
			TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
			Centered = true,
			Offset = new Vector2(0, -texture.GetHeight() / 2.0f),
		};

		plant.AddChild(sprite);
		return plant;
	}

	public static TownPlant CreateVariant(Vector2 worldPosition, int cellSize, int variant)
	{
		return variant switch
		{
			1 => Create(worldPosition, cellSize, Plant1TexturePath, "TownPlant1"),
			2 => Create(worldPosition, cellSize, Plant2TexturePath, "TownPlant2"),
			_ => Create(worldPosition, cellSize, Plant3TexturePath, "TownPlant3"),
		};
	}
}
