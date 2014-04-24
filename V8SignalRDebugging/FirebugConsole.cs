namespace V8SignalRDebugging
{
    using Microsoft.AspNet.SignalR.Hubs;
    using Microsoft.ClearScript;
    using Newtonsoft.Json;

    public class FirebugConsole
    {
        public FirebugConsole(IHubConnectionContext context)
        {
            Context = context;
        }

        public IHubConnectionContext Context
        {
            get;
            private set;
        }

        [ScriptMember("log")]
        public void Log(object message)
        {
            if (message.GetType().ToString() == "Microsoft.ClearScript.V8.V8ScriptItem")
                Context.All.console(new
                {
                    success = true,
                    body = new
                    {
                        type = "object",
                        value = JsonConvert.SerializeObject(message),
                    }
                });
            else
                Context.All.console(new
                {
                    success = true,
                    body = new
                    {
                        type = "string",
                        value = JsonConvert.SerializeObject(message),
                        text = message.ToString()
                    }
                });
        }
    }
}
