using System;
using System.IO.Ports;
using System.Text;
using System.Threading;

namespace Bev.Zzg
{
    /// <summary>
    /// Class for communication with the ZZG (Zeitzeichengeber) via RS232.
    /// </summary>
    public class BevZzg
    {
        private static int DELAY = 5;       // Delay time in ms, between sending and reading
        private bool isConnected;           // true if connection to ZZG ok
        private string comPortName;         // name of the COM port
        private string instrumentName;      // designation of instrument (user supplied)
        private SerialPort comPort;         // the com port object

        public bool IsConnected => isConnected;
        public string ComPort => comPortName;
        public string InstrumentName => instrumentName;

        public BevZzg(string sCom, string name)
        {
            instrumentName = name.Trim();
            if (instrumentName == "") instrumentName = "<unknown>";
            comPortName = sCom.ToUpper().Trim();
            isConnected = false;
            Connect();
        }
        public BevZzg(string sCom) : this(sCom, "") { }
        

        public string QueryTime()
        {
            if(!SendCommand("U?"))
                return "";
            string reply = ReadLine();
            if(reply.Length!=8) return "";
            return reply.Substring(2);
        }

        public string QueryDate()
        {
            if(!SendCommand("D?"))
                return "";
            string reply = ReadLine();
            if (reply.Length != 8) return "";
            return reply.Substring(2);

        }

        public string QueryAB()
        {
            if (!SendCommand("Z?"))
                return "";
            string reply = ReadLine();
            if (reply.Length != 11) return "";
            return reply.Substring(2);
        }

        public string QueryLeapSecond()
        {
            if (!SendCommand("S?"))
                return "";
            string reply = ReadLine();
            if (reply.Length != 16) return "";
            return reply.Substring(2);
        }

        public string QueryDate(int loop)
        {
            if (loop <= 0)
                loop = 1;
            string ret = "";
            for (int i = 0; i < loop; i++)
            {
                ret = QueryDate();
                if (ret != "") break;
            }
            return ret;
        }

        public string QueryAB(int loop)
        {
            if (loop <= 0)
                loop = 1;
            string ret = "";
            for (int i = 0; i < loop; i++)
            {
                ret = QueryAB();
                if (ret != "") break;
            }
            return ret;
        }

        public string QueryLeapSecond(int loop)
        {
            if (loop <= 0)
                loop = 1;
            string ret="";
            for (int i = 0; i < loop; i++)
            {
                ret = QueryLeapSecond();
                if (ret != "") break;
            }
            return ret;
        }

        /// <summary>
        /// Sets the ZZG's time. The supplied string must be of the correct format.
        /// </summary>
        /// <returns><c>true</c>, if time was set, <c>false</c> otherwise.</returns>
        /// <param name="str">String in HHmmss format.</param>
        /// <remarks>Synchronisation problem!</remarks>
        public bool SetTime(string str)
        {
            if (str.Length != 6)    // basic syntax check
                return false;
            return SendCommand("U="+str);
        }

        /// <summary>
        /// Sets the ZZG's time to system time.
        /// </summary>
        /// <returns><c>true</c>, if time was set, <c>false</c> otherwise.</returns>
        public bool SetTime() => SetTime(DateTime.UtcNow.ToString("HHmmss"));

        /// <summary>
        /// Sets the ZZG's date. The supplied string must be of the correct format.
        /// </summary>
        /// <returns><c>true</c>, if time was set, <c>false</c> otherwise.</returns>
        /// <param name="str">String in ddMMyy format.</param>
        /// <remarks>Synchronisation problem!</remarks>
        public bool SetDate(string str)
        {
            if (str.Length != 6)    // primitive syntax check
                return false;
            return SendCommand("D="+str);
        }

        /// <summary>
        /// Sets the ZZG's date to system date.
        /// </summary>
        /// <returns><c>true</c>, if time was set, <c>false</c> otherwise.</returns>
        public bool SetDate() => SetDate(DateTime.UtcNow.ToString("ddMMyy"));

