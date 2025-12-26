using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Persistence;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Drawing;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using MetaExtractor.Services;

namespace MetaExtractor
{
    public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages, IHasThumbImage, IServerEntryPoint
    {
        private readonly ILogger _logger;
        private readonly ILibraryManager _libraryManager;
        private readonly IProviderManager _providerManager;
        private readonly IItemRepository _itemRepository;
        public static Plugin? Instance { get; private set; }
        public static MetadataExportService? MetadataExporter { get; private set; }
        public static IntroSkipBackupService? IntroSkipBackupService { get; private set; }
        
        public static ExportProgress CurrentProgress { get; set; } = new ExportProgress();
        public static IntroSkipProgress IntroSkipProgress { get; set; } = new IntroSkipProgress();

        public override string Name => "Metadata Exporter";
        public override string Description => "Exports artwork and metadata from Emby's database to media folders as images and NFO files.";
        public override Guid Id => Guid.Parse("7f8e9d1c-2b3a-4e5f-a6b7-c8d9e0f1a2b3");

        public Plugin(IApplicationPaths appPaths, IXmlSerializer xmlSerializer, ILogManager logManager,
                     ILibraryManager libraryManager, IProviderManager providerManager, IItemRepository itemRepository)
            : base(appPaths, xmlSerializer)
        {
            Instance = this;
            _logger = logManager.GetLogger(GetType().Name);
            _libraryManager = libraryManager;
            _providerManager = providerManager;
            _itemRepository = itemRepository;
        }

        public override void UpdateConfiguration(BasePluginConfiguration configuration)
        {
            if (configuration is PluginConfiguration newConfig)
            {
                if (string.IsNullOrEmpty(newConfig.ConfigurationVersion))
                {
                    newConfig.ConfigurationVersion = Guid.NewGuid().ToString();
                }

                this.Configuration = newConfig;

                _logger.Info("Metadata Exporter configuration updated and reloaded.");
            }

            base.UpdateConfiguration(configuration);
        }

        public IEnumerable<PluginPageInfo> GetPages()
        {
            return new[]
            {
                new PluginPageInfo
                {
                    Name = "MetaExtractorConfiguration",
                    EmbeddedResourcePath = GetType().Namespace + ".MetaExtractorConfiguration.html",
                },
                new PluginPageInfo
                {
                    Name = "MetaExtractorConfigurationjs",
                    EmbeddedResourcePath = GetType().Namespace + ".MetaExtractorConfiguration.js"
                },
                new PluginPageInfo
                {
                    Name = "MetaExtractorConfigurationApi",
                    EmbeddedResourcePath = GetType().Namespace + ".Configuration.MetaExtractorConfiguration.Api.js"
                },
                new PluginPageInfo
                {
                    Name = "MetaExtractorConfigurationUtils",
                    EmbeddedResourcePath = GetType().Namespace + ".Configuration.MetaExtractorConfiguration.Utils.js"
                },
                new PluginPageInfo
                {
                    Name = "MetaExtractorConfigurationExport",
                    EmbeddedResourcePath = GetType().Namespace + ".Configuration.MetaExtractorConfiguration.Export.js"
                },
                new PluginPageInfo
                {
                    Name = "MetaExtractorConfigurationIntroSkip",
                    EmbeddedResourcePath = GetType().Namespace + ".Configuration.MetaExtractorConfiguration.IntroSkip.js"
                }
            };
        }

        public void Run()
        {
            MetadataExporter = new MetadataExportService(_logger, _libraryManager, _providerManager, _itemRepository);
            IntroSkipBackupService = new IntroSkipBackupService(_logger, _libraryManager, _itemRepository);
            _logger.Info("Metadata Exporter plugin started successfully.");
        }

        public void Dispose()
        {
            CurrentProgress.IsExporting = false;
            CurrentProgress.CurrentItem = "Plugin stopped";
            
            MetadataExporter = null;
            IntroSkipBackupService = null;
            Instance = null;
            
            _logger.Info("Metadata Exporter plugin stopped.");
        }

        public Stream GetThumbImage()
        {
            var assembly = typeof(Plugin).GetTypeInfo().Assembly;
            var resourceName = typeof(Plugin).Namespace + ".Images.logo.jpg";
            return assembly.GetManifestResourceStream(resourceName) ?? Stream.Null;
        }

        public ImageFormat ThumbImageFormat => ImageFormat.Jpg;
    }
}