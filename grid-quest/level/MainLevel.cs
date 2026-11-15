#nullable enable
using Godot;
using GridQuest.Entities;

public partial class MainLevel : Node2D
{
	public override void _Ready()
	{
		YSortEnabled = true;
		WorldProgress.ResetCurrentRunForFreshStart();
		SetupBackgroundMusic();
		CallDeferred(nameof(InitializeControlledEntity));
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

	private void InitializeControlledEntity()
	{
		var map = GetNodeOrNull<Map>("Map");
		Entity? entity = GetNodeOrNull<Entity>("Entity");
		if (entity == null)
		{
			foreach (Node node in GetTree().GetNodesInGroup("entities"))
			{
				if (node is Entity e) { entity = e; break; }
			}
		}
		if (entity == null)
			return;

		if (map != null)
			entity.GlobalPosition = map.GetRandomPlayerSpawnPosition();

		var controller = GetNodeOrNull<PlayerController>("PlayerController");
		if (controller != null)
		{
			// By default, begin with the level entity controlled by the player so existing gameplay/UI works.
			controller.Control(entity);
		}
	}
}
