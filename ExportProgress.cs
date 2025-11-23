using System.Collections.Generic;

namespace MetaExtractor
{
    public class ExportProgress
    {
        public bool IsExporting { get; set; }
        public int TotalItems { get; set; }
        public int ProcessedItems { get; set; }
        public int ExportedItems { get; set; }
        public int Percentage { get; set; }
        public string CurrentItem { get; set; } = string.Empty;
        public List<string> ExportLog { get; set; } = new List<string>();
    }
}
