#nullable enable
using Godot;
using System.Collections.Generic;

namespace GridQuest.Entities;

public partial class EntityPopulationManager : Node
{
	private static readonly PackedScene EntityScene = GD.Load<PackedScene>("res://entities/llm_entity.tscn");
	private static readonly string[] Names = { "Mara", "Tobin", "Juniper", "Bram", "Pip", "Nell" };
	private static readonly string[] Prompts =
	{
		"You are a thoughtful villager who likes learning about the people and places around you. Explore, ask questions, and remember what matters to you.",
		"You are an energetic villager who enjoys exploring and meeting others. Make your own choices, use your tools, and remember discoveries.",
		"You are a cautious but kind villager. Pay attention to your surroundings, seek out interesting places, and decide independently what to do.",
		"You are a practical villager who likes helping others. Observe the world, talk to nearby characters, and build memories through your actions.",
		"You are a curious villager with a playful outlook. Explore safely, interact with others, and act on what you learn.",
		"You are a quiet, observant villager. Notice changes in your environment, form your own goals, and keep useful memories."
	};

	[Export] public int EntitiesPerVillage { get; set; } = 3;
	[Export] public float RespawnDelaySeconds { get; set; } = 20.0f;
	[Export] public float DecisionIntervalSeconds { get; set; } = 20.0f;

	private readonly Dictionary<int, List<PendingRespawn>> _respawns = new();
	private global::Map? _map;

	private sealed class PendingRespawn
	{
		public float TimeLeft;
		public int ProfileIndex;
	}

	public override void _Ready()
	{
		AddToGroup("entity_population_manager");
		CallDeferred(nameof(Initialize));
	}

	private void Initialize()
	{
		_map = GetTree().CurrentScene?.GetNodeOrNull<global::Map>("Map") ?? GetParent()?.GetNodeOrNull<global::Map>("Map");
		if (_map == null)
		{
			GD.PushError("Could not initialize entity population: the current scene has no Map node.");
			return;
		}

		_respawns.Clear();
		for (int villageIndex = 0; villageIndex < _map.Villages.Count; villageIndex++)
		{
			_respawns[villageIndex] = new List<PendingRespawn>();
			for (int index = 0; index < EntitiesPerVillage; index++)
				SpawnEntity(villageIndex, index);
		}
	}

	public void ResetForNewMap() => Initialize();

	public override void _Process(double delta)
	{
		if (_map == null) return;
		foreach (var pair in _respawns)
		{
			for (int index = pair.Value.Count - 1; index >= 0; index--)
			{
				pair.Value[index].TimeLeft -= (float)delta;
				if (pair.Value[index].TimeLeft > 0.0f || IsVillageAreaOnScreen(pair.Key)) continue;
				int profileIndex = pair.Value[index].ProfileIndex;
				pair.Value.RemoveAt(index);
				SpawnEntity(pair.Key, profileIndex);
			}
		}
	}

	private void SpawnEntity(int villageIndex, int profileIndex)
	{
		if (_map == null || villageIndex < 0 || villageIndex >= _map.Villages.Count) return;
		if (EntityScene == null)
		{
			GD.PushError("Could not load the generic LLM entity scene.");
			return;
		}

		var entity = EntityScene.Instantiate<LlmEntity>();
		int profile = Mathf.PosMod(villageIndex * EntitiesPerVillage + profileIndex, Names.Length);
		entity.Name = Names[profile] + " " + (villageIndex + 1);
		entity.SkinId = SkinCatalog.All[(profile % (SkinCatalog.All.Count - 1)) + 1].Id;
		entity.SystemPrompt = Prompts[profile];
		entity.DecisionIntervalSeconds = DecisionIntervalSeconds;
		entity.GlobalPosition = _map.GetRandomPointInVillage(villageIndex);
		entity.Faction = "villager";
		entity.Died += () => OnEntityDied(entity, villageIndex, profileIndex);
		(GetTree().CurrentScene ?? this).AddChild(entity);
	}

	private void OnEntityDied(LlmEntity entity, int villageIndex, int profileIndex)
	{
		if (IsInstanceValid(entity)) entity.QueueFree();
		if (!_respawns.TryGetValue(villageIndex, out var pending)) return;
		pending.Add(new PendingRespawn
		{
			TimeLeft = Mathf.Max(0.0f, RespawnDelaySeconds),
			ProfileIndex = profileIndex
		});
	}

	private bool IsVillageAreaOnScreen(int villageIndex)
	{
		if (_map == null || villageIndex < 0 || villageIndex >= _map.Villages.Count) return false;
		Camera2D? camera = GetViewport().GetCamera2D();
		if (camera == null) return false;
		global::Map.VillageArea village = _map.Villages[villageIndex];
		Vector2 center = _map.CellToWorldCenter(village.Center.X, village.Center.Y);
		Vector2 zoom = camera.Zoom == Vector2.Zero ? Vector2.One : camera.Zoom;
		Vector2 worldSize = GetViewport().GetVisibleRect().Size / zoom;
		var visible = new Rect2(camera.GetScreenCenterPosition() - worldSize / 2.0f, worldSize).Grow(32.0f);
		return visible.HasPoint(center);
	}
}
