using System;
using System.Threading.Channels;

internal sealed record LiveSubscription(Guid Id, ChannelReader<string> Reader);
