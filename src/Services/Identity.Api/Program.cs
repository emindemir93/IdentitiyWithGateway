// // Copyright (c) Brock Allen & Dominick Baier. All rights reserved.
// // Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.


using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.SystemConsole.Themes;
using System;
using System.Linq;
using Identity.Api;
using Identity.Api.Data;
using Microsoft.EntityFrameworkCore;

// namespace Identity.Api
// {
//     public class Program
//     {
//         public static int Main(string[] args)
//         {
//             Log.Logger = new LoggerConfiguration()
//                 .MinimumLevel.Debug()
//                 .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
//                 .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
//                 .MinimumLevel.Override("System", LogEventLevel.Warning)
//                 .MinimumLevel.Override("Microsoft.AspNetCore.Authentication", LogEventLevel.Information)
//                 .Enrich.FromLogContext()
//                 .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level}] {SourceContext}{NewLine}{Message:lj}{NewLine}{Exception}{NewLine}", theme: AnsiConsoleTheme.Code)
//                 .CreateLogger();

//             try
//             {
//                 var builder = WebApplication.CreateBuilder(args);



//                 var app = builder
//                     .ConfigureServices()
//                     .ConfigurePipeline();
//                 var startup = new Startup(builder.Environment ,builder.Configuration);

//                 // Manually call ConfigureServices()
//                 var services = builder.Services;
//                 startup.ConfigureServices(services);

//                 var seed = args.Contains("/seed");
//                 if (seed)
//                 {
//                     args = args.Except(new[] { "/seed" }).ToArray();
//                 }
//                 if (seed)
//                 {
//                     Log.Information("Seeding database...");
//                     var config = app.Services.GetRequiredService<IConfiguration>();
//                     var connectionString = config.GetConnectionString("DefaultConnection");
//                     SeedData.EnsureSeedData(app);
//                     Log.Information("Done seeding database.");
//                     return 0;
//                 }

//                 Log.Information("Starting host...");
//                 app.Run();
//                 return 0;
//             }
//             catch (Exception ex)
//             {
//                 Log.Fatal(ex, "Host terminated unexpectedly.");
//                 return 1;
//             }
//             finally
//             {
//                 Log.CloseAndFlush();
//             }
//         }

//         public static IHostBuilder CreateHostBuilder(string[] args) =>
//             Host.CreateDefaultBuilder(args)
//                 .UseSerilog()
//                 .ConfigureWebHostDefaults(webBuilder =>
//                 {
//                     webBuilder
//                         .UseStartup<Startup>();
//                 });
//     }
// }

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((ctx, lc) => lc
        .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level}] {SourceContext}{NewLine}{Message:lj}{NewLine}{Exception}{NewLine}")
        .Enrich.FromLogContext()
        .ReadFrom.Configuration(ctx.Configuration));

    var app = builder
        .ConfigureServices()
        .ConfigurePipeline();

    // this seeding is only for the template to bootstrap the DB and users.
    // in production you will likely want a different approach.
    if (args.Contains("/seed"))
    {
        Log.Information("Seeding database...");
        SeedData.EnsureSeedData(app);
        Log.Information("Done seeding database. Exiting.");
    }
    using (var scope = app.Services.GetRequiredService<IServiceScopeFactory>().CreateScope())
    {
        var context = scope.ServiceProvider.GetService<ApplicationDbContext>();
        context.Database.Migrate();
    }
    app.UseSwagger();
    app.UseSwaggerUI();
    app.Run();
}
catch (Exception ex) when (ex.GetType().Name is not "StopTheHostException") // https://github.com/dotnet/runtime/issues/60600
{
    Log.Fatal(ex, "Unhandled exception");
}
finally
{
    Log.Information("Shut down complete");
    Log.CloseAndFlush();
}