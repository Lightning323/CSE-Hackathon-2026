using Godot;

// --- ENEMY TEST SPAWNER (temporary) ---
// Spawns enemies by difficulty using number keys (each press spawns another):
// 1 = Medium (Skeleton), 2 = Hard (Elite Skeleton).
// Remove this script/node when no longer needed for testing.
public partial class EnemyTestSpawner : Node2D
{
	private static readonly PackedScene SkeletonScene = GD.Load<PackedScene>("res://enemy/skeleton_enemy.tscn");
	private static readonly PackedScene EliteScene = GD.Load<PackedScene>("res://enemy/elite_skeleton_enemy.tscn");

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event is not InputEventKey keyEvent || !keyEvent.Pressed || keyEvent.Echo)
		{
			return;
		}

		PackedScene scene = keyEvent.Keycode switch
		{
			Key.Key1 => SkeletonScene,
			Key.Key2 => EliteScene,
			_ => null
		};

		if (scene == null)
		{
			return;
		}

		SpawnEnemy(scene);
		GetViewport().SetInputAsHandled();
	}

	private void SpawnEnemy(PackedScene scene)
	{
		Node instance = scene.Instantiate();
		if (instance is Node2D enemy)
		{
			Vector2 offset = new(GD.Randf() * 30.0f - 15.0f, GD.Randf() * 30.0f - 15.0f);
			enemy.GlobalPosition = GlobalPosition + offset;
			GetParent().AddChild(enemy);
		}
		else
		{
			instance.QueueFree();
		}
	}
}
