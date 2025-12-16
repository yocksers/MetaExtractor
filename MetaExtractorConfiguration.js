define(['loading', 'emby-input', 'emby-checkbox', 'emby-button'], function (loading) {
    'use strict';

    var pluginId = "7f8e9d1c-2b3a-4e5f-a6b7-c8d9e0f1a2b3";

    function View(view, params) {
        this.view = view;
        this.params = params;
        this.libraries = [];
        this.selectedItems = [];
        this.progressInterval = null;
        this.saveTimeout = null;
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

            view.querySelector('#chkUseCustomArtworkNames').checked = self.config.UseCustomArtworkNames === true;
            view.querySelector('#txtCustomPosterName').value = self.config.CustomPosterName || 'poster';
            view.querySelector('#txtCustomFanartName').value = self.config.CustomFanartName || 'fanart';
            view.querySelector('#txtCustomLogoName').value = self.config.CustomLogoName || 'clearlogo';
            view.querySelector('#txtCustomBannerName').value = self.config.CustomBannerName || 'banner';
            view.querySelector('#txtCustomThumbName').value = self.config.CustomThumbName || 'landscape';
            view.querySelector('#txtCustomArtName').value = self.config.CustomArtName || 'clearart';
            view.querySelector('#txtCustomDiscName').value = self.config.CustomDiscName || 'disc';
            
            self.toggleCustomArtworkNames();

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

            // Intro Skip Backup settings
            view.querySelector('#chkEnableIntroSkipBackup').checked = self.config.EnableIntroSkipBackup === true;
            view.querySelector('#txtIntroSkipBackupFilePath').value = self.config.IntroSkipBackupFilePath || '';
            view.querySelector('#chkIntroSkipIncludeInNfo').checked = self.config.IntroSkipIncludeInNfo !== false;

            var introSkipMode = self.config.IntroSkipSelectionMode || 'library';
            view.querySelector('input[name="introSkipSelectionMode"][value="' + introSkipMode + '"]').checked = true;
            self.toggleIntroSkipSelectionMode(introSkipMode);

            view.querySelector('#introSkipSettings').style.display = 
                self.config.EnableIntroSkipBackup === true ? 'block' : 'none';

            // Load intro skip libraries and series after config and libraries are loaded
            self.loadIntroSkipLibraries();
            self.loadIntroSkipSeries();

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

    View.prototype.toggleCustomArtworkNames = function () {
        var self = this;
        var useCustomNames = view.querySelector('#chkUseCustomArtworkNames').checked;
        var namesContainer = view.querySelector('#customArtworkNamesContainer');
        
        if (useCustomNames) {
            namesContainer.style.display = 'block';
        } else {
            namesContainer.style.display = 'none';
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

        self.config.UseCustomArtworkNames = view.querySelector('#chkUseCustomArtworkNames').checked;
        self.config.CustomPosterName = view.querySelector('#txtCustomPosterName').value || 'poster';
        self.config.CustomFanartName = view.querySelector('#txtCustomFanartName').value || 'fanart';
        self.config.CustomLogoName = view.querySelector('#txtCustomLogoName').value || 'clearlogo';
        self.config.CustomBannerName = view.querySelector('#txtCustomBannerName').value || 'banner';
        self.config.CustomThumbName = view.querySelector('#txtCustomThumbName').value || 'landscape';
        self.config.CustomArtName = view.querySelector('#txtCustomArtName').value || 'clearart';
        self.config.CustomDiscName = view.querySelector('#txtCustomDiscName').value || 'disc';

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

        self.config.EnableIntroSkipBackup = view.querySelector('#chkEnableIntroSkipBackup').checked;
        self.config.IntroSkipBackupFilePath = view.querySelector('#txtIntroSkipBackupFilePath').value;
        self.config.IntroSkipIncludeInNfo = view.querySelector('#chkIntroSkipIncludeInNfo').checked;

        var introSkipSelectionMode = view.querySelector('input[name="introSkipSelectionMode"]:checked').value;
        self.config.IntroSkipSelectionMode = introSkipSelectionMode;

        if (introSkipSelectionMode === 'library') {
            var introSkipLibraries = [];
            self.libraries.forEach(function (library) {
                var checkbox = view.querySelector('#introskip-lib-' + library.Id);
                if (checkbox && checkbox.checked) {
                    introSkipLibraries.push(library.Id);
                }
            });
            self.config.IntroSkipLibraryIds = introSkipLibraries;
            self.config.IntroSkipSelectedSeriesIds = [];
        } else {
            var selectedSeries = [];
            if (self.allSeries) {
                self.allSeries.forEach(function (series) {
                    var checkbox = view.querySelector('#introskip-series-' + series.Id);
                    if (checkbox && checkbox.checked) {
                        selectedSeries.push(series.Id);
                    }
                });
            }
            self.config.IntroSkipSelectedSeriesIds = selectedSeries;
            self.config.IntroSkipLibraryIds = [];
        }

        ApiClient.updatePluginConfiguration(pluginId, self.config).then(function () {
            loading.hide();
            Dashboard.alert('Settings saved successfully.');
        }).catch(function(error) {
            loading.hide();
            Dashboard.alert('Failed to save settings');
        });
    };

    View.prototype.autoSave = function () {
        var self = this;
        if (self.saveTimeout) {
            clearTimeout(self.saveTimeout);
        }
        self.saveTimeout = setTimeout(function () {
            self.submitSilently();
        }, 1000);
    };

    View.prototype.submitSilently = function () {
        var self = this;

        var enabledLibraries = [];
        self.libraries.forEach(function (library) {
            var checkbox = view.querySelector('#lib-' + library.Id);
            if (checkbox && checkbox.checked) {
                enabledLibraries.push(library.Id);
            }
        });

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

        self.config.UseCustomArtworkNames = view.querySelector('#chkUseCustomArtworkNames').checked;
        self.config.CustomPosterName = view.querySelector('#txtCustomPosterName').value || 'poster';
        self.config.CustomFanartName = view.querySelector('#txtCustomFanartName').value || 'fanart';
        self.config.CustomLogoName = view.querySelector('#txtCustomLogoName').value || 'clearlogo';
        self.config.CustomBannerName = view.querySelector('#txtCustomBannerName').value || 'banner';
        self.config.CustomThumbName = view.querySelector('#txtCustomThumbName').value || 'landscape';
        self.config.CustomArtName = view.querySelector('#txtCustomArtName').value || 'clearart';
        self.config.CustomDiscName = view.querySelector('#txtCustomDiscName').value || 'disc';

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

        self.config.EnableIntroSkipBackup = view.querySelector('#chkEnableIntroSkipBackup').checked;
        self.config.IntroSkipBackupFilePath = view.querySelector('#txtIntroSkipBackupFilePath').value;
        self.config.IntroSkipIncludeInNfo = view.querySelector('#chkIntroSkipIncludeInNfo').checked;

        var introSkipSelectionMode = view.querySelector('input[name="introSkipSelectionMode"]:checked').value;
        self.config.IntroSkipSelectionMode = introSkipSelectionMode;

        if (introSkipSelectionMode === 'library') {
            var introSkipLibraries = [];
            self.libraries.forEach(function (library) {
                var checkbox = view.querySelector('#introskip-lib-' + library.Id);
                if (checkbox && checkbox.checked) {
                    introSkipLibraries.push(library.Id);
                }
            });
            self.config.IntroSkipLibraryIds = introSkipLibraries;
            self.config.IntroSkipSelectedSeriesIds = [];
        } else {
            var selectedSeries = [];
            if (self.allSeries) {
                self.allSeries.forEach(function (series) {
                    var checkbox = view.querySelector('#introskip-series-' + series.Id);
                    if (checkbox && checkbox.checked) {
                        selectedSeries.push(series.Id);
                    }
                });
            }
            self.config.IntroSkipSelectedSeriesIds = selectedSeries;
            self.config.IntroSkipLibraryIds = [];
        }

        ApiClient.updatePluginConfiguration(pluginId, self.config).catch(function(error) {
            console.error('Auto-save failed:', error);
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

    View.prototype.initIntroSkipHandlers = function () {
        var self = this;
        var view = this.view;

        // Enable/disable intro skip settings
        view.querySelector('#chkEnableIntroSkipBackup').addEventListener('change', function () {
            view.querySelector('#introSkipSettings').style.display = this.checked ? 'block' : 'none';
        });

        // Selection mode toggle
        view.querySelectorAll('input[name="introSkipSelectionMode"]').forEach(function (radio) {
            radio.addEventListener('change', function () {
                self.toggleIntroSkipSelectionMode(this.value);
            });
        });

        // Backup button
        view.querySelector('#backupIntroSkipBtn').addEventListener('click', function () {
            self.backupIntroSkip();
        });

        // Restore button
        view.querySelector('#restoreIntroSkipBtn').addEventListener('click', function () {
            self.restoreIntroSkip();
        });

        // Series search
        view.querySelector('#seriesSearchBox').addEventListener('input', function () {
            self.filterSeries(this.value);
        });
    };

    View.prototype.loadIntroSkipLibraries = function () {
        var self = this;
        var libraryList = view.querySelector('#introSkipLibraryList');
        libraryList.innerHTML = '';

        self.libraries.forEach(function (library) {
            var isEnabled = self.config.IntroSkipLibraryIds &&
                self.config.IntroSkipLibraryIds.indexOf(library.Id) !== -1;

            var item = document.createElement('div');
            item.className = 'library-item';
            item.innerHTML =
                '<label class="emby-checkbox-label">' +
                '<input is="emby-checkbox" type="checkbox" id="introskip-lib-' + library.Id + '" ' +
                (isEnabled ? 'checked' : '') + '>' +
                '<span>' + library.Name + '</span>' +
                '</label>';

            libraryList.appendChild(item);
        });
    };

    View.prototype.loadIntroSkipSeries = function () {
        var self = this;
        loading.show();

        fetch(ApiClient.getUrl('/MetadataExporter/IntroSkip/Series'), {
            headers: { 'X-Emby-Token': ApiClient.accessToken() }
        }).then(function (response) {
            return response.json();
        }).then(function (series) {
            self.allSeries = series;
            self.renderIntroSkipSeries(series);
            loading.hide();
        }).catch(function (error) {
            console.error('Error loading series:', error);
            loading.hide();
        });
    };

    View.prototype.renderIntroSkipSeries = function (series) {
        var self = this;
        var seriesList = view.querySelector('#introSkipSeriesList');
        seriesList.innerHTML = '';

        series.forEach(function (s) {
            var isSelected = self.config.IntroSkipSelectedSeriesIds &&
                self.config.IntroSkipSelectedSeriesIds.indexOf(s.Id) !== -1;

            var item = document.createElement('div');
            item.className = 'library-item';
            item.innerHTML =
                '<label class="emby-checkbox-label">' +
                '<input is="emby-checkbox" type="checkbox" id="introskip-series-' + s.Id + '" ' +
                (isSelected ? 'checked' : '') + '>' +
                '<span>' + s.Name + (s.Year ? ' (' + s.Year + ')' : '') + '</span>' +
                '</label>';

            seriesList.appendChild(item);
        });
    };

    View.prototype.filterSeries = function (searchTerm) {
        var self = this;
        if (!self.allSeries) return;

        var filtered = self.allSeries.filter(function (s) {
            return s.Name.toLowerCase().indexOf(searchTerm.toLowerCase()) !== -1;
        });

        self.renderIntroSkipSeries(filtered);
    };

    View.prototype.toggleIntroSkipSelectionMode = function (mode) {
        var libraryDiv = view.querySelector('#introSkipLibrarySelection');
        var seriesDiv = view.querySelector('#introSkipSeriesSelection');

        if (mode === 'library') {
            libraryDiv.style.display = 'block';
            seriesDiv.style.display = 'none';
        } else {
            libraryDiv.style.display = 'none';
            seriesDiv.style.display = 'block';
        }
    };

    View.prototype.backupIntroSkip = function () {
        var self = this;
        loading.show();

        var statusDiv = view.querySelector('#introSkipStatus');
        var statusText = view.querySelector('#introSkipStatusText');

        fetch(ApiClient.getUrl('/MetadataExporter/IntroSkip/Backup'), {
            method: 'POST',
            headers: {
                'X-Emby-Token': ApiClient.accessToken(),
                'Content-Type': 'application/json'
            }
        }).then(function (response) {
            if (!response.ok) {
                // Try to parse error message from response
                return response.text().then(function(text) {
                    var errorMsg = text || response.statusText;
                    try {
                        var json = JSON.parse(text);
                        errorMsg = json.Message || errorMsg;
                    } catch (e) {}
                    throw new Error(errorMsg);
                });
            }
            // Handle 204 No Content (success with no body)
            if (response.status === 204) {
                return { Success: true, Message: 'Backup completed successfully. Episodes have been added/updated in the backup file.' };
            }
            return response.json();
        }).then(function (result) {
            loading.hide();
            statusDiv.style.display = 'block';
            statusDiv.style.background = result.Success ? 'rgba(76, 175, 80, 0.2)' : 'rgba(244, 67, 54, 0.2)';
            statusText.textContent = result.Message || 'Backup completed';

            if (result.Success) {
                Dashboard.alert(result.Message);
            } else {
                Dashboard.alert('Backup failed: ' + result.Message);
            }
        }).catch(function (error) {
            loading.hide();
            statusDiv.style.display = 'block';
            statusDiv.style.background = 'rgba(244, 67, 54, 0.2)';
            statusText.textContent = 'Backup failed: ' + error.message;
            console.error('Backup error:', error);
            Dashboard.alert('Backup failed: ' + error.message);
        });
    };

    View.prototype.restoreIntroSkip = function () {
        var self = this;

        // First, let user select a file
        require(['directorybrowser'], function (directoryBrowser) {
            var picker = new directoryBrowser();
            picker.show({
                callback: function (path) {
                    if (path) {
                        picker.close();
                        
                        // Ensure path ends with .json
                        var filePath = path;
                        if (!path.toLowerCase().endsWith('.json')) {
                            Dashboard.alert('Please select a valid JSON backup file.');
                            return;
                        }

                        // Confirm restore
                        require(['confirm'], function (confirm) {
                            confirm('This will restore intro skip markers from "' + filePath + '". Existing markers will be overwritten. Continue?', 'Restore Intro Skip').then(function () {
                                loading.show();

                                var statusDiv = view.querySelector('#introSkipStatus');
                                var statusText = view.querySelector('#introSkipStatusText');

                                fetch(ApiClient.getUrl('/MetadataExporter/IntroSkip/Restore'), {
                                    method: 'POST',
                                    headers: {
                                        'X-Emby-Token': ApiClient.accessToken(),
                                        'Content-Type': 'application/json'
                                    },
                                    body: JSON.stringify({ FilePath: filePath })
                                }).then(function (response) {
                                    if (!response.ok) {
                                        // Try to parse error message from response
                                        return response.text().then(function(text) {
                                            var errorMsg = text || response.statusText;
                                            try {
                                                var json = JSON.parse(text);
                                                errorMsg = json.Message || errorMsg;
                                            } catch (e) {}
                                            throw new Error(errorMsg);
                                        });
                                    }
                                    // Handle 204 No Content (success with no body)
                                    if (response.status === 204) {
                                        return { Success: true, Message: 'Restore completed successfully.' };
                                    }
                                    return response.json();
                                }).then(function (result) {
                                    loading.hide();
                                    statusDiv.style.display = 'block';
                                    statusDiv.style.background = result.Success ? 'rgba(76, 175, 80, 0.2)' : 'rgba(244, 67, 54, 0.2)';
                                    statusText.textContent = result.Message || 'Restore completed';

                                    if (result.Success) {
                                        Dashboard.alert(result.Message);
                                    } else {
                                        Dashboard.alert('Restore failed: ' + result.Message);
                                    }
                                }).catch(function (error) {
                                    loading.hide();
                                    statusDiv.style.display = 'block';
                                    statusDiv.style.background = 'rgba(244, 67, 54, 0.2)';
                                    statusText.textContent = 'Restore failed: ' + error.message;
                                    console.error('Restore error:', error);
                                    Dashboard.alert('Restore failed: ' + error.message);
                                });
                            });
                        });
                    } else {
                        picker.close();
                    }
                },
                header: 'Select Backup File to Restore',
                instruction: 'Choose a JSON backup file containing intro skip markers',
                includeFiles: true
            });
        });
    };

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

        view.querySelector('#chkUseCustomArtworkNames').addEventListener('change', function () {
            instance.toggleCustomArtworkNames();
        });

        view.addEventListener('change', function (e) {
            if (e.target.type === 'checkbox' || e.target.type === 'radio') {
                instance.autoSave();
            }
        });

        view.addEventListener('input', function (e) {
            if (e.target.type === 'text') {
                instance.autoSave();
            }
        });

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

        view.querySelector('#browseBackupFilePath').addEventListener('click', function () {
            require(['directorybrowser'], function (directoryBrowser) {
                var picker = new directoryBrowser();
                picker.show({
                    callback: function (path) {
                        if (path) {
                            // Append default filename if just a directory was selected
                            var filePath = path;
                            if (!path.toLowerCase().endsWith('.json')) {
                                filePath = path + (path.endsWith('\\') || path.endsWith('/') ? '' : '\\') + 'intro_markers.json';
                            }
                            view.querySelector('#txtIntroSkipBackupFilePath').value = filePath;
                        }
                        picker.close();
                    },
                    header: 'Select Backup File Location',
                    instruction: 'Choose a folder for the intro skip backup file',
                    includeFiles: false
                });
            });
        });

        // Intro Skip Backup handlers
        instance.initIntroSkipHandlers();

        return instance;
    };
});
