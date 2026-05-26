// ────────────────────────────────────────────────────────────
//  Project     : aa Mcp Server
//  Author      : NK
//  Date        : 26-05-2026
// ────────────────────────────────────────────────────────────
// Copyright 2026 The aaMcpServer Authors
// SPDX-License-Identifier: Apache-2.0
// ─────────────────────────────────────────────────────────────
//  File        : RollingFileLogger.cs
//  Namespace   : aaMcpServer.Logging
//  Purpose     : Rolling-file logger for the Historian MCP server with
//                slot-based rotation and background compression.
//
//  Rotation    : New file every N hours (default 6 => 4 slots/day).
//  Compression : Rotated files are zipped in the background
//                (non-blocking Task), never blocking the writer.
//  Retention   : Date folders older than N days are auto-deleted.
//  Safety      : Never throws from the write path; tracks health
//                counters and the last error for diagnostics.
//
//  Path layout :
//    {baseDir}/{yyyy-MM-dd}/{prefix}-HH-MM-HH-MM.txt   (active)
//    {baseDir}/{yyyy-MM-dd}/{prefix}-HH-MM-HH-MM.zip   (rotated)
// ─────────────────────────────────────────────────────────────

using System;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace aaMcpServer.Logging
{
    public sealed class RollingFileLogger : IDisposable
    {
        private readonly string _basePath;
        private readonly string _filePrefix;
        private readonly int _rotationHours;
        private readonly int _retentionDays;

        private readonly object _writeLock = new object();
        private StreamWriter _writer;
        private string _currentFilePath;
        private string _currentDateFolder;
        private int _currentSlot = -1;
        private int _currentDay = -1;
        private bool _disposed;

        private Timer _cleanupTimer;
        private const int CleanupIntervalMs = 30 * 60 * 1000; // 30 minutes

        // ── Health counters (read for diagnostics) ───────────────────────
        private int _writeAttempts;
        private int _writeSuccess;
        private int _writeFailed;
        private int _rotations;
        private int _compressions;
        private int _compressionFailures;
        private string _lastError = "";

        public int WriteAttempts => _writeAttempts;
        public int WriteSuccess => _writeSuccess;
        public int WriteFailed => _writeFailed;
        public int Rotations => _rotations;
        public int Compressions => _compressions;
        public int CompressionFailures => _compressionFailures;
        public string LastError => _lastError ?? "";
        public string CurrentFilePath => _currentFilePath ?? "";

        /// <summary>
        /// Create a rolling logger. <paramref name="baseDir"/> is the root folder
        /// for this logger's date sub-folders; <paramref name="filePrefix"/> is the
        /// filename root (the slot time-range and extension are appended).
        /// </summary>
        public RollingFileLogger(
            string baseDir,
            string filePrefix,
            int rotationHours = 6,
            int retentionDays = 7)
        {
            _basePath = string.IsNullOrWhiteSpace(baseDir) ? "logs" : baseDir;
            _filePrefix = string.IsNullOrWhiteSpace(filePrefix) ? "mcp-server" : filePrefix;

            // Rotation must divide evenly into 24h and be at least 1 hour.
            if (rotationHours < 1) rotationHours = 1;
            if (rotationHours > 24) rotationHours = 24;
            while (24 % rotationHours != 0) rotationHours--; // snap to a clean divisor (6,4,3,2,1...)
            _rotationHours = rotationHours;

            _retentionDays = retentionDays > 0 ? retentionDays : 7;
        }

        public void Start()
        {
            try
            {
                if (!Directory.Exists(_basePath))
                    Directory.CreateDirectory(_basePath);
            }
            catch (Exception ex)
            {
                _lastError = "StartFolderCreate: " + ex.Message;
            }

            _cleanupTimer = new Timer(
                CleanupCallback, null,
                TimeSpan.FromSeconds(15),
                TimeSpan.FromMilliseconds(CleanupIntervalMs));
        }

        /// <summary>Writes a single pre-formatted line. Never throws.</summary>
        public void WriteLine(string line)
        {
            if (_disposed) return;
            Interlocked.Increment(ref _writeAttempts);

            lock (_writeLock)
            {
                EnsureWriter();
                if (_writer == null)
                {
                    Interlocked.Increment(ref _writeFailed);
                    return;
                }

                try
                {
                    _writer.WriteLine(line);
                    _writer.Flush();
                    Interlocked.Increment(ref _writeSuccess);
                }
                catch (Exception ex)
                {
                    Interlocked.Increment(ref _writeFailed);
                    _lastError = "WriteLine: " + ex.Message;
                    CloseWriter();
                }
            }
        }

        // ── Writer rotation ──────────────────────────────────────────────

        private void EnsureWriter()
        {
            DateTime now = DateTime.Now;
            int dayOfYear = now.DayOfYear;
            int slot = now.Hour / _rotationHours; // 6h => 0..3

            if (_writer != null && _currentDay == dayOfYear && _currentSlot == slot)
                return;

            string previousFilePath = _currentFilePath;
            int previousDay = _currentDay;
            int previousSlot = _currentSlot;

            CloseWriter();

            bool crossingBoundary = previousDay != -1
                && (previousDay != dayOfYear || previousSlot != slot);
            if (crossingBoundary && !string.IsNullOrEmpty(previousFilePath))
            {
                string toCompress = previousFilePath;
                Task.Run(() => CompressFileBackground(toCompress));
                Interlocked.Increment(ref _rotations);
            }

            int slotStartH = slot * _rotationHours;
            int slotEndH = slotStartH + _rotationHours; // may be 24 (end of day)

            string dateFolder = Path.Combine(_basePath, now.ToString("yyyy-MM-dd"));
            string fileName = _filePrefix + "-"
                + slotStartH.ToString("D2") + "-00-"
                + slotEndH.ToString("D2") + "-00.txt";
            string newFilePath = Path.Combine(dateFolder, fileName);

            try
            {
                if (!Directory.Exists(dateFolder))
                    Directory.CreateDirectory(dateFolder);

                var fs = new FileStream(
                    newFilePath, FileMode.Append, FileAccess.Write, FileShare.Read);
                _writer = new StreamWriter(fs, new UTF8Encoding(false));

                _currentFilePath = newFilePath;
                _currentDateFolder = dateFolder;
                _currentDay = dayOfYear;
                _currentSlot = slot;
            }
            catch (Exception ex)
            {
                _lastError = "OpenWriter: " + ex.Message;
                _writer = null;
            }
        }

        private void CloseWriter()
        {
            if (_writer == null) return;
            try { _writer.Flush(); } catch { }
            try { _writer.Dispose(); } catch { }
            _writer = null;
        }

        // ── Background compression (.zip) ─────────────────────────────────

        private void CompressFileBackground(string txtFilePath)
        {
            try
            {
                if (string.IsNullOrEmpty(txtFilePath)) return;
                if (!File.Exists(txtFilePath)) return;
                if (!txtFilePath.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)) return;

                var fi = new FileInfo(txtFilePath);
                if (fi.Length == 0)
                {
                    try { fi.Delete(); } catch { }
                    return;
                }

                string zipPath = txtFilePath.Substring(0, txtFilePath.Length - 4) + ".zip";
                string entryName = Path.GetFileName(txtFilePath);

                using (var zipStream = new FileStream(zipPath, FileMode.Create, FileAccess.Write))
                using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create))
                {
                    var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
                    using (var entryStream = entry.Open())
                    using (var source = new FileStream(
                        txtFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        source.CopyTo(entryStream);
                    }
                }

                File.Delete(txtFilePath);
                Interlocked.Increment(ref _compressions);
            }
            catch (Exception ex)
            {
                Interlocked.Increment(ref _compressionFailures);
                _lastError = "Compress: " + ex.Message;
            }
        }

        // ── Retention cleanup ─────────────────────────────────────────────

        private void CleanupCallback(object state)
        {
            try
            {
                if (!Directory.Exists(_basePath)) return;
                DateTime cutoff = DateTime.Now.Date.AddDays(-_retentionDays);

                string[] dateFolders = Directory.GetDirectories(_basePath);
                foreach (string folder in dateFolders)
                {
                    try
                    {
                        string folderName = Path.GetFileName(folder);
                        DateTime folderDate;
                        if (!DateTime.TryParseExact(folderName, "yyyy-MM-dd",
                                CultureInfo.InvariantCulture, DateTimeStyles.None, out folderDate))
                            continue;

                        if (folderDate >= cutoff) continue;

                        lock (_writeLock)
                        {
                            if (_currentDateFolder != null &&
                                string.Equals(folder, _currentDateFolder,
                                    StringComparison.OrdinalIgnoreCase))
                                continue;
                        }

                        Directory.Delete(folder, true);
                    }
                    catch { /* per-folder failure is non-fatal */ }
                }
            }
            catch { /* cleanup failure is non-fatal */ }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            if (_cleanupTimer != null)
            {
                try { _cleanupTimer.Dispose(); } catch { }
                _cleanupTimer = null;
            }

            string lastFile;
            lock (_writeLock)
            {
                lastFile = _currentFilePath;
                CloseWriter();
            }

            // Compress the final active file on shutdown.
            if (!string.IsNullOrEmpty(lastFile))
            {
                try { CompressFileBackground(lastFile); } catch { }
            }
        }
    }
}
