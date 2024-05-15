using Google.Protobuf.WellKnownTypes;
using MeterReaderWeb.Services;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace MeterReaderClient
{
    public class ReadingFactory
    {
        private readonly ILogger<ReadingFactory> _logger;
        public ReadingFactory(ILogger<ReadingFactory> logger)
        {
            _logger = logger;
        }

        public Task<ReadingMessage> Generate(int customerId)
        {
            var readingMsg = new ReadingMessage
            {
                CustomerId = customerId,
                ReadingValue = new Random().Next(1000, 100000),
                ReadingTime = Timestamp.FromDateTime(DateTime.UtcNow)
            };
            return Task.FromResult(readingMsg);
        }
    }
}
