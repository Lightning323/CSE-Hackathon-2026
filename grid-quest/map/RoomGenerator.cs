using Godot;
using GridQuest.Map.MapObjects;

namespace GridQuest.Map;

public enum WallFeature
{
    Solid,
    Opening,
    Gate,
}

public static class RoomGenerator
{
    public static Node2D Create(
        int originX,
        int originY,
        int width,
        int height,
        WallFeature north = WallFeature.Solid,
        WallFeature south = WallFeature.Solid,
        WallFeature east = WallFeature.Solid,
        WallFeature west = WallFeature.Solid,
        bool gatesOpen = false,
        bool includeFloor = false)
    {
        var room = new Node2D
        {
            Name = "Room",
            YSortEnabled = true,
        };

        if (includeFloor)
        {
            var floor = new Node2D { Name = "Floor" };
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    floor.AddChild(MapFloors.FloorTiles.StoneFloorTile.MakeTile(originX + x, originY + y));
                }
            }
            room.AddChild(floor);
        }

		var walls = new Node2D { Name = "Walls", YSortEnabled = true };
		room.AddChild(walls);

		int midX = width / 2;
		int midY = height / 2;

		// Spawn a chest inside each room (avoids center and immediate edge-adjacent cells when possible)
		int chestX = originX + Mathf.Clamp(width / 2 - 1, 1, Mathf.Max(1, width - 2));
		int chestY = originY + Mathf.Clamp(height / 2 - 1, 1, Mathf.Max(1, height - 2));
		if (width > 4) chestX = originX + 2;
		if (height > 4) chestY = originY + 2;
		var chestScene = GD.Load<PackedScene>("res://map/map_objects/chest/chest.tscn");
		if (chestScene != null)
		{
			var chest = chestScene.Instantiate<Node2D>();
			chest.Position = new Vector2(chestX * MapConstants.CELL_SIZE + MapConstants.CELL_SIZE / 2.0f, chestY * MapConstants.CELL_SIZE + MapConstants.CELL_SIZE);
			room.AddChild(chest);
		}

        // North and South walls
        for (int x = 0; x < width; x++)
        {
            bool isNorthCorner = IsCorner(x, 0, width, height);
            WallFeature northFeature = isNorthCorner ? WallFeature.Solid : (x == midX ? north : WallFeature.Solid);
            PlaceEdge(walls, originX + x, originY, northFeature, GateFactory.GateAxis.Horizontal, gatesOpen, isNorthCorner);

            bool isSouthCorner = IsCorner(x, height - 1, width, height);
            WallFeature southFeature = isSouthCorner ? WallFeature.Solid : (x == midX ? south : WallFeature.Solid);
            PlaceEdge(walls, originX + x, originY + height - 1, southFeature, GateFactory.GateAxis.Horizontal, gatesOpen, isSouthCorner);
        }

        // West and East walls (excluding corners handled above)
        for (int y = 1; y < height - 1; y++)
        {
            WallFeature westFeature = (y == midY) ? west : WallFeature.Solid;
            PlaceEdge(walls, originX, originY + y, westFeature, GateFactory.GateAxis.Vertical, gatesOpen, false);

            WallFeature eastFeature = (y == midY) ? east : WallFeature.Solid;
            PlaceEdge(walls, originX + width - 1, originY + y, eastFeature, GateFactory.GateAxis.Vertical, gatesOpen, false);
        }

        return room;
    }

    private static bool IsCorner(int x, int y, int width, int height)
    {
        bool leftOrRight = x == 0 || x == width - 1;
        bool topOrBottom = y == 0 || y == height - 1;
        return leftOrRight && topOrBottom;
    }

    private static void PlaceEdge(Node2D parent, int x, int y, WallFeature feature, GateFactory.GateAxis gateAxis, bool gatesOpen, bool forceWall)
    {
        if (forceWall || feature == WallFeature.Solid)
        {
            parent.AddChild(WallFactory.Create(x, y));
            return;
        }

        if (feature == WallFeature.Gate)
        {
            parent.AddChild(GateFactory.Create(x, y, gateAxis, gatesOpen));
        }
    }
}