﻿using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using Microsoft.EntityFrameworkCore;
using SPO.ColdStorage.Entities;
using SPO.ColdStorage.Entities.Configuration;
using SPO.ColdStorage.Migration.Engine.Migration;
using SPO.ColdStorage.Migration.Engine.Model;
using System.Collections.Concurrent;

namespace SPO.ColdStorage.Migration.Engine
{
    /// <summary>
    /// Listens for new service bus messages for files to migrate to az blob
    /// </summary>
    public class ServiceBusMigrationListener : BaseComponent
    {
        private ServiceBusClient _sbClient;
        private ServiceBusProcessor _receiver;
        private ConcurrentBag<string> _ignoreDownloads = new();     // Files that are in progress of have errored
        private object _lockObj = new object();
        private int _filesProcessedFromQueue = 0;
        const int REPORT_QUEUE_LENGTH_EVERY = 10;

        public ServiceBusMigrationListener(Config config, DebugTracer debugTracer) : base(config, debugTracer)
        {
            _sbClient = new ServiceBusClient(_config.ConnectionStrings.ServiceBus);
            _receiver = _sbClient.CreateProcessor(_config.ServiceBusQueueName, new ServiceBusProcessorOptions
            {
                MaxConcurrentCalls = 10,
                ReceiveMode = ServiceBusReceiveMode.PeekLock
            });
        }

        public async Task ListenForFilesToMigrate()
        {
            try
            {
                // Start an initial DB session to avoid threads configuring context
                using (var db = new SPOColdStorageDbContext(_config))
                {
                    await db.TargetSharePointSites.CountAsync();
                }

                // add handler to process messages
                _receiver.ProcessMessageAsync += MessageHandler;

                // add handler to process any errors
                _receiver.ProcessErrorAsync += ErrorHandler;

                var sbConnectionProps = ServiceBusConnectionStringProperties.Parse(_config.ConnectionStrings.ServiceBus);
                _tracer.TrackTrace($"Listening on service-bus '{sbConnectionProps.Endpoint}' for new files to migrate.");

                // start processing 
                await _receiver.StartProcessingAsync();

                while (true)
                {
                    await Task.Delay(1000);
                }
            }
            finally
            {
                // Calling DisposeAsync on client types is required to ensure that network resources and other unmanaged objects are properly cleaned up.
                await _receiver.DisposeAsync();
                await _sbClient.DisposeAsync();
            }
        }

        // Handle received SB messages
        async Task MessageHandler(ProcessMessageEventArgs args)
        {
            string body = args.Message.Body.ToString();
            var msg = System.Text.Json.JsonSerializer.Deserialize<SharePointFileInfo>(body);
            if (msg != null && msg.IsValidInfo)
            {
                _tracer.TrackTrace($"Started migration for: {msg.ServerRelativeFilePath}");


                // Fire & forget file migration on background thread. Message completed on success.
                await StartFileMigrationAsync(msg, args);

                lock (_lockObj)
                {
                    _filesProcessedFromQueue++;
                    if (REPORT_QUEUE_LENGTH_EVERY % _filesProcessedFromQueue == 0)
                    {
                        _tracer.TrackTrace($"{_filesProcessedFromQueue} files migrated...");
                    }
                }
            }
            else
            {
                _tracer.TrackTrace($"Received unrecognised message: '{body}'. Sending to dead-letter queue.", Microsoft.ApplicationInsights.DataContracts.SeverityLevel.Error);
                await args.DeadLetterMessageAsync(args.Message);
            }
        }

        // Handle any errors when receiving SB messages
        Task ErrorHandler(ProcessErrorEventArgs args)
        {
            _tracer.TrackTrace(args.Exception.Message, Microsoft.ApplicationInsights.DataContracts.SeverityLevel.Error);
            _tracer.TrackException(args.Exception);
            return Task.CompletedTask;
        }

        private async Task StartFileMigrationAsync(SharePointFileInfo sharePointFileToMigrate, ProcessMessageEventArgs args)
        {
            string thisFileRef = sharePointFileToMigrate.FullSharePointUrl;
            if (_ignoreDownloads.Contains(thisFileRef))
            {
                _tracer.TrackTrace($"Already currently importing file '{sharePointFileToMigrate.FullSharePointUrl}'. Won't do it twice this session.",
                    Microsoft.ApplicationInsights.DataContracts.SeverityLevel.Warning);
                return;
            }

            _ignoreDownloads.Add(sharePointFileToMigrate.ServerRelativeFilePath);

            // Begin migration on common class
            using (var sharePointFileMigrator = new SharePointFileMigrator(_config, _tracer))
            {
                // Find/create SP context
                var app = await AuthUtils.GetNewClientApp(_config);

                long migratedFileSize = 0;
                bool success = false;
                try
                {
                    migratedFileSize = await sharePointFileMigrator.MigrateFromSharePointToBlobStorage(sharePointFileToMigrate, app);
                    success = true;
                }
                catch (Exception ex)
                {
                    _tracer.TrackException(ex);
                    _tracer.TrackTrace($"ERROR: Got fatal error '{ex.Message}' importing file '{sharePointFileToMigrate.FullSharePointUrl}'. Will try again",
                        Microsoft.ApplicationInsights.DataContracts.SeverityLevel.Error);

                    await sharePointFileMigrator.SaveErrorForFileMigrationToSql(ex, sharePointFileToMigrate);
                }
                finally
                {
                    // Import done/failed - remove from list of current imports
                    if (!_ignoreDownloads.TryTake(out thisFileRef!))
                    {
                        _tracer.TrackTrace($"Error removing file '{sharePointFileToMigrate.FullSharePointUrl}' from list of concurrent operations. Not sure what to do.",
                            Microsoft.ApplicationInsights.DataContracts.SeverityLevel.Warning);
                    }
                }

                if (success)
                {
                    // Complete the message. messages is deleted from the queue. 
                    try
                    {
                        await args.CompleteMessageAsync(args.Message);
                        _tracer.TrackTrace($"'{sharePointFileToMigrate.ServerRelativeFilePath}' ({migratedFileSize.ToString("N0")} bytes) migrated succesfully.");

                    }
                    catch (ServiceBusException ex)
                    {
                        base._tracer.TrackException(ex);
                        base._tracer.TrackTrace("Couldn't complete SB message: " + ex.Message);
                    }
                    await sharePointFileMigrator.SaveSucessfulFileMigrationToSql(sharePointFileToMigrate);
                }
                else
                {
                    // Leave for processing later
                    await args.AbandonMessageAsync(args.Message);
                }
            }
        }

    }
}
