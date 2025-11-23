define(['loading', 'emby-input', 'emby-checkbox', 'emby-button'], function (loading) {
    'use strict';

    var pluginId = "7f8e9d1c-2b3a-4e5f-a6b7-c8d9e0f1a2b3";

    function View(view, params) {
        this.view = view;
        this.params = params;
        this.libraries = [];
        this.selectedItems = [];
        this.progressInterval = null;
    }

    View.prototype.loadConfiguration = function () {
        var self = this;
        loading.show();

        Promise.all([
            ApiClient.getPluginConfiguration(pluginId),
            fetch(ApiClient.getUrl('/MetadataExporter/Libraries'), {
                headers: { 'X-Emby-Token': ApiClient.accessToken() }
            }).then(function(response) { return response.json(); })
        ]).then(function (results) {
            self.config = results[0];
            self.libraries = results[1];

            self.selectedItems = self.config.SelectedItemIds || [];

            self.renderLibraries();
            self.populateLibraryPicker();

            var selectionMode = self.config.SelectionMode || 'library';
            view.querySelector('input[name="selectionMode"][value="' + selectionMode + '"]').checked = true;
            self.toggleSelectionMode(selectionMode);

            view.querySelector('#exportArtwork').checked = self.config.ExportArtwork !== false;
            view.querySelector('#exportNfo').checked = self.config.ExportNfo !== false;
            view.querySelector('#overwriteNfo').checked = self.config.OverwriteNfo !== false;
            view.querySelector('#overwriteArtwork').checked = self.config.OverwriteArtwork !== false;
            view.querySelector('#dryRun').checked = self.config.DryRun === true;
            view.querySelector('#useCustomExportPath').checked = self.config.UseCustomExportPath === true;
            view.querySelector('#customExportPath').value = self.config.CustomExportPath || '';
            view.querySelector('#useHardlinks').checked = self.config.UseHardlinks !== false; 
            
            self.toggleCustomPathInput();
            
            view.querySelector('#exportPoster').checked = self.config.ExportPoster !== false;
            view.querySelector('#exportBackdrop').checked = self.config.ExportBackdrop !== false;
            view.querySelector('#exportLogo').checked = self.config.ExportLogo !== false;
            view.querySelector('#exportBanner').checked = self.config.ExportBanner !== false;
            view.querySelector('#exportThumb').checked = self.config.ExportThumb !== false;
            view.querySelector('#exportArt').checked = self.config.ExportArt !== false;
            view.querySelector('#exportDisc').checked = self.config.ExportDisc !== false;

            view.querySelector('#nfoIncludePlot').checked = self.config.NfoIncludePlot !== false;
            view.querySelector('#nfoIncludeTitles').checked = self.config.NfoIncludeTitles !== false;
            view.querySelector('#nfoIncludeActors').checked = self.config.NfoIncludeActors !== false;
            view.querySelector('#nfoIncludeDirectors').checked = self.config.NfoIncludeDirectors !== false;
            view.querySelector('#nfoIncludeRating').checked = self.config.NfoIncludeRating !== false;
            view.querySelector('#nfoIncludeYear').checked = self.config.NfoIncludeYear !== false;
            view.querySelector('#nfoIncludeMpaa').checked = self.config.NfoIncludeMpaa !== false;
            view.querySelector('#nfoIncludeGenres').checked = self.config.NfoIncludeGenres !== false;
            view.querySelector('#nfoIncludeStudios').checked = self.config.NfoIncludeStudios !== false;
            view.querySelector('#nfoIncludeRuntime').checked = self.config.NfoIncludeRuntime !== false;
            view.querySelector('#nfoIncludeTagline').checked = self.config.NfoIncludeTagline !== false;
            view.querySelector('#nfoIncludeCountries').checked = self.config.NfoIncludeCountries !== false;
            view.querySelector('#nfoIncludeProviderIds').checked = self.config.NfoIncludeProviderIds !== false;
            view.querySelector('#nfoIncludeTags').checked = self.config.NfoIncludeTags !== false;
            view.querySelector('#nfoIncludeDates').checked = self.config.NfoIncludeDates !== false;
            view.querySelector('#nfoIncludeTrailers').checked = self.config.NfoIncludeTrailers !== false;
            view.querySelector('#nfoIncludeDetailedRatings').checked = self.config.NfoIncludeDetailedRatings !== false;
            view.querySelector('#nfoIncludeCertification').checked = self.config.NfoIncludeCertification !== false;
            view.querySelector('#nfoIncludeFanart').checked = self.config.NfoIncludeFanart !== false;
            view.querySelector('#nfoIncludeCollections').checked = self.config.NfoIncludeCollections !== false;
            view.querySelector('#nfoIncludeUniqueIds').checked = self.config.NfoIncludeUniqueIds !== false;
            view.querySelector('#nfoIncludeWriters').checked = self.config.NfoIncludeWriters !== false;
            view.querySelector('#nfoIncludeChapters').checked = self.config.NfoIncludeChapters !== false; // Default true

            loading.hide();
        }).catch(function(error) {
            console.error('Error loading configuration:', error);
            loading.hide();
            Dashboard.alert('Failed to load configuration');
        });
    };

    View.prototype.renderLibraries = function () {
        var self = this;
        var libraryList = view.querySelector('#libraryList');
        libraryList.innerHTML = '';

        self.libraries.forEach(function (library) {
            var isEnabled = self.config.EnabledLibraryIds && 
                           self.config.EnabledLibraryIds.indexOf(library.Id) !== -1;

            var item = document.createElement('div');
            item.className = 'library-item';
            item.innerHTML = 
                '<label class="emby-checkbox-label">' +
                '<input is="emby-checkbox" type="checkbox" id="lib-' + library.Id + '" name="lib-' + library.Id + '" ' + 
                (isEnabled ? 'checked' : '') + ' />' +
                '<span>' + library.Name + '</span>' +
                '</label>';
            
            libraryList.appendChild(item);
        });
    };

    View.prototype.populateLibraryPicker = function () {
        var self = this;
        var libraryPicker = view.querySelector('#libraryPicker');
        
        self.libraries.forEach(function (library) {
            var option = document.createElement('option');
            option.value = library.Id;
            option.textContent = library.Name;
            libraryPicker.appendChild(option);
        });
    };

    View.prototype.toggleSelectionMode = function (mode) {
        var self = this;
        var libraryContainer = view.querySelector('#librarySelectionContainer');
        var itemContainer = view.querySelector('#itemSelectionContainer');

        if (mode === 'library') {
            libraryContainer.style.display = 'block';
            itemContainer.style.display = 'none';
            self.selectedItems = [];
            view.querySelector('#selectedItemCount').textContent = '0';
            var itemBrowser = view.querySelector('#itemBrowser');
            if (itemBrowser) {
                itemBrowser.style.display = 'none';
            }
        } else {
            libraryContainer.style.display = 'none';
            itemContainer.style.display = 'block';
            self.libraries.forEach(function (library) {
                var checkbox = view.querySelector('#lib-' + library.Id);
                if (checkbox) {
                    checkbox.checked = false;
                }
            });
        }
    };

    View.prototype.browseItems = function () {
        var self = this;
        var libraryId = view.querySelector('#libraryPicker').value;
        
        if (!libraryId) {
            Dashboard.alert('Please select a library first');
            return;
        }

        self.selectedItems = [];
        view.querySelector('#selectedItemCount').textContent = '0';

        loading.show();
        var itemBrowser = view.querySelector('#itemBrowser');
        var itemList = view.querySelector('#itemList');

        ApiClient.getItems(ApiClient.getCurrentUserId(), {
            ParentId: libraryId,
            Recursive: true,
            Fields: 'Path',
            IncludeItemTypes: 'Movie,Series',
            SortBy: 'SortName'
        }).then(function (result) {
            itemList.innerHTML = '';
            
            result.Items.forEach(function (item) {
                var isSelected = self.selectedItems.indexOf(item.Id) !== -1;
                
                var itemDiv = document.createElement('div');
                itemDiv.className = 'item-entry';
                itemDiv.innerHTML = 
                    '<label class="emby-checkbox-label">' +
                    '<input is="emby-checkbox" type="checkbox" data-item-id="' + item.Id + '" ' + 
                    (isSelected ? 'checked' : '') + ' />' +
                    '<span>' + item.Name + ' (' + item.Type + ')</span>' +
                    '</label>';
                
                var checkbox = itemDiv.querySelector('input');
                checkbox.addEventListener('change', function () {
                    self.toggleItemSelection(item.Id, this.checked);
                });
                
                itemList.appendChild(itemDiv);
            });

            itemBrowser.style.display = 'block';
            loading.hide();
        }).catch(function (error) {
            console.error('Error loading items:', error);
            loading.hide();
            Dashboard.alert('Failed to load items');
        });
    };

    View.prototype.toggleItemSelection = function (itemId, isSelected) {
        var self = this;
        var index = self.selectedItems.indexOf(itemId);
        
        if (isSelected && index === -1) {
            self.selectedItems.push(itemId);
        } else if (!isSelected && index !== -1) {
            self.selectedItems.splice(index, 1);
        }
        
        view.querySelector('#selectedItemCount').textContent = self.selectedItems.length;
    };

    View.prototype.toggleCustomPathInput = function () {
        var self = this;
        var useCustomPath = view.querySelector('#useCustomExportPath').checked;
        var pathContainer = view.querySelector('#customExportPathContainer');
        var hardlinksContainer = view.querySelector('#useHardlinksContainer');
        
        if (useCustomPath) {
            pathContainer.style.display = 'block';
            hardlinksContainer.style.display = 'block';
        } else {
            pathContainer.style.display = 'none';
            hardlinksContainer.style.display = 'none';
        }
    };

    View.prototype.submit = function () {
        var self = this;
        loading.show();

        var enabledLibraries = [];
        self.libraries.forEach(function (library) {
            var checkbox = view.querySelector('#lib-' + library.Id);
            if (checkbox && checkbox.checked) {
                enabledLibraries.push(library.Id);
            }
        });

        console.log('Saving enabled libraries:', enabledLibraries);

        var selectionMode = view.querySelector('input[name="selectionMode"]:checked').value;

        self.config.SelectionMode = selectionMode;
        if (selectionMode === 'library') {
            self.config.EnabledLibraryIds = enabledLibraries;
            self.config.SelectedItemIds = []; 
        } else {
            self.config.EnabledLibraryIds = []; 
            self.config.SelectedItemIds = self.selectedItems;
        }
        self.config.ExportArtwork = view.querySelector('#exportArtwork').checked;
        self.config.ExportNfo = view.querySelector('#exportNfo').checked;
        self.config.OverwriteNfo = view.querySelector('#overwriteNfo').checked;
        self.config.OverwriteArtwork = view.querySelector('#overwriteArtwork').checked;
        self.config.DryRun = view.querySelector('#dryRun').checked;
        self.config.UseCustomExportPath = view.querySelector('#useCustomExportPath').checked;
        self.config.CustomExportPath = view.querySelector('#customExportPath').value;
        self.config.UseHardlinks = view.querySelector('#useHardlinks').checked;
        self.config.ExportPoster = view.querySelector('#exportPoster').checked;
        self.config.ExportBackdrop = view.querySelector('#exportBackdrop').checked;
        self.config.ExportLogo = view.querySelector('#exportLogo').checked;
        self.config.ExportBanner = view.querySelector('#exportBanner').checked;
        self.config.ExportThumb = view.querySelector('#exportThumb').checked;
        self.config.ExportArt = view.querySelector('#exportArt').checked;

        self.config.NfoIncludePlot = view.querySelector('#nfoIncludePlot').checked;
        self.config.NfoIncludeTitles = view.querySelector('#nfoIncludeTitles').checked;
        self.config.NfoIncludeActors = view.querySelector('#nfoIncludeActors').checked;
        self.config.NfoIncludeDirectors = view.querySelector('#nfoIncludeDirectors').checked;
        self.config.NfoIncludeRating = view.querySelector('#nfoIncludeRating').checked;
        self.config.NfoIncludeYear = view.querySelector('#nfoIncludeYear').checked;
        self.config.NfoIncludeMpaa = view.querySelector('#nfoIncludeMpaa').checked;
        self.config.NfoIncludeGenres = view.querySelector('#nfoIncludeGenres').checked;
        self.config.NfoIncludeStudios = view.querySelector('#nfoIncludeStudios').checked;
        self.config.NfoIncludeRuntime = view.querySelector('#nfoIncludeRuntime').checked;
        self.config.NfoIncludeTagline = view.querySelector('#nfoIncludeTagline').checked;
        self.config.NfoIncludeCountries = view.querySelector('#nfoIncludeCountries').checked;
        self.config.NfoIncludeProviderIds = view.querySelector('#nfoIncludeProviderIds').checked;
        self.config.NfoIncludeTags = view.querySelector('#nfoIncludeTags').checked;
        self.config.NfoIncludeDates = view.querySelector('#nfoIncludeDates').checked;
        self.config.NfoIncludeTrailers = view.querySelector('#nfoIncludeTrailers').checked;
        self.config.NfoIncludeDetailedRatings = view.querySelector('#nfoIncludeDetailedRatings').checked;
        self.config.NfoIncludeCertification = view.querySelector('#nfoIncludeCertification').checked;
        self.config.NfoIncludeFanart = view.querySelector('#nfoIncludeFanart').checked;
        self.config.NfoIncludeCollections = view.querySelector('#nfoIncludeCollections').checked;
        self.config.NfoIncludeUniqueIds = view.querySelector('#nfoIncludeUniqueIds').checked;
        self.config.NfoIncludeWriters = view.querySelector('#nfoIncludeWriters').checked;
        self.config.NfoIncludeChapters = view.querySelector('#nfoIncludeChapters').checked;

        ApiClient.updatePluginConfiguration(pluginId, self.config).then(function () {
            loading.hide();
            Dashboard.alert('Settings saved successfully.');
        }).catch(function(error) {
            loading.hide();
            Dashboard.alert('Failed to save settings');
        });
    };

    View.prototype.exportMetadata = function () {
        var self = this;
        var exportBtn = view.querySelector('#exportBtn');
        var loadingSpinner = view.querySelector('#loadingSpinner');
        var resultsContainer = view.querySelector('#resultsContainer');

        var selectionMode = view.querySelector('input[name="selectionMode"]:checked').value;

        var enabledLibraries = [];
        if (selectionMode === 'library') {
            self.libraries.forEach(function (library) {
                var checkbox = view.querySelector('#lib-' + library.Id);
                if (checkbox && checkbox.checked) {
                    enabledLibraries.push(library.Id);
                }
            });

            if (enabledLibraries.length === 0) {
                Dashboard.alert('Please select at least one library to export.');
                return;
            }
        } else {
            if (self.selectedItems.length === 0) {
                Dashboard.alert('Please select at least one item to export.');
                return;
            }
        }

        console.log('Selection mode:', selectionMode);
        console.log('Libraries selected for export:', enabledLibraries);
        console.log('Items selected for export:', self.selectedItems.length);

        self.config.SelectionMode = selectionMode;
        if (selectionMode === 'library') {
            self.config.EnabledLibraryIds = enabledLibraries;
            self.config.SelectedItemIds = []; 
        } else {
            self.config.EnabledLibraryIds = []; 
            self.config.SelectedItemIds = self.selectedItems;
        }
        self.config.ExportArtwork = view.querySelector('#exportArtwork').checked;
        self.config.ExportNfo = view.querySelector('#exportNfo').checked;
        self.config.OverwriteNfo = view.querySelector('#overwriteNfo').checked;
        self.config.OverwriteArtwork = view.querySelector('#overwriteArtwork').checked;
        self.config.UseCustomExportPath = view.querySelector('#useCustomExportPath').checked;
        self.config.CustomExportPath = view.querySelector('#customExportPath').value;
        self.config.UseHardlinks = view.querySelector('#useHardlinks').checked;
        self.config.ExportPoster = view.querySelector('#exportPoster').checked;
        self.config.ExportBackdrop = view.querySelector('#exportBackdrop').checked;
        self.config.ExportLogo = view.querySelector('#exportLogo').checked;
        self.config.ExportBanner = view.querySelector('#exportBanner').checked;
        self.config.ExportThumb = view.querySelector('#exportThumb').checked;
        self.config.ExportArt = view.querySelector('#exportArt').checked;
        self.config.ExportDisc = view.querySelector('#exportDisc').checked;

        self.config.NfoIncludePlot = view.querySelector('#nfoIncludePlot').checked;
        self.config.NfoIncludeTitles = view.querySelector('#nfoIncludeTitles').checked;
        self.config.NfoIncludeActors = view.querySelector('#nfoIncludeActors').checked;
        self.config.NfoIncludeDirectors = view.querySelector('#nfoIncludeDirectors').checked;
        self.config.NfoIncludeRating = view.querySelector('#nfoIncludeRating').checked;
        self.config.NfoIncludeYear = view.querySelector('#nfoIncludeYear').checked;
        self.config.NfoIncludeMpaa = view.querySelector('#nfoIncludeMpaa').checked;
        self.config.NfoIncludeGenres = view.querySelector('#nfoIncludeGenres').checked;
        self.config.NfoIncludeStudios = view.querySelector('#nfoIncludeStudios').checked;
        self.config.NfoIncludeRuntime = view.querySelector('#nfoIncludeRuntime').checked;
        self.config.NfoIncludeTagline = view.querySelector('#nfoIncludeTagline').checked;
        self.config.NfoIncludeCountries = view.querySelector('#nfoIncludeCountries').checked;
        self.config.NfoIncludeProviderIds = view.querySelector('#nfoIncludeProviderIds').checked;
        self.config.NfoIncludeTags = view.querySelector('#nfoIncludeTags').checked;
        self.config.NfoIncludeDates = view.querySelector('#nfoIncludeDates').checked;
        self.config.NfoIncludeTrailers = view.querySelector('#nfoIncludeTrailers').checked;
        self.config.NfoIncludeDetailedRatings = view.querySelector('#nfoIncludeDetailedRatings').checked;
        self.config.NfoIncludeCertification = view.querySelector('#nfoIncludeCertification').checked;
        self.config.NfoIncludeFanart = view.querySelector('#nfoIncludeFanart').checked;
        self.config.NfoIncludeCollections = view.querySelector('#nfoIncludeCollections').checked;
        self.config.NfoIncludeUniqueIds = view.querySelector('#nfoIncludeUniqueIds').checked;
        self.config.NfoIncludeWriters = view.querySelector('#nfoIncludeWriters').checked;
        self.config.NfoIncludeChapters = view.querySelector('#nfoIncludeChapters').checked;

        exportBtn.disabled = true;
        loadingSpinner.classList.add('active');
        resultsContainer.classList.remove('visible');
        
        var progressText = view.querySelector('#progressText');
        if (progressText) {
            progressText.textContent = 'Initializing export...';
        }
        
        self.startProgressPolling();

        ApiClient.updatePluginConfiguration(pluginId, self.config).then(function () {
            console.log('Configuration saved, starting export...');
            
            var url = ApiClient.getUrl('/MetadataExporter/Export');

            return fetch(url, {
                method: 'POST',
                headers: {
                    'X-Emby-Token': ApiClient.accessToken()
                }
            });
        }).then(function (response) {
            if (!response.ok) {
                throw new Error('HTTP error ' + response.status);
            }
            return response.json();
        }).then(function (result) {
            loadingSpinner.classList.remove('active');
            exportBtn.disabled = false;
            self.stopProgressPolling();
            
            fetch(ApiClient.getUrl('/MetadataExporter/Progress'), {
                headers: { 'X-Emby-Token': ApiClient.accessToken() }
            })
            .then(function (response) { return response.json(); })
            .then(function (progress) {
                if (progress.ExportLog && progress.ExportLog.length > 0) {
                    self.updateDryRunLog(progress.ExportLog);
                }
            })
            .catch(function (error) {
                console.error('Error fetching final progress:', error);
            });

            if (result.Success) {
                self.displayResults(result);
            } else {
                Dashboard.alert('Error: ' + result.Message);
            }
        }).catch(function (error) {
            loadingSpinner.classList.remove('active');
            exportBtn.disabled = false;
            self.stopProgressPolling();
            console.error('Export error:', error);
            Dashboard.alert('Failed to export metadata: ' + (error.message || 'Unknown error'));
        });
    };

    View.prototype.startProgressPolling = function () {
        var self = this;
        var progressText = view.querySelector('#progressText');
        var progressBar = view.querySelector('#progressBar');
        
        self.progressInterval = setInterval(function () {
            fetch(ApiClient.getUrl('/MetadataExporter/Progress'), {
                headers: { 'X-Emby-Token': ApiClient.accessToken() }
            })
            .then(function (response) { return response.json(); })
            .then(function (progress) {
                if (progress.IsExporting) {
                    var message = progress.Percentage + '% - Processing: ' + progress.CurrentItem;
                    message += ' (' + progress.ProcessedItems + '/' + progress.TotalItems + ')';
                    if (progressText) {
                        progressText.textContent = message;
                    }
                    if (progressBar) {
                        progressBar.style.width = progress.Percentage + '%';
                    }
                    
                    if (progress.ExportLog && progress.ExportLog.length > 0) {
                        self.updateDryRunLog(progress.ExportLog);
                    }
                }
            })
            .catch(function (error) {
                console.error('Error fetching progress:', error);
            });
        }, 500); 
    };

    View.prototype.stopProgressPolling = function () {
        var self = this;
        if (self.progressInterval) {
            clearInterval(self.progressInterval);
            self.progressInterval = null;
        }
    };
    
    View.prototype.updateDryRunLog = function (logEntries) {
        console.log('updateDryRunLog called with', logEntries ? logEntries.length : 0, 'entries');
        var exportLogList = view.querySelector('#exportLogList');
        var exportLogCount = view.querySelector('#exportLogCount');
        
        if (!exportLogList) {
            console.error('exportLogList element not found');
            return;
        }
        
        if (!exportLogCount) {
            console.error('exportLogCount element not found');
            return;
        }
        
        if (logEntries && logEntries.length > 0) {
            exportLogCount.textContent = logEntries.length;
            
            var html = '';
            for (var i = 0; i < logEntries.length; i++) {
                html += '<div class="dry-run-log-entry">' + logEntries[i] + '</div>';
            }
            exportLogList.innerHTML = html;
            
            console.log('Export log updated with', logEntries.length, 'entries');
            
            exportLogList.scrollTop = exportLogList.scrollHeight;
        } else {
            console.log('No log entries to display');
        }
    };
    
    View.prototype.switchTab = function (tabId) {
        view.querySelectorAll('.tab-content').forEach(function (tab) {
            tab.classList.remove('active');
        });
        view.querySelectorAll('.tab-button').forEach(function (button) {
            button.classList.remove('active');
        });
        
        var selectedTab = view.querySelector('#' + tabId);
        var selectedButton = view.querySelector('.tab-button[data-tab="' + tabId + '"]');
        
        if (selectedTab) {
            selectedTab.classList.add('active');
        }
        if (selectedButton) {
            selectedButton.classList.add('active');
        }
    };
    
    View.prototype.downloadLog = function () {
        var exportLogList = view.querySelector('#exportLogList');
        if (!exportLogList) return;
        
        var logText = '';
        var entries = exportLogList.querySelectorAll('.dry-run-log-entry');
        
        if (entries.length === 0) {
            Dashboard.alert('No log entries to export.');
            return;
        }
        
        logText = 'Metadata Exporter Log\n';
        logText += 'Generated: ' + new Date().toLocaleString() + '\n';
        logText += '='.repeat(80) + '\n\n';
        
        entries.forEach(function (entry) {
            logText += entry.textContent + '\n';
        });
        
        var blob = new Blob([logText], { type: 'text/plain' });
        var url = window.URL.createObjectURL(blob);
        var a = document.createElement('a');
        a.href = url;
        a.download = 'metadata-export-log-' + new Date().toISOString().replace(/:/g, '-') + '.txt';
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
        window.URL.revokeObjectURL(url);
        
        Dashboard.alert('Log exported successfully.');
    };
    
    View.prototype.clearLog = function () {
        var exportLogList = view.querySelector('#exportLogList');
        var exportLogCount = view.querySelector('#exportLogCount');
        
        if (exportLogList) {
            exportLogList.innerHTML = '<div style=\"color: #666; text-align: center; padding: 2em;\">No export operations yet. Run an export to see logs here.</div>';
        }
        if (exportLogCount) {
            exportLogCount.textContent = '0';
        }
        
        Dashboard.alert('Log cleared.');
    };

    View.prototype.displayResults = function (result) {
        var resultsContainer = view.querySelector('#resultsContainer');
        var resultsHeader = view.querySelector('#resultsHeader');
        var resultsTitle = view.querySelector('#resultsTitle');
        var resultsStats = view.querySelector('#resultsStats');
        var errorList = view.querySelector('#errorList');

        resultsContainer.classList.add('visible');

        if (result.Success) {
            resultsHeader.className = 'results-header success';
            resultsTitle.textContent = '✓ Export Complete';
            
            var statsHtml = '<strong>Items Processed:</strong> ' + result.ItemsProcessed.toLocaleString();
            if (result.TotalItems && result.TotalItems > 0) {
                statsHtml += ' out of ' + result.TotalItems.toLocaleString() + ' total items';
            }
            resultsStats.innerHTML = statsHtml;

            if (result.Errors && result.Errors.length > 0) {
                errorList.innerHTML = '<h4 style="color: #ff9800;">Warnings:</h4>';
                result.Errors.forEach(function (error) {
                    var errorDiv = document.createElement('div');
                    errorDiv.style.padding = '0.5em';
                    errorDiv.style.color = '#ffcc00';
                    errorDiv.textContent = error;
                    errorList.appendChild(errorDiv);
                });
            } else {
                errorList.innerHTML = '<p style="text-align: center; padding: 1em; color: #3ad83a;">All metadata exported successfully!</p>';
            }
        } else {
            resultsHeader.className = 'results-header error';
            resultsTitle.textContent = '✗ Export Failed';
            resultsStats.innerHTML = '<strong>Error:</strong> ' + escapeHtml(result.Message);
            errorList.innerHTML = '';
        }

        resultsContainer.scrollIntoView({ behavior: 'smooth', block: 'start' });
    };

    function escapeHtml(text) {
        var map = {
            '&': '&amp;',
            '<': '&lt;',
            '>': '&gt;',
            '"': '&quot;',
            "'": '&#039;'
        };
        return text.replace(/[&<>"']/g, function(m) { return map[m]; });
    }

    var view;

    return function (viewInstance, params) {
        view = viewInstance;
        var instance = new View(view, params);

        view.addEventListener('viewshow', function () {
            instance.loadConfiguration();
        });

        view.addEventListener('viewhide', function () {
            instance.stopProgressPolling();
        });

        view.querySelector('form').addEventListener('submit', function (e) {
            instance.submit();
            e.preventDefault();
            return false;
        });

        view.querySelector('#exportBtn').addEventListener('click', function () {
            instance.exportMetadata();
        });

        view.querySelectorAll('input[name="selectionMode"]').forEach(function (radio) {
            radio.addEventListener('change', function () {
                instance.toggleSelectionMode(this.value);
            });
        });

        view.querySelector('#browseItemsBtn').addEventListener('click', function () {
            instance.browseItems();
        });
        
        view.querySelectorAll('.tab-button').forEach(function (button) {
            button.addEventListener('click', function () {
                var tabId = this.getAttribute('data-tab');
                instance.switchTab(tabId);
            });
        });
        
        view.querySelector('#exportLogBtn').addEventListener('click', function () {
            instance.downloadLog();
        });
        
        view.querySelector('#clearLogBtn').addEventListener('click', function () {
            instance.clearLog();
        });

        view.querySelector('#selectAllNfo').addEventListener('click', function () {
            var nfoCheckboxes = view.querySelectorAll('[id^="nfoInclude"]');
            nfoCheckboxes.forEach(function(checkbox) {
                checkbox.checked = true;
            });
        });
        
        view.querySelector('#selectNoneNfo').addEventListener('click', function () {
            var nfoCheckboxes = view.querySelectorAll('[id^="nfoInclude"]');
            nfoCheckboxes.forEach(function(checkbox) {
                checkbox.checked = false;
            });
        });

        // Custom export path toggle
        view.querySelector('#useCustomExportPath').addEventListener('change', function () {
            instance.toggleCustomPathInput();
        });

        // Browse for custom export path
        view.querySelector('#browseExportPath').addEventListener('click', function () {
            require(['directorybrowser'], function (directoryBrowser) {
                var picker = new directoryBrowser();
                picker.show({
                    callback: function (path) {
                        if (path) {
                            view.querySelector('#customExportPath').value = path;
                        }
                        picker.close();
                    },
                    header: 'Select Export Folder',
                    instruction: 'Choose a folder where metadata and artwork will be exported'
                });
            });
        });

        return instance;
    };
});
