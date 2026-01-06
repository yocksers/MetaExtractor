using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Tasks;

namespace MetaExtractor.ScheduledTasks
{
    public class MetadataBackupTask : IScheduledTask, IConfigurableScheduledTask
    {
        private readonly ILogger _logger;
        private readonly ILibraryManager _libraryManager;

        public MetadataBackupTask(ILogManager logManager, ILibraryManager libraryManager)
        {
            _logger = logManager.GetLogger(GetType().Name);
            _libraryManager = libraryManager;
        }

        public string Name => "Metadata Backup";

        public string Description => "Backs up NFO files and/or Intro Skip markers to a backup location";

        public string Category => "Library";

        public string Key => "MetadataBackup";

        public bool IsHidden => false;

        public bool IsEnabled => true;

        public bool IsLogged => true;

        public async Task Execute(CancellationToken cancellationToken, IProgress<double> progress)
        {
            if (Plugin.Instance == null)
            {
                _logger.Error("Plugin instance is not initialized");
                return;
            }

            var config = Plugin.Instance.Configuration;
            
            bool backupNfo = config.ScheduledTaskBackupNfo;
            bool backupIntroSkip = config.ScheduledTaskBackupIntroSkips;

            if (!backupNfo && !backupIntroSkip)
            {
                _logger.Info("No backup options enabled. Please enable NFO export and/or Intro Skip backup in settings.");
                return;
            }

            _logger.Info("Starting scheduled metadata backup task");
            _logger.Info($"NFO Backup: {(backupNfo ? "Enabled" : "Disabled")}");
            _logger.Info($"Intro Skip Backup: {(backupIntroSkip ? "Enabled" : "Disabled")}");

            double currentProgress = 0;
            double progressIncrement = 100.0 / (backupNfo && backupIntroSkip ? 2 : 1);

            try
            {
                if (backupNfo && Plugin.MetadataExporter != null)
                {
                    _logger.Info("Starting NFO backup...");
                    progress?.Report(currentProgress);

                    var exportResult = Plugin.MetadataExporter.ExportMetadata(
                        config.EnabledLibraryIds,
                        config,
                        cancellationToken);

                    if (exportResult.Success)
                    {
                        _logger.Info($"NFO backup completed successfully. Items processed: {exportResult.ItemsProcessed}");
                        config.LastExportTime = DateTime.UtcNow;
                        config.LastExportedItemsCount = exportResult.ItemsProcessed;
                    }
                    else
                    {
                        _logger.Error($"NFO backup failed: {exportResult.Message}");
                    }

                    currentProgress += progressIncrement;
                    progress?.Report(currentProgress);
                }

                if (backupIntroSkip && Plugin.IntroSkipBackupService != null)
                {
                    _logger.Info("Starting Intro Skip backup...");
                    progress?.Report(currentProgress);

                    var libraryIds = config.IntroSkipSelectionMode == "individual"
                        ? new List<string>()
                        : config.IntroSkipLibraryIds ?? new List<string>();
                    var seriesIds = config.IntroSkipSelectionMode == "individual"
                        ? config.IntroSkipSelectedSeriesIds ?? new List<string>()
                        : new List<string>();

                    if (libraryIds.Count == 0 && seriesIds.Count == 0)
                    {
                        _logger.Warn("No libraries or series selected for Intro Skip backup");
                    }
                    else
                    {
                        var backupResult = await Plugin.IntroSkipBackupService.BackupIntroSkipData(
                            libraryIds,
                            seriesIds,
                            config.IntroSkipBackupFilePath,
                            config.IntroSkipSavePerEpisode,
                            config.IntroSkipUseCustomFolder,
                            config.IntroSkipCustomFolderPath ?? string.Empty,
                            config.IntroSkipUseTvdbMatching,
                            cancellationToken);

                        if (backupResult.Success)
                        {
                            _logger.Info($"Intro Skip backup completed successfully. Items backed up: {backupResult.ItemsBackedUp}");
                        }
                        else
                        {
                            _logger.Error($"Intro Skip backup failed: {backupResult.Message}");
                        }
                    }

                    currentProgress += progressIncrement;
                    progress?.Report(currentProgress);
                }

                Plugin.Instance.SaveConfiguration();
                progress?.Report(100);
                _logger.Info("Scheduled metadata backup task completed");
            }
            catch (Exception ex)
            {
                _logger.ErrorException("Error during scheduled metadata backup", ex);
                throw;
            }
        }

        public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
        {
            return new[]
            {
                new TaskTriggerInfo
                {
                    Type = TaskTriggerInfo.TriggerDaily,
                    TimeOfDayTicks = TimeSpan.FromHours(2).Ticks
                }
            };
        }
    }
}