using System.Collections.Generic;
using System.Linq;

namespace MetaExtractor
{
    public class ExportProgress
    {
        private readonly object _lock = new object();
        private bool _isExporting;
        private int _totalItems;
        private int _processedItems;
        private int _exportedItems;
        private int _percentage;
        private string _currentItem = string.Empty;
        private readonly List<string> _exportLog = new List<string>();
        private const int MaxLogEntries = 1000;

        public bool IsExporting
        {
            get { lock (_lock) return _isExporting; }
            set { lock (_lock) _isExporting = value; }
        }

        public int TotalItems
        {
            get { lock (_lock) return _totalItems; }
            set { lock (_lock) _totalItems = value; }
        }

        public int ProcessedItems
        {
            get { lock (_lock) return _processedItems; }
            set { lock (_lock) _processedItems = value; }
        }

        public int ExportedItems
        {
            get { lock (_lock) return _exportedItems; }
            set { lock (_lock) _exportedItems = value; }
        }

        public int Percentage
        {
            get { lock (_lock) return _percentage; }
            set { lock (_lock) _percentage = value; }
        }

        public string CurrentItem
        {
            get { lock (_lock) return _currentItem; }
            set { lock (_lock) _currentItem = value; }
        }

        public List<string> ExportLog
        {
            get { lock (_lock) return _exportLog.ToList(); }
        }

        public void AddLogEntry(string entry)
        {
            lock (_lock)
            {
                _exportLog.Add(entry);
                if (_exportLog.Count > MaxLogEntries)
                {
                    _exportLog.RemoveAt(0);
                }
            }
        }

        public void ClearLog()
        {
            lock (_lock)
            {
                _exportLog.Clear();
            }
        }
    }
}
