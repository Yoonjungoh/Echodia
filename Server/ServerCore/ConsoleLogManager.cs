using System;
using System.IO;
using System.Text;
using System.Diagnostics;

public class ConsoleLogManager
{
    private static StringBuilder _stringBuilder = new StringBuilder();
    public static ConsoleLogManager Instance { get; } = new ConsoleLogManager();

    private ConsoleLogManager()
    {
        Console.WriteLine("Start SaveLogs");
        AppDomain.CurrentDomain.ProcessExit -= SaveLogs;
        AppDomain.CurrentDomain.ProcessExit += SaveLogs;
    }

    // 일반 로그
    public void Log(string message)
    {
        string caller = GetCallerInfo();
        string currentDateTime = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string logMessage = $"[{currentDateTime}] ({caller}) : {message}";
        Console.WriteLine(logMessage);
        _stringBuilder.AppendLine(logMessage);
    }

    // 예외 로그
    public void Log(Exception e)
    {
        string caller = GetCallerInfo();
        string currentDateTime = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string logMessage = $"[{currentDateTime}] ({caller}) : {e}";
        Console.WriteLine(logMessage);
        _stringBuilder.AppendLine(logMessage);
    }

    // 스택 정보를 가져오는 함수
    private string GetCallerInfo()
    {
        // 2단계 위의 호출자 (0은 현재 메서드, 1은 Log, 2는 Log를 호출한 메서드)
        StackFrame frame = new StackTrace().GetFrame(2);
        var method = frame.GetMethod();
        string className = method.DeclaringType?.Name ?? "UnknownClass";
        string methodName = method.Name;
        return $"{className}.{methodName}";
    }
    
    private static void SaveLogs(object sender, EventArgs e)
    {
        string currentDateTime = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string fileName = $"ConsoleLog_{currentDateTime}.txt";
        string directory = "../../../../Logs";

        if (!Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        string fullPath = Path.Combine(directory, fileName);
        File.WriteAllText(fullPath, _stringBuilder.ToString());
    }
}
