using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Persistence;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Providers;

namespace MetaExtractor.Services
{
    public class IntroSkipBackupService
    {
        private readonly ILogger _logger;
        private readonly ILibraryManager _libraryManager;
        private readonly IItemRepository _itemRepository;

        public IntroSkipBackupService(ILogger logger, ILibraryManager libraryManager, IItemRepository itemRepository)
        {
            _logger = logger;
            _libraryManager = libraryManager;
            _itemRepository = itemRepository;
        }

        public async Task<BackupResult> BackupIntroSkipData(
            List<string> libraryIds,
            List<string> seriesIds,
            string backupFilePath,
            bool savePerEpisode,
            bool useCustomFolder,
            string customFolderPath,
            bool useTvdbMatching,
            CancellationToken cancellationToken)
        {
            var result = new BackupResult { Success = true };
            var backupData = new List<IntroSkipBackupEntry>();
            int totalItems = 0;
            int itemsWithMarkers = 0;

            try
            {
                    Plugin.IntroSkipProgress.Reset();
                    Plugin.IntroSkipProgress.IsRunning = true;
                    Plugin.IntroSkipProgress.Operation = "Backup";
                    Plugin.IntroSkipProgress.StartTime = DateTime.Now;
                    Plugin.IntroSkipProgress.AddLogEntry("Starting intro skip backup");
                    
                    _logger.Info($"Starting intro skip backup to: {backupFilePath}");
                    _logger.Info($"Backup mode: {(savePerEpisode ? (useCustomFolder ? "Per-Episode (Custom Folder)" : "Per-Episode (Next to Video)") : "Centralized")}");
                    _logger.Info($"theTVDB Matching: {(useTvdbMatching ? "Enabled" : "Disabled")}");
                    _logger.Info($"Series IDs count: {seriesIds?.Count ?? 0}");
                    _logger.Info($"Library IDs count: {libraryIds?.Count ?? 0}");
                    
                    Plugin.IntroSkipProgress.AddLogEntry($"Backup mode: {(savePerEpisode ? "Per-Episode" : "Centralized")}");
                    Plugin.IntroSkipProgress.AddLogEntry($"theTVDB matching: {(useTvdbMatching ? "Enabled" : "Disabled")}");

                if (!savePerEpisode && string.IsNullOrWhiteSpace(backupFilePath))
                {
                    return new BackupResult
                    {
                        Success = false,
                        Message = "Backup file path is not configured."
                    };
                }
                
                if (savePerEpisode && useCustomFolder && string.IsNullOrWhiteSpace(customFolderPath))
                {
                    return new BackupResult
                    {
                        Success = false,
                        Message = "Custom folder path is not configured for per-episode backup mode."
                    };
                }

                if (!savePerEpisode && !backupFilePath.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                {
                    return new BackupResult
                    {
                        Success = false,
                        Message = $"Backup file path must end with .json. Current path: {backupFilePath}"
                    };
                }

                var backupDir = Path.GetDirectoryName(backupFilePath);
                _logger.Info($"Backup directory from path: '{backupDir}'");
                
                if (string.IsNullOrEmpty(backupDir))
                {
                    return new BackupResult
                    {
                        Success = false,
                        Message = $"Invalid backup file path. Could not determine directory from: {backupFilePath}"
                    };
                }
                
                if (Directory.Exists(backupDir))
                {
                    try
                    {
                        var testFile = Path.Combine(backupDir, $".write_test_{Guid.NewGuid()}.tmp");
                        File.WriteAllText(testFile, "test");
                        File.Delete(testFile);
                    }
                    catch (Exception ex)
                    {
                        return new BackupResult
                        {
                            Success = false,
                            Message = $"Backup directory '{backupDir}' is not writable. Please check permissions. Error: {ex.Message}"
                        };
                    }
                }

                IntroSkipBackup? existingBackup = null;
                if (File.Exists(backupFilePath))
                {
                    try
                    {
                        var existingJson = await File.ReadAllTextAsync(backupFilePath, cancellationToken);
                        existingBackup = JsonSerializer.Deserialize<IntroSkipBackup>(existingJson);
                        if (existingBackup?.Entries != null)
                        {
                            _logger.Info($"Loaded existing backup with {existingBackup.Entries.Count} entries");
                            backupData.AddRange(existingBackup.Entries);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Warn($"Could not load existing backup file: {ex.Message}. Will create new backup.");
                    }
                }
                else
                {
                    var backupDirForCreation = Path.GetDirectoryName(backupFilePath);
                    if (!string.IsNullOrEmpty(backupDirForCreation) && !Directory.Exists(backupDirForCreation))
                    {
                        try
                        {
                            _logger.Info($"Creating backup directory: {backupDirForCreation}");
                            Directory.CreateDirectory(backupDirForCreation);
                        }
                        catch (Exception ex)
                        {
                            return new BackupResult
                            {
                                Success = false,
                                Message = $"Failed to create backup directory '{backupDirForCreation}': {ex.Message}"
                            };
                        }
                    }
                }

                List<Episode> episodes;

                if (seriesIds != null && seriesIds.Count > 0)
                {
                    _logger.Info($"Backup mode: Individual series ({seriesIds.Count} series selected)");
                    episodes = new List<Episode>();

                    foreach (var seriesId in seriesIds)
                    {
                        _logger.Info($"Processing series ID: {seriesId}");
                        
                        if (!Guid.TryParse(seriesId, out Guid seriesIdGuid))
                        {
                            _logger.Warn($"Failed to parse series ID: {seriesId}");
                            continue;
                        }

                        var series = _libraryManager.GetItemById(seriesIdGuid);
                        if (series == null)
                        {
                            _logger.Warn($"Series not found for ID: {seriesIdGuid}");
                            continue;
                        }
                        
                        _logger.Info($"Found series: {series.Name} (ID: {seriesIdGuid}, InternalId: {series.InternalId})");

                        var episodeQuery = new InternalItemsQuery
                        {
                            IncludeItemTypes = new[] { typeof(Episode).Name },
                            AncestorIds = new[] { series.InternalId },
                            Recursive = true,
                            IsVirtualItem = false
                        };

                        var seriesEpisodes = _libraryManager.GetItemList(episodeQuery).Cast<Episode>().ToList();
                        _logger.Info($"Found {seriesEpisodes.Count} episodes for series: {series.Name}");
                        episodes.AddRange(seriesEpisodes);
                    }
                }
                else
                {
                    _logger.Info($"Backup mode: Library ({(libraryIds?.Count ?? 0)} libraries selected)");
                    var query = new InternalItemsQuery
                    {
                        IncludeItemTypes = new[] { typeof(Episode).Name },
                        Recursive = true,
                        IsVirtualItem = false
                    };

                    if (libraryIds != null && libraryIds.Count > 0)
                    {
                        var libraryInternalIds = new List<long>();
                        foreach (var libId in libraryIds)
                        {
                            if (Guid.TryParse(libId, out Guid libGuid))
                            {
                                var library = _libraryManager.GetItemById(libGuid);
                                if (library != null)
                                {
                                    libraryInternalIds.Add(library.InternalId);
                                    _logger.Info($"Added library: {library.Name} (InternalId: {library.InternalId})");
                                }
                            }
                        }
                        query.AncestorIds = libraryInternalIds.ToArray();
                    }

                    episodes = _libraryManager.GetItemList(query).Cast<Episode>().ToList();
                }
                
                totalItems = episodes.Count;
                Plugin.IntroSkipProgress.TotalItems = totalItems;

                _logger.Info($"Found {totalItems} episodes to process");
                Plugin.IntroSkipProgress.AddLogEntry($"Found {totalItems} episodes to process");
                
                if (totalItems == 0)
                {
                    _logger.Warn("No episodes found to backup. Check your library/series selection.");
                    Plugin.IntroSkipProgress.AddLogEntry("WARNING: No episodes found to backup");
                }

                foreach (var episode in episodes)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    Plugin.IntroSkipProgress.ProcessedItems++;
                    Plugin.IntroSkipProgress.CurrentSeries = episode.Series?.Name ?? "Unknown";
                    Plugin.IntroSkipProgress.CurrentItem = $"S{episode.ParentIndexNumber:D2}E{episode.IndexNumber:D2} - {episode.Name}";

                    var chapters = _itemRepository.GetChapters(episode);
                    if (chapters == null || chapters.Count == 0)
                    {
                        _logger.Debug($"No chapters for: {episode.Series?.Name} - {episode.Name}");
                        continue;
                    }

                    _logger.Debug($"Found {chapters.Count} chapters in: {episode.Series?.Name} - {episode.Name}");
                    var markerChapters = new List<ChapterMarkerInfo>();

                    foreach (var chapter in chapters)
                    {
                        try
                        {
                            var markerType = GetMarkerType(chapter);
                            
                            if (markerType == "IntroStart" || markerType == "IntroEnd")
                            {
                                markerChapters.Add(new ChapterMarkerInfo
                                {
                                    Name = chapter.Name,
                                    StartPositionTicks = chapter.StartPositionTicks,
                                    MarkerType = markerType
                                });
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.Debug($"Error reading marker type for chapter in {episode.Name}: {ex.Message}");
                        }
                    }

                    if (markerChapters.Count > 0)
                    {
                        var series = episode.Series;
                        var backupEntry = new IntroSkipBackupEntry
                        {
                            SeriesName = series?.Name ?? "Unknown",
                            SeriesId = episode.SeriesId.ToString(),
                            SeasonNumber = episode.ParentIndexNumber ?? 0,
                            EpisodeNumber = episode.IndexNumber ?? 0,
                            EpisodeName = episode.Name,
                            EpisodeId = episode.Id.ToString(),
                            FilePath = episode.Path,
                            Markers = markerChapters
                        };

                        if (series != null && series.ProviderIds != null)
                        {
                            series.ProviderIds.TryGetValue("Tvdb", out var tvdbId);
                            series.ProviderIds.TryGetValue("Tmdb", out var tmdbId);
                            series.ProviderIds.TryGetValue("Imdb", out var imdbId);
                            backupEntry.TvdbId = tvdbId;
                            backupEntry.TmdbId = tmdbId;
                            backupEntry.ImdbId = imdbId;
                        }
                        
                        if (useTvdbMatching && episode.ProviderIds != null)
                        {
                            episode.ProviderIds.TryGetValue("Tvdb", out var tvdbEpisodeId);
                            if (!string.IsNullOrEmpty(tvdbEpisodeId))
                            {
                                backupEntry.TvdbEpisodeId = tvdbEpisodeId;
                                _logger.Debug($"Added theTVDB Episode ID {tvdbEpisodeId} for {episode.Name}");
                            }
                        }

                        if (savePerEpisode)
                        {
                            if (!string.IsNullOrEmpty(episode.Path) && File.Exists(episode.Path))
                            {
                                try
                                {
                                    string introJsonPath;
                                    var episodeFileName = Path.GetFileNameWithoutExtension(episode.Path);
                                    
                                    if (useCustomFolder)
                                    {
                                        if (!Directory.Exists(customFolderPath))
                                        {
                                            Directory.CreateDirectory(customFolderPath);
                                            _logger.Info($"Created custom backup folder: {customFolderPath}");
                                        }
                                        
                                        var seriesName = SanitizeFileName(series?.Name ?? "Unknown");
                                        var seasonFolder = $"Season {episode.ParentIndexNumber ?? 0:D2}";
                                        var seriesFolder = Path.Combine(customFolderPath, seriesName);
                                        var seasonPath = Path.Combine(seriesFolder, seasonFolder);
                                        
                                        if (!Directory.Exists(seasonPath))
                                        {
                                            Directory.CreateDirectory(seasonPath);
                                        }
                                        
                                        introJsonPath = Path.Combine(seasonPath, $"{episodeFileName}.intro.json");
                                    }
                                    else
                                    {
                                        var episodeDir = Path.GetDirectoryName(episode.Path);
                                        if (string.IsNullOrEmpty(episodeDir))
                                        {
                                            _logger.Warn($"Could not determine directory for episode: {episode.Path}");
                                            continue;
                                        }
                                        introJsonPath = Path.Combine(episodeDir, $"{episodeFileName}.intro.json");
                                    }
                                    
                                    var jsonOptions = new JsonSerializerOptions
                                    {
                                        WriteIndented = true,
                                        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                                    };
                                    
                                    var json = JsonSerializer.Serialize(backupEntry, jsonOptions);
                                    await File.WriteAllTextAsync(introJsonPath, json, cancellationToken);
                                    
                                    _logger.Info($"Saved intro data to: {introJsonPath}");
                                    itemsWithMarkers++;
                                    Plugin.IntroSkipProgress.SuccessItems++;
                                    
                                    if (itemsWithMarkers % 10 == 0)
                                    {
                                        Plugin.IntroSkipProgress.AddLogEntry($"Backed up {itemsWithMarkers} episodes with intro markers");
                                    }
                                }
                                catch (Exception ex)
                                {
                                    _logger.Warn($"Failed to save intro data for {episode.Name}: {ex.Message}");
                                }
                            }
                        }
                        else
                        {
                            backupData.RemoveAll(e => e.EpisodeId == backupEntry.EpisodeId || 
                                (e.FilePath == backupEntry.FilePath && !string.IsNullOrEmpty(backupEntry.FilePath)));
                            
                            backupData.Add(backupEntry);
                            itemsWithMarkers++;
                            Plugin.IntroSkipProgress.SuccessItems++;
                            
                            if (itemsWithMarkers % 10 == 0)
                            {
                                Plugin.IntroSkipProgress.AddLogEntry($"Backed up {itemsWithMarkers} episodes with intro markers");
                            }
                        }
                    }
                }

                if (!savePerEpisode)
                {
                var jsonOptions = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                };

                var backupContainer = new IntroSkipBackup
                {
                    Version = "1.0",
                    BackupDate = DateTime.UtcNow,
                    TotalEpisodes = totalItems,
                    EpisodesWithMarkers = itemsWithMarkers,
                    Entries = backupData
                };

                var json = JsonSerializer.Serialize(backupContainer, jsonOptions);
                
                try
                {
                    await File.WriteAllTextAsync(backupFilePath, json, cancellationToken);
                    _logger.Info($"Successfully wrote backup file: {backupFilePath}");
                    Plugin.IntroSkipProgress.AddLogEntry($"Saved centralized backup to: {backupFilePath}");
                    
                    await ValidateBackup(backupFilePath, itemsWithMarkers);
                }
                catch (UnauthorizedAccessException ex)
                {
                    Plugin.IntroSkipProgress.AddLogEntry($"ERROR: Access denied - {ex.Message}");
                    return new BackupResult
                    {
                        Success = false,
                        Message = $"Access denied to backup file '{backupFilePath}'. Please check file permissions or choose a different location. Error: {ex.Message}"
                    };
                }
                catch (IOException ex)
                {
                    Plugin.IntroSkipProgress.AddLogEntry($"ERROR: Failed to write file - {ex.Message}");
                    return new BackupResult
                    {
                        Success = false,
                        Message = $"Failed to write backup file '{backupFilePath}'. Error: {ex.Message}"
                    };
                }
                } // End if (!savePerEpisode)

                result.TotalItems = totalItems;
                result.ItemsBackedUp = itemsWithMarkers;
                result.Message = savePerEpisode 
                    ? $"Successfully backed up {itemsWithMarkers} episodes with intro skip markers as individual .intro.json files"
                    : $"Successfully backed up {itemsWithMarkers} episodes with intro skip markers out of {totalItems} total episodes";
                
                _logger.Info(result.Message);
                Plugin.IntroSkipProgress.AddLogEntry(result.Message);
                Plugin.IntroSkipProgress.AddLogEntry($"Backup completed successfully");
                
                var validationErrors = Plugin.IntroSkipProgress.ValidationErrors;
                if (validationErrors.Count > 0)
                {
                    result.Message += $" (with {validationErrors.Count} validation warnings)";
                }
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = $"Backup failed: {ex.Message}";
                _logger.ErrorException("Error during intro skip backup", ex);
                Plugin.IntroSkipProgress.AddLogEntry($"ERROR: {ex.Message}");
            }
            finally
            {
                Plugin.IntroSkipProgress.IsRunning = false;
            }

            return result;
        }

        private async Task ValidateBackup(string backupFilePath, int expectedCount)
        {
            try
            {
                Plugin.IntroSkipProgress.AddLogEntry("Validating backup integrity...");
                
                if (!File.Exists(backupFilePath))
                {
                    var error = "Backup file not found after save";
                    Plugin.IntroSkipProgress.AddValidationError(error);
                    _logger.Error(error);
                    return;
                }

                var fileInfo = new FileInfo(backupFilePath);
                if (fileInfo.Length == 0)
                {
                    var error = "Backup file is empty";
                    Plugin.IntroSkipProgress.AddValidationError(error);
                    _logger.Error(error);
                    return;
                }

                var json = await File.ReadAllTextAsync(backupFilePath);
                var backup = JsonSerializer.Deserialize<IntroSkipBackup>(json);
                
                if (backup == null)
                {
                    var error = "Failed to deserialize backup file";
                    Plugin.IntroSkipProgress.AddValidationError(error);
                    _logger.Error(error);
                    return;
                }

                if (backup.Entries.Count != expectedCount)
                {
                    var warning = $"Entry count mismatch: expected {expectedCount}, found {backup.Entries.Count}";
                    Plugin.IntroSkipProgress.AddValidationError(warning);
                    _logger.Warn(warning);
                }

                int entriesWithoutMarkers = 0;
                int entriesWithoutIds = 0;
                foreach (var entry in backup.Entries)
                {
                    if (entry.Markers == null || entry.Markers.Count == 0)
                    {
                        entriesWithoutMarkers++;
                    }
                    
                    if (string.IsNullOrEmpty(entry.TvdbEpisodeId) && 
                        string.IsNullOrEmpty(entry.TvdbId) && 
                        string.IsNullOrEmpty(entry.TmdbId))
                    {
                        entriesWithoutIds++;
                    }
                }

                if (entriesWithoutMarkers > 0)
                {
                    var warning = $"{entriesWithoutMarkers} entries have no markers";
                    Plugin.IntroSkipProgress.AddValidationError(warning);
                    _logger.Warn(warning);
                }

                if (entriesWithoutIds > 0)
                {
                    var warning = $"{entriesWithoutIds} entries missing provider IDs (may affect portability)";
                    Plugin.IntroSkipProgress.AddValidationError(warning);
                    _logger.Warn(warning);
                }

                Plugin.IntroSkipProgress.AddLogEntry($"Validation complete: {backup.Entries.Count} entries, {fileInfo.Length / 1024}KB");
                _logger.Info($"Backup validation: {backup.Entries.Count} entries validated, file size {fileInfo.Length / 1024}KB");
            }
            catch (Exception ex)
            {
                var error = $"Validation error: {ex.Message}";
                Plugin.IntroSkipProgress.AddValidationError(error);
                _logger.ErrorException("Error validating backup", ex);
            }
        }

        private string? GetMarkerType(ChapterInfo chapter)
        {
            try
            {
                var markerTypeProp = chapter.GetType().GetProperty("MarkerType");
                if (markerTypeProp != null && markerTypeProp.CanRead)
                {
                    var value = markerTypeProp.GetValue(chapter);
                    return value?.ToString();
                }
            }
            catch (Exception ex)
            {
                _logger.Debug($"Error reading MarkerType property: {ex.Message}");
            }
            return null;
        }

        private string SanitizeFileName(string fileName)
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            var sanitized = new string(fileName.Select(c => invalidChars.Contains(c) ? '_' : c).ToArray());
            return sanitized;
        }

