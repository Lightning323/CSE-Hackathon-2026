using Godot;
using System;

public partial class MainLevel : Node2D
{
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		GD.Print("_Ready");
		YSortEnabled = true;
		WorldProgress.ResetCurrentRunForFreshStart();
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
		if (!bgm.Playing)
			bgm.Play();
	}

	public void StopBackgroundMusicForBoss()
	{
		var bgm = GetNodeOrNull<AudioStreamPlayer>("BgmPlayer");
		if (bgm != null && bgm.Playing)
			bgm.Stop();
		// Also stop any other ambient/background music players in scene if present.
		foreach (Node n in GetTree().GetNodesInGroup("ambient_music"))
		{
			if (n is AudioStreamPlayer asp) asp.Stop();
			else if (n is AudioStreamPlayer2D asp2d) asp2d.Stop();
		}
	}

	public void ResumeBackgroundMusicAfterBoss()
	{
		var bgm = GetNodeOrNull<AudioStreamPlayer>("BgmPlayer");
		if (bgm != null && !bgm.Playing)
		{
			bgm.VolumeDb = -8.0f;
			bgm.Play();
		}
		foreach (Node n in GetTree().GetNodesInGroup("ambient_music"))
		{
			if (n is AudioStreamPlayer asp && asp.Stream != null && !asp.Playing) asp.Play();
			else if (n is AudioStreamPlayer2D asp2d && asp2d.Stream != null && !asp2d.Playing) asp2d.Play();
		}
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
