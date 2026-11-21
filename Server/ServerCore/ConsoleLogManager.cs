using System;
using System.Diagnostics;
using System.IO;

public class ConsoleLogManager
{
    public static ConsoleLogManager Instance { get; } = new ConsoleLogManager();

    private readonly object _fileLock = new object();
    private StreamWriter _file;

    private ConsoleLogManager()
    {
        string directory = Environment.GetEnvironmentVariable("ECHODIA_LOG_DIRECTORY");
        if (string.IsNullOrWhiteSpace(directory))
        {
            // systemd captures stdout. Keep the existing file location for Windows services.
            if (!OperatingSystem.IsWindows())
                return;
            directory = Path.Combine("..", "..", "..", "..", "Logs");
        }

        try
        {
            Directory.CreateDirectory(directory);
            string filename = $"ConsoleLog_{DateTime.Now:yyyyMMdd_HHmmss}_{Environment.ProcessId}.txt";
            _file = new StreamWriter(Path.Combine(directory, filename), append: true);
            AppDomain.CurrentDomain.ProcessExit += (_, _) => CloseFile();
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"File logging unavailable: {error.Message}");
        }
    }

    public void Log(string message) => Write(message, new StackFrame(1).GetMethod());
    public void Log(Exception error) => Write(error.ToString(), new StackFrame(1).GetMethod());

    private void Write(string message, System.Reflection.MethodBase caller)
    {
        string line = $"[{DateTime.Now:yyyyMMdd_HHmmss}] ({caller?.DeclaringType?.Name}.{caller?.Name}) : {message}";
        Console.WriteLine(line);
        lock (_fileLock)
        {
            try { _file?.WriteLine(line); }
            catch (IOException error)
            {
                Console.Error.WriteLine($"File logging interrupted: {error.Message}");
                CloseFile();
            }
        }
    }

    private void CloseFile()
    {
        lock (_fileLock)
        {
            try { _file?.Dispose(); }
            catch (IOException error) { Console.Error.WriteLine(error.Message); }
            finally { _file = null; }
        }
    }
}
