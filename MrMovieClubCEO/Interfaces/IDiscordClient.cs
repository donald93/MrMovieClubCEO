using Discord;
using Discord.WebSocket;

namespace MrMovieClubCEO.Interfaces;

public interface IDiscordClient
{
    Func<SocketSlashCommand, Task> SlashCommandHandler { get; set; }
    Func<SocketMessage, Task> MessageReceivedHandler { get; set; }
    IList<ApplicationCommandProperties> Commands { get; set; }

    Task StartAsync();
    Task SendMessageToChannelAsync(ulong channelId, string message);
}