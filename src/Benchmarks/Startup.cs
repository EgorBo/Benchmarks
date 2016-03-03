// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Data.Common;
using System.Data.SqlClient;
using System.Reflection;
using Benchmarks.Configuration;
using Benchmarks.Data;
using Benchmarks.Middleware;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.PlatformAbstractions;

namespace Benchmarks
{
    public class Startup
    {
        public Startup(IApplicationEnvironment appEnv, IHostingEnvironment hostingEnv, Scenarios scenarios)
        {
            // Set up configuration sources.
            var builder = new ConfigurationBuilder()
                .SetBasePath(appEnv.ApplicationBasePath)
                .AddCommandLine(Program.Args)
                .AddJsonFile("appsettings.json")
                .AddJsonFile($"appsettings.{hostingEnv.EnvironmentName}.json", optional: true)
                .AddEnvironmentVariables();

            Configuration = builder.Build();

            Scenarios = scenarios;
        }

        public IConfigurationRoot Configuration { get; set; }

        public Scenarios Scenarios { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            services.Configure<AppSettings>(Configuration);

            // We re-register the Scenarios as an instance singleton here to avoid it being created again due to the
            // registration done in Program.Main
            services.AddSingleton(Scenarios);

            // Common DB services
            services.AddSingleton<IRandom, DefaultRandom>();
            services.AddSingleton<ApplicationDbSeeder>();
            services.AddEntityFrameworkSqlServer()
                .AddDbContext<ApplicationDbContext>();

            if (Scenarios.Any("Raw") || Scenarios.Any("Dapper"))
            {
                // TODO: Add support for plugging in different DbProviderFactory implementations via configuration
                services.AddSingleton<DbProviderFactory>(SqlClientFactory.Instance);
            }

            if (Scenarios.Any("Ef"))
            {
                services.AddScoped<EfDb>();
            }

            if (Scenarios.Any("Raw"))
            {
                services.AddScoped<RawDb>();
            }

            if (Scenarios.Any("Dapper"))
            {
                services.AddScoped<DapperDb>();
            }

            if (Scenarios.Any("Fortunes"))
            {
                services.AddWebEncoders();
            }

            if (Scenarios.Any("Mvc"))
            {
                var mvcBuilder = services.AddMvcCore()
                    .AddControllersAsServices(typeof(Startup).GetTypeInfo().Assembly);

                if (Scenarios.MvcApis)
                {
                    mvcBuilder.AddJsonFormatters();
                }

                if (Scenarios.MvcViews || Scenarios.Any("MvcDbFortunes"))
                {
                    mvcBuilder
                        .AddViews()
                        .AddRazorViewEngine();
                }
            }
        }

        public void Configure(IApplicationBuilder app, ApplicationDbSeeder dbSeeder, ApplicationDbContext dbContext)
        {
            app.UseErrorHandler();

            if (Scenarios.Plaintext)
            {
                app.UsePlainText();
            }

            if (Scenarios.Json)
            {
                app.UseJson();
            }

            // Single query endpoints
            if (Scenarios.DbSingleQueryRaw)
            {
                app.UseSingleQueryRaw();
            }

            if (Scenarios.DbSingleQueryDapper)
            {
                app.UseSingleQueryDapper();
            }

            if (Scenarios.DbSingleQueryEf)
            {
                app.UseSingleQueryEf();
            }

            // Multiple query endpoints
            if (Scenarios.DbMultiQueryRaw)
            {
                app.UseMultipleQueriesRaw();
            }

            if (Scenarios.DbMultiQueryDapper)
            {
                app.UseMultipleQueriesDapper();
            }

            if (Scenarios.DbMultiQueryEf)
            {
                app.UseMultipleQueriesEf();
            }

            // Fortunes endpoints
            if (Scenarios.DbFortunesRaw)
            {
                app.UseFortunesRaw();
            }

            if (Scenarios.DbFortunesDapper)
            {
                app.UseFortunesDapper();
            }

            if (Scenarios.DbFortunesEf)
            {
                app.UseFortunesEf();
            }

            if (Scenarios.Any("Db"))
            {
                dbContext.Database.EnsureCreated();

                if (!dbSeeder.Seed())
                {
                    Environment.Exit(1);
                }
            }

            if (Scenarios.Any("Mvc"))
            {
                app.UseMvc();
            }

            if (Scenarios.StaticFiles)
            {
                app.UseStaticFiles();
            }

            app.RunDebugInfoPage();
        }
    }
}
