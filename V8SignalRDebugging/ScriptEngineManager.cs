namespace V8SignalRDebugging
{
    using BaristaJS.AppEngine.Debugger;
    using Microsoft.AspNet.SignalR;
    using Microsoft.AspNet.SignalR.Hubs;
    using Microsoft.ClearScript.V8;
    using Newtonsoft.Json.Linq;
    using System;
    using System.Collections.Concurrent;
    using System.Threading.Tasks;
    using V8SignalRDebugging.Debugger;

    public class ScriptEngineManager
    {
        // Singleton instance
        private readonly static Lazy<ScriptEngineManager> m_instance = new Lazy<ScriptEngineManager>(() =>
            new ScriptEngineManager(GlobalHost.ConnectionManager.GetHubContext<ScriptEngineHub>().Clients));

        public ScriptEngineManager(IHubConnectionContext hubConnectionContext)
        {
            // TODO: Complete member initialization
            Clients = hubConnectionContext;

             m_debuggerConnection = new DebuggerConnection("tcp://localhost:5858");
             m_debuggerConnection.Connect();

             m_debuggerClient = new DebuggerClient(m_debuggerConnection);
            
            m_debuggerClient.ExceptionEvent += debuggerClient_ExceptionEvent;
            m_debuggerClient.BreakpointEvent += m_debuggerClient_BreakpointEvent;
        }

        public static ScriptEngineManager Instance
        {
            get
            {
                return m_instance.Value;
            }
        }

        private IHubConnectionContext Clients
        {
            get;
            set;
        }

        private readonly V8ScriptEngine m_scriptEngine = new V8ScriptEngine(V8ScriptEngineFlags.EnableDebugging, 5858);
        private DebuggerConnection m_debuggerConnection;
        private DebuggerClient m_debuggerClient;
        private ConcurrentDictionary<string, string> m_connectionTokens = new ConcurrentDictionary<string, string>();

        public async Task<Response> Backtrace(string connectionId)
        {
            var backtrace = new Request("backtrace");
            var backtraceResponse = await m_debuggerClient.SendRequestAsync(backtrace);

            return backtraceResponse;
        }

        public async Task<int> SetBreakpoint(string connectionId, Breakpoint breakpoint)
        {
            var response = await SetBreakpointInternal(connectionId, breakpoint);
            return response.Body.breakpoint;
        }

        public async Task Continue(StepAction stepAction = StepAction.Next, int? stepCount = null)
        {
            var continueRequest = new Request("continue");

            //TODO: Set these two 
            continueRequest.Arguments.stepaction = stepAction.ToString().ToLowerInvariant();

            if (stepCount.HasValue && stepCount.Value > 1)
                continueRequest.Arguments.stepCount = stepCount.Value;

            var continueResponse = await m_debuggerClient.SendRequestAsync(continueRequest);
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

        public object Evaluate(string connectionId, string expression)
        {
            var scriptName = GetCurrentScriptTargetName(connectionId);
            var result = m_scriptEngine.Evaluate(scriptName, true, expression);
            ResetConnectionToken(connectionId);
            return result;
        }

        private string GetCurrentScriptTargetName(string connectionId)
        {
            return connectionId + "_" + GetConnectionToken(connectionId) + ".js";
        }

        private string GetConnectionToken(string connectionId)
        {
            return m_connectionTokens.GetOrAdd(connectionId, id => TokenGenerator.GetUniqueKey(10));
        }

        private string ResetConnectionToken(string connectionId)
        {
            return m_connectionTokens.AddOrUpdate(connectionId, id => TokenGenerator.GetUniqueKey(10), (id, currentToken) => TokenGenerator.GetUniqueKey(10));
        }

        private async Task<Response> SetBreakpointInternal(string connectionId, Breakpoint breakpoint)
        {
            var breakPointRequest = new Request("setbreakpoint");

            //TODO: Set these two 
            breakPointRequest.Arguments.type = "script";
            breakPointRequest.Arguments.target = GetCurrentScriptTargetName(connectionId) + " [temp]";

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

        void m_debuggerClient_BreakpointEvent(object sender, BreakpointEventArgs e)
        {
            Clients.All.breakpointHit(e.BreakpointEvent);
        }

        private void debuggerClient_ExceptionEvent(object sender, ExceptionEventArgs e)
        {
            Clients.All.exception(e.ExceptionEvent);
        }
    }
}
