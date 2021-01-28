using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf.WellKnownTypes;
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
        private MeterReadingService.MeterReadingServiceClient client = null;

        public Worker(ILogger<Worker> logger, IConfiguration config, ReadingFactory factory)
        {
            _logger = logger;
            this.config = config;
            this.factory = factory ?? throw new ArgumentNullException(nameof(factory));
        }

        protected MeterReadingService.MeterReadingServiceClient Client 
        {
            get 
            {
                if (client == null) 
                {
                    var channel = GrpcChannel.ForAddress(config.GetValue<string>("Service:ServerUrl"));
                    client = new MeterReadingService.MeterReadingServiceClient(channel);
                }

                return client;
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);

                var customerId = config.GetValue<int>("Service:CustomerId");

                var pkt = new ReadingPacket()
                {
                    Successful = ReadingStatus.Success,
                    Notes = "This is our test"
                };

                for (var x = 0; x < 5; ++x) 
                {
                    pkt.Readings.Add(await factory.Generate(customerId));
                }

                var result = await Client.AddReadingAsync(pkt);
                if (result.Success == ReadingStatus.Success)
                {
                    _logger.LogInformation("Successfully Sent");
                }
                else 
                {
                    _logger.LogInformation("Failed to Send");
                }

                await Task.Delay(config.GetValue<int>("Service:DelayInterval"), stoppingToken);
            }
        }
    }
}
