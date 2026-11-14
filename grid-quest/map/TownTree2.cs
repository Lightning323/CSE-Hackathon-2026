using Godot;

namespace GridQuest.Map;

public static class TownTree2
{
	public const string TexturePath = "res://assets/images/map_tiles/trees/town_tree-2.png";

	public static Node2D Create(Vector2 worldPosition, int cellSize)
	{
		Texture2D texture = GD.Load<Texture2D>(TexturePath);
		var tree = new StaticBody2D
		{
			Name = "Tree",
			Position = worldPosition,
			ZIndex = 1,
		};

		var sprite = new Sprite2D
		{
			Texture = texture,
			TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
			Centered = true,
			Offset = new Vector2(0, -texture.GetHeight() / 2.0f + cellSize / 2.0f),
		};

		var collision = new CollisionShape2D
		{
			Shape = new RectangleShape2D { Size = new Vector2(12, 8) },
			Position = new Vector2(0, 2),
		};

		tree.AddChild(sprite);
		tree.AddChild(collision);
		return tree;
	}
}
