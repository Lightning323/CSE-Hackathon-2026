#nullable enable
using Godot;

namespace GridQuest.Entities.Enemy;

public partial class GoblinEnemy : EnemyEntity
{
	private static readonly Texture2D IdleTex = GD.Load<Texture2D>("res://assets/images/enemies/Goblin/gobu Idle.png");
	private static readonly Texture2D WalkTex = GD.Load<Texture2D>("res://assets/images/enemies/Goblin/gobu walk.png");
	private static readonly Texture2D AttackTex = GD.Load<Texture2D>("res://assets/images/enemies/Goblin/gobu Hop.png");

	protected override void OnEnemyReady()
	{
		// Small goblin that can fit through doors; shrink collision if present.
		var shapeNode = GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
		if (shapeNode?.Shape is CircleShape2D circle)
		{
			var smaller = (CircleShape2D)circle.Duplicate();
			smaller.Radius = 6.0f;
			shapeNode.Shape = smaller;
		}
		Sprite.Position = new Vector2(0, -4);
	}

	public override bool CanSpawnInBuildingInterior => true;
	public override bool CanSpawnInDungeonOnly => true;

	protected override SpriteFrames BuildSpriteFrames()
	{
		var frames = new SpriteFrames();
		frames.RemoveAnimation("default");
		// 3 rows (down, side, up), 32x32 frames
		AddGridAnimation(frames, "idle_down", IdleTex, 32, 32, 0, 3, 6.0f, true);
		AddGridAnimation(frames, "idle_side", IdleTex, 32, 32, 1, 3, 6.0f, true);
		AddGridAnimation(frames, "idle_up", IdleTex, 32, 32, 2, 3, 6.0f, true);
		AddGridAnimation(frames, "walk_down", WalkTex, 32, 32, 0, 6, 10.0f, true);
		AddGridAnimation(frames, "walk_side", WalkTex, 32, 32, 1, 6, 10.0f, true);
		AddGridAnimation(frames, "walk_up", WalkTex, 32, 32, 2, 6, 10.0f, true);
		AddGridAnimation(frames, "attack_down", AttackTex, 32, 32, 0, 8, 12.0f, false);
		AddGridAnimation(frames, "attack_side", AttackTex, 32, 32, 1, 8, 12.0f, false);
		AddGridAnimation(frames, "attack_up", AttackTex, 32, 32, 2, 8, 12.0f, false);
		return frames;
	}

	private static void AddGridAnimation(SpriteFrames frames, string name, Texture2D texture, int frameW, int frameH, int row, int count, float fps, bool loop)
	{
		if (texture == null) return;
		frames.AddAnimation(name);
		frames.SetAnimationSpeed(name, fps);
		frames.SetAnimationLoop(name, loop);
		for (int i = 0; i < count; i++)
		{
			frames.AddFrame(name, new AtlasTexture
			{
				Atlas = texture,
				Region = new Rect2(i * frameW, row * frameH, frameW, frameH)
			});
		}
	}
}
