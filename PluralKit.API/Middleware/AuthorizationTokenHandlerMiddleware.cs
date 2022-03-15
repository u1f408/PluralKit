using System.Text;
using System.Security.Cryptography;

using Dapper;

using PluralKit.Core;

namespace PluralKit.API;

public class AuthorizationTokenHandlerMiddleware
{
    private readonly RequestDelegate _next;

    public AuthorizationTokenHandlerMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task Invoke(HttpContext ctx, IDatabase db, ApiConfig config)
    {
        await Inner(ctx, db, config);
        await _next.Invoke(ctx);
    }

    private async Task Inner(HttpContext ctx, IDatabase db, ApiConfig config)
    {
        // if token is length 64 it is a system token
        // if token is length >100, it might a jwt (check base64)
        // if it is a jwt, it will contain the token, so check *that* against the database

        ctx.Request.Headers.TryGetValue("authorization", out var authHeaders);
        if (authHeaders.Count == 0) return;

        if (authHeaders[0].Length == 64)
        {
            var systemId = await db.Execute(conn => conn.QuerySingleOrDefaultAsync<SystemId?>(
                "select id from systems where token = @token",
                new { token = authHeaders[0] }
            ));

            if (systemId != null)
                ctx.Items.Add("SystemId", systemId);
        }
        else if (authHeaders[0].Length > 100)
        {
            var header = Encoding.UTF8.GetString(Convert.FromBase64String(authHeaders[0])).Split(":");
            if (GetSignature(config.JwtSigningToken, $"{header[0]}:{header[1]}") != header[2]) return;

            var systemId = await db.Execute(conn => conn.QuerySingleOrDefaultAsync<SystemId?>(
                "select id from systems where token = @token",
                new { token = header[1] }
            ));

            if (systemId != null)
            {
                ctx.Items.Add("SystemId", systemId);
                ctx.Items.Add("UserId", header[0]);
            }
        }
    }

    private string gethex(byte[] src) => string.Concat(Array.ConvertAll(src, b => b.ToString("x")));

    private string GetSignature(string key, string data)
    {
        using var shaAlgorithm = new HMACSHA256(Convert.FromBase64String(key));
        var signatureBytes = System.Text.Encoding.UTF8.GetBytes(data);
        var signatureHashBytes = shaAlgorithm.ComputeHash(signatureBytes);
        return gethex(signatureHashBytes);
    }
}