using System;
using System.Threading;

internal record StorageOrigin(string Source, string Group)
{
	private sealed class Restore(Action restore) : IDisposable
	{
		public void Dispose()
		{
			restore();
		}
	}

	public static StorageOrigin Current => current.Value ?? new StorageOrigin("unknown", "unknown");

	private static readonly AsyncLocal<StorageOrigin?> current = new AsyncLocal<StorageOrigin>();

	public static IDisposable Scope(string source, string group)
	{
		StorageOrigin previous = current.Value;
		current.Value = new StorageOrigin(source, group);
		return new Restore(() =>
		{
			current.Value = previous;
		});
	}
}
