using Autofac;

using Myriad.Gateway;
using Myriad.Rest;
using Myriad.Types;

using PluralKit.Core;

namespace PluralKit.Bot;

public class InteractionContext
{
    private readonly ILifetimeScope _services;

    public InteractionContext(InteractionCreateEvent evt, MessageContext mctx, ILifetimeScope services)
    {
        Event = evt;
        MessageContext = mctx;
        _services = services;
    }

    public InteractionCreateEvent Event { get; }

    public MessageContext MessageContext { get; }

    public ulong ChannelId => Event.ChannelId;
    public ulong? MessageId => Event.Message?.Id;
    public GuildMember? Member => Event.Member;
    public User User => Event.Member?.User ?? Event.User;
    public string Token => Event.Token;
    public ApplicationCommandInteractionData? Data => Event.Data;
    public string? CustomId => Event.Data?.CustomId;

    public async Task Execute<T>(Func<T, Task> handler)
    {
        await handler(_services.Resolve<T>());
    }

    public async Task Reply(string? content = null, Embed embed = null, bool ephemeral = true)
    {
        await Respond(InteractionResponse.ResponseType.ChannelMessageWithSource,
            new InteractionApplicationCommandCallbackData
            {
                Content = content,
                Flags = ephemeral ? Message.MessageFlags.Ephemeral : 0,
                Embeds = embed != null ? new[] { embed } : null
            });
    }

    public async Task Respond(InteractionResponse.ResponseType type,
                              InteractionApplicationCommandCallbackData? data)
    {
        var rest = _services.Resolve<DiscordApiClient>();
        await rest.CreateInteractionResponse(Event.Id, Event.Token,
            new InteractionResponse { Type = type, Data = data });
    }
}