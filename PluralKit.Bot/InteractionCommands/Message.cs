using Myriad.Types;
using Myriad.Rest;
using Myriad.Rest.Exceptions;

using PluralKit.Core;

namespace PluralKit.Bot;

public class MessageInteraction
{
    private readonly InteractionDispatchService _interactionDispatch;
    private readonly DiscordApiClient _rest;
    private readonly IDatabase _db;
    private readonly ModelRepository _repo;
    private readonly WebhookExecutorService _hook;
    private readonly LogChannelService _logChannel;

    public MessageInteraction(InteractionDispatchService interactionDispatch, DiscordApiClient rest, 
        IDatabase db, ModelRepository repo, WebhookExecutorService hook, LogChannelService logChannel)
    {
        _interactionDispatch = interactionDispatch;
        _rest = rest;
        _db = db;
        _repo = repo;
        _hook = hook;
        _logChannel = logChannel;
    }

    public async Task HandleMessageEdit(InteractionContext ctx)
    {
        var message = ctx.Data.Resolved.Messages?.FirstOrDefault();
        if (message == null) return;

        var pkMessage = await _db.Execute(conn => _repo.GetMessage(conn, message.Value.Key));
        if (pkMessage == null)
        {
            await ctx.Reply($"{Emojis.Error} This is not a message proxied by PluralKit.");
            return;
        }

        var system = await _repo.GetSystemByAccount(ctx.User.Id);
        if (pkMessage.System.Id != system?.Id)
        {
            await ctx.Reply($"{Emojis.Error} Can't edit messages sent by a different system.");
            return;
        }

        var id = _interactionDispatch.Register((ctx) => HandleMessageEditCallback(ctx, pkMessage.Message.Mid));

        await ctx.Respond(InteractionResponse.ResponseType.Modal, new()
            {
                CustomId = id,
                Title = "Edit Message",
                Components = new MessageComponent[]
                {
                    new()
                    {
                        Type = ComponentType.ActionRow,
                        Components = new MessageComponent[]
                        {
                            new()
                            {
                                Type = ComponentType.TextInput,
                                CustomId = "content",
                                Label = "Message Text",
                                MinLength = 1,
                                MaxLength = 2000,
                                Style = ButtonStyle.Secondary,
                                Required = true,
                                Placeholder = "The new message text",
                                Value = message.Value.Value.Content
                            }
                        }
                    }
                }
            }
        );
    }

    private async Task HandleMessageEditCallback(InteractionContext ctx, ulong messageId)
    {
        var msg = await _db.Execute(conn => _repo.GetMessage(conn, messageId));

        var originalMsg = await _rest.GetMessageOrNull(msg.Message.Channel, msg.Message.Mid);
        if (originalMsg == null)
            throw new PKError("Could not edit message.");

        var newContent = ctx.Data.Components[0].Components[0].Value;

        try
        {
            var editedMsg = await _hook.EditWebhookMessage(msg.Message.Channel, msg.Message.Mid, newContent);

            await ctx.Reply($"{Emojis.Success} Edited message!");

            await _logChannel.LogEditedMessage(msg, editedMsg, ctx.User, originalMsg!.Content!);
        }
        catch (NotFoundException)
        {
            throw new PKError("Could not edit message.");
        }
    }
}