using Godot;

namespace GridQuest.Map.MapObjects;

public static class TreeFactory
{
	public static Node2D Create(int x, int y)
	{
		return TownTree2.Create(x, y);
	}
}
