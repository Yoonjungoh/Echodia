using System;
using System.Collections.Generic;

/// <summary>
/// Google Visualization JSON API 응답 파서 (Unity 의존성 없음).
///
/// gviz 응답 형식 (parsedNumHeaders:2 기준):
///   "cols":[{"id":"A","label":"int Id","type":"number"}, ...]
///   "rows":[
///     {"c":[{"v":1.0,"f":"1"},{"v":"Gold"},null,null,...]},
///     ...
///   ]
///
/// 핵심 주의사항:
///   - gviz는 parsedNumHeaders:N 일 때 시트의 상위 N개 행(타입힌트+헤더)을
///     cols[i].label에 합쳐서 내려줌 ("int Id" 형태).
///     즉 rows[] 배열은 데이터 행만 담고 있음 — 헤더 행이 없음!
///   - 이 파서는 cols[i].label을 분리해 rows[0]/rows[1]을 합성하여
///     항상 rows[0]=타입힌트, rows[1]=헤더, rows[2~]=데이터 계약을 유지함.
///
/// 반환: List&lt;string[]&gt;
///   [0] = 타입힌트 행 (int, float, enum, protoenum, ...)
///   [1] = 헤더 행    (Id, MonsterType, ...)
///   [2~] = 데이터 행
/// </summary>
public static class GvizParser
{
    public static List<string[]> Parse(string raw, int colCount = 0)
    {
        var result = new List<string[]>();

        if (string.IsNullOrEmpty(raw))
        {
            Console.Error.WriteLine("[GvizParser] 응답이 비어있습니다.");
            return result;
        }

        // ── Step 1: JSONP 래퍼 제거
        int jsonStart = raw.IndexOf('{');
        int jsonEnd   = raw.LastIndexOf('}');
        if (jsonStart < 0 || jsonEnd < 0 || jsonEnd <= jsonStart)
        {
            Console.Error.WriteLine("[GvizParser] JSON 영역 없음. RAW 앞부분:\n"
                + raw.Substring(0, Math.Min(300, raw.Length)));
            return result;
        }
        string json = raw.Substring(jsonStart, jsonEnd - jsonStart + 1);

        // ── Step 2: cols 레이블 추출 → rows[0]/rows[1] 합성
        List<string> colLabels = ExtractColLabels(json);

        if (colCount == 0)
            colCount = colLabels.Count > 0 ? colLabels.Count : CountCols(json);

        string[] typeRow   = new string[colCount];
        string[] headerRow = new string[colCount];
        for (int i = 0; i < colCount; i++)
        {
            string label = i < colLabels.Count ? colLabels[i] : "";
            int spaceIdx = label.IndexOf(' ');
            if (spaceIdx > 0)
            {
                typeRow[i]   = label.Substring(0, spaceIdx);
                headerRow[i] = label.Substring(spaceIdx + 1);
            }
            else
            {
                typeRow[i]   = "";
                headerRow[i] = label;
            }
        }
        result.Add(typeRow);
        result.Add(headerRow);

        // ── Step 3: "rows" 배열 찾기
        int rowsIdx = json.IndexOf("\"rows\"");
        if (rowsIdx < 0)
        {
            Console.Error.WriteLine("[GvizParser] \"rows\" 키 없음.");
            return result;
        }
        int rowsArrayStart = json.IndexOf('[', rowsIdx);
        if (rowsArrayStart < 0) return result;

        // ── Step 4: 각 행 파싱
        int pos = rowsArrayStart + 1;
        while (pos < json.Length)
        {
            int cStart = FindNext(json, "{\"c\"", pos);
            if (cStart < 0) break;

            int cellArrayStart = json.IndexOf('[', cStart + 4);
            if (cellArrayStart < 0) break;

            int cellArrayEnd = FindMatchingBracket(json, cellArrayStart);
            if (cellArrayEnd < 0) break;

            string cellsJson = json.Substring(
                cellArrayStart + 1,
                cellArrayEnd - cellArrayStart - 1);

            string[] row = ParseCellArray(cellsJson, colCount);
            result.Add(row);
            pos = cellArrayEnd + 1;
        }

        return result;
    }

    private static List<string> ExtractColLabels(string json)
    {
        var labels = new List<string>();
        int colsIdx = json.IndexOf("\"cols\"");
        if (colsIdx < 0) return labels;

        int colsStart = json.IndexOf('[', colsIdx);
        if (colsStart < 0) return labels;

        int colsEnd = FindMatchingBracket(json, colsStart);
        if (colsEnd < 0) return labels;

        int pos = colsStart + 1;
        while (pos < colsEnd)
        {
            int objStart = json.IndexOf('{', pos);
            if (objStart < 0 || objStart >= colsEnd) break;

            int objEnd = FindMatchingBrace(json, objStart);
            if (objEnd < 0 || objEnd > colsEnd) break;

            string colObj = json.Substring(objStart, objEnd - objStart + 1);
            labels.Add(ExtractStringField(colObj, "label"));
            pos = objEnd + 1;
        }
        return labels;
    }

