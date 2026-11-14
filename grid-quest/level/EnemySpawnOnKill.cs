using Godot;

public partial class EnemySpawnOnKill : Node
{
	private static readonly PackedScene SkeletonScene = GD.Load<PackedScene>("res://enemy/skeleton_enemy.tscn");
	private static readonly PackedScene EliteScene = GD.Load<PackedScene>("res://enemy/elite_skeleton_enemy.tscn");
	private float _timer;

	public override void _Process(double delta)
	{
		_timer -= (float)delta;
		if (_timer > 0.0f) return;
		_timer = 2.0f;

		if (GetTree().GetNodesInGroup("enemies").Count > 0) return;

		// spawn one
		PackedScene scene = GD.Randf() < 0.7f ? SkeletonScene : EliteScene;
		Node inst = scene.Instantiate();
		if (inst is Node2D e)
		{
			Node2D player = GetTree().GetFirstNodeInGroup("player") as Node2D;
			Vector2 pos = player != null ? player.GlobalPosition + new Vector2((float)GD.RandRange(120, 200) * (GD.Randf() < 0.5f ? -1 : 1), (float)GD.RandRange(120, 200) * (GD.Randf() < 0.5f ? -1 : 1)) : Vector2.Zero;
			e.GlobalPosition = pos;
			GetTree().CurrentScene.AddChild(e);
		}
		else inst.QueueFree();
	}
}
