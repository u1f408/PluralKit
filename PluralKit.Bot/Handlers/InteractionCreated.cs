using Autofac;

using App.Metrics;

using Myriad.Cache;
using Myriad.Extensions;
using Myriad.Gateway;
using Myriad.Types;

using PluralKit.Core;

namespace PluralKit.Bot;

public class InteractionCreated: IEventHandler<InteractionCreateEvent>
{
    private readonly InteractionDispatchService _interactionDispatch;
    private readonly ILifetimeScope _services;
    private readonly IDiscordCache _cache;
    private readonly IMetrics _metrics;
    private readonly ModelRepository _repo;
    private readonly Autocompletion _autocomplete;

    public InteractionCreated(InteractionDispatchService interactionDispatch, ILifetimeScope services, IDiscordCache cache,
        IMetrics metrics, ModelRepository repo, Autocompletion autocomplete)
    {
        _interactionDispatch = interactionDispatch;
        _services = services;
        _cache = cache;
        _metrics = metrics;
        _repo = repo;
        _autocomplete = autocomplete;
    }

    public async Task Handle(int shardId, InteractionCreateEvent evt)
    {
        var rootChannel = await _cache.GetRootChannel(evt.ChannelId);

        // Get message context from DB (tracking w/ metrics)
        MessageContext mctx = null;

        if (evt.Type == Interaction.InteractionType.ApplicationCommand || evt.Type == Interaction.InteractionType.Autocomplete)
        {
            _metrics.Measure.Meter.Mark(BotMetrics.AppCommandsRun);

            using (_metrics.Measure.Timer.Time(BotMetrics.MessageContextQueryTime))
                mctx = await _repo.GetMessageContext((evt.User ?? evt.Member.User).Id, evt.GuildId ?? default, rootChannel.Id);
        }

        var ctx = new InteractionContext(evt, mctx, _services);

        try
        {
            await HandleInner(shardId, ctx);
        }
        catch (PKError e)
        {
            await ctx.Reply($"{Emojis.Error} {e.Message}");
        }
    }

    public async Task HandleInner(int shardId, InteractionContext ctx)
    {
        switch (ctx.Event.Type)
        {
            case Interaction.InteractionType.MessageComponent:
            case Interaction.InteractionType.ModalSubmit:
                {
                    var customId = ctx.Data?.CustomId;
                    if (customId != null)
                        await _interactionDispatch.Dispatch(customId, ctx);
                    break;
                }
            case Interaction.InteractionType.ApplicationCommand:
                await InteractionTree.HandleCommand(ctx);
                break;
            case Interaction.InteractionType.Autocomplete:
                await WrapAutocomplete(ctx);
                break;
        }
    }

    public async Task WrapAutocomplete(InteractionContext ctx)
    {
        var results = await _autocomplete.HandleAutocomplete(ctx);
        await ctx.Respond(
            InteractionResponse.ResponseType.AutocompleteResult,
            new()
            {
                Choices = results,
            }
        );
    }
}