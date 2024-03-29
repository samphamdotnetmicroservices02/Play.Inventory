using System;
using System.Net.Http;
using DnsClient.Internal;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using Play.Common.Identity;
using Play.Common.MassTransit;
using Play.Common.MongoDB;
using Play.Inventory.Service.Clients;
using Play.Inventory.Service.Entities;
using Play.Inventory.Service.Exceptions;
using Polly;
using Polly.CircuitBreaker;
using Polly.Extensions.Http;
using Polly.Timeout;
using Play.Common.HealthChecks;
using MassTransit;
using Play.Common.Logging;
using Play.Common.OpenTelemetry;

namespace Play.Inventory.Service
{
    public class Startup
    {
        private const string AllowedOriginSetting = "AllowedOrigin";

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddMongo()
                    .AddMongoRepository<InventoryItem>("inventoryitems")
                    .AddMongoRepository<CatalogItem>("catalogitems")
                    .AddMassTransitWithMessageBroker(Configuration, retryConfigurator =>
                    {
                        /*
                        * anytime a message is not able to be consumed by consumer, it will be retried three times, and we'd have 5 seconds delay
                        */
                        retryConfigurator.Interval(3, TimeSpan.FromSeconds(5));

                        /*
                        * don't retry if throw UnoknowItemException, because CatalogItems does not have that item.
                        * that's why we throw exception and don't execute operation.
                        */
                        retryConfigurator.Ignore(typeof(UnknownItemException));


                    })
                    .AddJwtBearerAuthentication();

            //AddCatalogClient(services);

            services.AddControllers();
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "Play.Inventory.Service", Version = "v1" });
            });

            services.AddHealthChecks()
                .AddMongoDb();

            services.AddSeqLogging(Configuration)
                .AddTracing(Configuration)
                .AddMetrics(Configuration);
        }


        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseSwagger();
                app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Play.Inventory.Service v1"));
                app.UseCors(builder =>
                {
                    builder.WithOrigins(Configuration[AllowedOriginSetting])
                        .AllowAnyHeader() //Allows any the headers that the client want to send
                        .AllowAnyMethod(); //Allows any the methods the client side want to use including GET, POST, PUT and all other verbs
                });
            }

            /*
            * Prometheus:
            * With the metrics side, we also need to do one more thing and that is to enable or create or expose what's going to
            * be called the scraping endpoint. So this is the endpoint that tools like Prometheus can use in a giving interval,
            * start pulling down and pulling into Prometheus, the metrics that we've been collecting across the lifetime of the
            * application. This "UseOpenTelemetryPrometheusScrapingEndpoint" is going to stand up that endpoint that it actually
            * ends with /metrics. You can configure it if you want to, for us, that's going to be good enough
            */
            app.UseOpenTelemetryPrometheusScrapingEndpoint();

            app.UseHttpsRedirection();

            app.UseRouting();

            app.UseAuthentication();

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();

                endpoints.MapPlayEconomyHealthCheck();
            });
        }

        private static void AddCatalogClient(IServiceCollection services)
        {
            /*
             * 5.SynchronousInterServiceCommunication/7.ImplementingRetriesWithExponentialBackoff
             * The one issue with exponential backoff is that if you have multiple instances of your inventory services
             * calling catalog and they are all waiting exactly 4 seconds, 8 seconds, 16 seconds between retries, that it
             * can actually cause a kind of burst of cores into catalog service at the very same time. Because they are all
             * waiting for the exactly the same time between retries. So to avoid overwhelming our catalog service, what we
             * can do is introduce a little bit of randomess. So that it is not exactly 4 seconds or exactly 8 seconds,
             * stuff like that 
             * jitterer, this is a way that we add randomness
            */
            Random jitterer = new Random();

            services.AddHttpClient<CatalogClient>(client =>
            {
                client.BaseAddress = new Uri("https://localhost:5001");
            })
            .AddTransientHttpErrorPolicy(builder => builder.Or<TimeoutRejectedException>().WaitAndRetryAsync(
                5,
                retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))
                                + TimeSpan.FromMilliseconds(jitterer.Next(0, 1000)),
                onRetry: (outcome, timespan, retryAttempt) =>
                {
                    var serviceProvider = services.BuildServiceProvider();
                    serviceProvider.GetService<ILogger<CatalogClient>>()?
                        .LogWarning($"Delaying for {timespan.TotalSeconds} seconds, then making retry {retryAttempt}");
                }
            ))
            .AddTransientHttpErrorPolicy(builder => builder.Or<TimeoutRejectedException>().CircuitBreakerAsync(
                3,
                TimeSpan.FromSeconds(15),
                onBreak: (outcome, timespan) =>
                {
                    var serviceProvider = services.BuildServiceProvider();
                    serviceProvider.GetService<ILogger<CatalogClient>>()?
                        .LogWarning($"Opening the circuit for {timespan.TotalSeconds} seconds...");
                },
                onReset: () =>
                {
                    var serviceProvider = services.BuildServiceProvider();
                    serviceProvider.GetService<ILogger<CatalogClient>>()?
                        .LogWarning($"Closing the circuit...");
                }
            ))
            .AddPolicyHandler(Policy.TimeoutAsync<HttpResponseMessage>(1));
            /* TimmeoutRejectedException to make sure that both policies (AddTransientHttpErrorPolicy, AddPolicyHandler)
             * so that they can work together. If we wait because of timeout produced by timeout policy then it will
             * fire a timeout exception. Let's go ahead and also retry. That's way we combine both policies.
            */
            // .AddPolicyHandler((serviceProvider, request) =>
            //     Policy.WrapAsync
            //     (
            //         HttpPolicyExtensions.HandleTransientHttpError().Or<TimeoutRejectedException>().WaitAndRetryAsync
            //         (
            //             5, //retry 5 times
            //             retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))
            //                 + TimeSpan.FromMilliseconds(jitterer.Next(0, 1000)),
            //             onRetry: (outcome, timespan, retryAttempt, context) =>
            //             {
            //                 serviceProvider.GetService<ILogger<CatalogClient>>()?
            //                     .LogWarning($"Delaying for {timespan.TotalSeconds} seconds, then making retry {retryAttempt}");
            //             }
            //         ),
            //         HttpPolicyExtensions.HandleTransientHttpError()
            //         .Or<TimeoutRejectedException>()
            //         .Or<BrokenCircuitException>()
            //         .CircuitBreakerAsync
            //         (
            //             2, // 3 requests fail and then open circuit
            //             TimeSpan.FromSeconds(15), // how much time we keep the circuit breaker open
            //             onBreak: (outcome, timespan) => // function when circuit open
            //             {
            //                 serviceProvider.GetService<ILogger<CatalogClient>>()?
            //                     .LogWarning($"Opening the circuit for {timespan.TotalSeconds} seconds...");
            //             },
            //             onReset: () => // function when circuit close
            //             {
            //                 serviceProvider.GetService<ILogger<CatalogClient>>()?
            //                     .LogWarning($"Closing the circuit...");
            //             },
            //             onHalfOpen: () =>
            //             {
            //                 serviceProvider.GetService<ILogger<CatalogClient>>()?
            //                     .LogWarning($"Half Open the circuit...");
            //             }
            //         ),
            //         Policy.TimeoutAsync<HttpResponseMessage>(1)
            //     )
            // );
        }

    }
}
