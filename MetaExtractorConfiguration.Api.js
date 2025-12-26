define([], function () {
    'use strict';

    var pluginId = "7f8e9d1c-2b3a-4e5f-a6b7-c8d9e0f1a2b3";

    return {
        getLibraries: function() {
            return fetch(ApiClient.getUrl('/MetadataExporter/Libraries'), {
                headers: { 'X-Emby-Token': ApiClient.accessToken() }
            }).then(function(response) { return response.json(); });
        },

        getPluginConfiguration: function() {
            return ApiClient.getPluginConfiguration(pluginId);
        },

        updatePluginConfiguration: function(config) {
            return ApiClient.updatePluginConfiguration(pluginId, config);
        },

        exportMetadata: function() {
            var url = ApiClient.getUrl('/MetadataExporter/Export');
            return fetch(url, {
                method: 'POST',
                headers: {
                    'X-Emby-Token': ApiClient.accessToken()
                }
            });
        },

        getExportProgress: function() {
            return fetch(ApiClient.getUrl('/MetadataExporter/Progress'), {
                headers: { 'X-Emby-Token': ApiClient.accessToken() }
            }).then(function(response) { return response.json(); });
        },

        getIntroSkipSeries: function() {
            return fetch(ApiClient.getUrl('/MetadataExporter/IntroSkip/Series'), {
                headers: { 'X-Emby-Token': ApiClient.accessToken() }
            }).then(function(response) { return response.json(); });
        },

        getIntroSkipProgress: function() {
            return fetch(ApiClient.getUrl('/MetadataExporter/IntroSkip/Progress'), {
                headers: { 'X-Emby-Token': ApiClient.accessToken() }
            }).then(function(response) { return response.json(); });
        },

        backupIntroSkip: function() {
            return fetch(ApiClient.getUrl('/MetadataExporter/IntroSkip/Backup'), {
                method: 'POST',
                headers: {
                    'X-Emby-Token': ApiClient.accessToken(),
                    'Content-Type': 'application/json'
                }
            });
        },

        restoreIntroSkip: function(filePath) {
            return fetch(ApiClient.getUrl('/MetadataExporter/IntroSkip/Restore'), {
                method: 'POST',
                headers: {
                    'X-Emby-Token': ApiClient.accessToken(),
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify({ FilePath: filePath })
            });
        },

        exportMigration: function(exportPath) {
            return fetch(ApiClient.getUrl('/MetadataExporter/IntroSkip/ExportMigration'), {
                method: 'POST',
                headers: {
                    'X-Emby-Token': ApiClient.accessToken(),
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify({ ExportPath: exportPath })
            });
        },

        importMigration: function(importPath, overwriteExisting) {
            return fetch(ApiClient.getUrl('/MetadataExporter/IntroSkip/ImportMigration'), {
                method: 'POST',
                headers: {
                    'X-Emby-Token': ApiClient.accessToken(),
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify({ 
                    ImportPath: importPath,
                    OverwriteExisting: overwriteExisting
                })
            });
        }
    };
});
