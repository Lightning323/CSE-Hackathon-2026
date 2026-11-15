#nullable enable
using System;
using System.Collections.Generic;

namespace GridQuest.Entities;

public static class EntityNameGenerator
{
	private static readonly string[] Adjectives =
	{
		"Brisk", "Calm", "Clever", "Daring", "Eager", "Fuzzy", "Gentle", "Jolly",
		"Kind", "Lucky", "Merry", "Nimble", "Quick", "Sunny", "Witty", "Zesty"
	};

	private static readonly string[] Nouns =
	{
		"Ash", "Birch", "Cedar", "Dawn", "Echo", "Finch", "Maple", "Moss",
		"Oak", "Pine", "River", "Sage", "Sky", "Stone", "Willow", "Wren"
	};

	private static readonly HashSet<string> UsedNames = new(StringComparer.OrdinalIgnoreCase);
	private static readonly Random Rng = new();

	public static string GenerateUnique()
	{
		for (int attempt = 0; attempt < 200; attempt++)
		{
			string name = $"{Adjectives[Rng.Next(Adjectives.Length)]}{Nouns[Rng.Next(Nouns.Length)]}";
			if (UsedNames.Add(name))
				return name;
		}

		for (int i = 2; i < 10000; i++)
		{
			string baseName = $"{Adjectives[Rng.Next(Adjectives.Length)]}{Nouns[Rng.Next(Nouns.Length)]}";
			string name = $"{baseName}{i}";
			if (UsedNames.Add(name))
				return name;
		}

		string fallback = $"Entity{Guid.NewGuid().ToString("N")[..6]}";
		UsedNames.Add(fallback);
		return fallback;
	}
}
