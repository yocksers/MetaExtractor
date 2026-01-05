using MediaBrowser.Model.Plugins;
using System;
using System.Collections.Generic;

namespace MetaExtractor
{
    public class PluginConfiguration : BasePluginConfiguration
    {
        public string ConfigurationVersion { get; set; } = string.Empty;
        
        public string SelectionMode { get; set; } = "library";
        
        public List<string> EnabledLibraryIds { get; set; } = new List<string>();
        
        public List<string> SelectedItemIds { get; set; } = new List<string>();
        
        public bool ExportArtwork { get; set; } = true;
        public bool ExportNfo { get; set; } = true;
        
        public bool ExportCollections { get; set; } = true;
        
        public bool ExportAllArtworkTypes { get; set; } = false;
        
        public bool ExportPoster { get; set; } = true;
        public bool ExportBackdrop { get; set; } = true;
        public bool ExportLogo { get; set; } = true;
        public bool ExportBanner { get; set; } = true;
        public bool ExportThumb { get; set; } = true;
        public bool ExportArt { get; set; } = true;
        public bool ExportDisc { get; set; } = true;
        
        public bool UseCustomArtworkNames { get; set; } = false;
        public string CustomPosterName { get; set; } = "poster";
        public string CustomFanartName { get; set; } = "fanart";
        public string CustomLogoName { get; set; } = "clearlogo";
        public string CustomBannerName { get; set; } = "banner";
        public string CustomThumbName { get; set; } = "landscape";
        public string CustomArtName { get; set; } = "clearart";
        public string CustomDiscName { get; set; } = "disc";
        
        public string NfoFormat { get; set; } = "Kodi";
        
        public bool OverwriteNfo { get; set; } = false;
        public bool OverwriteArtwork { get; set; } = false;
        
        public bool UseCustomExportPath { get; set; } = false;
        public string CustomExportPath { get; set; } = string.Empty;
        public bool UseHardlinks { get; set; } = true;
        
        public bool DryRun { get; set; } = false;
        
        private int _maxParallelTasks = 4;
        public int MaxParallelTasks 
        { 
            get => _maxParallelTasks; 
            set => _maxParallelTasks = Math.Max(1, Math.Min(16, value)); 
        }
        
        public bool NfoIncludePlot { get; set; } = true;
        public bool NfoIncludeTitles { get; set; } = true;
        public bool NfoIncludeActors { get; set; } = true;
        public bool NfoIncludeDirectors { get; set; } = true;
        public bool NfoIncludeRating { get; set; } = true;
        public bool NfoIncludeYear { get; set; } = true;
        public bool NfoIncludeMpaa { get; set; } = true;
        public bool NfoIncludeGenres { get; set; } = true;
        public bool NfoIncludeStudios { get; set; } = true;
        public bool NfoIncludeRuntime { get; set; } = true;
        public bool NfoIncludeTagline { get; set; } = true;
        public bool NfoIncludeCountries { get; set; } = true;
        public bool NfoIncludeProviderIds { get; set; } = true;
        public bool NfoIncludeTags { get; set; } = true;
        public bool NfoIncludeDates { get; set; } = true;
        public bool NfoIncludeTrailers { get; set; } = true;
        public bool NfoIncludeDetailedRatings { get; set; } = true;
        public bool NfoIncludeCertification { get; set; } = true;
        public bool NfoIncludeFanart { get; set; } = true;
        public bool NfoIncludeCollections { get; set; } = true;
        public bool NfoIncludeUniqueIds { get; set; } = true;
        public bool NfoIncludeWriters { get; set; } = true;
        public bool NfoIncludeChapters { get; set; } = true;
        
        public string IntroSkipSelectionMode { get; set; } = "library"; // library or individual
        public List<string> IntroSkipLibraryIds { get; set; } = new List<string>();
        public List<string> IntroSkipSelectedSeriesIds { get; set; } = new List<string>();
        public string IntroSkipBackupFilePath { get; set; } = string.Empty; // Full path to single backup file
        public bool IntroSkipIncludeInNfo { get; set; } = true;
        
        // Per-episode backup mode (saves JSON alongside episodes or in custom folder)
        public bool IntroSkipSavePerEpisode { get; set; } = false;
        public bool IntroSkipUseCustomFolder { get; set; } = false; // Save to custom folder instead of next to video files
        public string IntroSkipCustomFolderPath { get; set; } = string.Empty; // Path for custom folder backups
        
        // theTVDB Episode ID matching for portable intro skip data
        public bool IntroSkipUseTvdbMatching { get; set; } = true; // Use theTVDB episode IDs for matching (more portable)
        
        public bool IntroSkipRestoreFromScan { get; set; } = false;
        public List<string> IntroSkipScanFolderPaths { get; set; } = new List<string>();
        
        
        // Scheduled Task Settings (separate from manual export settings)
        public bool ScheduledTaskBackupNfo { get; set; } = true;
        public bool ScheduledTaskBackupIntroSkips { get; set; } = true;
        
        public DateTime LastExportTime { get; set; } = DateTime.MinValue;
        public int LastExportedItemsCount { get; set; } = 0;
    }
}
