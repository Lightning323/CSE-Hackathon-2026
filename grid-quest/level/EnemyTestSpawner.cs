using Godot;

// --- ENEMY TEST SPAWNER (temporary) ---
// Spawns a single test enemy by difficulty using number keys:
// 1 = Medium (Skeleton), 2 = Hard (Elite Skeleton).
// Remove this script/node when no longer needed for testing.
public partial class EnemyTestSpawner : Node2D
{
	private static readonly PackedScene SkeletonScene = GD.Load<PackedScene>("res://enemy/skeleton_enemy.tscn");
	private static readonly PackedScene EliteScene = GD.Load<PackedScene>("res://enemy/elite_skeleton_enemy.tscn");

	private Node2D _spawnedEnemy;

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
		if (IsInstanceValid(_spawnedEnemy))
		{
			_spawnedEnemy.QueueFree();
		}

		Node instance = scene.Instantiate();
		if (instance is Node2D enemy)
		{
			enemy.GlobalPosition = GlobalPosition;
			GetParent().AddChild(enemy);
			_spawnedEnemy = enemy;
		}
		else
		{
			instance.QueueFree();
		}
	}
}
