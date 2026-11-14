using Godot;

public partial class EnemySpawnOnKill : Node
{
	private static readonly PackedScene SkeletonScene = GD.Load<PackedScene>("res://enemy/skeleton_enemy.tscn");
	private static readonly PackedScene EliteScene = GD.Load<PackedScene>("res://enemy/elite_skeleton_enemy.tscn");

	[Export] public float SpawnCheckInterval { get; set; } = 3.0f;
	[Export] public float SpawnChance { get; set; } = 0.6f;
	[Export] public int MaxEnemies { get; set; } = 16;
	[Export] public float EliteChance { get; set; } = 0.12f;

	private float _timer;

	public override void _Process(double delta)
	{
		_timer -= (float)delta;
		if (_timer > 0.0f) return;
		_timer = SpawnCheckInterval;

		int enemyCount = GetTree().GetNodesInGroup("enemies").Count;
		if (enemyCount == 0)
		{
			SpawnEnemyOutsideCamera(force: true);
			return;
		}

		if (enemyCount >= MaxEnemies)
			return;

		if (GD.Randf() <= SpawnChance)
		{
			SpawnEnemyOutsideCamera(force: false);
		}
	}

	private void SpawnEnemyOutsideCamera(bool force)
	{
		Vector2? pos = FindOffCameraSpawnPosition(force ? 40 : 24);
		if (!pos.HasValue)
			return;

		PackedScene scene = GD.Randf() < EliteChance ? EliteScene : SkeletonScene;
		Node inst = scene.Instantiate();
		if (inst is Node2D enemy)
		{
			enemy.GlobalPosition = pos.Value;
			GetTree().CurrentScene.AddChild(enemy);
		}
		else
		{
			inst.QueueFree();
		}
	}

	private Vector2? FindOffCameraSpawnPosition(int attempts)
	{
		Camera2D camera = GetViewport().GetCamera2D();
		Rect2 visible = camera != null ? GetCameraWorldRect(camera).Grow(48.0f) : new Rect2(-99999, -99999, 0, 0);
		Rect2 mapBounds = new(32, 32, Map.Width * GridQuest.Map.MapConstants.CELL_SIZE - 64, Map.Height * GridQuest.Map.MapConstants.CELL_SIZE - 64);

		for (int i = 0; i < attempts; i++)
		{
			float x = (float)GD.RandRange(mapBounds.Position.X, mapBounds.End.X);
			float y = (float)GD.RandRange(mapBounds.Position.Y, mapBounds.End.Y);
			Vector2 candidate = new(x, y);
			if (camera != null && visible.HasPoint(candidate))
				continue;
			return candidate;
		}

		// Fallback: spawn far from player if camera visibility check couldn't find a spot.
		Node2D player = GetTree().GetFirstNodeInGroup("player") as Node2D;
		if (player != null)
		{
			float angle = (float)GD.RandRange(0.0, Mathf.Tau);
			float dist = (float)GD.RandRange(260.0, 420.0);
			Vector2 candidate = player.GlobalPosition + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * dist;
			if (mapBounds.HasPoint(candidate))
				return candidate;
		}

		return null;
	}

	private Rect2 GetCameraWorldRect(Camera2D camera)
	{
		Vector2 viewportSize = GetViewport().GetVisibleRect().Size;
		Vector2 zoom = camera.Zoom;
		if (zoom.X == 0 || zoom.Y == 0)
			zoom = Vector2.One;
		Vector2 worldSize = viewportSize / zoom;
		Vector2 topLeft = camera.GetScreenCenterPosition() - worldSize / 2.0f;
		return new Rect2(topLeft, worldSize);
	}
}
