namespace V8SignalRDebugging
{
    using Microsoft.AspNet.SignalR;
    using Microsoft.AspNet.SignalR.Hubs;
    using System;
    using System.Collections.Concurrent;
    using System.Threading.Tasks;
    using V8SignalRDebugging.Debugger;
    using V8SignalRDebugging.Debugger.Events;
    using V8SignalRDebugging.Debugger.Messages;

    /// <summary>
    /// Manages Script Engine Instances -- currently, each connection gets a script engine instance which is disposed of on disconnect.
    /// </summary>
    public class ScriptEngineManager
    {
        // Singleton instance
        private readonly static Lazy<ScriptEngineManager> ManagerInstance = new Lazy<ScriptEngineManager>(() =>
            new ScriptEngineManager(GlobalHost.ConnectionManager.GetHubContext<ScriptEngineHub>().Clients));

        private readonly ConcurrentDictionary<string, V8DebugScriptEngine> m_connectionScriptEngines =
            new ConcurrentDictionary<string, V8DebugScriptEngine>();

        public ScriptEngineManager(IHubConnectionContext hubConnectionContext)
        {
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

        public async Task<Response> ClearBreakpoint(string connectionId, int breakpointNumber)
        {
            var scriptEngine = GetScriptEngineForConnection(connectionId);
            var response = await scriptEngine.ClearBreakpoint(breakpointNumber);
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

            //For some reason, disconnect doesn't do what you think it does...
            var breakpoints = await scriptEngine.ListBreakpoints();
            foreach (var breakpoint in breakpoints)
            {
                await scriptEngine.ClearBreakpoint(breakpoint.BreakPointNumber);
            }
            await scriptEngine.Continue(StepAction.Out);

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

        public async Task Interrupt(string connectionId)
        {
            var scriptEngine = GetScriptEngineForConnection(connectionId);
            await scriptEngine.Interrupt();

            //Ensure that the script engine isn't stopped at something.
            var breakpoints = await scriptEngine.ListBreakpoints();
            foreach (var breakpoint in breakpoints)
            {
                await scriptEngine.ClearBreakpoint(breakpoint.BreakPointNumber);
            }
            await scriptEngine.Continue(StepAction.Out);
        }


        public void InitiateScriptEngine(string connectionId)
        {
            var scriptEngine = GetScriptEngineForConnection(connectionId);
            scriptEngine.BreakpointEvent += debuggerClient_BreakpointEvent;
            scriptEngine.ExceptionEvent += debuggerClient_ExceptionEvent;
        }

        public async Task<Response> Lookup(string connectionId, bool includeSource = false, params int[] handles)
        {
            var scriptEngine = GetScriptEngineForConnection(connectionId);
            var objects = await scriptEngine.Lookup(includeSource, handles);
            return objects;
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

        public async Task<Response> Scope(string connectionId, int scopeNumber, int? frameNumber = null)
        {
            var scriptEngine = GetScriptEngineForConnection(connectionId);
            var scope = await scriptEngine.Scope(scopeNumber, frameNumber);
            return scope;
        }

        public async Task<Response> Scopes(string connectionId, int? frameNumber = null)
        {
            var scriptEngine = GetScriptEngineForConnection(connectionId);
            var scopes = await scriptEngine.Scopes(frameNumber);
            return scopes;
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
