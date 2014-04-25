namespace V8SignalRDebugging
{
    using Microsoft.AspNet.SignalR;
    using Microsoft.AspNet.SignalR.Hubs;
    using Microsoft.ClearScript;
    using Microsoft.ClearScript.V8;
    using Newtonsoft.Json.Linq;
    using System;
    using System.Collections.Concurrent;
    using System.Threading.Tasks;
    using V8SignalRDebugging.Debugger;
    using V8SignalRDebugging.Debugger.Events;
    using V8SignalRDebugging.Debugger.Messages;

    public class ScriptEngineManager
    {
        // Singleton instance
        private readonly static Lazy<ScriptEngineManager> ManagerInstance = new Lazy<ScriptEngineManager>(() =>
            new ScriptEngineManager(GlobalHost.ConnectionManager.GetHubContext<ScriptEngineHub>().Clients));

        private readonly ConcurrentDictionary<string, V8DebugScriptEngine> m_connectionScriptEngines =
            new ConcurrentDictionary<string, V8DebugScriptEngine>();

        public ScriptEngineManager(IHubConnectionContext hubConnectionContext)
        {
            // TODO: Complete member initialization
            Clients = hubConnectionContext;
            
        }

        public static ScriptEngineManager Instance
        {
            get
            {
                return ManagerInstance.Value;
            }
        }

        private IHubConnectionContext Clients
        {
            get;
            set;
        }


        public async Task<Response> Backtrace(string connectionId)
        {
            var scriptEngine = GetScriptEngineForConnection(connectionId);
            var response = await scriptEngine.Backtrace();
            return response;
        }

        public async Task<Response> Continue(string connectionId, StepAction stepAction = StepAction.Next, int? stepCount = null)
        {
            var scriptEngine = GetScriptEngineForConnection(connectionId);
            var response = await scriptEngine.Continue(stepAction, stepCount);
            return response;
        }

        public async Task<Response> Disconnect(string connectionId)
        {
            var scriptEngine = GetScriptEngineForConnection(connectionId);
            var response = await scriptEngine.Disconnect();
            return response;
        }

        public async Task<object> Evaluate(string connectionId, string code)
        {
            var scriptEngine = GetScriptEngineForConnection(connectionId);
            var response = await scriptEngine.Evaluate(code);
            return response;
        }

        public async Task<Response> EvalImmediate(string connectionId, string expression)
        {
            var scriptEngine = GetScriptEngineForConnection(connectionId);
            var response = await scriptEngine.EvalImmediate(expression);
            return response;
        }


        public void InitiateScriptEngine(string connectionId)
        {
            var scriptEngine = GetScriptEngineForConnection(connectionId);
            scriptEngine.BreakpointEvent += debuggerClient_BreakpointEvent;
            scriptEngine.ExceptionEvent += debuggerClient_ExceptionEvent;
        }


        public void RemoveScriptEngine(string connectionId)
        {
            V8DebugScriptEngine scriptEngine;
            if (!m_connectionScriptEngines.TryRemove(connectionId, out scriptEngine))
                return;

            scriptEngine.BreakpointEvent -= debuggerClient_BreakpointEvent;
            scriptEngine.ExceptionEvent -= debuggerClient_ExceptionEvent;
            scriptEngine.Dispose();
        }

        public async Task<int> SetBreakpoint(string connectionId, Breakpoint breakpoint)
        {
            var scriptEngine = GetScriptEngineForConnection(connectionId);
            var breakpointNumber = await scriptEngine.SetBreakpoint(breakpoint);
            return breakpointNumber;
        }

        private V8DebugScriptEngine GetScriptEngineForConnection(string connectionId)
        {
            var console = new FirebugConsole(Clients);

            return m_connectionScriptEngines.GetOrAdd(connectionId, id => new V8DebugScriptEngine(connectionId, console));
        }

        private void debuggerClient_BreakpointEvent(object sender, BreakpointEventArgs e)
        {
            //TODO: Change this to notify only the connection associatd with the script engine. (or that group?!)
            Clients.All.breakpointHit(e.BreakpointEvent);
        }

        private void debuggerClient_ExceptionEvent(object sender, ExceptionEventArgs e)
        {
            //TODO Change this to notify only the connection associatd with the script engine.
            Clients.All.exception(e.ExceptionEvent);
        }
    }
}
