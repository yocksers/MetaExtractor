using System.Collections.Generic;
using System.Linq;

namespace MetaExtractor
{
    public class IntroSkipProgress
    {
        private readonly object _lock = new object();
        private bool _isRunning;
        private string _operation = string.Empty;
        private int _totalItems;
        private int _processedItems;
        private int _successItems;
        private int _failedItems;
        private int _skippedItems;
        private int _percentage;
        private string _currentItem = string.Empty;
        private string _currentSeries = string.Empty;
        private string _estimatedTimeRemaining = string.Empty;
        private System.DateTime _startTime;
        private readonly List<string> _progressLog = new List<string>();
        private readonly List<string> _validationErrors = new List<string>();
        private const int MaxLogEntries = 500;

        public bool IsRunning
        {
            get { lock (_lock) return _isRunning; }
            set { lock (_lock) _isRunning = value; }
        }

        public string Operation
        {
            get { lock (_lock) return _operation; }
            set { lock (_lock) _operation = value; }
        }

        public int TotalItems
        {
            get { lock (_lock) return _totalItems; }
            set { lock (_lock) _totalItems = value; }
        }

        public int ProcessedItems
        {
            get { lock (_lock) return _processedItems; }
            set 
            { 
                lock (_lock) 
                { 
                    _processedItems = value;
                    UpdatePercentageAndEta();
                } 
            }
        }

        public int SuccessItems
        {
            get { lock (_lock) return _successItems; }
            set { lock (_lock) _successItems = value; }
        }

        public int FailedItems
        {
            get { lock (_lock) return _failedItems; }
            set { lock (_lock) _failedItems = value; }
        }

        public int SkippedItems
        {
            get { lock (_lock) return _skippedItems; }
            set { lock (_lock) _skippedItems = value; }
        }

        public int Percentage
        {
            get { lock (_lock) return _percentage; }
        }

        public string CurrentItem
        {
            get { lock (_lock) return _currentItem; }
            set { lock (_lock) _currentItem = value; }
        }

        public string CurrentSeries
        {
            get { lock (_lock) return _currentSeries; }
            set { lock (_lock) _currentSeries = value; }
        }

        public string EstimatedTimeRemaining
        {
            get { lock (_lock) return _estimatedTimeRemaining; }
        }

        public System.DateTime StartTime
        {
            get { lock (_lock) return _startTime; }
            set { lock (_lock) _startTime = value; }
        }

        public List<string> ProgressLog
        {
            get { lock (_lock) return _progressLog.ToList(); }
        }

        public List<string> ValidationErrors
        {
            get { lock (_lock) return _validationErrors.ToList(); }
        }

        public void AddLogEntry(string entry)
        {
            lock (_lock)
            {
                var timestamp = System.DateTime.Now.ToString("HH:mm:ss");
                _progressLog.Add($"[{timestamp}] {entry}");
                if (_progressLog.Count > MaxLogEntries)
                {
                    _progressLog.RemoveAt(0);
                }
            }
        }

        public void AddValidationError(string error)
        {
            lock (_lock)
            {
                _validationErrors.Add(error);
            }
        }

        public void ClearLog()
        {
            lock (_lock)
            {
                _progressLog.Clear();
            }
        }

        public void ClearValidationErrors()
        {
            lock (_lock)
            {
                _validationErrors.Clear();
            }
        }

        public void Reset()
        {
            lock (_lock)
            {
                _isRunning = false;
                _operation = string.Empty;
                _totalItems = 0;
                _processedItems = 0;
                _successItems = 0;
                _failedItems = 0;
                _skippedItems = 0;
                _percentage = 0;
                _currentItem = string.Empty;
                _currentSeries = string.Empty;
                _estimatedTimeRemaining = string.Empty;
                _startTime = System.DateTime.MinValue;
            }
        }

        private void UpdatePercentageAndEta()
        {
            if (_totalItems > 0)
            {
                _percentage = (int)((_processedItems / (double)_totalItems) * 100);
                
                if (_processedItems > 0 && _startTime != System.DateTime.MinValue)
                {
                    var elapsed = System.DateTime.Now - _startTime;
                    var avgTimePerItem = elapsed.TotalSeconds / _processedItems;
                    var remainingItems = _totalItems - _processedItems;
                    var estimatedSeconds = avgTimePerItem * remainingItems;
                    
                    if (estimatedSeconds < 60)
                    {
                        _estimatedTimeRemaining = $"{(int)estimatedSeconds}s";
                    }
                    else if (estimatedSeconds < 3600)
                    {
                        _estimatedTimeRemaining = $"{(int)(estimatedSeconds / 60)}m {(int)(estimatedSeconds % 60)}s";
                    }
                    else
                    {
                        var hours = (int)(estimatedSeconds / 3600);
                        var minutes = (int)((estimatedSeconds % 3600) / 60);
                        _estimatedTimeRemaining = $"{hours}h {minutes}m";
                    }
                }
            }
        }
    }
}
