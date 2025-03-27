using System.Net;
using System.Runtime.InteropServices.JavaScript;
using System.Text;
using Discord;
using Discord.Net;
using Discord.WebSocket;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;
using MrMovieClubCEO.Interfaces;
using MrMovieClubCEO.Models.Configuration;
using MrMovieClubCEO.Models.Database;
using Newtonsoft.Json;

namespace MrMovieClubCEO;

public class Worker(ILogger<Worker> logger, IServiceProvider services, IMovieClubRepository repository)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            using var scope = services.CreateScope();
            var discordOptions = scope.ServiceProvider.GetRequiredService<IOptions<DiscordOptions>>().Value;

            await repository.InitializeAsync();

            await CreateAndStartDiscordClient(discordOptions.Token);
        }
    }

    private static DiscordSocketClient _discordClient;
    private static readonly Year5Puzzles Year5 = new();

    private async Task CreateAndStartDiscordClient(string token)
    {
        _discordClient = new DiscordSocketClient();
        _discordClient.Log += Log;

        await _discordClient.LoginAsync(TokenType.Bot, token);
        await _discordClient.StartAsync();

        _discordClient.Ready += DiscordClientReady;
        _discordClient.MessageReceived += MessageReceived;
        _discordClient.SlashCommandExecuted += SlashCommandHandler;

        // Block this task until the program is closed.
        await Task.Delay(-1);
    }

    private static Task Log(LogMessage msg)
    {
        Console.WriteLine(msg.ToString());
        return Task.CompletedTask;
    }

    public static async Task DiscordClientReady()
    {
        Console.WriteLine("Bot is connected!");

        List<ApplicationCommandProperties> applicationCommandProperties = new();

        // Let's do our global command
        var submitCommand = new SlashCommandBuilder();
        submitCommand.WithName("submit");
        submitCommand.WithDescription("Submit your answer to this puzzle");
        submitCommand.AddOption("answer", ApplicationCommandOptionType.String, "The answer to the puzzle",
            isRequired: true);

        applicationCommandProperties.Add(submitCommand.Build());

        var registerChannelCommand = new SlashCommandBuilder();
        registerChannelCommand.WithName("register");
        registerChannelCommand.WithDescription(
            "Register the current channel as the channel to post the leaderboard to.");
        applicationCommandProperties.Add(registerChannelCommand.Build());

        try
        {
            await _discordClient.BulkOverwriteGlobalApplicationCommandsAsync(applicationCommandProperties.ToArray());
            // With global commands we don't need the guild.
            // await _discordClient.CreateGlobalApplicationCommandAsync(submitCommand.Build());
            // Using the ready event is a simple implementation for the sake of the example. Suitable for testing and development.
            // For a production bot, it is recommended to only run the CreateGlobalApplicationCommandAsync() once for each command.
        }
        catch (HttpException exception)
        {
            // If our command was invalid, we should catch an ApplicationCommandException. This exception contains the path of the error as well as the error message. You can serialize the Error field in the exception to get a visual of where your error is.
            var json = JsonConvert.SerializeObject(exception.Errors, Formatting.Indented);

            // You can send this error somewhere or just print it to the console, for this example we're just going to print it.
            Console.WriteLine(json);
        }
    }

    private static async Task MessageReceived(SocketMessage message)
    {
        if (message.Channel is IDMChannel && !message.Author.IsBot)
        {
            await message.Channel.SendMessageAsync(
                $"Ah, hello {message.Author.Username}, welcome to the game! Let’s start things off easy and demonstrate how to submit answers! You’ll use the command `/submit` to officially send in answers. If you’re correct, you’ll be given the next challenge. If you’re not, you’ll be notified. The answers will always be related to a specific movie: the title, an actor, or something else\n\nTo get started, let’s answer a simple question. What is a movie submitted in our current category?");
        }
    }

    private async Task SlashCommandHandler(SocketSlashCommand command)
    {
        if (command.CommandName == "submit")
        {
            await command.RespondAsync($"Processing...");

            var answer = command.Data.Options.FirstOrDefault(x => x.Name == "answer")?.Value.ToString();

            var player = await repository.GetPlayerAsync(command.User.Id.ToString());

            if (player is null)
            {
                var correctAnswer = Year5.Puzzles.First()
                    .Answers.Any(a => string.Equals(a, answer, StringComparison.InvariantCultureIgnoreCase));

                if (correctAnswer)
                {
                    await command.Channel.SendMessageAsync(
                        $"Congratulations! You've completed the first puzzle. Here's your next challenge: {Year5.Puzzles.ToArray()[1].Question}");

                    await repository.UpsertPlayerAsync(new Player
                    {
                        Id = command.User.Id.ToString(),
                        Username = command.User.Username,
                        CurrentPuzzle = Year5.Puzzles.ToArray()[1].Id,
                        LastCompletedPuzzle = Year5.Puzzles.ToArray()[0].Id,
                        HasReceivedIntro = true
                    });

                    await UpdateLeaderboardAsync(command.User.Username);
                }
                else
                {
                    await command.Channel.SendMessageAsync($"Not quite, try again!");
                }

                return;
            }

            var usersPuzzleId = player.CurrentPuzzle;
            var currentPuzzle = Year5.Puzzles.First(p => p.Id == usersPuzzleId);
            var wasAnswerCorrect =
                currentPuzzle.Answers.Any(a => string.Equals(a, answer, StringComparison.InvariantCultureIgnoreCase));

            if (wasAnswerCorrect)
            {
                var indexOfNextPuzzle = Year5.Puzzles.ToList().IndexOf(currentPuzzle) + 1;

                if (indexOfNextPuzzle >= Year5.Puzzles.Count)
                {
                    await command.Channel.SendMessageAsync(
                        $"You are a worthy champion. Donald will be in touch with your prize.");
                    return;
                }

                var nextPuzzle = Year5.Puzzles.ToArray()[indexOfNextPuzzle];

                await command.Channel.SendMessageAsync(
                    $"Excellent job, here's you're next challenge:\n {nextPuzzle.Question}");

                player.CurrentPuzzle = nextPuzzle.Id;
                player.LastCompletedPuzzle = currentPuzzle.Id;

                await repository.UpsertPlayerAsync(player);

                await UpdateLeaderboardAsync(player.Username);
            }
            else
            {
                await command.Channel.SendMessageAsync($"Not quite, try again!");
                return;
            }
        }

        if (command.CommandName == "register")
        {
            if (command.IsDMInteraction)
            {
                await command.RespondAsync("This command is only to be used in guild channels.");
            }

            var guildRegistration = new GuildRegistration
            {
                Id = command.GuildId.ToString(), ChannelId = command.Channel.Id, ChannelName = command.Channel.Name,
            };

            await command.RespondAsync("Registering channel");
            await repository.RegisterLeaderboardChannel(guildRegistration);

            var registeredChannel = _discordClient.GetChannel(command.Channel.Id) as IMessageChannel;
            await registeredChannel.SendMessageAsync("This channel is now registered for leaderboard updates.");
        }
    }

    private async Task UpdateLeaderboardAsync(string username)
    {
        var guild = repository.GetGuildRegistration();

        var players = repository.GetPlayers();
        var leaderboard = players
            .Select(p => new
            {
                username = p.Username,
                index = Year5.Puzzles.ToList().FindIndex(puzzle => puzzle.Id == p.CurrentPuzzle)
            })
            .GroupBy(x => x.index)
            .OrderDescending();

        var stringBuilder = new StringBuilder();
        stringBuilder.AppendLine();

        foreach (var group in leaderboard)
        {
            stringBuilder.Append($"Puzzle #{group.Key}: ");
            for (var i = 0; i < group.Count(); i++)
            {
                stringBuilder.Append($"{group.ElementAt(i).username}");

                if (i < group.Count() - 1)
                {
                    stringBuilder.Append(", ");
                }
            }

            stringBuilder.AppendLine();
        }

        if (guild is null)
        {
            //handle error
            return;
        }

        var registeredChannel = _discordClient.GetChannel(guild.ChannelId) as IMessageChannel;
        await registeredChannel.SendMessageAsync(
            $"{username} just finished a puzzle! Here's the current leaderboard: {stringBuilder}");
    }
}