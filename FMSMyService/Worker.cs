using FMSISSTransactionService.Helpers;
using FMSISSTransactionService.Services;
using FMSMyService.Helpers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace FMSISSTransactionService
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly ServiceRun _serviceRun;
        private readonly IConfiguration _configuration;
        public static bool _IsExceptionOccurred = true;

        private static int _running = 0;
        private readonly int _cycleDelayMs;

        public Worker(ILogger<Worker> logger, ServiceRun serviceRun, IConfiguration configuration)
        {
            _logger = logger;
            _serviceRun = serviceRun;
            _configuration = configuration;
            _cycleDelayMs = _configuration.GetValue<int>("ServiceSettings:CycleDelayMs", 5000);
        }

        public override Task StartAsync(CancellationToken cancellationToken)
        {
            Logger.Simple($"================== SERVICE STARTED at: {DateTimeOffset.Now} ==============");
            return base.StartAsync(cancellationToken);
        }

        public override Task StopAsync(CancellationToken cancellationToken)
        {
            Logger.Simple($"================== SERVICE STOPPED at: {DateTimeOffset.Now} ==============");
            return base.StopAsync(cancellationToken);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                if (Interlocked.CompareExchange(ref _running, 1, 0) != 0)
                {
                    await Task.Delay(500, stoppingToken);
                    continue;
                }

                try
                {
                    await _serviceRun.StartServices();
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    Logger.Info("Service cancellation requested", "FMSISSTransactionService");
                }
                catch (Exception ex)
                {
                    _IsExceptionOccurred = true;
                    Logger.LogException(ex, "ExecuteAsync");
                }
                finally
                {
                    Interlocked.Exchange(ref _running, 0);
                }

                await Task.Delay(_cycleDelayMs, stoppingToken);
            }
        }
    }
}
