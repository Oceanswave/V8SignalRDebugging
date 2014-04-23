namespace BaristaJS.AppEngine.Debugger {

    using System;

    public sealed class ExceptionEventArgs : EventArgs {
        public ExceptionEventArgs(EventResponse exceptionEvent) {
            ExceptionEvent = exceptionEvent;
        }

        public EventResponse ExceptionEvent { get; private set; }
    }
}