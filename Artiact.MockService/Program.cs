using Artiact.SmartProxy.Services;

WebApplicationBuilder builder = WebApplication.CreateBuilder( args );

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Добавляем наши сервисы
builder.Services.AddSingleton<ICharacterCache, CharacterCache>();
builder.Services.AddScoped<IActionService, ActionService>();
//
// // Вспомогательные методы для обработки ответов
// static async Task<(string Body, HttpContent NewContent)> GetResponseContent( HttpContent originalContent )
// {
//     byte[] originalBytes = await originalContent.ReadAsByteArrayAsync();
//     using StreamReader reader = new( new MemoryStream( originalBytes ) );
//     string body = await reader.ReadToEndAsync();
//
//     ByteArrayContent newContent = new( originalBytes );
//     newContent.Headers.ContentType = originalContent.Headers.ContentType;
//
//     return ( body, newContent );
// }
//
// static async Task ProcessCharacterResponse<T>( string body,
//                                                string? characterName,
//                                                string? action,
//                                                IServiceProvider services,
//                                                Func<T, Character?> getCharacter ) where T : class
// {
//     if ( !String.IsNullOrEmpty( body ) )
//     {
//         CharacterCache characterCache = services.GetRequiredService<CharacterCache>();
//         T? response = JsonSerializer.Deserialize<T>( body );
//
//         if ( response != null )
//         {
//             Character? character = getCharacter( response );
//
//             if ( character != null )
//             {
//                 if ( action == null )
//                 {
//                     characterCache.AddCharacter( characterName, character );
//                 }
//                 else
//                 {
//                     characterCache.UpdateCharacter( characterName, character, action );
//                 }
//             }
//         }
//     }
// }

// // Настраиваем YARP
// builder.Services.AddReverseProxy()
//        .LoadFromConfig( builder.Configuration.GetSection( "ReverseProxy" ) )
//        .AddTransforms( transforms =>
//         {
//             transforms.AddResponseTransform( async responseContext =>
//                 {
//                     string? path = responseContext.HttpContext.Request.Path.Value;
//                     if ( path.StartsWith( "/characters/" ) )
//                     {
//                         ( string body, HttpContent newContent ) =
//                             await GetResponseContent( responseContext.ProxyResponse.Content );
//                         string? characterName = path.Split( '/' )[ 2 ];
//
//                         await ProcessCharacterResponse<CharacterResponse>(
//                             body,
//                             characterName,
//                             null,
//                             responseContext.HttpContext.RequestServices,
//                             r => r?.Data
//                         );
//
//                         responseContext.ProxyResponse.Content = newContent;
//                     }
//                     else if ( path.StartsWith( "/my/" ) && path.Contains( "/action" ) )
//                     {
//                         string?[] parts = path.Split( '/' );
//                         if ( parts.Length >= 4 )
//                         {
//                             ( string body, HttpContent newContent ) =
//                                 await GetResponseContent( responseContext.ProxyResponse.Content );
//                             string? characterName = parts[ 2 ];
//                             string? action = parts[ 4 ];
//
//                             await ProcessCharacterResponse<ActionResponse>(
//                                 body,
//                                 characterName,
//                                 action,
//                                 responseContext.HttpContext.RequestServices,
//                                 r => r?.Data?.Character
//                             );
//
//                             responseContext.ProxyResponse.Content = newContent;
//                         }
//                     }
//                 }
//             );
//         } );

WebApplication app = builder.Build();


app.UseHttpsRedirection();
// app.UseForwardedHeaders( new ForwardedHeadersOptions
// {
//     ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
// } );
// app.Use( async ( context, next ) =>
// {
//     var path = context.Request.Path.Value;
//     if ( path.StartsWith( "/characters/" ) )
//     {
//         var segments = path.Split( '/' );
//         if ( segments.Length > 2 && segments[ 2 ].Contains( "Mock" ) )
//         {
//             // Перенаправляем запрос в контроллер на новый маршрут
//             context.Request.Path = $"/mock{path}";
//         }
//     }
//
//     await next();
// } );
app.UseAuthorization();

app.MapControllers();
//
// app.MapReverseProxy();

app.Run();