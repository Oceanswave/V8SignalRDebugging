namespace V8SignalRDebugging
{
    using Microsoft.Owin.Cors;
    using Microsoft.Owin.FileSystems;
    using Microsoft.Owin.StaticFiles;
    using Owin;
    using System.IO;

    class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            app.UseCors(CorsOptions.AllowAll);

            var path = Path.Combine(
                Path.GetDirectoryName("..\\..\\"), "content");

            app.UseStaticFiles(new StaticFileOptions
            {
                FileSystem = new PhysicalFileSystem(path)
            });
            app.MapSignalR();
        }
    }
}
