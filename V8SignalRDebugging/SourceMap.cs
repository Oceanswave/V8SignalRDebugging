﻿/* ****************************************************************************
 *
 * Copyright (c) Microsoft Corporation. 
 *
 * This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
 * copy of the license can be found in the License.html file at the root of this distribution. If 
 * you cannot locate the Apache License, Version 2.0, please send an email to 
 * vspython@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
 * by the terms of the Apache License, Version 2.0.
 *
 * You must not remove this notice, or any other, from this software.
 *
 * ***************************************************************************/

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;

namespace Microsoft.NodejsTools {
    using Newtonsoft.Json;

    /// <summary>
    /// Reads a V3 source map as documented at https://docs.google.com/document/d/1U1RGAehQwRypUTovF1KRlpiOFze0b-_2gc6fAH0KY0k/edit?hl=en_US&pli=1&pli=1
    /// </summary>
    class SourceMap {
        private readonly Dictionary<string, object> m_mapInfo;
        private readonly LineInfo[] m_lines;
        private readonly string[] m_names, m_sources;
        private static readonly Dictionary<char, int> Base64Mapping = BuildBase64Mapping();
        
        /// <summary>
        /// Index into the mappings for the starting column
        /// </summary>
        private const int SourceStartingIndex = 0;
        /// <summary>
        /// Index into the mappings for the sources list index (optional)
        /// </summary>
        private const int SourcesIndex = 1;
        /// <summary>
        /// Index into the mappings for the zero-based starting line (optional)
        /// </summary>
        private const int OriginalLineIndex = 2;
        /// <summary>
        /// Index into the mappings for the zero-based starting column (optional)
        /// </summary>
        private const int OriginalColumnIndex = 3;
        /// <summary>
        /// Index into the mappings of the names list
        /// </summary>
        private const int NamesIndex = 4;

        /// <summary>
        /// Creates a new source map from the given input.  Raises InvalidOperationException
        /// if the file is not supported or invalid.
        /// </summary>
        /// <param name="input"></param>
        public SourceMap(TextReader input) {

            m_mapInfo = JsonConvert.DeserializeObject<Dictionary<string, object>>(input.ReadToEnd());
            if (Version != 3) {
                throw new NotSupportedException("Only V3 source maps are supported");
            }
            
            object value;
            if (m_mapInfo.TryGetValue("sources", out value)) {
                var sourceRoot = SourceRoot;

                var sources = value as ArrayList;

                if (sources == null)
                    throw new InvalidOperationException("Sources is null.");

                m_sources = sources.Cast<string>()
                    .Select(x => sourceRoot + x)
                    .ToArray();
            } else {
                m_sources = new string[0];
            }

            if (m_mapInfo.TryGetValue("names", out value)) {
                var names = value as ArrayList;
                if (names == null)
                    throw new InvalidOperationException("Names is null.");
                m_names = names.Cast<string>().ToArray();
            } else {
                m_names = new string[0];
            }

            List<LineInfo> lineInfos = new List<LineInfo>();
            object mappingsObj;
            if (m_mapInfo.TryGetValue("mappings", out mappingsObj) && mappingsObj is string) {
                var mappings = (string)mappingsObj;
                var lines = mappings.Split(';');
                
                // each ; separated section represents a line in the generated file
                int sourceIndex = 0, originalLine = 0, originalColumn = 0, originalName = 0;
                foreach (var line in lines) {
                    if (line.Length == 0) {
                        lineInfos.Add(new LineInfo(new SegmentInfo[0]));
                        continue;
                    }

                    var segments = line.Split(',');

                    // each , separated section represents a segment of the line
                    int generatedColumn = 0;
                    List<SegmentInfo> segmentInfos = new List<SegmentInfo>();
                    foreach (var segment in segments) {
                        // each segment is Base64 VLQ encoded

                        var info = DecodeVLQ(segment);
                        if (info.Length == 0) {
                            throw new InvalidOperationException("invalid data in source map, no starting column");
                        }

                        generatedColumn += info[SourceStartingIndex];

                        if (info.Length >= SourcesIndex) {
                            sourceIndex += info[SourcesIndex];
                        }

                        if (info.Length >= OriginalLineIndex) {
                            originalLine += info[OriginalLineIndex];
                        }

                        if (info.Length > OriginalColumnIndex) {
                            originalColumn += info[OriginalColumnIndex];
                        }

                        if (info.Length > NamesIndex) {
                            originalName += info[NamesIndex];
                        }

                        segmentInfos.Add(
                            new SegmentInfo(
                                generatedColumn,
                                sourceIndex,
                                originalLine,
                                originalColumn,
                                originalName
                            )
                        );
                    }

                    lineInfos.Add(new LineInfo(segmentInfos.ToArray()));
                }
            }
            m_lines = lineInfos.ToArray();
        }

