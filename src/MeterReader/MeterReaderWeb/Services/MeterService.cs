using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using MeterReaderLib;
using MeterReaderLib.Models;
using MeterReaderWeb.Data;
using MeterReaderWeb.Data.Entities;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace MeterReaderWeb.Services
{
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class MeterService : MeterReadingService.MeterReadingServiceBase
    {
        private readonly ILogger<MeterService> _logger;
        private readonly IReadingRepository _repository;
        private readonly JwtTokenValidationService _jwtToken;

        public MeterService(ILogger<MeterService> logger, IReadingRepository repository, JwtTokenValidationService jwtToken)
        {
            _logger = logger;
           _repository = repository;
            _jwtToken = jwtToken;
        }

        [AllowAnonymous]
        public override async Task<TokenResponse> CreateToken(TokenRequest tokenRequest, ServerCallContext context)
        {
            var cred = new CredentialModel()
            {
                UserName = tokenRequest.Username,
                Passcode = tokenRequest.Password
            };
            var response = await _jwtToken.GenerateTokenModelAsync(cred);
            if (response.Success)
            {
                return new TokenResponse()
                {
                    Token = response.Token,
                    Expiration = Timestamp.FromDateTime(response.Expiration),
                    Success = true
                };
            }
            return new TokenResponse()
            { Success = false };
        }
        public async override Task<StatusMessage> AddReading(ReadingPackage request, ServerCallContext context)
        {
            var result = new StatusMessage()
            {
                Success = ReadingStatus.Failure
            };

            if(request.Successful == ReadingStatus.Success)
            {
                try
                {
                    foreach (var r in request.Readings)
                    {
                        if(r.ReadingValue < 10000)
                        {
                            _logger.LogInformation("Reading value below acceptable level");
                            var metadata = new Metadata
                            {
                                { "BadValue", r.ReadingValue.ToString() },
                                { "Field", "ReadingValue" },
                                { "Message", "Readings are invalid" }
                            };
                            throw new RpcException(new Status(StatusCode.OutOfRange, "Value too low"), metadata);
                        }
                        var reading = new MeterReading()
                        {
                            Value = r.ReadingValue,
                            ReadingDate = r.ReadingTime.ToDateTime(),
                            CustomerId = r.CustomerId
                        };
                        _repository.AddEntity(reading);
                    }
                    if(await _repository.SaveAllAsync())
                    {
                        result.Success = ReadingStatus.Success;
                    }
                    _logger.LogInformation("Added {c} new readings", request.Readings.Count);
                }
                catch (RpcException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Exception thrown during AddReading: {ex}");
                    throw new RpcException(new Status(StatusCode.Internal, "Exception during add reading"), new Metadata
                    {
                        { "Exception", ex.Message }
                    });
                }
            }
            return result;
        }

        public async override Task<Empty> SendDiagnostics(IAsyncStreamReader<ReadingMessage> requestStream, ServerCallContext context)
        {
            var theTask = Task.Run(async () =>
            {
                await foreach (var reading in requestStream.ReadAllAsync())
                {
                    _logger.LogInformation($"Received reading: {reading}");
                }
            });
            await theTask;
            return new Empty();
        }
    }
}
