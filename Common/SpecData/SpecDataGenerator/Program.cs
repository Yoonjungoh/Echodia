// ============================================================
// SpecDataGenerator — Google Sheet → C# 코드 자동 생성기
// 실행: GenSpecData.bat (Common/SpecData/ 에서)
//
// 인자:
//   --id     <spreadsheetId>    (필수)
//   --key    <googleApiKey>     (필수, 시트 자동 탐색)
//   --proto  <Protocol.cs경로>  (선택, 기본: ../protoc-3.12.3-win64/bin/Protocol.cs)
//   --sheets <name1,name2,...>  (선택, 생략 시 API로 자동 탐색)
// ============================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

class Program
{
    static readonly HttpClient Http = new HttpClient();

    // ── CLI 인자
    static string SpreadsheetId = "15DaZH8xH5lCG-37xep0nZ66eFRW9H04uSKYgUccrtXo";
    static string ApiKey        = "";
    static string ProtoCsPath   = "../protoc-3.12.3-win64/bin/Protocol.cs";
    static readonly List<string> SheetNames = new();   // 비어 있으면 API로 자동 탐색

    // ── 출력 경로 (배치 파일 실행 위치 Common/SpecData/ 기준)
    const string CLIENT_DATA    = "../../Client/Assets/Scripts/Data/";
    const string CLIENT_MANAGER = "../../Client/Assets/Scripts/Managers/Core/";
    const string SERVER_DATA    = "../../Server/Server/Data/";

    // ── 상태
    static readonly HashSet<string> ProtoEnumNames = new();
    static readonly Dictionary<string, SheetParseResult> Results = new();

    // ────────────────────────────────────────────────────────
    static async Task Main(string[] args)
    {
        ParseArgs(args);

        Log("=== SpecDataGenerator 시작 ===");
        Log($"SpreadsheetId : {SpreadsheetId}");
        Log($"Protocol.cs   : {ProtoCsPath}");

        // --sheets 없으면 Google Sheets API v4로 자동 탐색
        if (SheetNames.Count == 0)
        {
            if (string.IsNullOrEmpty(ApiKey))
            {
                Console.Error.WriteLine("[ERROR] --key (Google API Key) 가 필요합니다.");
                Console.Error.WriteLine("        또는 --sheets 로 시트 이름을 직접 지정하세요.");
                Environment.Exit(1);
            }
            await DiscoverSheets();
        }

        if (SheetNames.Count == 0)
        {
            Console.Error.WriteLine("[ERROR] 시트를 찾지 못했습니다. Spreadsheet ID와 API Key를 확인하세요.");
            Environment.Exit(1);
        }

        Log($"시트 목록     : {string.Join(", ", SheetNames)}");
        Log("");

        ExtractProtoEnums();
        await FetchAllSheets();
        GenerateAll();

        Log("\n=== 완료 ===");
    }

