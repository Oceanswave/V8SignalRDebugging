namespace BaristaJS.AppEngine.Debugger
{
    public class Breakpoint
    {
        public Breakpoint()
        {
            this.Enabled = true;
        }

        public Breakpoint(int lineNumber)
            : this()
        {
            this.LineNumber = lineNumber;
        }

        public int BreakPointNumber
        {
            get;
            set;
        }

        public int LineNumber
        {
            get;
            set;
        }

        public int? Column
        {
            get;
            set;
        }

        public bool Enabled
        {
            get;
            set;
        }

        public string Condition
        {
            get;
            set;
        }

        public int? IgnoreCount
        {
            get;
            set;
        }
    }
}
