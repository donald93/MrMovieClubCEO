using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Options;
using MrMovieClubCEO.Models.Configuration;
using IDiscordClient = MrMovieClubCEO.Interfaces.IDiscordClient;

namespace MrMovieClubCEO.Services;

public class DiscordClient : IDiscordClient
{
    private readonly DiscordSocketClient _client;
    private readonly DiscordOptions _options;
    
    public Func<SocketSlashCommand, Task> SlashCommandHandler { get; set; }
    public Func<SocketMessage, Task> MessageReceivedHandler { get; set; }
    public IList<ApplicationCommandProperties> Commands { get; set; }
    
    public DiscordClient(IOptions<DiscordOptions> discordOptions)
    {
        this._options = discordOptions.Value;
        _client = new DiscordSocketClient();
    }
    
    public async Task StartAsync()
    {
        await _client.LoginAsync(TokenType.Bot, _options.Token);
        await _client.StartAsync();
        
        _client.Log += Log;
        _client.SlashCommandExecuted += SlashCommandHandler;
        _client.MessageReceived += MessageReceivedHandler;
        _client.Ready += DiscordClientReady;
        
        await Task.Delay(-1);
    }

    public async Task SendMessageToChannelAsync(ulong channelId, string message)
    {
        var registeredChannel = _client.GetChannel(channelId) as IMessageChannel;
        await registeredChannel.SendMessageAsync(message);
    }

    private async Task DiscordClientReady()
    {
        await _client.BulkOverwriteGlobalApplicationCommandsAsync(Commands.ToArray());
    }
    
    private static Task Log(LogMessage msg)
    {
        Console.WriteLine(msg.ToString());
        return Task.CompletedTask;
    }
}