using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Grpc.Net.Client;
using MeterReaderWeb.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MeterReaderClient
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly IConfiguration _config;
        private MeterReadingService.MeterReadingServiceClient _client;
        private ReadingFactory _readingFactory;
        private string token;
        private DateTime tokenExpiration = DateTime.MinValue;

        protected MeterReadingService.MeterReadingServiceClient client {
            get
            {
                if (_client == null)
                {
                    var channel = GrpcChannel.ForAddress(_config.GetValue<string>("Service:ServiceUrl"));
                    _client = new MeterReadingService.MeterReadingServiceClient(channel);
                }
                return _client;
            } 
        }

        public bool NeedsLogin() => string.IsNullOrEmpty(token) || tokenExpiration > DateTime.UtcNow;

        public Worker(ILogger<Worker> logger, IConfiguration config, ReadingFactory readingFacroty)
        {
            _logger = logger;
            _config= config;
            _readingFactory = readingFacroty;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var counter = 0;
            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);

                var customerId = _config.GetValue<int>("Service:CustomerId");

                //counter++;
                //if (counter % 10 == 0)
                //{
                //    Console.WriteLine("Sending Diagnostics..");
                //    var stream = client.SendDiagnostics();
                //    for (int i = 0; i < 5; i++)
                //    {
                //        var reading = await _readingFactory.Generate(customerId);
                //        await stream.RequestStream.WriteAsync(reading);
                //    }
                //    await stream.RequestStream.CompleteAsync();
                //}
                var readings = new List<ReadingMessage>();
                var readingPackage = new ReadingPackage
                {
                    Successful = ReadingStatus.Success,
                    Notes = "From the client"
                };
                for (int i = 0; i < 5; i++)
                {
                    readingPackage.Readings.Add(await _readingFactory.Generate(customerId));
                }

                try
                {
                    if (!NeedsLogin()  || await GenerateToken())
                    {
                        var headers = new Metadata
                        {
                            { "Authorization", $"Bearer {token}" }
                        };

                        var result = await client.AddReadingAsync(readingPackage, headers: headers);
                        if (result.Success == ReadingStatus.Success)
                        {
                            _logger.LogInformation("Successfully sent the message!");
                        }
                        else
                        {
                            _logger.LogError("Failed to send the message!");
                        }
                    }                    
                }
                catch (RpcException ex)
                {
                    if(ex.StatusCode == StatusCode.OutOfRange)
                    {
                        _logger.LogError($"Server threw out of range exception: {ex.Trailers}");
                    }
                    _logger.LogError($"Exception thrown during adding of reading: {ex}");
                }
                await Task.Delay(_config.GetValue<int>("Service:DelayInterval"), stoppingToken);
            }
        }

        private async Task<bool> GenerateToken()
        {
            var tokenRequest = new TokenRequest
            {
                Username = _config.GetValue<string>("Service:Username"),
                Password = _config.GetValue<string>("Service:Password")
            };
            var response = await client.CreateTokenAsync(tokenRequest);
            if (response.Success)
            {
                token = response.Token;
                tokenExpiration = response.Expiration.ToDateTime();
                return true;
            }
            return false;
        }
    }
}
