using Godot;
using System.Collections.Generic;

public partial class VillagePopulationManager : Node
{
	[Export] public int VillagersPerVillage { get; set; } = 4;
	[Export] public float RespawnDelaySeconds { get; set; } = 20.0f;

	private readonly Dictionary<int, List<PendingRespawn>> _pending = new();
	private Map _map;
	private static readonly PackedScene VillagerScene = GD.Load<PackedScene>("res://npc/villager/villager.tscn");

	private sealed class PendingRespawn
	{
		public float TimeLeft;
	}

	public override void _Ready()
	{
		CallDeferred(nameof(Initialize));
	}

	private void Initialize()
	{
		_map = GetTree().CurrentScene?.GetNodeOrNull<Map>("Map") ?? GetParent()?.GetNodeOrNull<Map>("Map");
		if (_map == null)
			return;

		var villages = _map.Villages;
		for (int i = 0; i < villages.Count; i++)
		{
			_pending[i] = new List<PendingRespawn>();
			for (int j = 0; j < VillagersPerVillage; j++)
			{
				SpawnVillager(i);
			}
		}
	}

	public override void _Process(double delta)
	{
		if (_map == null)
			return;

		float dt = (float)delta;
		for (int i = 0; i < _map.Villages.Count; i++)
		{
			if (!_pending.TryGetValue(i, out List<PendingRespawn> list) || list.Count == 0)
				continue;

			for (int p = list.Count - 1; p >= 0; p--)
			{
				list[p].TimeLeft -= dt;
				if (list[p].TimeLeft > 0.0f)
					continue;

				if (IsVillageAreaOnScreen(i))
					continue;

				list.RemoveAt(p);
				SpawnVillager(i);
			}
		}
	}

	private void SpawnVillager(int villageIndex)
	{
		if (_map == null || villageIndex < 0 || villageIndex >= _map.Villages.Count)
			return;

		Node instance = VillagerScene.Instantiate();
		if (instance is not Villager villager)
		{
			instance.QueueFree();
			return;
		}

		Map.VillageArea village = _map.Villages[villageIndex];
		villager.GlobalPosition = _map.GetRandomPointInVillage(villageIndex);
		villager.SetHome(village.Bounds, village.Center, villageIndex);
		villager.Died += OnVillagerDied;

		Node parent = GetTree().CurrentScene ?? this;
		parent.AddChild(villager);
	}

	private void OnVillagerDied(GridQuest.Entities.Npc.VillagerEntity villager, int villageIndex)
	{
		if (villageIndex < 0)
			return;
		if (!_pending.TryGetValue(villageIndex, out List<PendingRespawn> list))
		{
			list = new List<PendingRespawn>();
			_pending[villageIndex] = list;
		}
		list.Add(new PendingRespawn { TimeLeft = RespawnDelaySeconds });
	}

	private bool IsVillageAreaOnScreen(int villageIndex)
	{
		Camera2D camera = GetViewport().GetCamera2D();
		if (camera == null)
			return false;

		Map.VillageArea village = _map.Villages[villageIndex];
		Vector2 center = _map.CellToWorldCenter(village.Center.X, village.Center.Y);
		Rect2 visible = GetCameraWorldRect(camera);
		visible = visible.Grow(32.0f);
		return visible.HasPoint(center);
	}

	private Rect2 GetCameraWorldRect(Camera2D camera)
	{
		Vector2 viewportSize = GetViewport().GetVisibleRect().Size;
		Vector2 zoom = camera.Zoom;
		if (zoom.X == 0 || zoom.Y == 0)
			zoom = Vector2.One;
		Vector2 worldSize = viewportSize / zoom;
		Vector2 topLeft = camera.GetScreenCenterPosition() - worldSize / 2.0f;
		return new Rect2(topLeft, worldSize);
	}
}