        private const int VLQ_CONTINUATION_MASK = 0x20;
        private const int VLQ_SHIFT = 5;

        private int[] DecodeVLQ(string value) {
            List<int> res = new List<int>();

            int curPos = 0;
            while (curPos < value.Length) {
                long intValue = 0;
                int mappedValue, shift = 0;
                do {
                    if (curPos == value.Length) {
                        throw new InvalidOperationException("invalid data in source map, continued value doesn't continue");
                    }

                    try {
                        mappedValue = Base64Mapping[value[curPos++]];
                    } catch (KeyNotFoundException) {
                        throw new InvalidOperationException("invalid data in source map, base64 data out of range");
                    }

                    intValue |= (uint)((mappedValue & ~VLQ_CONTINUATION_MASK) << shift);
                    if (intValue > Int32.MaxValue) {
                        throw new InvalidOperationException("invalid data in source map, value is outside of 32-bit range");
                    }
                    shift += VLQ_SHIFT;
                } while ((mappedValue & VLQ_CONTINUATION_MASK) != 0);

                // least significant bit is sign bit
                if ((intValue & 0x01) != 0) {
                    res.Add((int)-(intValue >> 1));
                } else {
                    res.Add((int)(intValue >> 1));
                }
            }
            return res.ToArray();
        }

        /// <summary>
        /// Version number of the source map.
        /// </summary>
        public int Version {
            get {
                return GetValue("version", -1);
            }
        }

        /// <summary>
        /// Filename of the generated code
        /// </summary>
        public string File {
            get {
                return GetValue("file", "");
            }
        }

        /// <summary>
        /// Provides the root for the sources to save space, automatically
        /// included in the Sources array so it's not public.
        /// </summary>
        private string SourceRoot {
            get {
                return GetValue("sourceRoot", "");
            }
        }

        /// <summary>
        /// All of the filenames that were combined.
        /// </summary>
        public ReadOnlyCollection<string> Sources {
            get {
                return new ReadOnlyCollection<string>(m_sources);
            }
        }

        /// <summary>
        /// All of the variable/method names that appear in the code
        /// </summary>
        public ReadOnlyCollection<string> Names {
            get {
                return new ReadOnlyCollection<string>(m_names);
            }
        }

        /// <summary>
        /// Maps a location in the generated code into a location in the source code.
        /// </summary>
        public bool TryMapPoint(int lineNo, int columnNo, out SourceMapping res) {
            if (lineNo < m_lines.Length) {
                var line = m_lines[lineNo];
                for (int i = line.Segments.Length - 1; i >= 0; i--) {
                    if (line.Segments[i].GeneratedColumn <= columnNo) {
                        // we map to this column
                        res = new SourceMapping(
                            line.Segments[i].OriginalLine,
                            line.Segments[i].OriginalColumn,
                            line.Segments[i].SourceIndex < Sources.Count ? Sources[line.Segments[i].SourceIndex] : null,
                            line.Segments[i].OriginalName < Names.Count ? Names[line.Segments[i].OriginalName] : null
                        );
                        return true;
                    }
                }
                if (line.Segments.Length > 0) {
                    // we map to this column
                    res = new SourceMapping(
                        line.Segments[0].OriginalLine,
                        line.Segments[0].OriginalColumn,
                        line.Segments[0].SourceIndex < Sources.Count ? Sources[line.Segments[0].SourceIndex] : null,
                        line.Segments[0].OriginalName < Names.Count ? Names[line.Segments[0].OriginalName] : null
                    );
                    return true;
                }
            }
            res = default(SourceMapping);
            return false;
        }

