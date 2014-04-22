namespace V8SignalRDebugging
{
    using System;
    using Microsoft.ClearScript.V8;
    using Microsoft.Owin.Hosting;

    class Program
    {
        static void Main(string[] args)
        {
            var engine = new V8ScriptEngine();
            string url = "http://localhost:8080";
            using (WebApp.Start(url))
            {
                Console.WriteLine("Server running on {0}", url);
                Console.ReadLine();
            }
        }
    }
}
