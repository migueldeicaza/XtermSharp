using System;
using Terminal.Gui;

namespace GuiCsHost {
	class MainClass {
		public static void Main (string [] args)
		{
			Application.Init ();
			var top = Application.Top;
			var win = new Window ("MyApp") {
				X = 0,
				Y = 1,
				Width = Dim.Fill (),
				Height = Dim.Fill ()
			};
			top.Add (win);

			// Creates a menubar, the item "New" has a help menu.
			var menu = new MenuBar (new MenuBarItem [] {
				new MenuBarItem ("_File", new MenuItem [] {
					new MenuItem ("_New", "Creates new file", () => { }),
					new MenuItem ("_Close", "", () => { }),
					new MenuItem ("_Quit", "", () => { top.Running = false; })
					}),
				new MenuBarItem ("_Edit", new MenuItem [] {
					new MenuItem ("_Copy", "", null),
					new MenuItem ("C_ut", "", null),
					new MenuItem ("_Paste", "", null)
				    })
			});
			top.Add (menu);

			var terminal = new TerminalView () {
				X = 0,
				Y = 0,
				Width = Dim.Fill (),
				Height = Dim.Fill ()
			};
			top.Add (terminal);

			Application.Run ();
		}
	}
}
