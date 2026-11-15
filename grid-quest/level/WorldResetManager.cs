using Godot;
using GridQuest.Entities;

public partial class WorldResetManager : Node
{
	public override void _Ready()
	{
		AddToGroup("world_reset_manager");
	}

	public bool TryRandomizeWorld()
	{
		int cost = WorldProgress.GetWorldResetCost();
		Entity player = GetTree().GetFirstNodeInGroup("controlled_entity") as Entity ?? GetTree().GetFirstNodeInGroup("player") as Entity;
		Map map = GetTree().GetFirstNodeInGroup("map") as Map ?? GetTree().CurrentScene?.GetNodeOrNull<Map>("Map");
		if (player == null || map == null)
			return false;

		if (player.Gold < cost)
			return false;

		// Record progress/high score for reaching this reset, then spend and advance.
		WorldProgress.RegisterResetHighScoreProgress();
		player.Gold -= cost;

		ClearTransientWorldEntities();
		WorldProgress.CompleteWorldReset();
		map.GenerateMap();
		player.GlobalPosition = map.GetRandomPlayerSpawnPosition();
		ReinitializeWorldSystems();

		if (GetTree().GetFirstNodeInGroup("day_night_cycle") is DayNightCycle cycle)
			cycle.SetToSunrise(emitSunriseEvent: false);

		var monumentIndicator = GetTree().GetFirstNodeInGroup("monument_indicator") as Control;
		if (monumentIndicator != null && monumentIndicator.HasMethod("Refresh"))
			monumentIndicator.CallDeferred("Refresh");

		return true;
	}

	private void ClearTransientWorldEntities()
	{
		string[] groups = { "llm_entities", "coin_pickups", "projectiles" };
		foreach (string group in groups)
		{
			foreach (Node node in GetTree().GetNodesInGroup(group))
			{
				if (node.IsInGroup("controlled_entity") || node.IsInGroup("player")) continue;
				if (IsInstanceValid(node))
					node.QueueFree();
			}
		}

		// Extra safety for fireballs/coins not in expected groups.
		var scene = GetTree().CurrentScene;
		if (scene == null) return;
		foreach (Node child in scene.GetChildren())
		{
			if (child is Map || child is CanvasLayer || child is CanvasModulate) continue;
			if (child is Node && (child.Name.ToString().Contains("Fireball", System.StringComparison.OrdinalIgnoreCase) || child.Name.ToString().Contains("Coin", System.StringComparison.OrdinalIgnoreCase)))
				child.QueueFree();
		}
	}

	private void ReinitializeWorldSystems()
	{
		// Entity population is managed by level content; no automatic repopulation required.
	}
}
