using System.Text.RegularExpressions;

using Microsoft.AspNetCore.Mvc;

using PluralKit.Core;

namespace PluralKit.API;

public class PKControllerBase: ControllerBase
{
    private readonly Guid _requestId = Guid.NewGuid();
    private readonly Regex _shortIdRegex = new("^[a-z]{5}$");
    private readonly Regex _snowflakeRegex = new("^[0-9]{17,19}$");

    protected readonly ApiConfig _config;
    protected readonly IDatabase _db;
    protected readonly ModelRepository _repo;
    protected readonly DispatchService _dispatch;
    protected readonly RedisService _redis;

    public PKControllerBase(IServiceProvider svc)
    {
        _config = svc.GetRequiredService<ApiConfig>();
        _db = svc.GetRequiredService<IDatabase>();
        _repo = svc.GetRequiredService<ModelRepository>();
        _dispatch = svc.GetRequiredService<DispatchService>();
        _redis = svc.GetRequiredService<RedisService>();
    }

    protected SystemId? authenticatedSystem
    {
        get
        {
            HttpContext.Items.TryGetValue("SystemId", out var systemId);
            return (SystemId?)systemId;
        }
    }

    protected ulong? authenticatedUser
    {
        get
        {
            HttpContext.Items.TryGetValue("UserId", out var userId);
            return (ulong?)userId;
        }
    }

    protected Task<PKSystem?> ResolveSystem(string systemRef)
    {
        if (systemRef == "@me")
        {
            if (authenticatedSystem == null)
                throw Errors.GenericAuthError;
            return _repo.GetSystem(authenticatedSystem.Value);
        }

        if (Guid.TryParse(systemRef, out var guid))
            return _repo.GetSystemByGuid(guid);

        if (_snowflakeRegex.IsMatch(systemRef))
            return _repo.GetSystemByAccount(ulong.Parse(systemRef));

        if (_shortIdRegex.IsMatch(systemRef))
            return _repo.GetSystemByHid(systemRef);

        return Task.FromResult<PKSystem?>(null);
    }

    protected Task<PKMember?> ResolveMember(string memberRef)
    {
        if (Guid.TryParse(memberRef, out var guid))
            return _repo.GetMemberByGuid(guid);

        if (_shortIdRegex.IsMatch(memberRef))
            return _repo.GetMemberByHid(memberRef);

        return Task.FromResult<PKMember?>(null);
    }

    protected Task<PKGroup?> ResolveGroup(string groupRef)
    {
        if (Guid.TryParse(groupRef, out var guid))
            return _repo.GetGroupByGuid(guid);

        if (_shortIdRegex.IsMatch(groupRef))
            return _repo.GetGroupByHid(groupRef);

        return Task.FromResult<PKGroup?>(null);
    }

    protected LookupContext ContextFor(PKSystem system)
    {
        if (authenticatedSystem == null) return LookupContext.ByNonOwner;
        return authenticatedSystem == system.Id ? LookupContext.ByOwner : LookupContext.ByNonOwner;
    }

    protected LookupContext ContextFor(PKMember member)
    {
        if (authenticatedSystem == null) return LookupContext.ByNonOwner;
        return authenticatedSystem == member.System ? LookupContext.ByOwner : LookupContext.ByNonOwner;
    }

    protected LookupContext ContextFor(PKGroup group)
    {
        if (authenticatedSystem == null) return LookupContext.ByNonOwner;
        return authenticatedSystem == group.System ? LookupContext.ByOwner : LookupContext.ByNonOwner;
    }
}