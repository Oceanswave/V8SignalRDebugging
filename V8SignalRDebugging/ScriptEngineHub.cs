namespace V8SignalRDebugging
{
    using System.Net.Mail;
    using Microsoft.AspNet.SignalR;

    public class ScriptEngineHub : Hub
    {
        public void Send(string name, string message)
        {
            // Call the broadcastMessage method to update clients.
            Clients.All.addMessage(name, message);
        }
    }
}
