V8-SignalR Debugging.
==================

Demonstrates debugging running V8 scripts via SignalR


Using Clearscript V8, demonstrates the ability to leverage SignalR to broadcast V8 events (break, exception, etc) to interested clients.


Instructions:

1) Build and run the project.
2) Open a browser window to localhost:8080
3) Type some javascript based code in the top textarea (E.g. 1+1)
4) Set a breakpoint by typing a line number which you wish to break at in the "New Breakpoint" and press the "Set Breakpoint" button (0 based index)
5) Press the "Eval" button


The V8 script engine will run and stop at the breakpoint you've set.

The "Call Stack" tab shows the locals currently on the stack.

The "Console" tab allows additional scripts to eval and immediately return (i.e. mutate the value of a variable or output something)

Buttons at the top let you continue, step in, step out, stop.


Thrown exceptions will also stop at that point.


This is just a demonstration, feel free to fork away!



Roadmap:
-------

Make the code editor CodeMirror or Ace Editor based
Add ability to look at frames and so forth.
