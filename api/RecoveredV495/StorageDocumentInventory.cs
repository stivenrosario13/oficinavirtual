using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

internal static class StorageDocumentInventory
{
	public static async Task<List<StorageDocumentRow>> Read(SqlConnection connection, StorageUploadQueue queue, CancellationToken ct)
	{
		StorageQueueItem[] source = queue.Inventory();
		Dictionary<string, StorageQueueItem> local = source.ToDictionary((StorageQueueItem x) => "queue:" + x.Id);
		Dictionary<string, StorageQueueItem> remote = (from x in source
			where (object)x.Remote != null
			group x by x.Remote.Id).ToDictionary((IGrouping<string, StorageQueueItem> g) => g.Key, (IGrouping<string, StorageQueueItem> g) => g.First());
		List<(string category, string profile, string key, string mime, long size, string state)> files = new List<(string, string, string, string, long, string)>();
		List<StorageDocumentRow> result;
		await using (SqlCommand cmd = new SqlCommand("IF OBJECT_ID(N'dbo.institutional_files',N'U') IS NULL RETURN;\nSELECT id,\n  CASE WHEN JSON_QUERY(metadata,'$.warehouseRequest') IS NOT NULL THEN 'warehouse-request'\n       WHEN JSON_QUERY(metadata,'$.administrativeRequest') IS NOT NULL THEN 'administrative-form'\n       WHEN JSON_VALUE(metadata,'$.templateId') IS NOT NULL THEN 'template-form'\n       ELSE 'document-form' END category,\n  CASE WHEN SUBSTRING(file_data,1,@length)=@prefix AND DATALENGTH(file_data)<4096 THEN file_data ELSE NULL END reference,\n  JSON_VALUE(metadata,'$.contentType') mime,\n  COALESCE(TRY_CONVERT(bigint,JSON_VALUE(metadata,'$.size')),DATALENGTH(file_data)) original_size\nFROM dbo.institutional_files\nWHERE JSON_QUERY(metadata,'$.warehouseRequest') IS NOT NULL\n   OR JSON_QUERY(metadata,'$.administrativeRequest') IS NOT NULL\n   OR JSON_VALUE(metadata,'$.templateId') IS NOT NULL\n   OR (area='communications' AND channel='requests');", connection)
		{
			CommandTimeout = 20
		})
		{
			byte[] bytes = Encoding.UTF8.GetBytes("REGISTRO-STORAGE-V1:");
			cmd.Parameters.AddWithValue("@length", bytes.Length);
			cmd.Parameters.AddWithValue("@prefix", bytes);
			List<StorageDocumentRow> list;
			await using (SqlDataReader reader = await cmd.ExecuteReaderAsync(ct))
			{
				while (await reader.ReadAsync(ct))
				{
					string item = reader.GetString(1);
					string text = (reader.IsDBNull(3) ? "" : reader.GetString(3));
					if (text != "application/pdf" && !text.StartsWith("image/", StringComparison.Ordinal))
					{
						continue;
					}
					string item2 = "unknown";
					string item3 = "sql:" + reader.GetInt64(0);
					string item4 = "sql";
					long item5 = reader.GetInt64(4);
					if (!reader.IsDBNull(2))
					{
						StorageReference storageReference = StorageFiles.Decode((byte[])reader[2]);
						item3 = storageReference.Id;
						text = storageReference.Mime;
						item5 = storageReference.Size;
						StorageQueueItem storageQueueItem = (StorageFiles.IsQueued(storageReference) ? local.GetValueOrDefault(storageReference.Id) : remote.GetValueOrDefault(storageReference.Id));
						if ((object)storageQueueItem != null)
						{
							item2 = storageQueueItem.Origin?.Group ?? "unknown";
							if ((object)storageQueueItem.Remote != null)
							{
								item4 = "verified";
							}
							else
							{
								item4 = ((storageQueueItem.State == "review") ? "review" : "pending");
							}
						}
						else
						{
							item4 = (StorageFiles.IsQueued(storageReference) ? "pending" : "verified");
						}
					}
					files.Add((item, item2, item3, text, item5, item4));
				}
				list = (from f in files.DistinctBy(((string category, string profile, string key, string mime, long size, string state) f) => (category: f.category, profile: f.profile, key: f.key))
					group f by (category: f.category, profile: f.profile) into g
					select new StorageDocumentRow(g.Key.category, g.Key.profile, g.Count(((string category, string profile, string key, string mime, long size, string state) f) => f.mime.StartsWith("image/")), g.Count(((string category, string profile, string key, string mime, long size, string state) f) => f.mime == "application/pdf"), g.Sum(((string category, string profile, string key, string mime, long size, string state) f) => f.size), g.Count(((string category, string profile, string key, string mime, long size, string state) f) => f.state == "pending"), g.Count(((string category, string profile, string key, string mime, long size, string state) f) => f.state == "verified"), g.Count(((string category, string profile, string key, string mime, long size, string state) f) => f.state == "review"), g.Count(((string category, string profile, string key, string mime, long size, string state) f) => f.state == "sql")) into r
					orderby r.Category, r.Profile
					select r).ToList();
			}
			result = list;
		}
		return result;
	}
}
