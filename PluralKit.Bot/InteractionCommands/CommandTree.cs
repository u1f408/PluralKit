namespace PluralKit.Bot;

public static class InteractionTree
{
    public static Task HandleCommand(InteractionContext ctx)
    {
        switch (ctx.Data?.Name)
        {
            case "Edit Message":
                return ctx.Execute<MessageInteraction>(h => h.HandleMessageEdit(ctx));
        }

        return ctx.Reply("unknown command, probably not implemented?");
    }
}