namespace V8SignalRDebugging
{
    using System.Collections.Generic;
    using System.Dynamic;
    using System.Globalization;
    using Microsoft.AspNet.SignalR;
    using Microsoft.AspNet.SignalR.Hubs;
    using System;
    using System.Collections.Concurrent;
    using System.Linq;
    using System.Threading.Tasks;
    using Newtonsoft.Json.Linq;
    using V8SignalRDebugging.Debugger;
    using V8SignalRDebugging.Debugger.Events;
    using V8SignalRDebugging.Debugger.Messages;

    /// <summary>
    /// Manages Script Engine Instances and performs higher-level operations.
    /// </summary>
    /// <remarks>
    /// Currently, each connection gets a script engine instance which is disposed of on disconnect.
    /// </remarks>
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
            var breakpointResponse = await scriptEngine.ListAllBreakpoints();
            foreach (var breakpoint in breakpointResponse.GetBreakpoints().Where(bp => bp.ScriptName == scriptEngine.CurrentScriptName))
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
            var breakpoints = await scriptEngine.ListAllBreakpoints();
            foreach (var breakpoint in breakpoints.GetBreakpoints().Where(bp => bp.ScriptName == scriptEngine.CurrentScriptName))
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

        public async Task<dynamic> GetScopeVariables(string connectionId, int? scopeNumber = null, int? frameNumber = null)
        {
            var scriptEngine = GetScriptEngineForConnection(connectionId);

            if (scopeNumber.HasValue == false)
            {
                var scopes = await scriptEngine.Scopes(frameNumber);
                var typedScopes = scopes.GetScopes().ToList();
                foreach (var typedScope in typedScopes)
                {
                    typedScope.Object = await GetLocalsFromScopeObject(scriptEngine, typedScope.Object);
                }
                return typedScopes;
            }

            var response = await scriptEngine.Scope(scopeNumber.Value, frameNumber);
            var scope = response.GetScope();

            var result = await GetLocalsFromScopeObject(scriptEngine, scope.Object);
            return result;
        }

        public async Task<IList<Scope>> Scopes(string connectionId, int? frameNumber = null)
        {
            var scriptEngine = GetScriptEngineForConnection(connectionId);
            var scopes = await scriptEngine.Scopes(frameNumber);
            var typedScopes = scopes.GetScopes().ToList();
            foreach (var scope in typedScopes)
            {
                scope.Object = await GetLocalsFromScopeObject(scriptEngine, scope.Object);
            }
            return typedScopes;
        }

        public async Task<int> SetBreakpoint(string connectionId, Breakpoint breakpoint)
        {
            var scriptEngine = GetScriptEngineForConnection(connectionId);
            var response = await scriptEngine.SetBreakpoint(breakpoint);
            return response.Body.breakpoint;
        }

        private V8DebugScriptEngine GetScriptEngineForConnection(string connectionId)
        {
            var console = new FirebugConsole(Clients);

            return m_connectionScriptEngines.GetOrAdd(connectionId, id => new V8DebugScriptEngine(connectionId, console));
        }

        private async Task<dynamic> GetLocalsFromScopeObject(V8DebugScriptEngine engine, dynamic obj)
        {
            var dict = obj as IDictionary<string, JToken>;
            if (dict == null || dict.ContainsKey("ref") == false)
                return obj;

            var handle = dict["ref"].Value<int>();
            var result = await engine.Lookup(false, handle);

            var lookupObj = result.Body[handle.ToString(CultureInfo.InvariantCulture)];
            var lookupObjectDict = lookupObj as IDictionary<string, JToken>;
            if (lookupObjectDict == null || lookupObjectDict.ContainsKey("properties") == false)
                return lookupObj;

            dynamic properties = new ExpandoObject();
            foreach (var property in lookupObj.properties)
            {
                if (property.attributes != 4)
                    continue;

                //Capturing name here -- possibly the value gets disposed in the next await?
                string name = property.name;
                
                var propertyValue = await GetLocalsFromScopeObject(engine, property);
                ((IDictionary<string, object>)properties).Add(name, propertyValue);
            }

            return properties;
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
