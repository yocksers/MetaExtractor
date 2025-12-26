using MetaExtractor.Services;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Services;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MetaExtractor.Api
{
    [Route(ApiRoutes.ExportMetadata, "POST", Summary = "Exports metadata and artwork to media folders.")]
    public class ExportMetadataRequest : IReturn<ExportResult> { }

    [Route(ApiRoutes.GetLibraries, "GET", Summary = "Gets all available libraries.")]
    public class GetLibrariesRequest : IReturn<List<LibraryInfo>> { }

    [Route(ApiRoutes.GetProgress, "GET", Summary = "Gets current export progress.")]
    public class GetProgressRequest : IReturn<ExportProgress> { }

    [Route(ApiRoutes.BackupIntroSkip, "POST", Summary = "Backs up intro skip markers to JSON file.")]
    public class BackupIntroSkipRequest : IReturn<BackupResult> { }

    [Route(ApiRoutes.RestoreIntroSkip, "POST", Summary = "Restores intro skip markers from JSON file.")]
    public class RestoreIntroSkipRequest : IReturn<RestoreResult> 
    { 
        public string FilePath { get; set; } = string.Empty;
    }

    [Route(ApiRoutes.GetSeries, "GET", Summary = "Gets all TV series for intro skip backup selection.")]
    public class GetSeriesRequest : IReturn<List<SeriesInfo>> { }

    [Route(ApiRoutes.GetIntroSkipProgress, "GET", Summary = "Gets current intro skip backup/restore progress.")]
    public class GetIntroSkipProgressRequest : IReturn<IntroSkipProgress> { }

    [Route(ApiRoutes.ExportForMigration, "POST", Summary = "Exports all intro skip markers to a simplified portable format for server migration.")]
    public class ExportForMigrationRequest : IReturn<MigrationExportResult>
    {
        public string ExportPath { get; set; } = string.Empty;
    }

    [Route(ApiRoutes.ImportFromMigration, "POST", Summary = "Imports intro skip markers from a migration export file.")]
    public class ImportFromMigrationRequest : IReturn<MigrationImportResult>
    {
        public string ImportPath { get; set; } = string.Empty;
        public bool OverwriteExisting { get; set; } = false;
    }

    public class MetadataExporterApiService : IService
    {
        private readonly ILibraryManager _libraryManager;

        public MetadataExporterApiService(ILibraryManager libraryManager)
        {
            _libraryManager = libraryManager;
        }

        public ExportResult Post(ExportMetadataRequest request)
        {
            if (Plugin.Instance == null)
            {
                return new ExportResult
                {
                    Success = false,
                    Message = "Plugin instance is not initialized."
                };
            }

            if (Plugin.MetadataExporter == null)
            {
                return new ExportResult
                {
                    Success = false,
                    Message = "Metadata export service is not initialized."
                };
            }

            var config = Plugin.Instance.Configuration;
            
            var result = Plugin.MetadataExporter.ExportMetadata(config.EnabledLibraryIds, config, System.Threading.CancellationToken.None);

            if (result.Success)
            {
                config.LastExportTime = System.DateTime.UtcNow;
                config.LastExportedItemsCount = result.ItemsProcessed;
                Plugin.Instance.SaveConfiguration();
            }

            return result;
        }

        public List<LibraryInfo> Get(GetLibrariesRequest request)
        {
            var libraries = _libraryManager.GetItemList(new MediaBrowser.Controller.Entities.InternalItemsQuery
            {
                IncludeItemTypes = new[] { "CollectionFolder" },
                Recursive = false
            })
            .Select(i => new LibraryInfo
            {
                Id = i.Id.ToString(),
                Name = i.Name,
                CollectionType = i.GetType().Name
            })
            .ToList();

            return libraries;
        }

        public ExportProgress Get(GetProgressRequest request)
        {
            return Plugin.CurrentProgress;
        }

        public async Task<BackupResult> Post(BackupIntroSkipRequest request)
        {
            if (Plugin.Instance == null)
            {
                return new BackupResult
                {
                    Success = false,
                    Message = "Plugin instance is not initialized."
                };
            }

            if (Plugin.IntroSkipBackupService == null)
            {
                return new BackupResult
                {
                    Success = false,
                    Message = "Intro skip backup service is not initialized."
                };
            }

            var config = Plugin.Instance.Configuration;

            if (string.IsNullOrWhiteSpace(config.IntroSkipBackupFilePath))
            {
                return new BackupResult
                {
                    Success = false,
                    Message = "Backup file path is not configured."
                };
            }

            var backupFilePath = config.IntroSkipBackupFilePath;
            var savePerEpisode = config.IntroSkipSavePerEpisode;
            var useCustomFolder = config.IntroSkipUseCustomFolder;
            var customFolderPath = config.IntroSkipCustomFolderPath ?? string.Empty;
            var useTvdbMatching = config.IntroSkipUseTvdbMatching;
            
            var libraryIds = config.IntroSkipSelectionMode == "individual" 
                ? new List<string>() 
                : config.IntroSkipLibraryIds ?? new List<string>();
            var seriesIds = config.IntroSkipSelectionMode == "individual" 
                ? config.IntroSkipSelectedSeriesIds ?? new List<string>()
                : new List<string>();
            
            if (libraryIds.Count == 0 && seriesIds.Count == 0)
            {
                return new BackupResult
                {
                    Success = false,
                    Message = $"No libraries or series selected for backup. Selection mode: {config.IntroSkipSelectionMode}. " +
                             $"Please select at least one library or series in the Intro Skip Backup tab."
                };
            }
                
            return await Plugin.IntroSkipBackupService.BackupIntroSkipData(
                libraryIds, 
                seriesIds, 
                backupFilePath,
                savePerEpisode,
                useCustomFolder,
                customFolderPath,
                useTvdbMatching,
                CancellationToken.None);
        }

        public async Task<RestoreResult> Post(RestoreIntroSkipRequest request)
        {
            if (Plugin.Instance == null)
            {
                return new RestoreResult
                {
                    Success = false,
                    Message = "Plugin instance is not initialized."
                };
            }

            if (Plugin.IntroSkipBackupService == null)
            {
                return new RestoreResult
                {
                    Success = false,
                    Message = "Intro skip backup service is not initialized."
                };
            }

            var config = Plugin.Instance.Configuration;

            var restoreFromScan = config.IntroSkipRestoreFromScan;
            var scanFolderPaths = config.IntroSkipScanFolderPaths ?? new List<string>();
            
            if (restoreFromScan)
            {
                // Restore by scanning folders for .intro.json files
                if (scanFolderPaths.Count == 0)
                {
                    return new RestoreResult
                    {
                        Success = false,
                        Message = "No scan folders configured. Please add folders to scan in settings."
                    };
                }
                
                return await Plugin.IntroSkipBackupService.RestoreIntroSkipData(
                    string.Empty, 
                    true, 
                    scanFolderPaths, 
                    CancellationToken.None);
            }
            else
            {
                // Restore from centralized backup file
            if (string.IsNullOrWhiteSpace(request.FilePath))
            {
                return new RestoreResult
                {
                    Success = false,
                    Message = "No backup file selected."
                };
            }

            if (!System.IO.File.Exists(request.FilePath))
            {
                return new RestoreResult
                {
                    Success = false,
                    Message = "Selected backup file does not exist."
                };
            }

                return await Plugin.IntroSkipBackupService.RestoreIntroSkipData(
                    request.FilePath, 
                    true, 
                    null, 
                    CancellationToken.None);
            }
        }

        public List<SeriesInfo> Get(GetSeriesRequest request)
        {
            var series = _libraryManager.GetItemList(new MediaBrowser.Controller.Entities.InternalItemsQuery
            {
                IncludeItemTypes = new[] { "Series" },
                Recursive = true
            })
            .Cast<MediaBrowser.Controller.Entities.TV.Series>()
            .Select(s => new SeriesInfo
            {
                Id = s.Id.ToString(),
                Name = s.Name,
                Year = s.ProductionYear,
                TvdbId = s.ProviderIds?.ContainsKey("Tvdb") == true ? s.ProviderIds["Tvdb"] : null,
                Path = s.Path
            })
            .OrderBy(s => s.Name)
            .ToList();

            return series;
        }

        public IntroSkipProgress Get(GetIntroSkipProgressRequest request)
        {
            return Plugin.IntroSkipProgress;
        }

        public async Task<MigrationExportResult> Post(ExportForMigrationRequest request)
        {
            if (Plugin.Instance == null || Plugin.IntroSkipBackupService == null)
            {
                return new MigrationExportResult
                {
                    Success = false,
                    Message = "Plugin or service not initialized"
                };
            }

            if (string.IsNullOrWhiteSpace(request.ExportPath))
            {
                return new MigrationExportResult
                {
                    Success = false,
                    Message = "Export path is required"
                };
            }

            return await Plugin.IntroSkipBackupService.ExportForMigration(request.ExportPath, CancellationToken.None);
        }

        public async Task<MigrationImportResult> Post(ImportFromMigrationRequest request)
        {
            if (Plugin.Instance == null || Plugin.IntroSkipBackupService == null)
            {
                return new MigrationImportResult
                {
                    Success = false,
                    Message = "Plugin or service not initialized"
                };
            }

            if (string.IsNullOrWhiteSpace(request.ImportPath))
            {
                return new MigrationImportResult
                {
                    Success = false,
                    Message = "Import path is required"
                };
            }

            return await Plugin.IntroSkipBackupService.ImportFromMigration(request.ImportPath, request.OverwriteExisting, CancellationToken.None);
        }
    }

    public class SeriesInfo
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int? Year { get; set; }
        public string? TvdbId { get; set; }
        public string Path { get; set; } = string.Empty;
    }

    public class LibraryInfo
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string CollectionType { get; set; } = string.Empty;
    }
}
