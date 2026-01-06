define([
    'loading', 
    'emby-input', 
    'emby-checkbox', 
    'emby-button',
    'configurationpage?name=MetaExtractorConfigurationApi',
    'configurationpage?name=MetaExtractorConfigurationUtils',
    'configurationpage?name=MetaExtractorConfigurationExport',
    'configurationpage?name=MetaExtractorConfigurationIntroSkip'
], function (loading, embyInput, embyCheckbox, embyButton, apiModule, utils, exportModule, introSkipModule) {
    'use strict';

    var view;

    function View(viewElement, params) {
        this.view = viewElement;
        view = viewElement; // Keep global reference for compatibility
        this.params = params;
        this.libraries = [];
        this.selectedItems = [];
        this.progressInterval = null;
        this.saveTimeout = null;
        this.allSeries = [];
        this.scanFolderPaths = [];
        this.progressMonitorInterval = null;
    }

    View.prototype.loadConfiguration = function () {
        var self = this;
        loading.show();

        Promise.all([
            apiModule.getPluginConfiguration(),
            apiModule.getLibraries()
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
            view.querySelector('#exportCollections').checked = self.config.ExportCollections !== false;
            view.querySelector('#overwriteNfo').checked = self.config.OverwriteNfo !== false;
            view.querySelector('#overwriteArtwork').checked = self.config.OverwriteArtwork !== false;
            view.querySelector('#dryRun').checked = self.config.DryRun === true;
            view.querySelector('#useCustomExportPath').checked = self.config.UseCustomExportPath === true;
            view.querySelector('#customExportPath').value = self.config.CustomExportPath || '';
            view.querySelector('#useHardlinks').checked = self.config.UseHardlinks !== false; 
            
            self.toggleCustomPathInput();
            
            view.querySelector('#exportAllArtworkTypes').checked = self.config.ExportAllArtworkTypes === true;
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
            view.querySelector('#nfoIncludeChapters').checked = self.config.NfoIncludeChapters !== false;

            // Intro Skip Backup settings
            view.querySelector('#txtIntroSkipBackupFilePath').value = self.config.IntroSkipBackupFilePath || '';
            view.querySelector('#chkIntroSkipIncludeInNfo').checked = self.config.IntroSkipIncludeInNfo !== false;
            view.querySelector('#chkIntroSkipSavePerEpisode').checked = self.config.IntroSkipSavePerEpisode === true;
            view.querySelector('#chkIntroSkipUseCustomFolder').checked = self.config.IntroSkipUseCustomFolder === true;
            view.querySelector('#txtIntroSkipCustomFolderPath').value = self.config.IntroSkipCustomFolderPath || '';
            view.querySelector('#chkIntroSkipUseTvdbMatching').checked = self.config.IntroSkipUseTvdbMatching !== false;
            view.querySelector('#chkIntroSkipRestoreFromScan').checked = self.config.IntroSkipRestoreFromScan === true;
            
            // Scheduled Task settings
            view.querySelector('#chkScheduledTaskBackupNfo').checked = self.config.ScheduledTaskBackupNfo !== false;
            view.querySelector('#chkScheduledTaskBackupIntroSkips').checked = self.config.ScheduledTaskBackupIntroSkips !== false;

            var introSkipMode = self.config.IntroSkipSelectionMode || 'library';
            view.querySelector('input[name="introSkipSelectionMode"][value="' + introSkipMode + '"]').checked = true;
            introSkipModule.toggleSelectionMode(view, introSkipMode);
            introSkipModule.toggleBackupMode(view);
            introSkipModule.toggleCustomFolder(view);
            introSkipModule.toggleRestoreMode(view);
            introSkipModule.loadScanFolders(view, self);

            // Load intro skip libraries and series
            introSkipModule.loadLibraries(view, self);
            introSkipModule.loadSeries(view, self, apiModule);

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
        self.config.ExportCollections = view.querySelector('#exportCollections').checked;
        self.config.OverwriteNfo = view.querySelector('#overwriteNfo').checked;
        self.config.OverwriteArtwork = view.querySelector('#overwriteArtwork').checked;
        self.config.DryRun = view.querySelector('#dryRun').checked;
        self.config.UseCustomExportPath = view.querySelector('#useCustomExportPath').checked;
        self.config.CustomExportPath = view.querySelector('#customExportPath').value;
        self.config.UseHardlinks = view.querySelector('#useHardlinks').checked;
        self.config.ExportAllArtworkTypes = view.querySelector('#exportAllArtworkTypes').checked;
        self.config.ExportPoster = view.querySelector('#exportPoster').checked;
        self.config.ExportBackdrop = view.querySelector('#exportBackdrop').checked;
        self.config.ExportLogo = view.querySelector('#exportLogo').checked;
        self.config.ExportBanner = view.querySelector('#exportBanner').checked;
        self.config.ExportThumb = view.querySelector('#exportThumb').checked;
        self.config.ExportArt = view.querySelector('#exportArt').checked;
        self.config.ExportDisc = view.querySelector('#exportDisc').checked;

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
        
        self.config.IntroSkipSavePerEpisode = view.querySelector('#chkIntroSkipSavePerEpisode').checked;
        self.config.IntroSkipUseCustomFolder = view.querySelector('#chkIntroSkipUseCustomFolder').checked;
        self.config.IntroSkipCustomFolderPath = view.querySelector('#txtIntroSkipCustomFolderPath').value;
        self.config.IntroSkipUseTvdbMatching = view.querySelector('#chkIntroSkipUseTvdbMatching').checked;
        self.config.IntroSkipRestoreFromScan = view.querySelector('#chkIntroSkipRestoreFromScan').checked;
        self.config.IntroSkipScanFolderPaths = self.scanFolderPaths || [];
        
        self.config.ScheduledTaskBackupNfo = view.querySelector('#chkScheduledTaskBackupNfo').checked;
        self.config.ScheduledTaskBackupIntroSkips = view.querySelector('#chkScheduledTaskBackupIntroSkips').checked;

        apiModule.updatePluginConfiguration(self.config).then(function () {
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
        self.config.ExportCollections = view.querySelector('#exportCollections').checked;
        self.config.OverwriteNfo = view.querySelector('#overwriteNfo').checked;
        self.config.OverwriteArtwork = view.querySelector('#overwriteArtwork').checked;
        self.config.DryRun = view.querySelector('#dryRun').checked;
        self.config.UseCustomExportPath = view.querySelector('#useCustomExportPath').checked;
        self.config.CustomExportPath = view.querySelector('#customExportPath').value;
        self.config.UseHardlinks = view.querySelector('#useHardlinks').checked;
        self.config.ExportAllArtworkTypes = view.querySelector('#exportAllArtworkTypes').checked;
        self.config.ExportPoster = view.querySelector('#exportPoster').checked;
        self.config.ExportBackdrop = view.querySelector('#exportBackdrop').checked;
        self.config.ExportLogo = view.querySelector('#exportLogo').checked;
        self.config.ExportBanner = view.querySelector('#exportBanner').checked;
        self.config.ExportThumb = view.querySelector('#exportThumb').checked;
        self.config.ExportArt = view.querySelector('#exportArt').checked;
        self.config.ExportDisc = view.querySelector('#exportDisc').checked;

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
        
        self.config.IntroSkipSavePerEpisode = view.querySelector('#chkIntroSkipSavePerEpisode').checked;
        self.config.IntroSkipUseCustomFolder = view.querySelector('#chkIntroSkipUseCustomFolder').checked;
        self.config.IntroSkipCustomFolderPath = view.querySelector('#txtIntroSkipCustomFolderPath').value;
        self.config.IntroSkipUseTvdbMatching = view.querySelector('#chkIntroSkipUseTvdbMatching').checked;
        self.config.IntroSkipRestoreFromScan = view.querySelector('#chkIntroSkipRestoreFromScan').checked;
        self.config.IntroSkipScanFolderPaths = self.scanFolderPaths || [];
        
        self.config.ScheduledTaskBackupNfo = view.querySelector('#chkScheduledTaskBackupNfo').checked;
        self.config.ScheduledTaskBackupIntroSkips = view.querySelector('#chkScheduledTaskBackupIntroSkips').checked;

        apiModule.updatePluginConfiguration(self.config).catch(function(error) {
            console.error('Auto-save failed:', error);
        });
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

    return function (viewInstance, params) {
        view = viewInstance;
        var instance = new View(view, params);

        view.addEventListener('viewshow', function () {
            instance.loadConfiguration();
        });

        view.addEventListener('viewhide', function () {
            exportModule.stopProgressPolling(instance);
        });

        view.querySelector('form').addEventListener('submit', function (e) {
            instance.submit();
            e.preventDefault();
            return false;
        });

        view.querySelector('#exportBtn').addEventListener('click', function () {
            exportModule.exportMetadata(view, instance, apiModule, utils);
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
            exportModule.downloadLog(view);
        });
        
        view.querySelector('#clearLogBtn').addEventListener('click', function () {
            exportModule.clearLog(view);
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

        view.querySelector('#browseCustomFolderPath').addEventListener('click', function () {
            require(['directorybrowser'], function (directoryBrowser) {
                var picker = new directoryBrowser();
                picker.show({
                    callback: function (path) {
                        if (path) {
                            view.querySelector('#txtIntroSkipCustomFolderPath').value = path;
                        }
                        picker.close();
                    },
                    header: 'Select Custom Folder for Intro Skip Files',
                    instruction: 'Choose a folder where intro skip files will be saved in an organized structure',
                    includeFiles: false
                });
            });
        });

        // Intro Skip Backup handlers
        introSkipModule.initHandlers(view, instance, apiModule);

        return instance;
    };
});
