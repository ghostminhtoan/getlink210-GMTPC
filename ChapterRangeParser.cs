using System;
using System.Collections.Generic;
using System.Linq;

namespace get_link_manga
{
    /// <summary>
    /// Utility class để parse cú pháp chọn chapter kiểu máy in.
    /// Ví dụ: "1-3;5;9-10" → HashSet { 1, 2, 3, 5, 9, 10 }
    /// Dấu phân cách: ";" hoặc ","
    /// Range: "1-10" → từ 1 đến 10
    /// Single: "3" → chỉ 3
    /// Empty/null → return null (tải tất cả)
    /// </summary>
    public static class ChapterRangeParser
    {
        /// <summary>
        /// Parse chuỗi chapter selection thành HashSet các số chapter.
        /// Return null nếu chuỗi trống (nghĩa là tải tất cả).
        /// Throw FormatException nếu cú pháp không hợp lệ.
        /// </summary>
        public static HashSet<int> Parse(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return null; // Tải tất cả

            var result = new HashSet<int>();
            string cleaned = input.Trim();

            // Split bằng ";" hoặc ","
            string[] segments = cleaned.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (string segment in segments)
            {
                string trimmed = segment.Trim();
                if (string.IsNullOrEmpty(trimmed))
                    continue;

                if (trimmed.Contains("-"))
                {
                    // Range: "1-10"
                    string[] parts = trimmed.Split(new[] { '-' }, 2);
                    if (parts.Length != 2)
                        throw new FormatException($"Cú pháp range không hợp lệ: '{trimmed}'. Dùng format: 1-10");

                    if (!int.TryParse(parts[0].Trim(), out int start))
                        throw new FormatException($"Số bắt đầu không hợp lệ trong range: '{trimmed}'");

                    if (!int.TryParse(parts[1].Trim(), out int end))
                        throw new FormatException($"Số kết thúc không hợp lệ trong range: '{trimmed}'");

                    if (start > end)
                        throw new FormatException($"Số bắt đầu ({start}) lớn hơn số kết thúc ({end}) trong range: '{trimmed}'");

                    if (end - start > 10000)
                        throw new FormatException($"Range quá lớn ({start}-{end}). Tối đa 10000 chapters.");

                    for (int i = start; i <= end; i++)
                    {
                        result.Add(i);
                    }
                }
                else
                {
                    // Single: "5"
                    if (!int.TryParse(trimmed, out int num))
                        throw new FormatException($"Số không hợp lệ: '{trimmed}'");

                    result.Add(num);
                }
            }

            return result.Count > 0 ? result : null;
        }

        /// <summary>
        /// Try-parse version, return false nếu cú pháp không hợp lệ.
        /// </summary>
        public static bool TryParse(string input, out HashSet<int> result, out string errorMessage)
        {
            try
            {
                result = Parse(input);
                errorMessage = null;
                return true;
            }
            catch (FormatException ex)
            {
                result = null;
                errorMessage = ex.Message;
                return false;
            }
        }

        /// <summary>
        /// Tạo chuỗi mô tả ngắn gọn từ HashSet, ví dụ: "1-3, 5, 9-10"
        /// </summary>
        public static string ToDisplayString(HashSet<int> chapters)
        {
            if (chapters == null || chapters.Count == 0)
                return "Tất cả";

            var sorted = chapters.OrderBy(c => c).ToList();
            var ranges = new List<string>();
            int rangeStart = sorted[0];
            int rangePrev = sorted[0];

            for (int i = 1; i < sorted.Count; i++)
            {
                if (sorted[i] == rangePrev + 1)
                {
                    rangePrev = sorted[i];
                }
                else
                {
                    ranges.Add(rangeStart == rangePrev ? rangeStart.ToString() : $"{rangeStart}-{rangePrev}");
                    rangeStart = sorted[i];
                    rangePrev = sorted[i];
                }
            }
            ranges.Add(rangeStart == rangePrev ? rangeStart.ToString() : $"{rangeStart}-{rangePrev}");

            return string.Join(", ", ranges);
        }
    }
}
