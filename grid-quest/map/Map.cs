using Godot;
using GridQuest.Map;
using GridQuest.Map.MapFloors;
using GridQuest.Map.MapObjects;
using System;
using System.Collections.Generic;

public partial class Map : Node2D
{
	public const int Width = 300;
	public const int Height = 300;

	public readonly struct VillageArea
	{
		public readonly Rect2I Bounds;
		public readonly Vector2I Center;
		public readonly int Index;

		public VillageArea(Rect2I bounds, Vector2I center, int index)
		{
			Bounds = bounds;
			Center = center;
			Index = index;
		}
	}

	private readonly bool[,] _pathCells = new bool[Width, Height];
	private readonly bool[,] _reservedCells = new bool[Width, Height];
	private readonly bool[,] _occupiedCells = new bool[Width, Height];
	private readonly List<VillageArea> _villages = new();
	private Vector2I _monumentCell = new(Width / 2, Height / 2);

	public IReadOnlyList<VillageArea> Villages => _villages;
	public int MapWidth => Width;
	public int MapHeight => Height;
	public Vector2 MonumentWorldPosition => CellToWorldBase(_monumentCell.X, _monumentCell.Y);

	public override void _Ready()
	{
		YSortEnabled = true;
		GenerateMap();
	}

	public void GenerateMap()
	{
		ClearGeneratedLayers();
		Array.Clear(_pathCells, 0, _pathCells.Length);
		Array.Clear(_reservedCells, 0, _reservedCells.Length);
		Array.Clear(_occupiedCells, 0, _occupiedCells.Length);
		_villages.Clear();

		var rng = new RandomNumberGenerator();
		rng.Randomize();

		_monumentCell = PickMonumentCell(rng);
		ReserveMonumentArea(_monumentCell);

		var houses = GenerateVillages(rng);
		CarveMonumentApproachPaths(_monumentCell);

		AddChild(FloorLayer(Width, Height));
		GenerateBuildings();
		AddChild(ObjectLayer(Width, Height, rng, houses));
	}

	public Node2D GenerateBuilding(
		int originX,
		int originY,
		int width,
		int height,
		GridQuest.Map.WallFeature north = GridQuest.Map.WallFeature.Solid,
		GridQuest.Map.WallFeature south = GridQuest.Map.WallFeature.Solid,
		GridQuest.Map.WallFeature east = GridQuest.Map.WallFeature.Solid,
		GridQuest.Map.WallFeature west = GridQuest.Map.WallFeature.Solid,
		bool gatesOpen = false,
		bool includeFloor = true)
	{
		if (!CanPlaceBuilding(originX, originY, width, height))
			return null;

		var building = GridQuest.Map.RoomGenerator.Create(originX, originY, width, height, north, south, east, west, gatesOpen, includeFloor);
		building.Name = $"Building_{originX}_{originY}";
		ReserveRect(originX - 1, originY - 1, width + 2, height + 2);
		MarkOccupiedRect(originX, originY, width, height);
		AddChild(building);
		return building;
	}

	public Node2D GenerateDungeon(int originX, int originY, BuildingLayout layout,
		bool gatesOpen = true, bool includeFloor = true)
	{
		ArgumentNullException.ThrowIfNull(layout);
		if (!CanPlaceBuilding(originX, originY, layout.Size.X, layout.Size.Y))
			return null;

		Node2D building = BuildingGenerator.Create(layout, originX, originY, gatesOpen, includeFloor);
		building.Name = $"Building_{originX}_{originY}";
		// Reserve the entire footprint, including gaps, before wilderness objects are scattered.
		ReserveRect(originX - 1, originY - 1, layout.Size.X + 2, layout.Size.Y + 2);
		MarkOccupiedRect(originX, originY, layout.Size.X, layout.Size.Y);
		AddChild(building);
		return building;
	}

