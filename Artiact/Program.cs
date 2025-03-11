using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Artiact.Client;
using Artiact.Models;
using Artiact.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Artiact;

class Program
{
    static async Task Main( string[] args )
    {
        IConfigurationRoot configuration = new ConfigurationBuilder()
                                          .SetBasePath( AppContext.BaseDirectory )
                                          .AddJsonFile( "appsettings.json", optional: false, reloadOnChange: true )
                                          .AddUserSecrets<Program>()
                                          .Build();

        // Настройка сервисов
        ServiceProvider serviceProvider = new ServiceCollection()
                                         .AddHttpClient()
                                         .BuildServiceProvider();

        IHttpClientFactory? httpClientFactory = serviceProvider.GetService<IHttpClientFactory>();
        HttpClient httpClient = httpClientFactory.CreateClient();

        IConfigurationSection apiSettings = configuration.GetSection( "ApiSettings" );
        string? baseUrl = apiSettings[ "BaseUrl" ];
        string? username = apiSettings[ "Username" ];
        string? password = apiSettings[ "Password" ];
        string? characterName = apiSettings[ "Character" ];

        using ILoggerFactory factory = LoggerFactory.Create( builder => builder.AddConsole() );
        ILogger<IGameClient> logger = factory.CreateLogger<IGameClient>();
        IGameHttpClient gameHttpClient = new GameHttpClient( baseUrl, username, password, httpClient );
        GameClient client = new( gameHttpClient, characterName, logger );
        IGoalService goalService = new GoalService();
        MapService mapService = new( client );
        IStepBuilder stepBuilder = new StepBuilder( client, mapService );
        ActionService actionService = new( client, goalService, stepBuilder );
        await actionService.Initialize();
        await actionService.Action();
    }
}