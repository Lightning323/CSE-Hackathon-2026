using Godot;

public partial class WorldResetManager : Node
{
	public override void _Ready()
	{
		AddToGroup("world_reset_manager");
	}

	public bool TryRandomizeWorld()
	{
		int cost = WorldProgress.GetWorldResetCost();
		Player player = GetTree().GetFirstNodeInGroup("player") as Player;
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
		string[] groups = { "enemies", "villager_npcs", "knights", "villagers", "traders", "coin_pickups", "projectiles" };
		foreach (string group in groups)
		{
			foreach (Node node in GetTree().GetNodesInGroup(group))
			{
				if (node is Player) continue;
				if (IsInstanceValid(node))
					node.QueueFree();
			}
		}

		// Extra safety for fireballs/coins not in expected groups.
		var scene = GetTree().CurrentScene;
		if (scene == null) return;
		foreach (Node child in scene.GetChildren())
		{
			if (child is Player || child is Map || child is CanvasLayer || child is CanvasModulate) continue;
			if (child is Node && (child.Name.ToString().Contains("Fireball", System.StringComparison.OrdinalIgnoreCase) || child.Name.ToString().Contains("Coin", System.StringComparison.OrdinalIgnoreCase)))
				child.QueueFree();
		}
	}

	private void ReinitializeWorldSystems()
	{
		if (GetTree().GetFirstNodeInGroup("village_population_manager") is VillagePopulationManager vpm && vpm.HasMethod("ResetForNewMap"))
			vpm.CallDeferred("ResetForNewMap");
		else if (GetTree().CurrentScene?.GetNodeOrNull<Node>("VillagePopulationManager") is Node n1 && n1.HasMethod("ResetForNewMap"))
			n1.CallDeferred("ResetForNewMap");

		if (GetTree().GetFirstNodeInGroup("village_knight_population_manager") is VillageKnightPopulationManager kpm && kpm.HasMethod("ResetForNewMap"))
			kpm.CallDeferred("ResetForNewMap");
		else if (GetTree().CurrentScene?.GetNodeOrNull<Node>("VillageKnightPopulationManager") is Node n2 && n2.HasMethod("ResetForNewMap"))
			n2.CallDeferred("ResetForNewMap");

		if (GetTree().GetFirstNodeInGroup("enemy_spawn_manager") is EnemySpawnOnKill esm && esm.HasMethod("ResetForNewMap"))
			esm.CallDeferred("ResetForNewMap");
		else if (GetTree().CurrentScene?.GetNodeOrNull<Node>("EnemySpawnOnKill") is Node n3 && n3.HasMethod("ResetForNewMap"))
			n3.CallDeferred("ResetForNewMap");
	}
}