    // ── CLI 파싱
    static void ParseArgs(string[] args)
    {
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--id":    if (i + 1 < args.Length) SpreadsheetId = args[++i]; break;
                case "--key":   if (i + 1 < args.Length) ApiKey        = args[++i]; break;
                case "--proto": if (i + 1 < args.Length) ProtoCsPath   = args[++i]; break;
                case "--sheets":
                    if (i + 1 < args.Length)
                        foreach (var s in args[++i].Split(','))
                        { string n = s.Trim(); if (!string.IsNullOrEmpty(n)) SheetNames.Add(n); }
                    break;
            }
        }
    }

    // ────────────────────────────────────────────────────────
    // Google Sheets API v4 로 시트 목록 자동 탐색
    // ────────────────────────────────────────────────────────
    static async Task DiscoverSheets()
    {
        Log("\n=== 시트 자동 탐색 (Google Sheets API v4) ===");
        string url = $"https://sheets.googleapis.com/v4/spreadsheets/{SpreadsheetId}" +
                     $"?fields=sheets.properties.title&key={ApiKey}";
        try
        {
            string json = await Http.GetStringAsync(url);
            var discovered = ParseSheetTitlesFromV4Json(json);

            if (discovered.Count == 0)
            {
                Console.Error.WriteLine("[WARN] 시트 목록 파싱 실패. 응답:");
                Console.Error.WriteLine(json.Substring(0, Math.Min(400, json.Length)));
                return;
            }

            SheetNames.AddRange(discovered);
            Log($"  ✓ {discovered.Count}개 시트 발견: {string.Join(", ", discovered)}");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[ERROR] 시트 탐색 실패: {ex.Message}");
        }
    }

    // "sheets":[{"properties":{"title":"SheetName"}},...]  파싱
    static List<string> ParseSheetTitlesFromV4Json(string json)
    {
        var result = new List<string>();
        // "title":"SheetName" 패턴 추출
        foreach (Match m in Regex.Matches(json, "\"title\"\\s*:\\s*\"([^\"]+)\""))
            result.Add(m.Groups[1].Value.Trim());
        return result;
    }

    // ────────────────────────────────────────────────────────
    // 1. Protocol.cs → proto enum 이름 추출
    // ────────────────────────────────────────────────────────
    static void ExtractProtoEnums()
    {
        string path = Path.IsPathRooted(ProtoCsPath)
            ? ProtoCsPath
            : Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), ProtoCsPath));

        if (!File.Exists(path))
        {
            Console.Error.WriteLine($"[WARN] Protocol.cs 없음: {path}");
            Console.Error.WriteLine("       proto enum 매칭을 건너뜁니다.");
            return;
        }

        string content = File.ReadAllText(path, Encoding.UTF8);
        foreach (Match m in Regex.Matches(content, @"public\s+enum\s+(\w+)"))
            ProtoEnumNames.Add(m.Groups[1].Value);

        Log($"[proto enum] {ProtoEnumNames.Count}개: {string.Join(", ", ProtoEnumNames)}");
    }

    // ────────────────────────────────────────────────────────
    // 2. 시트 구조 다운로드
    // ────────────────────────────────────────────────────────
    static async Task FetchAllSheets()
    {
        Log("\n=== 시트 구조 다운로드 ===");
        foreach (string name in SheetNames)
        {
            Log($"  → {name} ...");
            try
            {
                string url = $"https://docs.google.com/spreadsheets/d/{SpreadsheetId}" +
                             $"/gviz/tq?tqx=out:json&sheet={Uri.EscapeDataString(name)}";
                string raw = await Http.GetStringAsync(url);
                var result = ParseSheetStructure(name, raw);
                Results[name] = result;
                Log($"    ✓ 컬럼 {result.Columns.Count}개 / 데이터 {result.SampleRows.Count}행");
                foreach (var col in result.Columns)
                    Log($"      [{col.TypeHint}] {col.FieldName}");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"  [ERROR] {name}: {ex.Message}");
            }
        }
    }

    static SheetParseResult ParseSheetStructure(string sheetName, string raw)
    {
        var result = new SheetParseResult { SheetName = sheetName };
        List<string[]> rows = GvizParser.Parse(raw);

        if (rows.Count < 2)
        {
            Console.Error.WriteLine($"  [WARN] {sheetName}: 행 부족 (Row1 타입힌트 + Row2 헤더 필요)");
            result.Columns    = new();
            result.SampleRows = new();
            return result;
        }

        string[] typeRow   = rows[0];
        string[] headerRow = rows[1];

        result.Columns = new();
        for (int i = 0; i < headerRow.Length; i++)
        {
            string field = headerRow[i].Trim();
            if (string.IsNullOrEmpty(field)) continue;
            string hint = i < typeRow.Length ? typeRow[i].Trim().ToLower() : "string";
            result.Columns.Add(new ColumnInfo { FieldName = field, TypeHint = hint, ColIndex = i });
        }

        result.SampleRows = new();
        for (int r = 2; r < rows.Count; r++)
        {
            string[] row = rows[r];
            if (row.All(c => string.IsNullOrWhiteSpace(c))) continue;
            result.SampleRows.Add(new List<string>(row));
        }
        return result;
    }

    // ────────────────────────────────────────────────────────
    // 3. 전체 코드 생성
    // ────────────────────────────────────────────────────────
    static void GenerateAll()
    {
        Log("\n=== 코드 생성 ===");
        EnsureDir(CLIENT_DATA); EnsureDir(CLIENT_MANAGER); EnsureDir(SERVER_DATA);

        GenerateSheetEnums();
        GenerateMetaData();
        GenerateGoogleSheetConfig();
        GenerateServerGvizParser();
        GenerateClientSpecDataManager();
        GenerateServerSpecDataManager();

        if (Results.ContainsKey("Config"))
        {
            GenerateClientConfigManager();
            GenerateServerConfigManager();
        }
    }

    // ────────────────────────────────────────────────────────
    // SheetEnums.cs — 클라이언트/서버 각각
    // ────────────────────────────────────────────────────────
    static void GenerateSheetEnums()
    {
        string body = BuildSheetEnumsBody();
        if (body == null) return;

        // 클라이언트: 네임스페이스 없음
        var clientSb = new StringBuilder();
        AppendHeader(clientSb);
        clientSb.AppendLine();
        clientSb.Append(body);
        WriteFile(CLIENT_DATA + "SheetEnums.cs", clientSb.ToString());

        // 서버: namespace Server.Data 감싸기
        var serverSb = new StringBuilder();
        AppendHeader(serverSb);
        serverSb.AppendLine();
        serverSb.AppendLine("namespace Server.Data");
        serverSb.AppendLine("{");
        foreach (var line in body.Split('\n'))
            serverSb.AppendLine("    " + line.TrimEnd('\r'));
        serverSb.AppendLine("}");
        WriteFile(SERVER_DATA + "SheetEnums.cs", serverSb.ToString());
    }

    // enum 정의 본문 생성 (네임스페이스 제외). null = 아무것도 없음
    static string? BuildSheetEnumsBody()
    {
        var sb = new StringBuilder();
        bool any = false;
        var written = new HashSet<string>();

        foreach (var kv in Results)
        {
            foreach (var col in kv.Value.Columns)
            {
                if (col.TypeHint != "enum") continue;
                if (IsProtoEnum(col.FieldName)) continue;  // proto enum은 SheetEnums에 넣지 않음
                if (!written.Add(col.FieldName)) continue;

                any = true;
                sb.AppendLine($"public enum {col.FieldName}");
                sb.AppendLine("{");
                sb.AppendLine("    None = 0,");
                int idx  = kv.Value.Columns.IndexOf(col);
                var seen = new HashSet<string> { "None" };
                foreach (var row in kv.Value.SampleRows)
                {
                    string val = idx < row.Count ? row[idx].Trim() : "";
                    if (!string.IsNullOrEmpty(val) && seen.Add(val))
                        sb.AppendLine($"    {val},");
                }
                sb.AppendLine("}");
                sb.AppendLine();
            }

            // Config 시트 → ConfigType enum 생성
            if (kv.Key == "Config" && written.Add("ConfigType"))
            {
                var cfgTypeCol = kv.Value.Columns.Find(c => c.FieldName == "ConfigType");
                if (cfgTypeCol != null)
                {
                    any = true;
                    sb.AppendLine("public enum ConfigType");
                    sb.AppendLine("{");
                    sb.AppendLine("    None = 0,");
                    int idx  = kv.Value.Columns.IndexOf(cfgTypeCol);
                    var seen = new HashSet<string> { "None" };
                    foreach (var row in kv.Value.SampleRows)
                    {
                        string val = idx < row.Count ? row[idx].Trim() : "";
                        if (!string.IsNullOrEmpty(val) && seen.Add(val))
                            sb.AppendLine($"    {val},");
                    }
                    sb.AppendLine("}");
                    sb.AppendLine();
                }
            }
        }

        if (!any) { Log("  (sheet enum 없음, SheetEnums.cs 스킵)"); return null; }
        return sb.ToString();
    }

    // ────────────────────────────────────────────────────────
    // MetaData.cs — 클라이언트/서버 각각
    // ────────────────────────────────────────────────────────
    static void GenerateMetaData()
    {
        bool needsProto = NeedsProtoUsing();

        // 클라이언트
        var clientSb = new StringBuilder();
        AppendHeader(clientSb);
        clientSb.AppendLine("using System;");
        if (needsProto) clientSb.AppendLine("using Google.Protobuf.Protocol;");
        clientSb.AppendLine();
        clientSb.AppendLine("// 모든 MetaData 클래스는 이 파일에서 통합 관리합니다.");

        foreach (var kv in Results)
        {
            if (kv.Key == "Config") continue;
            clientSb.AppendLine();
            AppendMetaDataClass(clientSb, kv.Value.SheetName, kv.Value.Columns, "");
        }
        WriteFile(CLIENT_DATA + "MetaData.cs", clientSb.ToString());

        // 서버
        var serverSb = new StringBuilder();
        AppendHeader(serverSb);
        serverSb.AppendLine("using System;");
        if (needsProto) serverSb.AppendLine("using Google.Protobuf.Protocol;");
        serverSb.AppendLine();
        serverSb.AppendLine("namespace Server.Data");
        serverSb.AppendLine("{");
        serverSb.AppendLine("// 모든 MetaData 클래스는 이 파일에서 통합 관리합니다.");

        foreach (var kv in Results)
        {
            if (kv.Key == "Config") continue;
            serverSb.AppendLine();
            AppendMetaDataClass(serverSb, kv.Value.SheetName, kv.Value.Columns, "    ");
        }
        serverSb.AppendLine("}");
        WriteFile(SERVER_DATA + "MetaData.cs", serverSb.ToString());
    }

    static void AppendMetaDataClass(StringBuilder sb, string name, List<ColumnInfo> cols, string indent)
    {
        string i1 = indent;
        string i2 = indent + "    ";
        string i3 = indent + "        ";

        sb.AppendLine($"{i1}// ── {name}");
        sb.AppendLine($"{i1}[Serializable]");
        sb.AppendLine($"{i1}public class {name}MetaData");
        sb.AppendLine($"{i1}{{");

        foreach (var col in cols)
        {
            if (col.TypeHint == "protoenum")
            {
                sb.AppendLine($"{i2}public int {col.FieldName}Value;");
                sb.AppendLine($"{i2}public {col.FieldName} Get{col.FieldName}()");
                sb.AppendLine($"{i2}{{");
                sb.AppendLine($"{i3}return ({col.FieldName}){col.FieldName}Value;");
                sb.AppendLine($"{i2}}}");
            }
            else
            {
                sb.AppendLine($"{i2}public {HintToCsType(col.TypeHint, col.FieldName)} {col.FieldName};");
            }
        }

        sb.AppendLine($"{i1}}}");
    }

    // ────────────────────────────────────────────────────────
    // GoogleSheetConfig.cs — 클라이언트/서버 각각
    // ────────────────────────────────────────────────────────
    static void GenerateGoogleSheetConfig()
    {
        string body = BuildGoogleSheetConfigBody();

        // 클라이언트: 네임스페이스 없음
        var clientSb = new StringBuilder();
        AppendHeader(clientSb);
        clientSb.AppendLine("using System.Collections.Generic;");
        clientSb.AppendLine();
        clientSb.Append(body);
        WriteFile(CLIENT_DATA + "GoogleSheetConfig.cs", clientSb.ToString());

        // 서버: namespace Server.Data 감싸기
        var serverSb = new StringBuilder();
        AppendHeader(serverSb);
        serverSb.AppendLine("using System.Collections.Generic;");
        serverSb.AppendLine();
        serverSb.AppendLine("namespace Server.Data");
        serverSb.AppendLine("{");
        foreach (var line in body.Split('\n'))
            serverSb.AppendLine("    " + line.TrimEnd('\r'));
        serverSb.AppendLine("}");
        WriteFile(SERVER_DATA + "GoogleSheetConfig.cs", serverSb.ToString());
    }

    static string BuildGoogleSheetConfigBody()
    {
        var sb = new StringBuilder();
        sb.AppendLine("public static class GoogleSheetConfig");
        sb.AppendLine("{");
        sb.AppendLine($"    public const string SpreadsheetId = \"{SpreadsheetId}\";");
        sb.AppendLine();
        sb.AppendLine("    public static readonly List<string> SheetNames =");
        sb.AppendLine("        new List<string>");
        sb.AppendLine("    {");
        foreach (string name in SheetNames)
            sb.AppendLine($"        \"{name}\",");
        sb.AppendLine("    };");
        sb.AppendLine();
        sb.AppendLine("    public static string BuildJsonUrl(string sheetName)");
        sb.AppendLine("    {");
        sb.AppendLine("        return string.Format(");
        sb.AppendLine("            \"https://docs.google.com/spreadsheets/d/{0}/gviz/tq?tqx=out:json&sheet={1}\",");
        sb.AppendLine("            SpreadsheetId, System.Uri.EscapeDataString(sheetName));");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    // ────────────────────────────────────────────────────────
    // 서버용 GvizParser.cs (namespace Server.Data 포함)
    // ────────────────────────────────────────────────────────
    static void GenerateServerGvizParser()
    {
        // dotnet run은 Common/SpecData/ 에서 실행되므로
        // SpecDataGenerator/GvizParser.cs 경로 시도
        string srcPath = Path.GetFullPath(Path.Combine(
            Directory.GetCurrentDirectory(), "SpecDataGenerator", "GvizParser.cs"));

        if (!File.Exists(srcPath))
        {
            Console.Error.WriteLine($"  [WARN] GvizParser.cs 소스 없음: {srcPath}");
            Console.Error.WriteLine("         Server/Server/Data/GvizParser.cs 를 수동으로 복사하세요.");
            return;
        }

        string src = File.ReadAllText(srcPath, Encoding.UTF8);

        // using 라인과 클래스 본문 분리 (using은 namespace 밖에 있어야 함)
        var usingLines  = new List<string>();
        var bodyLines   = new List<string>();
        bool inBody     = false;
        foreach (var raw in src.Split('\n'))
        {
            string line = raw.TrimEnd('\r');
            if (!inBody && line.TrimStart().StartsWith("using "))
                usingLines.Add(line.Trim());
            else
            {
                inBody = true;
                bodyLines.Add(line);
            }
        }
        // 중복 제거 후 필수 using 보장
        var usings = new HashSet<string>(usingLines) { "using System;", "using System.Collections.Generic;" };

        var sb = new StringBuilder();
        AppendHeader(sb);
        foreach (var u in usings.OrderBy(u => u))
            sb.AppendLine(u);
        sb.AppendLine();
        sb.AppendLine("namespace Server.Data");
        sb.AppendLine("{");
        foreach (var line in bodyLines)
        {
            if (!string.IsNullOrWhiteSpace(line))
                sb.AppendLine("    " + line);
            else
                sb.AppendLine();
        }
        sb.AppendLine("}");

        WriteFile(SERVER_DATA + "GvizParser.cs", sb.ToString());
    }

    // ────────────────────────────────────────────────────────
    // 클라이언트 SpecDataManager.cs
    // ────────────────────────────────────────────────────────
    static void GenerateClientSpecDataManager()
    {
        var sb = new StringBuilder();
        AppendHeader(sb);
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using UnityEngine;");
        sb.AppendLine("using UnityEngine.Networking;");
        if (NeedsProtoUsing()) sb.AppendLine("using Google.Protobuf.Protocol;");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Google Sheets JSON API를 런타임에 직접 호출하여 MetaData를 파싱합니다.");
        sb.AppendLine("/// 빌드 후에도 시트 데이터 변경이 즉시 반영됩니다 (재빌드 불필요).");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("public partial class SpecDataManager");
        sb.AppendLine("{");
        sb.AppendLine("    public bool IsReady { get; private set; }");
        sb.AppendLine();

        foreach (var kv in Results)
        {
            if (kv.Key == "Config") continue;
            string n = kv.Value.SheetName;
            string ln = LowerFirst(n);
            sb.AppendLine($"    Dictionary<int, {n}MetaData> _{ln}Dict = new Dictionary<int, {n}MetaData>();");
            sb.AppendLine($"    List<{n}MetaData>            _{ln}List = new List<{n}MetaData>();");
        }
        sb.AppendLine();

        sb.AppendLine("    public IEnumerator CoDownloadDataSheet()");
        sb.AppendLine("    {");
        sb.AppendLine("        IsReady = false;");
        sb.AppendLine("        Debug.Log(\"[SpecDataManager] 데이터 다운로드 시작\");");
        sb.AppendLine();
        foreach (var kv in Results)
            if (kv.Key != "Config") sb.AppendLine($"        yield return CoFetch_{kv.Value.SheetName}();");
        sb.AppendLine();
        sb.AppendLine("        IsReady = true;");
        sb.AppendLine("        Debug.Log(\"[SpecDataManager] 모든 데이터 로드 완료\");");
        sb.AppendLine("    }");
        sb.AppendLine();

        foreach (var kv in Results) { if (kv.Key != "Config") AppendClientFetch(sb, kv.Value); }
        foreach (var kv in Results) { if (kv.Key != "Config") AppendGetApi(sb, kv.Value, isServer: false); }

        AppendParseHelpers(sb, "    ");
        sb.AppendLine("}");

        WriteFile(CLIENT_MANAGER + "SpecDataManager.cs", sb.ToString());
    }

    static void AppendClientFetch(StringBuilder sb, SheetParseResult sheet)
    {
        string name = sheet.SheetName;
        string ln   = LowerFirst(name);
        var    cols = sheet.Columns;

        sb.AppendLine($"    IEnumerator CoFetch_{name}()");
        sb.AppendLine("    {");
        sb.AppendLine($"        string url = GoogleSheetConfig.BuildJsonUrl(\"{name}\");");
        sb.AppendLine("        using (UnityWebRequest req = UnityWebRequest.Get(url))");
        sb.AppendLine("        {");
        sb.AppendLine("            yield return req.SendWebRequest();");
        sb.AppendLine("            if (req.result != UnityWebRequest.Result.Success)");
        sb.AppendLine("            {");
        sb.AppendLine($"                Debug.LogError(\"[SpecDataManager] {name} 다운로드 실패: \" + req.error);");
        sb.AppendLine("                yield break;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine($"            _{ln}Dict.Clear();");
        sb.AppendLine($"            _{ln}List.Clear();");
        sb.AppendLine();
        sb.AppendLine($"            List<string[]> rows = GvizParser.Parse(req.downloadHandler.text, colCount: {cols.Count});");
        sb.AppendLine("            for (int i = 2; i < rows.Count; i++)");
        sb.AppendLine("            {");
        sb.AppendLine("                string[] cells = rows[i];");
        sb.AppendLine("                if (string.IsNullOrEmpty(cells[0])) continue;");
        sb.AppendLine("                int sheetRow = i + 1;");
        sb.AppendLine($"                {name}MetaData data = new {name}MetaData();");
        sb.AppendLine("                bool rowOk = true;");
        sb.AppendLine();

        foreach (var col in cols)
        {
            string target       = col.TypeHint == "protoenum" ? $"data.{col.FieldName}Value" : $"data.{col.FieldName}";
            string expr         = col.TypeHint == "protoenum" ? $"ParseInt(cells[{col.ColIndex}])" : BuildParseExpression($"cells[{col.ColIndex}]", col.TypeHint, col.FieldName);
            string displayField = col.TypeHint == "protoenum" ? $"{col.FieldName}Value" : col.FieldName;
            string displayType  = col.TypeHint == "protoenum" ? "protoenum (int)" : col.TypeHint;
            int    dispCol      = col.ColIndex + 1;

            sb.AppendLine($"                // {displayField} ({displayType})");
            sb.AppendLine("                try { " + target + " = " + expr + "; }");
            sb.AppendLine("                catch (Exception e)");
            sb.AppendLine("                {");
            sb.AppendLine($"                    Debug.LogWarning(\"[SpecDataManager] [{name}] 파싱 오류\\n\" +");
            sb.AppendLine($"                        \"  위치: 시트 행 \" + sheetRow + \", 열 {dispCol} ({displayField})\\n\" +");
            sb.AppendLine($"                        \"  타입: {displayType}\\n\" +");
            sb.AppendLine($"                        \"  값: \\\"\" + cells[{col.ColIndex}] + \"\\\"\\n\" +");
            sb.AppendLine($"                        \"  원인: \" + e.Message);");
            sb.AppendLine("                    rowOk = false;");
            sb.AppendLine("                }");
        }

        sb.AppendLine();
        sb.AppendLine("                if (rowOk)");
        sb.AppendLine("                {");
        sb.AppendLine($"                    _{ln}Dict[data.Id] = data;");
        sb.AppendLine($"                    _{ln}List.Add(data);");
        sb.AppendLine("                }");
        sb.AppendLine("                else");
        sb.AppendLine("                {");
        sb.AppendLine($"                    Debug.LogWarning(\"[SpecDataManager] [{name}] 행 \" + sheetRow + \" 파싱 오류로 스킵됨.\");");
        sb.AppendLine("                }");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine($"            Debug.Log(\"[SpecDataManager] {name} 로드 완료: \" + _{ln}List.Count + \"개\");");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine();
    }

    // ────────────────────────────────────────────────────────
    // 서버 SpecDataManager.cs
    // ────────────────────────────────────────────────────────
    static void GenerateServerSpecDataManager()
    {
        var sb = new StringBuilder();
        AppendHeader(sb);
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using System.Net.Http;");
        sb.AppendLine("using System.Threading.Tasks;");
        if (NeedsProtoUsing()) sb.AppendLine("using Google.Protobuf.Protocol;");
        sb.AppendLine();
        sb.AppendLine("namespace Server.Data");
        sb.AppendLine("{");
        sb.AppendLine("public partial class SpecDataManager");
        sb.AppendLine("{");
        sb.AppendLine("    #region Singleton");
        sb.AppendLine("    public static SpecDataManager Instance { get; } = new SpecDataManager();");
        sb.AppendLine("    SpecDataManager() { }");
        sb.AppendLine("    #endregion");
        sb.AppendLine();
        sb.AppendLine("    static readonly HttpClient Http = new HttpClient();");
        sb.AppendLine("    public bool IsReady { get; private set; }");
        sb.AppendLine();

        foreach (var kv in Results)
        {
            if (kv.Key == "Config") continue;
            string n = kv.Value.SheetName;
            string ln = LowerFirst(n);
            sb.AppendLine($"    Dictionary<int, {n}MetaData> _{ln}Dict = new Dictionary<int, {n}MetaData>();");
            sb.AppendLine($"    List<{n}MetaData>            _{ln}List = new List<{n}MetaData>();");
        }
        sb.AppendLine();

        sb.AppendLine("    public async Task DownloadDataSheet()");
        sb.AppendLine("    {");
        sb.AppendLine("        IsReady = false;");
        sb.AppendLine("        Console.WriteLine(\"[SpecDataManager] 데이터 다운로드 시작\");");
        sb.AppendLine();
        foreach (var kv in Results)
            if (kv.Key != "Config") sb.AppendLine($"        await Fetch_{kv.Value.SheetName}();");
        sb.AppendLine();
        sb.AppendLine("        IsReady = true;");
        sb.AppendLine("        Console.WriteLine(\"[SpecDataManager] 모든 데이터 로드 완료\");");
        sb.AppendLine("    }");
        sb.AppendLine();

        foreach (var kv in Results) { if (kv.Key != "Config") AppendServerFetch(sb, kv.Value); }
        foreach (var kv in Results) { if (kv.Key != "Config") AppendGetApi(sb, kv.Value, isServer: true); }

        AppendParseHelpers(sb, "    ");
        sb.AppendLine("}"); // class
        sb.AppendLine("}"); // namespace

        WriteFile(SERVER_DATA + "SpecDataManager.cs", sb.ToString());
    }

    static void AppendServerFetch(StringBuilder sb, SheetParseResult sheet)
    {
        string name = sheet.SheetName;
        string ln   = LowerFirst(name);
        var    cols = sheet.Columns;

        sb.AppendLine($"    async Task Fetch_{name}()");
        sb.AppendLine("    {");
        sb.AppendLine($"        string url = GoogleSheetConfig.BuildJsonUrl(\"{name}\");");
        sb.AppendLine("        try");
        sb.AppendLine("        {");
        sb.AppendLine("            string raw = await Http.GetStringAsync(url);");
        sb.AppendLine($"            _{ln}Dict.Clear();");
        sb.AppendLine($"            _{ln}List.Clear();");
        sb.AppendLine();
        sb.AppendLine($"            List<string[]> rows = GvizParser.Parse(raw, colCount: {cols.Count});");
        sb.AppendLine("            for (int i = 2; i < rows.Count; i++)");
        sb.AppendLine("            {");
        sb.AppendLine("                string[] cells = rows[i];");
        sb.AppendLine("                if (string.IsNullOrEmpty(cells[0])) continue;");
        sb.AppendLine("                int sheetRow = i + 1;");
        sb.AppendLine($"                {name}MetaData data = new {name}MetaData();");
        sb.AppendLine("                bool rowOk = true;");
        sb.AppendLine();

        foreach (var col in cols)
        {
            string target       = col.TypeHint == "protoenum" ? $"data.{col.FieldName}Value" : $"data.{col.FieldName}";
            string expr         = col.TypeHint == "protoenum" ? $"ParseInt(cells[{col.ColIndex}])" : BuildParseExpression($"cells[{col.ColIndex}]", col.TypeHint, col.FieldName);
            string displayField = col.TypeHint == "protoenum" ? $"{col.FieldName}Value" : col.FieldName;
            string displayType  = col.TypeHint == "protoenum" ? "protoenum (int)" : col.TypeHint;
            int    dispCol      = col.ColIndex + 1;

            sb.AppendLine($"                // {displayField} ({displayType})");
            sb.AppendLine("                try { " + target + " = " + expr + "; }");
            sb.AppendLine("                catch (Exception e)");
            sb.AppendLine("                {");
            sb.AppendLine($"                    Console.Error.WriteLine(\"[SpecDataManager] [{name}] 파싱 오류\\n\" +");
            sb.AppendLine($"                        \"  위치: 시트 행 \" + sheetRow + \", 열 {dispCol} ({displayField})\\n\" +");
            sb.AppendLine($"                        \"  타입: {displayType}\\n\" +");
            sb.AppendLine($"                        \"  값: \\\"\" + cells[{col.ColIndex}] + \"\\\"\\n\" +");
            sb.AppendLine($"                        \"  원인: \" + e.Message);");
            sb.AppendLine("                    rowOk = false;");
            sb.AppendLine("                }");
        }

        sb.AppendLine();
        sb.AppendLine("                if (rowOk)");
        sb.AppendLine("                {");
        sb.AppendLine($"                    _{ln}Dict[data.Id] = data;");
        sb.AppendLine($"                    _{ln}List.Add(data);");
        sb.AppendLine("                }");
        sb.AppendLine("                else");
        sb.AppendLine("                {");
        sb.AppendLine($"                    Console.Error.WriteLine(\"[SpecDataManager] [{name}] 행 \" + sheetRow + \" 파싱 오류로 스킵됨.\");");
        sb.AppendLine("                }");
        sb.AppendLine("            }");
        sb.AppendLine("            Console.WriteLine(\"[SpecDataManager] " + name + " 로드 완료: \" + _" + ln + "List.Count + \"개\");");
        sb.AppendLine("        }");
        sb.AppendLine("        catch (Exception ex)");
        sb.AppendLine("        {");
        sb.AppendLine("            Console.Error.WriteLine(\"[SpecDataManager] " + name + " 다운로드 실패: \" + ex.Message);");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine();
    }

    // Get API (클라이언트/서버 공통 구조)
    static void AppendGetApi(StringBuilder sb, SheetParseResult sheet, bool isServer)
    {
        string name = sheet.SheetName;
        string ln   = LowerFirst(name);
        string _ = isServer ? "" : "";  // 인덴트 같음

        sb.AppendLine($"    public {name}MetaData Get{name}(int id)");
        sb.AppendLine("    {");
        sb.AppendLine($"        _{ln}Dict.TryGetValue(id, out {name}MetaData result);");
        sb.AppendLine("        return result;");
        sb.AppendLine("    }");
        sb.AppendLine();

        // protoenum 오버로드
        foreach (var col in sheet.Columns)
        {
            if (col.TypeHint != "protoenum") continue;
            sb.AppendLine($"    public {name}MetaData Get{name}({col.FieldName} type)");
            sb.AppendLine("    {");
            sb.AppendLine($"        return Get{name}((int)type);");
            sb.AppendLine("    }");
            sb.AppendLine();
        }

        // proto enum 직접 사용 오버로드
        foreach (var col in sheet.Columns)
        {
            if (col.TypeHint != "enum" || !IsProtoEnum(col.FieldName)) continue;
            sb.AppendLine($"    public {name}MetaData Get{name}({col.FieldName} key)");
            sb.AppendLine("    {");
            sb.AppendLine($"        return Get{name}((int)key);");
            sb.AppendLine("    }");
            sb.AppendLine();
        }

        sb.AppendLine($"    public List<{name}MetaData> GetAll{name}()");
        sb.AppendLine("    {");
        sb.AppendLine($"        return _{ln}List;");
        sb.AppendLine("    }");
        sb.AppendLine();
    }

    // ────────────────────────────────────────────────────────
    // 클라이언트 ConfigManager.cs
    // ────────────────────────────────────────────────────────
    static void GenerateClientConfigManager()
    {
        if (!Results.TryGetValue("Config", out var cfg)) return;
        (int idIdx, int dtIdx, int ctIdx, int valIdx) = GetConfigColIndices(cfg);

        var sb = new StringBuilder();
        AppendHeader(sb);
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using UnityEngine;");
        sb.AppendLine("using UnityEngine.Networking;");
        sb.AppendLine();
        sb.AppendLine("public class ConfigManager");
        sb.AppendLine("{");
        sb.AppendLine("    public bool IsReady { get; private set; }");
        sb.AppendLine("    struct ConfigEntry { public string DataType; public string Value; }");
        sb.AppendLine("    readonly Dictionary<ConfigType, ConfigEntry> _dict = new Dictionary<ConfigType, ConfigEntry>();");
        sb.AppendLine();
        sb.AppendLine("    public IEnumerator CoDownloadConfig()");
        sb.AppendLine("    {");
        sb.AppendLine("        IsReady = false;");
        sb.AppendLine("        Debug.Log(\"[ConfigManager] Config 다운로드 시작\");");
        sb.AppendLine("        string url = GoogleSheetConfig.BuildJsonUrl(\"Config\");");
        sb.AppendLine("        using (UnityWebRequest req = UnityWebRequest.Get(url))");
        sb.AppendLine("        {");
        sb.AppendLine("            yield return req.SendWebRequest();");
        sb.AppendLine("            if (req.result != UnityWebRequest.Result.Success)");
        sb.AppendLine("            {");
        sb.AppendLine("                Debug.LogError(\"[ConfigManager] 다운로드 실패: \" + req.error);");
        sb.AppendLine("                IsReady = true; yield break;");
        sb.AppendLine("            }");
        sb.AppendLine("            _dict.Clear();");
        sb.AppendLine($"            List<string[]> rows = GvizParser.Parse(req.downloadHandler.text, colCount: {cfg.Columns.Count});");
        AppendConfigParseLoop(sb, idIdx, dtIdx, ctIdx, valIdx, isServer: false);
        sb.AppendLine("            Debug.Log(\"[ConfigManager] 로드 완료: \" + _dict.Count + \"개\");");
        sb.AppendLine("        }");
        sb.AppendLine("        IsReady = true;");
        sb.AppendLine("    }");
        sb.AppendLine();
        AppendConfigGetters(sb);
        sb.AppendLine("}");

        WriteFile(CLIENT_MANAGER + "ConfigManager.cs", sb.ToString());
    }

    // ────────────────────────────────────────────────────────
    // 서버 ConfigManager.cs
    // ────────────────────────────────────────────────────────
    static void GenerateServerConfigManager()
    {
        if (!Results.TryGetValue("Config", out var cfg)) return;
        (int idIdx, int dtIdx, int ctIdx, int valIdx) = GetConfigColIndices(cfg);

        var sb = new StringBuilder();
        AppendHeader(sb);
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using System.Net.Http;");
        sb.AppendLine("using System.Threading.Tasks;");
        sb.AppendLine();
        sb.AppendLine("namespace Server.Data");
        sb.AppendLine("{");
        sb.AppendLine("public class ConfigManager");
        sb.AppendLine("{");
        sb.AppendLine("    #region Singleton");
        sb.AppendLine("    public static ConfigManager Instance { get; } = new ConfigManager();");
        sb.AppendLine("    ConfigManager() { }");
        sb.AppendLine("    #endregion");
        sb.AppendLine();
        sb.AppendLine("    static readonly HttpClient Http = new HttpClient();");
        sb.AppendLine("    public bool IsReady { get; private set; }");
        sb.AppendLine("    struct ConfigEntry { public string DataType; public string Value; }");
        sb.AppendLine("    readonly Dictionary<ConfigType, ConfigEntry> _dict = new Dictionary<ConfigType, ConfigEntry>();");
        sb.AppendLine();
        sb.AppendLine("    public async Task DownloadConfig()");
        sb.AppendLine("    {");
        sb.AppendLine("        IsReady = false;");
        sb.AppendLine("        Console.WriteLine(\"[ConfigManager] Config 다운로드 시작\");");
        sb.AppendLine("        try");
        sb.AppendLine("        {");
        sb.AppendLine("            string url = GoogleSheetConfig.BuildJsonUrl(\"Config\");");
        sb.AppendLine("            string raw = await Http.GetStringAsync(url);");
        sb.AppendLine("            _dict.Clear();");
        sb.AppendLine($"            List<string[]> rows = GvizParser.Parse(raw, colCount: {cfg.Columns.Count});");
        AppendConfigParseLoop(sb, idIdx, dtIdx, ctIdx, valIdx, isServer: true);
        sb.AppendLine("            Console.WriteLine(\"[ConfigManager] 로드 완료: \" + _dict.Count + \"개\");");
        sb.AppendLine("        }");
        sb.AppendLine("        catch (Exception ex)");
        sb.AppendLine("        {");
        sb.AppendLine("            Console.Error.WriteLine(\"[ConfigManager] 다운로드 실패: \" + ex.Message);");
        sb.AppendLine("        }");
        sb.AppendLine("        IsReady = true;");
        sb.AppendLine("    }");
        sb.AppendLine();
        AppendConfigGetters(sb);
        sb.AppendLine("}"); // class
        sb.AppendLine("}"); // namespace

        WriteFile(SERVER_DATA + "ConfigManager.cs", sb.ToString());
    }

    static void AppendConfigParseLoop(StringBuilder sb, int idIdx, int dtIdx, int ctIdx, int valIdx, bool isServer)
    {
        string log = isServer ? "Console.Error.WriteLine" : "Debug.LogWarning";

        sb.AppendLine("            for (int i = 2; i < rows.Count; i++)");
        sb.AppendLine("            {");
        sb.AppendLine("                string[] cells = rows[i];");
        sb.AppendLine($"                if (string.IsNullOrEmpty(cells[{idIdx}])) continue;");
        sb.AppendLine("                int sheetRow = i + 1;");
        sb.AppendLine("                try");
        sb.AppendLine("                {");
        sb.AppendLine($"                    string dataType  = cells[{dtIdx}];");
        sb.AppendLine($"                    string configKey = cells[{ctIdx}];");
        sb.AppendLine($"                    string value     = cells[{valIdx}];");
        sb.AppendLine("                    if (!Enum.TryParse(configKey, true, out ConfigType key))");
        sb.AppendLine("                    {");
        sb.AppendLine($"                        {log}(\"[ConfigManager] [Config] 알 수 없는 ConfigType: \\\"\" + configKey + \"\\\" (시트 행 \" + sheetRow + \", 열 {ctIdx + 1})\");");
        sb.AppendLine("                        continue;");
        sb.AppendLine("                    }");
        sb.AppendLine("                    _dict[key] = new ConfigEntry { DataType = dataType, Value = value };");
        sb.AppendLine("                }");
        sb.AppendLine("                catch (Exception e)");
        sb.AppendLine("                {");
        sb.AppendLine($"                    {log}(\"[ConfigManager] [Config] 파싱 오류\\n\" +");
        sb.AppendLine($"                        \"  위치: 시트 행 \" + sheetRow + \"\\n\" +");
        sb.AppendLine($"                        \"  값: [\" + cells[{dtIdx}] + \", \" + cells[{ctIdx}] + \", \" + cells[{valIdx}] + \"]\\n\" +");
        sb.AppendLine($"                        \"  원인: \" + e.Message);");
        sb.AppendLine("                }");
        sb.AppendLine("            }");
    }

    static void AppendConfigGetters(StringBuilder sb)
    {
        sb.AppendLine("    public int GetInt(ConfigType key)");
        sb.AppendLine("    {");
        sb.AppendLine("        string s = GetRaw(key);");
        sb.AppendLine("        if (string.IsNullOrEmpty(s)) return 0;");
        sb.AppendLine("        if (s.Contains(\".\"))");
        sb.AppendLine("            return (int)float.Parse(s, System.Globalization.CultureInfo.InvariantCulture);");
        sb.AppendLine("        return int.Parse(s);");
        sb.AppendLine("    }");
        sb.AppendLine("    public float GetFloat(ConfigType key)");
        sb.AppendLine("    {");
        sb.AppendLine("        string s = GetRaw(key);");
        sb.AppendLine("        if (string.IsNullOrEmpty(s)) return 0f;");
        sb.AppendLine("        return float.Parse(s, System.Globalization.CultureInfo.InvariantCulture);");
        sb.AppendLine("    }");
        sb.AppendLine("    public string GetString(ConfigType key) => GetRaw(key);");
        sb.AppendLine("    public bool GetBool(ConfigType key)");
        sb.AppendLine("    {");
        sb.AppendLine("        string s = GetRaw(key);");
        sb.AppendLine("        return s == \"true\" || s == \"1\" || s == \"yes\";");
        sb.AppendLine("    }");
        sb.AppendLine("    public long GetLong(ConfigType key)");
        sb.AppendLine("    {");
        sb.AppendLine("        string s = GetRaw(key);");
        sb.AppendLine("        if (string.IsNullOrEmpty(s)) return 0L;");
        sb.AppendLine("        if (s.Contains(\".\"))");
        sb.AppendLine("            return (long)double.Parse(s, System.Globalization.CultureInfo.InvariantCulture);");
        sb.AppendLine("        return long.Parse(s);");
        sb.AppendLine("    }");
        sb.AppendLine("    public object GetAuto(ConfigType key)");
        sb.AppendLine("    {");
        sb.AppendLine("        if (!_dict.TryGetValue(key, out ConfigEntry entry)) return null;");
        sb.AppendLine("        return entry.DataType switch");
        sb.AppendLine("        {");
        sb.AppendLine("            \"int\"   => (object)GetInt(key),");
        sb.AppendLine("            \"float\" => (object)GetFloat(key),");
        sb.AppendLine("            \"bool\"  => (object)GetBool(key),");
        sb.AppendLine("            \"long\"  => (object)GetLong(key),");
        sb.AppendLine("            _       => (object)GetString(key),");
        sb.AppendLine("        };");
        sb.AppendLine("    }");
        sb.AppendLine("    string GetRaw(ConfigType key) =>");
        sb.AppendLine("        _dict.TryGetValue(key, out ConfigEntry e) ? e.Value : string.Empty;");
        sb.AppendLine();
    }

    // ────────────────────────────────────────────────────────
    // 유틸
    // ────────────────────────────────────────────────────────

    static (int id, int dt, int ct, int val) GetConfigColIndices(SheetParseResult cfg)
    {
        int idIdx = 0, dtIdx = 1, ctIdx = 2, valIdx = 3;
        foreach (var col in cfg.Columns)
        {
            switch (col.FieldName)
            {
                case "Id":         idIdx = col.ColIndex; break;
                case "DataType":   dtIdx = col.ColIndex; break;
                case "ConfigType": ctIdx = col.ColIndex; break;
                case "Value":      valIdx = col.ColIndex; break;
            }
        }
        return (idIdx, dtIdx, ctIdx, valIdx);
    }

    static string GetAssignLine(ColumnInfo col)
    {
        if (col.TypeHint == "protoenum")
            return $"{col.FieldName}Value = ParseInt(cells[{col.ColIndex}])";
        return $"{col.FieldName} = {BuildParseExpression($"cells[{col.ColIndex}]", col.TypeHint, col.FieldName)}";
    }

    static bool IsProtoEnum(string name) => ProtoEnumNames.Contains(name);

    static bool NeedsProtoUsing() =>
        ProtoEnumNames.Count > 0 &&
        Results.Values.Any(r => r.Columns.Any(c =>
            c.TypeHint == "protoenum" ||
            (c.TypeHint == "enum" && IsProtoEnum(c.FieldName))));

    static string HintToCsType(string hint, string fieldName) => hint switch
    {
        "int"    => "int",
        "float"  => "float",
        "double" => "double",
        "bool"   => "bool",
        "long"   => "long",
        "string" => "string",
        "enum"   => fieldName,
        _        => "string",
    };

    static string BuildParseExpression(string cellExpr, string hint, string fieldName) => hint switch
    {
        "int"    => $"ParseInt({cellExpr})",
        "float"  => $"ParseFloat({cellExpr})",
        "double" => $"string.IsNullOrEmpty({cellExpr}) ? 0.0 : double.Parse({cellExpr}, System.Globalization.CultureInfo.InvariantCulture)",
        "long"   => $"string.IsNullOrEmpty({cellExpr}) ? 0L : long.Parse({cellExpr})",
        "bool"   => $"({cellExpr} == \"true\" || {cellExpr} == \"1\" || {cellExpr} == \"yes\")",
        "string" => cellExpr,
        "enum"   => $"ParseEnum<{fieldName}>({cellExpr})",
        _        => cellExpr,
    };

    static void AppendParseHelpers(StringBuilder sb, string indent)
    {
        sb.AppendLine($"{indent}static int ParseInt(string s)");
        sb.AppendLine($"{indent}{{");
        sb.AppendLine($"{indent}    if (string.IsNullOrEmpty(s)) return 0;");
        sb.AppendLine($"{indent}    if (s.Contains(\".\"))");
        sb.AppendLine($"{indent}        return (int)float.Parse(s, System.Globalization.CultureInfo.InvariantCulture);");
        sb.AppendLine($"{indent}    return int.Parse(s);");
        sb.AppendLine($"{indent}}}");
        sb.AppendLine($"{indent}static float ParseFloat(string s)");
        sb.AppendLine($"{indent}{{");
        sb.AppendLine($"{indent}    if (string.IsNullOrEmpty(s)) return 0f;");
        sb.AppendLine($"{indent}    return float.Parse(s, System.Globalization.CultureInfo.InvariantCulture);");
        sb.AppendLine($"{indent}}}");
        sb.AppendLine($"{indent}static T ParseEnum<T>(string s) where T : struct");
        sb.AppendLine($"{indent}{{");
        sb.AppendLine($"{indent}    if (string.IsNullOrEmpty(s)) return default;");
        sb.AppendLine($"{indent}    return (T)Enum.Parse(typeof(T), s, true);");
        sb.AppendLine($"{indent}}}");
    }

    static string LowerFirst(string s) =>
        string.IsNullOrEmpty(s) ? s : char.ToLower(s[0]) + s.Substring(1);

    static void AppendHeader(StringBuilder sb)
    {
        sb.AppendLine("// ============================================================");
        sb.AppendLine("// Auto-generated by SpecDataGenerator.");
        sb.AppendLine("// Do NOT edit manually — 컬럼 구조 변경 시 GenSpecData.bat 재실행.");
        sb.AppendLine("// ============================================================");
    }

    static void WriteFile(string relativePath, string content)
    {
        string fullPath = Path.IsPathRooted(relativePath)
            ? relativePath
            : Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), relativePath));
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, content, new UTF8Encoding(false));
        Log($"  ✓ {Path.GetFileName(fullPath)} → {Path.GetDirectoryName(fullPath)}");
    }

    static void EnsureDir(string path)
    {
        string full = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), path));
        Directory.CreateDirectory(full);
    }

    static void Log(string msg) => Console.WriteLine(msg);
}

// ────────────────────────────────────────────────────────
// 내부 데이터 구조
// ────────────────────────────────────────────────────────
class SheetParseResult
{
    public string             SheetName  = "";
    public List<ColumnInfo>   Columns    = new();
    public List<List<string>> SampleRows = new();
}

class ColumnInfo
{
    public string FieldName = "";
    public string TypeHint  = "";
    public int    ColIndex;
}
