// Copyright (C) 2016-2018 madalien.com
// This file is part of bnetlauncher.
//
// bnetlauncher is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// bnetlauncher is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with bnetlauncher. If not, see <http://www.gnu.org/licenses/>.
//
//
// Contact:
// daf <daf@madalien.com>

using System;
using System.IO;
using System.Diagnostics;
using System.Threading;
using bnetlauncher.Utils;
using System.Windows.Forms;
using System.Drawing;

namespace bnetlauncher.Clients
{
    // This client is identical to BnetClient with except it uses an alternative launch method.
    // With it, it's possible to launch game variations such as ptr and wow classic, however it
    // relies on sending keypresses to the launch which is less reliable then the old method.
    // So BnetClient will be used if possible, otherwise BnetClient2.
    class BnetClient2: BnetClient
    {
        public BnetClient2()
        {
            Id = "battlenet2";
            Name = "Battle.net";
            Exe = "battle.net.exe";
            MustBeRunning = false;
        }

        /// <summary>
        /// When the client is starting fresh it does an automatic login that takes around 10 seconds, any
        /// launch command sent before it finishes is ignored. This waits until the client has been running
        /// long enough and its main window has grown to the full client size (the login window is small).
        /// If the client has already been running for a while it returns right away.
        /// </summary>
        private static void WaitForClientLogin()
        {
            const double MinLoginSeconds = 12;
            const double AlreadyRunningSeconds = 30;
            const double MaxWaitSeconds = 60;

            DateTime start = DateTime.Now;
            DateTime ready_since = DateTime.MinValue;
            int logged = 0;

            while (DateTime.Now.Subtract(start).TotalSeconds < MaxWaitSeconds)
            {
                Process client = null;
                foreach (var proc in Process.GetProcessesByName("Battle.net"))
                {
                    if (client == null || proc.MainWindowTitle == "Battle.net")
                    {
                        client = proc;
                    }
                }

                if (client != null)
                {
                    double age = double.MaxValue;
                    try
                    {
                        age = DateTime.Now.Subtract(client.StartTime).TotalSeconds;
                    }
                    catch (Exception ex)
                    {
                        Logger.Warning($"Couldn't get client start time: {ex.Message}");
                    }

                    if (age >= AlreadyRunningSeconds)
                    {
                        Logger.Information("Client has been running for a while, not waiting for login.");
                        return;
                    }

                    var size = client.MainWindowTitle == "Battle.net" ? WinApi.GetProcessMainWindowSize(client) : Size.Empty;
                    if (logged++ % 4 == 0)
                    {
                        Logger.Information($"Waiting for client login (running {(int)age}s, window {size.Width}x{size.Height}).");
                    }

                    if (age >= MinLoginSeconds && size.Width >= 1000 && size.Height >= 600)
                    {
                        // Window has to be big enough for a couple of seconds so we know it's stable.
                        if (ready_since == DateTime.MinValue)
                        {
                            ready_since = DateTime.Now;
                        }
                        else if (DateTime.Now.Subtract(ready_since).TotalSeconds >= 2)
                        {
                            Logger.Information("Client login finished.");
                            return;
                        }
                    }
                    else
                    {
                        ready_since = DateTime.MinValue;
                    }
                }

                Thread.Sleep(500);
            }

            Logger.Warning("Gave up waiting for client login, continuing anyway.");
        }

        /// <summary>
        /// Launches a battle.net client game using it's product code.
        ///
        /// Using the product code will open the client window on the apropriate
        /// tab after which enter key can be sent to start the game.
        ///
        /// </summary>
        /// <param name="cmd">Battle.net client ID command to launch.</param>
        public override bool Launch(string cmd)
        {
            Logger.Information($"Looking for installPath for '{cmd}'");
            string path = GetProductInstallPath(cmd);
            if (String.IsNullOrEmpty(path))
            {
                Logger.Error($"Couldn't find install path for {cmd}");
                return false;
            }

            // A freshly started client logs in on it's own first and ignores commands until it's done.
            WaitForClientLogin();

            try
            {
                // This is the launch parameters used by the blizzard launchers like "World of Warcraft Launcher.exe"
                // While the --game parameter is not supposed to be product code but a specific string like "wow_enus",
                // "diablo3_enus" or "hs_beta" which means there's no pattern to it or location i can extract it from.
                //
                // However during testing while --game is required it's content doesn't seem to be used for anything
                // putting in any string would still launch the game correctly, so we just fill the product code so
                // it's not empty.
                // The other two fields, gamepath and productcode must be correct though.
                Process p = Process.Start(Path.Combine(InstallPath, Exe), $"--game={cmd} --gamepath=\"{path}\" --productcode={cmd}");
                p.WaitForExit();

                // Only wait for the battle.net client window to be in the foreground for
                // a minute, otherwise exit
                DateTime start = DateTime.Now;
                while (DateTime.Now.Subtract(start).TotalMinutes < 1)
                {
                    // Agressivly scans for the battle.net client window to hit the foreground
                    foreach (var proc in Process.GetProcesses())
                    {
                        if (proc.MainWindowTitle == "Battle.net")
                        {
                            Logger.Information("Found windows for battle.net client.");

                            while (proc.MainWindowHandle == IntPtr.Zero)
                            {
                                Thread.Sleep(500);
                            }

                            Logger.Information("Maximizing battle.net window.");
                            WinApi.NativeMethods.ShowWindow(proc.MainWindowHandle, WinApi.NativeMethods.SW_SHOWMAXIMIZED);

                            // To get this color check debug bmp in Program.DataPath
                            var button_color = Color.FromArgb(255, 0, 116, 224);

                            var button_location = Point.Empty;
                            int popup_attempts = 0;
                            bool popup_seen = false;
                            DateTime settle_start = DateTime.Now;
                            for (int i = 0; i < 40; i++)
                            {
                                // The client might be showing a popup (like "This folder doesn't contain the
                                // correct version of this game") that covers the play button. If so try to
                                // dismiss it and give the client a moment to finish switching to the game's page.
                                if (WinApi.IsPopupShownInProcessMainWindow(proc))
                                {
                                    Logger.Information($"Client popup detected, trying to dismiss it (attempt {popup_attempts + 1}).");
                                    WinApi.DismissPopupInProcessMainWindow(proc, popup_attempts);
                                    popup_attempts++;
                                    popup_seen = true;
                                    Thread.Sleep(2000);
                                    continue;
                                }

                                button_location = WinApi.FindColorInProcessMainWindow(proc, button_color);
                                if (button_location != Point.Empty)
                                {
                                    // The client takes a moment to switch to the requested game's page, clicking
                                    // play too early would start whatever game was previously selected. So only
                                    // accept the button once a popup was handled or enough time has passed.
                                    if (popup_seen || DateTime.Now.Subtract(settle_start).TotalSeconds >= 4)
                                    {
                                        break;
                                    }

                                    Logger.Information("Play button found but client might still be switching games, waiting.");
                                    button_location = Point.Empty;
                                }
                                Thread.Sleep(500);
                            }

                            if (button_location == Point.Empty)
                            {
                                Logger.Warning("Play button not found after retries, skipping click.");
                                return false;
                            }

                            Logger.Information("Sending Mouse click at window");
                            WinApi.ClickWithinWindow(proc.MainWindowHandle, button_location);
                            return true;
                        }
                    }
                }

                Logger.Error("Failed to detect Battle.net client window in the foreground.");
                return false;
            }
            catch (Exception ex)
            {
                Logger.Error($"Couldn't start game using '{cmd}'.", ex);
                return false;
            }
        }
    }
}
