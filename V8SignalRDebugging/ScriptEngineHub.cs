namespace V8SignalRDebugging
{
    using System.Threading.Tasks;
    using Microsoft.AspNet.SignalR;
    using V8SignalRDebugging.Debugger;

    /// <summary>
    /// SignalR hub for interacting with the script engine.
    /// </summary>
    public class ScriptEngineHub : Hub
    {
        private readonly ScriptEngineManager m_scriptEngineManager;

        public ScriptEngineHub() : this(ScriptEngineManager.Instance) { }

        public ScriptEngineHub(ScriptEngineManager scriptEngineManager)
        {
            m_scriptEngineManager = scriptEngineManager;
        }

        public async Task Backtrace()
        {
            var result = await m_scriptEngineManager.Backtrace(Context.ConnectionId);

            Clients.All.backtrace(result);
        }

        public async void ContinueBreakpoint(string stepAction, int? stepCount = null)
        {
            StepAction eStepAction;
            stepAction.TryParseEnum(true, StepAction.Next, out eStepAction);
            await m_scriptEngineManager.Continue(Context.ConnectionId, eStepAction, stepCount);
            Clients.All.breakpointContinue(stepAction, stepCount);
        }

        public async Task Disconnect()
        {
            var result = await m_scriptEngineManager.Disconnect(Context.ConnectionId);

            Clients.All.disconnected(result);
        }

        public async Task Eval(string code)
        {
            Clients.All.beginEval();

            var result = await m_scriptEngineManager.Evaluate(Context.ConnectionId, code);

            Clients.All.evalResult(result);
        }

        public async Task EvalImmediate(string expression)
        {
            var result = await m_scriptEngineManager.EvalImmediate(Context.ConnectionId, expression);

            Clients.All.console(result);
        }

        public async Task Interrupt()
        {
            await m_scriptEngineManager.Interrupt(Context.ConnectionId);
            Clients.All.interrupt();
        }

        public async Task Scope(int scopeNumber, int? frameNumber = null)
        {
            var scope = await m_scriptEngineManager.Scope(Context.ConnectionId, scopeNumber, frameNumber);
            Clients.All.scope(scope);
        }

        public async Task Scopes(int? frameNumber = null)
        {
            var scopes = await m_scriptEngineManager.Scopes(Context.ConnectionId, frameNumber);
            Clients.All.scopes(scopes);
        }

        public async Task SetBreakpoint(int lineNumber, int? column = null, bool enabled = true, string condition = null, int? ignoreCount = 0 )
        {
            var bp = new Breakpoint
            {
                LineNumber = lineNumber,
                Column = column,
                Enabled = enabled,
                Condition = condition,
                IgnoreCount = ignoreCount
            };
            
            var breakpointNumber = await m_scriptEngineManager.SetBreakpoint(Context.ConnectionId, bp);

            Clients.All.breakpointSet(new
            {
                breakpointNumber,
                lineNumber,
                column,
                enabled,
                condition,
                ignoreCount
            });
        }

        public void ShareCode(string code)
        {
            Clients.Others.codeUpdated(code);
        }

        public override Task OnConnected()
        {
            m_scriptEngineManager.InitiateScriptEngine(Context.ConnectionId);
            return base.OnConnected();
        }

        public override Task OnDisconnected()
        {
            m_scriptEngineManager.RemoveScriptEngine(Context.ConnectionId);
            return base.OnDisconnected();
        }
    }
}
