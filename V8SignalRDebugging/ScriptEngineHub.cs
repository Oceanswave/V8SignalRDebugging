namespace V8SignalRDebugging
{
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

        public async void SetBreakpoint(int lineNumber)
        {
            var breakpointNumber = await m_scriptEngineManager.SetBreakpoint(lineNumber);

            Clients.All.breakpointSet(new {
                id = breakpointNumber,
                lineNumber = lineNumber,
                /*column = column,
                enabled = enabled,
                condition = condition,
                ignoreCount = ignoreCount*/
            });
        }

        public async void ContinueBreakpoint(StepAction stepAction = StepAction.Next, int? stepCount = null)
        {
            await m_scriptEngineManager.Continue(stepAction, stepCount);
            Clients.All.breakpointContinue(stepAction, stepCount);
        }

        public async void Eval(string name, string code)
        {
            var result = await m_scriptEngineManager.Evaluate(code);

            Clients.All.evalResult(name, result);
        }

        public void Send(string name, string message)
        {
            // Call the broadcastMessage method to update clients.
            Clients.All.addMessage(name, message);
        }
    }
}
