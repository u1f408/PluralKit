using Myriad.Gateway;

using PluralKit.Core;

namespace PluralKit.Bot;

public class GuildMemberRemoved: IEventHandler<GuildMemberRemoveEvent>
{
    public readonly RedisService _redis;
    public GuildMemberRemoved(RedisService redis)
    {
        _redis = redis;
    }

    public async Task Handle(int _, GuildMemberRemoveEvent evt)
    {
        await _redis.Connection.GetDatabase().HashDeleteAsync($"user_guilds::{evt.User.Id.ToString()}", evt.GuildId.ToString());
    }
}