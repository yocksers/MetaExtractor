define(['loading'], function (loading) {
    'use strict';

    return {
        exportMetadata: function(view, self, apiModule, utils) {
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
            self.config.ExportCollections = view.querySelector('#exportCollections').checked;
            self.config.OverwriteNfo = view.querySelector('#overwriteNfo').checked;
            self.config.OverwriteArtwork = view.querySelector('#overwriteArtwork').checked;
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
            
            this.startProgressPolling(view, apiModule, self);

            apiModule.updatePluginConfiguration(self.config).then(function () {
                console.log('Configuration saved, starting export...');
                return apiModule.exportMetadata();
            }).then(function (response) {
                if (!response.ok) {
                    throw new Error('HTTP error ' + response.status);
                }
                return response.json();
            }).then(function (result) {
                loadingSpinner.classList.remove('active');
                exportBtn.disabled = false;
                this.stopProgressPolling(self);
                
                apiModule.getExportProgress().then(function (progress) {
                    if (progress.ExportLog && progress.ExportLog.length > 0) {
                        this.updateDryRunLog(view, progress.ExportLog);
                    }
                }.bind(this)).catch(function (error) {
                    console.error('Error fetching final progress:', error);
                });

                if (result.Success) {
                    this.displayResults(view, result, utils);
                } else {
                    Dashboard.alert('Error: ' + result.Message);
                }
            }.bind(this)).catch(function (error) {
                loadingSpinner.classList.remove('active');
                exportBtn.disabled = false;
                this.stopProgressPolling(self);
                console.error('Export error:', error);
                Dashboard.alert('Failed to export metadata: ' + (error.message || 'Unknown error'));
            }.bind(this));
        },

        startProgressPolling: function(view, apiModule, self) {
            var progressText = view.querySelector('#progressText');
            var progressBar = view.querySelector('#progressBar');
            
            self.progressInterval = setInterval(function () {
                apiModule.getExportProgress().then(function (progress) {
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
                            this.updateDryRunLog(view, progress.ExportLog);
                        }
                    }
                }.bind(this)).catch(function (error) {
                    console.error('Error fetching progress:', error);
                });
            }.bind(this), 500); 
        },

        stopProgressPolling: function(self) {
            if (self.progressInterval) {
                clearInterval(self.progressInterval);
                self.progressInterval = null;
            }
        },
        
        updateDryRunLog: function(view, logEntries) {
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
        },

        displayResults: function(view, result, utils) {
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
                resultsStats.innerHTML = '<strong>Error:</strong> ' + utils.escapeHtml(result.Message);
                errorList.innerHTML = '';
            }

            resultsContainer.scrollIntoView({ behavior: 'smooth', block: 'start' });
        },

        downloadLog: function(view) {
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
        },
        
        clearLog: function(view) {
            var exportLogList = view.querySelector('#exportLogList');
            var exportLogCount = view.querySelector('#exportLogCount');
            
            if (exportLogList) {
                exportLogList.innerHTML = '<div style="color: #666; text-align: center; padding: 2em;">No export operations yet. Run an export to see logs here.</div>';
            }
            if (exportLogCount) {
                exportLogCount.textContent = '0';
            }
            
            Dashboard.alert('Log cleared.');
        }
    };
});
