using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace GridQuest.Menus.MainMenu;

public sealed class SaveInfo
{
	public string Id { get; set; } = "";
	public string Title { get; set; } = "";
	public string Description { get; set; } = "";
	public string CreatedUtc { get; set; } = "";
	public string FolderPath { get; set; } = "";
}

public static class SaveManager
{
	public const string SavesDirName = "saves";

	public static string GetSavesRootPath()
	{
		return Path.Combine(OS.GetUserDataDir(), SavesDirName);
	}

	public static void EnsureSavesRoot()
	{
		Directory.CreateDirectory(GetSavesRootPath());
	}

	public static List<SaveInfo> ListSaves()
	{
		EnsureSavesRoot();
		var result = new List<SaveInfo>();
		foreach (var dir in Directory.GetDirectories(GetSavesRootPath()))
		{
			var info = LoadSaveInfo(dir);
			if (info != null)
				result.Add(info);
		}
		return result
			.OrderByDescending(s => s.CreatedUtc ?? "", StringComparer.Ordinal)
			.ThenBy(s => s.Title ?? "", StringComparer.OrdinalIgnoreCase)
			.ToList();
	}

	public static SaveInfo LoadSaveInfo(string folderPath)
	{
		try
		{
			var metaPath = Path.Combine(folderPath, "save.json");
			if (!File.Exists(metaPath))
				return null;

			var json = File.ReadAllText(metaPath);
			var info = JsonSerializer.Deserialize<SaveInfo>(json);
			if (info == null)
				return null;
			info.FolderPath = folderPath;
			if (string.IsNullOrWhiteSpace(info.Id))
				info.Id = Path.GetFileName(folderPath);
			return info;
		}
		catch (Exception e)
		{
			GD.PushWarning($"Failed to load save info from '{folderPath}': {e.Message}");
			return null;
		}
	}

	public static SaveInfo CreateSave(string title, string description)
	{
		EnsureSavesRoot();
		title = (title ?? "").Trim();
		description = (description ?? "").Trim();
		if (string.IsNullOrWhiteSpace(title))
			title = "New Save";

		var id = $"{DateTime.UtcNow:yyyyMMdd_HHmmss}_{Guid.NewGuid().ToString("N")[..8]}";
		var folderPath = Path.Combine(GetSavesRootPath(), id);
		Directory.CreateDirectory(folderPath);

		var info = new SaveInfo
		{
			Id = id,
			Title = title,
			Description = description,
			CreatedUtc = DateTime.UtcNow.ToString("o"),
			FolderPath = folderPath
		};

		WriteSaveInfo(info);
		return info;
	}

	public static void WriteSaveInfo(SaveInfo info)
	{
		if (info == null || string.IsNullOrWhiteSpace(info.FolderPath))
			return;
		Directory.CreateDirectory(info.FolderPath);
		var metaPath = Path.Combine(info.FolderPath, "save.json");
		var json = JsonSerializer.Serialize(info, new JsonSerializerOptions { WriteIndented = true });
		File.WriteAllText(metaPath, json);
	}

	public static bool DeleteSave(SaveInfo info)
	{
		if (info == null || string.IsNullOrWhiteSpace(info.FolderPath))
			return false;
		try
		{
			if (Directory.Exists(info.FolderPath))
				Directory.Delete(info.FolderPath, true);
			return true;
		}
		catch (Exception e)
		{
			GD.PushWarning($"Failed to delete save '{info.FolderPath}': {e.Message}");
			return false;
		}
	}

	public static void OpenSavesFolder()
	{
		EnsureSavesRoot();
		OS.ShellOpen(GetSavesRootPath());
	}
}
