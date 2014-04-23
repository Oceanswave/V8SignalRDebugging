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

    public class ScriptEngineManager
    {
        // Singleton instance
        private readonly static Lazy<ScriptEngineManager> m_instance = new Lazy<ScriptEngineManager>(() =>
            new ScriptEngineManager(GlobalHost.ConnectionManager.GetHubContext<ScriptEngineHub>().Clients));

        public ScriptEngineManager(IHubConnectionContext hubConnectionContext)
        {
            // TODO: Complete member initialization
            Clients = hubConnectionContext;
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

        public async Task<object> Evaluate(string expression)
        {
            if (m_debuggerConnection == null)
            {
                m_debuggerConnection = new DebuggerConnection("tcp://localhost:5858");
                m_debuggerConnection.Connect();
            }

            if (m_debuggerClient == null)
            {
                m_debuggerClient = new DebuggerClient(m_debuggerConnection);
                var response = await m_debuggerClient.SendRequestAsync(new Request("version"));
                Console.WriteLine(response);

                m_debuggerClient.ExceptionEvent += debuggerClient_ExceptionEvent;
                m_debuggerClient.BreakpointEvent += m_debuggerClient_BreakpointEvent;
            }

            //var script = m_scriptEngine.Compile("asdf.js", expression);

            var breakPointRequest = new Request("setbreakpoint");
            breakPointRequest.Arguments.type = "script";
            breakPointRequest.Arguments.target = "asdf.js [temp]";
            breakPointRequest.Arguments.line = 1;
            var breakPointResponse = await m_debuggerClient.SendRequestAsync(breakPointRequest);
            Console.WriteLine(breakPointResponse);

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
            Clients.All.exception(e.BreakpointEvent);
        }

        private void debuggerClient_ExceptionEvent(object sender, ExceptionEventArgs e)
        {
            Clients.All.exception(e.ExceptionEvent);
        }
    }
}
