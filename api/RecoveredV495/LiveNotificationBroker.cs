using System;
using System.Collections.Concurrent;
using System.Threading.Channels;

internal sealed class LiveNotificationBroker
{
	private readonly ConcurrentDictionary<Guid, Channel<string>> subscribers = new ConcurrentDictionary<Guid, Channel<string>>();

	public LiveSubscription Subscribe()
	{
		Guid guid = Guid.NewGuid();
		Channel<string> channel = Channel.CreateBounded<string>(new BoundedChannelOptions(20)
		{
			FullMode = BoundedChannelFullMode.DropOldest,
			SingleReader = true,
			SingleWriter = false
		});
		subscribers[guid] = channel;
		return new LiveSubscription(guid, channel.Reader);
	}

	public void Publish(string message)
	{
		string item = $"{message}:{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
		foreach (Channel<string> value in subscribers.Values)
		{
			value.Writer.TryWrite(item);
		}
	}

	public void Unsubscribe(Guid id)
	{
		if (subscribers.TryRemove(id, out Channel<string> value))
		{
			value.Writer.TryComplete();
		}
	}
}
