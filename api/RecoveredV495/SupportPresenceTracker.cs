using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

internal sealed class SupportPresenceTracker
{
	private sealed record Presence(string Name, string Role, string Department, Guid? ConversationId, bool IsTyping, DateTimeOffset UpdatedAt);

	private readonly ConcurrentDictionary<string, Presence> users = new ConcurrentDictionary<string, Presence>();

	public void Touch(string userId, string name, string role, string department, Guid? conversationId, bool isTyping)
	{
		users[userId] = new Presence(name, role, department, conversationId, isTyping, DateTimeOffset.UtcNow);
	}

	public object Snapshot(string department, Guid? conversationId, string excludeUserId)
	{
		DateTimeOffset now = DateTimeOffset.UtcNow;
		foreach (string item in (from item in users
			where (now - item.Value.UpdatedAt).TotalMinutes > 3.0
			select item.Key).ToList())
		{
			users.TryRemove(item, out Presence _);
		}
		List<Presence> list = (from item in users
			where item.Key != excludeUserId && (now - item.Value.UpdatedAt).TotalSeconds < 40.0
			select item.Value).ToList();
		bool supportOnline = list.Any((Presence item) =>
		{
			switch (item.Role)
			{
			case "Administrator":
			case "Technology":
			case "GeneralServices":
			case "HumanResources":
				return true;
			default:
				return false;
			}
		});
		List<string> typing = (from item in list
			where item.IsTyping && item.ConversationId == conversationId && (now - item.UpdatedAt).TotalSeconds < 5.0
			select item.Name).Distinct().ToList();
		return new
		{
			supportOnline = supportOnline,
			typing = typing,
			onlineCount = list.Count
		};
	}
}
