using Dapper;

using System.Text.Json;

using Myriad.Types;

using PluralKit.Core;

namespace PluralKit.Bot;

public class Autocompletion
{
    private readonly IDatabase _db;
    public Autocompletion(IDatabase db)
    {
        _db = db;
    }

    public async Task<ApplicationCommandOption.Choice[]> HandleAutocomplete(InteractionContext ctx)
    {
        var input = ((JsonElement)ctx.Data.Options[0].Options.First(x => x.Focused ?? false).Value).GetString();

        var results = new ApplicationCommandOption.Choice[] { };

        if (ctx.MessageContext.SystemId == null)
            return results;

        switch (ctx.Data.Name)
        {
            case "member":
                results = await FindMemberAutocomplete(input, ctx.MessageContext.SystemId.Value);
                break;
            case "group":
                results = await FindGroupAutocomplete(input, ctx.MessageContext.SystemId.Value);
                break;
        }

        return results.Length > 10 ? results.Take(10).ToArray() : results;
    }

    private async Task<ApplicationCommandOption.Choice[]> FindMemberAutocomplete(string input, SystemId system)
    {
        var members = await _db.Execute(c => c.QueryAsync<PKMember>(
            "select hid, name from members where hid ilike '%'||@input||'%' or name ilike '%'||@input||'%' or display_name ilike '%'||@input||'%' and system = @system",
            new { input = input, system = system }
        ));

        return members.Select(m => new ApplicationCommandOption.Choice($"({m.Hid}) {m.Name}", m.Hid)).ToArray();
    }


    private async Task<ApplicationCommandOption.Choice[]> FindGroupAutocomplete(string input, SystemId system)
    {
        var groups = await _db.Execute(c => c.QueryAsync<PKGroup>(
            "select hid, name from groups where hid ilike '%'||@input||'%' or name ilike '%'||@input||'%' or display_name ilike '%'||@input||'%' and system = @system",
            new { input = input, system = system }
        ));

        return groups.Select(m => new ApplicationCommandOption.Choice($"({m.Hid}) {m.Name}", m.Hid)).ToArray();
    }
}