MetaExtractor (Metadata Exporter) for Emby
MetaExtractor is a robust plugin for Emby Server that allows administrators to export metadata and artwork from the internal Emby database directly to media folders (or a custom location). It generates Kodi/Jellyfin compatible NFO files and saves existing artwork images.
This tool is essential for users who want to backup their metadata, migrate to other media center software, or simply ensure their media assets are stored locally alongside their video files.
🚀 Key Features
📦 Metadata & NFO Export
Comprehensive NFO Generation: Creates standard XML NFO files compatible with Kodi, Jellyfin, and other media managers.
Granular Control: Toggle specific metadata fields to include or exclude:
Plots, Taglines, and Outlines
Cast, Directors, Writers, and Musicians
Ratings (Community, Critic, MPAA, Certification)
Tags, Genres, and Studios
Provider IDs (IMDb, TMDB, TVDB)
Chapter markers with timestamps
Collections and Sets
Smart Overwrite: Choose to overwrite existing NFOs or only create missing ones.
🎨 Artwork Export
Image Types: Supports export of Posters, Backdrops, Logos, Banners, Thumbs, Clear Art, and Disc Art.
High Quality: Exports the exact images currently cached or saved in the Emby database.
Space Saving: Optional Hardlink mode creates file links instead of copying data (when exporting to a custom path on the same drive), saving massive amounts of disk space.
⚙️ Advanced Configuration
Selection Modes:
Library Mode: Export entire libraries at once.
Individual Mode: Browse and select specific movies or TV shows to export.
Custom Export Path: Option to export all metadata to a separate directory structure (mirroring your library) instead of cluttering your media folders.
Dry Run Mode: "Try before you buy" — simulate an export operation and view a detailed log of exactly what files would be created without writing anything to disk.
Real-time Logging: View export operations in real-time via the web UI.
🛠️ Installation
Download the latest release .dll file.
Shut down your Emby Server.
Place the MetaExtractor.dll file into your Emby plugins folder:
Windows: C:\Users\[User]\AppData\Roaming\Emby-Server\programdata\plugins
Linux/Docker: /var/lib/emby/plugins (or your mapped volume).
Restart Emby Server.
Navigate to Dashboard > Plugins to verify installation.
📖 Usage
Once installed, navigate to the plugin configuration page via Dashboard > Plugins > Metadata Exporter.
1. The Export Tab
Select Libraries: Choose which libraries (Movies, TV Shows) you want to process.
Or Select Items: Switch to "Individual items" mode to pick specific media.
Export Button: Starts the background process. A progress bar will indicate status.
2. The Settings Tab
Configure how the export handles files:
File Options: Enable "Overwrite" if you want to update existing NFOs with fresh data from Emby.
Dry Run: Check this box to test your settings. No files will be created.
Custom Path: Enable this to replicate your library structure in a different folder (e.g., D:\MetadataBackup\Movies\Avatar (2009)\...).
NFO Fields: Check/Uncheck specific data points (e.g., if you don't want "People/Cast" in your NFOs, uncheck Actors/Cast).
3. The Log Tab
View a detailed history of the last operation.
If Dry Run was enabled, this log lists every file that would have been created.
Download the log to a text file for review.
