using Godot;

namespace GridQuest.Map.MapObjects;

public partial class TreeFactory : MapObjectFactory
{
	public const string TexturePath = "res://assets/images/map_tiles/trees/town_tree-2.png";

	public TreeFactory Create(int x, int y)
	{
		Texture2D texture = GD.Load<Texture2D>(TexturePath);
		var tree = new TreeFactory
		{
			Name = "TownTree",
			Position = new Vector2(x * MapConstants.CELL_SIZE, y * MapConstants.CELL_SIZE),
			BlocksMovement = true,
			YSortEnabled = false,
		};

		var visual = new Node2D { Name = "Visual" };
		var sprite = new Sprite2D
		{
			Texture = texture,
			TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
			Centered = true,
			Offset = new Vector2(0, -texture.GetHeight() / 2.0f),
		};
		visual.AddChild(sprite);

		var body = new StaticBody2D { Name = "CollisionBody" };
		var collision = new CollisionShape2D
		{
			Shape = new RectangleShape2D { Size = new Vector2(14, 10) },
			Position = new Vector2(0, -5),
		};
		body.AddChild(collision);

		tree.AddChild(visual);
		tree.AddChild(body);
		return tree;
	}
}
