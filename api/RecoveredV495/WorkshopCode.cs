using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Text.RegularExpressions.Generated;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Primitives;

internal static class WorkshopCode
{
	public static string Normalize(string raw)
	{
		string text = raw.Trim();
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
				if (dictionary.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
				{
					text = value.ToString();
					break;
				}
			}
		}
		try
		{
			text = Uri.UnescapeDataString(text);
		}
		catch (UriFormatException)
		{
		}
		text = text.Trim().Trim('\'', '"').Replace("\r", "")
			.Replace("\n", "")
			.Replace("\t", "")
			.ToUpperInvariant()
			.Replace('’', '\'')
			.Replace('‘', '\'')
			.Replace('\u00b4', '\'')
			.Replace('`', '\'')
			.Replace('Ç', '|')
			.Replace('¦', '|');
		Match match = MalformedPrefixedQr().Match(text);
		if (match.Success)
		{
			return $"REAL-MNT|{match.Groups[1].Value}-{match.Groups[2].Value}-{match.Groups[3].Value}-{match.Groups[4].Value}";
		}
		Match match2 = MalformedDocument().Match(text);
		if (match2.Success)
		{
			return $"{match2.Groups[1].Value}-{match2.Groups[2].Value}-{match2.Groups[3].Value}-{match2.Groups[4].Value}";
		}
		return text;
	}

	[GeneratedCode("System.Text.RegularExpressions.Generator", "8.0.14.32403")]
	private static Regex MalformedPrefixedQr()
	{
		return _003CRegexGenerator_g_003EFC9510E207557D455D080BCE812B632D0941F29A4F266CEA61F0E1247391CDA28__MalformedPrefixedQr_0.Instance;
	}

	[GeneratedCode("System.Text.RegularExpressions.Generator", "8.0.14.32403")]
	private static Regex MalformedDocument()
	{
		return _003CRegexGenerator_g_003EFC9510E207557D455D080BCE812B632D0941F29A4F266CEA61F0E1247391CDA28__MalformedDocument_1.Instance;
	}
}
