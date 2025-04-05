using System.Text;
using Discord;
using Discord.WebSocket;
using MrMovieClubCEO.Interfaces;
using MrMovieClubCEO.Models.Database;
using IDiscordClient = MrMovieClubCEO.Interfaces.IDiscordClient;

namespace MrMovieClubCEO;

public class Worker(IDiscordClient discordClient, IMovieClubRepository movieClubRepository, ISunsetRepository sunsetRepository)
    : BackgroundService
{
    private readonly Year5Puzzles _year5 = new();
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            Console.WriteLine("Worker starting...");
            try {
                await movieClubRepository.InitializeAsync();
            
                discordClient.SlashCommandHandler = SlashCommandHandler;
                discordClient.MessageReceivedHandler = MessageReceived;
                discordClient.Commands = CreateSlashCommands();
            
                await discordClient.StartAsync();   
                Console.WriteLine("App initialized");
            } catch (Exception ex) {
                Console.WriteLine($"Error during startup: {ex}");
                throw;
            }
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
        
        var resendClueCommand = new SlashCommandBuilder();
        resendClueCommand.WithName("resend");
        resendClueCommand.WithDescription(
            "Resends the current puzzle in case you forgot. Or maybe it changed?");
        applicationCommandProperties.Add(resendClueCommand.Build());
        
        return applicationCommandProperties;
    }

    private async Task MessageReceived(SocketMessage message)
    {
        var player = await movieClubRepository.GetPlayerAsync(message.Author.Id.ToString());

        if (player is null && message.Channel is IDMChannel && !message.Author.IsBot)
        {
                await message.Channel.SendMessageAsync(
                    $"Ah, hello {message.Author.Username}, welcome to the game! Let’s start things off easy and demonstrate how to submit answers! You’ll use the command `/submit` to officially send in answers. If you’re correct, you’ll be given the next challenge. If you’re not, you’ll be notified. The answers will always be related to a specific movie: the title, an actor, or something else\n\nTo get started, let’s answer a simple question. What is a movie submitted in our current category?");
            
        }
        else if (message.Channel is IDMChannel && !message.Author.IsBot)
        {
            await message.Channel.SendMessageAsync(
                "If you're in need of help I'm sure Donald has hints. If you meant to submit, use /submit.");
        }
    }

    private async Task SlashCommandHandler(SocketSlashCommand command)
    {
        if (command.CommandName == "submit")
        {
            await Submit(command);
            return;
        }
        
        if (command.CommandName == "resend")
        {
            await WhatIsMyPuzzle(command);
            return;
        }

        if (command.CommandName == "register")
        {
            await RegisterChannel(command);
        }
    }

    private async Task WhatIsMyPuzzle(SocketSlashCommand command)
    {
        var player = await movieClubRepository.GetPlayerAsync(command.User.Id.ToString());
        
        var currentPuzzle = _year5.Puzzles.FirstOrDefault(p => p.Id == player.CurrentPuzzle);
        
        if (currentPuzzle.Id == "59811583-4c42-49ad-afcf-b286e4adc363")
        {
            var sunset = await sunsetRepository.GetSunsetToday();

            var beforeSunset = sunset.AddMinutes(-1);

            await command.Channel.SendMessageAsync(
                $"Here's the puzzle for you again:\n {beforeSunset}");
        }
        else
        {
            await command.Channel.SendMessageAsync(
                $"Here's the puzzle for you again:\n {currentPuzzle.Question}");
        }
    }

    private async Task Submit(SocketSlashCommand command)
    {
        await command.RespondAsync($"Processing...");

        var answer = command.Data.Options.FirstOrDefault(x => x.Name == "answer")?.Value.ToString();

        var player = await movieClubRepository.GetPlayerAsync(command.User.Id.ToString());

        if (player is null)
        {
            var correctAnswer = _year5.Puzzles.First()
                .Answers.Any(a => string.Equals(a, answer, StringComparison.InvariantCultureIgnoreCase));

            if (correctAnswer)
            {
                await command.Channel.SendMessageAsync(
                    $"Congratulations! You've completed the first puzzle. Here's your next challenge: {_year5.Puzzles.ToArray()[1].Question}");

                await movieClubRepository.UpsertPlayerAsync(new Player
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
                
                await AnnounceWinner(player.Username);
                return;
            }

            var nextPuzzle = _year5.Puzzles.ToArray()[indexOfNextPuzzle];

            if (nextPuzzle.Id == "59811583-4c42-49ad-afcf-b286e4adc363")
            {
                var sunset = await sunsetRepository.GetSunsetToday();
                
                var beforeSunset = sunset.AddMinutes(-2);
                
                await command.Channel.SendMessageAsync(
                    $"Excellent job, here's you're next challenge:\n {beforeSunset}");
            }
            else
            {
                await command.Channel.SendMessageAsync(
                    $"Excellent job, here's you're next challenge:\n {nextPuzzle.Question}");
            }

            player.CurrentPuzzle = nextPuzzle.Id;
            player.LastCompletedPuzzle = currentPuzzle.Id;

            await movieClubRepository.UpsertPlayerAsync(player);

            await UpdateLeaderboardAsync(player.Username);
        }
        else
        {
            await command.Channel.SendMessageAsync($"Not quite, try again!");
        }
    }

    private async Task RegisterChannel(SocketSlashCommand command)
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
        await movieClubRepository.RegisterLeaderboardChannel(guildRegistration);

        await discordClient.SendMessageToChannelAsync(command.Channel.Id, "This channel is now registered for leaderboard updates.");
    }

    private async Task AnnounceWinner(string username)
    {
        var guild = movieClubRepository.GetGuildRegistration();

        if (guild is null)
        {
            //handle error
            return;
        }

        await discordClient.SendMessageToChannelAsync(guild.ChannelId,
            $"{username} has completed the game! Congratulations! Thank you to everyone who participated.");
    }
    
    private async Task UpdateLeaderboardAsync(string username)
    {
        var guild = movieClubRepository.GetGuildRegistration();

        var players = movieClubRepository.GetPlayers();
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