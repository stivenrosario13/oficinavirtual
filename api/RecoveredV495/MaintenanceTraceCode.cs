using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Primitives;

internal static class MaintenanceTraceCode
{
	public static string Normalize(string value)
	{
		string text = value.Trim();
		for (int i = 0; i < 2; i++)
		{
			try
			{
				string text2 = Uri.UnescapeDataString(text);
				if (text2 == text)
				{
					break;
				}
				text = text2;
				continue;
			}
			catch (UriFormatException)
			{
			}
			break;
		}
		if (Uri.TryCreate(text, UriKind.Absolute, out Uri result))
		{
			Dictionary<string, StringValues> dictionary = QueryHelpers.ParseQuery(result.Query);
			string[] array = new string[4] { "equipment", "code", "qr", "token" };
			foreach (string key in array)
			{
				if (dictionary.TryGetValue(key, out var value2) && !string.IsNullOrWhiteSpace(value2))
				{
					text = value2.ToString();
					break;
				}
			}
		}
		text = text.Trim().Trim('\'', '"').Replace("\r", "")
			.Replace("\n", "")
			.Replace("\t", "");
		try
		{
			text = Uri.UnescapeDataString(text);
		}
		catch (UriFormatException)
		{
		}
		text = text.ToUpperInvariant().Replace('’', '\'').Replace('‘', '\'')
			.Replace('\u00b4', '\'')
			.Replace('`', '\'')
			.Replace('Ç', '|')
			.Replace('¦', '|');
		Match match = Regex.Match(text, "REAL\\W*MNT\\W*SOL\\W*ALM\\W*(\\d{8})\\W*(\\d{6})\\W*([A-Z0-9]{4,})$");
		if (match.Success)
		{
			return $"REAL-MNT|SOL-ALM-{match.Groups[1].Value}-{match.Groups[2].Value}-{match.Groups[3].Value}";
		}
		match = Regex.Match(text, "(?:^|\\W)SOL\\W*ALM\\W*(\\d{8})\\W*(\\d{6})\\W*([A-Z0-9]{4,})$");
		if (match.Success)
		{
			return $"SOL-ALM-{match.Groups[1].Value}-{match.Groups[2].Value}-{match.Groups[3].Value}";
		}
		Match match2 = Regex.Match(text, "REAL\\W*MNT\\W*(ALM|TAL)\\W*(\\d{8})\\W*(\\d{6})\\W*([A-Z0-9]{5,})$");
		if (match2.Success)
		{
			return $"REAL-MNT|{match2.Groups[1].Value}-{match2.Groups[2].Value}-{match2.Groups[3].Value}-{match2.Groups[4].Value}";
		}
		match2 = Regex.Match(text, "(?:^|\\W)(ALM|TAL)\\W*(\\d{8})\\W*(\\d{6})\\W*([A-Z0-9]{5,})$");
		if (!match2.Success)
		{
			return text;
		}
		return $"{match2.Groups[1].Value}-{match2.Groups[2].Value}-{match2.Groups[3].Value}-{match2.Groups[4].Value}";
	}
}
