namespace V8SignalRDebugging
{
    using Microsoft.AspNet.SignalR;

    public class ScriptEngineHub : Hub
    {
        private readonly ScriptEngineManager m_scriptEngineManager;

        public ScriptEngineHub() : this(ScriptEngineManager.Instance) { }

        public ScriptEngineHub(ScriptEngineManager scriptEngineManater)
        {
            m_scriptEngineManager = scriptEngineManater;
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
