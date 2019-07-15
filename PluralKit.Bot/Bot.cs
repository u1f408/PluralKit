using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NodaTime;
using Npgsql;
using Sentry;
using Sentry.Extensibility;

namespace PluralKit.Bot
{
    class Initialize
    {
        private IConfiguration _config;
        
        static void Main(string[] args) => new Initialize { _config = InitUtils.BuildConfiguration(args).Build()}.MainAsync().GetAwaiter().GetResult();

        private async Task MainAsync()
        {
            Console.WriteLine("Starting PluralKit...");
            
            InitUtils.Init();
            
            using (var services = BuildServiceProvider())
            {
                var coreConfig = services.GetRequiredService<CoreConfig>();
                var botConfig = services.GetRequiredService<BotConfig>();

                using (SentrySdk.Init(coreConfig.SentryUrl))
                {

                    Console.WriteLine("- Connecting to database...");
                    using (var conn = await services.GetRequiredService<DbConnectionFactory>().Obtain())
                        await Schema.CreateTables(conn);

                    Console.WriteLine("- Connecting to Discord...");
                    var client = services.GetRequiredService<IDiscordClient>() as DiscordShardedClient;
                    await client.LoginAsync(TokenType.Bot, botConfig.Token);
                    await client.StartAsync();

                    Console.WriteLine("- Initializing bot...");
                    await services.GetRequiredService<Bot>().Init();

                    await Task.Delay(-1);
                }
            }
        }

        public ServiceProvider BuildServiceProvider() => new ServiceCollection()
                .AddTransient(_ => _config.GetSection("PluralKit").Get<CoreConfig>() ?? new CoreConfig())
                .AddTransient(_ => _config.GetSection("PluralKit").GetSection("Bot").Get<BotConfig>() ?? new BotConfig())
                
                .AddTransient(svc => new DbConnectionFactory(svc.GetRequiredService<CoreConfig>().Database))
                
                .AddSingleton<IDiscordClient, DiscordShardedClient>()
                .AddSingleton<Bot>()

                .AddTransient<CommandService>(_ => new CommandService(new CommandServiceConfig
                {
                    CaseSensitiveCommands = false,
                    QuotationMarkAliasMap = new Dictionary<char, char>
                    {
                        {'"', '"'},
                        {'\'', '\''},
                        {'‘', '’'},
                        {'“', '”'},
                        {'„', '‟'},
                    },
                    DefaultRunMode = RunMode.Async
                }))
                .AddTransient<EmbedService>()
                .AddTransient<ProxyService>()
                .AddTransient<LogChannelService>()
                .AddTransient<DataFileService>()
                
                .AddSingleton<WebhookCacheService>()
                
                .AddTransient<SystemStore>()
                .AddTransient<MemberStore>()
                .AddTransient<MessageStore>()
                .AddTransient<SwitchStore>()
            
                .AddScoped<IHub>(_ => HubAdapter.Instance)
                .BuildServiceProvider();
    }
    class Bot
    {
        private IServiceProvider _services;
        private DiscordShardedClient _client;
        private CommandService _commands;
        private ProxyService _proxy;
        private Timer _updateTimer;
        private IHub _hub;

        public Bot(IServiceProvider services, IDiscordClient client, CommandService commands, ProxyService proxy, IHub hub)
        {
            this._services = services;
            this._client = client as DiscordShardedClient;
            this._commands = commands;
            this._proxy = proxy;
            _hub = hub;
        }

        public async Task Init()
        {
            _commands.AddTypeReader<PKSystem>(new PKSystemTypeReader());
            _commands.AddTypeReader<PKMember>(new PKMemberTypeReader());
            _commands.CommandExecuted += CommandExecuted;
            await _commands.AddModulesAsync(Assembly.GetEntryAssembly(), _services);

            _client.ShardReady += ShardReady;

            // Deliberately wrapping in an async function *without* awaiting, we don't want to "block" since this'd hold up the main loop
            // These handlers return Task so we gotta be careful not to return the Task itself (which would then be awaited) - kinda weird design but eh
            _client.MessageReceived += async (msg) => MessageReceived(msg).CatchException(HandleRuntimeError);
            _client.ReactionAdded += async (message, channel, reaction) => _proxy.HandleReactionAddedAsync(message, channel, reaction).CatchException(HandleRuntimeError);
            _client.MessageDeleted += async (message, channel) => _proxy.HandleMessageDeletedAsync(message, channel).CatchException(HandleRuntimeError);
        }

        private async Task UpdatePeriodic()
        {
            // Method called every 60 seconds
            await _client.SetGameAsync($"pk;help | in {_client.Guilds.Count} servers");
        }

