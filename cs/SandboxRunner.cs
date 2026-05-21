using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace AbiturEliteCode.cs;

public static class SandboxRunner
{
    public static void InterceptSandbox(string[] args)
    {
        if (args.Length > 0 && args[0] == "--sandbox-run")
        {
            // keep a reference to the real stdout so we can talk to the parent app later
            var originalOut = Console.Out;

            // self-monitoring background thread for the sandbox process
            var monitorThread = new Thread(() =>
            {
                try
                {
                    var process = Process.GetCurrentProcess();
                    long baselineMemory = process.PrivateMemorySize64;
                    var lastCpuTime = process.TotalProcessorTime;
                    var lastCheckTime = DateTime.UtcNow;
                    int cpuViolationCount = 0;

                    // cancel immediately if memory grows more than 256MB over baseline or exceeds 1GB absolute
                    long memoryDeltaLimit = 256L * 1024L * 1024L;
                    long memoryAbsoluteLimit = 1024L * 1024L * 1024L;
                    double cpuLimit = 0.85;

                    while (true)
                    {
                        Thread.Sleep(33);
                        process.Refresh();

                        var currentTime = DateTime.UtcNow;
                        var currentCpuTime = process.TotalProcessorTime;

                        double elapsed = (currentTime - lastCheckTime).TotalMilliseconds;
                        double cpuUsage = elapsed > 0
                            ? (currentCpuTime - lastCpuTime).TotalMilliseconds / elapsed / Environment.ProcessorCount
                            : 0;

                        long currentMemory = process.PrivateMemorySize64;

                        bool shouldCancel = false;
                        string cancelReason = "";

                        // react on the first memory violation
                        if (currentMemory - baselineMemory > memoryDeltaLimit || currentMemory > memoryAbsoluteLimit)
                        {
                            shouldCancel = true;
                            cancelReason = "Memory";
                        }

                        // react on sustained cpu violation
                        if (!shouldCancel && cpuUsage > cpuLimit)
                        {
                            cpuViolationCount++;
                            if (cpuViolationCount >= 3)
                            {
                                shouldCancel = true;
                                cancelReason = "CPU";
                            }
                        }
                        else if (!shouldCancel)
                        {
                            cpuViolationCount = 0;
                        }

                        if (shouldCancel)
                        {
                            // bypass the muted stdout to send error directly to parent and terminate
                            Console.SetOut(originalOut);
                            Console.WriteLine($"AEC_ERROR|Ungewöhnlich hohe Systemauslastung ({cancelReason}) erkannt. Möglicherweise ineffizienter Code (z.B. Endlosschleife/Massenspeicher) oder unsicheres Level.");
                            Environment.Exit(1);
                        }

                        lastCpuTime = currentCpuTime;
                        lastCheckTime = currentTime;
                    }
                }
                catch { }
            });

            monitorThread.IsBackground = true;
            monitorThread.Start();

            try
            {
                // read base64 encoded dll from standard input
                string base64Dll = Console.ReadLine();
                if (string.IsNullOrEmpty(base64Dll)) return;

                byte[] dllBytes = Convert.FromBase64String(base64Dll);

                // buffer console output instead of muting it (to allow debugging through console write)
                using var debugWriter = new StringWriter();
                Console.SetOut(debugWriter);

                var assembly = Assembly.Load(dllBytes);

                // find our injected bootstrap class
                var bootstrapType = assembly.GetType("AecSandboxBootstrap");
                var method = bootstrapType.GetMethod("Execute", BindingFlags.Public | BindingFlags.Static);

                // run the validator (execute expects: out bool success, out string feedback)
                object[] methodArgs = new object[] { false, "" };
                method.Invoke(null, methodArgs);

                bool success = (bool)methodArgs[0];
                string feedback = (string)methodArgs[1];

                // get intercepted console output
                string consoleOutput = debugWriter.ToString();

                // restore console before sending the result back to main app
                Console.SetOut(originalOut);

                // sanitize newlines so the parent reads the whole payload in one ReadLine()
                string cleanConsole = consoleOutput?.Replace("\r", "")?.Replace("\n", "<br>") ?? "";
                string cleanFeedback = feedback?.Replace("\r", "")?.Replace("\n", "<br>") ?? "";

                // send via safe ipc stream
                Console.WriteLine($"AEC_RESULT|{success}|{cleanFeedback}|{cleanConsole}");
            }
            catch (Exception ex)
            {
                // restore console before sending the error!
                Console.SetOut(originalOut);
                var inner = ex.InnerException ?? ex;
                string cleanError = inner.Message.Replace("\r", "").Replace("\n", " | ");
                Console.WriteLine($"AEC_ERROR|{inner.GetType().Name}: {cleanError}");
            }
            finally
            {
                Environment.Exit(0);
            }
        }
    }

    public static async Task<TestResult> RunInSandboxAsync(byte[] compiledDll, CancellationToken token)
    {
        string processPath = Environment.ProcessPath;
        if (string.IsNullOrEmpty(processPath))
            return new TestResult
            {
                Success = false,
                Error = new Exception("Konnte den Sandbox-Prozess nicht ermitteln.")
            };

        var startInfo = new ProcessStartInfo
        {
            FileName = processPath,
            Arguments = "--sandbox-run",
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process
        {
            StartInfo = startInfo
        };

        try
        {
            process.Start();

            // send payload to the hidden child process and explicitly flush the stream
            await process.StandardInput.WriteLineAsync(Convert.ToBase64String(compiledDll));
            await process.StandardInput.FlushAsync();
            process.StandardInput.Close();

            // wait with a strict timeout to prevent infinite loops from hanging user pc
            var timeoutTask = Task.Delay(8000, token);
            var readTask = process.StandardOutput.ReadLineAsync();

            var completedTask = await Task.WhenAny(readTask, timeoutTask);

            if (completedTask == timeoutTask)
            {
                process.Kill();
                return new TestResult
                {
                    Success = false,
                    Error = new Exception("TIMEOUT: Das Custom Level hat sich aufgehängt (Endlosschleife?) und wurde sicher terminiert.")
                };
            }

            string resultLine = await readTask;

            if (string.IsNullOrEmpty(resultLine))
            {
                return new TestResult
                {
                    Success = false,
                    Error = new Exception("Der Sandbox-Prozess ist unerwartet abgestürzt (Speicherüberlauf/StackOverflow).")
                
                };
            }

            // parse ipc protocol
            if (resultLine.StartsWith("AEC_RESULT|"))
            {
                var parts = resultLine.Split(new[] { '|' }, 4);
                bool success = bool.Parse(parts[1]);
                string feedback = parts.Length > 2 ? parts[2].Replace("<br>", "\n") : "";
                string consoleOutput = parts.Length > 3 ? parts[3].Replace("<br>", "\n") : "";

                return new TestResult
                {
                    Success = success,
                    Feedback = feedback,
                    ConsoleOutput = consoleOutput
                };
            }
            else if (resultLine.StartsWith("AEC_ERROR|"))
            {
                var parts = resultLine.Split(new[] { '|' }, 2);
                return new TestResult
                {
                    Success = false,
                    Error = new Exception(parts[1])
                };
            }

            return new TestResult
            {
                Success = false,
                Error = new Exception("Unbekanntes Sandbox Protokoll Format.")
            };
        }
        catch (Exception ex)
        {
            return new TestResult
            {
                Success = false,
                Error = new Exception($"Sandbox Fehler: {ex.Message}")
            };
        }
        finally
        {
            if (!process.HasExited) process.Kill();
        }
    }
}