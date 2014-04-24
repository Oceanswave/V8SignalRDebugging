namespace V8SignalRDebugging.Debugger.Events
{
    using System;
    using V8SignalRDebugging.Debugger.Messages;

    public sealed class ExceptionEventArgs : EventArgs
    {
        public ExceptionEventArgs(EventResponse exceptionEvent)
        {
            ExceptionEvent = exceptionEvent;
        }

        public EventResponse ExceptionEvent { get; private set; }
    }
}