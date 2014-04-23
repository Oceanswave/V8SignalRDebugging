namespace V8SignalRDebugging
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.AspNet.SignalR;
    using Microsoft.AspNet.SignalR.Hubs;
    using Microsoft.ClearScript.V8;
    using BaristaJS.AppEngine.Debugger;
    using Newtonsoft.Json.Linq;
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

        public async Task<int> SetBreakpoint(int lineNumber, int? column = null, bool enabled = true, string condition = null, int? ignoreCount = null)
        {
            if (lineNumber < 1)
                throw new ArgumentOutOfRangeException("lineNumber");

            var breakPointRequest = new Request("setbreakpoint");

            //TODO: Set these two 
            breakPointRequest.Arguments.type = "script";
            breakPointRequest.Arguments.target = "asdf.js [temp]";

            breakPointRequest.Arguments.line = lineNumber;

            if (column.HasValue && column > 0)
                breakPointRequest.Arguments.column = column.Value;

            if (enabled == false)
                breakPointRequest.Arguments.enabled = false;

            if (String.IsNullOrWhiteSpace(condition) == false)
                breakPointRequest.Arguments.condition = condition;

            if (ignoreCount.HasValue && ignoreCount > 0)
                breakPointRequest.Arguments.ignoreCount = ignoreCount.Value;

            var breakPointResponse = await m_debuggerClient.SendRequestAsync(breakPointRequest);

            return breakPointResponse.Success
                ? breakPointResponse.Body.breakpoint
                : 0;
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

        public async Task<object> Evaluate(string expression)
        {
            return m_scriptEngine.Evaluate("asdf.js", expression);

            //return m_scriptEngine.Evaluate(script);

/*
            var evaluateRequest = new Request("evaluate");
            evaluateRequest.Arguments.expression = expression;
            //evaluateRequest.Arguments.frame = 1;
            evaluateRequest.Arguments.global = true;
            evaluateRequest.Arguments.disable_break = false;
            evaluateRequest.Arguments.additional_context = new JArray();*/

            //var evalResponse = await m_debuggerClient.SendRequestAsync(evaluateRequest);

            //Console.WriteLine(evalResponse);
            //return evalResponse;
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
