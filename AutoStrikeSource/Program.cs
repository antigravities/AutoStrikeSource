using Gameloop.Vdf;
using Gameloop.Vdf.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace AutoStrikeSource {
    class Program {
        private static List<string> steps = new List<string>();

        public static string FindSteam() {
            if(RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
                string loc = Microsoft.Win32.Registry.GetValue("HKEY_LOCAL_MACHINE\\SOFTWARE\\Classes\\steam\\Shell\\Open\\Command", null, "").ToString();

                if(!loc.Equals("")) {
                    Regex launchMatch = new Regex("\\\"(.*)\\\" \\-\\- \"%1\"");

                    if(!launchMatch.IsMatch(loc)) return null;

                    return Path.GetDirectoryName(launchMatch.Matches(loc)[0].Groups[1].Value);
                }
            }

            return null;
        }

        public static List<string> FindSteamLibraries(string steam) {
            dynamic vdf = VdfConvert.Deserialize(File.ReadAllText(string.Join(Path.DirectorySeparatorChar, new List<string> { steam, "config", "config.vdf" })));

            List<string> libraries = new List<string>();

            if(Directory.Exists(string.Join(Path.DirectorySeparatorChar, new List<string> { steam, "steamapps" }))) libraries.Add(string.Join(Path.DirectorySeparatorChar, new List<string> { steam, "steamapps" }));

            try {
                foreach(VProperty child in ( (VObject) vdf.Value.Software.valve.steam ).Children()) {
                    if(child.Key.StartsWith("BaseInstallFolder_")) {
                        libraries.Add(string.Join(Path.DirectorySeparatorChar, new List<string> { child.Value.ToString(), "steamapps" }));
                    }
                }
            } catch(Exception) {
                return null;
            }

            return libraries;
        }

        static void UpdateProgressBar(int percent, string text) {
            Console.Clear();

            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine("Auto-Strike: Source");
            Console.ForegroundColor = ConsoleColor.White;

            Console.WriteLine();
            Console.WriteLine(text);
            Console.WriteLine();

            Console.Write("[");
            for(int i = 0; i < percent / 2; i++) Console.Write("=");
            for(int i = 0; i < ( 50 - ( percent / 2 ) ); i++) Console.Write(" ");
            Console.WriteLine("] " + percent + "%");

            Console.WriteLine();
            Console.WriteLine();

            Console.ForegroundColor = ConsoleColor.DarkGray;

            foreach(string s in steps.Take(5)) {
                Console.WriteLine(s);
            }

            steps.Insert(0, text);
            Console.ForegroundColor = ConsoleColor.White;
        }

        static void ModMountCfg(string gmod, string cstrike) {
            VProperty prop = VdfConvert.Deserialize(File.ReadAllText(string.Join(Path.DirectorySeparatorChar, new List<string> { gmod, "garrysmod", "cfg", "mount.cfg" })));
            prop.Value["cstrike"] = new VValue(cstrike);
            File.WriteAllText(string.Join(Path.DirectorySeparatorChar, new List<string> { gmod, "garrysmod", "cfg", "mount.cfg" }), VdfConvert.Serialize(prop));
        }

        static async Task Run() {
            Console.ForegroundColor = ConsoleColor.White;

            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine("Auto-Strike: Source");
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("");
            Console.WriteLine("This tool automates the acquisition of Counter-Strike: Source assets from the Source Dedicated Server distribution. It is available for free from Steam. To learn about the Source Dedicated Server, visit https://developer.valvesoftware.com/wiki/Source_Dedicated_Server.");
            Console.WriteLine("");
            Console.WriteLine("In short, this tool will:");
            Console.WriteLine("1. Acquire the SteamPipe download tools.");
            Console.WriteLine("2. Download the Counter-Strike: Source dedicated server.");
            Console.WriteLine("3. Move the game content directory to the Garry's Mod SteamApps directory.");
            Console.WriteLine("4. Adjust the Garry's Mod mount.cfg file to mount Counter-Strike: Source.");
            Console.WriteLine("5. Clean up.");
            Console.WriteLine("");
            Console.WriteLine("Before starting, make sure that you have approximately 2 GB of disk space on the drive Garry's Mod is installed. This process may take a long time depending on your internet connection, CPU speed, and hard disk speed.");
            Console.WriteLine("");
            Console.WriteLine("Press C to continue, or any other key to exit...");

            if(Console.ReadKey(true).Key != ConsoleKey.C) {
                Console.WriteLine("Aborting...");
                return;
            }

            string gmod = null;

            string steam = FindSteam();

            while(gmod == null) {
                if(steam == null || !Directory.Exists(steam)) {
                    Console.Clear();
                    Console.ForegroundColor = ConsoleColor.DarkYellow;
                    Console.WriteLine("Auto-Strike: Source");
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.WriteLine();
                    Console.WriteLine("Could not automatically find Steam or Garry's Mod, or the path you provided was incorrect. Please enter Steam's location below:");
                    Console.WriteLine();
                    steam = Console.ReadLine();
                }

                UpdateProgressBar(0, "Finding Garry's Mod");

                foreach(string lib in FindSteamLibraries(steam)) {
                    if(!File.Exists(lib + Path.DirectorySeparatorChar + "appmanifest_4000.acf")) continue;
                    else gmod = string.Join(Path.DirectorySeparatorChar, new List<string> { lib, "common", "GarrysMod" });
                }
            }

            UpdateProgressBar(1, "Found Garry's Mod at " + gmod);

            UpdateProgressBar(2, "Preparing to download SteamCMD");
            Directory.CreateDirectory(gmod + Path.DirectorySeparatorChar + "ass");

            UpdateProgressBar(3, "Downloading SteamCMD");

            string assbase = gmod + Path.DirectorySeparatorChar + "ass" + Path.DirectorySeparatorChar;

            HttpClient client = new HttpClient();

            using(ZipArchive arc = new ZipArchive(await ( await client.GetAsync("https://steamcdn-a.akamaihd.net/client/installer/steamcmd.zip") ).Content.ReadAsStreamAsync())) {
                UpdateProgressBar(13, "Unzipping SteamCMD");

                foreach(ZipArchiveEntry entry in arc.Entries) {
                    if(entry.FullName.Equals("steamcmd.exe")) {
                        using(FileStream file = File.Create(assbase + "steamcmd.exe"))
                        using(Stream zipped = entry.Open()) {
                            zipped.CopyTo(file);
                        }
                    }
                }
            }

            UpdateProgressBar(23, "Updating SteamCMD");

            Process p = new Process {
                StartInfo = new ProcessStartInfo {
                    Arguments = "+quit",
                    FileName = assbase + "steamcmd.exe",
                    UseShellExecute = true
                }
            };

            p.Start();
            p.WaitForExit();

            UpdateProgressBar(33, "Writing install script");

            File.WriteAllText(assbase + "install.cmd", @"
@ShutdownOnFailedCommand 1
@NoPromptForPassword 1

login anonymous

force_install_dir cstrike
app_update 232330 validate

quit
");

            UpdateProgressBar(34, "Downloading Counter-Strike: Source Dedicated Server");

            p = new Process {
                StartInfo = new ProcessStartInfo {
                    Arguments = "+runscript install.cmd",
                    FileName = assbase + "steamcmd.exe",
                    UseShellExecute = true
                }
            };

            p.Start();
            p.WaitForExit();

            UpdateProgressBar(90, "Moving files");

            Directory.Move(string.Join(Path.DirectorySeparatorChar, new List<string> { assbase, "cstrike", "cstrike" }), gmod + Path.DirectorySeparatorChar + "cstrike");

            UpdateProgressBar(97, "Modifying mount.cfg");

            ModMountCfg(gmod, gmod + Path.DirectorySeparatorChar + "cstrike");

            UpdateProgressBar(98, "Cleaning up");
            Directory.Delete(assbase, true);

            UpdateProgressBar(100, "Done");

            Console.ReadKey();
        }

        static void Main(string[] args) {
            Run().Wait();
        }
    }
}
