﻿using System;
using System.IO;

namespace CodeIndex.Common
{
    public class ChangedSource
    {
        public string FilePath { get; set; }
        public string OldPath { get; set; }
        public WatcherChangeTypes ChangesType { get; set; }
        public DateTime ChangedUTCDate { get; set; } = DateTime.UtcNow;

        public override string ToString()
        {
            return $"Changed Source: {FilePath} {OldPath} {ChangesType} {ChangedUTCDate}";
        }
    }
}
