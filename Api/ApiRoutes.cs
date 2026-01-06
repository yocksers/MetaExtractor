namespace MetaExtractor.Api
{
    public static class ApiRoutes
    {
        public const string ExportMetadata = "/MetadataExporter/Export";
        public const string GetLibraries = "/MetadataExporter/Libraries";
        public const string GetProgress = "/MetadataExporter/Progress";
        public const string BackupIntroSkip = "/MetadataExporter/IntroSkip/Backup";
        public const string RestoreIntroSkip = "/MetadataExporter/IntroSkip/Restore";
        public const string GetSeries = "/MetadataExporter/IntroSkip/Series";
        public const string GetIntroSkipProgress = "/MetadataExporter/IntroSkip/Progress";
        public const string ExportForMigration = "/MetadataExporter/IntroSkip/ExportMigration";
        public const string ImportFromMigration = "/MetadataExporter/IntroSkip/ImportMigration";
    }
}