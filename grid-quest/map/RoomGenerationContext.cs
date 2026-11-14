using Godot;
using GridQuest.Map.MapObjects;
using System;
using System.Collections.Generic;

namespace GridQuest.Map;

// One context per building, with all room roots in the same coordinate space.
public sealed class RoomGenerationContext
{
	internal readonly record struct Edge(WallFeature Feature, GateFactory.GateAxis Axis, bool Open, bool Corner);
	private readonly Dictionary<Vector2I, Edge> _edges = new();
	private readonly Dictionary<Vector2I, Node2D> _nodes = new();
	private readonly HashSet<Vector2I> _floors = new();
	private readonly List<Rect2I> _rooms = new();

	internal void AddRoom(Rect2I bounds, Dictionary<Vector2I, Edge> edges, Node2D walls)
	{
		// Validate the whole room before changing an existing room's geometry.
		foreach (Rect2I room in _rooms)
		{
			Rect2I overlap = room.Intersection(bounds);
			if (overlap.Size.X > 1 && overlap.Size.Y > 1)
				throw new ArgumentException("Rooms may share boundary cells, but may not overlap interiors.");
		}
		var merged = new Dictionary<Vector2I, Edge>();
		foreach (var (cell, edge) in edges)
			merged[cell] = _edges.TryGetValue(cell, out Edge existing) ? Merge(existing, edge) : edge;

		foreach (var (cell, edge) in merged)
		{
			if (_edges.TryGetValue(cell, out Edge existing) && existing == edge)
				continue;
			if (_nodes.Remove(cell, out Node2D previous))
			{
				previous.GetParent().RemoveChild(previous);
				previous.QueueFree();
			}
			Node2D node = edge.Feature switch
			{
				WallFeature.Solid => WallFactory.Create(cell.X, cell.Y),
				WallFeature.Gate => GateFactory.Create(cell.X, cell.Y, edge.Axis, edge.Open),
				_ => null,
			};
			if (node != null)
			{
				walls.AddChild(node);
				_nodes[cell] = node;
			}
			// Openings must be recorded too, so later solid edges cannot close them.
			_edges[cell] = edge;
		}
		_rooms.Add(bounds);
	}

	internal bool AddFloor(Vector2I cell) => _floors.Add(cell);

	private static Edge Merge(Edge a, Edge b)
	{
		if ((a.Corner && b.Feature != WallFeature.Solid) || (b.Corner && a.Feature != WallFeature.Solid))
			throw new ArgumentException("A doorway cannot overlap another room's corner. Align the rooms or move the doorway.");
		if (a.Feature == WallFeature.Gate && b.Feature == WallFeature.Gate && (a.Axis != b.Axis || a.Open != b.Open))
			throw new ArgumentException("Shared gates must agree on orientation and open state.");
		// Gate > opening > solid, independent of creation order. Corners remain solid.
		Edge chosen = Rank(a.Feature) >= Rank(b.Feature) ? a : b;
		return chosen with { Corner = a.Corner || b.Corner };
	}

	private static int Rank(WallFeature feature) => feature switch
	{
		WallFeature.Gate => 2,
		WallFeature.Opening => 1,
		_ => 0,
	};
}
