using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

internal static class StorageFiles
{
	internal const string Prefix = "REGISTRO-STORAGE-V1:";

	internal static StorageService? Service { get; private set; }

	internal static IStorageUploadQueue? Queue { get; set; }

	public static bool Enabled { get; private set; }

	public static void Configure(IConfiguration config, StorageService? service = null)
	{
		Queue = null;
		Enabled = !string.Equals(config["STORAGE_ENABLED"], "false", StringComparison.OrdinalIgnoreCase);
		string text = config["STORAGE_KEY"];
		Service = service ?? (string.IsNullOrWhiteSpace(text) ? null : new StorageService(text));
	}

	public static StorageService Required()
	{
		return Service ?? throw new StorageServiceException("STORAGE_CONFIG", "El servidor no tiene configurada la clave de almacenamiento (STORAGE_KEY). No se puede consultar ni transferir archivos a la API hasta restaurarla.");
	}

	public static string Hash(byte[] bytes)
	{
		return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
	}

	public static string? DetectMime(byte[] b)
	{
		if (b.AsSpan().StartsWith("%PDF-"u8))
		{
			return "application/pdf";
		}
		if (b.AsSpan().StartsWith(new byte[8] { 137, 80, 78, 71, 13, 10, 26, 10 }))
		{
			return "image/png";
		}
		if (b.Length >= 3 && b[0] == byte.MaxValue && b[1] == 216 && b[2] == byte.MaxValue)
		{
			return "image/jpeg";
		}
		if (b.AsSpan().StartsWith("GIF87a"u8) || b.AsSpan().StartsWith("GIF89a"u8))
		{
			return "image/gif";
		}
		if (b.Length >= 12 && b.AsSpan(0, 4).SequenceEqual("RIFF"u8) && b.AsSpan(8, 4).SequenceEqual("WEBP"u8))
		{
			return "image/webp";
		}
		if (b.Length >= 16 && b.AsSpan(4, 4).SequenceEqual("ftyp"u8) && (b.AsSpan(8, 4).SequenceEqual("avif"u8) || b.AsSpan(8, 4).SequenceEqual("avis"u8)))
		{
			return "image/avif";
		}
		return null;
	}

	public static bool IsReference(byte[] bytes)
	{
		return bytes.AsSpan().StartsWith(Encoding.UTF8.GetBytes("REGISTRO-STORAGE-V1:"));
	}

	public static byte[] Encode(StorageReference reference)
	{
		return Encoding.UTF8.GetBytes("REGISTRO-STORAGE-V1:" + JsonSerializer.Serialize(reference));
	}

	public static StorageReference Decode(byte[] bytes)
	{
		return JsonSerializer.Deserialize<StorageReference>(Encoding.UTF8.GetString(bytes).Substring("REGISTRO-STORAGE-V1:".Length)) ?? throw new StorageServiceException("STORAGE_REFERENCE", "Referencia de archivo inválida.");
	}

	public static async Task<byte[]> Store(byte[] bytes, string kind, CancellationToken ct)
	{
		if (IsReference(bytes))
		{
			return bytes;
		}
		string text = DetectMime(bytes);
		if (text == null)
		{
			if ((bytes.Length >= 12 && bytes.AsSpan(4, 4).SequenceEqual("ftyp"u8) && Enumerable.Contains(new string[8] { "heic", "heix", "hevc", "hevx", "heim", "heis", "mif1", "msf1" }, Encoding.ASCII.GetString(bytes, 8, 4))) || bytes.AsSpan().StartsWith("BM"u8) || bytes.AsSpan().StartsWith("II*\0"u8) || bytes.AsSpan().StartsWith(new byte[4] { 77, 77, 0, 42 }))
			{
				throw new StorageServiceException("STORAGE_FORMAT", "Convierte esta imagen a JPG, PNG o WebP para guardarla en el almacenamiento.", 422);
			}
			return bytes;
		}
		if (!Enabled)
		{
			throw new StorageServiceException("STORAGE_DISABLED", "El almacenamiento está desactivado. Actívalo para guardar imágenes y PDF; no se guardaron sus bytes en SQL.");
		}
		StorageReference reference = ((Queue == null || !(kind != "integration-verification")) ? (await Required().Upload(bytes, text, kind, ct, null, null, kind == "integration-verification")) : (await Queue.Enqueue(bytes, text, kind, ct)));
		return Encode(reference);
	}

	public static bool IsQueued(StorageReference reference)
	{
		return reference.Id.StartsWith("queue:", StringComparison.Ordinal);
	}

	public static async Task<byte[]> Read(byte[] bytes, CancellationToken ct)
	{
		if (!IsReference(bytes))
		{
			return bytes;
		}
		StorageReference reference = Decode(bytes);
		return (!IsQueued(reference)) ? (await Required().Download(reference, ct)) : (await (Queue ?? throw new StorageServiceException("STORAGE_QUEUE_CONFIG", "La cola privada de archivos no está disponible.")).Read(reference, ct));
	}

