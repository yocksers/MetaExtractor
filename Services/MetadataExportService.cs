using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Persistence;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.MediaInfo;
using MediaBrowser.Model.Providers;

namespace MetaExtractor.Services
{
    public class MetadataExportService
    {
        private readonly ILogger _logger;
        private readonly ILibraryManager _libraryManager;
        private readonly IProviderManager _providerManager;
        private readonly IItemRepository _itemRepository;

        [DllImport("Kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool CreateHardLink(string lpFileName, string lpExistingFileName, IntPtr lpSecurityAttributes);

        private static bool IsWindows()
        {
            return RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        }

        public MetadataExportService(ILogger logger, ILibraryManager libraryManager, IProviderManager providerManager, IItemRepository itemRepository)
        {
            _logger = logger;
            _libraryManager = libraryManager;
            _providerManager = providerManager;
            _itemRepository = itemRepository;
        }

        public ExportResult ExportMetadata(List<string> enabledLibraryIds, PluginConfiguration config, CancellationToken cancellationToken = default)
        {
            var result = new ExportResult { Success = true };
            var exportedCount = 0;
            var errors = new ConcurrentBag<string>();

            Plugin.CurrentProgress = new ExportProgress
            {
                IsExporting = true,
                TotalItems = 0,
                ProcessedItems = 0,
                ExportedItems = 0,
                Percentage = 0,
                CurrentItem = "Starting export..."
            };
            Plugin.CurrentProgress.ClearLog();

            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!config.ExportNfo && !config.ExportArtwork)
                {
                    result.Success = false;
                    result.Message = "No export options enabled. Please enable NFO export and/or artwork export in settings.";
                    return result;
                }
                
                _logger.Info("Starting metadata export...");
                _logger.Info($"Selection mode: {config.SelectionMode}");
                _logger.Info($"Dry run mode: {config.DryRun}");

                var items = new List<BaseItem>();

                if (config.SelectionMode == "individual")
                {
                    if (config.SelectedItemIds == null || config.SelectedItemIds.Count == 0)
                    {
                        _logger.Info("No items selected for export");
                        result.Message = "No items selected. Please select at least one item in settings.";
                        result.ItemsProcessed = 0;
                        return result;
                    }

                    _logger.Info($"Selected item IDs: {string.Join(", ", config.SelectedItemIds)}");

                    foreach (var itemId in config.SelectedItemIds)
                    {
                        BaseItem? item = null;
                        
                        if (Guid.TryParse(itemId, out Guid itemGuid))
                        {
                            item = _libraryManager.GetItemById(itemGuid);
                        }
                        else if (long.TryParse(itemId, out long itemIdLong))
                        {
                            item = _libraryManager.GetItemById(itemIdLong);
                        }
                        else
                        {
                            _logger.Warn($"Invalid item ID format: {itemId}");
                            continue;
                        }

                        if (item == null)
                        {
                            _logger.Warn($"Item not found: {itemId}");
                            continue;
                        }

                        _logger.Info($"Found item: {item.Name} (Type: {item.GetType().Name})");

                        if (item is Series)
                        {
                            items.Add(item);
                            
                            var seriesItems = _libraryManager.GetItemList(new InternalItemsQuery
                            {
                                Parent = item,
                                Recursive = true
                            }).ToList();
                            items.AddRange(seriesItems);
                            _logger.Info($"Added series '{item.Name}', {seriesItems.Count(i => i is Season)} seasons, and {seriesItems.Count(i => i is Episode)} episodes");
                        }
                        else if (item is BoxSet boxSet)
                        {
                            // Add the collection itself for NFO/artwork export
                            items.Add(item);
                            
                            // Also export metadata for all items in the collection
                            // BoxSet uses LinkedChildren instead of regular Parent relationship
                            try
                            {
                                var children = boxSet.GetRecursiveChildren();
                                var collectionItems = children.Where(c => c is Movie || c is Series).ToList();
                                items.AddRange(collectionItems);
                                _logger.Info($"Added collection '{item.Name}' with {collectionItems.Count} items");
                            }
                            catch (Exception ex)
                            {
                                _logger.Warn($"Could not get children for collection '{item.Name}': {ex.Message}");
                                _logger.Info($"Added collection '{item.Name}' with 0 items");
                            }
                        }
                        else
                        {
                            items.Add(item);
                        }
                    }
                }
                else
                {
                    _logger.Info($"Enabled library IDs: {string.Join(", ", enabledLibraryIds)}");

                    if (enabledLibraryIds == null || enabledLibraryIds.Count == 0)
                    {
                        _logger.Info("No libraries selected for export");
                        result.Message = "No libraries selected. Please select at least one library in settings.";
                        result.ItemsProcessed = 0;
                        return result;
                    }

                    foreach (var libraryId in enabledLibraryIds)
                    {
                        if (!Guid.TryParse(libraryId, out Guid libGuid))
                        {
                            _logger.Warn($"Invalid library ID format: {libraryId}");
                            continue;
                        }

                        var library = _libraryManager.GetItemById(libGuid);
                        if (library == null)
                        {
                            _logger.Warn($"Library not found: {libraryId}");
                            continue;
                        }

                        _logger.Info($"Processing library: {library.Name} (ID: {libraryId})");

                        var itemTypes = new List<string> { "Movie", "Episode", "Series", "Season" };
                        if (config.ExportCollections)
                        {
                            itemTypes.Add("BoxSet");
                        }

                        List<BaseItem> libraryItems;
                        
                        // Special handling for Collections library - BoxSets don't use standard Parent relationship
                        if (library is MediaBrowser.Controller.Entities.CollectionFolder collectionFolder && 
                            collectionFolder.CollectionType == "boxsets")
                        {
                            _logger.Debug($"Library '{library.Name}' is a Collections library, using special query");
                            // Query all BoxSets without Parent filter for Collections library
                            libraryItems = _libraryManager.GetItemList(new InternalItemsQuery
                            {
                                IncludeItemTypes = new[] { "BoxSet" },
                                Recursive = true
                            }).ToList();
                        }
                        else
                        {
                            libraryItems = _libraryManager.GetItemList(new InternalItemsQuery
                            {
                                Parent = library,
                                Recursive = true,
                                IncludeItemTypes = itemTypes.ToArray()
                            }).ToList();
                        }

                        _logger.Info($"Found {libraryItems.Count} items in library '{library.Name}'");
                        items.AddRange(libraryItems);
                    }
                }

                _logger.Info($"Total items to process: {items.Count}");

                var totalItems = items.Count;
                var processedItems = 0;

                Plugin.CurrentProgress.TotalItems = totalItems;
                Plugin.CurrentProgress.ProcessedItems = 0;
                Plugin.CurrentProgress.ExportedItems = 0;
                Plugin.CurrentProgress.Percentage = 0;

                int lastReportedPercent = -1;
                
                int maxParallel = config.MaxParallelTasks;
                
                Parallel.ForEach(items, new ParallelOptions { CancellationToken = cancellationToken, MaxDegreeOfParallelism = maxParallel }, item =>
                {
                    try
                    {
                        if (ExportItemMetadata(item, config, cancellationToken))
                        {
                            Interlocked.Increment(ref exportedCount);
                        }
                        
                        var currentProcessed = Interlocked.Increment(ref processedItems);
                        
                        var currentPercent = (int)((currentProcessed / (double)totalItems) * 100);
                        bool shouldUpdate = (currentPercent != lastReportedPercent && currentPercent % 5 == 0) || 
                                          (currentProcessed % 10 == 0) || 
                                          currentProcessed == totalItems;
                        
                        if (shouldUpdate)
                        {
                            Plugin.CurrentProgress.CurrentItem = item.Name ?? "Unknown";
                            Plugin.CurrentProgress.ProcessedItems = currentProcessed;
                            Plugin.CurrentProgress.ExportedItems = exportedCount;
                            Plugin.CurrentProgress.Percentage = currentPercent;
                            Interlocked.Exchange(ref lastReportedPercent, currentPercent);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        _logger.ErrorException($"Error exporting metadata for {item.Name}", ex);
                        errors.Add($"Failed to export {item.Name}: {ex.Message}");
                    }
                });

                result.ItemsProcessed = processedItems;
                result.TotalItems = totalItems;
                result.Errors = errors.ToList();
                if (exportedCount == 0)
                {
                    result.Message = $"Processed {processedItems} item(s), but no files were exported. All files may already exist (check overwrite settings) or items may not have metadata/artwork to export.";
                }
                else if (exportedCount < processedItems)
                {
                    result.Message = $"Successfully exported metadata for {exportedCount} out of {processedItems} item(s). {processedItems - exportedCount} item(s) were skipped (files already exist or no data to export).";
                }
                else
                {
                    result.Message = $"Successfully exported metadata for {exportedCount} item(s).";
                }
                _logger.Info(result.Message);
            }
            catch (OperationCanceledException)
            {
                _logger.Info("Metadata export was cancelled by user");
                result.Success = false;
                result.Message = "Export cancelled by user";
            }
            catch (Exception ex)
            {
                _logger.ErrorException("Error during metadata export", ex);
                result.Success = false;
                result.Message = $"Export failed: {ex.Message}";
            }
            finally
            {
                Plugin.CurrentProgress.IsExporting = false;
                Plugin.CurrentProgress.CurrentItem = "Export complete";
            }

            return result;
        }

        private string? GetExportDirectory(BaseItem item, PluginConfiguration config)
        {
            string? originalDirectory;
            
            if (item is Series || item is Season)
            {
                originalDirectory = item.Path;
            }
            else if (item is BoxSet boxSet)
            {
                // Collections don't have a physical path, use a custom path or the configured path
                if (config.UseCustomExportPath && !string.IsNullOrEmpty(config.CustomExportPath))
                {
                    // Use custom export path with a "Collections" subfolder
                    originalDirectory = Path.Combine(config.CustomExportPath, "Collections", SanitizeFileName(item.Name));
                }
                else
                {
                    // Use the library path if available, otherwise use Emby's data path
                    var libraryPath = GetLibraryPath(item);
                    if (!string.IsNullOrEmpty(libraryPath))
                    {
                        originalDirectory = Path.Combine(libraryPath, "Collections", SanitizeFileName(item.Name));
                    }
                    else
                    {
                        _logger.Debug($"Collection {item.Name} has no library path, cannot export");
                        return null;
                    }
                }
                
                // Ensure the directory exists for collections
                if (!string.IsNullOrEmpty(originalDirectory) && !Directory.Exists(originalDirectory) && !config.DryRun)
                {
                    try
                    {
                        Directory.CreateDirectory(originalDirectory);
                        _logger.Debug($"Created directory for collection: {originalDirectory}");
                    }
                    catch (Exception ex)
                    {
                        _logger.Error($"Failed to create directory for collection {item.Name}: {ex.Message}");
                        return null;
                    }
                }
            }
            else
            {
                var itemPath = item.Path;
                if (string.IsNullOrEmpty(itemPath))
                {
                    _logger.Debug($"Item {item.Name} has no path");
                    return null;
                }
                originalDirectory = Path.GetDirectoryName(itemPath);
            }
            
            if (string.IsNullOrEmpty(originalDirectory))
            {
                _logger.Debug($"Could not determine directory for {item.Name}");
                return null;
            }
            
            if (config.UseCustomExportPath && !string.IsNullOrEmpty(config.CustomExportPath))
            {
                try
                {
                    if (!Directory.Exists(config.CustomExportPath))
                    {
                        _logger.Warn($"Custom export path does not exist: {config.CustomExportPath}");
                        return originalDirectory;
                    }

                    var libraryPath = GetLibraryPath(item);
                    if (!string.IsNullOrEmpty(libraryPath))
                    {
                        var relativePath = Path.GetRelativePath(libraryPath, originalDirectory);
                        
                        var customDirectory = Path.Combine(config.CustomExportPath, relativePath);
                        
                        if (!Directory.Exists(customDirectory) && !config.DryRun)
                        {
                            Directory.CreateDirectory(customDirectory);
                        }
                        
                        return customDirectory;
                    }
                    else
                    {
                        _logger.Debug($"Could not determine library path for {item.Name}, using original directory");
                    }
                }
                catch (Exception ex)
                {
                    _logger.ErrorException($"Error creating custom export path for {item.Name}, using original directory", ex);
                }
            }
            
            return originalDirectory;
        }

        private string? GetLibraryPath(BaseItem item)
        {
            try
            {
                var folder = item.GetParent();
                while (folder != null)
                {
                    if (folder is MediaBrowser.Controller.Entities.Folder libraryFolder && libraryFolder.IsTopParent)
                    {
                        return libraryFolder.Path;
                    }
                    folder = folder.GetParent();
                }
            }
            catch (Exception ex)
            {
                _logger.Debug($"Error getting library path for {item.Name}: {ex.Message}");
            }
            
            return null;
        }

        private bool ExportItemMetadata(BaseItem item, PluginConfiguration config, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            if (item == null)
            {
                _logger.Warn("ExportItemMetadata called with null item");
                return false;
            }
            
            string? directory = GetExportDirectory(item, config);
            
            if (string.IsNullOrEmpty(directory))
            {
                _logger.Debug($"Could not determine export directory for {item.Name}");
                var skipEntry = $"Skipped (no export directory): {item.Name}";
                Plugin.CurrentProgress.AddLogEntry(skipEntry);
                return false;
            }

            var exported = false;

            if (config.ExportArtwork)
            {
                exported |= ExportArtwork(item, directory, config, cancellationToken);
            }

            if (config.ExportNfo)
            {
                exported |= ExportNfo(item, directory, config, cancellationToken);
            }

            if (!exported)
            {
                var skipEntry = $"Skipped (no metadata/artwork to export or all files exist): {item.Name}";
                Plugin.CurrentProgress.AddLogEntry(skipEntry);
            }

            return exported;
        }

        private bool ExportArtwork(BaseItem item, string directory, PluginConfiguration config, CancellationToken cancellationToken)
        {
            var exported = false;

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                var posterName = config.UseCustomArtworkNames ? config.CustomPosterName : "poster";
                var fanartName = config.UseCustomArtworkNames ? config.CustomFanartName : "fanart";
                var logoName = config.UseCustomArtworkNames ? config.CustomLogoName : "clearlogo";
                var bannerName = config.UseCustomArtworkNames ? config.CustomBannerName : "banner";
                var thumbName = config.UseCustomArtworkNames ? config.CustomThumbName : "landscape";
                var artName = config.UseCustomArtworkNames ? config.CustomArtName : "clearart";
                var discName = config.UseCustomArtworkNames ? config.CustomDiscName : "disc";
                
                if (config.ExportPoster && item.HasImage(ImageType.Primary))
                {
                    exported |= ExportImage(item, ImageType.Primary, directory, posterName, config, cancellationToken);
                }

                if (config.ExportBackdrop && item.HasImage(ImageType.Backdrop))
                {
                    var backdropCount = item.GetImages(ImageType.Backdrop).Count();
                    for (int i = 0; i < backdropCount; i++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        exported |= ExportImage(item, ImageType.Backdrop, directory, i == 0 ? fanartName : $"{fanartName}{i}", config, cancellationToken, i);
                    }
                }

                if (config.ExportLogo && item.HasImage(ImageType.Logo))
                {
                    exported |= ExportImage(item, ImageType.Logo, directory, logoName, config, cancellationToken);
                }

                if (config.ExportBanner && item.HasImage(ImageType.Banner))
                {
                    exported |= ExportImage(item, ImageType.Banner, directory, bannerName, config, cancellationToken);
                }

                if (config.ExportThumb && item.HasImage(ImageType.Thumb))
                {
                    exported |= ExportImage(item, ImageType.Thumb, directory, thumbName, config, cancellationToken);
                }

                if (config.ExportArt && item.HasImage(ImageType.Art))
                {
                    exported |= ExportImage(item, ImageType.Art, directory, artName, config, cancellationToken);
                }

                if (config.ExportDisc && item.HasImage(ImageType.Disc))
                {
                    exported |= ExportImage(item, ImageType.Disc, directory, discName, config, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.ErrorException($"Error exporting artwork for {item.Name}", ex);
            }

            return exported;
        }

        private bool ExportImage(BaseItem item, ImageType imageType, string directory, string baseName, PluginConfiguration config, CancellationToken cancellationToken, int imageIndex = 0)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                var imageInfo = item.GetImageInfo(imageType, imageIndex);
                if (imageInfo == null || string.IsNullOrEmpty(imageInfo.Path))
                {
                    return false;
                }

                var sourceFile = imageInfo.Path;
                if (!File.Exists(sourceFile))
                {
                    return false;
                }

                var extension = Path.GetExtension(sourceFile);
                var targetFile = Path.Combine(directory, $"{baseName}{extension}");

                if (File.Exists(targetFile) && !config.OverwriteArtwork)
                {
                    _logger.Debug($"Skipping {imageType} for {item.Name} - file already exists");
                    var skipEntry = $"{imageType} (skipped - already exists): {item.Name}";
                    Plugin.CurrentProgress.AddLogEntry(skipEntry);
                    return false;
                }

                if (config.DryRun)
                {
                    var logEntry = $"[DRY RUN] {imageType}: {item.Name} → {targetFile}";
                    Plugin.CurrentProgress.AddLogEntry(logEntry);
                    return true;
                }

                if (config.UseCustomExportPath && config.UseHardlinks)
                {
                    if (!IsWindows())
                    {
                        _logger.Debug("Hardlinks are only supported on Windows. Falling back to copy.");
                    }
                    else
                    {
                        try
                        {
                            if (File.Exists(targetFile))
                            {
                                File.Delete(targetFile);
                            }
                            
                            if (CreateHardLink(targetFile, sourceFile, IntPtr.Zero))
                            {
                                var successEntry = $"{imageType} (hardlink): {item.Name} → {targetFile}";
                                Plugin.CurrentProgress.AddLogEntry(successEntry);
                                _logger.Debug($"Created hardlink for {imageType} for {item.Name} to {targetFile}");
                                return true;
                            }
                            else
                            {
                                _logger.Debug($"Hardlink creation failed, falling back to copy for {targetFile}");
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.Debug($"Hardlink creation failed ({ex.Message}), falling back to copy for {targetFile}");
                        }
                    }
                }

                int retries = 5;
                int delay = 100;
                
                for (int attempt = 0; attempt < retries; attempt++)
                {
                    try
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        
                        using (var sourceStream = new FileStream(sourceFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                        using (var targetStream = new FileStream(targetFile, FileMode.Create, FileAccess.Write, FileShare.Read))
                        {
                            sourceStream.CopyTo(targetStream);
                        }
                        
                        var successEntry = $"{imageType}: {item.Name} → {targetFile}";
                        Plugin.CurrentProgress.AddLogEntry(successEntry);
                        
                        _logger.Debug($"Exported {imageType} for {item.Name} to {targetFile}");
                        return true;
                    }
                    catch (IOException ex) when (attempt < retries - 1)
                    {
                        _logger.Debug($"Retry {attempt + 1}/{retries} for {targetFile}: {ex.Message}");
                        System.Threading.Thread.Sleep(delay);
                        delay *= 2;
                    }
                    catch (IOException ex) when (attempt == retries - 1)
                    {
                        _logger.Warn($"Unable to export {imageType} for {item.Name} after {retries} attempts - file may be locked: {ex.Message}");
                        return false;
                    }
                }
                
                return false;
            }
            catch (Exception ex)
            {
                _logger.ErrorException($"Error exporting {imageType} image for {item.Name}", ex);
                return false;
            }
        }

        private bool ExportNfo(BaseItem item, string directory, PluginConfiguration config, CancellationToken cancellationToken)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                string nfoPath;
                
                if (item is Series)
                {
                    nfoPath = Path.Combine(directory, "tvshow.nfo");
                }
                else if (item is Season)
                {
                    nfoPath = Path.Combine(directory, "season.nfo");
                }
                else
                {
                    var fileName = Path.GetFileNameWithoutExtension(item.Path);
                    nfoPath = Path.Combine(directory, $"{fileName}.nfo");
                }

                if (File.Exists(nfoPath) && !config.OverwriteNfo)
                {
                    _logger.Debug($"Skipping NFO for {item.Name} - file already exists");
                    var skipEntry = $"NFO (skipped - already exists): {item.Name}";
                    Plugin.CurrentProgress.AddLogEntry(skipEntry);
                    return false;
                }

                var xml = GenerateNfoXml(item, config);
                if (string.IsNullOrEmpty(xml))
                {
                    return false;
                }

                if (config.DryRun)
                {
                    var logEntry = $"[DRY RUN] NFO: {item.Name} → {nfoPath}";
                    Plugin.CurrentProgress.AddLogEntry(logEntry);
                    return true;
                }

                cancellationToken.ThrowIfCancellationRequested();
                
                var tempPath = nfoPath + ".tmp";
                try
                {
                    File.WriteAllText(tempPath, xml, Encoding.UTF8);
                    
                    if (File.Exists(nfoPath))
                    {
                        File.Delete(nfoPath);
                    }
                    File.Move(tempPath, nfoPath);
                }
                finally
                {
                    if (File.Exists(tempPath))
                    {
                        try 
                        { 
                            File.Delete(tempPath); 
                        } 
                        catch (Exception cleanupEx)
                        {
                            _logger.Debug($"Failed to cleanup temp file {tempPath}: {cleanupEx.Message}");
                        }
                    }
                }
                
                var successEntry = $"NFO: {item.Name} → {nfoPath}";
                Plugin.CurrentProgress.AddLogEntry(successEntry);
                
                _logger.Debug($"Exported NFO for {item.Name} to {nfoPath}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.ErrorException($"Error exporting NFO for {item.Name}", ex);
                return false;
            }
        }

        private string GenerateNfoXml(BaseItem item, PluginConfiguration config)
        {
            var settings = new XmlWriterSettings
            {
                Indent = true,
                Encoding = Encoding.UTF8,
                OmitXmlDeclaration = false
            };

            using (var stringWriter = new Utf8StringWriter())
            using (var writer = XmlWriter.Create(stringWriter, settings))
            {
                string rootElement = "movie";
                bool isEpisode = item is Episode;
                bool isMovie = item is Movie;
                bool isSeries = item is Series;
                bool isSeason = item is Season;
                
                if (isEpisode)
                {
                    rootElement = "episodedetails";
                }
                else if (isSeason)
                {
                    rootElement = "season";
                }
                else if (isSeries)
                {
                    rootElement = "tvshow";
                }

                writer.WriteStartDocument();
                
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0.0";
                writer.WriteComment($"Written {timestamp} by MetaExtractor {version} for Emby");

                writer.WriteStartElement(rootElement);

                if (config.NfoIncludePlot && !string.IsNullOrEmpty(item.Overview))
                {
                    writer.WriteStartElement("plot");
                    writer.WriteCData(item.Overview);
                    writer.WriteEndElement();
                    
                    writer.WriteStartElement("outline");
                    var outlineText = item.Overview;
                    if (config.NfoIncludeTagline && !string.IsNullOrEmpty(item.Tagline))
                    {
                        outlineText = item.Tagline;
                    }
                    writer.WriteCData(outlineText);
                    writer.WriteEndElement();
                }

                writer.WriteElementString("lockdata", "false");

                if (config.NfoIncludeDates)
                {
                    writer.WriteElementString("dateadded", item.DateCreated.ToString("yyyy-MM-dd HH:mm:ss"));
                }

                if (config.NfoIncludeTitles)
                {
                    writer.WriteElementString("title", item.Name);
                    writer.WriteElementString("originaltitle", item.OriginalTitle ?? item.Name);
                }

                if (config.NfoIncludeActors)
                {
                    WritePeopleInfo(writer, item, isEpisode);
                }

                if (config.NfoIncludeDirectors)
                {
                    WriteDirectors(writer, item);
                }
                
                if (config.NfoIncludeWriters)
                {
                    WriteWriters(writer, item);
                }

                if (config.NfoIncludeTrailers)
                {
                    WriteTrailers(writer, item);
                }

                if (config.NfoIncludeRating && item.CommunityRating.HasValue)
                {
                    writer.WriteElementString("rating", item.CommunityRating.Value.ToString("0.0"));
                }
                
                if (config.NfoIncludeDetailedRatings)
                {
                    WriteDetailedRatings(writer, item);
                }

                if (config.NfoIncludeYear && item.PremiereDate.HasValue)
                {
                    writer.WriteElementString("year", item.PremiereDate.Value.Year.ToString(CultureInfo.InvariantCulture));
                }

                if (config.NfoIncludeTitles)
                {
                    writer.WriteElementString("sorttitle", item.SortName ?? item.Name);
                }

                if (config.NfoIncludeMpaa && !string.IsNullOrEmpty(item.OfficialRating))
                {
                    writer.WriteElementString("mpaa", item.OfficialRating);
                    
                    if (config.NfoIncludeCertification)
                    {
                        writer.WriteElementString("certification", item.OfficialRating);
                    }
                }

                if (config.NfoIncludeProviderIds)
                {
                    WriteProviderIdElements(writer, item);
                }

                if (config.NfoIncludeDates && item.PremiereDate.HasValue)
                {
                    string dateStr = item.PremiereDate.Value.ToString("yyyy-MM-dd");
                    if (isEpisode)
                    {
                    }
                    else
                    {
                        writer.WriteElementString("premiered", dateStr);
                        if (!isSeries)
                        {
                            writer.WriteElementString("releasedate", dateStr);
                        }
                        else
                        {
                            var series = item as Series;
                            if (series != null && series.EndDate.HasValue)
                            {
                                writer.WriteElementString("enddate", series.EndDate.Value.ToString("yyyy-MM-dd"));
                            }
                        }
                    }
                }

                if (config.NfoIncludeRating && item.CriticRating.HasValue)
                {
                    writer.WriteElementString("criticrating", ((int)item.CriticRating.Value).ToString(CultureInfo.InvariantCulture));
                }

                if (config.NfoIncludeRuntime && item.RunTimeTicks.HasValue)
                {
                    var runtime = TimeSpan.FromTicks(item.RunTimeTicks.Value);
                    writer.WriteElementString("runtime", ((int)runtime.TotalMinutes).ToString(CultureInfo.InvariantCulture));
                }

                if (config.NfoIncludeTagline && isMovie)
                {
                    var movie = item as Movie;
                    if (movie != null && !string.IsNullOrEmpty(movie.Tagline))
                    {
                        writer.WriteElementString("tagline", movie.Tagline);
                    }
                }

                if (config.NfoIncludeCountries && item.ProductionLocations != null && item.ProductionLocations.Length > 0)
                {
                    foreach (var country in item.ProductionLocations)
                    {
                        writer.WriteElementString("country", country);
                    }
                }

                if (config.NfoIncludeGenres && item.Genres != null)
                {
                    foreach (var genre in item.Genres)
                    {
                        writer.WriteElementString("genre", genre);
                    }
                }

                if (config.NfoIncludeStudios && item.Studios != null)
                {
                    foreach (var studio in item.Studios)
                    {
                        writer.WriteElementString("studio", studio);
                    }
                }

                if (config.NfoIncludeTags && item.Tags != null && item.Tags.Length > 0)
                {
                    foreach (var tag in item.Tags)
                    {
                        if (!string.IsNullOrEmpty(tag))
                        {
                            writer.WriteElementString("tag", tag);
                        }
                    }
                }

                if (config.NfoIncludeCollections)
                {
                    WriteCollections(writer, item);
                }

                if (config.NfoIncludeUniqueIds)
                {
                    WriteUniqueIds(writer, item);
                    WriteProviderIdFields(writer, item);
                }

                if (isEpisode)
                {
                    var episode = item as Episode;
                    if (episode != null)
                    {
                        if (config.NfoIncludeTitles)
                        {
                            var series = episode.Series;
                            if (series != null)
                            {
                                writer.WriteElementString("showtitle", series.Name);
                            }
                        }
                        
                        if (episode.IndexNumber.HasValue)
                        {
                            writer.WriteElementString("episode", episode.IndexNumber.Value.ToString(CultureInfo.InvariantCulture));
                            
                            if (episode.IndexNumberEnd.HasValue && episode.IndexNumberEnd.Value != episode.IndexNumber.Value)
                            {
                                writer.WriteElementString("displayepisode", episode.IndexNumber.Value.ToString(CultureInfo.InvariantCulture));
                            }
                        }
                        if (episode.ParentIndexNumber.HasValue)
                        {
                            writer.WriteElementString("season", episode.ParentIndexNumber.Value.ToString(CultureInfo.InvariantCulture));
                            
                            if (episode.ParentIndexNumber.Value == 0)
                            {
                                writer.WriteElementString("displayseason", "0");
                            }
                        }
                        if (config.NfoIncludeDates && episode.PremiereDate.HasValue)
                        {
                            writer.WriteElementString("aired", episode.PremiereDate.Value.ToString("yyyy-MM-dd"));
                        }
                    }
                }

                if (isSeries)
                {
                    var series = item as Series;
                    if (series != null)
                    {
                        if (config.NfoIncludeProviderIds)
                        {
                            WriteEpisodeGuide(writer, item);
                        }
                        
                        if (config.NfoIncludeProviderIds && item.ProviderIds.TryGetValue("Tvdb", out var tvdbId))
                        {
                            writer.WriteElementString("id", tvdbId);
                        }
                        
                        writer.WriteElementString("season", "-1");
                        writer.WriteElementString("episode", "-1");
                        writer.WriteElementString("displayorder", "aired");
                        
                        if (series.Status != null)
                        {
                            writer.WriteElementString("status", series.Status.ToString());
                        }
                    }
                }

                if (isSeason)
                {
                    var season = item as Season;
                    if (season != null && season.IndexNumber.HasValue)
                    {
                        writer.WriteElementString("seasonnumber", season.IndexNumber.Value.ToString(CultureInfo.InvariantCulture));
                    }
                }

                if (config.NfoIncludeChapters)
                {
                    WriteChapters(writer, item, config);
                }

                WriteFileInfo(writer, item);
                
                if (config.NfoIncludeFanart)
                {
                    WriteFanartSection(writer, item);
                }

                writer.WriteEndElement();
                writer.Flush();

                return stringWriter.ToString();
            }
        }

        private void WritePeopleInfo(XmlWriter writer, BaseItem item, bool isEpisode)
        {
            if (writer == null || item == null)
                return;
                
            try
            {
                var people = _libraryManager.GetItemPeople(item);
                
                if (people == null || people.Count == 0)
                    return;
                
                foreach (var person in people)
                {
                    if (person.Type == PersonType.Director)
                        continue;

                    writer.WriteStartElement("actor");
                        
                        if (!string.IsNullOrEmpty(person.Name))
                        {
                            writer.WriteElementString("name", person.Name);
                        }
                        
                        if (!string.IsNullOrEmpty(person.Role))
                        {
                            writer.WriteElementString("role", person.Role);
                        }
                        
                        writer.WriteElementString("type", person.Type.ToString());
                        
                        try
                        {
                            if (!string.IsNullOrEmpty(person.Name))
                            {
                                var personEntity = _libraryManager.GetItemList(new InternalItemsQuery
                                {
                                    Name = person.Name,
                                    IncludeItemTypes = new[] { "Person" },
                                    Limit = 1
                                }).FirstOrDefault() as Person;

                                if (personEntity != null && personEntity.ProviderIds != null && personEntity.ProviderIds.Count > 0)
                                {
                                    foreach (var providerId in personEntity.ProviderIds)
                                    {
                                        var key = providerId.Key.ToLower();
                                        writer.WriteElementString($"{key}id", providerId.Value);
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.Debug($"Could not get provider IDs for person {person.Name}: {ex.Message}");
                    }
                    
                    writer.WriteEndElement(); 
                }
            }
            catch (Exception ex)
            {
                _logger.ErrorException($"Error writing people information for {item.Name}", ex);
            }
        }

        private void WriteDirectors(XmlWriter writer, BaseItem item)
        {
            try
            {
                var people = _libraryManager.GetItemPeople(item);
                
                if (people != null && people.Count > 0)
                {
                    foreach (var person in people.Where(p => p.Type == PersonType.Director))
                    {
                        writer.WriteStartElement("director");
                        
                        try
                        {
                            if (!string.IsNullOrEmpty(person.Name))
                            {
                                var personEntity = _libraryManager.GetItemList(new InternalItemsQuery
                                {
                                    Name = person.Name,
                                    IncludeItemTypes = new[] { "Person" },
                                    Limit = 1
                                }).FirstOrDefault() as Person;

                                if (personEntity != null && personEntity.ProviderIds != null && personEntity.ProviderIds.Count > 0)
                                {
                                    foreach (var providerId in personEntity.ProviderIds)
                                    {
                                        var key = providerId.Key.ToLower();
                                        writer.WriteAttributeString($"{key}id", providerId.Value);
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.Debug($"Could not get provider IDs for director {person.Name}: {ex.Message}");
                        }
                        
                        writer.WriteString(person.Name ?? string.Empty);
                        writer.WriteEndElement(); 
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.ErrorException($"Error writing director information for {item.Name}", ex);
            }
        }

        private void WriteWriters(XmlWriter writer, BaseItem item)
        {
            try
            {
                var people = _libraryManager.GetItemPeople(item);
                
                if (people != null && people.Count > 0)
                {
                    var writers = people.Where(p => p.Type == PersonType.Writer).ToList();
                    foreach (var person in writers)
                    {
                        writer.WriteStartElement("writer");
                        
                        try
                        {
                            if (!string.IsNullOrEmpty(person.Name))
                            {
                                var personEntity = _libraryManager.GetItemList(new InternalItemsQuery
                                {
                                    Name = person.Name,
                                    IncludeItemTypes = new[] { "Person" },
                                    Limit = 1
                                }).FirstOrDefault() as Person;

                                if (personEntity != null && personEntity.ProviderIds != null && personEntity.ProviderIds.Count > 0)
                                {
                                    foreach (var providerId in personEntity.ProviderIds)
                                    {
                                        var key = providerId.Key.ToLower();
                                        writer.WriteAttributeString($"{key}id", providerId.Value);
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.Debug($"Could not get provider IDs for writer {person.Name}: {ex.Message}");
                        }
                        
                        writer.WriteString(person.Name ?? string.Empty);
                        writer.WriteEndElement(); 
                    }
                    
                    foreach (var person in writers)
                    {
                        writer.WriteStartElement("credits");
                        
                        try
                        {
                            if (!string.IsNullOrEmpty(person.Name))
                            {
                                var personEntity = _libraryManager.GetItemList(new InternalItemsQuery
                                {
                                    Name = person.Name,
                                    IncludeItemTypes = new[] { "Person" },
                                    Limit = 1
                                }).FirstOrDefault() as Person;

                                if (personEntity != null && personEntity.ProviderIds != null && personEntity.ProviderIds.Count > 0)
                                {
                                    foreach (var providerId in personEntity.ProviderIds)
                                    {
                                        var key = providerId.Key.ToLower();
                                        writer.WriteAttributeString($"{key}id", providerId.Value);
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.Debug($"Could not get provider IDs for credits {person.Name}: {ex.Message}");
                        }
                        
                        writer.WriteString(person.Name ?? string.Empty);
                        writer.WriteEndElement(); 
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.ErrorException($"Error writing writer information for {item.Name}", ex);
            }
        }

        private void WriteTrailers(XmlWriter writer, BaseItem item)
        {
            try
            {
                if (item.RemoteTrailers != null && item.RemoteTrailers.Length > 0)
                {
                    foreach (var trailerUrl in item.RemoteTrailers)
                    {
                        if (!string.IsNullOrEmpty(trailerUrl))
                        {
                            writer.WriteElementString("trailer", trailerUrl);
                        }
                    }
                }
                
                if (item.LocalTrailerIds != null && item.LocalTrailerIds.Length > 0)
                {
                    foreach (var trailerId in item.LocalTrailerIds)
                    {
                        var trailerItem = _libraryManager.GetItemById(trailerId);
                        if (trailerItem != null && !string.IsNullOrEmpty(trailerItem.Path))
                        {
                            writer.WriteElementString("trailer", trailerItem.Path);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.ErrorException("Error writing trailers", ex);
            }
        }

        private void WriteProviderIdElements(XmlWriter writer, BaseItem item)
        {
            if (item.ProviderIds == null || item.ProviderIds.Count == 0)
                return;

            if (item.ProviderIds.TryGetValue("Imdb", out var imdbId))
            {
                writer.WriteElementString("imdbid", imdbId);
                writer.WriteElementString("id", imdbId);
                
                if (item is Movie)
                {
                    writer.WriteElementString("imdb_id", imdbId);
                }
            }

            if (item.ProviderIds.TryGetValue("Tvdb", out var tvdbId))
            {
                writer.WriteElementString("tvdbid", tvdbId);
            }

            if (item.ProviderIds.TryGetValue("Tmdb", out var tmdbId))
            {
                writer.WriteElementString("tmdbid", tmdbId);
            }
            
            if (item.ProviderIds.TryGetValue("TvRage", out var tvrageId))
            {
                writer.WriteElementString("tvrageid", tvrageId);
            }
            
            if (item.ProviderIds.TryGetValue("Trakt", out var traktId))
            {
                writer.WriteElementString("traktid", traktId);
            }
            
            if (item.ProviderIds.TryGetValue("Zap2It", out var zap2itId))
            {
                writer.WriteElementString("zap2itid", zap2itId);
            }

            foreach (var providerId in item.ProviderIds)
            {
                var key = providerId.Key.ToLower();
                if (key != "imdb" && key != "tvdb" && key != "tmdb")
                {
                    var sanitizedKey = System.Text.RegularExpressions.Regex.Replace(key, @"[^a-z0-9_]", "");
                    
                    if (!string.IsNullOrEmpty(sanitizedKey) && !char.IsDigit(sanitizedKey[0]))
                    {
                        writer.WriteElementString($"{sanitizedKey}id", providerId.Value);
                    }
                }
            }
        }

        private void WriteUniqueIds(XmlWriter writer, BaseItem item)
        {
            if (item.ProviderIds == null || item.ProviderIds.Count == 0)
                return;

            bool hasImdb = item.ProviderIds.ContainsKey("Imdb");
            bool hasTmdb = item.ProviderIds.ContainsKey("Tmdb");

            foreach (var providerId in item.ProviderIds)
            {
                writer.WriteStartElement("uniqueid");
                writer.WriteAttributeString("type", providerId.Key.ToLowerInvariant());
                
                if (providerId.Key.Equals("Imdb", StringComparison.OrdinalIgnoreCase))
                {
                    writer.WriteAttributeString("default", "true");
                }
                else if (providerId.Key.Equals("Tmdb", StringComparison.OrdinalIgnoreCase) && !hasImdb)
                {
                    writer.WriteAttributeString("default", "true");
                }
                else if (providerId.Key.Equals("Tvdb", StringComparison.OrdinalIgnoreCase) && !hasImdb && !hasTmdb)
                {
                    writer.WriteAttributeString("default", "true");
                }
                
                writer.WriteString(providerId.Value);
                writer.WriteEndElement(); 
            }
        }

        private void WriteEpisodeGuide(XmlWriter writer, BaseItem item)
        {
            if (item.ProviderIds == null || item.ProviderIds.Count == 0)
                return;

            var guideData = new Dictionary<string, string>();
            foreach (var providerId in item.ProviderIds)
            {
                guideData[providerId.Key.ToLowerInvariant()] = providerId.Value.ToLowerInvariant();
            }

            if (guideData.Count > 0)
            {
                var jsonStr = "{" + string.Join(",", guideData.Select(kvp => $"\"{kvp.Key}\":\"{kvp.Value}\"")) + "}";
                writer.WriteElementString("episodeguide", jsonStr);
            }
        }

        private void WriteFileInfo(XmlWriter writer, BaseItem item)
        {
        }

        private void WriteDetailedRatings(XmlWriter writer, BaseItem item)
        {
            writer.WriteStartElement("ratings");
            
            if (item.CommunityRating.HasValue)
            {
                writer.WriteStartElement("rating");
                writer.WriteAttributeString("name", "themoviedb");
                writer.WriteAttributeString("max", "10");
                writer.WriteAttributeString("default", "true");
                writer.WriteElementString("value", item.CommunityRating.Value.ToString("0.0", CultureInfo.InvariantCulture));
                writer.WriteElementString("votes", "0");
                writer.WriteEndElement(); 
            }
            
            if (item.CriticRating.HasValue)
            {
                writer.WriteStartElement("rating");
                writer.WriteAttributeString("name", "metacritic");
                writer.WriteAttributeString("max", "100");
                writer.WriteElementString("value", item.CriticRating.Value.ToString("0.0", CultureInfo.InvariantCulture));
                writer.WriteElementString("votes", "0");
                writer.WriteEndElement(); 
            }
            
            writer.WriteEndElement(); 
        }

        private void WriteFanartSection(XmlWriter writer, BaseItem item)
        {
            writer.WriteStartElement("fanart");
            
            if (item.ImageInfos != null && item.ImageInfos.Length > 0)
            {
                foreach (var imageInfo in item.ImageInfos)
                {
                    if (string.IsNullOrEmpty(imageInfo.Path))
                        continue;

                    writer.WriteStartElement("thumb");
                    
                    switch (imageInfo.Type)
                    {
                        case ImageType.Primary:
                            writer.WriteAttributeString("aspect", "poster");
                            break;
                        case ImageType.Backdrop:
                            writer.WriteAttributeString("aspect", "fanart");
                            writer.WriteAttributeString("preview", imageInfo.Path);
                            break;
                        case ImageType.Banner:
                            writer.WriteAttributeString("aspect", "banner");
                            break;
                        case ImageType.Logo:
                            writer.WriteAttributeString("aspect", "clearlogo");
                            break;
                        case ImageType.Art:
                            writer.WriteAttributeString("aspect", "clearart");
                            break;
                        case ImageType.Disc:
                            writer.WriteAttributeString("aspect", "discart");
                            break;
                        case ImageType.Thumb:
                            writer.WriteAttributeString("aspect", "landscape");
                            break;
                        case ImageType.Box:
                            writer.WriteAttributeString("aspect", "keyart");
                            break;
                    }
                    
                    writer.WriteString(imageInfo.Path);
                    writer.WriteEndElement(); 
                }
            }
            
            writer.WriteEndElement(); 
        }

        private void WriteCollections(XmlWriter writer, BaseItem item)
        {
            if (item is Movie movie && movie.Collections != null && movie.Collections.Length > 0)
            {
                foreach (var collection in movie.Collections)
                {
                    writer.WriteStartElement("set");
                    
                    try
                    {
                        if (collection.Id != 0)
                        {
                            var collectionEntity = _libraryManager.GetItemById(collection.Id);
                            if (collectionEntity != null && collectionEntity.ProviderIds != null && collectionEntity.ProviderIds.Count > 0)
                            {
                                foreach (var providerId in collectionEntity.ProviderIds)
                                {
                                    var key = providerId.Key.ToLowerInvariant();
                                    writer.WriteAttributeString($"{key}colid", providerId.Value);
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Debug($"Could not get provider IDs for collection {collection.Name}: {ex.Message}");
                    }
                    
                    writer.WriteElementString("name", collection.Name);
                    
                    try
                    {
                        if (collection.Id != 0)
                        {
                            var collectionEntity = _libraryManager.GetItemById(collection.Id);
                            if (collectionEntity != null && !string.IsNullOrEmpty(collectionEntity.Overview))
                            {
                                writer.WriteElementString("overview", collectionEntity.Overview);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Debug($"Could not get overview for collection {collection.Name}: {ex.Message}");
                    }
                    
                    writer.WriteEndElement(); 
                }
            }
        }

        private void WriteProviderIdFields(XmlWriter writer, BaseItem item)
        {
            if (item.ProviderIds == null || item.ProviderIds.Count == 0)
                return;

            
            if (item.ProviderIds.TryGetValue("TmdbCollection", out var collectionId))
            {
                writer.WriteElementString("tmdbsetid", collectionId);
            }
            
            if (item.ProviderIds.TryGetValue("Wikidata", out var wikidataId))
            {
                writer.WriteElementString("wikidataid", wikidataId);
            }
        }

        private void WriteChapters(XmlWriter writer, BaseItem item, PluginConfiguration config)
        {
            try
            {
                var video = item as Video;
                if (video == null)
                    return;

                var chapters = _itemRepository.GetChapters(item);
                if (chapters == null || chapters.Count == 0)
                    return;

                writer.WriteStartElement("chapters");

                foreach (var chapter in chapters)
                {
                    writer.WriteStartElement("chapter");
                    
                    if (!string.IsNullOrEmpty(chapter.Name))
                    {
                        writer.WriteElementString("name", chapter.Name);
                    }
                    
                    var startTime = TimeSpan.FromTicks(chapter.StartPositionTicks);
                    writer.WriteElementString("starttime", startTime.ToString(@"hh\:mm\:ss\.fff"));
                    
                    if (config.IntroSkipIncludeInNfo)
                    {
                        try
                        {
                            var markerTypeProp = chapter.GetType().GetProperty("MarkerType");
                            if (markerTypeProp != null && markerTypeProp.CanRead)
                            {
                                var markerType = markerTypeProp.GetValue(chapter)?.ToString();
                                if (!string.IsNullOrEmpty(markerType) && markerType != "None")
                                {
                                    writer.WriteElementString("markertype", markerType);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.Debug($"Could not read MarkerType for chapter: {ex.Message}");
                        }
                    }
                    
                    writer.WriteEndElement(); // chapter
                }

                writer.WriteEndElement(); // chapters
            }
            catch (Exception ex)
            {
                _logger.Debug($"Could not write chapter information for {item.Name}: {ex.Message}");
            }
        }

        private string SanitizeFileName(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                return "Unknown";
            }

            var invalidChars = Path.GetInvalidFileNameChars();
            var sanitized = string.Join("_", fileName.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries));
            
            // Also replace some additional characters that might cause issues
            sanitized = sanitized.Replace(":", "_").Replace("?", "_").Replace("*", "_");
            
            return string.IsNullOrWhiteSpace(sanitized) ? "Unknown" : sanitized.TrimEnd('.');
        }
    }

    public class Utf8StringWriter : StringWriter
    {
        public override Encoding Encoding => Encoding.UTF8;
    }

    public class ExportResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public int ItemsProcessed { get; set; }
        public int TotalItems { get; set; }
        public List<string> Errors { get; set; } = new List<string>();
    }
}
