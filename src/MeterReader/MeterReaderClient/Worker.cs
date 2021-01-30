using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Grpc.Net.Client;
using MeterReaderWeb.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MeterReaderClient
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly IConfiguration config;
        private readonly ReadingFactory factory;
        private readonly ILoggerFactory loggerFactory;
        private MeterReadingService.MeterReadingServiceClient client = null;
        private string token;
        private DateTime expiration = DateTime.MinValue;

        public Worker(ILogger<Worker> logger, IConfiguration config, ReadingFactory factory, ILoggerFactory loggerFactory)
        {
            _logger = logger;
            this.config = config;
            this.factory = factory ?? throw new ArgumentNullException(nameof(factory));
            this.loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));

        }

        protected MeterReadingService.MeterReadingServiceClient Client 
        {
            get 
            {
                if (client == null) 
                {
                    var cert = new X509Certificate2(config["Service:CertFilename"], config["Service:CertPassword"]);

                    var handler = new HttpClientHandler();
                    handler.ClientCertificates.Add(cert);

                    var Httpclient = new HttpClient(handler);

                    var opts = new GrpcChannelOptions()
                    {
                        HttpClient = Httpclient,
                        LoggerFactory = loggerFactory
                    };
                    var channel = GrpcChannel.ForAddress(config.GetValue<string>("Service:ServerUrl"), opts);
                    client = new MeterReadingService.MeterReadingServiceClient(channel);
                }

                return client;
            }
        }

        protected bool NeedsLogin() => string.IsNullOrWhiteSpace(token) || expiration > DateTime.UtcNow;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var counter = 0;

            var customerId = config.GetValue<int>("Service:CustomerId");
            
            while (!stoppingToken.IsCancellationRequested)
            {
                counter++;

                if (counter % 10 == 0) 
                {
                    Console.Write("Sending Diagnostics");
                    var stream = Client.SendDiagnostics();
                    for (var x = 0; x < 5; x++) 
                    {
                        var reading = await factory.Generate(customerId);
                        await stream.RequestStream.WriteAsync(reading);
                    }

                    await stream.RequestStream.CompleteAsync();
                }

                _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);


                var pkt = new ReadingPacket()
                {
                    Successful = ReadingStatus.Success,
                    Notes = "This is our test"
                };

                for (var x = 0; x < 5; ++x) 
                {
                    pkt.Readings.Add(await factory.Generate(customerId));
                }

                try
                {
                        var result = await Client.AddReadingAsync(pkt);
                        if (result.Success == ReadingStatus.Success)
                        {
                            _logger.LogInformation("Successfully Sent");
                        }
                        else
                        {
                            _logger.LogInformation("Failed to Send");
                        }
                    
                }
                catch (RpcException ex) 
                {
                    if (ex.StatusCode == StatusCode.OutOfRange) 
                    {
                        _logger.LogError($"{ex.Trailers}");
                    }
                    _logger.LogError($"Exception Thrown: {ex}");
                }
                await Task.Delay(config.GetValue<int>("Service:DelayInterval"), stoppingToken);
            }
        }

        private async Task<bool> GenerateToken()
        {
            var request = new TokenRequest()
            {
                Username = config["Service:Username"],
                Password = config["Service:Password"]
            };

            var response = await Client.CreateTokenAsync(request);

            if (response.Success) 
            {
                token = response.Token;
                expiration = response.Expiration.ToDateTime();

                return true;
            }

            return false;
        }
    }
}
