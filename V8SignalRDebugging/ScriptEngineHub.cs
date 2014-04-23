namespace V8SignalRDebugging
{
    using System.Threading.Tasks;
    using BaristaJS.AppEngine.Debugger;
    using BaristaJS.AppEngine.Extensions;
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

        public async Task Backtrace()
        {
            var result = await m_scriptEngineManager.Backtrace(Context.ConnectionId);

            Clients.All.backtrace(result);
        }

        public async Task SetBreakpoint(int lineNumber)
        {
            await m_scriptEngineManager.SetBreakpoint(Context.ConnectionId, new Breakpoint { LineNumber = lineNumber });

            Clients.All.breakpointSet(new {
                lineNumber = lineNumber,
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
            await m_scriptEngineManager.Continue(eStepAction, stepCount);
            Clients.All.breakpointContinue(stepAction, stepCount);
        }

        public void ShareCode(string code)
        {
            Clients.Others.codeUpdated(code);
        }

        public void Eval(string name, string code)
        {
            var result = m_scriptEngineManager.Evaluate(Context.ConnectionId, code);

            Clients.All.evalResult(name, result);
        }

        public async Task EvalImmediate(string expression)
        {
            var result = await m_scriptEngineManager.EvalImmediate(expression);

            Clients.All.evalImmediateResult(result.Success ? result.Body.text : result.Message);
        }

        public void Send(string name, string message)
        {
            // Call the broadcastMessage method to update clients.
            Clients.All.addMessage(name, message);
        }
    }
}
