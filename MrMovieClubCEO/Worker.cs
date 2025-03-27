using System.Text;
using Discord;
using Discord.WebSocket;
using MrMovieClubCEO.Interfaces;
using MrMovieClubCEO.Models.Database;
using IDiscordClient = MrMovieClubCEO.Interfaces.IDiscordClient;

namespace MrMovieClubCEO;

public class Worker(IDiscordClient discordClient, IMovieClubRepository repository)
    : BackgroundService
{
    private readonly Year5Puzzles _year5 = new();
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await repository.InitializeAsync();
            
            discordClient.SlashCommandHandler = SlashCommandHandler;
            discordClient.MessageReceivedHandler = MessageReceived;
            discordClient.Commands = CreateSlashCommands();
            
            await discordClient.StartAsync();    
        }
    }

    private IList<ApplicationCommandProperties> CreateSlashCommands()
    {
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
        
        return applicationCommandProperties;
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
                var correctAnswer = _year5.Puzzles.First()
                    .Answers.Any(a => string.Equals(a, answer, StringComparison.InvariantCultureIgnoreCase));

                if (correctAnswer)
                {
                    await command.Channel.SendMessageAsync(
                        $"Congratulations! You've completed the first puzzle. Here's your next challenge: {_year5.Puzzles.ToArray()[1].Question}");

                    await repository.UpsertPlayerAsync(new Player
                    {
                        Id = command.User.Id.ToString(),
                        Username = command.User.Username,
                        CurrentPuzzle = _year5.Puzzles.ToArray()[1].Id,
                        LastCompletedPuzzle = _year5.Puzzles.ToArray()[0].Id,
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
            var currentPuzzle = _year5.Puzzles.First(p => p.Id == usersPuzzleId);
            var wasAnswerCorrect =
                currentPuzzle.Answers.Any(a => string.Equals(a, answer, StringComparison.InvariantCultureIgnoreCase));

            if (wasAnswerCorrect)
            {
                var indexOfNextPuzzle = _year5.Puzzles.ToList().IndexOf(currentPuzzle) + 1;

                if (indexOfNextPuzzle >= _year5.Puzzles.Count)
                {
                    await command.Channel.SendMessageAsync(
                        $"You are a worthy champion. Donald will be in touch with your prize.");
                    return;
                }

                var nextPuzzle = _year5.Puzzles.ToArray()[indexOfNextPuzzle];

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

            await discordClient.SendMessageToChannelAsync(command.Channel.Id, "This channel is now registered for leaderboard updates.");
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
                index = _year5.Puzzles.ToList().FindIndex(puzzle => puzzle.Id == p.CurrentPuzzle)
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

        await discordClient.SendMessageToChannelAsync(guild.ChannelId,
            $"{username} just finished a puzzle! Here's the current leaderboard: {stringBuilder}");
    }
}