        private bool SetMarkerType(ChapterInfo chapter, MarkerType markerType)
        {
            try
            {
                var markerTypeProp = chapter.GetType().GetProperty("MarkerType");
                if (markerTypeProp != null && markerTypeProp.CanWrite)
                {
                    markerTypeProp.SetValue(chapter, markerType);
                    return true;
                }
                else
                {
                    _logger.Warn($"MarkerType property not found or not writable on ChapterInfo");
                }
            }
            catch (Exception ex)
            {
                _logger.ErrorException($"Error setting MarkerType property", ex);
            }
            return false;
        }

        public async Task<RestoreResult> RestoreIntroSkipData(string backupFilePath, bool overwriteExisting, CancellationToken cancellationToken = default)
        {
            return await RestoreIntroSkipData(backupFilePath, overwriteExisting, null, cancellationToken);
        }

        public async Task<RestoreResult> RestoreIntroSkipData(
            string backupFilePath, 
            bool overwriteExisting, 
            List<string>? scanFolderPaths, 
            CancellationToken cancellationToken = default)
        {
            var result = new RestoreResult { Success = true };
            int restored = 0;
            int skipped = 0;
            int notFound = 0;

            try
            {
                Plugin.IntroSkipProgress.Reset();
                Plugin.IntroSkipProgress.IsRunning = true;
                Plugin.IntroSkipProgress.Operation = "Restore";
                Plugin.IntroSkipProgress.StartTime = DateTime.Now;
                Plugin.IntroSkipProgress.AddLogEntry("Starting intro skip restore");
                
                _logger.Info($"Starting intro skip restore");
                
                List<IntroSkipBackupEntry> entries = new List<IntroSkipBackupEntry>();
                
                if (scanFolderPaths != null && scanFolderPaths.Count > 0)
                {
                    _logger.Info($"Restore mode: Scanning folders for .intro.json files");
                    _logger.Info($"Scanning {scanFolderPaths.Count} root folders");
                    
                    foreach (var folderPath in scanFolderPaths)
                    {
                        if (!Directory.Exists(folderPath))
                        {
                            _logger.Warn($"Scan folder not found: {folderPath}");
                            continue;
                        }
                        
                        try
                        {
                            var introJsonFiles = Directory.GetFiles(folderPath, "*.intro.json", SearchOption.AllDirectories);
                            _logger.Info($"Found {introJsonFiles.Length} .intro.json files in: {folderPath}");
                            
                            foreach (var introJsonFile in introJsonFiles)
                            {
                                try
                                {
                                    var json = await File.ReadAllTextAsync(introJsonFile, cancellationToken);
                                    var entry = JsonSerializer.Deserialize<IntroSkipBackupEntry>(json);
                                    
                                    if (entry != null && entry.Markers != null && entry.Markers.Count > 0)
                                    {
                                        entries.Add(entry);
                                        _logger.Debug($"Loaded intro data from: {introJsonFile}");
                                    }
                                }
                                catch (Exception ex)
                                {
                                    _logger.Warn($"Failed to parse intro file {introJsonFile}: {ex.Message}");
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.Warn($"Error scanning folder {folderPath}: {ex.Message}");
                        }
                    }
                    
                    _logger.Info($"Loaded {entries.Count} entries from scanned folders");
                }
                else
                {
                    _logger.Info($"Restore mode: Centralized backup file");
                    _logger.Info($"Restore from: {backupFilePath}");

                if (!File.Exists(backupFilePath))
                {
                    result.Success = false;
                    result.Message = "Backup file not found";
                    return result;
                }

                var json = await File.ReadAllTextAsync(backupFilePath, cancellationToken);
                var backup = JsonSerializer.Deserialize<IntroSkipBackup>(json);

                if (backup == null || backup.Entries == null)
                {
                    result.Success = false;
                    result.Message = "Invalid backup file format";
                    return result;
                }

                _logger.Info($"Found {backup.Entries.Count} entries in backup from {backup.BackupDate:yyyy-MM-dd HH:mm:ss}");
                    entries = backup.Entries;
                }

                Plugin.IntroSkipProgress.TotalItems = entries.Count;
                Plugin.IntroSkipProgress.AddLogEntry($"Processing {entries.Count} entries from backup");

                foreach (var entry in entries)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    Plugin.IntroSkipProgress.ProcessedItems++;
                    Plugin.IntroSkipProgress.CurrentSeries = entry.SeriesName;
                    Plugin.IntroSkipProgress.CurrentItem = $"S{entry.SeasonNumber:D2}E{entry.EpisodeNumber:D2} - {entry.EpisodeName}";

                    Episode? episode = null;

                    if (!string.IsNullOrEmpty(entry.TvdbEpisodeId))
                    {
                        var query = new InternalItemsQuery
                        {
                            IncludeItemTypes = new[] { typeof(Episode).Name },
                            Recursive = true,
                            IsVirtualItem = false
                        };
                        
                        var allEpisodes = _libraryManager.GetItemList(query).Cast<Episode>();
                        foreach (var ep in allEpisodes)
                        {
                            if (ep.ProviderIds != null && ep.ProviderIds.TryGetValue("Tvdb", out var epTvdbId))
                            {
                                if (epTvdbId == entry.TvdbEpisodeId)
                                {
                                    episode = ep;
                                    _logger.Debug($"Matched episode by theTVDB Episode ID: {entry.TvdbEpisodeId}");
                                    break;
                                }
                            }
                        }
                    }

                    if (episode == null && long.TryParse(entry.EpisodeId, out long episodeId))
                    {
                        episode = _libraryManager.GetItemById(episodeId) as Episode;
                    }

                    if (episode == null && !string.IsNullOrEmpty(entry.FilePath))
                    {
                        episode = _libraryManager.FindByPath(entry.FilePath, false) as Episode;
                    }

                    if (episode == null)
                    {
                        var query = new InternalItemsQuery
                        {
                            IncludeItemTypes = new[] { typeof(Episode).Name },
                            ParentIndexNumber = entry.SeasonNumber,
                            IndexNumber = entry.EpisodeNumber,
                            Recursive = true
                        };

                        var matchingEpisodes = _libraryManager.GetItemList(query).Cast<Episode>();

                        foreach (var ep in matchingEpisodes)
                        {
                            var series = ep.Series;
                            if (series != null && series.ProviderIds != null)
                            {
                                series.ProviderIds.TryGetValue("Tvdb", out var seriesTvdbId);
                                series.ProviderIds.TryGetValue("Tmdb", out var seriesTmdbId);
                                series.ProviderIds.TryGetValue("Imdb", out var seriesImdbId);
                                
                                var tvdbMatch = !string.IsNullOrEmpty(entry.TvdbId) && seriesTvdbId == entry.TvdbId;
                                var tmdbMatch = !string.IsNullOrEmpty(entry.TmdbId) && seriesTmdbId == entry.TmdbId;
                                var imdbMatch = !string.IsNullOrEmpty(entry.ImdbId) && seriesImdbId == entry.ImdbId;

                                if (tvdbMatch || tmdbMatch || imdbMatch)
                                {
                                    episode = ep;
                                    break;
                                }
                            }
                        }
                    }

                    if (episode == null)
                    {
                        _logger.Debug($"Episode not found: {entry.SeriesName} S{entry.SeasonNumber:D2}E{entry.EpisodeNumber:D2}");
                        notFound++;
                        Plugin.IntroSkipProgress.FailedItems++;
                        continue;
                    }

                    if (!overwriteExisting)
                    {
                        var existingChapters = _itemRepository.GetChapters(episode);
                        bool hasMarkers = false;

                        if (existingChapters != null)
                        {
                            foreach (var chapter in existingChapters)
                            {
                                var markerType = GetMarkerType(chapter);
                                if (!string.IsNullOrEmpty(markerType) && markerType != "None")
                                {
                                    hasMarkers = true;
                                    break;
                                }
                            }
                        }

                        if (hasMarkers)
                        {
                            _logger.Debug($"Skipping {episode.Name} - already has intro markers");
                            skipped++;
                            Plugin.IntroSkipProgress.SkippedItems++;
                            continue;
                        }
                    }

                    var chapters = _itemRepository.GetChapters(episode)?.ToList() ?? new List<ChapterInfo>();

                    foreach (var marker in entry.Markers)
                    {
                        _logger.Info($"Restoring marker: {marker.Name} ({marker.MarkerType}) at {TimeSpan.FromTicks(marker.StartPositionTicks)}");
                        
                        if (!Enum.TryParse<MarkerType>(marker.MarkerType, out var markerTypeEnum))
                        {
                            _logger.Warn($"Invalid MarkerType: {marker.MarkerType}, skipping");
                            continue;
                        }
                        
                        var existingChapter = chapters.FirstOrDefault(c => 
                            Math.Abs(c.StartPositionTicks - marker.StartPositionTicks) < TimeSpan.FromSeconds(1).Ticks);

                        if (existingChapter != null)
                        {
                            existingChapter.Name = marker.Name;
                            if (SetMarkerType(existingChapter, markerTypeEnum))
                            {
                                _logger.Info($"Updated existing chapter '{existingChapter.Name}' with MarkerType: {markerTypeEnum}");
                            }
                            else
                            {
                                _logger.Warn($"Failed to set MarkerType for existing chapter '{existingChapter.Name}'");
                            }
                        }
                        else
                        {
                            var newChapter = new ChapterInfo
                            {
                                Name = marker.Name,
                                StartPositionTicks = marker.StartPositionTicks
                            };
                            
                            if (SetMarkerType(newChapter, markerTypeEnum))
                            {
                                chapters.Add(newChapter);
                                _logger.Info($"Created new chapter '{newChapter.Name}' with MarkerType: {markerTypeEnum}");
                            }
                            else
                            {
                                _logger.Warn($"Failed to create chapter with MarkerType, skipping '{marker.Name}'");
                            }
                        }
                    }

                    chapters = chapters.OrderBy(c => c.StartPositionTicks).ToList();

                    _itemRepository.SaveChapters(episode.InternalId, chapters);
                    
                    restored++;
                    Plugin.IntroSkipProgress.SuccessItems++;
                    
                    if (restored % 10 == 0)
                    {
                        Plugin.IntroSkipProgress.AddLogEntry($"Restored {restored} episodes");
                    }
                    
                    _logger.Info($"Restored {entry.Markers.Count} markers for: {episode.Series?.Name} - S{episode.ParentIndexNumber:D2}E{episode.IndexNumber:D2} - {episode.Name}");
                }

                result.ItemsRestored = restored;
                result.ItemsSkipped = skipped;
                result.ItemsNotFound = notFound;
                result.Message = $"Restore complete: {restored} restored, {skipped} skipped, {notFound} not found";
                
                _logger.Info(result.Message);
                Plugin.IntroSkipProgress.AddLogEntry(result.Message);
                Plugin.IntroSkipProgress.AddLogEntry("Restore completed successfully");
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = $"Restore failed: {ex.Message}";
                _logger.ErrorException("Error during intro skip restore", ex);
                Plugin.IntroSkipProgress.AddLogEntry($"ERROR: {ex.Message}");
            }
            finally
            {
                Plugin.IntroSkipProgress.IsRunning = false;
            }

            return result;
        }
        
        public async Task<MigrationExportResult> ExportForMigration(string exportPath, CancellationToken cancellationToken = default)
        {
            var result = new MigrationExportResult { Success = true };
            
            try
            {
                Plugin.IntroSkipProgress.Reset();
                Plugin.IntroSkipProgress.IsRunning = true;
                Plugin.IntroSkipProgress.Operation = "Export for Migration";
                Plugin.IntroSkipProgress.StartTime = DateTime.Now;
                Plugin.IntroSkipProgress.AddLogEntry("Starting migration export - scanning all episodes");
                
                _logger.Info("Starting migration export");
                
                var query = new InternalItemsQuery
                {
                    IncludeItemTypes = new[] { typeof(Episode).Name },
                    Recursive = true,
                    IsVirtualItem = false
                };
                
                var episodes = _libraryManager.GetItemList(query).Cast<Episode>().ToList();
                var exportData = new List<MigrationEntry>();
                
                Plugin.IntroSkipProgress.TotalItems = episodes.Count;
                Plugin.IntroSkipProgress.AddLogEntry($"Scanning {episodes.Count} episodes");
                
                foreach (var episode in episodes)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    Plugin.IntroSkipProgress.ProcessedItems++;
                    Plugin.IntroSkipProgress.CurrentSeries = episode.Series?.Name ?? "Unknown";
                    Plugin.IntroSkipProgress.CurrentItem = $"S{episode.ParentIndexNumber:D2}E{episode.IndexNumber:D2} - {episode.Name}";
                    
                    var chapters = _itemRepository.GetChapters(episode);
                    if (chapters == null || chapters.Count == 0) continue;
                    
                    var introMarkers = new List<ChapterMarkerInfo>();
                    foreach (var chapter in chapters)
                    {
                        var markerType = GetMarkerType(chapter);
                        if (markerType == "IntroStart" || markerType == "IntroEnd")
                        {
                            introMarkers.Add(new ChapterMarkerInfo
                            {
                                Name = chapter.Name,
                                StartPositionTicks = chapter.StartPositionTicks,
                                MarkerType = markerType
                            });
                        }
                    }
                    
                    if (introMarkers.Count > 0)
                    {
                        var series = episode.Series;
                        var migrationEntry = new MigrationEntry
                        {
                            SeriesName = series?.Name ?? "Unknown",
                            TvdbId = series?.ProviderIds?.ContainsKey("Tvdb") == true ? series.ProviderIds["Tvdb"] : null,
                            TmdbId = series?.ProviderIds?.ContainsKey("Tmdb") == true ? series.ProviderIds["Tmdb"] : null,
                            SeasonNumber = episode.ParentIndexNumber ?? 0,
                            EpisodeNumber = episode.IndexNumber ?? 0,
                            EpisodeName = episode.Name,
                            TvdbEpisodeId = episode.ProviderIds?.ContainsKey("Tvdb") == true ? episode.ProviderIds["Tvdb"] : null,
                            IntroStartTicks = introMarkers.FirstOrDefault(m => m.MarkerType == "IntroStart")?.StartPositionTicks ?? 0,
                            IntroEndTicks = introMarkers.FirstOrDefault(m => m.MarkerType == "IntroEnd")?.StartPositionTicks ?? 0
                        };
                        
                        exportData.Add(migrationEntry);
                        Plugin.IntroSkipProgress.SuccessItems++;
                        
                        if (exportData.Count % 50 == 0)
                        {
                            Plugin.IntroSkipProgress.AddLogEntry($"Found {exportData.Count} episodes with intro markers");
                        }
                    }
                }
                
                var export = new MigrationExport
                {
                    ExportVersion = "1.0",
                    ExportDate = DateTime.UtcNow,
                    Description = "Portable intro skip data for migration between Emby servers",
                    TotalSeries = exportData.GroupBy(e => e.TvdbId ?? e.SeriesName).Count(),
                    TotalEpisodes = exportData.Count,
                    Entries = exportData
                };
                
                var jsonOptions = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                };
                
                var json = JsonSerializer.Serialize(export, jsonOptions);
                await File.WriteAllTextAsync(exportPath, json, cancellationToken);
                
                result.TotalEpisodesExported = exportData.Count;
                result.Message = $"Successfully exported {exportData.Count} episodes with intro markers from {export.TotalSeries} series";
                
                Plugin.IntroSkipProgress.AddLogEntry(result.Message);
                Plugin.IntroSkipProgress.AddLogEntry($"Export saved to: {exportPath}");
                _logger.Info(result.Message);
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = $"Migration export failed: {ex.Message}";
                _logger.ErrorException("Error during migration export", ex);
                Plugin.IntroSkipProgress.AddLogEntry($"ERROR: {ex.Message}");
            }
            finally
            {
                Plugin.IntroSkipProgress.IsRunning = false;
            }
            
