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
        #region Fields
        private static int DELAY = 5;       // Delay time in ms, between sending and reading
        private bool bConnected;            // true if connection to ZZG ok
        private string sComPort;            // name of the COM port
        private string sName;               // designation of instrument (user supplied)
        private SerialPort portCOM;         // the com port object
        #endregion

        #region Properties

        /// <summary>
        /// Gets a bool value indicating whether this instrument is connected.
        /// </summary>
        /// <value><c>true</c> if the instrument is connected; otherwise, <c>false</c>.</value>
        public bool IsConnected { get { return bConnected; } }

        /// <summary>
        /// Gets the COM port supplied in the constructor.
        /// </summary>
        /// <value>The COM port name.</value>
        public string ComPort { get { return sComPort; } }

        /// <summary>
        /// Gets the name of the instrument.
        /// </summary>
        public string InstrumentName { get { return sName; } }

        #endregion

        #region Ctor

        /// <summary>
        /// Initializes a new instance of the <see cref="Bev.Zzg.BevZzg"/> class.
        /// </summary>
        /// <remarks>It has a very restricted functionality. Instrument is not connected.</remarks>
        /// <param name="sCom">The name of the COM port.</param>
        public BevZzg(string sCom, string name)
        {
            sName = name.Trim();
            if (sName == "") sName = "<unknown>";
            sComPort = sCom.ToUpper().Trim();
            bConnected = false;
            Connect();
        }
        public BevZzg(string sCom) : this(sCom, "")
        { }
        
        #endregion

        #region Public methods - Query instrument

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

        #endregion

        #region Public methods - Query instrument until success 

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

        #endregion

        #region Public methods - Set instrument parameters and setting

        /// <summary>
        /// Sets the ZZG's time. The supplied string must be of the correct format.
        /// </summary>
        /// <returns><c>true</c>, if time was set, <c>false</c> otherwise.</returns>
        /// <param name="str">String in HHmmss format.</param>
        /// <remarks>Synchronisation problem!</remarks>
        public bool SetTime(string str)
        {
            if (str.Length != 6)    // primitive syntax check
                return false;
            return SendCommand("U="+str);
        }

        /// <summary>
        /// Sets the ZZG's time to system time.
        /// </summary>
        /// <returns><c>true</c>, if time was set, <c>false</c> otherwise.</returns>
        public bool SetTime()
        {
            return SetTime(DateTime.UtcNow.ToString("HHmmss"));
        }

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
        public bool SetDate()
        {
            return SetDate(DateTime.UtcNow.ToString("ddMMyy"));
        }

        #endregion

        #region Public methods - high level synchro check
        public BevZzgStatus CheckSynchro()
        {
            Connect();

            if (!bConnected)
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

        #endregion

        #region Public methods - Connect/Disconnect

        public bool Connect()
        {
            if (bConnected)
                return false;
            try
            {
                portCOM = new SerialPort(sComPort, 4800, Parity.None, 8, StopBits.One);
                // the following settings are not documented -> subject to experiments
                portCOM.Handshake = Handshake.None; //TODO
                portCOM.ReadTimeout = 100;
                portCOM.WriteTimeout = 100;
                portCOM.RtsEnable = false; //TODO
                portCOM.DtrEnable = false; //TODO
                portCOM.Open();
                bConnected = true;
                if (QueryAB(5) == "") Disconnect(); // check on senseful reply
                return true;
            }
            catch (Exception)
            {
                bConnected = false;
                return false;
            }
        }
 
        public bool Disconnect()
        {
            if (!bConnected)
                return false;
            if (portCOM.IsOpen) 
            {
                portCOM.Close();
                bConnected = false;
                return true;
            }
            bConnected = false;
            return false;
        }

        #endregion

        #region Private methods - basics

        /// <summary>
        /// Sends a command (string) to the instrument via RS232. Adds a carriage return to the string.
        /// </summary>
        /// <returns><c>true</c>, if successful, <c>false</c> otherwise.</returns>
        /// <param name="sCommand">A command as string.</param>
        private bool SendCommand(string sCommand)
        {
            if (!bConnected) return false;
            portCOM.DiscardInBuffer();
            sCommand += "\r";
            byte[] cmd = Encoding.ASCII.GetBytes(sCommand);
            try
            {
                portCOM.Write(cmd, 0, cmd.Length);
            }
            catch (Exception)
            {
                return false;
            }
            Thread.Sleep(DELAY);
            return true;
        }

        //private string ReadLine()
        //{
        //    string temp = "";
        //    int i = 0;
        //    while (!temp.Contains("\n") && i < MAXLOOP)
        //    {
        //        temp = ReadBytes();
        //        i++;
        //        Thread.Sleep(DELAY);
        //    }
        //    temp = temp.Replace("\n", "");
        //    temp = temp.Replace("\r", "");
        //    return temp;
        //}

        /// <summary>
        /// Reads a line from the instrument. Usually a reply to a sent command.
        /// </summary>
        /// <returns>The read string, empty if not successful.</returns>
        private string ReadLine()
        {
            if (!bConnected) return "";
            string temp = "";
            try
                { temp = portCOM.ReadLine(); }
            catch(Exception)
                { return temp; }
            temp = temp.Replace("\n", "");
            temp = temp.Replace("\r", "");
            return temp;
        }

        /// <summary>
        /// Receives a character array from the instrument. Converted tu a string.
        /// </summary>
        /// <remarks>Can be an empty string even when working.</remarks>
        /// <returns>The string received.</returns>
        private string ReadBytes()
        {
            if (!bConnected) return "";
            byte[] buffer = new byte[portCOM.BytesToRead];
            try
            {
                portCOM.Read(buffer, 0, buffer.Length);
                return Encoding.UTF8.GetString(buffer);
            }
            catch (Exception)
            {
                return "";
            }
        }

        #endregion

        #region Private methods - high level

        /// <summary>
        /// Checks if the ZZG time is synchronous with system time.
        /// </summary>
        /// <returns>Synchronization status.</returns>
        private BevZzgStatus CheckTimeSynchro()
        {
            if (!bConnected)
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
            if (!bConnected)
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

        #endregion

        public override string ToString()
        {
            return string.Format("[{0}@{1}]", sName, sComPort);
        }

    }
}