	public static async Task<string?> StoreDataUrl(string? value, CancellationToken ct)
	{
		if (value == null || !value.StartsWith("data:", StringComparison.Ordinal))
		{
			return value;
		}
		int num = value.IndexOf(',');
		if (num < 0 || !value.Substring(0, num).EndsWith(";base64", StringComparison.Ordinal))
		{
			if (value.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase) || value.StartsWith("data:application/pdf", StringComparison.OrdinalIgnoreCase))
			{
				throw new StorageServiceException("STORAGE_FORMAT", "La imagen o el PDF incrustado debe usar base64 válido.", 422);
			}
			return value;
		}
		byte[] array;
		try
		{
			array = Convert.FromBase64String(value.Substring(num + 1));
		}
		catch (FormatException)
		{
			throw new StorageServiceException("STORAGE_FORMAT", "Imagen incrustada inválida.", 400);
		}
		if (DetectMime(array) == null)
		{
			if (value.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase) || value.StartsWith("data:application/pdf", StringComparison.OrdinalIgnoreCase))
			{
				throw new StorageServiceException("STORAGE_FORMAT", "El archivo incrustado debe ser una imagen compatible o un PDF válido.", 422);
			}
			return value;
		}
		Encoding uTF = Encoding.UTF8;
		return uTF.GetString(await Store(array, "embedded-image", ct));
	}

	public static async Task<string?> ReadDataUrl(string? value, CancellationToken ct)
	{
		if (value == null || !value.StartsWith("REGISTRO-STORAGE-V1:", StringComparison.Ordinal))
		{
			return value;
		}
		StorageReference storageReference = Decode(Encoding.UTF8.GetBytes(value));
		string mime = storageReference.Mime;
		return "data:" + mime + ";base64," + Convert.ToBase64String(await Read(Encoding.UTF8.GetBytes(value), ct));
	}

	public static async Task<string?> ResolveQueuedReferences(string value, CancellationToken ct)
	{
		bool pending = false;
		if (value.StartsWith("REGISTRO-STORAGE-V1:", StringComparison.Ordinal))
		{
			string text = await Resolve(value);
			return pending ? null : text;
		}
		JsonNode node = JsonNode.Parse(value);
		await Visit(node);
		return pending ? null : node?.ToJsonString();
		async Task<string> Resolve(string text2)
		{
			if (!text2.StartsWith("REGISTRO-STORAGE-V1:", StringComparison.Ordinal))
			{
				return text2;
			}
			StorageReference reference = Decode(Encoding.UTF8.GetBytes(text2));
			if (!IsQueued(reference))
			{
				return text2;
			}
			StorageReference storageReference = ((Queue != null) ? (await Queue.Resolve(reference, ct)) : null);
			StorageReference storageReference2 = storageReference;
			if ((object)storageReference2 == null)
			{
				pending = true;
				return text2;
			}
			return Encoding.UTF8.GetString(Encode(storageReference2));
		}
		async Task Visit(JsonNode? current)
		{
			if (current is JsonObject obj)
			{
				KeyValuePair<string, JsonNode>[] array = obj.ToArray();
				for (int i = 0; i < array.Length; i++)
				{
					KeyValuePair<string, JsonNode> keyValuePair = array[i];
					if (!(keyValuePair.Value is JsonValue jsonValue) || !jsonValue.TryGetValue<string>(out string value2))
					{
						await Visit(keyValuePair.Value);
					}
					else
					{
						JsonObject jsonObject = obj;
						string key = keyValuePair.Key;
						jsonObject[key] = await Resolve(value2);
					}
				}
			}
			if (current is JsonArray arr)
			{
				for (int i = 0; i < arr.Count; i++)
				{
					if (!(arr[i] is JsonValue jsonValue2) || !jsonValue2.TryGetValue<string>(out string value3))
					{
						await Visit(arr[i]);
					}
					else
					{
						JsonArray jsonArray = arr;
						int index = i;
						jsonArray[index] = await Resolve(value3);
					}
				}
			}
		}
	}

	public static async Task<string?> TransformJson(string? json, bool upload, CancellationToken ct)
	{
		if (json == null || (upload ? (!json.Contains("data:", StringComparison.Ordinal)) : (!json.Contains("REGISTRO-STORAGE-V1:", StringComparison.Ordinal))))
		{
			return json;
		}
		JsonNode node = JsonNode.Parse(json);
		await Visit(node);
		return node?.ToJsonString() ?? json;
		async Task Visit(JsonNode? current)
		{
			if (current is JsonObject obj)
			{
				KeyValuePair<string, JsonNode>[] array = obj.ToArray();
				for (int i = 0; i < array.Length; i++)
				{
					KeyValuePair<string, JsonNode> keyValuePair = array[i];
					if (!(keyValuePair.Value is JsonValue jsonValue) || !jsonValue.TryGetValue<string>(out string value))
					{
						await Visit(keyValuePair.Value);
					}
					else
					{
						JsonObject jsonObject = obj;
						string key = keyValuePair.Key;
						string text = ((!upload) ? (await ReadDataUrl(value, ct)) : (await StoreDataUrl(value, ct)));
						jsonObject[key] = text;
					}
				}
			}
			if (current is JsonArray arr)
			{
				for (int i = 0; i < arr.Count; i++)
				{
					if (!(arr[i] is JsonValue jsonValue2) || !jsonValue2.TryGetValue<string>(out string value2))
					{
						await Visit(arr[i]);
					}
					else
					{
						JsonArray jsonArray = arr;
						int index = i;
						string text2 = ((!upload) ? (await ReadDataUrl(value2, ct)) : (await StoreDataUrl(value2, ct)));
						jsonArray[index] = text2;
					}
				}
			}
		}
	}
}