            return result;
        }
        
        public async Task<MigrationImportResult> ImportFromMigration(string importPath, bool overwriteExisting, CancellationToken cancellationToken = default)
        {
            var result = new MigrationImportResult { Success = true };
            int imported = 0;
            int skipped = 0;
            int notFound = 0;
            
            try
            {
                Plugin.IntroSkipProgress.Reset();
                Plugin.IntroSkipProgress.IsRunning = true;
                Plugin.IntroSkipProgress.Operation = "Import from Migration";
                Plugin.IntroSkipProgress.StartTime = DateTime.Now;
                Plugin.IntroSkipProgress.AddLogEntry("Starting migration import");
                
                _logger.Info("Starting migration import");
                
                if (!File.Exists(importPath))
                {
                    result.Success = false;
                    result.Message = "Migration file not found";
                    Plugin.IntroSkipProgress.AddLogEntry("ERROR: Migration file not found");
                    return result;
                }
                
                var json = await File.ReadAllTextAsync(importPath, cancellationToken);
                var import = JsonSerializer.Deserialize<MigrationExport>(json);
                
                if (import == null || import.Entries == null)
                {
                    result.Success = false;
                    result.Message = "Invalid migration file format";
                    Plugin.IntroSkipProgress.AddLogEntry("ERROR: Invalid migration file format");
                    return result;
                }
                
                Plugin.IntroSkipProgress.TotalItems = import.Entries.Count;
                Plugin.IntroSkipProgress.AddLogEntry($"Importing {import.Entries.Count} episodes from {import.TotalSeries} series");
                Plugin.IntroSkipProgress.AddLogEntry($"Export date: {import.ExportDate:yyyy-MM-dd HH:mm}");
                _logger.Info($"Importing {import.Entries.Count} episodes from migration file dated {import.ExportDate}");
                
                foreach (var entry in import.Entries)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    Plugin.IntroSkipProgress.ProcessedItems++;
                    Plugin.IntroSkipProgress.CurrentSeries = entry.SeriesName;
                    Plugin.IntroSkipProgress.CurrentItem = $"S{entry.SeasonNumber:D2}E{entry.EpisodeNumber:D2} - {entry.EpisodeName}";
                    
                    Episode? episode = null;
                    
                    if (!string.IsNullOrEmpty(entry.TvdbEpisodeId))
                    {
                        var allEpisodes = _libraryManager.GetItemList(new InternalItemsQuery
                        {
                            IncludeItemTypes = new[] { typeof(Episode).Name },
                            Recursive = true
                        }).Cast<Episode>();
                        
                        foreach (var ep in allEpisodes)
                        {
                            if (ep.ProviderIds?.TryGetValue("Tvdb", out var epTvdbId) == true && epTvdbId == entry.TvdbEpisodeId)
                            {
                                episode = ep;
                                break;
                            }
                        }
                    }
                    
                    if (episode == null && !string.IsNullOrEmpty(entry.TvdbId))
                    {
                        var episodeQuery = new InternalItemsQuery
                        {
                            IncludeItemTypes = new[] { typeof(Episode).Name },
                            ParentIndexNumber = entry.SeasonNumber,
                            IndexNumber = entry.EpisodeNumber,
                            Recursive = true
                        };
                        
                        var episodes = _libraryManager.GetItemList(episodeQuery).Cast<Episode>();
                        foreach (var ep in episodes)
                        {
                            var series = ep.Series;
                            if (series?.ProviderIds?.TryGetValue("Tvdb", out var seriesTvdbId) == true && seriesTvdbId == entry.TvdbId)
                            {
                                episode = ep;
                                break;
                            }
                        }
                    }
                    
                    if (episode == null)
                    {
                        notFound++;
                        Plugin.IntroSkipProgress.FailedItems++;
                        continue;
                    }
                    
                    if (!overwriteExisting)
                    {
                        var existingChapters = _itemRepository.GetChapters(episode);
                        bool hasMarkers = existingChapters?.Any(c =>
                        {
                            var mt = GetMarkerType(c);
                            return !string.IsNullOrEmpty(mt) && mt != "None";
                        }) == true;
                        
                        if (hasMarkers)
                        {
                            skipped++;
                            Plugin.IntroSkipProgress.SkippedItems++;
                            continue;
                        }
                    }
                    
                    var chapters = _itemRepository.GetChapters(episode)?.ToList() ?? new List<ChapterInfo>();
                    
                    if (entry.IntroStartTicks > 0)
                    {
                        var introStart = new ChapterInfo
                        {
                            Name = "Intro Start",
                            StartPositionTicks = entry.IntroStartTicks
                        };
                        SetMarkerType(introStart, MarkerType.IntroStart);
                        chapters.Add(introStart);
                    }
                    
                    if (entry.IntroEndTicks > 0)
                    {
                        var introEnd = new ChapterInfo
                        {
                            Name = "Intro End",
                            StartPositionTicks = entry.IntroEndTicks
                        };
                        SetMarkerType(introEnd, MarkerType.IntroEnd);
                        chapters.Add(introEnd);
                    }
                    
                    chapters = chapters.OrderBy(c => c.StartPositionTicks).ToList();
                    _itemRepository.SaveChapters(episode.InternalId, chapters);
                    
                    imported++;
                    Plugin.IntroSkipProgress.SuccessItems++;
                    
                    if (imported % 10 == 0)
                    {
                        Plugin.IntroSkipProgress.AddLogEntry($"Imported {imported} episodes");
                    }
                }
                
                result.ItemsImported = imported;
                result.ItemsSkipped = skipped;
                result.ItemsNotFound = notFound;
                result.Message = $"Migration import complete: {imported} imported, {skipped} skipped, {notFound} not found";
                
                Plugin.IntroSkipProgress.AddLogEntry(result.Message);
                _logger.Info(result.Message);
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = $"Migration import failed: {ex.Message}";
                _logger.ErrorException("Error during migration import", ex);
                Plugin.IntroSkipProgress.AddLogEntry($"ERROR: {ex.Message}");
            }
            finally
            {
                Plugin.IntroSkipProgress.IsRunning = false;
            }
            
