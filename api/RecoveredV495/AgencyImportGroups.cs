using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

internal sealed class AgencyImportGroups
{
	private static readonly Regex Spaces = new Regex("\\s+", RegexOptions.CultureInvariant);

	private static readonly Regex Prefix = new Regex("^(\\d+)\\s*[-|–—]\\s*(\\S.*)$", RegexOptions.CultureInvariant);

	private readonly Dictionary<string, List<(string Name, string? Code, int Agencies)>> candidates = new Dictionary<string, List<(string, string, int)>>(StringComparer.OrdinalIgnoreCase);

	public AgencyImportGroups(IEnumerable<AgencyImportGroup> existing, IEnumerable<string> incoming)
	{
		foreach (AgencyImportGroup item in existing)
		{
			Add(item.Name, item.Agencies);
		}
		foreach (string item2 in incoming)
		{
			Add(item2, 0);
		}
	}

	private static (string Name, string Key, string? Code) Identity(string value)
	{
		string text = Spaces.Replace(value.Trim(), " ");
		Match match = Prefix.Match(text);
		if (!match.Success)
		{
			return (Name: text, Key: text, Code: null);
		}
		string text2 = match.Groups[1].Value.TrimStart('0');
		return (Name: text, Key: match.Groups[2].Value.Trim(), Code: (text2.Length == 0) ? "0" : text2);
	}

	private void Add(string value, int agencies)
	{
		(string, string, string) tuple = Identity(value);
		if (!candidates.TryGetValue(tuple.Item2, out List<(string, string, int)> value2))
		{
			value2 = (candidates[tuple.Item2] = new List<(string, string, int)>());
		}
		List<(string, string, int)> list2 = value2;
		string item;
		if (agencies <= 0)
		{
			(item, _, _) = tuple;
		}
		else
		{
			item = value;
		}
		list2.Add((item, tuple.Item3, agencies));
	}

	public (string Name, string? Error) Resolve(string value)
	{
		(string, string, string) tuple = Identity(value);
		List<(string, string, int)> source = candidates[tuple.Item2];
		if (source.Where(((string Name, string Code, int Agencies) x) => x.Code != null).Select(((string Name, string Code, int Agencies) x) => x.Code).Distinct()
			.Count() > 1)
		{
			return (Name: tuple.Item1, Error: "El nombre del grupo coincide con códigos distintos; revisa el grupo antes de importar");
		}
		string item = source.OrderByDescending(((string Name, string Code, int Agencies) x) => x.Agencies).ThenBy(((string Name, string Code, int Agencies) x) => x.Name, StringComparer.Ordinal).First()
			.Item1;
		return (Name: item, Error: null);
	}
}