	private void GenerateBuildings()
	{
		// Generate before wilderness decoration so entrances and interiors stay clear.
		BuildingLayout dungeon = BuildingGenerator.Plan(8);
		for (int y = 5; y + dungeon.Size.Y < 98; y += 5)
		{
			bool placed = false;
			for (int x = 5; x + dungeon.Size.X < 98; x += 5)
			{
				if (GenerateDungeon(x, y, dungeon) == null) continue;
				placed = true;
				break;
			}
			if (placed) break;
		}
		// TryPlaceBuildingNear(24, 24, 8, 6, GridQuest.Map.WallFeature.Solid, GridQuest.Map.WallFeature.Gate, GridQuest.Map.WallFeature.Solid, GridQuest.Map.WallFeature.Solid);
		// TryPlaceBuildingNear(70, 18, 10, 8, GridQuest.Map.WallFeature.Opening, GridQuest.Map.WallFeature.Solid, GridQuest.Map.WallFeature.Gate, GridQuest.Map.WallFeature.Solid);
		// TryPlaceBuildingNear(24, 70, 12, 7, GridQuest.Map.WallFeature.Solid, GridQuest.Map.WallFeature.Gate, GridQuest.Map.WallFeature.Solid, GridQuest.Map.WallFeature.Opening, gatesOpen: true);
	}

	private void TryPlaceBuildingNear(
		int preferredX,
		int preferredY,
		int width,
		int height,
		GridQuest.Map.WallFeature north,
		GridQuest.Map.WallFeature south,
		GridQuest.Map.WallFeature east,
		GridQuest.Map.WallFeature west,
		bool gatesOpen = false,
		bool includeFloor = true)
	{
		int[] offsets = { 0, 6, -6, 12, -12, 18, -18, 24, -24, 30, -30 };
		foreach (int oy in offsets)
		{
			foreach (int ox in offsets)
			{
				int x = preferredX + ox;
				int y = preferredY + oy;
				if (GenerateBuilding(x, y, width, height, north, south, east, west, gatesOpen, includeFloor) != null)
					return;
			}
		}
	}

	private bool CanPlaceBuilding(int originX, int originY, int width, int height)
	{
		if (width < 3 || height < 3 || originX < 2 || originY < 2 ||
			originX + (long)width >= Width - 1 || originY + (long)height >= Height - 1)
			return false;

		for (int y = originY - 1; y <= originY + height; y++)
		{
			for (int x = originX - 1; x <= originX + width; x++)
			{
				if (!IsInBounds(x, y))
					return false;
				if (_pathCells[x, y] || _reservedCells[x, y] || _occupiedCells[x, y])
					return false;
				if (IsInAnyVillage(x, y))
					return false;
			}
		}

		Vector2I center = new(originX + width / 2, originY + height / 2);
		if (center.DistanceTo(_monumentCell) < 18)
			return false;

		return true;
	}

	public Vector2 CellToWorldCenter(int x, int y)
	{
		return new Vector2(x * MapConstants.CELL_SIZE + MapConstants.CELL_SIZE / 2.0f, y * MapConstants.CELL_SIZE + MapConstants.CELL_SIZE / 2.0f);
	}

	public Vector2 CellToWorldBase(int x, int y)
	{
		return new Vector2(x * MapConstants.CELL_SIZE + MapConstants.CELL_SIZE / 2.0f, y * MapConstants.CELL_SIZE + MapConstants.CELL_SIZE);
	}

	public bool IsCellInBounds(int x, int y) => IsInBounds(x, y);

	public Vector2 GetRandomPointInVillage(int villageIndex)
	{
		if (villageIndex < 0 || villageIndex >= _villages.Count)
			return CellToWorldCenter(Width / 2, Height / 2);

		VillageArea village = _villages[villageIndex];
		var rng = new RandomNumberGenerator();
		rng.Randomize();
		for (int i = 0; i < 60; i++)
		{
			int x = rng.RandiRange(village.Bounds.Position.X + 2, village.Bounds.End.X - 3);
			int y = rng.RandiRange(village.Bounds.Position.Y + 2, village.Bounds.End.Y - 3);
			if (!IsInBounds(x, y))
				continue;
			if (x <= 0 || y <= 0 || x >= Width - 1 || y >= Height - 1)
				continue;
			if (_occupiedCells[x, y])
				continue;
			// Prefer non-path tiles inside the village for more natural wandering/spawns.
			if (_pathCells[x, y] && i < 40)
				continue;
			return CellToWorldCenter(x, y);
		}

		for (int i = 0; i < 40; i++)
		{
			int x = rng.RandiRange(village.Bounds.Position.X + 2, village.Bounds.End.X - 3);
			int y = rng.RandiRange(village.Bounds.Position.Y + 2, village.Bounds.End.Y - 3);
			if (!IsInBounds(x, y) || _occupiedCells[x, y])
				continue;
			return CellToWorldCenter(x, y);
		}

		return CellToWorldCenter(village.Center.X, village.Center.Y);
	}

