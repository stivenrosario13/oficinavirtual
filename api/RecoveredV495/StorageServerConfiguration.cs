using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

internal static class StorageServerConfiguration
{
	internal static string RestoreAndPersist(IConfiguration config, string contentRoot, string? webRoot)
	{
		string fullPath = Path.GetFullPath(Path.Combine(contentRoot, "App_Data"));
		string text = Path.GetFullPath(webRoot ?? Path.Combine(contentRoot, "wwwroot")).TrimEnd(Path.DirectorySeparatorChar);
		if (fullPath.Equals(text, StringComparison.OrdinalIgnoreCase) || fullPath.StartsWith(text + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
		{
			throw new InvalidOperationException("La configuración privada de almacenamiento debe estar fuera de wwwroot.");
		}
		string text2 = Path.Combine(fullPath, "storage-credentials.json");
		string value = config["STORAGE_KEY"]?.Trim();
		string result = "server";
		if (string.IsNullOrWhiteSpace(value))
		{
			if (!File.Exists(text2))
			{
				return "missing";
			}
			try
			{
				value = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(text2))?.GetValueOrDefault("STORAGE_KEY");
			}
			catch (Exception ex) when ((ex is IOException || ex is UnauthorizedAccessException || ex is JsonException) ? true : false)
			{
				Console.Error.WriteLine("No se pudo leer el respaldo privado de almacenamiento.");
				return "missing";
			}
			if (string.IsNullOrWhiteSpace(value))
			{
				return "missing";
			}
			config["STORAGE_KEY"] = value;
			return "private-backup";
		}
		string text3 = null;
		try
		{
			Directory.CreateDirectory(fullPath);
			string text4 = JsonSerializer.Serialize(new Dictionary<string, string> { { "STORAGE_KEY", value } });
			if (!File.Exists(text2) || File.ReadAllText(text2) != text4)
			{
				text3 = Path.Combine(fullPath, "storage-credentials-" + Guid.NewGuid().ToString("N") + ".tmp");
				File.WriteAllText(text3, text4);
				File.Move(text3, text2, overwrite: true);
			}
		}
		catch (Exception ex2) when ((ex2 is IOException || ex2 is UnauthorizedAccessException) ? true : false)
		{
			Console.Error.WriteLine("Almacenamiento configurado; no se pudo conservar su respaldo privado.");
			result = "server-without-backup";
		}
		finally
		{
			if (text3 != null && File.Exists(text3))
			{
				try
				{
					File.Delete(text3);
				}
				catch (Exception ex3) when ((ex3 is IOException || ex3 is UnauthorizedAccessException) ? true : false)
				{
				}
			}
		}
		return result;
	}
}
