namespace V8SignalRDebugging
{
    using System.Threading.Tasks;
    using Microsoft.AspNet.SignalR;
    using V8SignalRDebugging.Debugger;

    public class ScriptEngineHub : Hub
    {
        private readonly ScriptEngineManager m_scriptEngineManager;

        public ScriptEngineHub() : this(ScriptEngineManager.Instance) { }

        public ScriptEngineHub(ScriptEngineManager scriptEngineManager)
        {
            m_scriptEngineManager = scriptEngineManager;
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

        public async Task Backtrace()
        {
            var result = await m_scriptEngineManager.Backtrace(Context.ConnectionId);

            Clients.All.backtrace(result);
        }

        public async Task SetBreakpoint(int lineNumber)
        {
            await m_scriptEngineManager.SetBreakpoint(Context.ConnectionId, new Breakpoint { LineNumber = lineNumber });

            Clients.All.breakpointSet(new {
                lineNumber,
                /*column = column,
                enabled = enabled,
                condition = condition,
                ignoreCount = ignoreCount*/
            });
        }

        public async void ContinueBreakpoint(string stepAction, int? stepCount = null)
        {
            StepAction eStepAction;
            stepAction.TryParseEnum(true, StepAction.Next, out eStepAction);
            await m_scriptEngineManager.Continue(Context.ConnectionId, eStepAction, stepCount);
            Clients.All.breakpointContinue(stepAction, stepCount);
        }

        public void ShareCode(string code)
        {
            Clients.Others.codeUpdated(code);
        }

        public async Task Disconnect()
        {
            var result = await m_scriptEngineManager.Disconnect(Context.ConnectionId);

            Clients.All.disconnected(result);
        }

        public async Task Eval(string name, string code)
        {
            var result = await m_scriptEngineManager.Evaluate(Context.ConnectionId, code);

            Clients.All.evalResult(name, result);
        }

        public async Task EvalImmediate(string expression)
        {
            var result = await m_scriptEngineManager.EvalImmediate(Context.ConnectionId, expression);

            Clients.All.console(result);
        }
    }
}
