using Autofac;

using Myriad.Gateway;
using Myriad.Types;

namespace PluralKit.Bot;

public class InteractionCreated: IEventHandler<InteractionCreateEvent>
{
    private readonly InteractionDispatchService _interactionDispatch;
    private readonly ILifetimeScope _services;

    public InteractionCreated(InteractionDispatchService interactionDispatch, ILifetimeScope services)
    {
        _interactionDispatch = interactionDispatch;
        _services = services;
    }

    public async Task Handle(int shardId, InteractionCreateEvent evt)
    {
        if (evt.Type == Interaction.InteractionType.MessageComponent || evt.Type == Interaction.InteractionType.ModalSubmit)
        {
            var customId = evt.Data?.CustomId;
            if (customId != null)
            {
                var ctx = new InteractionContext(evt, _services);
                await _interactionDispatch.Dispatch(customId, ctx);
            }
        }
        else if (evt.Type == Interaction.InteractionType.ApplicationCommand)
        {
            var ctx = new InteractionContext(evt, _services);
            switch (evt.Data.Name) {
                case "Edit Message":
                    await _services.Resolve<MessageInteraction>().HandleMessageEdit(ctx);
                    break;
            }
        }
    }
}