        public BevZzgStatus CheckSynchro()
        {
            Connect();

            if (!isConnected)
                return BevZzgStatus.NotConnected;

            int TIMELOOP = 50;
            int DATELOOP = 5;

            BevZzgStatus status;
            int nSync = 0;
            int nSysTimeChanged = 0;
            int nAsyncTime = 0;
            int nAsyncDate = 0;
            int nNoResp = 0;
            int nNoCon = 0;

            // when dates are not synchronized, return without testing time values
            for (int i = 0; i < DATELOOP; i++)
            {
                status = CheckDateSynchro();
                if (status == BevZzgStatus.Synchron) nSync++;
                if (status == BevZzgStatus.DateAsync) nAsyncDate++;
            }
            // here we use a more simple categorization
            if(nSync==0)
            {
                if (nAsyncDate != 0)
                    return BevZzgStatus.DateAsync;
                else
                    return BevZzgStatus.Unspecified;
            }

            // loop CheckTimeSynchro() many times and categorize the return values
            nSync = 0;
            nAsyncDate = 0;
            for (int i = 0; i < TIMELOOP; i++)
            {
                status = CheckTimeSynchro();
                if (status == BevZzgStatus.Synchron)
                    nSync++;
                if (status == BevZzgStatus.TimeAsync)
                    nAsyncTime++;
                if ((status & BevZzgStatus.SysTimeChanged) == BevZzgStatus.SysTimeChanged)
                    nSysTimeChanged++;
                if ((status & BevZzgStatus.NoResponse) == BevZzgStatus.NoResponse)
                    nNoResp++;
                if (status == BevZzgStatus.NotConnected)
                    nNoCon++;
            }

            var result = new ZzgDecision(nSync, nAsyncTime, nSysTimeChanged, nNoResp, TIMELOOP);
            return result.Status;
        }

        public bool Connect()
        {
            if (isConnected)
                return false;
            try
            {
                comPort = new SerialPort(comPortName, 4800, Parity.None, 8, StopBits.One);
                // the following settings are not documented -> subject to experiments
                comPort.Handshake = Handshake.None; //TODO
                comPort.ReadTimeout = 100;
                comPort.WriteTimeout = 100;
                comPort.RtsEnable = false; //TODO
                comPort.DtrEnable = false; //TODO
                comPort.Open();
                isConnected = true;
                if (QueryAB(5) == "") Disconnect(); // check on senseful reply
                return true;
            }
            catch (Exception)
            {
                isConnected = false;
                return false;
            }
        }
 
        public bool Disconnect()
        {
            if (!isConnected)
                return false;
            if (comPort.IsOpen) 
            {
                comPort.Close();
                isConnected = false;
                return true;
            }
            isConnected = false;
            return false;
        }

        private bool SendCommand(string sCommand)
        {
            if (!isConnected) return false;
            comPort.DiscardInBuffer();
            sCommand += "\r";
            byte[] cmd = Encoding.ASCII.GetBytes(sCommand);
            try
            {
                comPort.Write(cmd, 0, cmd.Length);
            }
            catch (Exception)
            {
                return false;
            }
            Thread.Sleep(DELAY);
            return true;
        }

        private string ReadLine()
        {
            if (!isConnected) return "";
            string temp = "";
            try
                { temp = comPort.ReadLine(); }
            catch(Exception)
                { return temp; }
            temp = temp.Replace("\n", "");
            temp = temp.Replace("\r", "");
            return temp;
        }

        /// <summary>
        /// Checks if the ZZG time is synchronous with system time.
        /// </summary>
        /// <returns>Synchronization status.</returns>
        private BevZzgStatus CheckTimeSynchro()
        {
            if (!isConnected)
                return BevZzgStatus.NotConnected;
            
            string sysTimeBegin = DateTime.UtcNow.ToString("HHmmss");
            string zzgTime = QueryTime();
            string sysTimeEnd = DateTime.UtcNow.ToString("HHmmss");

            BevZzgStatus ret = BevZzgStatus.Synchron;
            if (sysTimeBegin!=sysTimeEnd)
                ret = ret | BevZzgStatus.SysTimeChanged;
            if (zzgTime == "")
                ret = ret | BevZzgStatus.NoResponse;
            if (ret != BevZzgStatus.Synchron)
                return ret;
            if (zzgTime != sysTimeBegin)
                ret = ret | BevZzgStatus.TimeAsync;
            return ret; // is BevZzgStatus.Synchron here
        }

        /// <summary>
        /// Checks if the ZZG date is synchronous with system date.
        /// </summary>
        /// <returns>Synchronization status.</returns>
        private BevZzgStatus CheckDateSynchro()
        {
            if (!isConnected)
                return BevZzgStatus.NotConnected;

            string sysDateBegin = DateTime.UtcNow.ToString("ddMMyy");
            string zzgDate = QueryDate(5); // call at most 5 times 
            string sysDateEnd = DateTime.UtcNow.ToString("ddMMyy");

            BevZzgStatus ret = BevZzgStatus.Synchron;
            if (sysDateBegin != sysDateEnd)
                ret = ret | BevZzgStatus.SysTimeChanged;
            if (zzgDate == "")
                ret = ret | BevZzgStatus.NoResponse;
            if (ret != BevZzgStatus.Synchron)
                return ret;
            if (zzgDate != sysDateBegin)
                ret = ret | BevZzgStatus.DateAsync;
            return ret; // is BevZzgStatus.Synchron here
        }

        public override string ToString() => $"[{instrumentName}@{comPortName}]";

    }
}

