using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Primitives;

internal static class AdministrativeDeliveryQr
{
	public static string? RequestNumber(string? raw)
	{
		if (string.IsNullOrWhiteSpace(raw) || raw.Length > 2048)
		{
			return null;
		}
		string text = raw.Trim();
		bool flag = Uri.TryCreate(text, UriKind.Absolute, out Uri result);
		bool flag2 = flag;
		if (flag2)
		{
			string scheme = result.Scheme;
			bool flag3 = ((scheme == "http" || scheme == "https") ? true : false);
			flag2 = flag3;
		}
		if (flag2)
		{
			if (result.AbsolutePath.StartsWith("/api/institutional-documents/lookup/", StringComparison.OrdinalIgnoreCase))
			{
				text = Uri.UnescapeDataString(result.AbsolutePath.Substring("/api/institutional-documents/lookup/".Length));
			}
			else
			{
				Dictionary<string, StringValues> dictionary = QueryHelpers.ParseQuery(result.Query);
				if (!dictionary.TryGetValue("equipment", out var value) || value.Count != 1)
				{
					return null;
				}
				text = value.ToString();
			}
		}
		if (text.StartsWith("REAL-MNT|", StringComparison.Ordinal))
		{
			text = text.Substring(9);
		}
		if (text.Length > 40 || !Regex.IsMatch(text, "^SOL-ADM-[A-Za-z0-9-]+$"))
		{
			return null;
		}
		return text;
	}
}
