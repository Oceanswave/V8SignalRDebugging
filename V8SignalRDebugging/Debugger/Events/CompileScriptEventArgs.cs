namespace V8SignalRDebugging.Debugger.Events
{
    using System;
    using V8SignalRDebugging.Debugger.Messages;

    public sealed class CompileScriptEventArgs : EventArgs
    {
        public CompileScriptEventArgs(EventResponse compileScriptEvent)
        {
            CompileScriptEvent = compileScriptEvent;
        }

        public EventResponse CompileScriptEvent { get; private set; }
    }
}