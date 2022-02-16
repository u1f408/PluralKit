using System.Diagnostics;

using App.Metrics;

using Myriad.Builders;
using Myriad.Cache;
using Myriad.Gateway;
using Myriad.Rest;
using Myriad.Rest.Types.Requests;
using Myriad.Rest.Exceptions;
using Myriad.Types;

using NodaTime;

using Serilog;

namespace PluralKit.Bot;

public class ErrorMessageService
{
    // globally rate limit errors for now, don't want to spam users when something breaks
    private static readonly Duration MinErrorInterval = Duration.FromSeconds(10);
    private static readonly Duration IntervalFromStartup = Duration.FromMinutes(2);
    private readonly ILogger _logger;
    private readonly IDiscordCache _cache;

    private readonly BotConfig _botConfig;
    private readonly IMetrics _metrics;
    private readonly DiscordApiClient _rest;

    public ErrorMessageService(BotConfig botConfig, IDiscordCache cache, IMetrics metrics, ILogger logger, DiscordApiClient rest)
    {
        _botConfig = botConfig;
        _cache = cache;
        _metrics = metrics;
        _logger = logger;
        _rest = rest;

        lastErrorTime = SystemClock.Instance.GetCurrentInstant();
    }

    // private readonly ConcurrentDictionary<ulong, Instant> _lastErrorInChannel = new ConcurrentDictionary<ulong, Instant>();
    private Instant lastErrorTime { get; set; }

    public async Task SendErrorMessage(InteractionCreateEvent evt, string errorId)
    {
        var now = SystemClock.Instance.GetCurrentInstant();
        var embed = GenerateEmbed(now, errorId);
        
        // this is dumb
        // apparently there's no way to send a message to an interaction without knowing if it's been responded to already
        // so we try to followup, then if that fails, we send an interaction response
        // hopefully one of those work?
        try
        {
            await _rest.ExecuteWebhook(await _cache.GetOwnUser(), evt.Token, new()
            {
                Content = $"> **Error code:** `{errorId}`",
                Embeds = new[] { embed },
                Flags = Message.MessageFlags.Ephemeral,
            });
        }
        catch (UnknownDiscordRequestException)
        {
            await _rest.CreateInteractionResponse(evt.Id, evt.Token, new()
            {
                Type = InteractionResponse.ResponseType.ChannelMessageWithSource,
                Data = new()
                {
                    Content = $"> **Error code:** `{errorId}`",
                    Embeds = new[] { embed },
                    Flags = Message.MessageFlags.Ephemeral,
                }
            });
        }
    }

    public async Task SendErrorMessage(ulong channelId, string errorId)
    {
        var now = SystemClock.Instance.GetCurrentInstant();
        if (!ShouldSendErrorMessage(channelId, now))
        {
            _logger.Warning("Rate limited sending error message to {ChannelId} with error code {ErrorId}",
                channelId, errorId);
            _metrics.Measure.Meter.Mark(BotMetrics.ErrorMessagesSent, "throttled");
            return;
        }

        var embed = GenerateEmbed(now, errorId);

        try
        {
            await _rest.CreateMessage(channelId,
                new MessageRequest { Content = $"> **Error code:** `{errorId}`", Embed = embed });

            _logger.Information("Sent error message to {ChannelId} with error code {ErrorId}", channelId, errorId);
            _metrics.Measure.Meter.Mark(BotMetrics.ErrorMessagesSent, "sent");
        }
        catch (Exception e)
        {
            _logger.Error(e, "Error sending error message to {ChannelId}", channelId);
            _metrics.Measure.Meter.Mark(BotMetrics.ErrorMessagesSent, "failed");
            throw;
        }
    }

    private Embed GenerateEmbed(Instant now, string errorId)
    {
        var channelInfo = _botConfig.IsBetaBot
            ? "**#hi-please-break-the-beta-bot** on **[the support server *(click to join)*](https://discord.gg/THvbH59btW)**"
            : "**#bug-reports-and-errors** on **[the support server *(click to join)*](https://discord.gg/PczBt78)**";

        var embed = new EmbedBuilder()
            .Color(0xE74C3C)
            .Title("Internal error occurred")
            .Description($"For support, please send the error code above in {channelInfo} with a description of what you were doing at the time.")
            .Footer(new Embed.EmbedFooter(errorId))
            .Timestamp(now.ToDateTimeOffset().ToString("O"));

        return embed.Build();
    }

    private bool ShouldSendErrorMessage(ulong channelId, Instant now)
    {
        // if (_lastErrorInChannel.TryGetValue(channelId, out var lastErrorTime))

        var startupTime = Instant.FromDateTimeUtc(Process.GetCurrentProcess().StartTime.ToUniversalTime());
        // don't send errors during startup
        // mostly because Npgsql throws a bunch of errors when opening connections sometimes???
        if (now - startupTime < IntervalFromStartup && !_botConfig.IsBetaBot)
            return false;

        var interval = now - lastErrorTime;
        if (interval < MinErrorInterval)
            return false;

        // _lastErrorInChannel[channelId] = now;
        lastErrorTime = now;
        return true;
    }
}