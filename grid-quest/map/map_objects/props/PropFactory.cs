using Godot;

namespace GridQuest.Map.MapObjects;

public partial class PropFactory : MapObjectFactory
{
	public static PropFactory Create(
		int x,
		int y,
		string texturePath,
		string name,
		bool blocksMovement,
		Vector2 spriteOffset,
		Vector2? collisionSize = null,
		Vector2? collisionPosition = null,
		bool ySort = true)
	{
		Texture2D texture = GD.Load<Texture2D>(texturePath);
		var prop = new PropFactory
		{
			Name = name,
			Position = new Vector2(x * MapConstants.CELL_SIZE + MapConstants.CELL_SIZE / 2.0f, y * MapConstants.CELL_SIZE + MapConstants.CELL_SIZE),
			BlocksMovement = blocksMovement,
			YSortEnabled = ySort,
		};

		var sprite = new Sprite2D
		{
			Name = "Sprite",
			Texture = texture,
			TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
			Offset = spriteOffset,
		};
		prop.AddChild(sprite);

		if (blocksMovement && collisionSize.HasValue)
		{
			var body = new StaticBody2D { Name = "StaticBody2D" };
			var shape = new CollisionShape2D
			{
				Name = "CollisionShape2D",
				Position = collisionPosition ?? Vector2.Zero,
				Shape = new RectangleShape2D { Size = collisionSize.Value },
			};
			body.AddChild(shape);
			prop.AddChild(body);
		}

		return prop;
	}
}
