using Godot;

namespace GridQuest.Map.MapObjects;

public partial class TreeFactory : MapObjectFactory
{
	public const string ScenePath = "res://map/map_objects/town_tree_2.tscn";
	private static readonly PackedScene Scene = GD.Load<PackedScene>(ScenePath);

	public static TreeFactory Instantiate(Vector2 worldPosition)
	{
		var tree = Scene.Instantiate<TreeFactory>();
		tree.Position = worldPosition;
		return tree;
	}

	public static TreeFactory Create(int x, int y)
	{
		return Instantiate(new Vector2(x * MapConstants.CELL_SIZE + MapConstants.CELL_SIZE / 2.0f, y * MapConstants.CELL_SIZE + MapConstants.CELL_SIZE));
	}

	public override void _Ready()
	{
		BlocksMovement = true;
		YSortEnabled = true;

		var sprite = GetNodeOrNull<Sprite2D>("Sprite2D");
		if (sprite?.Texture != null)
		{
			sprite.Offset = new Vector2(0, -sprite.Texture.GetHeight() / 2.0f);
		}
	}
}
