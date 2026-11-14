using Godot;
using GridQuest.Map;

namespace GridQuest.Map.MapObjects;

public partial class PlantFactory : MapObjectFactory
{
	public const string Plant1TexturePath = "res://assets/images/map_tiles/plants/town_plant-1.png";
	public const string Plant2TexturePath = "res://assets/images/map_tiles/plants/town_plant-2.png";
	public const string Plant3TexturePath = "res://assets/images/map_tiles/plants/town_plant-3.png";

	private PlantFactory Create(int x, int y, string texturePath, string name)
	{
		Texture2D texture = GD.Load<Texture2D>(texturePath);
		var plant = new PlantFactory
		{
			Name = name,
			Position = new Vector2(x * MapConstants.CELL_SIZE, y * MapConstants.CELL_SIZE),
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

	public PlantFactory CreateVariant(int x,int y,  int variant)
	{
		return variant switch
		{
			1 => Create(x,y, Plant1TexturePath, "TownPlant1"),
			2 => Create(x,y, Plant2TexturePath, "TownPlant2"),
			_ => Create(x,y, Plant3TexturePath, "TownPlant3"),
		};
	}
}
