using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GridQuest.Map;

public sealed record BuildingRoom(Rect2I Bounds, WallFeature North, WallFeature South, WallFeature East, WallFeature West);

public sealed class BuildingLayout
{
	public IReadOnlyList<BuildingRoom> Rooms { get; }
	public Vector2I Size { get; }
	internal BuildingLayout(List<BuildingRoom> rooms, Vector2I size)
	{
		Rooms = rooms.AsReadOnly();
		Size = size;
	}
}

public static class BuildingGenerator
{
	private static readonly Vector2I[] Directions = { Vector2I.Up, Vector2I.Down, Vector2I.Right, Vector2I.Left };

	public static BuildingLayout Plan(int roomCount, int roomWidth = 8, int roomHeight = 7,
		ulong? seed = null, WallFeature connection = WallFeature.Opening)
	{
		if (roomCount < 1)
			throw new ArgumentOutOfRangeException(nameof(roomCount));
		if (roomWidth < 5 || roomHeight < 5)
			throw new ArgumentOutOfRangeException(nameof(roomWidth), "Building rooms need at least 5 cells per dimension for clear chest and doorway routes.");
		if (connection != WallFeature.Opening && connection != WallFeature.Gate)
			throw new ArgumentException("Rooms must connect through openings or gates.", nameof(connection));

		using var rng = new RandomNumberGenerator();
		if (seed.HasValue) rng.Seed = seed.Value;
		else rng.Randomize();
		var slots = new List<Vector2I> { Vector2I.Zero };
		var occupied = new HashSet<Vector2I>(slots);
		var features = new List<WallFeature[]> { new WallFeature[4] };
		var frontier = new List<(int Room, int Direction)>();
		void Expand(int index)
		{
			for (int d = 0; d < Directions.Length; d++)
				if (!occupied.Contains(slots[index] + Directions[d]))
					frontier.Add((index, d));
		}
		Expand(0);
		while (slots.Count < roomCount)
		{
			int pick = rng.RandiRange(0, frontier.Count - 1);
			var (parent, direction) = frontier[pick];
			frontier[pick] = frontier[^1];
			frontier.RemoveAt(frontier.Count - 1);
			Vector2I slot = slots[parent] + Directions[direction];
			if (!occupied.Add(slot)) continue;
			int index = slots.Count;
			slots.Add(slot);
			features.Add(new WallFeature[4]);
			features[parent][direction] = connection;
			features[index][direction ^ 1] = connection;
			Expand(index);
		}

		int minX = slots.Min(p => p.X), minY = slots.Min(p => p.Y);
		var rooms = new List<BuildingRoom>();
		int maxX = 0, maxY = 0;
		for (int i = 0; i < slots.Count; i++)
		{
			int x = checked((slots[i].X - minX) * (roomWidth - 1));
			int y = checked((slots[i].Y - minY) * (roomHeight - 1));
			var bounds = new Rect2I(x, y, roomWidth, roomHeight);
			maxX = Math.Max(maxX, checked(x + roomWidth));
			maxY = Math.Max(maxY, checked(y + roomHeight));
			rooms.Add(new BuildingRoom(bounds, features[i][0], features[i][1], features[i][2], features[i][3]));
		}
		// The northernmost room always has an exposed north edge for an entrance.
		int entrance = rooms.FindIndex(r => r.Bounds.Position.Y == 0);
		rooms[entrance] = rooms[entrance] with { North = connection };
		return new BuildingLayout(rooms, new Vector2I(maxX, maxY));
	}

	public static Node2D Create(int roomCount, int originX = 0, int originY = 0,
		int roomWidth = 8, int roomHeight = 7, ulong? seed = null,
		WallFeature connection = WallFeature.Opening, bool gatesOpen = true, bool includeFloor = true)
	{
		return Create(Plan(roomCount, roomWidth, roomHeight, seed, connection), originX, originY, gatesOpen, includeFloor);
	}

	public static Node2D Create(BuildingLayout layout, int originX = 0, int originY = 0,
		bool gatesOpen = true, bool includeFloor = true)
	{
		ArgumentNullException.ThrowIfNull(layout);
		_ = checked(originX + layout.Size.X);
		_ = checked(originY + layout.Size.Y);
		var building = new Node2D { Name = "Building", YSortEnabled = true };
		var context = new RoomGenerationContext();
		var rng = new RandomNumberGenerator();
		rng.Seed = unchecked((ulong)(originX * 92821 + originY * 68917 + layout.Size.X * 31 + layout.Size.Y * 17 + layout.Rooms.Count * 97));
		try
		{
			for (int i = 0; i < layout.Rooms.Count; i++)
			{
				BuildingRoom spec = layout.Rooms[i];
				bool isEntranceRoom = spec.Bounds.Position.Y == 0 && spec.North != WallFeature.Solid;
				bool includeChest = !isEntranceRoom && (i % 2 == 1 || rng.Randf() < 0.35f);
				Node2D room = RoomGenerator.Create(originX + spec.Bounds.Position.X, originY + spec.Bounds.Position.Y,
					spec.Bounds.Size.X, spec.Bounds.Size.Y, spec.North, spec.South, spec.East, spec.West,
					gatesOpen, includeFloor, context,
					includeChest: includeChest,
					restockingChest: includeChest,
					includeDecor: true);
				room.Name = $"Room_{i}";
				building.AddChild(room);
			}
			return building;
		}
		catch
		{
			building.Free();
			throw;
		}
	}
}
