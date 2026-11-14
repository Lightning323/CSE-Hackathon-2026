using Godot;

namespace GridQuest.Map.MapObjects;

public partial class GateFactory : MapObjectFactory
{
	public const string ScenePath = "res://map/map_objects/gate/gate.tscn";
	public const string ClosedXTexturePath = "res://assets/images/map_tiles/gate/gate_closed_X.png";
	public const string ClosedYTexturePath = "res://assets/images/map_tiles/gate/gate_closed_Y.png";
	public const string OpenXTexturePath = "res://assets/images/map_tiles/gate/gate_open_X.png";
	public const string OpenYTexturePath = "res://assets/images/map_tiles/gate/gate_open_Y.png";

	private static readonly PackedScene Scene = GD.Load<PackedScene>(ScenePath);

	public enum GateAxis
	{
		Horizontal,
		Vertical,
	}

	[Export] public GateAxis Axis { get; set; } = GateAxis.Horizontal;
	[Export] public bool IsOpen { get; set; }

	public static GateFactory Instantiate(Vector2 worldPosition, GateAxis axis = GateAxis.Horizontal, bool isOpen = false)
	{
		var gate = Scene.Instantiate<GateFactory>();
		gate.Position = worldPosition;
		gate.Configure(axis, isOpen);
		return gate;
	}

	public static GateFactory Create(int x, int y, GateAxis axis = GateAxis.Horizontal, bool isOpen = false)
	{
		return Instantiate(
			new Vector2(x * MapConstants.CELL_SIZE + MapConstants.CELL_SIZE / 2.0f, y * MapConstants.CELL_SIZE + MapConstants.CELL_SIZE),
			axis,
			isOpen);
	}

	public void Configure(GateAxis axis, bool isOpen)
	{
		Axis = axis;
		IsOpen = isOpen;
		BlocksMovement = !isOpen;

		var sprite = GetNodeOrNull<Sprite2D>("Sprite2D");
		if (sprite != null)
		{
			sprite.Texture = GD.Load<Texture2D>(GetTexturePath(axis, isOpen));
		}

		var collision = GetNodeOrNull<CollisionShape2D>("StaticBody2D/CollisionShape2D");
		if (collision != null)
		{
			collision.Disabled = isOpen;
		}
	}

	private static string GetTexturePath(GateAxis axis, bool isOpen)
	{
		return (axis, isOpen) switch
		{
			(GateAxis.Horizontal, false) => ClosedXTexturePath,
			(GateAxis.Horizontal, true) => OpenXTexturePath,
			(GateAxis.Vertical, false) => ClosedYTexturePath,
			(GateAxis.Vertical, true) => OpenYTexturePath,
			_ => ClosedXTexturePath,
		};
	}
}
