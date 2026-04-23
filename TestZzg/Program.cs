using System;
using System.IO;
using System.Threading;
using System.Collections.Generic;
using System.Text;
using MyConsoleUI;
using TestZZG.Properties;

namespace Bev.Zzg
{
    class MainClass
    {
        private static bool exitLoop = false;

        public static void Main(string[] args)
        {
            // all settings are stored in TestZZG.exe.config
            Settings settings = new Settings();
            string sFilename = Path.Combine(settings.LogFilePath, settings.LogFileName);

            // instantiate the basic console UI and give welcome message
            var ui = new MyUI();
            ui.Welcome();

            // logging program start
            Logging(sFilename, string.Format("# {0} Program {1}, version {2},    started.", DateTime.UtcNow.ToString("dd.MM.yyyy HH:mm:ss"), ui.Title, ui.Version));

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
            ui.EmptyLine();
            ui.WriteLine("Log file: " + sFilename);
            ui.WriteLine("Polling interval: " + settings.PollingMinutes + " min");
            foreach (var z in zzgs) ui.WriteLine("Instrument: " + z.ToString());
            ui.EmptyLine();

            // prepare the result text line with StringBuilder
            StringBuilder sb = new StringBuilder(100);
            BevZzgStatus status;
            bool bAlert = false; // true if at least one instrument's status is not "Synchron"

            // here comes the polling loop
            while (!exitLoop)
            {
                bAlert = false;
                sb.Clear();
                sb.Append(string.Format("  {0}", DateTime.UtcNow.ToString("dd.MM.yyyy HH:mm:ss")));

                foreach (var z in zzgs)
                {
                    // checking ZZG
                    ui.StartOperation("checking " + z.ToString());
                    status = z.CheckSynchro();
                    ui.Done(" -> "+status.ToString());

                    // complete the result line
                    sb.Append(string.Format(" {0}->{1}", z, status));

                    // set alert flag
                    if (status != BevZzgStatus.Synchron) bAlert = true;

                    // check break condition
                    if (exitLoop) ExitOnCtrlC(sFilename);
                }

                // check break condition
                if (exitLoop) ExitOnCtrlC(sFilename);

                if (bAlert) sb[0] = '!';

                // log result
                ui.WritingFile(sFilename);
                if (Logging(sFilename, sb.ToString()))
                    ui.Done();
                else
                    ui.Abort();

                // Alert!
                if (bAlert) AlertUser();

                ui.EmptyLine();

                // now wait, give chance to stop every half second
                for (int i = 0; i < settings.PollingMinutes*120-10; i++) 
                {
                    Thread.Sleep(500);
                    if (exitLoop) ExitOnCtrlC (sFilename); 
                }
            }
            ExitOnCtrlC(sFilename);
        }

        /// <summary>
        /// This is the very basic logger for the application.
        /// </summary>
        /// <param name="filename">Name of log-file.</param>
        /// <param name="line">Line of text to be logged in file.</param>
        /// <returns></returns>
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
            Logging(filename, string.Format ("# {0} Program stopped by user (CTRL+C).", DateTime.UtcNow.ToString ("dd.MM.yyyy HH:mm:ss")));
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