	private void ClearGeneratedLayers()
	{
		foreach (string name in new[] { "Floor", "Objects" })
		{
			Node existing = GetNodeOrNull<Node>(name);
			existing?.QueueFree();
		}

		var toRemove = new List<Node>();
		foreach (Node child in GetChildren())
		{
			if (child.Name.ToString().StartsWith("Building_", StringComparison.Ordinal))
				toRemove.Add(child);
		}
		foreach (Node node in toRemove)
			node.QueueFree();
	}

	private TileMapLayer FloorLayer(int width, int height)
	{
		TileMapLayer floorLayer = FloorTiles.CreateLayer("Floor");

		for (int y = 0; y < height; y++)
		{
			for (int x = 0; x < width; x++)
			{
				if (_pathCells[x, y])
					FloorTiles.SetPath(floorLayer, x, y);
				else
					FloorTiles.SetGrass(floorLayer, x, y);
			}
		}

		return floorLayer;
	}

	private Node2D ObjectLayer(int width, int height, RandomNumberGenerator rng, List<Vector2I> houses)
	{
		Node2D objectsRoot = new() { Name = "Objects", YSortEnabled = true };
		Node2D wallsLayer = new() { Name = "Walls", YSortEnabled = false, ZIndex = -1 };
		Node2D plantsLayer = new() { Name = "Plants", YSortEnabled = false };
		Node2D decorLayer = new() { Name = "Decor", YSortEnabled = true };
		Node2D treesLayer = new() { Name = "Trees", YSortEnabled = true };
		Node2D housesLayer = new() { Name = "Houses", YSortEnabled = true };

		for (int y = 0; y < height; y++)
		{
			for (int x = 0; x < width; x++)
			{
				bool isBorder = x == 0 || y == 0 || x == width - 1 || y == height - 1;
				if (isBorder)
				{
					if (!_occupiedCells[x, y])
					{
						wallsLayer.AddChild(WallFactory.Create(x, y));
						MarkOccupied(x, y);
					}
					continue;
				}

				if (_occupiedCells[x, y] || _pathCells[x, y])
					continue;

				bool inVillage = IsInAnyVillage(x, y);
				if (inVillage)
				{
					if (_reservedCells[x, y] || _pathCells[x, y] || _occupiedCells[x, y])
						continue;

					float villageRoll = rng.Randf();
					if (villageRoll < 0.055f)
					{
						decorLayer.AddChild(CreateTownBush(x, y, rng));
						MarkOccupied(x, y);
					}
					else if (villageRoll < 0.16f)
					{
						plantsLayer.AddChild(PlantFactory.Create(x, y, rng.RandiRange(1, 3)));
					}
					continue;
				}

				if (_reservedCells[x, y])
					continue;

				// Leave a small ring around villages clear for village-specific town trees/decor.
				if (IsNearAnyVillageBounds(x, y, 3))
					continue;

				float roll = rng.Randf();
				if (roll < 0.018f)
				{
					treesLayer.AddChild(CreateForestTree(x, y, rng));
					MarkOccupied(x, y);
				}
				else if (roll < 0.028f)
				{
					decorLayer.AddChild(CreateForestRock(x, y));
					MarkOccupied(x, y);
				}
				else if (roll < 0.034f)
				{
					decorLayer.AddChild(CreateForestTrunk(x, y));
					MarkOccupied(x, y);
				}
				else if (roll < 0.055f)
				{
					plantsLayer.AddChild(CreateForestBush(x, y, rng));
				}
				else if (roll < 0.11f)
				{
					plantsLayer.AddChild(PlantFactory.Create(x, y, rng.RandiRange(1, 3)));
				}
			}
		}

		foreach (Vector2I houseCell in houses)
		{
			housesLayer.AddChild(HouseFactory.Create(houseCell.X, houseCell.Y));
			MarkHouseFootprint(houseCell.X, houseCell.Y);
		}

		PlaceTownTreesAroundVillages(treesLayer, rng);

		var monumentScene = GD.Load<PackedScene>("res://map/map_objects/monument/monument.tscn");
		var monument = monumentScene.Instantiate<Node2D>();
		monument.Position = CellToWorldBase(_monumentCell.X, _monumentCell.Y);
		treesLayer.AddChild(monument);
		MarkOccupiedRect(_monumentCell.X - 1, _monumentCell.Y - 1, 3, 2);

		objectsRoot.AddChild(wallsLayer);
		objectsRoot.AddChild(plantsLayer);
		objectsRoot.AddChild(decorLayer);
		objectsRoot.AddChild(housesLayer);
		objectsRoot.AddChild(treesLayer);
		return objectsRoot;
	}

