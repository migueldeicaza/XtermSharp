XtermSharp
----------
XtermSharp is a VT100/Xterm terminal emulator for .NET, the engine is
intended to be agnostic from potential front-ends and backends.

Consumers use a higher-level implementation that provides integration
with their UI toolkit of choice.   This module contains an implementation
for Cocoa/Mac that can serve as a reference to other ports.

The terminal itself does not deal with connecting the data to to a process
or a remote server.   Data is sent to the terminal by passing a byte array
with data.

The SampleTester program is an internal test program that exercises
some of the Terminal API.

XtermSharp.Mac
--------------

This is a library built on top of XtermSharp that provides a MacOS
NSView that can be used in your applications to embed a terminal
emulator.  This maps the Mac input to send data to the underlying terminal.

Like XtermSharp, this does not wire up the terminal to a backend or a
process, this is something that your code still needs to do.

An example terminal on how to connect a Unix shell process is
available in the MacTerminal project.

History
-------

This code was originally a port of xterm.js, but has been adjusted for
processing Unicode using NStack and to make the front-end pluggable in
a .NET way.