            return result;
        }
    }

    public class IntroSkipBackup
    {
        public string Version { get; set; } = "1.0";
        public DateTime BackupDate { get; set; }
        public int TotalEpisodes { get; set; }
        public int EpisodesWithMarkers { get; set; }
        public List<IntroSkipBackupEntry> Entries { get; set; } = new List<IntroSkipBackupEntry>();
    }

    public class IntroSkipBackupEntry
    {
        public string SeriesName { get; set; } = string.Empty;
        public string SeriesId { get; set; } = string.Empty;
        public string? TvdbId { get; set; }
        public string? TmdbId { get; set; }
        public string? ImdbId { get; set; }
        public string? TvdbEpisodeId { get; set; } // theTVDB Episode ID for portable matching
        public int SeasonNumber { get; set; }
        public int EpisodeNumber { get; set; }
        public string EpisodeName { get; set; } = string.Empty;
        public string EpisodeId { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public List<ChapterMarkerInfo> Markers { get; set; } = new List<ChapterMarkerInfo>();
    }

    public class ChapterMarkerInfo
    {
        public string Name { get; set; } = string.Empty;
        public long StartPositionTicks { get; set; }
        public string MarkerType { get; set; } = string.Empty;
    }

    public class BackupResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public int TotalItems { get; set; }
        public int ItemsBackedUp { get; set; }
    }

    public class RestoreResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public int ItemsRestored { get; set; }
        public int ItemsSkipped { get; set; }
        public int ItemsNotFound { get; set; }
    }

    public class MigrationExport
    {
        public string ExportVersion { get; set; } = "1.0";
        public DateTime ExportDate { get; set; }
        public string Description { get; set; } = string.Empty;
        public int TotalSeries { get; set; }
        public int TotalEpisodes { get; set; }
        public List<MigrationEntry> Entries { get; set; } = new List<MigrationEntry>();
    }

    public class MigrationEntry
    {
        public string SeriesName { get; set; } = string.Empty;
        public string? TvdbId { get; set; }
        public string? TmdbId { get; set; }
        public int SeasonNumber { get; set; }
        public int EpisodeNumber { get; set; }
        public string EpisodeName { get; set; } = string.Empty;
        public string? TvdbEpisodeId { get; set; }
        public long IntroStartTicks { get; set; }
        public long IntroEndTicks { get; set; }
    }

    public class MigrationExportResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public int TotalEpisodesExported { get; set; }
    }

    public class MigrationImportResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public int ItemsImported { get; set; }
        public int ItemsSkipped { get; set; }
        public int ItemsNotFound { get; set; }
    }
    
    public enum MarkerType
    {
        None = 0,
        IntroStart = 1,
        IntroEnd = 2,
        CreditsStart = 3
    }
}