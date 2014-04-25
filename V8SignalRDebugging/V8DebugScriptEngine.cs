namespace V8SignalRDebugging
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Threading.Tasks;
    using Microsoft.ClearScript.V8;
    using Newtonsoft.Json.Linq;
    using V8SignalRDebugging.Debugger;
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

        public async Task<Response> Backtrace()
        {
            var backtrace = new Request("backtrace");
            var backtraceResponse = await m_debuggerClient.SendRequestAsync(backtrace);

            return backtraceResponse;
        }

        public async Task<Response> ClearBreakpoint(int breakpointNumber)
        {
            var clearBreakpointRequest = new Request("clearbreakpoint");
            clearBreakpointRequest.Arguments.breakpoint = breakpointNumber;

            var clearBreakpointResponse = await m_debuggerClient.SendRequestAsync(clearBreakpointRequest);
            return clearBreakpointResponse;
        }

        public async Task<Response> Continue(StepAction stepAction = StepAction.Next, int? stepCount = null)
        {
            var continueRequest = new Request("continue");

            //TODO: Set these two 
            continueRequest.Arguments.stepaction = stepAction.ToString().ToLowerInvariant();

            if (stepCount.HasValue && stepCount.Value > 1)
                continueRequest.Arguments.stepCount = stepCount.Value;

            var continueResponse = await m_debuggerClient.SendRequestAsync(continueRequest);
            return continueResponse;
        }

        public async Task<Response> Disconnect()
        {
            var disconnectRequest = new Request("disconnect");
            var disconnectResponse = await m_debuggerClient.SendRequestAsync(disconnectRequest);

            return disconnectResponse;
        }

        public async Task<object> Evaluate(string code)
        {
            var result = m_scriptEngine.Evaluate(m_currentScriptName, true, code);

            await ResetScriptEngine();

            return result;
        }

        public async Task<Response> EvalImmediate(string expression)
        {
            var evaluateRequest = new Request("evaluate");
            evaluateRequest.Arguments.expression = expression;
            evaluateRequest.Arguments.frame = 0;
            //evaluateRequest.Arguments.global = true;
            evaluateRequest.Arguments.disable_break = false;
            evaluateRequest.Arguments.additional_context = new JArray();

            var evalResponse = await m_debuggerClient.SendRequestAsync(evaluateRequest);

            return evalResponse;
        }

        public async Task<IList<Breakpoint>> ListBreakpoints()
        {
            var listBreakpointsRequest = new Request("listbreakpoints");
            var listBreakpointsResponse = await m_debuggerClient.SendRequestAsync(listBreakpointsRequest);

            var breakpoints = new List<Breakpoint>();
            foreach (var breakpoint in listBreakpointsResponse.Body.breakpoints)
            {
                if (breakpoint.script_name != m_currentScriptName + " [Temp]")
                    continue;

                var concreteBreakpoint = new Breakpoint
                {
                    BreakPointNumber = breakpoint.number,
                    LineNumber = breakpoint.line,
                    Column = breakpoint.column,
                    GroupId = breakpoint.groupId,
                    HitCount = breakpoint.hit_count,
                    Enabled = breakpoint.active,
                    IgnoreCount = breakpoint.ignoreCount
                };
                breakpoints.Add(concreteBreakpoint);
            }

            return breakpoints;
        }

        public async Task Interrupt()
        {
            m_scriptEngine.Interrupt();

            await ResetScriptEngine();
        }

        public async Task<int> SetBreakpoint(Breakpoint breakpoint)
        {
            m_breakpoints.Add(breakpoint);

            var response = await SetBreakpointInternal(breakpoint);
            return response.Body.breakpoint;
        }

        private async Task<Response> SetBreakpointInternal(Breakpoint breakpoint)
        {
            var breakPointRequest = new Request("setbreakpoint");

            breakPointRequest.Arguments.type = "script";
            breakPointRequest.Arguments.target = m_currentScriptName + " [temp]";

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
            m_currentScriptName = GetRandomScriptTargetName();
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
