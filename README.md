# 📦 MetaExtractor (Metadata Exporter) for Emby
<img width="768" height="512" alt="logo" src="https://github.com/user-attachments/assets/7ecdf039-89e6-4b2c-97c6-4bb46a760987" />


![Emby Plugin](https://img.shields.io/badge/Emby-Plugin-green.svg)
![Platform](https://img.shields.io/badge/Platform-Windows%20%7C%20Linux%20%7C%20Docker-blue)
![License](https://img.shields.io/badge/License-MIT-orange)

**MetaExtractor** is a powerful plugin for Emby Server that allows administrators to export internal database metadata and cached artwork directly to the file system. It generates standard NFO files (Kodi/Jellyfin compatible) and saves images alongside your media files. Additionally, it can backup and restore intro skip markers to protect your intro detection data.

This tool is essential for:
*   **Backups:** Securing your curated metadata, custom artwork, and intro skip markers.
*   **Migration:** Preparing your library for transfer to other media center software (Kodi, Jellyfin, Plex).
*   **Local Management:** Ensuring your media assets are stored locally rather than locked in the Emby database.
*   **Data Protection:** Safeguarding intro skip detection data from database corruption or library rebuilds.

---

## ✨ Features

### 📄 Comprehensive NFO Export
Generates XML NFO files compatible with most media managers. You have full granular control over what data gets written:
*   **Basic Info:** Titles, Year, Plot, Taglines, Outlines.
*   **People:** Cast, Directors, Writers (including Provider IDs).
*   **Details:** MPAA Ratings, Custom Certifications, Genres, Studios, Tags.
*   **Technical:** Run times, Dates (Premiered/Added), Chapter markers with timestamps and intro skip markers.
*   **IDs:** Full support for IMDb, TMDB, TVDB, and other provider IDs.
*   **Collections:** Movie collections/sets with provider IDs.
*   **Detailed Ratings:** Multiple rating sources (TMDB, Metacritic) with proper formatting.

### 🖼️ Artwork Export
Exports the exact images currently in your Emby database to your media folders:
*   **Supported Types:** Posters, Backdrops, Logos, Banners, Thumbs, Clear Art, and Disc Art.
*   **Naming Standards:** Uses standard naming conventions (e.g., `poster.jpg`, `fanart.jpg`, `clearlogo.png`) or custom names you define.
*   **Multiple Backdrops:** Automatically exports all backdrop images with proper numbering.

### ⏭️ Intro Skip Backup & Restore
Protect your intro detection data from being lost:
*   **Backup to JSON:** Export intro skip markers (IntroStart, IntroEnd, CreditStart) from episodes to a JSON backup file.
*   **Selective Backup:** Choose entire libraries or specific TV series to backup.
*   **File Browser:** Easy-to-use file picker for selecting backup locations and restore files.
*   **Smart Restore:** Matches episodes by ID, file path, or series/season/episode numbers with provider ID verification.
*   **NFO Integration:** Optionally include intro skip markers in NFO chapter data.
*   **Incremental Updates:** Backup file is updated with new/modified episodes, preserving existing data.

### ⚙️ Advanced Functionality
*   **Library & Item Selection:** Export entire libraries at once (Recursive) or select specific Movies/Shows manually.
*   **Auto-Save:** Settings automatically save as you change them - no more clicking "Save" repeatedly!
*   **Dry Run Mode:** "Try before you buy." Run a full export simulation to see the logs of what *would* happen without writing a single file to disk.
*   **Custom Export Path:** Option to export metadata to a mirrored directory structure in a separate location (e.g., a backup drive) instead of your source media folders.
*   **Hardlinking (Space Saver):** When exporting to a custom path on the same volume, the plugin can use filesystem Hardlinks instead of copying files, saving massive amounts of disk space (Windows only).
*   **Overwrite Control:** Separate settings to overwrite existing NFOs or Artwork, or only fill in what is missing.
*   **Configurable Performance:** Adjust parallel processing threads (1-16) to balance speed vs system load.
*   **Custom Artwork Names:** Define your own naming conventions for exported artwork files.
*   **Real-time Progress:** Live progress tracking with percentage, item counts, and current file being processed.
*   **Comprehensive Logging:** Detailed export log with download capability for troubleshooting.

---

## 🚀 Installation

1.  Download the latest `MetaExtractor.dll` from the **[Releases](../../releases)** page.
2.  Stop your Emby Server.
3.  Copy the `.dll` file into your Emby Plugins folder:
    *   **Windows:** `\Users\<User>\AppData\Roaming\Emby-Server\programdata\plugins`
    *   **Linux:** `/var/lib/emby/plugins`
    *   **Docker:** `/config/plugins` (depending on your mapping)
4.  Start Emby Server.
5.  Go to **Dashboard** -> **Plugins** to verify it is loaded.

---

## 📖 Usage Guide

Once installed, open the plugin configuration page via **Dashboard > Plugins > Metadata Exporter**.

### 1. The Export Tab
This is where you run the metadata and artwork export job.
*   **Selection Mode:** Choose **"Export entire libraries"** for bulk processing or **"Select individual items"** to pick specific media.
*   **Export Options:** Toggle artwork export and/or NFO generation.
*   **Overwrite Settings:** Control whether to overwrite existing files or skip them.
*   **Dry Run Mode:** Enable this to test your settings safely without making any filesystem changes.
*   **Custom Export Path:** Optionally export to a different location with hardlink support.
*   **Custom Artwork Names:** Define your own file naming conventions for exported images.
*   **NFO Field Selection:** Fine-tune exactly what metadata gets included in NFO files.
*   **Export Button:** Click to start the process. A progress bar will appear showing the current item being processed.
*   **Results:** Once finished, a summary will show total items processed and any errors encountered.

### 2. The Intro Skip Backup Tab
Protect your intro detection data with backup and restore functionality.
*   **Enable Feature:** Turn on intro skip backup/restore functionality.
*   **Backup File Path:** Choose where to store your backup JSON file using the Browse button.
*   **Selection Mode:** 
    *   **Library Mode:** Backup all series from selected libraries.
    *   **Individual Mode:** Select specific TV series to backup.
*   **Backup Button:** Exports intro skip markers to your backup file (incremental updates).
*   **Restore Button:** Opens a file picker to select a backup file and restore intro markers to your library.
*   **NFO Integration:** Option to include intro skip markers in exported NFO chapter data.

### 3. The Log Tab
*   View a real-time log of the export operation.
*   Essential for checking what files are being created during a **Dry Run**.
*   Shows hardlink creation, file copies, and any errors encountered.
*   **Download Log:** Saves the log to a `.txt` file for debugging.
*   **Clear Log:** Resets the log display.

## 💡 Tips & Best Practices

1.  **Always use Dry Run first** when testing new settings, especially with large libraries.
2.  **Backup regularly** before making any bulk file operations.
3.  **Use Custom Export Path** with hardlinks to create instant backups without duplicating data (same volume only).
4.  **Adjust Max Parallel Tasks** based on your server's capabilities - lower values for older hardware.
5.  **Backup intro skip data regularly** to protect against database issues or library rebuilds.
6.  **Test restore on a few episodes first** before restoring entire libraries.

---

## ⚠️ Disclaimer

While this plugin includes safety features like "Dry Run," **always ensure you have backups** of your media library data before performing bulk file operations, especially if using the "Overwrite" features. Intro skip restore will modify your Emby database chapter markers, so backup your database before major restore operations.

## 📄 License

Distributed under the MIT License. See `LICENSE` for more information.

If you enjoy this plugin and wish to show your appreciation, you can...

<a href="https://buymeacoffee.com/yockser" target="_blank"><img src="https://cdn.buymeacoffee.com/buttons/v2/default-yellow.png" alt="Buy Me A Coffee" style="height: 60px !important;width: 217px !important;" ></a>
