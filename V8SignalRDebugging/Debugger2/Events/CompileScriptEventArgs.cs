namespace BaristaJS.AppEngine.Debugger {

    using System;

    public sealed class CompileScriptEventArgs : EventArgs {
        public CompileScriptEventArgs(EventResponse compileScriptEvent) {
            CompileScriptEvent = compileScriptEvent;
        }

        public EventResponse CompileScriptEvent { get; private set; }
    }
}