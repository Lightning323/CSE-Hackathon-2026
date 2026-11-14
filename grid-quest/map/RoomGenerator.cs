using Godot;
using GridQuest.Map.MapFloors;
using GridQuest.Map.MapObjects;
using System;
using System.Collections.Generic;

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
		int originX, int originY, int width, int height,
		WallFeature north = WallFeature.Solid,
		WallFeature south = WallFeature.Solid,
		WallFeature east = WallFeature.Solid,
		WallFeature west = WallFeature.Solid,
		bool gatesOpen = false, bool includeFloor = false,
		RoomGenerationContext context = null)
	{
		if (width < 3 || height < 3)
			throw new ArgumentOutOfRangeException(nameof(width), "Room dimensions include walls and must both be at least 3 cells.");
		_ = checked(originX + width);
		_ = checked(originY + height);
		foreach (WallFeature feature in new[] { north, south, east, west })
			if (!Enum.IsDefined(feature))
				throw new ArgumentOutOfRangeException(nameof(north), "Unknown wall feature.");

		context ??= new RoomGenerationContext();
		var edges = new Dictionary<Vector2I, RoomGenerationContext.Edge>();
		void Edge(int x, int y, WallFeature feature, GateFactory.GateAxis axis, bool corner = false)
		{
			edges[new Vector2I(originX + x, originY + y)] = new(feature, axis, gatesOpen, corner);
		}
		for (int x = 0; x < width; x++)
		{
			bool corner = x == 0 || x == width - 1;
			Edge(x, 0, !corner && x == width / 2 ? north : WallFeature.Solid, GateFactory.GateAxis.Horizontal, corner);
			Edge(x, height - 1, !corner && x == width / 2 ? south : WallFeature.Solid, GateFactory.GateAxis.Horizontal, corner);
		}
		for (int y = 1; y < height - 1; y++)
		{
			Edge(0, y, y == height / 2 ? west : WallFeature.Solid, GateFactory.GateAxis.Vertical);
			Edge(width - 1, y, y == height / 2 ? east : WallFeature.Solid, GateFactory.GateAxis.Vertical);
		}

		var room = new Node2D { Name = "Room", YSortEnabled = true };
		var walls = new Node2D { Name = "Walls", YSortEnabled = true };
		room.AddChild(walls);
		try
		{
			context.AddRoom(new Rect2I(originX, originY, width, height), edges, walls);
		}
		catch
		{
			room.Free();
			throw;
		}
		if (includeFloor)
		{
			var floor = FloorTiles.CreateLayer("Floor");
			for (int y = originY; y < originY + height; y++)
				for (int x = originX; x < originX + width; x++)
					if (context.AddFloor(new Vector2I(x, y)))
						FloorTiles.SetStone(floor, x, y);
			room.AddChild(floor);
		}

		// Keep all center-to-door routes clear; tiny rooms have no safe chest cell.
		if (width >= 5 && height >= 5)
		{
			var chestScene = GD.Load<PackedScene>("res://map/map_objects/chest/chest.tscn");
			if (chestScene != null)
			{
				var chest = chestScene.Instantiate<Node2D>();
				chest.Position = new Vector2((originX + 1) * MapConstants.CELL_SIZE + MapConstants.CELL_SIZE / 2.0f,
					(originY + 1) * MapConstants.CELL_SIZE + MapConstants.CELL_SIZE);
				room.AddChild(chest);
			}
		}
		return room;
	}
}
