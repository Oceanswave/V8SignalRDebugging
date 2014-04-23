/* ****************************************************************************
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
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;

namespace Microsoft.VisualStudioTools.Project {
    /// <summary>
    /// Base class that can receive output from <see cref="ProcessOutput"/>.
    /// 
    /// If this class implements <see cref="IDisposable"/>, it will be disposed
    /// when the <see cref="ProcessOutput"/> object is disposed.
    /// </summary>
    abstract class Redirector {
        /// <summary>
        /// Called when a line is written to standard output.
        /// </summary>
        /// <param name="line">The line of text, not including the newline. This
        /// is never null.</param>
        public abstract void WriteLine(string line);
        /// <summary>
        /// Called when a line is written to standard error.
        /// </summary>
        /// <param name="line">The line of text, not including the newline. This
        /// is never null.</param>
        public abstract void WriteErrorLine(string line);

        /// <summary>
        /// Called when output is written that should be brought to the user's
        /// attention. The default implementation does nothing.
        /// </summary>
        public virtual void Show() {
        }

        /// <summary>
        /// Called when output is written that should be brought to the user's
        /// immediate attention. The default implementation does nothing.
        /// </summary>
        public virtual void ShowAndActivate() {
        }
    }

    sealed class TeeRedirector : Redirector, IDisposable {
        private readonly Redirector[] m_redirectors;

        public TeeRedirector(params Redirector[] redirectors) {
            m_redirectors = redirectors;
        }

        public void Dispose() {
            foreach (var redir in m_redirectors.OfType<IDisposable>()) {
                redir.Dispose();
            }
        }

        public override void WriteLine(string line) {
            foreach (var redir in m_redirectors) {
                redir.WriteLine(line);
            }
        }

        public override void WriteErrorLine(string line) {
            foreach (var redir in m_redirectors) {
                redir.WriteErrorLine(line);
            }
        }

        public override void Show() {
            foreach (var redir in m_redirectors) {
                redir.Show();
            }
        }

        public override void ShowAndActivate() {
            foreach (var redir in m_redirectors) {
                redir.ShowAndActivate();
            }
        }
    }

    /// <summary>
    /// Represents a process and its captured output.
    /// </summary>
    sealed class ProcessOutput : IDisposable {
        private readonly Process m_process;
        private readonly string m_arguments;
        private readonly List<string> m_output, m_error;
        private ProcessWaitHandle m_waitHandle;
        private readonly Redirector m_redirector;
        private bool m_isDisposed;

        private static readonly char[] EolChars = { '\r', '\n' };
        private static readonly char[] NeedToBeQuoted = { ' ', '"' };

        /// <summary>
        /// Runs the provided executable file and allows the program to display
        /// output to the user.
        /// </summary>
        /// <param name="filename">Executable file to run.</param>
        /// <param name="arguments">Arguments to pass.</param>
        /// <returns>A <see cref="ProcessOutput"/> object.</returns>
        public static ProcessOutput RunVisible(string filename, params string[] arguments) {
            return Run(filename, arguments, null, null, true, null);
        }

        /// <summary>
        /// Runs the provided executable file hidden and captures any output
        /// messages.
        /// </summary>
        /// <param name="filename">Executable file to run.</param>
        /// <param name="arguments">Arguments to pass.</param>
        /// <returns>A <see cref="ProcessOutput"/> object.</returns>
        public static ProcessOutput RunHiddenAndCapture(string filename, params string[] arguments) {
            return Run(filename, arguments, null, null, false, null);
        }

        /// <summary>
        /// Runs the file with the provided settings.
        /// </summary>
        /// <param name="filename">Executable file to run.</param>
        /// <param name="arguments">Arguments to pass.</param>
        /// <param name="workingDirectory">Starting directory.</param>
        /// <param name="env">Environment variables to set.</param>
        /// <param name="visible">
        /// False to hide the window and redirect output to
        /// <see cref="StandardOutputLines"/> and
        /// <see cref="StandardErrorLines"/>.
        /// </param>
        /// <param name="redirector">
        /// An object to receive redirected output.
        /// </param>
        /// <param name="quoteArgs">
        /// True to ensure each argument is correctly quoted.
        /// </param>
        /// <param name="elevate">
        /// True to run the process as an administrator. See
        /// <see cref="RunElevated"/>.
        /// </param>
        /// <returns>A <see cref="ProcessOutput"/> object.</returns>
        public static ProcessOutput Run(string filename,
                                        IEnumerable<string> arguments,
                                        string workingDirectory,
                                        IEnumerable<KeyValuePair<string, string>> env,
                                        bool visible,
                                        Redirector redirector,
                                        bool quoteArgs = true,
                                        bool elevate = false) {
            if (elevate) {
                return RunElevated(filename, arguments, workingDirectory, redirector, quoteArgs);
            }

            var psi = new ProcessStartInfo(filename);
            if (quoteArgs) {
                psi.Arguments = string.Join(" ",
                    arguments.Where(a => a != null).Select(QuoteSingleArgument));
            } else {
                psi.Arguments = string.Join(" ", arguments.Where(a => a != null));
            }
            psi.WorkingDirectory = workingDirectory;
            psi.CreateNoWindow = !visible;
            psi.UseShellExecute = false;
            psi.RedirectStandardError = !visible || (redirector != null);
            psi.RedirectStandardOutput = !visible || (redirector != null);
            if (env != null) {
                foreach (var kv in env) {
                    psi.EnvironmentVariables[kv.Key] = kv.Value;
                }
            }

            var process = new Process();
            process.StartInfo = psi;
            return new ProcessOutput(process, redirector);
        }

        /// <summary>
        /// Runs the file with the provided settings as a user with
        /// administrative permissions. The window is always hidden and output
        /// is provided to the redirector when the process terminates.
        /// </summary>
        /// <param name="filename">Executable file to run.</param>
        /// <param name="arguments">Arguments to pass.</param>
        /// <param name="workingDirectory">Starting directory.</param>
        /// <param name="redirector">
        /// An object to receive redirected output.
        /// </param>
        /// <param name="quoteArgs"></param>
        /// <returns>A <see cref="ProcessOutput"/> object.</returns>
        public static ProcessOutput RunElevated(string filename,
                                                IEnumerable<string> arguments,
                                                string workingDirectory,
                                                Redirector redirector,
                                                bool quoteArgs = true) {
            var outFile = Path.GetTempFileName();
            var errFile = Path.GetTempFileName();
            var psi = new ProcessStartInfo("cmd.exe");
            psi.CreateNoWindow = true;
            psi.WindowStyle = ProcessWindowStyle.Hidden;
            psi.UseShellExecute = true;
            psi.Verb = "runas";

            string args;
            if (quoteArgs) {
                args = string.Join(" ", arguments.Where(a => a != null).Select(QuoteSingleArgument));
            } else {
                args = string.Join(" ", arguments.Where(a => a != null));
            }
            psi.Arguments = string.Format("/S /C \"{0} {1} >>{2} 2>>{3}\"",
                QuoteSingleArgument(filename),
                args,
                QuoteSingleArgument(outFile),
                QuoteSingleArgument(errFile)
            );
            psi.WorkingDirectory = workingDirectory;
            psi.CreateNoWindow = true;
            psi.UseShellExecute = true;

            var process = new Process();
            process.StartInfo = psi;
            var result = new ProcessOutput(process, redirector);
            if (redirector != null) {
                result.Exited += (s, e) => {
                    try {
                        try {
                            var lines = File.ReadAllLines(outFile);
                            foreach (var line in lines) {
                                redirector.WriteLine(line);
                            }
                        } catch (Exception) {
                            redirector.WriteErrorLine("Failed to obtain standard output from elevated process.");
                        }
                        try {
                            var lines = File.ReadAllLines(errFile);
                            foreach (var line in lines) {
                                redirector.WriteErrorLine(line);
                            }
                        } catch (Exception) {
                            redirector.WriteErrorLine("Failed to obtain standard error from elevated process.");
                        }
                    } finally {
                        try {
                            File.Delete(outFile);
                        } catch { }
                        try {
                            File.Delete(errFile);
                        } catch { }
                    }
                };
            }
            return result;
        }

        internal static IEnumerable<string> SplitLines(string source) {
            int start = 0;
            int end = source.IndexOfAny(EolChars);
            while (end >= start) {
                yield return source.Substring(start, end - start);
                start = end + 1;
                if (source[start - 1] == '\r' && start < source.Length && source[start] == '\n') {
                    start += 1;
                }

                if (start < source.Length) {
                    end = source.IndexOfAny(EolChars, start);
                } else {
                    end = -1;
                }
            }
            if (start <= 0) {
                yield return source;
            } else if (start < source.Length) {
                yield return source.Substring(start);
            }
        }

        internal static string QuoteSingleArgument(string arg) {
            if (string.IsNullOrEmpty(arg)) {
                return "\"\"";
            }
            if (arg.IndexOfAny(NeedToBeQuoted) < 0) {
                return arg;
            }

            if (arg.StartsWith("\"") && arg.EndsWith("\"")) {
                bool inQuote = false;
                int consecutiveBackslashes = 0;
                foreach (var c in arg) {
                    if (c == '"') {
                        if (consecutiveBackslashes % 2 == 0) {
                            inQuote = !inQuote;
                        }
                    }

                    if (c == '\\') {
                        consecutiveBackslashes += 1;
                    } else {
                        consecutiveBackslashes = 0;
                    }
                }
                if (!inQuote) {
                    return arg;
                }
            }

            var newArg = arg.Replace("\"", "\\\"");
            if (newArg.EndsWith("\\")) {
                newArg += "\\";
            }
            return "\"" + newArg + "\"";
        }

        private ProcessOutput(Process process, Redirector redirector) {
            m_arguments = QuoteSingleArgument(process.StartInfo.FileName) + " " + process.StartInfo.Arguments;
            m_redirector = redirector;
            if (m_redirector == null) {
                m_output = new List<string>();
                m_error = new List<string>();
            }

            m_process = process;
            if (m_process.StartInfo.RedirectStandardOutput) {
                m_process.OutputDataReceived += OnOutputDataReceived;
            }
            if (m_process.StartInfo.RedirectStandardError) {
                m_process.ErrorDataReceived += OnErrorDataReceived;
            }

            m_process.Exited += OnExited;
            m_process.EnableRaisingEvents = true;

            try {
                m_process.Start();
            } catch (Exception ex) {
                m_error.AddRange(SplitLines(ex.ToString()));
                m_process = null;
            }

            if (m_process != null) {
                if (m_process.StartInfo.RedirectStandardOutput) {
                    m_process.BeginOutputReadLine();
                }
                if (m_process.StartInfo.RedirectStandardError) {
                    m_process.BeginErrorReadLine();
                }
            }
        }

        private void OnOutputDataReceived(object sender, DataReceivedEventArgs e) {
            if (m_isDisposed) {
                return;
            }

            if (!string.IsNullOrEmpty(e.Data)) {
                foreach (var line in SplitLines(e.Data)) {
                    if (m_output != null) {
                        m_output.Add(line);
                    }
                    if (m_redirector != null) {
                        m_redirector.WriteLine(line);
                    }
                }
            }
        }

        private void OnErrorDataReceived(object sender, DataReceivedEventArgs e) {
            if (m_isDisposed) {
                return;
            }

            if (!string.IsNullOrEmpty(e.Data)) {
                foreach (var line in SplitLines(e.Data)) {
                    if (m_error != null) {
                        m_error.Add(line);
                    }
                    if (m_redirector != null) {
                        m_redirector.WriteLine(line);
                    }
                }
            }
        }

        /// <summary>
        /// The arguments that were originally passed, including the filename.
        /// </summary>
        public string Arguments {
            get {
                return m_arguments;
            }
        }

        /// <summary>
        /// The exit code or null if the process never started or has not
        /// exited.
        /// </summary>
        public int? ExitCode {
            get {
                if (m_process == null || !m_process.HasExited) {
                    return null;
                }
                return m_process.ExitCode;
            }
        }

        /// <summary>
        /// Gets or sets the priority class of the process.
        /// </summary>
        public ProcessPriorityClass PriorityClass {
            get {
                if (m_process != null && !m_process.HasExited) {
                    try {
                        return m_process.PriorityClass;
                    } catch (Win32Exception) {
                    } catch (InvalidOperationException) {
                        // Return Normal if we've raced with the process
                        // exiting.
                    }
                }
                return ProcessPriorityClass.Normal;
            }
            set {
                if (m_process != null && !m_process.HasExited) {
                    try {
                        m_process.PriorityClass = value;
                    } catch (Win32Exception) {
                    } catch (InvalidOperationException) {
                        // Silently fail if we've raced with the process
                        // exiting.
                    }
                }
            }
        }

        /// <summary>
        /// The redirector that was originally passed.
        /// </summary>
        public Redirector Redirector {
            get { return m_redirector; }
        }

        private void FlushAndCloseOutput() {
            if (m_process == null) {
                return;
            }

            if (m_process.StartInfo.RedirectStandardOutput) {
                m_process.CancelOutputRead();
            }
            if (m_process.StartInfo.RedirectStandardError) {
                m_process.CancelErrorRead();
            }
        }

        /// <summary>
        /// The lines of text sent to standard output. These do not include
        /// newline characters.
        /// </summary>
        public IEnumerable<string> StandardOutputLines {
            get {
                return m_output;
            }
        }

        /// <summary>
        /// The lines of text sent to standard error. These do not include
        /// newline characters.
        /// </summary>
        public IEnumerable<string> StandardErrorLines {
            get {
                return m_error;
            }
        }

        /// <summary>
        /// A handle that can be waited on. It triggers when the process exits.
        /// </summary>
        public WaitHandle WaitHandle {
            get {
                if (m_waitHandle == null && m_process != null) {
                    m_waitHandle = new ProcessWaitHandle(m_process);
                }
                return m_waitHandle;
            }
        }

        /// <summary>
        /// Waits until the process exits.
        /// </summary>
        public void Wait() {
            if (m_process != null) {
                m_process.WaitForExit();
                FlushAndCloseOutput();
            }
        }

        /// <summary>
        /// Waits until the process exits or the timeout expires.
        /// </summary>
        /// <param name="timeout">The maximum time to wait.</param>
        /// <returns>
        /// True if the process exited before the timeout expired.
        /// </returns>
        public bool Wait(TimeSpan timeout) {
            if (m_process != null) {
                bool exited = m_process.WaitForExit((int)timeout.TotalMilliseconds);
                if (exited) {
                    FlushAndCloseOutput();
                }
                return exited;
            }
            return true;
        }

        /// <summary>
        /// Enables using 'await' on this object.
        /// </summary>
        public TaskAwaiter<int> GetAwaiter() {
            var tcs = new TaskCompletionSource<int>();
            
            if (m_process == null) {
                tcs.SetCanceled();
            } else {
                m_process.Exited += (s, e) => {
                    FlushAndCloseOutput();
                    tcs.TrySetResult(m_process.ExitCode);
                };
                if (m_process.HasExited) {
                    FlushAndCloseOutput();
                    tcs.TrySetResult(m_process.ExitCode);
                }
            }

            return tcs.Task.GetAwaiter();
        }

        /// <summary>
        /// Immediately stops the process.
        /// </summary>
        public void Kill() {
            if (m_process != null) {
                m_process.Kill();
                FlushAndCloseOutput();
            }
        }

        /// <summary>
        /// Raised when the process exits.
        /// </summary>
        public event EventHandler Exited;

        private void OnExited(object sender, EventArgs e) {
            var evt = Exited;
            if (evt != null) {
                evt(this, e);
            }
        }

        class ProcessWaitHandle : WaitHandle {
            public ProcessWaitHandle(Process process) {
                Debug.Assert(process != null);
                SafeWaitHandle = new SafeWaitHandle(process.Handle, false); // Process owns the handle
            }
        }

        /// <summary>
        /// Called to dispose of unmanaged resources.
        /// </summary>
        public void Dispose() {
            if (!m_isDisposed) {
                m_isDisposed = true;
                if (m_process != null) {
                    if (m_process.StartInfo.RedirectStandardOutput) {
                        m_process.OutputDataReceived -= OnOutputDataReceived;
                    }
                    if (m_process.StartInfo.RedirectStandardError) {
                        m_process.ErrorDataReceived -= OnErrorDataReceived;
                    }
                    m_process.Dispose();
                }
                var disp = m_redirector as IDisposable;
                if (disp != null) {
                    disp.Dispose();
                }
                if (m_waitHandle != null) {
                    m_waitHandle.Dispose();
                }
            }
        }
    }
}
