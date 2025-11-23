using MetaExtractor.Services;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Services;
using System.Collections.Generic;
using System.Linq;

namespace MetaExtractor.Api
{
    [Route(ApiRoutes.ExportMetadata, "POST", Summary = "Exports metadata and artwork to media folders.")]
    public class ExportMetadataRequest : IReturn<ExportResult> { }

    [Route(ApiRoutes.GetLibraries, "GET", Summary = "Gets all available libraries.")]
    public class GetLibrariesRequest : IReturn<List<LibraryInfo>> { }

    [Route(ApiRoutes.GetProgress, "GET", Summary = "Gets current export progress.")]
    public class GetProgressRequest : IReturn<ExportProgress> { }

    public class MetadataExporterApiService : IService
    {
        private readonly ILibraryManager _libraryManager;

        public MetadataExporterApiService(ILibraryManager libraryManager)
        {
            _libraryManager = libraryManager;
        }

        public ExportResult Post(ExportMetadataRequest request)
        {
            if (Plugin.MetadataExporter == null)
            {
                return new ExportResult
                {
                    Success = false,
                    Message = "Metadata export service is not initialized."
                };
            }

            if (Plugin.Instance == null)
            {
                return new ExportResult
                {
                    Success = false,
                    Message = "Plugin instance is not initialized."
                };
            }

            var config = Plugin.Instance.Configuration;
            
            var result = Plugin.MetadataExporter.ExportMetadata(config.EnabledLibraryIds, config, System.Threading.CancellationToken.None);

            if (result.Success)
            {
                config.LastExportTime = System.DateTime.Now;
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
    }

    public class LibraryInfo
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string CollectionType { get; set; } = string.Empty;
    }
}
