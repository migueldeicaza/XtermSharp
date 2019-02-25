XtermSharp is intended to be a library that provides a .NET emulator
for VT100/Xterm terminals.   The library is intended to to provide the
basic parsing capabilities, which need to be wired up both to render
and to send input to the terminal.

The plan is to add a few options, at least a gui.cs (to create terminals
inside terminal applications), one for Cocoa, one for Gtk+ and so on.

This is currently based on xterm.js to get the basics going, but the code
will soon remove various levels of abstraction and move towards a model
that is suitable to implement various front-ends.

