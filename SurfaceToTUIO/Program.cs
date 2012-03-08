/***************************************************************************************
*  Author: 	Toni Schmidt
*  Mail:	toni.schmidt@uni-konstanz.de
*
*		Project Squidy, http://www.squidy-lib.de
*		Human-Computer Interaction Group
*		University of Konstanz, Germany
*		http://hci.uni-konstanz.de
*		http://sourceforge.net/projects/squidy-lib/
*
*  Copyright © 2009, Human-Computer Interaction Group, University of Konstanz, Germany
*  
*  This file is part of SurfaceToTUIO.
*
*  SurfaceToTUIO is free software: you can redistribute it and/or modify
*  it under the terms of the GNU Lesser General Public License as published by
*  the Free Software Foundation, either version 3 of the License, or
*  (at your option) any later version.
*
*  SurfaceToTUIO is distributed in the hope that it will be useful,
*  but WITHOUT ANY WARRANTY; without even the implied warranty of
*  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
*  GNU Lesser General Public License for more details.
*
*  You should have received a copy of the GNU Lesser General Public License
*  along with SurfaceToTUIO.  If not, see <http://www.gnu.org/licenses/>.
*
***************************************************************************************/


using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Surface;
using Microsoft.Surface.Core;
using Microsoft.Xna.Framework;

namespace SurfaceToTUIO
{
    static class Program
    {
        // Hold on to the game window.
        static GameWindow Window;

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            // Disable the WinForms unhandled exception dialog.
            // SurfaceShell will notify the user.
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);

            // Apply Surface globalization settings
            GlobalizationSettings.ApplyToCurrentThread();

            using (App1 app = new App1())
            {
                app.Run();
            }
        }

        /// <summary>
        /// Registers event handlers and sets the initial position of the game window.
        /// </summary>
        /// <param name="window">the game window</param>
        internal static void PositionWindow(GameWindow window)
        {
            if (window == null)
                throw new ArgumentNullException("window");
            
            Window = window;
            IntPtr hWnd = Window.Handle;
            Form form = (Form)Form.FromHandle(hWnd);
            form.ShowInTaskbar = false;
            //form.FormBorderStyle = FormBorderStyle.FixedToolWindow;
            form.SetDesktopBounds(-100, -100, 10, 10);
        }
      
    }
}

