using MediaBrowser.Model.Plugins;
using System;
using System.Collections.Generic;

namespace MetaExtractor
{
    public class PluginConfiguration : BasePluginConfiguration
    {
        public string ConfigurationVersion { get; set; } = Guid.NewGuid().ToString();
        
        public string SelectionMode { get; set; } = "library";
        
        public List<string> EnabledLibraryIds { get; set; } = new List<string>();
        
        public List<string> SelectedItemIds { get; set; } = new List<string>();
        
        public bool ExportArtwork { get; set; } = true;
        public bool ExportNfo { get; set; } = true;
        
        public bool ExportPoster { get; set; } = true;
        public bool ExportBackdrop { get; set; } = true;
        public bool ExportLogo { get; set; } = true;
        public bool ExportBanner { get; set; } = true;
        public bool ExportThumb { get; set; } = true;
        public bool ExportArt { get; set; } = true;
        public bool ExportDisc { get; set; } = true;
        
        public string NfoFormat { get; set; } = "Kodi";
        
        public bool OverwriteNfo { get; set; } = false;
        public bool OverwriteArtwork { get; set; } = false;
        
        public bool UseCustomExportPath { get; set; } = false;
        public string CustomExportPath { get; set; } = string.Empty;
        public bool UseHardlinks { get; set; } = true;
        
        public bool DryRun { get; set; } = false;
        
        // NFO Metadata Fields
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
        
        public DateTime LastExportTime { get; set; } = DateTime.MinValue;
        public int LastExportedItemsCount { get; set; } = 0;
    }
}