    private static string ExtractStringField(string obj, string fieldName)
    {
        string key = "\"" + fieldName + "\"";
        int idx = obj.IndexOf(key);
        if (idx < 0) return "";

        int colon = obj.IndexOf(':', idx + key.Length);
        if (colon < 0) return "";

        int vs = colon + 1;
        while (vs < obj.Length && obj[vs] == ' ') vs++;
        if (vs >= obj.Length || obj[vs] != '"') return "";

        int end = vs + 1;
        while (end < obj.Length)
        {
            if (obj[end] == '\\') { end += 2; continue; }
            if (obj[end] == '"') break;
            end++;
        }
        string s = obj.Substring(vs + 1, end - vs - 1);
        return s.Replace("\\\"", "\"").Replace("\\\\", "\\").Replace("\\n", "\n");
    }

    private static int CountCols(string json)
    {
        int colsIdx = json.IndexOf("\"cols\"");
        if (colsIdx < 0) return 0;

        int colsStart = json.IndexOf('[', colsIdx);
        if (colsStart < 0) return 0;

        int colsEnd = FindMatchingBracket(json, colsStart);
        if (colsEnd < 0) return 0;

        int count = 0, depth = 0;
        bool inStr = false;
        for (int i = colsStart; i <= colsEnd; i++)
        {
            char c = json[i];
            if (inStr) { if (c == '\\') i++; else if (c == '"') inStr = false; continue; }
            if (c == '"') { inStr = true; continue; }
            if (c == '{') { depth++; if (depth == 1) count++; }
            else if (c == '}') depth--;
        }
        return count;
    }

    private static string[] ParseCellArray(string cellsJson, int colCount)
    {
        var cells = new List<string>();
        int pos = 0, len = cellsJson.Length;

        while (pos < len)
        {
            while (pos < len && (cellsJson[pos] == ',' || cellsJson[pos] == ' '
                || cellsJson[pos] == '\r' || cellsJson[pos] == '\n'
                || cellsJson[pos] == '\t')) pos++;
            if (pos >= len) break;

            if (cellsJson[pos] == '{')
            {
                int cellEnd = FindMatchingBrace(cellsJson, pos);
                if (cellEnd < 0) break;
                string cellJson = cellsJson.Substring(pos, cellEnd - pos + 1);
                cells.Add(ExtractV(cellJson));
                pos = cellEnd + 1;
            }
            else if (pos + 3 < len && cellsJson.Substring(pos, 4) == "null")
            {
                cells.Add(string.Empty);
                pos += 4;
            }
            else
            {
                pos++;
            }
        }

        while (cells.Count < colCount)
            cells.Add(string.Empty);

        return cells.ToArray();
    }

    private static string ExtractV(string cell)
    {
        int vIdx = cell.IndexOf("\"v\"");
        if (vIdx < 0) return string.Empty;

        int colon = cell.IndexOf(':', vIdx + 3);
        if (colon < 0) return string.Empty;

        int vs = colon + 1;
        while (vs < cell.Length && cell[vs] == ' ') vs++;
        if (vs >= cell.Length) return string.Empty;

        char first = cell[vs];

        if (vs + 3 < cell.Length && cell.Substring(vs, 4) == "null")
            return string.Empty;
        if (vs + 3 < cell.Length && cell.Substring(vs, 4) == "true")
            return "true";
        if (vs + 4 < cell.Length && cell.Substring(vs, 5) == "false")
            return "false";

        if (first == '"')
        {
            int end = vs + 1;
            while (end < cell.Length)
            {
                if (cell[end] == '\\') { end += 2; continue; }
                if (cell[end] == '"') break;
                end++;
            }
            string s = cell.Substring(vs + 1, end - vs - 1);
            return s.Replace("\\\"", "\"").Replace("\\\\", "\\").Replace("\\n", "\n");
        }

        if (char.IsDigit(first) || first == '-')
        {
            int end = vs;
            while (end < cell.Length
                && (char.IsDigit(cell[end]) || cell[end] == '.'
                    || cell[end] == '-' || cell[end] == 'E'
                    || cell[end] == 'e' || cell[end] == '+'))
                end++;

            string num = cell.Substring(vs, end - vs);

            if (float.TryParse(num,
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out float f))
            {
                if (f == Math.Floor(f) && !float.IsInfinity(f))
                    return ((long)f).ToString();
                return f.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }
            return num;
        }

        return string.Empty;
    }

    private static int FindNext(string s, string target, int from)
        => s.IndexOf(target, from, StringComparison.Ordinal);

    private static int FindMatchingBracket(string s, int open)
    {
        int depth = 0;
        bool inStr = false;
        for (int i = open; i < s.Length; i++)
        {
            if (inStr) { if (s[i] == '\\') i++; else if (s[i] == '"') inStr = false; continue; }
            if (s[i] == '"') { inStr = true; continue; }
            if (s[i] == '[') depth++;
            else if (s[i] == ']') { depth--; if (depth == 0) return i; }
        }
        return -1;
    }

    private static int FindMatchingBrace(string s, int open)
    {
        int depth = 0;
        bool inStr = false;
        for (int i = open; i < s.Length; i++)
        {
            if (inStr) { if (s[i] == '\\') i++; else if (s[i] == '"') inStr = false; continue; }
            if (s[i] == '"') { inStr = true; continue; }
            if (s[i] == '{') depth++;
            else if (s[i] == '}') { depth--; if (depth == 0) return i; }
        }
        return -1;
    }
}
