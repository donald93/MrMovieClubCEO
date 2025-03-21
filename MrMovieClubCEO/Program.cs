using System.Net;
using Discord;
using Discord.Net;
using Discord.WebSocket;
using Microsoft.Azure.Cosmos;
using MrMovieClubCEO;
using Newtonsoft.Json;

public class Program
{
    
    private static DiscordSocketClient _discordClient;
    private static CosmosClient _cosmosClient;
    private static Container _container;

    public static async Task Main()
    {
        _cosmosClient = CreateCosmosClient();
        var database = await _cosmosClient.CreateDatabaseIfNotExistsAsync("MovieClub");
        var containerResponse = await database.Database.CreateContainerIfNotExistsAsync("Year5Puzzle", "/id");
        _container = containerResponse.Container;
        
        await CreateAndStartDiscordClient();
    }

    private static async Task CreateAndStartDiscordClient()
    {
        _discordClient = new DiscordSocketClient();
        _discordClient.Log += Log;

        //  You can assign your bot token to a string, and pass that in to connect.
        //  This is, however, insecure, particularly if you plan to have your code hosted in a public repository.
        var token = "secret";

        // Some alternative options would be to keep your token in an Environment Variable or a standalone file.
        // var token = Environment.GetEnvironmentVariable("NameOfYourEnvironmentVariable");
        // var token = File.ReadAllText("token.txt");
        // var token = JsonConvert.DeserializeObject<AConfigurationClass>(File.ReadAllText("config.json")).Token;

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

        // Let's do our global command
        var globalCommand = new SlashCommandBuilder();
        globalCommand.WithName("submit");
        globalCommand.WithDescription("Submit your answer to this puzzle");
        globalCommand.AddOption("answer", ApplicationCommandOptionType.String, "The answer to the puzzle", isRequired: true);

        try
        {
            // With global commands we don't need the guild.
            await _discordClient.CreateGlobalApplicationCommandAsync(globalCommand.Build());
            // Using the ready event is a simple implementation for the sake of the example. Suitable for testing and development.
            // For a production bot, it is recommended to only run the CreateGlobalApplicationCommandAsync() once for each command.
        }
        catch(ApplicationCommandException exception)
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
            await message.Channel.SendMessageAsync($"Ah, hello {message.Author.Username}, welcome to the game! Let’s start things off easy and demonstrate how to submit answers! You’ll use the command `/submit` to officially send in answers. If you’re correct, you’ll be given the next challenge. If you’re not, you’ll be notified. The answers will always be related to a specific movie: the title, an actor, or something else\n\nTo get started, let’s answer a simple question. What is a movie submitted in our current category?");
        }
    }
    
    private static async Task SlashCommandHandler(SocketSlashCommand command)
    {
        await command.RespondAsync($"Processing...");
        var year5 = new Year5Puzzles();
        var answer = command.Data.Options.FirstOrDefault(x => x.Name == "answer")?.Value.ToString();

        ItemResponse<Player> userResponse = default;
        
        try
        {
            userResponse =
                await _container.ReadItemAsync<Player>(command.User.Id.ToString(), new  PartitionKey(command.User.Id.ToString()));
        }
        catch (CosmosException cosmosException)
        {
            if (cosmosException.StatusCode == HttpStatusCode.NotFound)
            {
                var correctAnswer = year5.Puzzles.First().Answers.Any(a => string.Equals(a, answer, StringComparison.InvariantCultureIgnoreCase));
                
                if (correctAnswer)
                {
                    await command.Channel.SendMessageAsync($"Congratulations! You've completed the first puzzle. Here's your next challenge: {year5.Puzzles.ToArray()[1].Question}");
                    
                    await _container.UpsertItemAsync(new Player
                    {
                        Id = command.User.Id.ToString(),
                        Username = command.User.Username,
                        CurrentPuzzle = year5.Puzzles.ToArray()[1].Id,
                        HasReceivedIntro = true
                    }, new PartitionKey(command.User.Id.ToString()));
                }
                else
                {
                    await command.Channel.SendMessageAsync($"Not quite, try again!");
                }

                return;
            }
        }

        var usersPuzzleId = userResponse.Resource.CurrentPuzzle;
        var currentPuzzle = year5.Puzzles.First(p => p.Id == usersPuzzleId);
        var wasAnswerCorrect = currentPuzzle.Answers.Any(a => string.Equals(a, answer, StringComparison.InvariantCultureIgnoreCase));
        
        if (wasAnswerCorrect)
        {
            var indexOfNextPuzzle = year5.Puzzles.ToList().IndexOf(currentPuzzle) + 1;

            if (indexOfNextPuzzle >= year5.Puzzles.Count)
            {
                await command.Channel.SendMessageAsync($"You are a worthy champion. Donald will be in touch with your prize.");
                return;
            }
            
            var nextPuzzle = year5.Puzzles.ToArray()[indexOfNextPuzzle];
            
            await command.Channel.SendMessageAsync($"Excellent job, here's you're next challenge:\n {nextPuzzle.Question}");
            
            var player = userResponse.Resource;
            player.CurrentPuzzle = nextPuzzle.Id;
            
            await _container.UpsertItemAsync(player, new PartitionKey(player.Id));
        }
        else
        {
            await command.Channel.SendMessageAsync($"Not quite, try again!");
            return;
        }
    }

    private static CosmosClient CreateCosmosClient()
    {
        CosmosClient client = new(
            connectionString: "secret",
            new CosmosClientOptions 
            { 
                SerializerOptions = new CosmosSerializationOptions { PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase },
            });
        return client;
    }

}