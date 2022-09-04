using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace ModFinder.Util
{
  internal class Logger : IDisposable
  {
    internal static readonly string LogFile = Path.Combine(Main.AppFolder, "Log.txt");
    internal static Logger Log
    {
      get
      {
        _log ??= new();
        return _log;
      }
    }
    private static Logger _log;

    private readonly CancellationTokenSource CancelTokenSource = new();
    private readonly BlockingCollection<string> LogQueue = new();

    private Logger()
    {
      new Thread(new ThreadStart(() => ProcessLogs(CancelTokenSource.Token))).Start();
    }

    public void Info(string log)
    {
      LogQueue.Add($"[I] {log}");
    }

    public void Warning(string log)
    {
      LogQueue.Add($"[W] {log}");
    }

    public void Error(string log, Exception e = null)
    {
      LogQueue.Add($"[E] {log}");
      if (e is not null)
      {
        LogQueue.Add($"[E] {e}");
      }
    }

    public void Verbose(string log)
    {
      LogQueue.Add($"[V] {log}");
    }

    public void Dispose()
    {
      CancelTokenSource.Cancel();
    }

    private void ProcessLogs(CancellationToken cancellationToken)
    {
      Trace.WriteLine($"Logging to {LogFile}.");

      try
      {
        if (File.Exists(LogFile))
        {
          var oldLogFile = Path.Combine(Main.AppFolder, "Log_old.txt");
          Trace.WriteLine($"Moving old log to {oldLogFile}");
          File.Copy(LogFile, oldLogFile, overwrite: true);
          File.Delete(LogFile);
        }

        using (var stream = new FileStream(LogFile, FileMode.OpenOrCreate, FileAccess.ReadWrite))
        using (var writer = new StreamWriter(stream))
        {
          while (!cancellationToken.IsCancellationRequested)
          {
            if (LogQueue.TryTake(out string log, 5 * 1000, cancellationToken))
              writer.WriteLine($"[{DateTime.Now.ToString("M/d hh:mm:ss")}]{log}");
            else
              writer.Flush();
          }
        }
      }
      catch (Exception e)
      {
        Trace.WriteLine($"Logging was interrupted: {e}");
      }

      Trace.WriteLine("Logging has stopped.");
    }
  }
}
