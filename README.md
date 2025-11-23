# 📦 MetaExtractor (Metadata Exporter) for Emby
<img width="768" height="512" alt="logo" src="https://github.com/user-attachments/assets/7ecdf039-89e6-4b2c-97c6-4bb46a760987" />


![Emby Plugin](https://img.shields.io/badge/Emby-Plugin-green.svg)
![Platform](https://img.shields.io/badge/Platform-Windows%20%7C%20Linux%20%7C%20Docker-blue)
![License](https://img.shields.io/badge/License-MIT-orange)

**MetaExtractor** is a powerful plugin for Emby Server that allows administrators to export internal database metadata and cached artwork directly to the file system. It generates standard NFO files (Kodi/Jellyfin compatible) and saves images alongside your media files.

This tool is essential for:
*   **Backups:** Securing your curated metadata and custom artwork.
*   **Migration:** Preparing your library for transfer to other media center software (Kodi, Jellyfin, Plex).
*   **Local Management:** Ensuring your media assets are stored locally rather than locked in the Emby database.

---

## ✨ Features

### 📄 Comprehensive NFO Export
Generates XML NFO files compatible with most media managers. You have full granular control over what data gets written:
*   **Basic Info:** Titles, Year, Plot, Taglines, Outlines.
*   **People:** Cast, Directors, Writers (including Provider IDs).
*   **Details:** MPAA Ratings, Custom Certifications, Genres, Studios, Tags.
*   **Technical:** Run times, Dates (Premiered/Added), Chapter markers with timestamps.
*   **IDs:** Full support for IMDb, TMDB, TVDB, and other provider IDs.

### 🖼️ Artwork Export
Exports the exact images currently in your Emby database to your media folders:
*   **Supported Types:** Posters, Backdrops, Logos, Banners, Thumbs, Clear Art, and Disc Art.
*   **Naming Standards:** Uses standard naming conventions (e.g., `poster.jpg`, `fanart.jpg`, `clearlogo.png`).

### ⚙️ Advanced Functionality
*   **Library & Item Selection:** Export entire libraries at once (Recursive) or select specific Movies/Shows manually.
*   **Dry Run Mode:** "Try before you buy." Run a full export simulation to see the logs of what *would* happen without writing a single file to disk.
*   **Custom Export Path:** Option to export metadata to a mirrored directory structure in a separate location (e.g., a backup drive) instead of your source media folders.
*   **Hardlinking (Space Saver):** When exporting to a custom path on the same volume, the plugin can use filesystem Hardlinks instead of copying files, saving massive amounts of disk space (Windows only).
*   **Overwrite Control:** Separate settings to overwrite existing NFOs or Artwork, or only fill in what is missing.

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
This is where you run the job.
*   **Selection Mode:** Choose **"Export entire libraries"** for bulk processing or **"Select individual items"** to pick specific media.
*   **Export Button:** Click to start the process. A progress bar will appear showing the current item being processed.
*   **Results:** Once finished, a summary will show total items processed and any errors encountered.

### 2. The Settings Tab
Configure exactly how the plugin behaves.
*   **Export Types:** Toggle Artwork and/or NFO generation.
*   **File Options:** 
    *   *Overwrite:* If unchecked, existing files are skipped (faster).
    *   *Dry Run:* Enable this to test your settings safely.
    *   *Custom Path:* Define a separate folder to dump metadata into.
*   **NFO Fields:** A checklist of every data point available. Uncheck fields you want to exclude from the NFOs.

### 3. The Log Tab
*   View a real-time log of the export operation.
*   Essential for checking what files are being created during a **Dry Run**.
*   **Download Log:** Saves the log to a `.txt` file for debugging.

---

## ⚠️ Disclaimer

While this plugin includes safety features like "Dry Run," **always ensure you have backups** of your media library data before performing bulk file operations, especially if using the "Overwrite" features.

## 📄 License

Distributed under the MIT License. See `LICENSE` for more information.
