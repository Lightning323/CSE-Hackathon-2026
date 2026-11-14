using Godot;

namespace GridQuest.Map.MapObjects;

public abstract partial class MapObjectFactory : Node2D
{
	[Export] public bool BlocksMovement { get; set; }

	public virtual int FootprintWidthCells => 1;
	public virtual int FootprintHeightCells => 1;
}
