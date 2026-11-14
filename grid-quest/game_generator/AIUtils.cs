
namespace GridQuest.GameGenerator;

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

static class JsonUtils
{

	public static readonly JsonSerializerOptions JsonOptions = new()
	{
		WriteIndented = true,
		PropertyNameCaseInsensitive = true,
	};


	public static T TryParseJson<T>(string text) where T : class
	{
		if (string.IsNullOrWhiteSpace(text)) return null;
		text = text.Trim();

		var fence = Regex.Match(text, "```(?:json)?\\s*(\\{.*\\})\\s*```", RegexOptions.Singleline | RegexOptions.IgnoreCase);
		if (fence.Success) text = fence.Groups[1].Value.Trim();

		if (!text.StartsWith("{"))
		{
			int start = text.IndexOf('{');
			int end = text.LastIndexOf('}');
			if (start >= 0 && end > start) text = text.Substring(start, end - start + 1);
		}

		try
		{
			return JsonSerializer.Deserialize<T>(text, JsonOptions);
		}
		catch
		{
			return null;
		}
	}
}