        private async Task ShardReady(DiscordSocketClient shardClient)
        {
            //_updateTimer = new Timer((_) => UpdatePeriodic(), null, 0, 60*1000);

            Console.WriteLine($"Shard #{shardClient.ShardId} connected to {shardClient.Guilds.Sum(g => g.Channels.Count)} channels in {shardClient.Guilds.Count} guilds.");
            //Console.WriteLine($"PluralKit started as {_client.CurrentUser.Username}#{_client.CurrentUser.Discriminator} ({_client.CurrentUser.Id}).");
        }

        private async Task CommandExecuted(Optional<CommandInfo> cmd, ICommandContext ctx, IResult _result)
        {
            // TODO: refactor this entire block, it's fugly.
            if (!_result.IsSuccess) {
                if (_result.Error == CommandError.Unsuccessful || _result.Error == CommandError.Exception) {
                    // If this is a PKError (ie. thrown deliberately), show user facing message
                    // If not, log as error
                    var exception = (_result as ExecuteResult?)?.Exception;
                    if (exception is PKError) {
                        await ctx.Message.Channel.SendMessageAsync($"{Emojis.Error} {exception.Message}");
                    } else if (exception is TimeoutException) {
                        await ctx.Message.Channel.SendMessageAsync($"{Emojis.Error} Operation timed out. Try being faster next time :)");
                    } else if (_result is PreconditionResult)
                    {
                        await ctx.Message.Channel.SendMessageAsync($"{Emojis.Error} {_result.ErrorReason}");
                    } else {
                        HandleRuntimeError((_result as ExecuteResult?)?.Exception);
                    }
                } else if ((_result.Error == CommandError.BadArgCount || _result.Error == CommandError.MultipleMatches) && cmd.IsSpecified) {
                    await ctx.Message.Channel.SendMessageAsync($"{Emojis.Error} {_result.ErrorReason}\n**Usage: **pk;{cmd.Value.Remarks}");
                } else if (_result.Error == CommandError.UnknownCommand || _result.Error == CommandError.UnmetPrecondition || _result.Error == CommandError.ObjectNotFound) {
                    await ctx.Message.Channel.SendMessageAsync($"{Emojis.Error} {_result.ErrorReason}");
                }
            }
        }

        private async Task MessageReceived(SocketMessage _arg)
        {
            // _client.CurrentUser will be null if we've connected *some* shards but not shard #0 yet
            // This will cause an error in WebhookCacheServices so we just workaround and don't process any messages
            // until we properly connect. TODO: can we do this without chucking away a bunch of messages?
            if (_client.CurrentUser == null) return;

            using (SentrySdk.PushScope())
            using (var serviceScope = _services.CreateScope())
            {
                SentrySdk.AddBreadcrumb("event.message", data: new Dictionary<string, string>()
                {
                    {"content", _arg.Content},
                    {"user", _arg.Author.Id.ToString()},
                    {"channel", _arg.Channel.Id.ToString()},
                    {"guild", ((_arg.Channel as IGuildChannel)?.GuildId ?? 0).ToString()}
                });

                // Ignore system messages (member joined, message pinned, etc)
                var arg = _arg as SocketUserMessage;
                if (arg == null) return;

                // Ignore bot messages
                if (arg.Author.IsBot || arg.Author.IsWebhook) return;

                int argPos = 0;
                // Check if message starts with the command prefix
                if (arg.HasStringPrefix("pk;", ref argPos, StringComparison.OrdinalIgnoreCase) ||
                    arg.HasStringPrefix("pk!", ref argPos, StringComparison.OrdinalIgnoreCase) ||
                    arg.HasMentionPrefix(_client.CurrentUser, ref argPos))
                {
                    // Essentially move the argPos pointer by however much whitespace is at the start of the post-argPos string
                    var trimStartLengthDiff = arg.Content.Substring(argPos).Length -
                                              arg.Content.Substring(argPos).TrimStart().Length;
                    argPos += trimStartLengthDiff;

                    // If it does, fetch the sender's system (because most commands need that) into the context,
                    // and start command execution
                    // Note system may be null if user has no system, hence `OrDefault`
                    PKSystem system;
                    using (var conn = await serviceScope.ServiceProvider.GetService<DbConnectionFactory>().Obtain())
                        system = await conn.QueryFirstOrDefaultAsync<PKSystem>(
                            "select systems.* from systems, accounts where accounts.uid = @Id and systems.id = accounts.system",
                            new {Id = arg.Author.Id});
                    await _commands.ExecuteAsync(new PKCommandContext(_client, arg, system), argPos,
                        serviceScope.ServiceProvider);
                }
                else
                {
                    // If not, try proxying anyway
                    await _proxy.HandleMessageAsync(arg);
                }
            }
        }

        private void HandleRuntimeError(Exception e)
        {
            SentrySdk.CaptureException(e);
            Console.Error.WriteLine(e);
        }
    }
}