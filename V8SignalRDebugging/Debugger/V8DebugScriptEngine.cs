namespace V8SignalRDebugging.Debugger
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Threading.Tasks;
    using Microsoft.ClearScript.V8;
    using Newtonsoft.Json.Linq;
    using V8SignalRDebugging.Debugger.Events;
    using V8SignalRDebugging.Debugger.Messages;

    public class V8DebugScriptEngine : IDisposable
    {
        public event EventHandler<ExceptionEventArgs> ExceptionEvent;
        public event EventHandler<BreakpointEventArgs> BreakpointEvent;

        private V8ScriptEngine m_scriptEngine;
        private DebuggerConnection m_debuggerConnection;
        private DebuggerClient m_debuggerClient;

        private readonly string m_name;
        private string m_currentScriptName;

        private readonly List<Breakpoint> m_breakpoints = new List<Breakpoint>(); 

        public V8DebugScriptEngine(string name, FirebugConsole console)
        {
            if (String.IsNullOrWhiteSpace(name))
                throw new ArgumentNullException("name");

            if (console == null)
                throw new ArgumentNullException("console");

            m_name = name;

            //Create the Script Engine/
            var debuggingPort = PortUtilities.FindFreePort(IPAddress.Loopback);
            m_scriptEngine = new V8ScriptEngine(name, V8ScriptEngineFlags.DisableGlobalMembers | V8ScriptEngineFlags.EnableDebugging, debuggingPort)
            {
                AllowReflection = false,
            };

            m_scriptEngine.AddHostObject("console", console);

            //Create the connection to the debug port.
            m_debuggerConnection = new DebuggerConnection("tcp://127.0.0.1:" + debuggingPort);
            m_debuggerConnection.Connect();

            m_debuggerClient = new DebuggerClient(m_debuggerConnection);

            m_debuggerClient.ExceptionEvent += debuggerClient_ExceptionEvent;
            m_debuggerClient.BreakpointEvent += m_debuggerClient_BreakpointEvent;

            m_currentScriptName = GetRandomScriptTargetName();
        }

        /// <summary>
        /// Gets the name of the current script (valid between calls to Eval())
        /// </summary>
        public string CurrentScriptName
        {
            get
            {
                return m_currentScriptName + " [temp]";
            }
        }

        /// <summary>
        /// The request backtrace returns a backtrace (or stacktrace) from the current execution state.
        /// </summary>
        /// <remarks>
        /// When issuing a request a range of frames can be supplied. The top frame is frame number 0. If no frame range is supplied data for 10 frames will be returned.
        /// </remarks>
        /// <param name="fromFrame"></param>
        /// <param name="toFrame"></param>
        /// <param name="bottom"></param>
        /// <returns></returns>
        public async Task<Response> Backtrace(int? fromFrame = null, int? toFrame = null, bool? bottom = null)
        {
            var backtrace = new Request("backtrace");

            if (fromFrame.HasValue && toFrame.HasValue)
            {
                backtrace.Arguments.fromFrame = fromFrame.Value;
                backtrace.Arguments.toFrame = toFrame.Value;
            }

            if (bottom.HasValue)
            {
                backtrace.Arguments.bottom = bottom.Value;
            }

            var backtraceResponse = await m_debuggerClient.SendRequestAsync(backtrace);
            return backtraceResponse;
        }

        /// <summary>
        /// The request changebreakpoint changes the status of a break point.
        /// </summary>
        /// <param name="breakpointNumber">number of the break point to clear</param>
        /// <param name="enabled">initial enabled state. True or false, default is true</param>
        /// <param name="condition">string with break point condition</param>
        /// <param name="ignoreCount">number specifying the number of break point hits</param>
        /// <returns></returns>
        public async Task<Response> ChangeBreakpoint(int breakpointNumber, bool enabled = true, string condition = null, int ignoreCount = 0)
        {
            var changeBreakpointRequest = new Request("changebreakpoint");
            changeBreakpointRequest.Arguments.breakpoint = breakpointNumber;
            if (enabled != true)
                changeBreakpointRequest.Arguments.enabled = false;

            if (String.IsNullOrWhiteSpace(condition) == false)
                changeBreakpointRequest.Arguments.condition = condition;

            if (ignoreCount > 0)
                changeBreakpointRequest.Arguments.ignoreCount = ignoreCount;

            var changeBreakpointResponse = await m_debuggerClient.SendRequestAsync(changeBreakpointRequest);
            return changeBreakpointResponse;
        }

        /// <summary>
        /// The request clearbreakpoint clears a break point.
        /// </summary>
        /// <param name="breakpointNumber">number of the break point to clear</param>
        /// <returns></returns>
        public async Task<Response> ClearBreakpoint(int breakpointNumber)
        {
            var clearBreakpointRequest = new Request("clearbreakpoint");
            clearBreakpointRequest.Arguments.breakpoint = breakpointNumber;

            var clearBreakpointResponse = await m_debuggerClient.SendRequestAsync(clearBreakpointRequest);
            return clearBreakpointResponse;
        }

        /// <summary>
        /// The request continue is a request from the debugger to start the VM running again. As part of the continue request the debugger can specify if it wants the VM to perform a single step action.
        /// </summary>
        /// <param name="stepAction"></param>
        /// <param name="stepCount"></param>
        /// <returns></returns>
        public async Task<Response> Continue(StepAction stepAction = StepAction.Next, int? stepCount = null)
        {
            var continueRequest = new Request("continue");

            continueRequest.Arguments.stepaction = stepAction.ToString().ToLowerInvariant();

            if (stepCount.HasValue && stepCount.Value > 1)
                continueRequest.Arguments.stepCount = stepCount.Value;

            var continueResponse = await m_debuggerClient.SendRequestAsync(continueRequest);
            return continueResponse;
        }

        /// <summary>
        /// The request disconnect is used to detach the remote debugger from the debuggee.
        /// </summary>
        /// <remarks>
        /// This will trigger the debuggee to disable all active breakpoints and resumes execution if the debuggee was previously stopped at a break.
        /// </remarks>
        /// <returns></returns>
        public async Task<Response> Disconnect()
        {
            var disconnectRequest = new Request("disconnect");

            var disconnectResponse = await m_debuggerClient.SendRequestAsync(disconnectRequest);
            return disconnectResponse;
        }

        /// <summary>
        /// Evaluates the specified code.
        /// </summary>
        /// <remarks>
        /// Subsequent calls to eval will use a different script name.
        /// </remarks>
        /// <param name="code"></param>
        /// <returns></returns>
        public async Task<object> Evaluate(string code)
        {
            var result = m_scriptEngine.Evaluate(m_currentScriptName, true, code);

            await ResetScriptEngine();
            return result;
        }

        /// <summary>
        /// The request evaluate is used to evaluate an expression. The body of the result is as described in response object serialization below.
        /// </summary>
        /// <param name="expression"></param>
        /// <param name="disableBreak"></param>
        /// <returns></returns>
        public async Task<Response> EvalImmediate(string expression, bool disableBreak = false)
        {
            var evaluateRequest = new Request("evaluate");
            evaluateRequest.Arguments.expression = expression;
            evaluateRequest.Arguments.frame = 0;
            //evaluateRequest.Arguments.global = true;
            evaluateRequest.Arguments.disable_break = disableBreak;
            evaluateRequest.Arguments.additional_context = new JArray();

            var evalResponse = await m_debuggerClient.SendRequestAsync(evaluateRequest);
            return evalResponse;
        }

        /// <summary>
        /// The request frame selects a new selected frame and returns information for that. If no frame number is specified the selected frame is returned.
        /// </summary>
        /// <param name="frameNumber"></param>
        /// <returns></returns>
        public async Task<Response> Frame(int? frameNumber)
        {
            var frameRequest = new Request("frame");

            if (frameNumber.HasValue)
                frameRequest.Arguments.number = frameNumber.Value;

            var frameResponse = await m_debuggerClient.SendRequestAsync(frameRequest);
            return frameResponse;
        }

        /// <summary>
        /// The request gc is a request to run the garbage collector in the debuggee.
        /// </summary>
        /// <returns></returns>
        public async Task<Response> GarbageCollect()
        {
            var gcRequest = new Request("gc");

            var gcResponse = await m_debuggerClient.SendRequestAsync(gcRequest);
            return gcResponse;
        }

        /// <summary>
        /// The request listbreakpoints is used to get information on breakpoints that may have been set by the debugger.
        /// </summary>
        /// <returns></returns>
        public async Task<Response> ListAllBreakpoints()
        {
            var listBreakpointsRequest = new Request("listbreakpoints");

            var listBreakpointsResponse = await m_debuggerClient.SendRequestAsync(listBreakpointsRequest);
            return listBreakpointsResponse;
        }

        /// <summary>
        /// Stops the script engine at the current point.
        /// </summary>
        /// <returns></returns>
        public async Task Interrupt()
        {
            m_scriptEngine.Interrupt();

            await ResetScriptEngine();
        }

        /// <summary>
        /// The request lookup is used to lookup objects based on their handle.
        /// </summary>
        /// <remarks>
        /// The individual array elements of the body of the result is as described in response object serialization below.
        /// </remarks>
        /// <param name="includeSource"></param>
        /// <param name="handles"></param>
        /// <returns></returns>
        public async Task<Response> Lookup(bool includeSource = false, params int[] handles)
        {
            var lookupRequest = new Request("lookup");
            lookupRequest.Arguments.handles = handles;
            lookupRequest.Arguments.includeSource = includeSource;

            var response = await m_debuggerClient.SendRequestAsync(lookupRequest);
            return response;
        }

        /// <summary>
        /// The request scope returns information on a given scope for a given frame. If no frame number is specified the selected frame is used.
        /// </summary>
        /// <param name="number"></param>
        /// <param name="frameNumber"></param>
        /// <returns></returns>
        public async Task<Response> Scope(int number, int? frameNumber = null)
        {
            var scopeRequest = new Request("scope");
            scopeRequest.Arguments.number = number;

            if (frameNumber.HasValue)
                scopeRequest.Arguments.frameNumber = frameNumber;

            var response = await m_debuggerClient.SendRequestAsync(scopeRequest);
            return response;
        }

        /// <summary>
        /// The request scopes returns all the scopes for a given frame. If no frame number is specified the selected frame is returned.
        /// </summary>
        /// <param name="frameNumber"></param>
        /// <returns></returns>
        public async Task<Response> Scopes(int? frameNumber = null)
        {
            var scopesRequest = new Request("scopes");

            if (frameNumber.HasValue)
                scopesRequest.Arguments.frameNumber = frameNumber.Value;

            var response = await m_debuggerClient.SendRequestAsync(scopesRequest);
            return response;
        }

        /// <summary>
        /// The request scripts retrieves active scripts from the VM.
        /// </summary>
        /// <remarks>
        /// An active script is source code from which there is still live objects in the VM. This request will always force a full garbage collection in the VM.
        /// </remarks>
        /// <param name="types">types of scripts to retrieve</param>
        /// <param name="ids">array of id's of scripts to return. If this is not specified all scripts are requrned</param>
        /// <param name="includeSource">boolean indicating whether the source code should be included for the scripts returned</param>
        /// <param name="filter">filter string or script id.</param>
        /// <returns></returns>
        public async Task<Response> Scripts(ScriptType? types = null, int[] ids = null, bool includeSource = false,
            string filter = null)
        {
            var scriptsRequest = new Request("scripts");

            if (types.HasValue)
                scriptsRequest.Arguments.types = (byte)types.Value;

            if (ids != null && ids.Length > 0)
                scriptsRequest.Arguments.ids = ids;

            if (includeSource)
                scriptsRequest.Arguments.includeSource = true;

            if (String.IsNullOrWhiteSpace(filter) == false)
            {
                int scriptNumber;
                if (int.TryParse(filter, out scriptNumber))
                    scriptsRequest.Arguments.filter = scriptNumber;
                else
                    scriptsRequest.Arguments.filter = filter;
            }
            

            var response = await m_debuggerClient.SendRequestAsync(scriptsRequest);
            return response;
        }

        /// <summary>
        /// The request source retrieves source code for a frame.
        /// </summary>
        /// <remarks>
        /// It returns a number of source lines running from the fromLine to but not including the toLine,
        /// that is the interval is open on the "to" end. For example,
        /// requesting source from line 2 to 4 returns two lines (2 and 3).
        /// Also note that the line numbers are 0 based: the first line is line 0.
        /// </remarks>
        /// <param name="frameNumber">frame number (default selected frame)</param>
        /// <param name="fromLine">from line within the source default is line 0</param>
        /// <param name="toLine">to line within the source this line is not included in the result default is the number of lines in the script</param>
        /// <returns></returns>
        public async Task<Response> Source(int? frameNumber = null, int? fromLine = null, int? toLine = null)
        {
            var sourceRequest = new Request("source");

            if (frameNumber.HasValue)
                sourceRequest.Arguments.frame = frameNumber.Value;

            if (fromLine.HasValue)
                sourceRequest.Arguments.fromLine = fromLine.Value;

            if (toLine.HasValue)
                sourceRequest.Arguments.toLine = toLine.Value;

            var response = await m_debuggerClient.SendRequestAsync(sourceRequest);
            return response;
        }

        /// <summary>
        /// The request setbreakpoint creates a new break point.
        /// </summary>
        /// <remarks>
        /// This request can be used to set both function and script break points. A function break point sets a break point in an existing function whereas a script break point sets a break point in a named script. A script break point can be set even if the named script is not found.
        /// </remarks>
        /// <param name="breakpoint"></param>
        /// <returns></returns>
        public async Task<dynamic> SetBreakpoint(Breakpoint breakpoint)
        {
            m_breakpoints.Add(breakpoint);

            var response = await SetBreakpointInternal(breakpoint);
            return response;
        }

        private async Task<Response> SetBreakpointInternal(Breakpoint breakpoint)
        {
            var breakPointRequest = new Request("setbreakpoint");

            breakPointRequest.Arguments.type = "script";
            breakPointRequest.Arguments.target = CurrentScriptName;

            breakPointRequest.Arguments.line = breakpoint.LineNumber;

            if (breakpoint.Column.HasValue && breakpoint.Column > 0)
                breakPointRequest.Arguments.column = breakpoint.Column.Value;

            if (breakpoint.Enabled == false)
                breakPointRequest.Arguments.enabled = false;

            if (String.IsNullOrWhiteSpace(breakpoint.Condition) == false)
                breakPointRequest.Arguments.condition = breakpoint.Condition;

            if (breakpoint.IgnoreCount.HasValue && breakpoint.IgnoreCount > 0)
                breakPointRequest.Arguments.ignoreCount = breakpoint.IgnoreCount.Value;

            var breakPointResponse = await m_debuggerClient.SendRequestAsync(breakPointRequest);
            return breakPointResponse;
        }

        private async Task ResetScriptEngine()
        {
            //clear existing breakpoints.
            var currentBreakpoints = await ListAllBreakpoints();

            foreach (var breakpoint in currentBreakpoints.GetBreakpoints().Where(bp => bp.ScriptName == CurrentScriptName))
            {
                await ClearBreakpoint(breakpoint.BreakPointNumber);
            }

            await GarbageCollect();

            m_currentScriptName = GetRandomScriptTargetName();

            //Set breakpoints on new "instance"
            foreach (var breakpoint in m_breakpoints)
            {
                await SetBreakpointInternal(breakpoint);
            }
        }

        private void m_debuggerClient_BreakpointEvent(object sender, BreakpointEventArgs e)
        {
            if (ExceptionEvent != null)
                BreakpointEvent(sender, new BreakpointEventArgs(e.BreakpointEvent));
        }

        private void debuggerClient_ExceptionEvent(object sender, ExceptionEventArgs e)
        {
            if (ExceptionEvent != null)
                ExceptionEvent(sender, new ExceptionEventArgs(e.ExceptionEvent));
        }

        private string GetRandomScriptTargetName()
        {
            return m_name + "_" + TokenGenerator.GetUniqueKey(10) + ".js";
        }

        public void Dispose()
        {
            if (m_debuggerClient != null)
            {
                m_debuggerClient.ExceptionEvent -= debuggerClient_ExceptionEvent;
                m_debuggerClient.BreakpointEvent -= m_debuggerClient_BreakpointEvent;
                m_debuggerClient = null;
            }

            if (m_debuggerConnection != null)
            {
                m_debuggerConnection.Close();
                m_debuggerConnection.Dispose();
                m_debuggerConnection = null;
            }

            if (m_scriptEngine != null)
            {
                m_scriptEngine.Interrupt();
                m_scriptEngine.Dispose();
                m_scriptEngine = null;
            }
        }
    }
}
