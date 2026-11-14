using Godot;

namespace GridQuest.Map.MapObjects;

public partial class TownTree2 : MapObject
{
	public const string TexturePath = "res://assets/images/map_tiles/trees/town_tree-2.png";

	public static TownTree2 Create(Vector2 worldPosition, int cellSize)
	{
		Texture2D texture = GD.Load<Texture2D>(TexturePath);
		var tree = new TownTree2
		{
			Name = "TownTree2",
			Position = worldPosition,
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
