namespace V8SignalRDebugging.Debugger.Events
{
    using System;
    using V8SignalRDebugging.Debugger.Messages;

    public sealed class BreakpointEventArgs : EventArgs
    {
        public BreakpointEventArgs(EventResponse breakpointEvent)
        {
            BreakpointEvent = breakpointEvent;
        }

        public EventResponse BreakpointEvent { get; private set; }
    }
}