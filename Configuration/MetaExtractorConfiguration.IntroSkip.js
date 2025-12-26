define(['loading'], function (loading) {
    'use strict';

    return {
        initHandlers: function(view, self, apiModule) {
            // Per-episode backup mode toggle
            view.querySelector('#chkIntroSkipSavePerEpisode').addEventListener('change', function () {
                this.toggleBackupMode(view);
                self.autoSave();
            }.bind(this));
            
            // Custom folder toggle
            view.querySelector('#chkIntroSkipUseCustomFolder').addEventListener('change', function () {
                this.toggleCustomFolder(view);
                self.autoSave();
            }.bind(this));
            
            // Restore from scan toggle
            view.querySelector('#chkIntroSkipRestoreFromScan').addEventListener('change', function () {
                this.toggleRestoreMode(view);
                self.autoSave();
            }.bind(this));
            
            // Add scan folder button
            view.querySelector('#addScanFolderBtn').addEventListener('click', function () {
                this.addScanFolder(view, self);
            }.bind(this));

            // Selection mode toggle
            view.querySelectorAll('input[name="introSkipSelectionMode"]').forEach(function (radio) {
                radio.addEventListener('change', function () {
                    this.toggleSelectionMode(view, this.value);
                }.bind(this));
            }.bind(this));

            // Backup button
            view.querySelector('#backupIntroSkipBtn').addEventListener('click', function () {
                this.backup(view, self, apiModule);
            }.bind(this));

            // Restore button
            view.querySelector('#restoreIntroSkipBtn').addEventListener('click', function () {
                this.restore(view, self, apiModule);
            }.bind(this));

            // Series search
            view.querySelector('#seriesSearchBox').addEventListener('input', function () {
                this.filterSeries(view, self, this.value);
            }.bind(this));
            
            // Clear progress log button
            view.querySelector('#clearProgressLog').addEventListener('click', function () {
                this.clearProgressLog(view);
            }.bind(this));
            
            // Migration export button
            view.querySelector('#exportMigrationBtn').addEventListener('click', function () {
                this.exportForMigration(view, self, apiModule);
            }.bind(this));
            
            // Migration import button
            view.querySelector('#importMigrationBtn').addEventListener('click', function () {
                this.importFromMigration(view, self, apiModule);
            }.bind(this));
        },

        loadLibraries: function(view, self) {
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
        },

        loadSeries: function(view, self, apiModule) {
            loading.show();

            apiModule.getIntroSkipSeries().then(function (series) {
                self.allSeries = series;
                this.renderSeries(view, self, series);
                loading.hide();
            }.bind(this)).catch(function (error) {
                console.error('Error loading series:', error);
                loading.hide();
            });
        },

        renderSeries: function(view, self, series) {
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
        },

        filterSeries: function(view, self, searchTerm) {
            if (!self.allSeries) return;

            var filtered = self.allSeries.filter(function (s) {
                return s.Name.toLowerCase().indexOf(searchTerm.toLowerCase()) !== -1;
            });

            this.renderSeries(view, self, filtered);
        },

        toggleSelectionMode: function(view, mode) {
            var libraryDiv = view.querySelector('#introSkipLibrarySelection');
            var seriesDiv = view.querySelector('#introSkipSeriesSelection');

            if (mode === 'library') {
                libraryDiv.style.display = 'block';
                seriesDiv.style.display = 'none';
            } else {
                libraryDiv.style.display = 'none';
                seriesDiv.style.display = 'block';
            }
        },
        
        toggleBackupMode: function(view) {
            var savePerEpisode = view.querySelector('#chkIntroSkipSavePerEpisode').checked;
            var centralizedSection = view.querySelector('#centralizedBackupSection');
            var perEpisodeSection = view.querySelector('#perEpisodeOptionsSection');
            
            if (centralizedSection) {
                centralizedSection.style.display = savePerEpisode ? 'none' : 'block';
            }
            if (perEpisodeSection) {
                perEpisodeSection.style.display = savePerEpisode ? 'block' : 'none';
            }
        },
        
        toggleCustomFolder: function(view) {
            var useCustomFolder = view.querySelector('#chkIntroSkipUseCustomFolder').checked;
            var customFolderSection = view.querySelector('#customFolderSection');
            
            if (customFolderSection) {
                customFolderSection.style.display = useCustomFolder ? 'block' : 'none';
            }
        },
        
        toggleRestoreMode: function(view) {
            var restoreFromScan = view.querySelector('#chkIntroSkipRestoreFromScan').checked;
            var scanFoldersSection = view.querySelector('#scanFoldersSection');
            
            if (scanFoldersSection) {
                scanFoldersSection.style.display = restoreFromScan ? 'block' : 'none';
            }
        },
        
        loadScanFolders: function(view, self) {
            self.scanFolderPaths = self.config.IntroSkipScanFolderPaths || [];
            this.renderScanFolders(view, self);
        },
        
        renderScanFolders: function(view, self) {
            var scanFoldersList = view.querySelector('#scanFoldersList');
            
            if (!scanFoldersList) return;
            
            scanFoldersList.innerHTML = '';
            
            self.scanFolderPaths.forEach(function (folderPath, index) {
                var folderDiv = document.createElement('div');
                folderDiv.style.cssText = 'display: flex; gap: 0.5em; align-items: center; margin-bottom: 0.5em;';
                folderDiv.innerHTML = 
                    '<input is="emby-input" type="text" value="' + folderPath + '" style="flex: 1;" readonly />' +
                    '<button is="emby-button" type="button" class="raised button-cancel" data-index="' + index + '" style="padding: 0.6em 1em;">' +
                    '<span>Remove</span>' +
                    '</button>';
                
                folderDiv.querySelector('button').addEventListener('click', function () {
                    this.removeScanFolder(view, self, index);
                }.bind(this));
                
                scanFoldersList.appendChild(folderDiv);
            }.bind(this));
        },
        
        addScanFolder: function(view, self) {
            var newFolderInput = view.querySelector('#txtNewScanFolder');
            var newFolder = newFolderInput.value.trim();
            
            if (!newFolder) {
                Dashboard.alert('Please enter a folder path');
                return;
            }
            
            if (self.scanFolderPaths.indexOf(newFolder) !== -1) {
                Dashboard.alert('This folder is already in the list');
                return;
            }
            
            self.scanFolderPaths.push(newFolder);
            newFolderInput.value = '';
            this.renderScanFolders(view, self);
            self.autoSave();
        },
        
        removeScanFolder: function(view, self, index) {
            self.scanFolderPaths.splice(index, 1);
            this.renderScanFolders(view, self);
            self.autoSave();
        },

        startProgressMonitoring: function(view, apiModule, self) {
            if (self.progressMonitorInterval) {
                clearInterval(self.progressMonitorInterval);
            }
            
            self.progressMonitorInterval = setInterval(function () {
                this.updateProgress(view, apiModule, self);
            }.bind(this), 500);
            
            // Initial update
            this.updateProgress(view, apiModule, self);
        },
        
        stopProgressMonitoring: function(self) {
            if (self.progressMonitorInterval) {
                clearInterval(self.progressMonitorInterval);
                self.progressMonitorInterval = null;
            }
        },
        
        updateProgress: function(view, apiModule, self) {
            apiModule.getIntroSkipProgress().then(function (progress) {
                this.displayProgress(view, progress);
                
                // Stop monitoring when operation completes
                if (!progress.IsRunning && self.progressMonitorInterval) {
                    setTimeout(function () {
                        this.stopProgressMonitoring(self);
                    }.bind(this), 2000);
                }
            }.bind(this)).catch(function (error) {
                console.error('Error fetching progress:', error);
            });
        },
        
        displayProgress: function(view, progress) {
            // Update progress bar
            var percentage = progress.Percentage || 0;
            var progressBar = view.querySelector('#progressBar');
            var progressBarText = view.querySelector('#progressBarText');
            var progressPercentage = view.querySelector('#progressPercentage');
            
            progressBar.style.width = percentage + '%';
            progressBarText.textContent = percentage + '%';
            progressPercentage.textContent = percentage + '%';
            
            // Update status text
            var status = 'Processing...';
            if (progress.TotalItems > 0) {
                status = progress.ProcessedItems + ' of ' + progress.TotalItems + ' episodes processed';
            }
            view.querySelector('#progressStatus').textContent = status;
            
            // Update counters
            view.querySelector('#progressProcessed').textContent = progress.ProcessedItems || 0;
            view.querySelector('#progressSuccess').textContent = progress.SuccessItems || 0;
            view.querySelector('#progressSkipped').textContent = progress.SkippedItems || 0;
            view.querySelector('#progressFailed').textContent = progress.FailedItems || 0;
            
            // Update current item info
            view.querySelector('#progressCurrentSeries').textContent = progress.CurrentSeries || '-';
            view.querySelector('#progressCurrentItem').textContent = progress.CurrentItem || '-';
            view.querySelector('#progressEta').textContent = progress.EstimatedTimeRemaining || 'Calculating...';
            
            // Update log
            if (progress.ProgressLog && progress.ProgressLog.length > 0) {
                var logDiv = view.querySelector('#progressLog');
                var logHtml = '';
                
                // Show last 20 entries
                var recentLogs = progress.ProgressLog.slice(-20);
                recentLogs.forEach(function (entry) {
                    var color = '#ccc';
                    if (entry.includes('ERROR')) color = '#f44336';
                    else if (entry.includes('WARNING')) color = '#ff9800';
                    else if (entry.includes('complete')) color = '#00c853';
                    
                    logHtml += '<div style="color: ' + color + '; margin-bottom: 0.25em;">' + entry + '</div>';
                });
                
                logDiv.innerHTML = logHtml;
                logDiv.scrollTop = logDiv.scrollHeight;
            }
            
            // Update validation errors
            if (progress.ValidationErrors && progress.ValidationErrors.length > 0) {
                var validationContainer = view.querySelector('#validationErrorsContainer');
                var validationErrorsDiv = view.querySelector('#validationErrors');
                
                validationContainer.style.display = 'block';
                
                var errorsHtml = '';
                progress.ValidationErrors.forEach(function (error) {
                    errorsHtml += '<div style="margin-bottom: 0.5em;">• ' + error + '</div>';
                });
                
                validationErrorsDiv.innerHTML = errorsHtml;
            } else {
                view.querySelector('#validationErrorsContainer').style.display = 'none';
            }
            
            // Change bar color based on status
            if (!progress.IsRunning) {
                if (progress.FailedItems > 0) {
                    progressBar.style.background = 'linear-gradient(90deg, #f44336, #d32f2f)';
                } else {
                    progressBar.style.background = 'linear-gradient(90deg, #00c853, #00a152)';
                }
            } else {
                progressBar.style.background = 'linear-gradient(90deg, #00a4dc, #0078d7)';
            }
        },
        
        clearProgressLog: function(view) {
            view.querySelector('#progressLog').innerHTML = '';
            view.querySelector('#validationErrorsContainer').style.display = 'none';
        },

        backup: function(view, self, apiModule) {
            // Show progress container
            var progressContainer = view.querySelector('#introSkipProgressContainer');
            progressContainer.style.display = 'block';
            view.querySelector('#progressOperation').textContent = 'Backing Up';
            
            // Start monitoring progress
            this.startProgressMonitoring(view, apiModule, self);
            
            loading.show();

            var statusDiv = view.querySelector('#introSkipStatus');
            var statusText = view.querySelector('#introSkipStatusText');

            apiModule.backupIntroSkip().then(function (response) {
                if (!response.ok) {
                    return response.text().then(function(text) {
                        var errorMsg = text || response.statusText;
                        try {
                            var json = JSON.parse(text);
                            errorMsg = json.Message || errorMsg;
                        } catch (e) {}
                        throw new Error(errorMsg);
                    });
                }
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
        },

        restore: function(view, self, apiModule) {
            // First, let user select a file
            require(['directorybrowser'], function (directoryBrowser) {
                var picker = new directoryBrowser();
                picker.show({
                    callback: function (path) {
                        if (path) {
                            picker.close();
                            
                            var filePath = path;
                            if (!path.toLowerCase().endsWith('.json')) {
                                Dashboard.alert('Please select a valid JSON backup file.');
                                return;
                            }

                            // Confirm restore
                            require(['confirm'], function (confirm) {
                                confirm('This will restore intro skip markers from "' + filePath + '". Existing markers will be overwritten. Continue?', 'Restore Intro Skip').then(function () {
                                    // Show progress container
                                    var progressContainer = view.querySelector('#introSkipProgressContainer');
                                    progressContainer.style.display = 'block';
                                    view.querySelector('#progressOperation').textContent = 'Restoring';
                                    
                                    // Start monitoring progress
                                    this.startProgressMonitoring(view, apiModule, self);
                                    
                                    loading.show();

                                    var statusDiv = view.querySelector('#introSkipStatus');
                                    var statusText = view.querySelector('#introSkipStatusText');

                                    apiModule.restoreIntroSkip(filePath).then(function (response) {
                                        if (!response.ok) {
                                            return response.text().then(function(text) {
                                                var errorMsg = text || response.statusText;
                                                try {
                                                    var json = JSON.parse(text);
                                                    errorMsg = json.Message || errorMsg;
                                                } catch (e) {}
                                                throw new Error(errorMsg);
                                            });
                                        }
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
                                }.bind(this));
                            }.bind(this));
                        } else {
                            picker.close();
                        }
                    }.bind(this),
                    header: 'Select Backup File to Restore',
                    instruction: 'Choose a JSON backup file containing intro skip markers',
                    includeFiles: true
                });
            }.bind(this));
        },

        exportForMigration: function(view, self, apiModule) {
            require(['directorybrowser'], function (directoryBrowser) {
                var picker = new directoryBrowser();
                picker.show({
                    callback: function (path) {
                        if (path) {
                            picker.close();
                            
                            var exportPath = path;
                            if (!path.toLowerCase().endsWith('.json')) {
                                exportPath = path + (path.endsWith('\\') || path.endsWith('/') ? '' : '\\') + 'intro-skip-migration-export.json';
                            }
                            
                            var progressContainer = view.querySelector('#introSkipProgressContainer');
                            progressContainer.style.display = 'block';
                            view.querySelector('#progressOperation').textContent = 'Exporting';
                            
                            this.startProgressMonitoring(view, apiModule, self);
                            
                            loading.show();
                            
                            apiModule.exportMigration(exportPath).then(function (response) {
                                if (!response.ok) {
                                    return response.text().then(function(text) {
                                        var errorMsg = text || response.statusText;
                                        try {
                                            var json = JSON.parse(text);
                                            errorMsg = json.Message || errorMsg;
                                        } catch (e) {}
                                        throw new Error(errorMsg);
                                    });
                                }
                                return response.json();
                            }).then(function (result) {
                                loading.hide();
                                
                                if (result.Success) {
                                    Dashboard.alert('Migration export complete! ' + result.Message + '\n\nFile saved to: ' + exportPath);
                                } else {
                                    Dashboard.alert('Migration export failed: ' + result.Message);
                                }
                            }).catch(function (error) {
                                loading.hide();
                                console.error('Migration export error:', error);
                                Dashboard.alert('Migration export failed: ' + error.message);
                            });
                        } else {
                            picker.close();
                        }
                    }.bind(this),
                    header: 'Select Location for Migration Export',
                    instruction: 'Choose where to save the migration export file (will export ALL intro skip markers)',
                    includeFiles: true,
                    includeDirectories: true
                });
            }.bind(this));
        },
        
        importFromMigration: function(view, self, apiModule) {
            require(['directorybrowser'], function (directoryBrowser) {
                var picker = new directoryBrowser();
                picker.show({
                    callback: function (path) {
                        if (path) {
                            picker.close();
                            
                            if (!path.toLowerCase().endsWith('.json')) {
                                Dashboard.alert('Please select a valid JSON migration file.');
                                return;
                            }
                            
                            // Ask about overwriting existing markers
                            require(['confirm'], function (confirm) {
                                confirm('Import intro skip markers from migration file?\n\n' + 
                                       'This will import ALL episodes from the file.\n\n' +
                                       'Click OK to OVERWRITE existing markers, or Cancel to skip episodes that already have markers.', 
                                       'Import Migration').then(function () {
                                    this.performMigrationImport(view, self, apiModule, path, true);
                                }.bind(this), function () {
                                    confirm('Would you like to import but SKIP episodes that already have markers?', 
                                           'Skip Existing?').then(function () {
                                        this.performMigrationImport(view, self, apiModule, path, false);
                                    }.bind(this));
                                }.bind(this));
                            }.bind(this));
                        } else {
                            picker.close();
                        }
                    }.bind(this),
                    header: 'Select Migration File to Import',
                    instruction: 'Choose the migration export JSON file to import',
                    includeFiles: true
                });
            }.bind(this));
        },
        
        performMigrationImport: function(view, self, apiModule, importPath, overwriteExisting) {
            var progressContainer = view.querySelector('#introSkipProgressContainer');
            progressContainer.style.display = 'block';
            view.querySelector('#progressOperation').textContent = 'Importing';
            
            this.startProgressMonitoring(view, apiModule, self);
            
            loading.show();
            
            apiModule.importMigration(importPath, overwriteExisting).then(function (response) {
                if (!response.ok) {
                    return response.text().then(function(text) {
                        var errorMsg = text || response.statusText;
                        try {
                            var json = JSON.parse(text);
                            errorMsg = json.Message || errorMsg;
                        } catch (e) {}
                        throw new Error(errorMsg);
                    });
                }
                return response.json();
            }).then(function (result) {
                loading.hide();
                
                if (result.Success) {
                    Dashboard.alert('Migration import complete!\n\n' + result.Message);
                } else {
                    Dashboard.alert('Migration import failed: ' + result.Message);
                }
            }).catch(function (error) {
                loading.hide();
                console.error('Migration import error:', error);
                Dashboard.alert('Migration import failed: ' + error.message);
            });
        }
    };
});