	private Vector2I PickMonumentCell(RandomNumberGenerator rng)
	{
		// Camera limits in current player setup stop at 1600x1600 world units (100x100 cells).
		// Keep monument within that reachable area, away from edges, and biased toward center.
		const int cameraMaxCellX = 100;
		const int cameraMaxCellY = 100;
		const int edgeMargin = 18;

		int minX = edgeMargin;
		int maxX = cameraMaxCellX - edgeMargin - 1;
		int minY = edgeMargin;
		int maxY = cameraMaxCellY - edgeMargin - 1;

		int x = BiasedCenterRange(rng, minX, maxX);
		int y = BiasedCenterRange(rng, minY, maxY);
		return new Vector2I(x, y);
	}

	private static int BiasedCenterRange(RandomNumberGenerator rng, int min, int max)
	{
		float a = rng.Randf();
		float b = rng.Randf();
		float centered01 = (a + b) * 0.5f; // Triangular distribution biased toward center
		return Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(min, max, centered01)), min, max);
	}

	private List<Vector2I> GenerateVillages(RandomNumberGenerator rng)
	{
		var houses = new List<Vector2I>();
		var centers = new List<Vector2I>();
		int targetVillages = 12;
		int attempts = 0;

		while (centers.Count < targetVillages && attempts < 600)
		{
			attempts++;
			var center = new Vector2I(rng.RandiRange(20, Width - 21), rng.RandiRange(20, Height - 21));
			if (!IsVillageCenterValid(center, centers))
				continue;

			centers.Add(center);
			VillageArea area = GenerateVillage(center, rng, houses, _villages.Count);
			_villages.Add(area);
		}

		return houses;
	}

	private bool IsVillageCenterValid(Vector2I center, List<Vector2I> centers)
	{
		if (center.X < 20 || center.Y < 20 || center.X >= Width - 20 || center.Y >= Height - 20)
			return false;
		if (center.DistanceTo(_monumentCell) < 34)
			return false;

		foreach (Vector2I other in centers)
		{
			if (center.DistanceTo(other) < 32)
				return false;
		}

		return true;
	}

	private VillageArea GenerateVillage(Vector2I center, RandomNumberGenerator rng, List<Vector2I> houses, int villageIndex)
	{
		Rect2I bounds = new(center.X - 12, center.Y - 11, 25, 23);
		bounds = bounds.Intersection(new Rect2I(2, 2, Width - 4, Height - 4));
		CarveRect(center.X - 2, center.Y - 1, 5, 3, true, true);

		var slots = new List<Vector2I>
		{
			new(center.X - 7, center.Y - 6),
			new(center.X + 7, center.Y - 6),
			new(center.X - 7, center.Y + 6),
			new(center.X + 7, center.Y + 6),
			new(center.X, center.Y - 8),
			new(center.X, center.Y + 8),
		};

		Shuffle(slots, rng);
		int houseCount = rng.RandiRange(3, 4);
		int placed = 0;

		foreach (Vector2I slot in slots)
		{
			if (placed >= houseCount)
				break;
			if (!CanPlaceHouse(slot))
				continue;

			PlaceHouse(slot, houses);
			ConnectHouseToCenter(slot, center);
			DecorateAroundHouse(slot, rng);
			placed++;
		}

		CarvePathLine(center.X - 10, center.Y, center.X + 10, center.Y);
		CarvePathLine(center.X, center.Y - 9, center.X, center.Y + 9);
		PrepareVillageForDecoration(bounds);
		return new VillageArea(bounds, center, villageIndex);
	}

	private void PrepareVillageForDecoration(Rect2I bounds)
	{
		// Keep paths and occupied cells blocked, but allow non-path village ground to receive decor.
		for (int y = bounds.Position.Y; y < bounds.End.Y; y++)
		{
			for (int x = bounds.Position.X; x < bounds.End.X; x++)
			{
				if (!IsInBounds(x, y))
					continue;
				if (x == 0 || y == 0 || x == Width - 1 || y == Height - 1)
					continue;
				if (_occupiedCells[x, y] || _pathCells[x, y])
					continue;
				_reservedCells[x, y] = false;
			}
		}
	}

	private bool IsInAnyVillage(int x, int y)
	{
		Vector2I p = new(x, y);
		foreach (VillageArea village in _villages)
		{
			if (village.Bounds.HasPoint(p))
				return true;
		}
		return false;
	}

	private bool IsNearAnyVillageBounds(int x, int y, int margin)
	{
		Vector2I p = new(x, y);
		foreach (VillageArea village in _villages)
		{
			Rect2I expanded = new(village.Bounds.Position.X - margin, village.Bounds.Position.Y - margin, village.Bounds.Size.X + margin * 2, village.Bounds.Size.Y + margin * 2);
			if (expanded.HasPoint(p))
				return true;
		}
		return false;
	}

	private bool CanPlaceHouse(Vector2I houseCell)
	{
		for (int y = houseCell.Y - 4; y <= houseCell.Y + 1; y++)
		{
			for (int x = houseCell.X - 3; x <= houseCell.X + 3; x++)
			{
				if (!IsInBounds(x, y))
					return false;
				if (_reservedCells[x, y] || _pathCells[x, y] || _occupiedCells[x, y])
					return false;
			}
		}

		if (houseCell.DistanceTo(_monumentCell) < 22)
			return false;

		return true;
	}

	private void PlaceHouse(Vector2I houseCell, List<Vector2I> houses)
	{
		houses.Add(houseCell);
		// Reserve a tight area around the house during placement to avoid overlaps, then release
		// non-critical ground later for village decoration.
		ReserveRect(houseCell.X - 3, houseCell.Y - 4, 7, 6);
		CarveRect(houseCell.X - 1, houseCell.Y + 1, 3, 2, true, true);
	}

	private void ConnectHouseToCenter(Vector2I houseCell, Vector2I center)
	{
		Vector2I door = new(houseCell.X, houseCell.Y + 2);
		Vector2I junction = new(houseCell.X, center.Y);
		CarvePathLine(door.X, door.Y, junction.X, junction.Y);
		CarvePathLine(junction.X, junction.Y, center.X, center.Y);
		CarvePathLine(houseCell.X - 1, houseCell.Y + 1, houseCell.X + 1, houseCell.Y + 1);
	}

	private void DecorateAroundHouse(Vector2I houseCell, RandomNumberGenerator rng)
	{
		// Leave a few clear spots near houses so village decor can fill in naturally elsewhere.
		for (int i = 0; i < 4; i++)
		{
			int dx = rng.RandiRange(-4, 4);
			int dy = rng.RandiRange(-5, 2);
			int x = houseCell.X + dx;
			int y = houseCell.Y + dy;
			if (!IsInBounds(x, y) || _pathCells[x, y] || _occupiedCells[x, y])
				continue;
			if (Math.Abs(dx) <= 2 && dy >= -3 && dy <= 0)
				continue;
			if (rng.Randf() < 0.25f)
				_reservedCells[x, y] = true;
		}
	}

	private void ReserveMonumentArea(Vector2I cell)
	{
		ReserveRect(cell.X - 5, cell.Y - 5, 11, 11);
		CarveRect(cell.X - 1, cell.Y + 2, 3, 2, true, true);
	}

	private void CarveMonumentApproachPaths(Vector2I cell)
	{
		// Cardinal approach paths to make the monument easier to find.
		CarvePathLine(cell.X, 1, cell.X, Height - 2);
		CarvePathLine(1, cell.Y, Width - 2, cell.Y);
		// Widen crossing around monument slightly
		CarveRect(cell.X - 1, cell.Y - 2, 3, 5, true, true);
		CarveRect(cell.X - 2, cell.Y - 1, 5, 3, true, true);
	}

	private static Node2D CreateForestTree(int x, int y, RandomNumberGenerator rng)
	{
		int variant = rng.RandiRange(0, 2);
		return variant switch
		{
			0 => PropFactory.Create(x, y, "res://assets/images/map_tiles/trees/forest_tree-orange.png", "ForestTreeOrange", true, new Vector2(0, -34), new Vector2(14, 10), new Vector2(0, -2)),
			1 => PropFactory.Create(x, y, "res://assets/images/map_tiles/trees/forest_tree-pink.png", "ForestTreePink", true, new Vector2(0, -34), new Vector2(14, 10), new Vector2(0, -2)),
			_ => PropFactory.Create(x, y, "res://assets/images/map_tiles/trees/forest_tree-dried.png", "ForestTreeDried", true, new Vector2(0, -42), new Vector2(16, 10), new Vector2(0, -2)),
		};
	}

	private static Node2D CreateForestRock(int x, int y)
	{
		return PropFactory.Create(x, y, "res://assets/images/map_tiles/forest_rock.png", "ForestRock", true, new Vector2(0, -8), new Vector2(18, 10), new Vector2(0, -2), true);
	}

	private static Node2D CreateForestTrunk(int x, int y)
	{
		return PropFactory.Create(x, y, "res://assets/images/map_tiles/trees/forest_trunk.png", "ForestTrunk", true, new Vector2(0, -10), new Vector2(20, 8), new Vector2(0, -2), true);
	}

	private static Node2D CreateForestBush(int x, int y, RandomNumberGenerator rng)
	{
		string tex = rng.Randf() < 0.5f
			? "res://assets/images/map_tiles/plants/forest_bush.png"
			: "res://assets/images/map_tiles/plants/forest_bush-tall.png";
		return PropFactory.Create(x, y, tex, "ForestBush", false, Vector2.Zero, null, null, false);
	}

	private static Node2D CreateTownTree(int x, int y, RandomNumberGenerator rng)
	{
		int variant = rng.RandiRange(1, 2);
		return variant switch
		{
			1 => PropFactory.Create(x, y, "res://assets/images/map_tiles/trees/town_tree-1.png", "TownTree", true, new Vector2(0, -46), new Vector2(14, 10), new Vector2(0, -2)),
			_ => PropFactory.Create(x, y, "res://assets/images/map_tiles/trees/town_tree-2.png", "TownTree", true, new Vector2(0, -54), new Vector2(14, 10), new Vector2(0, -2)),
		};
	}

	private void PlaceTownTreesAroundVillages(Node2D treesLayer, RandomNumberGenerator rng)
	{
		foreach (VillageArea village in _villages)
		{
			int target = rng.RandiRange(4, 7);
			int placed = 0;
			for (int attempt = 0; attempt < 80 && placed < target; attempt++)
			{
				int side = rng.RandiRange(0, 3);
				int x = 0;
				int y = 0;
				switch (side)
				{
					case 0: // top band
						x = rng.RandiRange(village.Bounds.Position.X - 2, village.Bounds.End.X + 1);
						y = rng.RandiRange(village.Bounds.Position.Y - 3, village.Bounds.Position.Y - 1);
						break;
					case 1: // bottom band
						x = rng.RandiRange(village.Bounds.Position.X - 2, village.Bounds.End.X + 1);
						y = rng.RandiRange(village.Bounds.End.Y, village.Bounds.End.Y + 2);
						break;
					case 2: // left band
						x = rng.RandiRange(village.Bounds.Position.X - 3, village.Bounds.Position.X - 1);
						y = rng.RandiRange(village.Bounds.Position.Y - 1, village.Bounds.End.Y);
						break;
					default: // right band
						x = rng.RandiRange(village.Bounds.End.X, village.Bounds.End.X + 2);
						y = rng.RandiRange(village.Bounds.Position.Y - 1, village.Bounds.End.Y);
						break;
				}

				if (!CanPlaceTownTree(x, y, village.Index))
					continue;

				treesLayer.AddChild(CreateTownTree(x, y, rng));
				MarkOccupied(x, y);
				placed++;
			}
		}
	}

	private bool CanPlaceTownTree(int x, int y, int villageIndex)
	{
		if (x <= 1 || y <= 1 || x >= Width - 2 || y >= Height - 2)
			return false;
		if (_pathCells[x, y] || _occupiedCells[x, y] || _reservedCells[x, y])
			return false;

		// Must be around this village, not inside other villages, and clear of monument.
		bool nearOwn = false;
		for (int i = 0; i < _villages.Count; i++)
		{
			VillageArea v = _villages[i];
			Rect2I expanded = new(v.Bounds.Position.X - 3, v.Bounds.Position.Y - 3, v.Bounds.Size.X + 6, v.Bounds.Size.Y + 6);
			if (!expanded.HasPoint(new Vector2I(x, y)))
				continue;
			if (i != villageIndex)
				return false;
			nearOwn = true;
		}

		if (!nearOwn)
			return false;
		if (new Vector2I(x, y).DistanceTo(_monumentCell) < 12)
			return false;

		return true;
	}

	private static Node2D CreateTownBush(int x, int y, RandomNumberGenerator rng)
	{
		int variant = rng.RandiRange(1, 3);
		string tex = variant switch
		{
			1 => "res://assets/images/map_tiles/trees/town_bush-1.png",
			2 => "res://assets/images/map_tiles/trees/town_bush-2.png",
			_ => "res://assets/images/map_tiles/trees/town_bush-3.png",
		};
		return PropFactory.Create(x, y, tex, "TownBush", true, new Vector2(0, -12), new Vector2(14, 8), new Vector2(0, -2), true);
	}

	private void MarkOccupied(int x, int y)
	{
		if (!IsInBounds(x, y))
			return;
		_occupiedCells[x, y] = true;
		_reservedCells[x, y] = true;
	}

	private void MarkOccupiedRect(int startX, int startY, int w, int h)
	{
		for (int y = startY; y < startY + h; y++)
			for (int x = startX; x < startX + w; x++)
				MarkOccupied(x, y);
	}

	private void MarkHouseFootprint(int houseCellX, int houseCellY)
	{
		MarkOccupiedRect(houseCellX - 2, houseCellY - 1, 5, 2);
	}

	private void CarvePathLine(int x0, int y0, int x1, int y1)
	{
		int x = x0;
		int y = y0;
		int dx = Math.Abs(x1 - x0);
		int sx = x0 < x1 ? 1 : -1;
		int dy = -Math.Abs(y1 - y0);
		int sy = y0 < y1 ? 1 : -1;
		int err = dx + dy;

		while (true)
		{
			SetPath(x, y);
			if (x == x1 && y == y1)
				break;
			int e2 = 2 * err;
			if (e2 >= dy)
			{
				err += dy;
				x += sx;
			}
			if (e2 <= dx)
			{
				err += dx;
				y += sy;
			}
		}
	}

	private void SetPath(int x, int y)
	{
		if (!IsInBounds(x, y))
			return;
		if (x == 0 || y == 0 || x == Width - 1 || y == Height - 1)
			return;
		_pathCells[x, y] = true;
		_reservedCells[x, y] = true;
	}

	private void CarveRect(int startX, int startY, int w, int h, bool markPath, bool markReserved)
	{
		for (int y = startY; y < startY + h; y++)
		{
			for (int x = startX; x < startX + w; x++)
			{
				if (!IsInBounds(x, y))
					continue;
				if (x == 0 || y == 0 || x == Width - 1 || y == Height - 1)
					continue;
				if (markPath)
					_pathCells[x, y] = true;
				if (markReserved)
					_reservedCells[x, y] = true;
			}
		}
	}

	private void ReserveRect(int startX, int startY, int w, int h)
	{
		CarveRect(startX, startY, w, h, false, true);
	}

	private static bool IsInBounds(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;

	private static void Shuffle<T>(IList<T> list, RandomNumberGenerator rng)
	{
		for (int i = list.Count - 1; i > 0; i--)
		{
			int j = rng.RandiRange(0, i);
			(list[i], list[j]) = (list[j], list[i]);
		}
	}
}
