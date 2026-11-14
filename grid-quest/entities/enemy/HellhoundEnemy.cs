using Godot;

namespace GridQuest.Entities.Enemy;

public partial class HellhoundEnemy : EnemyEntity
{
	[Export] public int Variant { get; set; } = 1; // 1,2,3 (Hellhound_1..3)

	public override void _Ready()
	{
		Variant = Mathf.Clamp(Variant, 1, 3);
		TexturePath = $"res://assets/images/enemies/Hellhound_{Variant}/Idle.png";
		base.Speed = 1000.0f; // Extremely fast swarm unit
		base.MaxHealth = 8;   // Individual hound health; boss fight uses a large streaming health pool
		base.AttackDamage = 1;
		base.AttackRange = 22.0f;
		base.AttackCooldown = 0.55f;
		CoinDropCount = 0; // Boss swarm reward handled by boss manager on final kill
		base._Ready();
		Scale = new Vector2(0.5f, 0.5f);
	}

	protected override bool ShouldFlipHorizontallyForSide() => true;

	protected override void UpdateFacing(Vector2 direction)
	{
		// Side-view hellhound sheets: flip horizontally based on X direction.
		FaceLeft = direction.X < 0.0f;
		Facing = "side";
		ApplyFacingFlip();
	}

	protected override string GetAnimationName(EnemyState state, string facing)
	{
		return state switch
		{
			EnemyState.Walk => "run",
			EnemyState.Attack => "attack",
			_ => "idle",
		};
	}

	protected override SpriteFrames BuildSpriteFrames()
	{
		SpriteFrames frames = new();
		frames.RemoveAnimation("default");
		string basePath = $"res://assets/images/enemies/Hellhound_{Variant}/";

		AddStripAnimation(frames, basePath + "Idle.png", "idle", 10.0f, true);
		AddStripAnimation(frames, basePath + "Walk.png", "walk", 10.0f, true);
		AddStripAnimation(frames, basePath + "Run.png", "run", 12.0f, true);
		AddStripAnimation(frames, basePath + "Attack_1.png", "attack", 12.0f, false);
		AddStripAnimation(frames, basePath + "Hurt.png", "hurt", 10.0f, false);
		AddStripAnimation(frames, basePath + "Dead.png", "dead", 10.0f, false);
		return frames;
	}

	private static void AddStripAnimation(SpriteFrames frames, string texturePath, string animName, float fps, bool loop)
	{
		Texture2D tex = GD.Load<Texture2D>(texturePath);
		if (tex == null)
		{
			GD.PushWarning($"Hellhound animation texture missing: {texturePath}");
			return;
		}
		int h = tex.GetHeight();
		int w = tex.GetWidth();
		int frameCount = h > 0 ? Mathf.Max(1, w / h) : 1;
		frames.AddAnimation(animName);
		frames.SetAnimationSpeed(animName, fps);
		frames.SetAnimationLoop(animName, loop);
		for (int i = 0; i < frameCount; i++)
		{
			frames.AddFrame(animName, new AtlasTexture
			{
				Atlas = tex,
				Region = new Rect2(i * h, 0, h, h)
			});
		}
	}
}
