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
            CancellationToken cancellationToken)
        {
            var result = new BackupResult { Success = true };
            var backupData = new List<IntroSkipBackupEntry>();
            int totalItems = 0;
            int itemsWithMarkers = 0;

            try
            {
                _logger.Info($"Starting intro skip backup to: {backupFilePath}");
                _logger.Info($"Series IDs count: {seriesIds?.Count ?? 0}");
                _logger.Info($"Library IDs count: {libraryIds?.Count ?? 0}");

                if (string.IsNullOrWhiteSpace(backupFilePath))
                {
                    return new BackupResult
                    {
                        Success = false,
                        Message = "Backup file path is not configured."
                    };
                }

                if (!backupFilePath.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
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

                _logger.Info($"Found {totalItems} episodes to process");
                
                if (totalItems == 0)
                {
                    _logger.Warn("No episodes found to backup. Check your library/series selection.");
                }

                foreach (var episode in episodes)
                {
                    cancellationToken.ThrowIfCancellationRequested();

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

                        backupData.RemoveAll(e => e.EpisodeId == backupEntry.EpisodeId || 
                            (e.FilePath == backupEntry.FilePath && !string.IsNullOrEmpty(backupEntry.FilePath)));
                        
                        backupData.Add(backupEntry);
                        itemsWithMarkers++;
                    }
                }

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
                }
                catch (UnauthorizedAccessException ex)
                {
                    return new BackupResult
                    {
                        Success = false,
                        Message = $"Access denied to backup file '{backupFilePath}'. Please check file permissions or choose a different location. Error: {ex.Message}"
                    };
                }
                catch (IOException ex)
                {
                    return new BackupResult
                    {
                        Success = false,
                        Message = $"Failed to write backup file '{backupFilePath}'. Error: {ex.Message}"
                    };
                }

                result.TotalItems = totalItems;
                result.ItemsBackedUp = itemsWithMarkers;
                result.Message = $"Successfully backed up {itemsWithMarkers} episodes with intro skip markers out of {totalItems} total episodes";
                
                _logger.Info(result.Message);
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = $"Backup failed: {ex.Message}";
                _logger.ErrorException("Error during intro skip backup", ex);
            }

            return result;
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
            var result = new RestoreResult { Success = true };
            int restored = 0;
            int skipped = 0;
            int notFound = 0;

            try
            {
                _logger.Info($"Starting intro skip restore from: {backupFilePath}");

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

                foreach (var entry in backup.Entries)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    Episode? episode = null;

                    if (long.TryParse(entry.EpisodeId, out long episodeId))
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
                    
                    _logger.Info($"Restored {entry.Markers.Count} markers for: {episode.Series?.Name} - S{episode.ParentIndexNumber:D2}E{episode.IndexNumber:D2} - {episode.Name}");
                }

                result.ItemsRestored = restored;
                result.ItemsSkipped = skipped;
                result.ItemsNotFound = notFound;
                result.Message = $"Restore complete: {restored} restored, {skipped} skipped, {notFound} not found";
                
                _logger.Info(result.Message);
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = $"Restore failed: {ex.Message}";
                _logger.ErrorException("Error during intro skip restore", ex);
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
}
