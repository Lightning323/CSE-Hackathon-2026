using System;
using System.Collections.Generic;

namespace GridQuest.entities.player;

public sealed class UpgradeDef
{
	public string Id { get; init; }
	public string Name { get; init; }
	public string Description { get; init; }
	public int BaseCost { get; init; }
	public int CostPerLevel { get; init; }

	public int GetCost(int currentLevel)
	{
		if (currentLevel < 0) currentLevel = 0;
		return BaseCost + CostPerLevel * currentLevel;
	}
}

public static class UpgradeCatalog
{
	public static readonly List<UpgradeDef> All = new()
	{
		new UpgradeDef { Id = "player_movement_speed", Name = "Player Movement Speed", Description = "Increase base movement speed.", BaseCost = 1, CostPerLevel = 2 },
		new UpgradeDef { Id = "player_acceleration", Name = "Player Acceleration", Description = "Increase movement acceleration/responsiveness.", BaseCost = 1, CostPerLevel = 2 },
		new UpgradeDef { Id = "attack_speed", Name = "Attack Speed", Description = "Reduce attack cooldown for faster attacks.", BaseCost = 2, CostPerLevel = 2 },
		new UpgradeDef { Id = "attack_damage", Name = "Attack Damage", Description = "Increase melee and weapon attack damage.", BaseCost = 2, CostPerLevel = 2 },
		new UpgradeDef { Id = "maximum_health", Name = "Maximum Health", Description = "Increase maximum health.", BaseCost = 2, CostPerLevel = 3 },
		new UpgradeDef { Id = "slow_health_regeneration", Name = "Slow Health Regeneration", Description = "Regenerate health slowly over time.", BaseCost = 1, CostPerLevel = 2 },
		new UpgradeDef { Id = "health_regeneration_delay", Name = "Health Regeneration Delay", Description = "Reduce delay before health regeneration starts after taking damage.", BaseCost = 1, CostPerLevel = 2 },
		new UpgradeDef { Id = "damage_reduction_armor", Name = "Damage Reduction Armor", Description = "Reduce incoming damage.", BaseCost = 2, CostPerLevel = 3 },
		new UpgradeDef { Id = "lifesteal", Name = "Lifesteal", Description = "Heal when dealing damage.", BaseCost = 2, CostPerLevel = 3 },
		new UpgradeDef { Id = "thorns_damage", Name = "Thorns Damage", Description = "Deal damage back to attackers when hit.", BaseCost = 2, CostPerLevel = 3 },
		new UpgradeDef { Id = "knockback_strength", Name = "Knockback Strength", Description = "Increase knockback dealt to enemies.", BaseCost = 1, CostPerLevel = 2 },
		new UpgradeDef { Id = "projectile_speed", Name = "Projectile Speed", Description = "Increase projectile travel speed.", BaseCost = 1, CostPerLevel = 2 },
		new UpgradeDef { Id = "projectile_damage", Name = "Projectile Damage", Description = "Increase projectile damage.", BaseCost = 2, CostPerLevel = 2 },
		new UpgradeDef { Id = "melee_range", Name = "Melee Range", Description = "Increase melee attack range.", BaseCost = 1, CostPerLevel = 2 },
		new UpgradeDef { Id = "movement_speed_after_taking_damage", Name = "Movement Speed After Taking Damage", Description = "Gain a temporary movement speed boost after taking damage.", BaseCost = 1, CostPerLevel = 2 },
		new UpgradeDef { Id = "movement_speed_after_killing_an_enemy", Name = "Movement Speed After Killing an Enemy", Description = "Gain a temporary movement speed boost after killing an enemy.", BaseCost = 1, CostPerLevel = 2 },
		new UpgradeDef { Id = "bonus_damage_to_bosses", Name = "Bonus Damage to Bosses", Description = "Deal bonus damage to bosses.", BaseCost = 2, CostPerLevel = 3 },
	};

	private static readonly Dictionary<string, UpgradeDef> ById = BuildMap();

	private static Dictionary<string, UpgradeDef> BuildMap()
	{
		var map = new Dictionary<string, UpgradeDef>(StringComparer.Ordinal);
		foreach (var d in All) map[d.Id] = d;
		return map;
	}

	public static UpgradeDef Get(string id) => id != null && ById.TryGetValue(id, out var d) ? d : null;
}
