
XtermSharp
----------

![Build Status](https://github.com/migueldeicaza/XtermSharp/workflows/CI/badge.svg)
[![Build Status](https://migueldeicaza.visualstudio.com/XtermSharp/_apis/build/status/XtermSharp-Mac-CI?branchName=master)](https://migueldeicaza.visualstudio.com/XtermSharp/_build/latest?definitionId=9&branchName=master)

<img width="45%" alt="XtermSharpGui" src="https://user-images.githubusercontent.com/36863/54497310-80eda980-48cf-11e9-84c2-14ddc054a4b6.png"><img width="45%" alt="XtermSharpMac" src="https://user-images.githubusercontent.com/36863/54497311-80eda980-48cf-11e9-9695-d7425e43262d.png">

XtermSharp is a VT100/Xterm terminal emulator for .NET, the engine is
intended to be agnostic from potential front-ends and backends.

Consumers use a higher-level implementation that provides integration
with their UI toolkit of choice.  This module contains both
implementation a Cocoa/Mac and a
[Terminal.Gui](https://github.com/migueldeicaza/gui.cs)
implementations that can serve as a reference to other ports.

The terminal itself does not deal with connecting the data to to a process
or a remote server.   Data is sent to the terminal by passing a byte array
with data to the "Feed" method.   

Convenience classes exist to spawn a subprocess and connecting the
terminal to a local process, and allow some customization of the
environment variables to pass to the child.

The SampleTester program is an internal test program that exercises
some of the Terminal API.

Long term, I want to have an embeddable console for
MonoDevelop/VisualStudioMac that is a proper terminal, so I can debug
things like gui.cs without launching an external window as well as
having a full terminal for my Gui.cs library.



XtermSharp.Mac
--------------

This is a library built on top of XtermSharp that provides a MacOS
NSView that can be used in your applications to embed a terminal
emulator.  This maps the Mac input to send data to the underlying terminal.

Like XtermSharp, this does not wire up the terminal to a backend or a
process, this is something that your code still needs to do.

An example terminal on how to connect a Unix shell process is
available in the MacTerminal project.

Features
--------

The engine is pretty good at this point and lets you use Emacs, vi,
top, mc, blessed and gui.cs both with the keyboard and the mouse.
Some features are only available on the Mac version, as the
text-version with curses imposes some limitations.

The unicode handling is pretty decent, but does not have support for
Grapheme clusters yet, that is coming up later.

Scrollback and reflow are currently missing, those are tied together,
and I think it can be done better than the original xterm.js design
supported.

Roadmap
-------

There are many issues filed that track some of the capabilities that
are missing, the list is by no means complete.   What I would like to
do is package this in a reusable form for all available platforms
(Windows, Mac, Linux, iOS, Android) so others can use this to embed
terminal emulators where necessary.

There is currently a native piece of code needed to fork/exec safely
on MacOS in the presence of MacOS recent "Runtime Hardening".   

Some of the Gui.cs code needs to be moved to its XtermSharp.GuiCs
library, and the SubprocessTerminalView needs to introduced for MacOS.

History
-------

I wanted to have a console emulator for .NET since the days of
Silverlight, but never quite got around it - but having an engine that
could have different front-ends and backends is something that I have
desired for a long time.  I did [some bits and
pieces](https://github.com/mono/pty-sharp) some years ago which are
not required nowadays.

This code was originally a port of xterm.js, but has been adjusted for
proper processing Unicode using NStack and to make the front-end
pluggable in a .NET way.  While xterm.js was a useful starting point,
the code has deviated and there is limited value in continuing the
port, this code can now live on its own.

