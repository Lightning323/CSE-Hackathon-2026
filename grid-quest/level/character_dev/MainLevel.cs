using Godot;
using System;

public partial class MainLevel : Node2D
{
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		GD.Print("_Ready");
		YSortEnabled = true;
		SetupBackgroundMusic();
		CallDeferred(nameof(PlacePlayerAtRandomSpawn));
	}

	private void SetupBackgroundMusic()
	{
		var bgm = GetNodeOrNull<AudioStreamPlayer>("BgmPlayer") ?? new AudioStreamPlayer { Name = "BgmPlayer" };
		if (bgm.GetParent() == null)
			AddChild(bgm);

		var stream = GD.Load<AudioStream>("res://assets/audio/sfx/forest_ancient_path.ogg");
		if (stream is AudioStreamOggVorbis ogg)
			ogg.Loop = true;
		bgm.Stream = stream;
		bgm.Autoplay = false;
		bgm.VolumeDb = -8.0f;
		bgm.ProcessMode = ProcessModeEnum.Always;
		bgm.Play();
	}

	private void PlacePlayerAtRandomSpawn()
	{
		var player = GetNodeOrNull<Node2D>("Player");
		var map = GetNodeOrNull<Map>("Map");
		if (player == null || map == null)
			return;

		player.GlobalPosition = map.GetRandomPlayerSpawnPosition();
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}
}