        /// <summary>
        /// Maps a location in the generated code into a location in the source code.
        /// </summary>
        public bool TryMapLine(int lineNo, out SourceMapping res) {
            if (lineNo < m_lines.Length) {
                var line = m_lines[lineNo];
                if (line.Segments.Length > 0) {
                    res = new SourceMapping(
                        line.Segments[0].OriginalLine,
                        0,
                        Sources[line.Segments[0].SourceIndex],
                        line.Segments[0].OriginalName < Names.Count ? 
                            Names[line.Segments[0].OriginalName] : 
                            null
                    );
                    return true;
                }
            }
            res = default(SourceMapping);
            return false;
        }

        /// <summary>
        /// Maps a location in the source code into the generated code.
        /// </summary>
        public bool TryMapPointBack(int lineNo, int columnNo, out SourceMapping res) {
            int? firstBestLine = null, secondBestLine = null;
            for (int i = 0; i < m_lines.Length; i++) {
                var line = m_lines[i];
                int? originalColumn = null;
                foreach (var segment in line.Segments) {
                    if (segment.OriginalLine == lineNo) {
                        if (segment.OriginalColumn <= columnNo) {
                            originalColumn = segment.OriginalColumn;
                        } else if (originalColumn != null) {
                            res = new SourceMapping(
                                i,
                                columnNo - originalColumn.Value,
                                File,
                                null
                            );
                            return true;
                        } else {
                            // code like:
                            //      constructor(public greeting: string) { }
                            // gets compiled into:
                            //      function Greeter(greeting) {
                            //          this.greeting = greeting;
                            //      }
                            // If we're going to pick a line out of here we'd rather pick the
                            // 2nd line where we're going to hit a breakpoint.

                            if (firstBestLine == null) {
                                firstBestLine = i;
                            } else if (secondBestLine == null && firstBestLine.Value != i) {
                                secondBestLine = i;
                            }
                        }
                    } else if (segment.OriginalLine > lineNo && firstBestLine != null) {
                        // not a perfect matching on column (e.g. requested 0, mapping starts at 4)
                        res = new SourceMapping(secondBestLine ?? firstBestLine.Value, 0, File, null);
                        return true;
                    }
                }
            }

            res = default(SourceMapping);
            return false;
        }

        private T GetValue<T>(string name, T defaultValue) {
            object version;
            if (m_mapInfo.TryGetValue(name, out version) && version is T) {
                return (T)version;
            }
            return defaultValue;
        }

        private static Dictionary<char, int> BuildBase64Mapping() {
            const string base64Mapping = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/";

            Dictionary<char, int> mapping = new Dictionary<char, int>();
            for (int i = 0; i < base64Mapping.Length; i++) {
                mapping[base64Mapping[i]] = i;
            }
            return mapping;
        }

        private struct LineInfo {
            public readonly SegmentInfo[] Segments;

            public LineInfo(SegmentInfo[] segments) {
                Segments = segments;
            }
        }

        public struct SegmentInfo {
            public readonly int GeneratedColumn;
            public readonly int SourceIndex;
            public readonly int OriginalLine;
            public readonly int OriginalColumn;
            public readonly int OriginalName;

            public SegmentInfo(int generatedColumn, int sourceIndex, int originalLine, int originalColumn, int originalName) {
                GeneratedColumn = generatedColumn;
                SourceIndex = sourceIndex;
                OriginalLine = originalLine;
                OriginalColumn = originalColumn;
                OriginalName = originalName;
            }
        }
    }

    public class SourceMapping {
        public readonly int Line, Column;
        public readonly string FileName, Name;

        internal SourceMapping(int line, int column, string filename, string name) {
            Line = line;
            Column = column;
            FileName = filename;
            Name = name;
        }
    }
}
