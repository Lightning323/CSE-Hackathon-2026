#nullable enable
using System.Collections.Generic;
using Godot;

namespace GridQuest.Entities.Npc;

/// <summary>
/// Spawns and respawns allied knights per generated village.
/// </summary>
public partial class VillageKnightPopulationManager : Node
{
	[Export] public int MinKnightsPerVillage { get; set; } = 2;
	[Export] public int MaxKnightsPerVillage { get; set; } = 2;
	[Export] public float RespawnDelaySeconds { get; set; } = 10.0f;

	private readonly Dictionary<int, List<PendingRespawn>> _pending = new();
	private global::Map? _map;
	private static readonly PackedScene KnightScene = GD.Load<PackedScene>("res://npc/knight_npc.tscn");

	private sealed class PendingRespawn { public float TimeLeft; }

	public override void _Ready() => CallDeferred(nameof(Initialize));

	private void Initialize()
	{
		_map = GetTree().CurrentScene?.GetNodeOrNull<global::Map>("Map") ?? GetParent()?.GetNodeOrNull<global::Map>("Map");
		if (_map == null) return;

		var rng = new RandomNumberGenerator();
		rng.Randomize();
		var villages = _map.Villages;
		for (int i = 0; i < villages.Count; i++)
		{
			_pending[i] = new List<PendingRespawn>();
			int count = rng.RandiRange(MinKnightsPerVillage, MaxKnightsPerVillage);
			for (int j = 0; j < count; j++) SpawnKnight(i);
		}
	}

	public override void _Process(double delta)
	{
		if (_map == null) return;
		float dt = (float)delta;
		for (int i = 0; i < _map.Villages.Count; i++)
		{
			if (!_pending.TryGetValue(i, out List<PendingRespawn>? list) || list == null || list.Count == 0) continue;
			for (int p = list.Count - 1; p >= 0; p--)
			{
				list[p].TimeLeft -= dt;
				if (list[p].TimeLeft > 0.0f) continue;
				if (IsVillageAreaOnScreen(i)) continue;
				list.RemoveAt(p);
				SpawnKnight(i);
			}
		}
	}

	private void SpawnKnight(int villageIndex)
	{
		if (_map == null || villageIndex < 0 || villageIndex >= _map.Villages.Count) return;
		Node instance = KnightScene.Instantiate();
		if (instance is not Knight knight)
		{
			instance.QueueFree();
			return;
		}
		global::Map.VillageArea village = _map.Villages[villageIndex];
		knight.GlobalPosition = _map.GetRandomPointInVillage(villageIndex);
		knight.SetHome(village.Bounds, village.Center, villageIndex);
		knight.Died += OnKnightDied;
		Node parent = GetTree().CurrentScene ?? this;
		parent.AddChild(knight);
	}

	private void OnKnightDied(Knight knight, int villageIndex)
	{
		if (villageIndex < 0) return;
		if (!_pending.TryGetValue(villageIndex, out List<PendingRespawn>? list) || list == null)
		{
			list = new List<PendingRespawn>();
			_pending[villageIndex] = list;
		}
		list.Add(new PendingRespawn { TimeLeft = RespawnDelaySeconds });
	}

	private bool IsVillageAreaOnScreen(int villageIndex)
	{
		if (_map == null) return false;
		Camera2D? camera = GetViewport().GetCamera2D();
		if (camera == null) return false;
		global::Map.VillageArea village = _map.Villages[villageIndex];
		Vector2 center = _map.CellToWorldCenter(village.Center.X, village.Center.Y);
		Rect2 visible = GetCameraWorldRect(camera).Grow(32.0f);
		return visible.HasPoint(center);
	}

	private Rect2 GetCameraWorldRect(Camera2D camera)
	{
		Vector2 viewportSize = GetViewport().GetVisibleRect().Size;
		Vector2 zoom = camera.Zoom == Vector2.Zero ? Vector2.One : camera.Zoom;
		Vector2 worldSize = viewportSize / zoom;
		Vector2 topLeft = camera.GetScreenCenterPosition() - worldSize / 2.0f;
		return new Rect2(topLeft, worldSize);
	}
}
