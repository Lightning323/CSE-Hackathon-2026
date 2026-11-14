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
		RoomGenerationContext context = null,
		bool includeChest = true,
		bool restockingChest = false,
		bool includeDecor = false)
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

		var room = new Node2D
		{
			Name = "Room",
			YSortEnabled = true,
			// Keep building interiors visually dark, but still readable when lit by the player.
			Modulate = new Color(0.38f, 0.38f, 0.42f, 1.0f),
		};
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

		bool chestPlaced = false;
		Vector2I chestCell = new(originX + 1, originY + 1);
		// Keep all center-to-door routes clear; tiny rooms have no safe chest cell.
		if (includeChest && width >= 5 && height >= 5)
		{
			var chestScene = GD.Load<PackedScene>("res://map/map_objects/chest/chest.tscn");
			if (chestScene != null)
			{
				var chest = chestScene.Instantiate<Node2D>();
				chest.Position = new Vector2(chestCell.X * MapConstants.CELL_SIZE + MapConstants.CELL_SIZE / 2.0f,
					chestCell.Y * MapConstants.CELL_SIZE + MapConstants.CELL_SIZE);
				if (chest is Chest chestObj)
					chestObj.RestocksDaily = restockingChest;
				room.AddChild(chest);
				chestPlaced = true;
			}
		}

		if (includeDecor)
		{
			AddRoomDecor(room, originX, originY, width, height, north, south, east, west, chestPlaced ? chestCell : null);
		}

		return room;
	}

	private static void AddRoomDecor(Node2D room, int originX, int originY, int width, int height,
		WallFeature north, WallFeature south, WallFeature east, WallFeature west, Vector2I? chestCell)
	{
		var rng = new RandomNumberGenerator();
		rng.Seed = unchecked((ulong)(originX * 73856093 ^ originY * 19349663 ^ width * 83492791 ^ height * 2654435761));

		var doorCells = new HashSet<Vector2I>();
		if (north != WallFeature.Solid) doorCells.Add(new Vector2I(originX + width / 2, originY));
		if (south != WallFeature.Solid) doorCells.Add(new Vector2I(originX + width / 2, originY + height - 1));
		if (west != WallFeature.Solid) doorCells.Add(new Vector2I(originX, originY + height / 2));
		if (east != WallFeature.Solid) doorCells.Add(new Vector2I(originX + width - 1, originY + height / 2));

		var candidates = new List<Vector2I>();
		for (int y = originY + 1; y < originY + height - 1; y++)
		{
			for (int x = originX + 1; x < originX + width - 1; x++)
			{
				var cell = new Vector2I(x, y);
				if (chestCell.HasValue && cell == chestCell.Value) continue;
				if (IsNearDoorRoute(cell, doorCells, originX, originY, width, height)) continue;
				candidates.Add(cell);
			}
		}

		Shuffle(candidates, rng);
		int decorCount = Math.Clamp(rng.RandiRange(1, 3), 0, candidates.Count);
		for (int i = 0; i < decorCount; i++)
		{
			Vector2I cell = candidates[i];
			if (rng.Randf() < 0.7f)
				room.AddChild(PlantFactory.Create(cell.X, cell.Y, rng.RandiRange(1, 3)));
			else
				room.AddChild(PropFactory.Create(cell.X, cell.Y, "res://assets/images/map_tiles/plants/forest_bush.png", "RoomDecorBush", false, Vector2.Zero, null, null, false));
		}
	}

	private static bool IsNearDoorRoute(Vector2I cell, HashSet<Vector2I> doorCells, int originX, int originY, int width, int height)
	{
		foreach (Vector2I door in doorCells)
		{
			if (cell.DistanceTo(door) <= 1.5f) return true;
			// keep center lines clearer for movement through openings
			if (door.X == originX || door.X == originX + width - 1)
			{
				if (cell.Y == door.Y && Math.Abs(cell.X - door.X) <= 2) return true;
			}
			if (door.Y == originY || door.Y == originY + height - 1)
			{
				if (cell.X == door.X && Math.Abs(cell.Y - door.Y) <= 2) return true;
			}
		}
		// keep a little breathing room around room center
		Vector2I center = new(originX + width / 2, originY + height / 2);
		return cell == center;
	}

	private static void Shuffle<T>(IList<T> list, RandomNumberGenerator rng)
	{
		for (int i = list.Count - 1; i > 0; i--)
		{
			int j = rng.RandiRange(0, i);
			(list[i], list[j]) = (list[j], list[i]);
		}
	}
}
