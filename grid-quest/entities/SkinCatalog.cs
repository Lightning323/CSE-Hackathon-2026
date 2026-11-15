using Godot;
using System.Collections.Generic;

namespace GridQuest.Entities;

public sealed class EntitySkin
{
	public string Id { get; }
	public string DisplayName { get; }
	public string SpriteSheetPath { get; }

	public EntitySkin(string id, string displayName, string spriteSheetPath)
	{
		Id = id;
		DisplayName = displayName;
		SpriteSheetPath = spriteSheetPath;
	}
}

public static class SkinCatalog
{
	private const string VillagerPath = "res://assets/images/characters/villagers/";

	public static IReadOnlyList<EntitySkin> All { get; } = new[]
	{
		new EntitySkin("player", "Player", "res://assets/images/characters/cute_Player.png"),
		new EntitySkin("folk-dogwalker", "Dogwalker", VillagerPath + "folk-dogwalker.png"),
		new EntitySkin("folk-grandma", "Grandma", VillagerPath + "folk-grandma.png"),
		new EntitySkin("school-teacher", "Teacher", VillagerPath + "school-teacher.png"),
		new EntitySkin("season-pumpkin", "Pumpkin", VillagerPath + "season-pumpkin.png"),
		new EntitySkin("work-chef", "Chef", VillagerPath + "work-chef.png"),
		new EntitySkin("shop-barista", "Barista", VillagerPath + "shop-barista.png")
	};

	public static EntitySkin Get(string id)
	{
		foreach (var skin in All)
			if (skin.Id == id) return skin;
		return All[0];
	}

	public static SpriteFrames BuildSpriteFrames(string skinId)
	{
		EntitySkin skin = Get(skinId);
		Texture2D texture = GD.Load<Texture2D>(skin.SpriteSheetPath);
		var frames = new SpriteFrames();
		frames.RemoveAnimation("default");

		foreach (string direction in new[] { "down", "side", "up" })
		{
			AddAnimation(frames, texture, $"idle_{direction}", 0, 3, 4.0f, true);
			AddAnimation(frames, texture, $"walk_{direction}", 1, 3, 8.0f, true);
			AddAnimation(frames, texture, $"attack_{direction}", 2, 3, 8.0f, false);
		}
		AddAnimation(frames, texture, "death", 2, 3, 6.0f, false);
		return frames;
	}

	private static void AddAnimation(SpriteFrames frames, Texture2D texture, string name, int row, int count, float fps, bool loop)
	{
		frames.AddAnimation(name);
		frames.SetAnimationSpeed(name, fps);
		frames.SetAnimationLoopMode(name, loop ? SpriteFrames.LoopMode.Linear : SpriteFrames.LoopMode.None);
		for (int frame = 0; frame < count; frame++)
			frames.AddFrame(name, new AtlasTexture
			{
				Atlas = texture,
				Region = new Rect2(frame * 32, row * 32, 32, 32)
			});
	}
}
