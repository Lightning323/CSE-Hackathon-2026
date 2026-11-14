using Godot;
using GridQuest.Map;
using GridQuest.Map.MapObjects;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class GenerationTests : Node
{
	private int _checks;
	private static readonly Vector2I[] Directions = { Vector2I.Up, Vector2I.Down, Vector2I.Left, Vector2I.Right };

	public override async void _Ready()
	{
		try
		{
			TestSharedEdges();
			TestCornersAndValidation();
			for (ulong seed = 0; seed < 30; seed++)
				foreach (int count in new[] { 1, 2, 5, 15 })
					TestBuilding(seed, count);
			var gated = BuildingGenerator.Create(4, seed: 9, connection: WallFeature.Gate);
			AddChild(gated);
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
			foreach (GateFactory gate in Descendants(gated).OfType<GateFactory>())
			{
				Check(gate.IsOpen && !gate.BlocksMovement, "Open gate metadata survives Ready");
				Check(gate.GetNode<CollisionShape2D>("StaticBody2D/CollisionShape2D").Disabled, "Open gate collision disabled");
				gate.Configure(gate.Axis, false);
				Check(gate.BlocksMovement && !gate.GetNode<CollisionShape2D>("StaticBody2D/CollisionShape2D").Disabled, "Gate can close");
			}
			gated.Free();
			TestMapIntegration();
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
			GD.Print($"Generation tests passed: {_checks} checks.");
			GetTree().Quit();
		}
		catch (Exception error)
		{
			GD.PushError(error.ToString());
			GetTree().Quit(1);
		}
	}

	private void TestSharedEdges()
	{
		foreach (bool vertical in new[] { false, true })
		foreach (bool reverse in new[] { false, true })
		foreach (WallFeature a in Enum.GetValues<WallFeature>())
		foreach (WallFeature b in Enum.GetValues<WallFeature>())
		{
			var root = new Node2D();
			var context = new RoomGenerationContext();
			Node2D Room(bool second)
			{
				WallFeature f = second ? b : a;
				return RoomGenerator.Create(second && !vertical ? 6 : 0, second && vertical ? 6 : 0, 7, 7,
					north: second && vertical ? f : WallFeature.Solid,
					south: !second && vertical ? f : WallFeature.Solid,
					east: !second && !vertical ? f : WallFeature.Solid,
					west: second && !vertical ? f : WallFeature.Solid, context: context, includeFloor: true);
			}
			root.AddChild(Room(reverse));
			root.AddChild(Room(!reverse));
			var objects = Descendants(root).OfType<MapObjectFactory>().Where(n => n is WallFactory or GateFactory).ToList();
			Check(objects.Select(n => n.Position).Distinct().Count() == objects.Count, "Unique shared wall/gate cells");
			Vector2 door = CellBase(vertical ? new(3, 6) : new(6, 3));
			var occupants = objects.Where(n => n.Position == door).ToList();
			if (a == WallFeature.Gate || b == WallFeature.Gate)
				Check(occupants.Count == 1 && occupants[0] is GateFactory gate && gate.Axis == (vertical ? GateFactory.GateAxis.Horizontal : GateFactory.GateAxis.Vertical), "Shared gate preserved and oriented");
			else if (a == WallFeature.Opening || b == WallFeature.Opening)
				Check(occupants.Count == 0, "Opening preserved against solid");
			else Check(occupants.Count == 1 && occupants[0] is WallFactory, "Single solid wall");
			var floors = Descendants(root).OfType<Sprite2D>().Where(n => n.GetParent().Name == "Floor").ToList();
			Check(floors.Count == 91 && floors.Select(n => n.Position).Distinct().Count() == 91, "Shared floor deduplicated");
			root.Free();
		}
	}

	private void TestCornersAndValidation()
	{
		var root = new Node2D();
		var context = new RoomGenerationContext();
		foreach (Vector2I p in new[] { new Vector2I(0, 0), new(6, 0), new(0, 6), new(6, 6) })
			root.AddChild(RoomGenerator.Create(p.X, p.Y, 7, 7, context: context));
		Check(Descendants(root).OfType<WallFactory>().Count(n => n.Position == CellBase(new(6, 6))) == 1, "Four-room corner has one wall");
		root.Free();
		root = new Node2D();
		context = new RoomGenerationContext();
		root.AddChild(RoomGenerator.Create(0, 0, 7, 7, east: WallFeature.Opening, context: context));
		int before = Descendants(root).Count();
		Throws(() => RoomGenerator.Create(6, 3, 7, 7, context: context), "Door/corner conflict rejected");
		Throws(() => RoomGenerator.Create(2, 2, 7, 7, context: context), "Interior overlap rejected");
		Check(Descendants(root).Count() == before, "Rejected room leaves existing geometry unchanged");
		root.Free();
		root = new Node2D();
		context = new RoomGenerationContext();
		root.AddChild(RoomGenerator.Create(0, 0, 7, 7, east: WallFeature.Gate, context: context));
		Throws(() => RoomGenerator.Create(6, 0, 7, 7, west: WallFeature.Gate, gatesOpen: true, context: context), "Conflicting gate state rejected");
		root.Free();
		Throws(() => RoomGenerator.Create(0, 0, 2, 4), "Invalid room size rejected");
		Throws(() => BuildingGenerator.Plan(0), "Invalid count rejected");
		Throws(() => BuildingGenerator.Plan(2, connection: WallFeature.Solid), "Disconnected feature rejected");
		Throws(() => BuildingGenerator.Plan(2, 4, 5), "Unsafe building room size rejected");
	}

	private void TestBuilding(ulong seed, int count)
	{
		int width = seed % 2 == 0 ? 5 : 8, height = seed % 3 == 0 ? 6 : 7;
		BuildingLayout layout = BuildingGenerator.Plan(count, width, height, seed);
		Check(layout.Rooms.Count == count, "Requested room count");
		Check(layout.Rooms.SequenceEqual(BuildingGenerator.Plan(count, width, height, seed).Rooms), "Repeatable seed");
		Vector2I origin = new(-11, 13);
		Node2D building = BuildingGenerator.Create(layout, origin.X, origin.Y);
		var geometry = Descendants(building).OfType<MapObjectFactory>().Where(n => n is WallFactory or GateFactory).ToList();
		Check(geometry.Count == geometry.Select(n => n.Position).Distinct().Count(), "Building has unique boundary nodes");
		var floors = Descendants(building).OfType<Sprite2D>().Where(n => n.GetParent().Name == "Floor").ToList();
		Check(floors.Count == floors.Select(n => n.Position).Distinct().Count(), "Building has unique floors");
		var walkable = new HashSet<Vector2I>(floors.Select(n => new Vector2I((int)n.Position.X / 16, (int)n.Position.Y / 16)));
		foreach (Node2D obstacle in Descendants(building).OfType<Node2D>().Where(n => n is WallFactory || n.Name == "Chest"))
			walkable.Remove(new Vector2I((int)((obstacle.Position.X - 8) / 16), (int)(obstacle.Position.Y / 16) - 1));
		Vector2I start = origin + layout.Rooms[0].Bounds.Position + new Vector2I(width / 2, height / 2);
		var reached = new HashSet<Vector2I> { start };
		var queue = new Queue<Vector2I>();
		queue.Enqueue(start);
		while (queue.TryDequeue(out Vector2I p))
			foreach (Vector2I direction in Directions)
				if (walkable.Contains(p + direction) && reached.Add(p + direction)) queue.Enqueue(p + direction);
		foreach (BuildingRoom room in layout.Rooms)
			Check(reached.Contains(origin + room.Bounds.Position + new Vector2I(width / 2, height / 2)), "All rooms reachable on rendered geometry");
		BuildingRoom entry = layout.Rooms.First(r => r.North != WallFeature.Solid && r.Bounds.Position.Y == 0);
		Check(reached.Contains(origin + entry.Bounds.Position + new Vector2I(width / 2, 0)), "Exterior entrance reachable");
		building.Free();
	}

	private void TestMapIntegration()
	{
		var map = new Map();
		AddChild(map);
		var buildings = map.GetChildren().OfType<Node2D>().Where(n => n.Name.ToString().StartsWith("Building_")).ToList();
		Check(buildings.Any(n => n.GetChildren().Count(c => c.Name.ToString().StartsWith("Room_")) == 5), "Runtime map places the five-room dungeon");
		var floors = new HashSet<Vector2>(buildings.SelectMany(Descendants).OfType<Sprite2D>()
			.Where(n => n.GetParent().Name == "Floor").Select(n => n.Position));
		foreach (MapObjectFactory obstacle in Descendants(map.GetNode("Objects")).OfType<MapObjectFactory>())
			Check(!floors.Contains(obstacle.Position - new Vector2(8, 16)), "Wilderness objects excluded from building floors");
		Check(map.GenerateDungeon(0, 0, BuildingGenerator.Plan(1)) == null, "Out-of-bounds site rejected");
		Check(map.GenerateDungeon(5, 5, BuildingGenerator.Plan(1, 1000, 1000)) == null, "Oversized site rejected");
		map.Free();
	}

	private static IEnumerable<Node> Descendants(Node root)
	{
		foreach (Node child in root.GetChildren())
		{
			yield return child;
			foreach (Node descendant in Descendants(child)) yield return descendant;
		}
	}
	private static Vector2 CellBase(Vector2I p) => new(p.X * 16 + 8, p.Y * 16 + 16);
	private void Check(bool condition, string message)
	{
		_checks++;
		if (!condition) throw new Exception(message);
	}
	private void Throws(Action action, string message)
	{
		try { action(); }
		catch (ArgumentException) { _checks++; return; }
		throw new Exception(message);
	}
}
