#nullable enable
using Godot;

namespace GridQuest.Entities.Enemy;

public partial class BloodMonsterEnemy : EnemyEntity
{
	private static readonly Texture2D IdleTex = GD.Load<Texture2D>("res://assets/images/enemies/Blood_Monster/Blood Monster_A_Idle.png");
	private static readonly Texture2D WalkTex = GD.Load<Texture2D>("res://assets/images/enemies/Blood_Monster/Blood Monster_A_Walk.png");
	private static readonly Texture2D AttackTex = GD.Load<Texture2D>("res://assets/images/enemies/Blood_Monster/Blood Monster_A_Attack01.png");

	protected override void OnEnemyReady()
	{
		// Large sprite; keep centered-ish visuals
		Sprite.Position = new Vector2(0, -18);
	}

	public override bool CanSpawnInBuildingInterior => false;
	public override bool IsDayThreeOnlySpawn => true;

	protected override SpriteFrames BuildSpriteFrames()
	{
		var frames = new SpriteFrames();
		frames.RemoveAnimation("default");
		AddSheetAnimation(frames, "idle_down", IdleTex, 100, 100, 6, 5.0f, true);
		AddSheetAnimation(frames, "idle_side", IdleTex, 100, 100, 6, 5.0f, true);
		AddSheetAnimation(frames, "idle_up", IdleTex, 100, 100, 6, 5.0f, true);
		AddSheetAnimation(frames, "walk_down", WalkTex, 100, 100, 8, 10.0f, true);
		AddSheetAnimation(frames, "walk_side", WalkTex, 100, 100, 8, 10.0f, true);
		AddSheetAnimation(frames, "walk_up", WalkTex, 100, 100, 8, 10.0f, true);
		AddSheetAnimation(frames, "attack_down", AttackTex, 100, 100, 8, 12.0f, false);
		AddSheetAnimation(frames, "attack_side", AttackTex, 100, 100, 8, 12.0f, false);
		AddSheetAnimation(frames, "attack_up", AttackTex, 100, 100, 8, 12.0f, false);
		return frames;
	}

	private static void AddSheetAnimation(SpriteFrames frames, string name, Texture2D texture, int frameW, int frameH, int count, float fps, bool loop)
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
				Region = new Rect2(i * frameW, 0, frameW, frameH)
			});
		}
	}
}
