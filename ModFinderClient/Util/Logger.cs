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
      LogQueue.Add($"[---Info]{log}");
    }

    public void Warning(string log)
    {
      LogQueue.Add($"[Warning]{log}");
    }

    public void Error(string log, Exception e = null)
    {
      LogQueue.Add($"[--Error]{log}");
      if (e is not null)
      {
        LogQueue.Add($"[--Error]{e}");
      }
    }

    public void Verbose(string log)
    {
      LogQueue.Add($"[Verbose]{log}");
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
        }

        using (var stream = new FileStream(LogFile, FileMode.OpenOrCreate, FileAccess.Write))
        using (var writer = new StreamWriter(stream))
        {
          while (!cancellationToken.IsCancellationRequested)
          {
            if (LogQueue.TryTake(out string log, 5 * 1000, cancellationToken))
              writer.WriteLine(log);
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
