using Grpc.Core;
using MeterReaderWeb.Data;
using MeterReaderWeb.Data.Entities;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MeterReaderWeb.Services
{
    public class MeterService : MeterReadingService.MeterReadingServiceBase
    {
        private readonly ILogger<MeterService> logger;
        private readonly IReadingRepository repository;

        public MeterService(ILogger<MeterService> logger, IReadingRepository repository)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }
        public async override Task<StatusMessage> AddReading(ReadingPacket request, 
                                                       ServerCallContext context)
        {
            var result = new StatusMessage() {

                Success = ReadingStatus.Failure };
            if (request.Successful == ReadingStatus.Success) 
            {
                try { 
                
                    foreach (var r in request.Readings) 
                    {
                        var reading = new MeterReading()
                        {
                            Value = r.ReadingValue,
                            ReadingDate = r.ReadingTime.ToDateTime(),
                            CustomerId = r.CustomerId
                        };

                        repository.AddEntity(reading);
                    }
                    if (await repository.SaveAllAsync()) 
                    {
                        logger.LogInformation($"Stored {request.Readings.Count} New Readings....");
                        result.Success = ReadingStatus.Success;
                    }
                }
                catch (Exception ex) 
                {
                    result.Message = "Exception thrown during process";
                    logger.LogError($"Exception thrown during saving of reading: {ex}");
                }
            }

            return result;
        }
    }
}
