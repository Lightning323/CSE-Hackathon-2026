using System;
using Godot;
using GridQuest.Map;

namespace GridQuest;

public partial class MapGrid : Node2D
{
	[Export] public int Width { get; set; } = 20;
	[Export] public int Height { get; set; } = 20;
	[Export] public int CellSize { get; set; } = 16;
	[Export(PropertyHint.Range, "0,1,0.01")] public float PlantChance { get; set; } = 0.08f;
	[Export] public int RandomSeed { get; set; } = 1337;

	private Node2D[,] _occupants = new Node2D[0, 0];

	public override void _Ready()
	{
		_occupants = new Node2D[Width, Height];
		GenerateFloor();
		PlaceCenterTree();
		GeneratePlants();
	}

	private void GenerateFloor()
	{
		AddChild(GrassFloor.CreateLayer(Width, Height, CellSize));
	}

	private void PlaceCenterTree()
	{
		int cx = Width / 2;
		int cy = Height / 2;
		if (!IsInBounds(cx, cy))
		{
			return;
		}

		Node2D objectsLayer = GetNodeOrNull<Node2D>("Objects") ?? new Node2D { Name = "Objects" };
		if (objectsLayer.GetParent() == null)
		{
			AddChild(objectsLayer);
		}

		Node2D tree = TownTree2.Create(CellToWorldCenter(cx, cy), CellSize);
		objectsLayer.AddChild(tree);
		_occupants[cx, cy] = tree;
	}

	private void GeneratePlants()
	{
		Node2D itemsLayer = new() { Name = "Items" };
		AddChild(itemsLayer);

		Func<Vector2, Node2D>[] plantFactories =
		{
			TownPlant1.Create,
			TownPlant2.Create,
			TownPlant3.Create,
		};

		var rng = new RandomNumberGenerator();
		rng.Seed = (ulong)RandomSeed;

		for (int y = 0; y < Height; y++)
		{
			for (int x = 0; x < Width; x++)
			{
				if (_occupants[x, y] != null)
				{
					continue;
				}

				if (rng.Randf() > PlantChance)
				{
					continue;
				}

				Node2D plant = plantFactories[rng.RandiRange(0, plantFactories.Length - 1)](CellToWorldCenter(x, y));
				itemsLayer.AddChild(plant);
				_occupants[x, y] = plant;
			}
		}
	}

	public Vector2 CellToWorldCenter(int x, int y)
	{
		return new Vector2(x * CellSize + CellSize / 2.0f, y * CellSize + CellSize / 2.0f);
	}

	public Vector2I WorldToCell(Vector2 world)
	{
		return new Vector2I(Mathf.FloorToInt(world.X / CellSize), Mathf.FloorToInt(world.Y / CellSize));
	}

	public bool IsInBounds(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;

	public Node2D GetOccupant(int x, int y) => IsInBounds(x, y) ? _occupants[x, y] : null;

	public bool TryPlaceOccupant(int x, int y, Node2D node)
	{
		if (!IsInBounds(x, y) || _occupants[x, y] != null)
		{
			return false;
		}
		_occupants[x, y] = node;
		return true;
	}
}
