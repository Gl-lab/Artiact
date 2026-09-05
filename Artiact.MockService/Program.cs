using Artiact.SmartProxy.Services;
using Artiact.SmartProxy.Models;

WebApplicationBuilder builder = WebApplication.CreateBuilder( args );
builder.Configuration.Sources.Clear();
builder.WebHost.ConfigureKestrel( options => options.ListenLocalhost( 5000 ) );

builder.Services.AddControllers().AddJsonOptions( options => options.JsonSerializerOptions.Converters.Add( new RoundTripDateTimeConverter() ) );
builder.Services.AddSingleton<IMockScenarioStore, MockScenarioStore>();

WebApplication app = builder.Build();
app.MapControllers();
app.MapFallback( () => Results.Problem(
    statusCode: StatusCodes.Status404NotFound,
    extensions: new Dictionary<string, object?> { [ "code" ] = "unsupported_route" } ) );
app.Run();

public partial class Program;
