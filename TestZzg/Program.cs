using System;
using System.IO;
using System.Threading;
using System.Collections.Generic;
using System.Text;
using At.Matus.UI.ConsoleUI;
using TestZZG.Properties;

namespace Bev.Zzg
{
    class MainClass
    {
        private static bool exitLoop = false;

        public static void Main(string[] args)
        {
            Settings settings = new Settings(); // all settings are stored in TestZZG.exe.config
            string fileName = Path.Combine(settings.LogFilePath, settings.LogFileName);

            ConsoleUI.Welcome();

            // logging program start
            Logging(fileName, $"# {DateTime.UtcNow.ToString("dd.MM.yyyy HH:mm:ss")} Program {ConsoleUI.Title}, version {ConsoleUI.Version}");

            // The ctrl+C event handler just sets a bool field.
            // Interference with lengthy operations are thus avoided
            Console.CancelKeyPress += delegate(object sender, ConsoleCancelEventArgs e) 
            {
                e.Cancel = true;
                exitLoop = true;
            };

            AppDomain.CurrentDomain.ProcessExit += delegate (object sender, EventArgs e) // does not work
            {
                exitLoop = true;
            };


            // although we have only two instruments, a List<> seems appropiate
            List<BevZzg> zzgs = new List<BevZzg>();
            zzgs.Add(new BevZzg(settings.Port1, settings.Name1)); // instantiate a ZZG, connect to it and add to list
            zzgs.Add(new BevZzg(settings.Port2, settings.Name2)); // instantiate a ZZG, connect to it and add to list

            // print out basic parameters
            ConsoleUI.WriteLine();
            ConsoleUI.WriteLine($"Log file: {fileName}");
            ConsoleUI.WriteLine($"Polling interval: {settings.PollingMinutes} min");
            foreach (var z in zzgs) ConsoleUI.WriteLine($"Instrument: {z}");
            ConsoleUI.WriteLine();

            // prepare the result text line with StringBuilder
            StringBuilder sb = new StringBuilder();
            BevZzgStatus status;
            bool alert = false; // true if at least one instrument's status is not "Synchron"

            while (!exitLoop)
            {
                alert = false;
                sb.Clear();
                sb.Append($"  {DateTime.UtcNow.ToString("dd.MM.yyyy HH:mm:ss")}");

                foreach (var z in zzgs)
                {
                    // checking ZZG
                    ConsoleUI.StartOperation($"checking  {z}");
                    status = z.CheckSynchro();
                    ConsoleUI.Done($" -> {status}");

                    // complete the result line
                    sb.Append($" {z}->{status}");

                    // set alert flag
                    if (status != BevZzgStatus.Synchron) alert = true;

                    // check break condition
                    if (exitLoop) ExitOnCtrlC(fileName);
                }

                // check break condition
                if (exitLoop) ExitOnCtrlC(fileName);

                if (alert) sb[0] = '!';

                // log result
                ConsoleUI.WritingFile(fileName);
                if (Logging(fileName, sb.ToString()))
                    ConsoleUI.Done();
                else
                    ConsoleUI.Abort();

                // Alert!
                if (alert) AlertUser();

                ConsoleUI.WriteLine();

                // now wait, give chance to stop every half second
                for (int i = 0; i < settings.PollingMinutes*120-10; i++) 
                {
                    Thread.Sleep(500);
                    if (exitLoop) ExitOnCtrlC (fileName); 
                }
            }
            ExitOnCtrlC(fileName);
        }

        static bool Logging(string filename, string line)
        {
            try
            {
                StreamWriter sr = new StreamWriter(filename, true);
                sr.WriteLine(line);
                sr.Close();
            }
            catch (Exception)
            { return false; }
            return true;
        }

        static void ExitOnCtrlC(string filename)
        {
            Logging(filename, $"# {DateTime.UtcNow.ToString("dd.MM.yyyy HH:mm:ss")} Program stopped by user (CTRL+C).");
            Environment.Exit(0);
        }

        static void AlertUser()
        {
            Console.BackgroundColor = ConsoleColor.Red;
            Console.ForegroundColor = ConsoleColor.DarkBlue;
            Console.WriteLine("                                                       ");
            Console.WriteLine("             !!!!!!!!!!!!!!!!!!!!!!!!!!!!!             ");
            Console.WriteLine("             !!!!!                   !!!!!             ");
            Console.WriteLine("             !!!!!       ALERT       !!!!!             ");
            Console.WriteLine("             !!!!!                   !!!!!             ");
            Console.WriteLine("             !!!!!!!!!!!!!!!!!!!!!!!!!!!!!             ");
            Console.WriteLine("                                                       ");
            Console.ResetColor();
        }
    }
}
