using System.Security.Cryptography;

using Microsoft.AspNetCore.Mvc;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using StackExchange.Redis;

using PluralKit.Core;

namespace PluralKit.API;

// Internal API definitions
// I would prefer if you do not use any of these APIs in your own integrations.
// It is unstable and subject to change at any time (which is why it's not versioned)

// If for some reason you do need access to something defined here,
// let us know in #api-support on the support server (https://discord.com/invite/PczBt78) and I'll see if it can be made public

[ApiController]
[Route("private")]
public class PrivateController: PKControllerBase
{
    public PrivateController(IServiceProvider svc) : base(svc) { }

    [HttpGet("meta")]
    public async Task<ActionResult<JObject>> Meta()
    {
        var db = _redis.Connection.GetDatabase();
        var redisInfo = await db.HashGetAllAsync("pluralkit:shardstatus");
        var shards = redisInfo.Select(x => Proto.Unmarshal<ShardState>(x.Value)).OrderBy(x => x.ShardId);

        var redisClusterInfo = await db.HashGetAllAsync("pluralkit:cluster_stats");
        var clusterInfo = redisClusterInfo.Select(x => JsonConvert.DeserializeObject<ClusterMetricInfo>(x.Value));

        var guildCount = clusterInfo.Sum(x => x.GuildCount);
        var channelCount = clusterInfo.Sum(x => x.ChannelCount);

        var stats = await _repo.GetStats();

        var o = new JObject();
        o.Add("shards", shards.ToJson());
        o.Add("stats", stats.ToJson(guildCount, channelCount));
        o.Add("version", BuildInfoService.FullVersion);

        return Ok(o);
    }

    [HttpGet("oauth2/callback")]
    public async Task<IActionResult> OAuth2Callback([FromQuery] string code)
    {
        using var client = new HttpClient();

        var res = await client.PostAsync("https://discord.com/api/v10/oauth2/token", new FormUrlEncodedContent(
            new Dictionary<string, string>{
            { "client_id", _config.ClientId },
            { "client_secret", _config.ClientSecret },
            { "grant_type", "authorization_code" },
            { "redirect_uri", "http://localhost:5000/private/oauth2/callback" },
            { "code", code },
        }));

        if (!res.IsSuccessStatusCode) {
            return BadRequest(1);
        }

        var c = JsonConvert.DeserializeObject<OAuth2TokenResponse>(await res.Content.ReadAsStringAsync());

        if (c.access_token == null)
            return BadRequest(2);

        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {c.access_token}");

        var resp = await client.GetAsync("https://discord.com/api/v10/users/@me");
        Console.WriteLine(await resp.Content.ReadAsStringAsync());
        var user = JsonConvert.DeserializeObject<JObject>(await resp.Content.ReadAsStringAsync());
        var userId = user.Value<String>("id");

        var system = await ResolveSystem(userId);
        if (system == null)
            return NotFound();

        resp = await client.GetAsync("https://discord.com/api/v10/users/@me/guilds");
        var guilds = JsonConvert.DeserializeObject<JArray>(await resp.Content.ReadAsStringAsync());
        await _redis.Connection.GetDatabase().HashSetAsync(
            $"user_guilds::{userId}",
            guilds.Select(g => new HashEntry(g.Value<string>("id"), true)).ToArray()
        );

        var o = new JObject();

        // todo: generate system token if it's missing

        o.Add("system", system.ToJson(LookupContext.ByOwner, APIVersion.V2));
        o.Add("user", user);
        o.Add("token", Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(GetSignedToken(userId, system.Token))));

        return Ok(o);
    }

    private string gethex(byte[] src) => string.Concat(Array.ConvertAll(src, b => b.ToString("x")));

    private string GetSignedToken(string userId, string token)
    {
        var data = $"{userId}:{token}";

        using (var shaAlgorithm = new HMACSHA256(Convert.FromBase64String(_config.JwtSigningToken)))
        {
            var signatureBytes = System.Text.Encoding.UTF8.GetBytes(data);
            var signatureHashBytes = shaAlgorithm.ComputeHash(signatureBytes);
            var signatureHashHex = gethex(signatureHashBytes);

            return $"{data}:{signatureHashHex}";
        }
    }
}

public record OAuth2TokenResponse
{
    public string access_token;
}

public static class PrivateJsonExt
{
    public static JArray ToJson(this IEnumerable<ShardState> shards)
    {
        var o = new JArray();

        foreach (var shard in shards)
        {
            var s = new JObject();
            s.Add("id", shard.ShardId);

            if (!shard.Up)
                s.Add("status", "down");
            else
                s.Add("status", "up");

            s.Add("ping", shard.Latency);
            s.Add("disconnection_count", shard.DisconnectionCount);
            s.Add("last_heartbeat", shard.LastHeartbeat.ToString());
            s.Add("last_connection", shard.LastConnection.ToString());
            if (shard.HasClusterId)
                s.Add("cluster_id", shard.ClusterId);

            o.Add(s);
        }

        return o;
    }
}