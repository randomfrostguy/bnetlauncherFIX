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

        // How long to keep waiting for the play button to show up (a game update can take a while).
        private const double MaxWaitMinutes = 180;

        // How long without a play button (and without a popup) before the wait notice is shown.
        private const double NoticeAfterSeconds = 15;

        /// <summary>
        /// Waits for the given time while keeping any window bnetlauncher shows responsive.
        /// </summary>
        private static void PumpWait(int milliseconds)
        {
            DateTime end = DateTime.Now.AddMilliseconds(milliseconds);
            while (DateTime.Now < end)
            {
                Application.DoEvents();
                Thread.Sleep(20);
            }
        }

        /// <summary>
        /// Shows a small always on top window telling the user the game is being updated. It's placed on the
        /// lower half of the screen so it doesn't get in the way of the popup dismissing clicks.
        /// </summary>
        /// <param name="onClosed">Called when the window is closed.</param>
        private static Form ShowMaintenanceNotice(Action onClosed)
        {
            var area = Screen.PrimaryScreen.WorkingArea;
            var size = new Size(480, 190);

            var form = new Form
            {
                Text = "bnetlauncher",
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false,
                TopMost = true,
                StartPosition = FormStartPosition.Manual,
                ClientSize = size,
                Location = new Point(area.Left + (area.Width - size.Width) / 2, area.Top + area.Height * 72 / 100)
            };

            var title = new Label
            {
                Text = "Please wait, Azeroth is receiving maintenance!",
                Font = new Font("Segoe UI", 13f, FontStyle.Bold),
                Location = new Point(20, 16),
                Size = new Size(440, 30)
            };

            var details = new Label
            {
                Text = "bnetlauncher is waiting for the Play button. The game is most likely updating. " +
                    "This window will close and the game will start on its own once it's ready.",
                Font = new Font("Segoe UI", 10f),
                Location = new Point(20, 54),
                Size = new Size(440, 56)
            };

            var bar = new ProgressBar
            {
                Style = ProgressBarStyle.Marquee,
                MarqueeAnimationSpeed = 30,
                Location = new Point(20, 122),
                Size = new Size(440, 14)
            };

            var cancel = new Button
            {
                Text = "Cancel",
                Location = new Point(380, 148),
                Size = new Size(80, 28)
            };
            cancel.Click += (sender, e) => form.Close();

            form.Controls.Add(title);
            form.Controls.Add(details);
            form.Controls.Add(bar);
            form.Controls.Add(cancel);
            form.FormClosed += (sender, e) => onClosed?.Invoke();

            form.Show();
            return form;
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
            Logger.Information($"Target game product code: '{cmd}'.");
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
                            bool notice_done = false;
                            bool notice_cancelled = false;
                            Form notice = null;
                            DateTime settle_start = DateTime.Now;
                            IntPtr window_handle = proc.MainWindowHandle;

                            try
                            {
                                // Keeps looking for the play button for a long time, since a game that's updating
                                // doesn't have one until the update is done.
                                while (!notice_cancelled && DateTime.Now.Subtract(settle_start).TotalMinutes < MaxWaitMinutes)
                                {
                                    // The client replaces its window when the login finishes, so make sure we're not
                                    // holding on to an old window handle (it would report an empty 0x0 window).
                                    proc.Refresh();
                                    if (proc.MainWindowHandle == IntPtr.Zero)
                                    {
                                        PumpWait(500);
                                        continue;
                                    }

                                    if (proc.MainWindowHandle != window_handle)
                                    {
                                        Logger.Information("Client window changed, maximizing the new one.");
                                        window_handle = proc.MainWindowHandle;
                                        WinApi.NativeMethods.ShowWindow(window_handle, WinApi.NativeMethods.SW_SHOWMAXIMIZED);
                                    }

                                    // The client might be showing a popup (like "This folder doesn't contain the
                                    // correct version of this game" or "What's New") that covers the play button.
                                    // If so try to dismiss it and give the client a moment to finish switching to
                                    // the game's page.
                                    if (WinApi.IsPopupShownInProcessMainWindow(proc))
                                    {
                                        Logger.Information($"Client popup detected, trying to dismiss it (attempt {popup_attempts + 1}).");
                                        WinApi.DismissPopupInProcessMainWindow(proc, popup_attempts);
                                        popup_attempts++;
                                        popup_seen = true;
                                        PumpWait(2000);
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
                                    else if (notice == null && DateTime.Now.Subtract(settle_start).TotalSeconds >= NoticeAfterSeconds)
                                    {
                                        // No play button and no popup, most likely the game is updating.
                                        Logger.Information("Play button not available, game is probably updating. Showing wait notice.");
                                        Splash.Hide();
                                        notice = ShowMaintenanceNotice(() =>
                                        {
                                            if (!notice_done)
                                            {
                                                Logger.Information("User closed the wait notice.");
                                                notice_cancelled = true;
                                            }
                                        });
                                    }

                                    PumpWait(500);
                                }
                            }
                            finally
                            {
                                // Closes the wait notice (if it was shown), the game is ready or we're giving up.
                                notice_done = true;
                                if (notice != null)
                                {
                                    notice.Close();
                                    notice.Dispose();
                                    Splash.Unhide();
                                }
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
