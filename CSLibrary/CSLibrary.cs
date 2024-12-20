/*
Copyright (c) 2023 Convergence Systems Limited

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:
The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
*/

using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;
using System.Threading;
using System.Runtime.CompilerServices;

using System.Net;
using System.Net.Sockets;

using System.IO;
using System.IO.Ports;

using CSLibrary;
using CSLibrary.Constants;
using CSLibrary.Structures;
using CSLibrary.Events;
using CSLibrary.Tools;
using CSLibrary.Text;

namespace CSLibrary
{
    /// <summary>
    /// Reader HighLevelInterface
    /// </summary>
    public sealed partial class HighLevelInterface : IDisposable
    {
        #region ====================== Fixed Variable ======================
        private const int BYTES_PER_LEN_UNIT = 4;

        private const uint INVALID_POWER_VALUE = uint.MaxValue;
        private const uint INVALID_PROFILE_VALUE = uint.MaxValue;
        private const int DATA_FIELD_INDEX = 20;
        private const int RSSI_FIELD_INDEX = 12;
        private const int ANT_FIELD_INDEX = 14;
        private const int MS_FIELD_INDEX = 8;
        private const int RFID_PACKET_COMMON_SIZE = 8;

        private const ushort PC_START_OFFSET = 1;
        private const ushort PC_WORD_LENGTH = 1;
        private const ushort EPC_START_OFFSET = 2;
        private const ushort EPC_WORD_LENGTH = 6;
        private const ushort ACC_PWD_START_OFFSET = 2;
        private const ushort ACC_PWD_WORD_LENGTH = 2;
        private const ushort KILL_PWD_START_OFFSET = 0;
        private const ushort KILL_PWD_WORD_LENGTH = 2;
        private const ushort ONE_WORD_LEN = 1;
        private const ushort TWO_WORD_LEN = 2;

        private const ushort USER_WORD_LENGTH = 1;
        private const uint MAXFRECHANNEL = 50;

//        private const uint SelectFlags.SELECT     = 0x00000001;
//        private const uint RFID_FLAG_PERFORM_POST_MATCH = 0x00000002;
//	    private const uint RFID_FLAG_DISABLE_INVENTORY  = 0x00000004;

        #endregion

        #region ====================== Private Variable ======================

        private CSLibrary.Structures.Version hardwareVersion = new CSLibrary.Structures.Version();
        private LinkProfileInfoList m_linkProfileList = null;
        private CSLibraryOperationParms m_rdr_opt_parms = new CSLibraryOperationParms();
        private object syncRoot = new Object();
        private RFState m_state = RFState.IDLE;
        private Result m_Result = Result.OK;

        private GPIOTrigger m_GPI0InterruptTrigger = GPIOTrigger.OFF;
        private GPIOTrigger m_GPI1InterruptTrigger = GPIOTrigger.OFF;

        private Thread g_hWndThread = null;

        private bool bShutdownRequired = false;
        private int bStop = 0;
        private bool bDataMode = false;
        // Track whether Dispose has been called.
        private bool disposed = false;

        private TagAccessCallbackDelegate accessCallback = null;
        private InventoryCallbackDelegate inventoryCallback = null;

        private Operation   CurrentOperation;
        private Result      CurrentOperationResult;

        private bool m_TCPNotification = false;


        /// <summary>
        /// Current Path
        /// </summary>
        /// 
#if !WindowsCE
        public readonly string CurrentPath = System.IO.Path.GetDirectoryName(typeof(HighLevelInterface).Assembly.Location);
#else
        public readonly string CurrentPath = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().GetName().CodeBase);
#endif

#if DEBUG
        CSLibrary.Tools.Counter TimerCnt = new CSLibrary.Tools.Counter();
#endif
        #region Save Parmeters
        private List<RegionCode> m_save_country_list = new List<RegionCode>();
        private RegionCode m_save_region_code = RegionCode.UNKNOWN;
        private ResponseMode m_save_resp_mode = ResponseMode.COMPACT;
        private TagGroup m_save_taggroup = new TagGroup(Selected.ALL, Session.S0, SessionTarget.A);
        private RadioOperationMode m_save_oper_mode = RadioOperationMode.NONCONTINUOUS;
        private SingulationAlgorithm m_save_singulation = SingulationAlgorithm.DYNAMICQ;
        private SingulationAlgorithmParms m_save_SingulationAlg = null;
        private LBT m_save_enable_lbt = LBT.OFF;
        private uint m_save_freq_channel = 2;
        private uint m_save_country_code = 0;
        private uint m_save_link_profile = 2;
        private uint m_save_power_level = 300;
        private double m_save_selected_freq = 0;
        private uint m_save_inventory_cycle = 0;
        private uint m_save_inventory_duration = 0;
        private bool m_save_fixed_channel = false;
        private bool m_save_agile_channel = false;
        private bool m_save_blocking_mode = false;
        private bool m_save_extern_lo = false;
        private uint m_save_antenna_port = 0;
        private string m_save_readerName = "CSL RFID Reader";
        private List<S_EPC> m_sorted_epc_records = new List<S_EPC>();
        private RadioOperationMode m_save_antenna_cycle = RadioOperationMode.NONCONTINUOUS;
        private AntennaSequenceMode m_save_antenna_cycle_sequence_mode = AntennaSequenceMode.NORMAL; /*AntennaSequenceMode.UNKNOWN*/
private int m_save_antenna_cycle_sequence_size = 0;
        private uint[] currentInventoryFreqRevIndex = null;
        private int m_save_rflna_high_comp = 1;
        private int m_save_rflna_gain = 1;
        private int m_save_iflna_gain = 24;
        private int m_save_ifagc_gain = -6;

        /// <summary>
        /// OEM value
        /// </summary>
        private Machine m_oem_machine = Machine.UNKNOWN;
        private uint m_oem_hipower = 0;
        private uint m_oem_maxpower = 300;
        private uint m_oem_freq_modification_flag = 0x00;
        private uint m_oem_special_country_version = 0x00;
        private uint m_oem_table_version;

        /// <summary>
        /// current MacErrorCode
        /// </summary>
        public uint MacErrorCode = 0;

        AntennaList m_AntennaList = null;

        public AntennaList AntennaList
        {
            get { lock (syncRoot) return m_AntennaList; }
            set { lock (syncRoot) m_AntennaList = value; }
        }

        //public byte[] AntennaPortSequence { get; set; }
        //public AntennaSequenceMode AntennaSequenceMode { get; set; }
        //public uint AntennaSequenceSize { get; set; }

        AntennaSequenceMode m_mode = AntennaSequenceMode.NORMAL;
        public AntennaSequenceMode AntennaSequenceMode
        {
            get { return m_mode; }
            set { m_mode = value; }
        }

        uint m_sequence_size = 0;
        /// <summary>
        /// Antenna sequence size for SEQUENCE MODE
        /// </summary>
        public uint AntennaSequenceSize
        {
            get { lock (syncRoot) return m_sequence_size; }
            set { lock (syncRoot) m_sequence_size = value; }
        }

        byte[] m_sequence_list = new byte[48];
        /// <summary>
        /// Antenna list
        /// </summary>
        public byte[] AntennaPortSequence
        {
            get { lock (syncRoot) return m_sequence_list; }
            set { lock (syncRoot) m_sequence_list = value; }
        }

        #endregion
#if TEMPLOG
        private const string TmpLogFile = "TempLogging.txt";
#endif

        #endregion
        
        #region ====================== Extern Variable ======================

        public UInt32 LastMacErrorCode;
        public UInt16 ErrorPort;

        internal UInt32 _EngineeringTest_Operation = 0;
        public UInt32 _Debug_Inventory_Round_Begin_Diagnostics_Atmel_Time = 0;
        public UInt32 _Debug_Inventory_Round_End_Diagnostics_Atmel_Time = 0;
        public UInt32 _Debug_Inventory_Round_End_Diagnostics_EPC_successfully_read = 0;
        public UInt32 _Debug_Inventory_Round_End_Diagnostics_RN16 = 0;
        public UInt32 _Debug_Inventory_Round_End_Diagnostics_RN16_timeout = 0;
        public UInt32 _Debug_Inventory_Round_End_Diagnostics_EPC_timeout = 0;
        public UInt32 _Debug_Inventory_Round_End_Diagnostics_CRC = 0;

        public UInt32 _Debug_Inventory_Cycle_End_Diagnostics_EPC_RX = 0;
        public UInt32 _Debug_Inventory_Cycle_End_Diagnostics_RN16 = 0;

        public UInt32 _Debug_Carrier_Info_Atmel_Time = 0;
        public UInt32 _Debug_Carrier_Info_PLLDIVMULT = 0;
        public UInt16 _Debug_Carrier_Info_CW_STATE = 0;
        public Single _Debug_EngineeringTest_RSSI;
        public byte _Debug_EngineeringTest_RSSI1;
        public Single _Debug_EngineeringTest_RSSI2;
        public byte _Debug_EngineeringTest_RSSI3;


        /// <summary>
        /// Get Communication Interface Type
        /// </summary>
        public INTERFACETYPE CurrentInterfaceType
        {
            get { lock (syncRoot) { return m_DeviceInterfaceType; } }
        }
        
        /// <summary>
        /// Get Current Freq Channel Index
        /// </summary>
        public UInt16 CurrentFreqChannelIndex
        {
            get { lock (syncRoot) { return m_CurrentFreqChannelIndex; } }
        }

        /// <summary>
        /// Get Reader connection name
        /// </summary>
        public string DeviceNameOrIP
        {
            get
            {
                lock (syncRoot)
                {
                    return m_DeviceName;
                }
            }
        }

        /// <summary>
        /// Get IP Address
        /// </summary>
        public string IPAddress
        {
            get { lock (syncRoot) {
                if (ReaderStatus == 1 && m_DeviceInterfaceType == INTERFACETYPE.IPV4)
                    return hostIP.ToString();
                else
                    return "0.0.0.0";
            } }
        }
        /// <summary>
        /// Get MAC Address
        /// </summary>
        public string MacAddress
        {
            get { lock (syncRoot) return m_macAddress; }
        }

#if nouse
        /// <summary>
        /// Reconnect Timeout
        /// </summary>
        public uint ReconnectTimeout
        {
            get { lock (syncRoot) { return m_save_tcp_timeout; } }
            set { lock (syncRoot) { m_save_tcp_timeout = value; } }
        }
#endif
        /// <summary>
        /// Reconnect Timeout
        /// </summary>
        public uint ReconnectTimeout
        {
            get { lock (syncRoot) { return m_ConnectionTimeOut; } }
            set { lock (syncRoot) { m_ConnectionTimeOut = value; } }
        }

        /// <summary>
        /// Get or Set Reader Name
        /// </summary>
        public string Name
        {
            get { lock (syncRoot) { return m_save_readerName; } }
            set { lock (syncRoot) { m_save_readerName = value; } }
        }
        /// <summary>
        /// CSLibrary Operation parameters
        /// Notes : you must config this parameters before perform any operation
        /// </summary>
        public CSLibraryOperationParms Options
        {
            get { lock (syncRoot) { return m_rdr_opt_parms; } }
            set { lock (syncRoot) { m_rdr_opt_parms = value; } }
        }

        /// <summary>
        /// Get current power level
        /// </summary>
        public uint SelectedPowerLevel
        {
            get { lock (syncRoot) { return m_save_power_level; } }
        }
        /// <summary>
        /// Get Current Selected Frequency Channel
        /// </summary>
        public uint SelectedChannel
        {
            get { lock (syncRoot) { return m_save_freq_channel; } }
        }
        /// <summary>
        /// Get Region Profile
        /// </summary>
        public RegionCode SelectedRegionCode
        {
            get { lock (syncRoot) { return m_save_region_code; } }
        }
        /// <summary>
        /// Current selected frequency
        /// </summary>
        public uint SelectedLinkProfile
        {
            get { lock (syncRoot) { return m_save_link_profile; } }
        }
        /// <summary>
        /// Get current frequency 
        /// </summary>
        public double SelectedFrequencyBand
        {
            get { lock (syncRoot) { return m_save_selected_freq; } }
        }
        /// <summary>
        /// Get Current LBT status
        /// </summary>
        public LBT LBT_ON
        {
            get { lock (syncRoot) { return m_save_enable_lbt; } }
        }
        /// <summary>
        /// get last function return code
        /// </summary>
        public Result LastResultCode
        {
            get { lock (syncRoot) { return m_Result; } }
        }
        /// <summary>
        /// get last function return error messsage
        /// </summary>
        public string LastErrorMessage
        {
            get
            {
                switch (LastResultCode)
                {
                    case Result.OK:
                        return "Success";

                    case Result.ALREADY_OPEN:
                        return "Radio is already opened";

                    case Result.BUFFER_TOO_SMALL:
                        return "Buffer is too small";

                    case Result.CURRENTLY_NOT_ALLOWED:
                        return "RFID function is not currently allowed";

                    case Result.DRIVER_LOAD:
                        return "Failed to load radio driver";

                    case Result.DRIVER_MISMATCH:
                        return "Driver version does not match required version";

                    case Result.EMULATION_MODE:
                        return "Cannot call this function in emulation mode";

                    case Result.FAILURE:
                        return "General failure";

                    case Result.INVALID_ANTENNA:
                        return "Antenna port is not valid";

                    case Result.INVALID_HANDLE:
                        return "Radio handle is not valid";

                    case Result.INVALID_PARAMETER:
                        return "Parameter is not valid";

                    case Result.MAX_RETRY_EXIT:
                        return "Tag access maximum retry excess";

                    case Result.NETWORK_LOST:
                    case Result.NETWORK_RESET:
                        return "Network is reset or lost connection";

                    case Result.NO_SUCH_RADIO:
                        return "Radio with supplied ID is not attached to the system";

                    case Result.NOT_INITIALIZED:
                        return "RFID library has not been previously initialized";

                    case Result.NOT_SUPPORTED:
                        return "Function is currently not supported";

                    case Result.OPERATION_CANCELLED:
                        return "The operation was cancelled";

                    case Result.OUT_OF_MEMORY:
                        return "RFID library failed to allocate memory";

                    case Result.POWER_DOWN_FAIL:
                        return "RFID power down failed";

                    case Result.POWER_UP_FAIL:
                        return "RFID power up failed";

                    case Result.RADIO_BUSY:
                        return "Operation cannot be performed because radio is busy";

                    case Result.RADIO_FAILURE:
                        return "The radio module indicated a failure";

                    case Result.RADIO_NOT_PRESENT:
                        return "The radio has been detached from the system";

                    case Result.RADIO_NOT_RESPONDING:
                        return "The radio is not responding";

                    case Result.NONVOLATILE_INIT_FAILED:
                        return "The radio failed to initialize nonvolatile memory update";

                    case Result.NONVOLATILE_OUT_OF_BOUNDS:
                        return "Nonvolatile memory address is out of range";

                    case Result.NONVOLATILE_WRITE_FAILED:
                        return "The radio failed to write to nonvolatile memory";

                    case Result.SYSTEM_CATCH_EXCEPTION:
                        return "Exception catch..";

                    default:
                        return "Unknown error";
                }
            }
        }

        /// <summary>
        /// If true, it can only set to hopping channel.
        /// </summary>
        public bool IsHoppingChannelOnly
        {
            get { return m_oem_freq_modification_flag != 0x00; }
        }
        /// <summary>
        /// If true, it can only set to fixed channel.
        /// Otherwise, both fixed and hopping can be set.
        /// </summary>
        public bool IsFixedChannelOnly
        {
            get { return (m_save_country_code == 1 || m_save_country_code == 3 || m_save_country_code == 8); }
        }
        /// <summary>
        /// Get Fixed frequency channel
        /// </summary>
        public bool IsFixedChannel
        {
            get { lock (syncRoot) { return m_save_fixed_channel; } }
        }
        /// <summary>
        /// Current Operation State
        /// </summary>
        public RFState State
        {
            get { lock (syncRoot) { return m_state; } }
            private set { lock (syncRoot) { m_state = value; } }
        }

        public Machine OEMDeviceType
        {
            get
            {
                return m_oem_machine;
            }
        }

        public ChipSetID OEMChipSetID
        {
            get
            {
                switch (m_oem_machine)
                {
                    case Machine.CS103:
                    case Machine.CS108:
                    case Machine.CS203X:
                    case Machine.CS206:
                    case Machine.CS209:
                    case Machine.CS333:
                    case Machine.CS463:
                    case Machine.CS468XJ:
                    case Machine.CS468X:
                        return ChipSetID.R2000;
                }

                return ChipSetID.R1000;
            }
        }

        public uint OEMCountryCode
        {
            get
            {
                return m_save_country_code;
            }
        }

        public uint OEMSpecialCountryVersion
        {
            get
            {
                return m_oem_special_country_version;
            }
        }

        public uint OEMHiPower
        {
            get
            {
                return m_oem_hipower;
            }
        }

        public uint OEMMaxPower
        {
            get
            {
                return m_oem_maxpower;
            }
        }

        public bool OEMFreqModifiedFlag
        {
            get
            {
                return (m_oem_freq_modification_flag == 0x00);
            }
        }

        private Machine GetOEMDeviceType
        {
            get
            {
                uint dataBuf = 0xff;

                m_Result = MacReadOemData(0xa4, ref dataBuf);
                if (m_Result != Result.OK)
                    return Machine.UNKNOWN;

                if (dataBuf >= (uint)Machine.MACHINE_CODE_END)
                    return Machine.UNKNOWN;

                return (Machine)dataBuf;
            }
        }

        private uint GetOEMCountryCode
        {
            get
            {
                uint dataBuf = 0xff;

                m_Result = MacReadOemData(0x2, ref dataBuf);
                if (m_Result != Result.OK)
                    return 0;

                return dataBuf;
            }
        }

        private uint GetOEMHiPower
        {
            get
            {
                uint maxPower = 0xff;

                m_Result = MacReadOemData(0xa3, ref maxPower);
                if (m_Result != Result.OK)
                    return 0;

                return maxPower;
            }
        }

        private uint GetOEMMaxPower
        {
            get
            {
                uint dataBuf = 0xff;

                m_Result = MacReadOemData(0xa6, ref dataBuf);
                if (m_Result != Result.OK)
                    return 0;

                // for Old Module
                if (dataBuf == 0)
                {
                    if (m_oem_machine == Machine.CS101 && m_save_country_code == 1)
                        return 275;

                    return 300;
                }

                return dataBuf;
            }
        }

        private uint GetOEMFreqModificationFlag
        {
            get
            {
                uint dataBuf = 0xff;

                m_Result = MacReadOemData(0x8F, ref dataBuf);
                if (m_Result != Result.OK)
                    return 0xaa;

                return dataBuf;
            }
        }

        private uint GetOEMSpecialCountryVersion
        {
            get
            {
                uint dataBuf = 0xff;
                m_Result = MacReadOemData(0x8E, ref dataBuf);
                return dataBuf;
            }
        }

        private uint GetOEMTableVersion
        {
            get
            {
                uint dataBuf = 0xff;
                m_Result = MacReadOemData(0x0B, ref dataBuf);
                return dataBuf;
            }
        }


        #region ====================== Channel Busy Status ======================
        public int LBTChannelBusy;
        public int LBTChannelClear;
        public RFState[] ChannelStatus = new RFState[16];
        #endregion

        #endregion

        #region ====================== Callback Event Handler ======================
        /// <summary>
        /// Reader Operation State Event
        /// </summary>
        public event EventHandler<CSLibrary.Events.OnStateChangedEventArgs> OnStateChanged;

        /// <summary>
        /// Tag Inventory(including Inventory and search) callback event
        /// </summary>
        public event EventHandler<CSLibrary.Events.OnAsyncCallbackEventArgs> OnAsyncCallback;
        
        /// <summary>
        /// Tag Access (including Tag read/write/kill/lock) completed event
        /// </summary>
        public event EventHandler<CSLibrary.Events.OnAccessCompletedEventArgs> OnAccessCompleted;

        /// <summary>
        /// Firmware Upgrade Status
        /// </summary>
        public event EventHandler<CSLibrary.Events.OnFirmwareUpgradeEventArgs> OnFirmwareUpgrade;

        #endregion

        #region ====================== Constructor ======================
        /// <summary>
        /// Default Constructor without debugger
        /// </summary>
        public HighLevelInterface()
        {
        }
        
        /// <summary>
        /// Destructor
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            // This object will be cleaned up by the Dispose method.
            // Therefore, you should call GC.SupressFinalize to
            // take this object off the finalization queue
            // and prevent finalization code for this object
            // from executing a second time.
            GC.SuppressFinalize(this);
        }

        // Dispose(bool disposing) executes in two distinct scenarios.
        // If disposing equals true, the method has been called directly
        // or indirectly by a user's code. Managed and unmanaged resources
        // can be disposed.
        // If disposing equals false, the method has been called by the
        // runtime from inside the finalizer and you should not reference
        // other objects. Only unmanaged resources can be disposed.
        private void Dispose(bool disposing)
        {
            // Check to see if Dispose has already been called.
            if (!this.disposed)
            {
                // If disposing equals true, dispose all managed
                // and unmanaged resources.
                if (disposing)
                {
                    // Dispose managed resources.
                }

#if GPIOINTERRUPT
                if (pollGPIHandle != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(pollGPIHandle);
                    pollGPIHandle = IntPtr.Zero;
                }
#endif
                // Call the appropriate methods to clean up
                // unmanaged resources here.
                // If disposing is false,
                // only the following code is executed.

                // Note disposing has been done.
                disposed = true;

            }

            if (m_AntennaList != null)
            {
                m_AntennaList.Clear();
                m_AntennaList = null;
            }
        }
        /// <summary>
        /// Destructor
        /// </summary>
        ~HighLevelInterface()
        {
            Dispose(false);
        }

        #endregion

        #region ====================== Structure ======================
        /// <summary>
        /// Communication interface
        /// </summary>
        public enum INTERFACETYPE
        {
            /// <summary>
            /// Unknown
            /// </summary>
            UNKNOWN,

            /// <summary>
            /// IPv4 (Enther Net)
            /// </summary>
            IPV4,

            /// <summary>
            /// USB
            /// </summary>
            USB,

            /// <summary>
            /// Serial Port
            /// </summary>
            SERIAL,
        }

        #endregion

        #region ====================== Startup ======================

        enum OSVERSION
        {
            WIN32,
            WINCE,
            MONO,
            UNKNOWN
        }

        private static OSVERSION GetOSVersion()
        {
            switch (Environment.OSVersion.Platform)
            {
                case PlatformID.Win32NT:
                case PlatformID.Win32S:
                case PlatformID.Win32Windows:
                    return OSVERSION.WIN32;

                case PlatformID.WinCE:
                    return OSVERSION.WINCE;

                default:
                    switch ((int)Environment.OSVersion.Platform)
                    {
                        case 5: /* Xbox */
                            return OSVERSION.WIN32;
                        
                        case 4: /* Unix */ 
                        case 6: /* MacOSX */
                        case 128:
                            return OSVERSION.MONO;
                    }

                    return OSVERSION.UNKNOWN;
            }
        }

        public void SetTCPNotificationEnable(bool enable)
        {
            m_TCPNotification = enable;
        }

        /// <summary>
        /// Connect CS101 intenal reader and allocate resources
        /// (only for CS101 internal reader)
        /// </summary>
        /// <returns>Result</returns>
        public Result Connect()
        {
            if (GetOSVersion () != OSVERSION.WINCE)
                return Result.NOT_SUPPORTED;

            if (RFID_PowerOnOff(1) != true)// Power Off RFID Reader
                return Result.NOT_SUPPORTED;

            System.Threading.Thread.Sleep(100);

            if (RFID_PowerOnOff(0) != true)// Power On RFID Reader
                return Result.POWER_UP_FAIL;

            System.Threading.Thread.Sleep(1400); // 1000-1300

            m_oem_machine = Machine.CS101; // Set default to CS101
            return Connect("USB", 0);
        }


/*        /// <summary>
        /// Allows the RFID Reader Library to properly initialize any internal data structures 
        /// and put itself into a well-known ready state. 
        /// This function must be called before any other RFID Reader Library functions. 
        /// If the RFID Reader Library has already been initialized, additional calls to RFID_Startup have no effect.
        ///
        /// Note: This function is for backward compatibility only and will be deprecated in future version
        /// Please use Startup(string ipAddress, int timeout) instead
        /// </summary>*/
        /// <summary>
        /// Face out command, please use Connect(string ipAddress, int timeout);
        /// </summary>
        /// <param name="ipAddress"></param>
        /// <param name="timeout"></param>
        /// <param name="libraryDebug"></param>
        /// <returns></returns>
        public Result Connect(string ipAddress, int timeout, bool libraryDebug)
        {
//            LibraryDebugMode = libraryDebug;
            return Connect(ipAddress, (uint)timeout);
        }


        /// <summary>
        /// Connect to reader and allocate resources
        /// </summary>
        /// <param name="DeviceName">Destination Device Name</param>
        /// <value="x.x.x.x">Destination IP Address</value>
        /// <value="USB">Destination First USB Reader</value>
        /// <value="USB<sn>">Destination specified USB Reader</value>
        /// <value="COM<x>">Destination specified Serial Reader</value>
        /// <param name="TimeOut">Connection Timeout</param>
        /// <returns></returns>
        public Result Connect(string DeviceName, uint TimeOut)
        {
            DateTime ConnectionTimeout = DateTime.Now.AddMilliseconds (TimeOut);

            if (bShutdownRequired)
                return Result.ALREADY_OPEN;

            m_ConnectionTimeOut = TimeOut;
            m_linkProfileList = new LinkProfileInfoList();

            m_Result = Result.FAILURE;
            do
            {
                try
                {
                    if (!COMM_Connect(DeviceName))
                    {
                        //m_Result = Result.NETWORK_LOST;
                        continue;
                    }

                    bShutdownRequired = true;

                    m_Result = MacClearError();

                    if (m_Result !=  Result.OK)
                        continue;


                    uint vvv = 0;

                    /*
                    for (uint cnt = 0; cnt < 16; cnt++)
                    {
                        MacWriteRegister((MacRegister)  0x701, cnt);
                        Console.WriteLine("0x701:" + cnt.ToString("X4"));
                        MacReadRegister((MacRegister)0x702, ref vvv);
                        Console.WriteLine("0x702:" + vvv.ToString("X4"));
                        MacReadRegister((MacRegister)0x703, ref vvv);
                        Console.WriteLine("0x703:" + vvv.ToString("X4"));
                        MacReadRegister((MacRegister)0x704, ref vvv);
                        Console.WriteLine("0x704:" + vvv.ToString("X4"));
                        MacReadRegister((MacRegister)0x705, ref vvv);
                        Console.WriteLine("0x705:" + vvv.ToString("X4"));
                        MacReadRegister((MacRegister)0x706, ref vvv);
                        Console.WriteLine("0x706:" + vvv.ToString("X4"));
                        MacReadRegister((MacRegister)0x707, ref vvv);
                        Console.WriteLine("0x707:" + vvv.ToString("X4"));
                    }
                    */
                    
                    // Get OEM data
                    // 0x0000 : date
                    // 0x0002 : Region Code
                    // 0x00a0 : Get Device Interface // no use
                    // 0x00a3 : Get API Mode
                    // 0x00a4 : Get Device Type
                    // 0x00a5 : Max Out Power
                    // 0x00a6 : Max Traget Power

                    m_oem_machine = GetOEMDeviceType;          // First OEM Action
                    m_save_country_code = GetOEMCountryCode;   // Second OEM Action
                    m_oem_hipower = GetOEMHiPower;
                    m_oem_maxpower = GetOEMMaxPower; // last OEM action
                    m_save_readerName = m_oem_machine.ToString() + " RFID Reader";
                    m_oem_freq_modification_flag = GetOEMFreqModificationFlag;
                    m_oem_special_country_version = GetOEMSpecialCountryVersion;
                    m_oem_table_version = GetOEMTableVersion;

                    /*
                     * HST_TAGACC_DESC_CFG 
                     * 0        Verify           Verify after write  
                     * 8:1     Retry 
                     * Number of time to retry the write if the verify failed 
                     * 31:9   Reserved      reserved - read / write as zero */
                    ThrowException(MacWriteRegister(MacRegister.HST_TAGACC_DESC_CFG  /*0xA01*/, 0x1ff));

                    //Gen Country List
                    GenCountryList();

                    for (int cnt = 0; cnt < 16; cnt++)
                        ChannelStatus[cnt] = RFState.UNKNOWN;

                    //DO INITIAL HERE
                    LoadDefaultSetting(true);

                    FireStateChangedEvent(RFState.IDLE);
                }
                catch (ReaderException ex)
                {
                    Disconnect();
                    m_Result = ex.ErrorCode;
                }
                catch (System.Exception ex)
                {
                    m_Result = Result.SYSTEM_CATCH_EXCEPTION;
                }

                if (m_Result == Result.OK)
                    break;

            } while (ConnectionTimeout > DateTime.Now);

            return m_Result;
        }



#if oldcode
        /// <summary>
        /// Connect to reader and allocate resources
        /// </summary>
        /// <param name="DeviceName">Destination Device Name</param>
        /// <value="x.x.x.x">Destination IP Address</value>
        /// <value="USB">Destination First USB Reader</value>
        /// <value="USB<sn>">Destination specified USB Reader</value>
        /// <value="COM<x>">Destination specified Serial Reader</value>
        /// <param name="TimeOut">Connection Timeout</param>
        /// <returns></returns>
        public Result Connect(string DeviceName, uint TimeOut)
        {
            if (bShutdownRequired)
                return Result.ALREADY_OPEN;

            try
            {
                m_linkProfileList = new LinkProfileInfoList();

                m_ConnectionTimeOut = TimeOut;
                if (COMM_Connect(DeviceName) != true)
                    return Result.FAILURE;
                
//                Set_FCC_Freq_Channel();
                //set_pll();

                bShutdownRequired = true;

                ThrowException(MacClearError());

                // Get OEM data
                // 0x0000 : date
                // 0x0002 : Region Code
                // 0x00a0 : Get Device Interface // no use
                // 0x00a3 : Get API Mode
                // 0x00a4 : Get Device Type
                // 0x00a5 : Max Out Power
                // 0x00a6 : Max Traget Power

                m_oem_machine = GetOEMDeviceType;          // First OEM Action
                m_save_country_code = GetOEMCountryCode;   // Second OEM Action
                m_oem_hipower = GetOEMHiPower;
                m_oem_maxpower = GetOEMMaxPower; // last OEM action
                m_save_readerName = m_oem_machine.ToString() + " RFID Reader";

                uint dataBuf = 0xff;

                /*
                 * HST_TAGACC_DESC_CFG 
                 * 0        Verify           Verify after write  
                 * 8:1     Retry 
                 * Number of time to retry the write if the verify failed 
                 * 31:9   Reserved      reserved - read / write as zero */
                //ThrowException(MacWriteRegister(MacRegister.HST_TAGACC_DESC_CFG  /*0xA01*/, 0x1ff));

                //Gen Country List
                GenCountryList();

                for (int cnt = 0; cnt < 16; cnt++)
                    ChannelStatus[cnt] = RFState.UNKNOWN;

#if __NO_USE__
                for (uint i = 0; i < 15; i++)
                {
                    AntennaPortConfig ant = new AntennaPortConfig();
                    AntennaPortStatus ants = new AntennaPortStatus();
                    GetAntennaPortConfiguration(i, ref ant);
                    GetAntennaPortStatus(i, ref ants);
                    Debug.WriteLine(
                        string.Format("{0},{1},{2},{3},{4},{5},{6},{7},{8}",
                        i,
                        ants.state,
                        ant.powerLevel,
                        ant.dwellTime,
                        ant.numberInventoryCycles,
                        ant.physicalRxPort,
                        ant.physicalTxPort,
                        ant.antennaSenseThreshold,
                        ants.antennaSenseValue));
                }
#endif
                //DO INITIAL HERE
                LoadDefaultSetting(true);

                FireStateChangedEvent(RFState.IDLE);
            }
            catch (ReaderException ex)
            {
                Disconnect();
#if DEBUG
                CSLibrary.Diagnostics.CoreDebug.Logger.ErrorException("HighLevelInterface.Connect()", ex);
#endif
                m_Result = ex.ErrorCode;
            }
            catch (System.Exception ex)
            {
#if DEBUG
                CSLibrary.Diagnostics.CoreDebug.Logger.ErrorException("HighLevelInterface.Connect()", ex);
#endif
                m_Result = Result.SYSTEM_CATCH_EXCEPTION;
            }
            return m_Result;
        }
#endif

#if nouse
        /// <summary>
        /// Start reconnect by using previously configured setting until success,
        /// You can call StopReconnect to stop it.
        /// </summary>
        /// <param name="FailureRetries">Reconnect retries</param>
        /// <returns></returns>
        public Result Reconnect(int FailureRetries, uint TimeOut)
        {
            if (FailureRetries < 0)
                return Result.INVALID_PARAMETER;

            if (FailureRetries > 0)
                return Reconnect(0);

            DateTime ReconnectTimeout = DateTime.Now.AddMilliseconds (m_ConnectionTimeOut);

            do
            {
                return Reconnect(1);
                FailureRetries--;
            } while (FailureRetries > 0 && DateTime.Now < ReconnectTimeout);
        }
#endif
            
        /// <summary>
        /// Start reconnect by using previously configured setting until success,
        /// You can call StopReconnect to stop it.
        /// </summary>
        /// <param name="FailureRetries">Reconnect retries</param>
        /// <returns></returns>
        public Result Reconnect(int FailureRetries)
        {
            Thread.Sleep(4000);
            
            for (int cnt = 0; ((cnt < FailureRetries) || (FailureRetries == 0)); cnt++)
            {
                DateTime ReconnectTimeout = DateTime.Now.AddMilliseconds (m_ConnectionTimeOut);

                do
                {
                    Disconnect();

                    try
                    {
                        //Initial HERE
                        if (COMM_Connect(m_DeviceName) == false)
                        {
                            Thread.Sleep(1000);  // delay 1s if can not connect to reader
                            m_Result = Result.FAILURE;
                            continue;
                        }

                        bShutdownRequired = true;

                        //DO INITIAL HERE
                        LoadDefaultSetting(false);

                        FireStateChangedEvent(RFState.IDLE);

                        m_Result = Result.OK;
                    }
                    catch (ReaderException ex)
                    {
                        Disconnect();
                        m_Result = ex.ErrorCode;
                    }
                    catch (System.Exception ex)
                    {
                        m_Result = Result.SYSTEM_CATCH_EXCEPTION;
                    }
                    if (m_Result == Result.OK)
                    {
                        if (m_GPI0InterruptTrigger != GPIOTrigger.OFF)
                            m_Result = SetGPI0Interrupt(m_DeviceName, m_GPI0InterruptTrigger);

                        if (m_GPI1InterruptTrigger != GPIOTrigger.OFF)
                            m_Result = SetGPI0Interrupt(m_DeviceName, m_GPI1InterruptTrigger);
                    }
                    
                    if (m_Result == Result.OK)
                        return Result.OK;

                } while (DateTime.Now < ReconnectTimeout);
            }
            
            return m_Result;
        }


#if nouse
        /// <summary>
        /// Start reconnect by using previously configured setting until success,
        /// You can call StopReconnect to stop it.
        /// </summary>
        /// <param name="FailureRetries">Reconnect retries</param>
        /// <returns></returns>
        public Result Reconnect(int FailureRetries)
        {
            while((FailureRetries > 0 || FailureRetries == -1) && !disposed)
            {
                //Disconnect first and ignore all warning
                Disconnect();

                try
                {
                    //Initial HERE

                    if (COMM_Connect(m_DeviceName) == false)
                    {
                        m_Result = Result.FAILURE;
                        FailureRetries--;
                        continue;
                    }

//                    Set_FCC_Freq_Channel();
                    //set_pll();

                    bShutdownRequired = true;

                    //DO INITIAL HERE
                    LoadDefaultSetting(false);

                    FireStateChangedEvent(RFState.IDLE);

                    m_Result = Result.OK;

                    break;
                }
                catch (ReaderException ex)
                {
                    FailureRetries--;
                    Disconnect();
#if DEBUG
                    CSLibrary.Diagnostics.CoreDebug.Logger.ErrorException("HighLevelInterface.Reconnect()", ex);
#endif
                    m_Result = ex.ErrorCode;
                    continue;
                }
                catch (System.Exception ex)
                {
                    FailureRetries--;
#if DEBUG
                    CSLibrary.Diagnostics.CoreDebug.Logger.ErrorException("HighLevelInterface.Reconnect()", ex);
#endif
                    m_Result = Result.SYSTEM_CATCH_EXCEPTION;
                    continue;
                }
            }
            return m_Result;
        }
#endif

        /// <summary>
        /// Disconnect reader and free resources 
        /// </summary>
        /// <returns>Return OK if Success</returns>
        public Result Disconnect()
        {
            if (!bShutdownRequired)
                return Result.NOT_INITIALIZED;

            try
            {
                m_Result = Result.OK;

                COMM_Disconnect();
                
                bShutdownRequired = false;
            }
            catch (ArgumentNullException)
            {
                m_Result = Result.INVALID_PARAMETER;
            }
            catch (OutOfMemoryException)
            {
                m_Result = Result.OUT_OF_MEMORY;
            }
            catch (Exception)
            {
                m_Result = Result.SYSTEM_CATCH_EXCEPTION;
            }
            return m_Result;
        }


        private void LoadDefaultSetting(bool defaultSetting)
        {

            if (defaultSetting)
            {
                //Load Default
                switch (m_save_country_code)
                {
                    case 1: ThrowException(SetFixedChannel(RegionCode.ETSI, 0, LBT.OFF)); break;
                    case 2:
                            switch (m_oem_special_country_version)
                            {
                                case 0x00:
                                case 0x2A2A2A53: // RWS
                                case 0x2A2A5257:
                                    ThrowException(SetHoppingChannels(RegionCode.FCC));
                                    break;
                                case 0x4F464341:
                                    ThrowException(SetHoppingChannels(RegionCode.HK));
                                    break;
                                case 0x2A2A4153:
                                    ThrowException(SetHoppingChannels(RegionCode.AU));
                                    break;
                                case 0x2A2A4E5A:
                                    ThrowException(SetHoppingChannels(RegionCode.NZ));
                                    break;
                                case 0x2A2A5347:
                                    ThrowException(SetHoppingChannels(RegionCode.SG));
                                    break;
                                case 0x2A2A5448:
                                    ThrowException(SetHoppingChannels(RegionCode.TH));
                                    break;
                                case 0x2A2A5A41:
                                    ThrowException(SetHoppingChannels(RegionCode.SAHOPPING));
                                    break;

                                default:
                                    ThrowException(SetHoppingChannels(RegionCode.FCC));
                                    break;
                            }
                        break;

/*                        
                        if (m_oem_freq_modification_flag == 0x00)
                            ThrowException(SetHoppingChannels(RegionCode.FCC));
                        else
                            ThrowException(SetHoppingChannels(RegionCode.HK));
                        break;
 */
                    case 3: ThrowException(SetFixedChannel(RegionCode.JP, 2, LBT.OFF)); break;
                    case 4: ThrowException(SetHoppingChannels(RegionCode.TW)); break;
                    case 6: ThrowException(SetHoppingChannels(RegionCode.KR)); break;
                    case 7: ThrowException(SetHoppingChannels(RegionCode.CN)); break;
                    case 8: ThrowException(SetFixedChannel(RegionCode.JP, 2, LBT.OFF)); break;
                    case 9: ThrowException(SetFixedChannel(RegionCode.ETSIUPPERBAND, 0, LBT.OFF)); break;
                    default:
                        ThrowException(Result.INVALID_PARAMETER);
                        break;
                }

                //Load LinkProfile
                ThrowException(m_linkProfileList.Load());

                //CS468 Port initialization
                ThrowException(SetDefaultAntennaList());

                //Set Compact data mode
                ThrowException(SetRadioResponseDataMode(ResponseMode.COMPACT));

                //Inital profile to 2
                switch (OEMChipSetID)
                {
                    case ChipSetID.R1000:
                        m_save_link_profile = 2;
                        ThrowException(SetCurrentLinkProfile(2));
                        break;

                    case ChipSetID.R2000:
                        m_save_link_profile = 1;
                        ThrowException(SetCurrentLinkProfile(1));
                        break;
                }

                ThrowException(SetTagGroup(Selected.ALL, Session.S0, SessionTarget.A));

                //Set Singulation
                ThrowException(SetCurrentSingulationAlgorithm(SingulationAlgorithm.DYNAMICQ));

                //Operation Mode
                ThrowException(SetOperationMode(RadioOperationMode.CONTINUOUS));

                /*
                                if (m_oem_machine == Machine.CS101 || m_oem_machine == Machine.CS203)
                                {
                                    ThrowException(MacWriteRegister(MacRegister.HST_ANT_DESC_SEL, m_save_antenna_port = 0));
                                    ThrowException(MacWriteRegister(MacRegister.HST_ANT_DESC_DWELL, m_save_inventory_duration = 0));
                                    ThrowException(MacWriteRegister(MacRegister.HST_ANT_DESC_INV_CNT, m_save_inventory_cycle = 65535));
                                }
                */
            }
            else
            {
                if (m_save_region_code == RegionCode.UNKNOWN)
                {
                    //Load Default
                    switch (m_save_country_code)
                    {
                        case 1: ThrowException(SetFixedChannel(RegionCode.ETSI, 0, LBT.OFF)); break;
                        case 2:
                            if (m_oem_freq_modification_flag == 0x00 || m_oem_special_country_version == 0x2A525753)
                                ThrowException(SetHoppingChannels(RegionCode.FCC));
                            else
                                ThrowException(SetHoppingChannels(RegionCode.HK));
                            break;
                        case 3: ThrowException(SetFixedChannel(RegionCode.JP, 2, LBT.OFF)); break;
                        case 4: ThrowException(SetHoppingChannels(RegionCode.AU)); break;
                        case 6: ThrowException(SetHoppingChannels(RegionCode.KR)); break;
                        case 7: ThrowException(SetHoppingChannels(RegionCode.CN)); break;
                        case 8: ThrowException(SetHoppingChannels(RegionCode.JP)); break;
                        case 9: ThrowException(SetFixedChannel(RegionCode.ETSIUPPERBAND, 0, LBT.OFF)); break;
                        default:
                            ThrowException(Result.INVALID_PARAMETER);
                            break;
                    }
                }
                else
                {
                    //Load Saved setting
                    if (m_save_fixed_channel)
                    {
                        ThrowException(SetFixedChannel());
                    }
                    else
                    {
                        if (m_save_agile_channel)
                        {
                            ThrowException(SetAgileChannels(m_save_region_code));
                        }
                        else
                        {
                            ThrowException(SetHoppingChannels());
                        }
                    }
                }

                if (m_AntennaList != null)
                {
                    ThrowException(m_AntennaList.Store(this));
                }
                else
                {
                    ThrowException(SetDefaultAntennaList());
                }

                //Set Compact data mode
                ThrowException(SetRadioResponseDataMode(m_save_resp_mode));

                //Inital profile to 2
                ThrowException(SetCurrentLinkProfile(m_save_link_profile));

                //Inital power to 300
                //ThrowException(SetPowerLevel(m_save_power_level));

                // 
                if (m_save_SingulationAlg != null)
                    ThrowException(SetSingulationAlgorithmParms(m_save_singulation, m_save_SingulationAlg));

                //Set Singulation
                ThrowException(SetCurrentSingulationAlgorithm(m_save_singulation));

                /*                Program.appSetting.AntennaSequenceMode = Program.ReaderXP.AntennaSequenceMode;
                                Program.appSetting.AntennaSequenceSize = Program.ReaderXP.AntennaSequenceSize;
                                Program.appSetting.AntennaPortSequence = Program.ReaderXP.AntennaPortSequence;

                                if (Program.ReaderXP.SetOperationMode(
                                    Program.appSetting.Cfg_continuous_mode ? RadioOperationMode.CONTINUOUS : RadioOperationMode.NONCONTINUOUS,
                                    Program.appSetting.AntennaSequenceMode,
                                    (int)Program.appSetting.AntennaSequenceSize
                                    ) != CSLibrary.Constants.Result.OK)
                                {
                                    MessageBox.Show("SetOperationMode failed");
                                }

                                if ((Program.ReaderXP.AntennaSequenceMode & AntennaSequenceMode.SEQUENCE) != 0)
                                {
                                    byte[] seq = new byte[Program.appSetting.AntennaPortSequence.Length];
                                    for (int i = 0; i < Program.appSetting.AntennaSequenceSize; i++)
                                    {
                                        seq[i] = (byte)Program.appSetting.AntennaPortSequence[i];
                                        //MessageBox.Show(Program.appSetting.AntennaPortSequence[i].ToString());
                                    }

                                    if (Program.ReaderXP.SetAntennaSequence(seq, (uint)Program.appSetting.AntennaSequenceSize, Program.appSetting.AntennaSequenceMode) != CSLibrary.Constants.Result.OK)
                                    {
                                        MessageBox.Show("SetAntennaSequence failed");
                                    }
                                }
                */

                ThrowException(SetTagGroup(m_save_taggroup));
                //ThrowException(SetTagGroup(Selected.ALL, Session.S0, SessionTarget.A));
                //ThrowException(SetTagGroup(Program.appSetting.tagGroup));
                //public Result SetTagGroup(Selected gpSelect, Session gpSession, SessionTarget gpSessionTarget)

                //Operation Mode
                ThrowException(SetOperationMode(m_save_oper_mode, m_save_antenna_cycle_sequence_mode, m_save_antenna_cycle_sequence_size));

//                if ((m_save_antenna_cycle_sequence_mode & AntennaSequenceMode.SEQUENCE) != 0)
                {
  //                  ThrowException(SetAntennaSequence(m_sequence_list, (uint)m_save_antenna_cycle_sequence_size, m_save_antenna_cycle_sequence_mode));
                }

            }
        }

        #endregion

        #region ====================== Version ======================
        /// <summary>
        /// Get RFID CSharp Library Version
        /// </summary>
        /// <returns></returns>
        public CSLibrary.Structures.Version GetCSLibraryVersion()
        {
            System.Version sver = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            CSLibrary.Structures.Version ver = new CSLibrary.Structures.Version();
            ver.major = (uint)sver.Major;
            ver.minor = (uint)sver.Minor;
            ver.patch = (uint)sver.Build;
            return ver;
        }
        /// <summary>
        /// face out command
        /// </summary>
        public LibraryVersion GetRfidLibraryVersion()
        {
            LibraryVersion nullver = new LibraryVersion();

            nullver.major = 0x00;
            nullver.minor = 0x00;
            nullver.patch = 0x00;

            return nullver;
            //GetLibraryVersion();
        }
        /// <summary>
        /// Get RFID Library Version
        /// 
        /// Note: This function is for backward compatibility only and will be deprecated in future version
        /// Please use GetHardwareVersion() instead
        /// </summary>
        public CSLibrary.Structures.Version GetDriverVersion()
        {
            return GetHardwareVersion();
        }
        /// <summary>
        /// GetFirmwareVersion
        /// </summary>
        /// <returns></returns>
        public MacVersion GetFirmwareVersion()
        {
            MacVersion version = new MacVersion();

            uint value = 0;
            
            if (MacReadRegister (MacRegister.MAC_VER, ref value) != Result.OK)
                return null;
            
            version.patch = 0xfff & value;
            version.minor = 0xfff & (value >> 12);
            version.major = 0xff & (value >> 24);

            return version;
        }

        /// <summary>
        /// hardware rev # is actually the r1000 CHIP info
        /// </summary>
        /// <returns></returns>
        public CSLibrary.Structures.Version GetHardwareVersion()
        {
            CSLibrary.Structures.Version hardwareVersion = new CSLibrary.Structures.Version();
            uint rawVersionR1000 = 0;

            m_Result = MacReadRegister(MacRegister.MAC_RFTRANSINFO /*0x0002*/, ref rawVersionR1000);

            if (m_Result == Result.OK)
            {
                hardwareVersion.major = (rawVersionR1000 >> 0x10) & 0x07; // control block
                hardwareVersion.minor = (rawVersionR1000 >> 0x00) & 0xFF; // chip rev
                hardwareVersion.patch = 0;
            }

            return hardwareVersion;
        }

        private const UInt32 MONTH_MASK = 0x0000000F;
        private const Int32 MONTH_SHFT = 0x00;
        private const UInt32 DAY_MASK = 0x0000001F;
        private const Int32 DAY_SHFT = 0x04;
        /*private const UInt32 MIN_MASK = 0x000FFF00;
        private const Int32 MIN_SHFT = 0x08;
        private const UInt32 SEC_MASK = 0x0FF00000;
        private const Int32 SEC_SHFT = 0x14;*/
        private const UInt32 YEAR_MASK = 0x0000FFFF;
        private const Int32 YEAR_SHFT = 0x00;
        /*private const UInt32 WEEK_MASK = 0x00FF0000;
        private const Int32 WEEK_SHFT = 0x10;*/
        /// <summary>
        /// Get Manufacture Date
        /// </summary>
        /// <returns></returns>
        public string GetManufactureDate()
        {
            uint[] data = new uint[2];
            int year, month, day;

            try
            {
                if (MacReadOemData (0x00000000, 2, data) == Result.OK)
                {
                    year = (int)((data[0] & YEAR_MASK) >> YEAR_SHFT);
                    month = (int)((data[1] & MONTH_MASK) >> MONTH_SHFT);
                    day = (int)((data[1] >> DAY_SHFT) & DAY_MASK);
                    return new DateTime(year, month, day).ToString("dd-MMM-yyyy");
                }
            }
            catch { }

            return "Unknown";
        }
        /// <summary>
        /// Get PCBA number
        /// </summary>
        /// <returns></returns>
        public string GetPCBAssemblyCode()
        {
            uint[] data = new uint[4];

            try
            {
                if (MacReadOemData(0x00000004, 4, data) == Result.OK)
                {
                    return uint32ArrayToString(data).Replace("\0","");
                }
            }
            catch { }

            return "Unknown";
        }

        private String uint32ArrayToString (UInt32[] source)
        {
            StringBuilder sb = new StringBuilder();

            // Byte at offset is total byte len, 2nd byte is always 3

            for (int index = 0; index < source.Length; index++)
            {
                sb.Append((Char)(source[index] >> 24 & 0x000000FF));
                sb.Append((Char)(source[index] >> 16 & 0x000000FF));
                sb.Append((Char)(source[index] >> 8 & 0x000000FF));
                sb.Append((Char)(source[index] >> 0 & 0x000000FF));
            }

            return sb.ToString();
        }
        #endregion

        #region ====================== Reader Function set ======================
        /// <summary>
        /// Available Maximum Power you can set on specific region
        /// </summary>
        public uint GetActiveMaxPowerLevel(RegionCode region)
        {
            // MAX Power 32dB
            if ((m_oem_hipower == 1) ||
                (m_oem_machine == Machine.CS468INT) ||
                (m_oem_machine == Machine.CS469) ||
                (region == RegionCode.IN) || 
                (region == RegionCode.G800) ||
                (m_oem_machine == Machine.CS209) ||
                (m_oem_machine == Machine.CS103) ||
                (m_oem_machine == Machine.CS108)                
                )
                return 320;

            // MAX Power 27.5dB
            if ((m_oem_machine == Machine.CS101 && region == RegionCode.ETSI) ||
                (m_oem_machine == Machine.CS203 && region == RegionCode.JP))
                return 275;

            return 300;
        }

        /// <summary>
        /// Available Maximum Power you can set on specific region
        /// </summary>
        private uint GetSoftwareMaxPowerLevel(RegionCode region)
        {
            // MAX Power 32dB
            if ((m_oem_hipower == 1) ||
                (m_oem_machine == Machine.CS468INT) ||
                (m_oem_machine == Machine.CS463) ||
                (m_oem_machine == Machine.CS468XJ) ||
                (m_oem_machine == Machine.CS469) ||
                (region == RegionCode.IN) ||
                (region == RegionCode.G800) ||
                (m_oem_machine == Machine.CS209) ||
                (m_oem_machine == Machine.CS103) ||
                (m_oem_machine == Machine.CS108)
                )
                return 320;

            // MAX Power 27.5dB
            if ((m_oem_machine == Machine.CS101 && region == RegionCode.ETSI) ||
                (m_oem_machine == Machine.CS203 && region == RegionCode.JP))
                return 275;

            return 300;
        }

        /// <summary>
        /// Available Maximum Power you can set on current region
        /// </summary>
        public uint GetActiveMaxPowerLevel()
        {
            return (GetActiveMaxPowerLevel(m_save_region_code));
        }
        
        /// <summary>
        /// Available Link Profile you can use on specific region
        /// </summary>
        /// <param name="region"></param>
        /// <returns></returns>
        public uint[] GetActiveLinkProfile(RegionCode region)
        {
            switch (OEMChipSetID)
            {
                case ChipSetID.R1000:
                    return new uint[] { 0, 1, 2, 3, 4, 5 };
                /*
                                switch (region)
                                    {
                                        case RegionCode.JP:
                                            return new uint[] { 2, 3, 5 };

                                        default:
                                            return new uint[] { 0, 1, 2, 3, 4, 5 };
                                    }
                    break;
                */
                case ChipSetID.R2000:
                    if (OEMDeviceType == Machine.CS468XJ && OEMCountryCode == 7)
                        return new uint[] { 0, 1, 2 };
                    
                    if (region == RegionCode.JP)
                        return new uint[] { 1, 2 };

                    if (region == RegionCode.KR)
                        return new uint[] { 0, 1, 2 };

                    return new uint[] { 0, 1, 2, 3 };
            }

            return new uint[0];
            


#if oldcode
            if (m_oem_machine == Machine.CS203)
            {
                switch (region)
                {
                    case RegionCode.CN:
                    case RegionCode.CN1:
                    case RegionCode.CN2:
                    case RegionCode.CN3:
                    case RegionCode.CN4:
                    case RegionCode.CN5:
                    case RegionCode.CN6:
                    case RegionCode.CN7:
                    case RegionCode.CN8:
                    case RegionCode.CN9:
                    case RegionCode.CN10:
                    case RegionCode.CN11:
                    case RegionCode.CN12:
                    case RegionCode.ETSI:
                    case RegionCode.JP:
                    case RegionCode.KR:
                        return new uint[] {0, 2, 3 };

                    case RegionCode.JP2012:
                        return new uint[] {2, 3, 5 };
                    
                    case RegionCode.UNKNOWN:
                        return new uint[0];

                    default:
                        return new uint[] { 0, 1, 2, 3, 4 };
                }
            }

            switch (region)
            {
                case RegionCode.CN:
                case RegionCode.CN1:
                case RegionCode.CN2:
                case RegionCode.CN3:
                case RegionCode.CN4:
                case RegionCode.CN5:
                case RegionCode.CN6:
                case RegionCode.CN7:
                case RegionCode.CN8:
                case RegionCode.CN9:
                case RegionCode.CN10:
                case RegionCode.CN11:
                case RegionCode.CN12:
                case RegionCode.ETSI:
                case RegionCode.JP:
                case RegionCode.KR:
                    return new uint[] { 0, 2, 3, 5 };

                case RegionCode.JP2012:
                    return new uint[] { 2, 3, 5 };

                case RegionCode.UNKNOWN:
                    return new uint[0];

                default:
                    return new uint[] { 0, 1, 2, 3, 4, 5 };
            }
            
#if nouse            
            if (m_oem_machine == Machine.CS101 || m_oem_machine == Machine.CS468)
            {
                switch (region)
                {
                    case RegionCode.CN:
                    case RegionCode.CN1:
                    case RegionCode.CN2:
                    case RegionCode.CN3:
                    case RegionCode.CN4:
                    case RegionCode.CN5:
                    case RegionCode.CN6:
                    case RegionCode.CN7:
                    case RegionCode.CN8:
                    case RegionCode.CN9:
                    case RegionCode.CN10:
                    case RegionCode.CN11:
                    case RegionCode.CN12:
                    case RegionCode.ETSI:
                    case RegionCode.JP:
                    case RegionCode.JP2012:
                    case RegionCode.KR:
                        return new uint[] { 0, 2, 3, 5 };
                    case RegionCode.UNKNOWN:
                        return new uint[0];
                    default:
#if ALL_PROFILE
                        return new uint[] { 0, 1, 2, 3, 4, 5, 6, 7 };
#else
                        return new uint[] { 0, 1, 2, 3, 4, 5 };
#endif

                }
            }
            else if (m_oem_machine == Machine.CS203)
            {
                switch (region)
                {
                    case RegionCode.CN:
                    case RegionCode.CN1:
                    case RegionCode.CN2:
                    case RegionCode.CN3:
                    case RegionCode.CN4:
                    case RegionCode.CN5:
                    case RegionCode.CN6:
                    case RegionCode.CN7:
                    case RegionCode.CN8:
                    case RegionCode.CN9:
                    case RegionCode.CN10:
                    case RegionCode.CN11:
                    case RegionCode.CN12:
                    case RegionCode.ETSI:
                    case RegionCode.JP:
                    case RegionCode.JP2012:
                    case RegionCode.KR:
                        return new uint[] { 0, 2, 3 };
                    case RegionCode.UNKNOWN:
                        return new uint[0];
                    default:
                        return new uint[] { 0, 1, 2, 3, 4 };
                }
            }
#endif

            return new uint[] { 0 };

#if nouse
            switch (region)
            {
#if CS101 || CS468
                case RegionCode.CN:
                case RegionCode.CN1:
                case RegionCode.CN2:
                case RegionCode.CN3:
                case RegionCode.CN4:
                case RegionCode.CN5:
                case RegionCode.CN6:
                case RegionCode.CN7:
                case RegionCode.CN8:
                case RegionCode.CN9:
                case RegionCode.CN10:
                case RegionCode.CN11:
                case RegionCode.CN12:
                case RegionCode.ETSI:
                case RegionCode.JP:
                case RegionCode.KR:
                    return new uint[] { 0, 2, 3, 5 };
                case RegionCode.UNKNOWN:
                    return new uint[0];
                default:
#if ALL_PROFILE
                    return new uint[] { 0, 1, 2, 3, 4, 5, 6, 7 };
#else
                    return new uint[] { 0, 1, 2, 3, 4, 5 };
#endif
#elif CS203
                case RegionCode.CN:
                case RegionCode.CN1:
                case RegionCode.CN2:
                case RegionCode.CN3:
                case RegionCode.CN4:
                case RegionCode.CN5:
                case RegionCode.CN6:
                case RegionCode.CN7:
                case RegionCode.CN8:
                case RegionCode.CN9:
                case RegionCode.CN10:
                case RegionCode.CN11:
                case RegionCode.CN12:
                case RegionCode.ETSI:
                case RegionCode.JP:
                case RegionCode.KR:
                return new uint[] { 0, 2, 3 };
                case RegionCode.UNKNOWN:
                    return new uint[0];
                default:
                return new uint[] { 0, 1, 2, 3, 4 };
#endif
            }
#endif
#endif
        }

        /// <summary>
        /// Available Link Profile you can use on current region
        /// </summary>
        public uint[] GetActiveLinkProfile()
        {
            return GetActiveLinkProfile(m_save_region_code);
        }

        /// <summary>
        /// Set Antenna List to factory default value.
        /// </summary>
        public Result SetDefaultAntennaList()
        {
            DEBUG_WriteLine(DEBUGLEVEL.API, "HighLevelInterface.SetDefaultAntennaList()");
            
            try
            {
                /*if(m_AntennaList != null)
                {
                    m_AntennaList.Clear();
                    m_AntennaList = null;
                } */
                m_AntennaList = new AntennaList(AntennaList.DEFAULT_ANTENNA_LIST, true);
                //ThrowException(m_AntennaList.Load(this));
                for (int i = 0; i < m_AntennaList.Count; i++)
                {
                    if (m_AntennaList[i].PowerLevel > GetSoftwareMaxPowerLevel(m_save_region_code))
                        m_AntennaList[i].PowerLevel = GetSoftwareMaxPowerLevel(m_save_region_code);
                }
                ThrowException(m_AntennaList.Store(this));
            }
            catch (ReaderException ex)
            {
                DEBUG_WriteLine (DEBUGLEVEL.API, "HighLevelInterface.SetDefaultAntennaList() : " + ex.Message);
                //CSLibrary.Diagnostics.CoreDebug.Logger.ErrorException("HighLevelInterface.SetDefaultAntennaList()", ex);
            }
            catch (Exception ex)
            {
                DEBUG_WriteLine(DEBUGLEVEL.API, "HighLevelInterface.SetDefaultAntennaList() : " + ex.Message);
                //CSLibrary.Diagnostics.CoreDebug.Logger.ErrorException("HighLevelInterface.SetDefaultAntennaList()", ex);
            }
            return m_Result;
        }

        /// <summary>
        /// Available region you can use
        /// </summary>
        public List<RegionCode> GetActiveRegionCode()
        {
            DEBUG_WriteLine(DEBUGLEVEL.API, "HighLevelInterface.GetActiveRegionCode()");

            return m_save_country_list;
        }
        /// <summary>
        /// GetCountryCode
        /// </summary>
        /// <returns>Result</returns>
        public Result GetCountryCode(ref uint code)
        {
            code = m_save_country_code;

            if (code < 0 || code > 8)
                return Result.INVALID_OEM_COUNTRY_CODE;

            return Result.OK;
        }
        /// <summary>
        /// Get Mac Error Code
        /// </summary>
        /// <returns>Error Code</returns>
        public Result GetMacErrorCode(ref uint code)
        {
            return (m_Result = MacReadRegister(MacRegister.MAC_ERROR /*0x0005*/, ref code));
        }

        /// <summary>
        /// GetAntennaSequence
        /// </summary>
        /// <param name="sequence"></param>
        /// <returns></returns>
        public Result GetAntennaSequence(Byte[] sequence)
        {
            ushort cycles = 0;
            AntennaSequenceMode mode = AntennaSequenceMode.UNKNOWN;
            uint sequenceSize = 0;
            if ((m_Result = GetOperationMode(ref cycles, ref mode, ref sequenceSize)) == Result.OK)
            {
                if (sequenceSize > 0)
                {
                    uint numberOfInt = sequenceSize / 8;
                    uint numberOfReminder = sequenceSize % 8;
                    uint[] data = new uint[ numberOfInt + (numberOfReminder > 0 ? 1 : 0)];
                    if ((m_Result = MacReadOemData(0xA7, (uint)data.Length, data)) == Result.OK)
                    {
                        byte[] seq = new byte[sequenceSize];
                        try
                        {
                            for (int i = 0; i < data.Length; i++)
                            {
                                seq[0 + i * 8] = (byte)((data[i] >> 28) & 0xF);
                                seq[1 + i * 8] = (byte)((data[i] >> 24) & 0xF);
                                seq[2 + i * 8] = (byte)((data[i] >> 20) & 0xF);
                                seq[3 + i * 8] = (byte)((data[i] >> 16) & 0xF);
                                seq[4 + i * 8] = (byte)((data[i] >> 12) & 0xF);
                                seq[5 + i * 8] = (byte)((data[i] >> 8) & 0xF);
                                seq[6 + i * 8] = (byte)((data[i] >> 4) & 0xF);
                                seq[7 + i * 8] = (byte)((data[i] >> 0) & 0xF);
                            }
                        }
                        catch (IndexOutOfRangeException ex)
                        {
                            //Done
                        }
                        sequence = (Byte[])seq.Clone();
                    }
                }
            }
            return m_Result;
        }

        /// <summary>
        /// GetAntennaSequence
        /// </summary>
        /// <param name="sequence">Antenna Sequence</param>
        /// <param name="sequenceSize">Antenna Sequence Size</param>
        /// <param name="mode">Antenna Sequence Mode</param>
        /// <returns></returns>
        public Result GetAntennaSequence(byte[] sequence, ref uint sequenceSize, ref AntennaSequenceMode mode)
        {
            string __FUNCTION__ = "GetAntennaSequence";
            ushort cycles = 0;
            //AntennaSequenceMode mode = AntennaSequenceMode.UNKNOWN;
            //uint sequenceSize = 0;
            if ((m_Result = GetOperationMode(ref cycles, ref mode, ref sequenceSize)) == Result.OK)
            {
                if (sequenceSize > 0)
                {
                    uint numberOfInt = sequenceSize / 8;
                    uint numberOfReminder = sequenceSize % 8;
                    uint[] data = new uint[numberOfInt + (numberOfReminder > 0 ? 1 : 0)];
                    if ((m_Result = MacReadOemData(0xA7, data)) == Result.OK)
                    {
                        byte[] seq = new byte[(sequenceSize / 8 + 1) * 8];
                        try
                        {
                            for (int i = 0; i < data.Length; i++)
                            {
                                seq[0 + i * 8] = (byte)((data[i] >> 28) & 0xF);
                                seq[1 + i * 8] = (byte)((data[i] >> 24) & 0xF);
                                seq[2 + i * 8] = (byte)((data[i] >> 20) & 0xF);
                                seq[3 + i * 8] = (byte)((data[i] >> 16) & 0xF);
                                seq[4 + i * 8] = (byte)((data[i] >> 12) & 0xF);
                                seq[5 + i * 8] = (byte)((data[i] >> 8) & 0xF);
                                seq[6 + i * 8] = (byte)((data[i] >> 4) & 0xF);
                                seq[7 + i * 8] = (byte)((data[i] >> 0) & 0xF);
                            }
                        }
                        catch (IndexOutOfRangeException ex)
                        {
                            //Done
                        }
                        Array.Copy(seq, 0, sequence, 0, (int)sequenceSize);
                    }
                }
            }
            return m_Result;
        }

        /// <summary>
        /// Set Antenna Sequence Size
        /// </summary>
        /// <param name="sequenceSize">Size of the antenna sequence. This value must not greater than 48.</param>
        /// <returns></returns>
        public Result SetAntennaSequence(int sequenceSize)
        {
            DEBUG_WriteLine(DEBUGLEVEL.API, "HighLevelInterface.SetAntennaSequence(int sequenceSize)");

            if (sequenceSize < 0 || sequenceSize > 48)
            {
                return Result.INVALID_PARAMETER;
            }

            try
            {
                //ThrowRfidException(SetOperationMode(m_save_antenna_cycle, m_save_antenna_cycle_sequence_mode, sequenceSize));
                m_Result = SetOperationMode(m_save_antenna_cycle, m_save_antenna_cycle_sequence_mode, sequenceSize);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message);
            }
            return m_Result;
        }

        /// <summary>
        /// SetAntennaSequence
        /// </summary>
        /// <param name="sequence">Antenna number list, value must be within 0-15</param>
        /// <returns></returns>
        public Result SetAntennaSequence(Byte[] sequence)
        {
            if (sequence == null || sequence.Length == 0)
            {
                return Result.INVALID_PARAMETER;
            }
            uint numberOfInt = (uint)(sequence.Length / 8);
            uint numberOfReminder = (uint)(sequence.Length % 8);
            uint[] data = new uint[numberOfInt + (numberOfReminder > 0 ? 1 : 0)];
            try
            {
                for (int i = 0; i < data.Length; i++)
                {
                    data[i] |= (uint)((sequence[0 + i * 8] & 0xF) << 28);
                    data[i] |= (uint)((sequence[1 + i * 8] & 0xF) << 24);
                    data[i] |= (uint)((sequence[2 + i * 8] & 0xF) << 20);
                    data[i] |= (uint)((sequence[3 + i * 8] & 0xF) << 16);
                    data[i] |= (uint)((sequence[4 + i * 8] & 0xF) << 12);
                    data[i] |= (uint)((sequence[5 + i * 8] & 0xF) << 8);
                    data[i] |= (uint)((sequence[6 + i * 8] & 0xF) << 4);
                    data[i] |= (uint)((sequence[7 + i * 8] & 0xF) << 0);
                }
            }
            catch (IndexOutOfRangeException ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message);
            }
            return (m_Result = MacWriteOemData(0xA6, (uint)data.Length, data));
        }

        /// <summary>
        /// SetAntennaSequenceonnect
        /// 
        /// </summary>
        /// <param name="sequence">Antenna Sequence</param>
        /// <param name="sequenceSize">Antenna Sequence Size, it must be within 1-48</param>
        /// <param name="sequenceMode">Antenna Sequence Mode</param>
        /// <returns></returns>
        public Result SetAntennaSequence(byte[] sequence, uint sequenceSize, AntennaSequenceMode sequenceMode)
        {
            string __FUNCTION__ = "SetAntennaSequence";
            //ResetResult();
            if (sequence == null || sequenceSize < 1 || sequenceSize > 48)
            {
                return Result.INVALID_PARAMETER;
            }

            // set antenna sequence mode & size
            //ThrowRfidException(SetOperationMode(RadioOperationMode.CONTINUOUS, sequenceMode, (int)sequenceSize));
            SetOperationMode(RadioOperationMode.CONTINUOUS, sequenceMode, (int)sequenceSize);

            // reset the size of byte array to multiple of 8
            byte[] seq = sequence.Length % 8 == 0 ? new byte[sequence.Length] : new byte[(sequence.Length / 8 + 1) * 8];

            Array.Copy(sequence, 0, seq, 0, sequence.Length);
            uint[] data = new uint[seq.Length / 8];

            try
            {
                for (int i = 0; i < data.Length; i++)
                {
                    data[i] |= (uint)((seq[0 + i * 8] & 0xF) << 28);
                    data[i] |= (uint)((seq[1 + i * 8] & 0xF) << 24);
                    data[i] |= (uint)((seq[2 + i * 8] & 0xF) << 20);
                    data[i] |= (uint)((seq[3 + i * 8] & 0xF) << 16);
                    data[i] |= (uint)((seq[4 + i * 8] & 0xF) << 12);
                    data[i] |= (uint)((seq[5 + i * 8] & 0xF) << 8);
                    data[i] |= (uint)((seq[6 + i * 8] & 0xF) << 4);
                    data[i] |= (uint)((seq[7 + i * 8] & 0xF) << 0);
                }
            }
            catch (IndexOutOfRangeException ex)
            {
            }
            return (m_Result = MacWriteOemData(0xA7, data));
        }

        /// <summary>
        /// SetAntennaSequence
        /// </summary>
        /// <param name="sequenceMode">Sequence mode.</param>
        /// <param name="sequenceSize">Size of the antenna sequence. This value must not greater than 48.</param>
        /// <param name="sequence">Antenna number list, value must be within 0-15 and only 48 sequences can be used.</param>
        /// <returns></returns>
        public Result SetAntennaSequence(AntennaSequenceMode sequenceMode, int sequenceSize, AntennaPortCollections sequence)
        {
            //ResetResult();
            
            if (sequence == null || sequence.Count == 0 || /*sequenceMode == AntennaSequenceMode.UNKNOWN ||*/ sequenceSize < 0 || sequenceSize > 48)
            {
                return Result.INVALID_PARAMETER;
            }

            uint numberOfInt = (uint)(sequence.Count / 8);
            uint numberOfReminder = (uint)(sequence.Count % 8);
            uint[] data = new uint[numberOfInt + (numberOfReminder > 0 ? 1 : 0)];
            try
            {
                //ThrowRfidException(SetOperationMode(m_save_antenna_cycle, sequenceMode, sequenceSize));
                SetOperationMode(m_save_antenna_cycle, sequenceMode, sequenceSize);

                for (int i = 0; i < data.Length; i++)
                {
                    data[i] |= (uint)(((byte)sequence[0 + i * 8] & 0xF) << 28);
                    data[i] |= (uint)(((byte)sequence[1 + i * 8] & 0xF) << 24);
                    data[i] |= (uint)(((byte)sequence[2 + i * 8] & 0xF) << 20);
                    data[i] |= (uint)(((byte)sequence[3 + i * 8] & 0xF) << 16);
                    data[i] |= (uint)(((byte)sequence[4 + i * 8] & 0xF) << 12);
                    data[i] |= (uint)(((byte)sequence[5 + i * 8] & 0xF) << 8);
                    data[i] |= (uint)(((byte)sequence[6 + i * 8] & 0xF) << 4);
                    data[i] |= (uint)(((byte)sequence[7 + i * 8] & 0xF) << 0);
                }
            }
            catch (ArgumentOutOfRangeException ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message);
            }

            return (m_Result = MacWriteOemData(0xA7, data));
        }

        /// <summary>
        /// Retrieves the operation mode for the RFID radio module.  The 
        /// operation mode cannot be retrieved while a radio module is 
        /// executing a tag-protocol operation. 
        /// </summary>
        /// <param name="cycles">The number of antenna cycles to be completed for command execution.
        /// <para>0x0001 = once cycle through</para>
        /// <para>0xFFFF = cycle forever until a CANCEL is received.</para></param>
        /// <param name="mode">Antenna Sequence mode.</param>
        /// <param name="sequenceSize">Sequence size. Maximum value is 48</param>
        /// <returns></returns>
        public Result GetOperationMode(ref ushort cycles, ref AntennaSequenceMode mode, ref uint sequenceSize)
        {
            uint value = 0;
            if ((m_Result = MacReadRegister(MacRegister.HST_ANT_CYCLES /*0x700*/, ref value)) == Result.OK)
            {
                cycles = (ushort)(0xffff & value);
                mode = (AntennaSequenceMode)((value >> 16) & 0x3);
                sequenceSize = (value >> 18) & 0x3F;
            }
            return m_Result;
        }
        
        /// <summary>
        /// Sets the operation mode of RFID radio module.  By default, when 
        /// an application opens a radio, the RFID Reader Library sets the 
        /// reporting mode to non-continuous.  An RFID radio module's 
        /// operation mode will remain in effect until it is explicitly changed 
        /// via RFID_RadioSetOperationMode, or the radio is closed and re-
        /// opened (at which point it will be set to non-continuous mode).  
        /// The operation mode may not be changed while a radio module is 
        /// executing a tag-protocol operation. 
        /// </summary>
        /// <param name="cycles">The number of antenna cycles to be completed for command execution.
        /// <para>0x0001 = once cycle through</para>
        /// <para>0xFFFF = cycle forever until a CANCEL is received.</para></param>
        /// <param name="mode">Antenna Sequence mode.</param>
        /// <param name="sequenceSize">Sequence size. Maximum value is 48</param>
        /// <returns></returns>
        public Result SetOperationMode(ushort cycles, AntennaSequenceMode mode, uint sequenceSize)
        {
            uint value = 0, value1 = 0;

            if (sequenceSize > 48)
            {
                return Result.INVALID_PARAMETER;
            }

            if ((m_Result = MacReadRegister(MacRegister.HST_ANT_CYCLES/*0x700*/, ref value)) == Result.OK)
            {
                value1 = (cycles | ((uint)mode & 0x3) << 16 | (sequenceSize & 0x3F) << 18);
                if (((value >> 24) & 0x01) != 0)
                    value1 |= (0x01 << 24);

                return (m_Result = MacWriteRegister(MacRegister.HST_ANT_CYCLES/*0x700*/, value1));
            }

            return m_Result;
        }
        
        /// <summary>
        /// Sets the operation mode of RFID radio module.  By default, when 
        /// an application opens a radio, the RFID Reader Library sets the 
        /// reporting mode to non-continuous.  An RFID radio module's 
        /// operation mode will remain in effect until it is explicitly changed 
        /// via RFID_RadioSetOperationMode, or the radio is closed and re-
        /// opened (at which point it will be set to non-continuous mode).  
        /// The operation mode may not be changed while a radio module is 
        /// executing a tag-protocol operation. 
        /// </summary>
        /// <param name="mode">The operation mode for the radio module.</param>
        /// <returns></returns>
        public Result SetOperationMode(RadioOperationMode mode)
        {
            ushort cycles = 0;
            AntennaSequenceMode smode = AntennaSequenceMode.UNKNOWN;
            uint sequenceSize = 0;

            if (RadioOperationMode.UNKNOWN == mode)
            {
                return Result.INVALID_PARAMETER;
            }
            if ((m_Result = GetOperationMode(ref cycles, ref smode, ref sequenceSize)) == Result.OK)
            {
                m_Result = SetOperationMode((ushort)(mode == RadioOperationMode.CONTINUOUS ? 0xFFFF : 1), smode, sequenceSize);
            }

            m_save_oper_mode = mode;
            return m_Result;
        }

        /// <summary>
        /// Sets the radio's operation mode.  An RFID radio module operation mode
        /// will remain in effect until it is explicitly changed via RadioSetOperationMode
        /// </summary>
        /// <param name="operationMode">The operation mode for the radio module.</param>
        /// <param name="sequenceMode">The antenna sequence mode for the radio module.</param>
        /// <param name="sequenceSize">The antenna sequence size for the radio module. This must be between 0 to 48.</param>
        /// <returns></returns>
        public Result SetOperationMode (RadioOperationMode operationMode, AntennaSequenceMode sequenceMode, int sequenceSize)
        {
            String __FUNCTION__ = "SetOperationMode";

            if (m_state == RFState.BUSY)
                return Result.RADIO_BUSY;
            
            try
            {
                // Validate the operation mode
                switch (operationMode)
                {
                    // Valid operation modes
                    case RadioOperationMode.CONTINUOUS:
                    case RadioOperationMode.NONCONTINUOUS:
                        {
                            break;
                        }
                    // Invalid operation modes
                    default:
                        {
                            return Result.INVALID_PARAMETER;
                        }
                } // switch (mode)

                switch (sequenceMode)
                {
                    case AntennaSequenceMode.NORMAL:
                    case AntennaSequenceMode.SEQUENCE:
                    case AntennaSequenceMode.SMART_CHECK:
                    case AntennaSequenceMode.SEQUENCE_SMART_CHECK:
                        break;
                    default:
                        {
                            return Result.INVALID_PARAMETER;
                        }
                }

                if (sequenceSize < 0 || sequenceSize > 48)
                {
                    return Result.INVALID_PARAMETER;
                }

                // Let the radio object set the operation mode
                uint old_data = 0;
                FreqAgileMode AgileEnable;

                MacReadRegister(MacRegister.HST_ANT_CYCLES, ref old_data);

                if ((old_data & (1 << 24)) == 0)
                    AgileEnable = FreqAgileMode.DISABLE;
                else
                    AgileEnable = FreqAgileMode.ENABLE;

                // Set the antenna cycles register to either perform a single cycle or to
                // cycle until a cancel
                UInt32 registerValue = (UInt32)(((operationMode == RadioOperationMode.CONTINUOUS) ? 0xffff : 0x00001) |
                                                ((int)sequenceMode << 16) |
                                                ((int)sequenceSize << 18) |
                                                ((AgileEnable == FreqAgileMode.ENABLE) ? 1 << 24 : 0x0000));

                MacWriteRegister(MacRegister.HST_ANT_CYCLES, registerValue);

#if testmephist                
                HST_ANT_CYCLES value = new HST_ANT_CYCLES(operationMode, sequenceMode, sequenceSize, AgileEnable);

                MacWriteRegister(MacRegister.HST_ANT_CYCLES, value.RawData);
#endif
      
                m_save_antenna_cycle = operationMode;
                m_save_antenna_cycle_sequence_mode = sequenceMode;
                m_save_antenna_cycle_sequence_size = sequenceSize;
            }
            catch (Exception ex)
            {
            }

            return  Result.OK;
        }

        /// <summary>
        /// Retrieves the operation mode for the RFID radio module.  The 
        /// operation mode cannot be retrieved while a radio module is 
        /// executing a tag-protocol operation. 
        /// </summary>
        /// <param name="mode"> return will receive the current operation mode.</param>
        /// <returns></returns>
        public Result GetOperationMode(ref RadioOperationMode mode)
        {
            uint value = 0;

            if (MacReadRegister(MacRegister.HST_ANT_CYCLES /*0x700 HST_ANT_CYCLES*/, ref value) != Result.OK)
                return Result.FAILURE;

            if (value == 0xffff)
                mode = RadioOperationMode.CONTINUOUS;
            else
                mode = RadioOperationMode.NONCONTINUOUS;

            return Result.OK;
        }
        
        /// <summary>
        /// Allows the application to retrieve the current power state of the 
        /// radio module.  The radio power state cannot be retrieved while a 
        /// radio module is executing a tag-protocol operation. 
        /// </summary>
        /// <param name="state">
        /// return will contain the current power state of the radio module.</param>
        /// <returns></returns>
        public Result GetPowerState(ref RadioPowerState state)
        {
            const uint HST_PWRMGMT_MODE_NORMAL = 0x0;
            const uint HST_PWRMGMT_MODE_LOWPOWER_STANDBY = 0x1;

            uint value = 0;

            if (MacReadRegister(MacRegister.HST_PWRMGMT /*0x0200 HST_PWRMGMT*/, ref value) != Result.OK)
                return Result.FAILURE;
            
            switch (value)
            {
                case HST_PWRMGMT_MODE_NORMAL:
                    state = RadioPowerState.FULL;
                    break;

                case HST_PWRMGMT_MODE_LOWPOWER_STANDBY:
                    state = RadioPowerState.STANDBY;
                    break;

                default:
                    state = RadioPowerState.UNKNOWN;
                    break;
            }

            return Result.OK;
        }
        
        /// <summary>
        /// Allows the application to set the power state of the radio module.  
        /// The radio power state cannot be set while a radio module is 
        /// executing a tag-protocol operation. 
        /// </summary>
        /// <param name="powerState">
        /// The new power state for the RFID radio module.</param>
        /// <returns></returns>
        public Result SetPowerState(RadioPowerState powerState)
        {
            if (RadioPowerState.UNKNOWN == powerState)
                return Result.INVALID_PARAMETER;

            if ((m_Result = MacWriteRegister(MacRegister.HST_PWRMGMT, (uint)powerState)) != Result.OK)
                return m_Result;

            if ((m_Result = COMM_HostCommand(HST_CMD.SETPWRMGMTCFG)) != Result.OK)
                return m_Result;

            return Result.OK;
        }

        /// <summary>
        /// Get Current active LinkProfile information
        /// </summary>
        /// <returns></returns>
        public LinkProfileInfo GetActiveLinkProfileInfo()
        {
            return m_linkProfileList.getActiveProfile();
        }

        /// <summary>
        /// Get Current active LinkProfile information
        /// </summary>
        /// <returns></returns>
        public LinkProfileInfo GetActiveLinkProfileInfo(uint index)
        {
            return m_linkProfileList.getActiveProfile(index);
        }

        /// <summary>
        ///  Allows the application to retrieve the current link profile for the 
        ///  radio module.  The current link profile cannot be retrieved while a 
        ///  radio module is executing a tag-protocol operation. 
        /// </summary>
        /// <returns></returns>
        public Result GetCurrentLinkProfile(ref uint link)
        {
            return MacReadRegister(MacRegister.HST_RFTC_CURRENT_PROFILE, ref link);
        }

        /// <summary>
        /// Allows the application to set the current link profile for the radio 
        /// module.  A link profile will remain in effect until changed by a 
        /// subsequent call to RFID_RadioSetCurrentLinkProfile.  The 
        /// current link profile cannot be set while a radio module is executing 
        /// a tag-protocol operation. 
        /// </summary>
        /// <param name="profile">
        /// The link profile to make the current link profile.  If this 
        /// parameter does not represent a valid link profile, 
        /// RFID_ERROR_INVALID_PARAMETER is returned. </param>
        /// <returns></returns>
        public Result SetCurrentLinkProfile(uint profile)
        {
            DEBUG_WriteLine(DEBUGLEVEL.API, "HighLevelInterface.SetCurrentLinkProfile(uint profile)");
        
            AGAIN:
            try
            {
                ThrowException(SetCurrentLinkProfile(profile, true));

                if (m_Result == Result.OK)
                {
                    m_linkProfileList.setActiveProfileIndex((int)profile);
                }
            }
            catch (ReaderException ex)
            {
                if (FireIfReset(ex.ErrorCode) == Result.OK)
                {
                    goto AGAIN;
                }
            }
            catch
            {
                m_Result = Result.SYSTEM_CATCH_EXCEPTION;
            }
            return m_Result;
        }

        /// <summary>
        /// Allows the application to set the current link profile for the radio 
        /// module.  A link profile will remain in effect until changed by a 
        /// subsequent call to RFID_RadioSetCurrentLinkProfile.  The 
        /// current link profile cannot be set while a radio module is executing 
        /// a tag-protocol operation. 
        /// </summary>
        /// <param name="profile">
        /// The link profile to make the current link profile.  If this 
        /// parameter does not represent a valid link profile, 
        /// RFID_ERROR_INVALID_PARAMETER is returned. </param>
        /// <param name="extenLO">
        /// config enable and disable external LO </param>
        /// <returns></returns>
        public Result SetCurrentLinkProfile(uint profile, bool extenLO)
        {
            AGAIN:
                if (!ValidLinkProfile(profile))
                {
                    return Result.INVALID_PARAMETER;
                }

                // ThrowException(RadioSetCurrentLinkProfile(m_save_link_profile = profile));
                UInt32 currentProfile = 0;

                MacReadRegister(MacRegister.HST_RFTC_CURRENT_PROFILE, ref currentProfile);

                try
                {
                    // Write the current profile register with the appropriate value and
                    // then instruct the MAC to set the current profile (by issuing the
                    // appropriate command)
                    //UInt32 registerValue = currentProfile;
                    UInt32 registerValue = profile;

                    //HST_RFTC_PROF_CURRENTPROF_SET_PROF(registerValue, profile);
                    MacWriteRegister(MacRegister.HST_RFTC_CURRENT_PROFILE, registerValue);

                    m_save_link_profile = profile;

                    //                    MacWriteRegister(HST_CMD, CMD_UPDATELINKPROFILE);
                    COMM_HostCommand(HST_CMD.UPDATELINKPROFILE);

                    SetRadioExtenalLO(m_save_extern_lo = extenLO);

                    // Set LNA 
                    SetLNA(m_save_rflna_high_comp, m_save_rflna_gain, m_save_iflna_gain, m_save_ifagc_gain);
                }
                catch (ReaderException ex)
                {
                    if (FireIfReset(ex.ErrorCode) == Result.OK)
                    {
                        goto AGAIN;
                    }
                }
                catch
                {
                    return Result.SYSTEM_CATCH_EXCEPTION;
                }
            return m_Result;
        }

        /// <summary>
        /// RF LNA compression mode = 0, 1
        /// RF LNA Gain = 1, 7, 13
        /// IF LNA Gain = 6, 12, 18, 24
        /// AGC Gain = -12, -6, 0, 6
        /// </summary>
        /// <param name="rflna_high_comp_norm"></param>
        /// <param name="rflna_gain_norm"></param>
        /// <param name="iflna_gain_norm"></param>
        /// <param name="ifagc_gain_norm"></param>
        /// <param name="ifagc_gain_norm"></param>
        /// <returns></returns>
        public Result SetLNA(int rflna_high_comp, int rflna_gain, int iflna_gain, int ifagc_gain)
        {
            if (rflna_high_comp != 00 && rflna_high_comp != 1)
                return Result.INVALID_PARAMETER;

            if (rflna_gain != 1 && rflna_gain != 7 && rflna_gain != 13)
                return Result.INVALID_PARAMETER;

            if (iflna_gain != 6 && iflna_gain != 12 && iflna_gain != 18 && iflna_gain != 24)
                return Result.INVALID_PARAMETER;

            if (ifagc_gain != -12 && ifagc_gain != -6 && ifagc_gain != 0 && ifagc_gain != 6)
                return Result.INVALID_PARAMETER;

            m_save_rflna_high_comp = rflna_high_comp;
            m_save_rflna_gain = rflna_gain;
            m_save_iflna_gain = iflna_gain;
            m_save_ifagc_gain = ifagc_gain;

            int rflna_high_comp_norm = rflna_high_comp;
            int rflna_gain_norm = 0;
            int iflna_gain_norm = 0;
            int ifagc_gain_norm = 0;

            switch (rflna_gain)
            {
                case 1:
                    rflna_gain_norm = 0;
                    break;
                case 7:
                    rflna_gain_norm = 2;
                    break;
                case 13:
                    rflna_gain_norm = 3;
                    break;
            }

            switch (iflna_gain)
            {
                case 24:
                    iflna_gain_norm = 0;
                    break;
                case 18:
                    iflna_gain_norm = 1;
                    break;
                case 12:
                    iflna_gain_norm = 3;
                    break;
                case 6:
                    iflna_gain_norm = 7;
                    break;
            }

            switch (ifagc_gain)
            {
                case -12:
                    ifagc_gain_norm = 0;
                    break;
                case -6:
                    ifagc_gain_norm = 4;
                    break;
                case 0:
                    ifagc_gain_norm = 6;
                    break;
                case 6:
                    ifagc_gain_norm = 7;
                    break;
            }

            int value = rflna_high_comp_norm << 8 |
                rflna_gain_norm << 6 |
                iflna_gain_norm << 3 |
                ifagc_gain_norm;

            return MacBypassWriteRegister (0x450, (UInt16)(value));
        }

        private bool ValidLinkProfile(uint profile)
        {
            uint[] prof = GetActiveLinkProfile();
            foreach (uint link in prof)
            {
                if (link == profile)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Allows an application to retrieve a single logical antenna port's 
        /// configuration parameters  e.g., dwell time, power level, and 
        /// number of inventory cycles.  Even if the logical antenna port is 
        /// disabled, an application is allowed to retrieve these configuration 
        /// parameters.  Retrieving configuration parameters does not cause a 
        /// logical antenna port to be automatically enabled; the application 
        /// must still enable the logical antenna port via 
        /// RFID_AntennaPortSetState.  The antenna-port configuration 
        /// cannot be retrieved while a radio module is executing a tag-
        /// protocol operation. 
        /// </summary>
        /// <param name="antenna">A structure that upon return will 
        /// contain the antenna-port configuration 
        /// parameters. </param>
        /// <returns>
        /// </returns>
        public Result GetAntennaPortConfiguration(ref AntennaPortConfig antenna)
        {
            return (m_Result = AntennaPortGetConfiguration(0, antenna));
        }
        /// <summary>
        /// Allows an application to retrieve a single logical antenna port's 
        /// configuration parameters  e.g., dwell time, power level, and 
        /// number of inventory cycles.  Even if the logical antenna port is 
        /// disabled, an application is allowed to retrieve these configuration 
        /// parameters.  Retrieving configuration parameters does not cause a 
        /// logical antenna port to be automatically enabled; the application 
        /// must still enable the logical antenna port via 
        /// RFID_AntennaPortSetState.  The antenna-port configuration 
        /// cannot be retrieved while a radio module is executing a tag-
        /// protocol operation. 
        /// </summary>
        /// <param name="port">antenna-port</param>
        /// <param name="antenna">A structure that upon return will 
        /// contain the antenna-port configuration 
        /// parameters. </param>
        /// <returns>
        /// </returns>
        public Result GetAntennaPortConfiguration(uint port, ref AntennaPortConfig antenna)
        {
            return (m_Result = AntennaPortGetConfiguration(port, antenna));
        }

        /// <summary>
        /// Allows an application to configure several parameters for a single 
        /// logical antenna port e.g.,  dwell time, power level, and number 
        /// of inventory cycles.  Even if the logical antenna port is disabled, 
        /// an application is allowed to set these configuration parameters.  
        /// Setting configuration parameters does not cause a logical antenna 
        /// port to be automatically enabled; the application must still enable 
        /// the logical antenna port via RFID_AntennaPortSetState.  The 
        /// antenna-port configuration cannot be set while a radio module is 
        /// executing a tag-protocol operation. 
        /// NOTE:  Since RFID_AntennaPortSetConfiguration sets all of the 
        /// configuration parameters that are present in the 
        /// RFID_ANTENNA_PORT_CONFIG structure, if an application wishes to 
        /// leave some parameters unchanged, the application should first call 
        /// RFID_AntennaPortGetConfiguration to retrieve the current 
        /// settings, update the values in the structure that are to be 
        /// changed, and then call RFID_AntennaPortSetConfiguration. 
        /// </summary>
        /// <param name="antenna">A structure that contains the 
        /// antenna-port configuration parameters.  This 
        /// parameter must not be NULL.  In version 1.1, 
        /// the physicalRxPort and physicalTxPort 
        /// fields must be the same. </param>
        /// <returns></returns>
        public Result SetAntennaPortConfiguration(AntennaPortConfig antenna)
        {
            if (antenna == null)
                return Result.INVALID_PARAMETER;

            return (m_Result = AntennaPortSetConfiguration(0, antenna));
        }
        /// <summary>
        /// Allows an application to configure several parameters for a single 
        /// logical antenna port e.g.,  dwell time, power level, and number 
        /// of inventory cycles.  Even if the logical antenna port is disabled, 
        /// an application is allowed to set these configuration parameters.  
        /// Setting configuration parameters does not cause a logical antenna 
        /// port to be automatically enabled; the application must still enable 
        /// the logical antenna port via RFID_AntennaPortSetState.  The 
        /// antenna-port configuration cannot be set while a radio module is 
        /// executing a tag-protocol operation. 
        /// NOTE:  Since RFID_AntennaPortSetConfiguration sets all of the 
        /// configuration parameters that are present in the 
        /// RFID_ANTENNA_PORT_CONFIG structure, if an application wishes to 
        /// leave some parameters unchanged, the application should first call 
        /// RFID_AntennaPortGetConfiguration to retrieve the current 
        /// settings, update the values in the structure that are to be 
        /// changed, and then call RFID_AntennaPortSetConfiguration. 
        /// </summary>
        /// <param name="port">antenna-port</param>
        /// <param name="antenna">A structure that contains the 
        /// antenna-port configuration parameters.  This 
        /// parameter must not be NULL.  In version 1.1, 
        /// the physicalRxPort and physicalTxPort 
        /// fields must be the same. </param>
        /// <returns></returns>
        public Result SetAntennaPortConfiguration(uint port, AntennaPortConfig antenna)
        {
            if (antenna == null)
                return Result.INVALID_PARAMETER;

            if (antenna.powerLevel > GetSoftwareMaxPowerLevel(m_save_region_code))
                return (m_Result = Result.INVALID_PARAMETER);

            m_AntennaList[(int)port].AntennaConfig = antenna;

            return (m_Result = AntennaPortSetConfiguration(port, antenna));
        }
        
        private Result SetAntennaPortConfiguration(uint virtual_port, uint physical_port)
        {
            AntennaPortConfig antenna = new AntennaPortConfig();
            
            if ((m_Result = AntennaPortGetConfiguration(virtual_port, antenna)) != Result.OK)
                return m_Result;

            antenna.physicalRxPort = antenna.physicalTxPort = physical_port;

            return (m_Result = AntennaPortSetConfiguration(virtual_port, antenna));
        }

        /// <summary>
        /// Retrieves the status of the requested logical antenna port for a 
        /// particular radio module.  The antenna-port status cannot be 
        /// retrieved while a radio module is executing a tag-protocol 
        /// operation. 
        /// </summary>
        /// <param name="portStatus"></param>
        /// <returns></returns>
        public Result GetAntennaPortStatus(AntennaPortStatus portStatus)
        {
            return (m_Result = AntennaPortGetStatus(0, portStatus));
        }

        /// <summary>
        /// Retrieves the status of the requested logical antenna port for a 
        /// particular radio module.  The antenna-port status cannot be 
        /// retrieved while a radio module is executing a tag-protocol 
        /// operation. 
        /// </summary>
        /// <param name="port">antenna port</param>
        /// <param name="portStatus"></param>
        /// <returns></returns>
        public Result GetAntennaPortStatus(uint port, AntennaPortStatus portStatus)
        {
            return (m_Result = AntennaPortGetStatus(port, portStatus));
        }

        /// <summary>
        /// Retrieves the status of the requested logical antenna port for a 
        /// particular radio module.  The antenna-port status cannot be 
        /// retrieved while a radio module is executing a tag-protocol 
        /// operation. 
        /// </summary>
        /// <param name="portStatus"></param>
        /// <returns></returns>
        public Result SetAntennaPortStatus(AntennaPortStatus portStatus)
        {
            return (m_Result = AntennaPortSetStatus(0, portStatus));
        }

        /// <summary>
        /// Retrieves the status of the requested logical antenna port for a 
        /// particular radio module.  The antenna-port status cannot be 
        /// retrieved while a radio module is executing a tag-protocol 
        /// operation. 
        /// </summary>
        /// <param name="port">antenna port</param>
        /// <param name="portStatus"></param>
        /// <returns></returns>
        public Result SetAntennaPortStatus(uint port, AntennaPortStatus portStatus)
        {
            m_AntennaList[(int)port].AntennaStatus = portStatus;

            return (m_Result = AntennaPortSetStatus(port, portStatus));
        }

        /// <summary>
        /// Allows an application to specify whether or not a radio module's 
        /// logical antenna port is enabled for subsequent tag operations.  The 
        /// antenna-port state cannot be set while a radio module is executing 
        /// a tag-protocol operation. 
        /// </summary>
        /// <param name="portState">The new state of the logical antenna port. </param>
        /// <returns></returns>
        public Result SetAntennaPortState(AntennaPortState portState)
        {
            if (portState == AntennaPortState.UNKNOWN)
                return Result.INVALID_PARAMETER;

            return (m_Result = AntennaPortSetState(0, portState));
        }

        /// <summary>
        /// Allows an application to specify whether or not a radio module's 
        /// logical antenna port is enabled for subsequent tag operations.  The 
        /// antenna-port state cannot be set while a radio module is executing 
        /// a tag-protocol operation. 
        /// </summary>
        /// <param name="port">antenna port</param>
        /// <param name="portState">The new state of the logical antenna port.</param>
        /// <returns></returns>
        public Result SetAntennaPortState(uint port, AntennaPortState portState)
        {
            if (portState == AntennaPortState.UNKNOWN)
                return Result.INVALID_PARAMETER;

            m_AntennaList[(int)port].State = portState;

            return (m_Result = AntennaPortSetState(port, portState));
        }

        /// <summary>
        /// Allows the application to retrieve the mode of data reporting for 
        /// tag-access operations.  The data-reporting mode may not be 
        /// retrieved while a radio module is executing a tag-protocol 
        /// operation. 
        /// </summary>
        /// <returns></returns>
        private Result GetRadioResponseDataMode(ref ResponseMode mode)
        {
            UInt32 registerValue = 0;

            MacReadRegister(MacRegister.HST_CMNDIAGS, ref registerValue);

		    // DMS Bug 9035:  Changing the logic to require specific complement of bits 
		    // to be set for each mode, otherwise throw an exception. 
		    //    Compact  = only inv resp bit is set
		    //    Normal   = inv resp and status bit is set
		    //    Extended = inv resp, status, and diag bit is set

		    // Figure out the mode for the packets
		    if(registerValue ==  (1 << 4))
		    {
                mode = ResponseMode.COMPACT;
		    }
		    else if (registerValue == (1 << 4 | 1 << 2))
		    {
                mode = ResponseMode.NORMAL;
		    }
		    else if (registerValue == (1 << 4 | 1 << 2 | 1))
		    {
                mode = ResponseMode.EXTENDED;
		    }
		    else
		    {
                return Result.INVALID_PARAMETER;
		    }

		    return Result.OK;
        }

        /// <summary>
        /// Allows the application to control the mode of data reporting for 
        /// tag-access operations.  By default, when an application opens a 
        /// radio, the RFID Reader Library sets the reporting mode to 
        /// "Compact".  The data-reporting mode will remain in effect until a 
        /// subsequent call to RFID_RadioSetResponseDataMode, or the radio 
        /// is closed and re-opened (at which point the data mode is set to 
        /// normal).  The data-reporting mode may not be changed while a 
        /// radio module is executing a tag-protocol operation. 
        /// </summary>
        /// <param name="mode">The requested data-reporting mode for 
        /// the data type specified by </param>
        /// <returns></returns>
        public Result SetRadioResponseDataMode(ResponseMode mode)
        {
		    UInt32 registerValue = 0;

            if (mode == ResponseMode.UNKNOWN)
                return Result.INVALID_PARAMETER;

		    // Based upon the mode selected, set the common diagnostics register
		    // appropriately
		    switch (mode)
		    {
                case ResponseMode.EXTENDED:
			    {
				    // Set the diagnostics bit in the register
				    //HST_CMNDIAGS_SET_DIAGS_ENABLED(registerValue);
                    registerValue |= 1 | 2 | 1<<4;
				    // Fall through on purpose
			    } // case RFID_RESPONSE_MODE_EXTENDED
                break;

                case ResponseMode.NORMAL:
			    {
				    // Set the status bit in the register
				    //HST_CMNDIAGS_SET_STATUS_ENABLED(registerValue);
                    registerValue |= 2 | 1<< 4;
                    // Fall through on purpose
			    } // case RFID_RESPONSE_MODE_NORMAL
                break;

                case ResponseMode.COMPACT:
			    {
				    // Set the inventory response bit in the register
				    //HST_CMNDIAGS_SET_INVRESP_ENABLED(registerValue);
				    // Set the RFU fields properly
				    //HST_CMNDIAGS_SET_RFU1(registerValue, 0);
                    registerValue |= 0x10;
                    break;
			    } // case RFID_RESPONSE_MODE_COMPACT

                default:
			    {
                    return Result.INVALID_PARAMETER;
				    break;
			    } // default
		    } // switch (mode)

		    // Write the common diagnostics register
            return MacWriteRegister(MacRegister.HST_CMNDIAGS, registerValue);
        }

        public Result SetRadioResponseDataMode(uint mode)
        {
            UInt32 registerValue = 0;

            MacReadRegister(MacRegister.HST_CMNDIAGS, ref registerValue);

            registerValue |= mode;

            // Write the common diagnostics register
            return MacWriteRegister(MacRegister.HST_CMNDIAGS, registerValue);
        }

        /// <summary>
        /// GetPowerLevel
        /// </summary>
        public Result GetPowerLevel(ref uint pwrlvl)
        {
            return GetPowerLevel(m_save_antenna_port, ref pwrlvl);
        }

        /// <summary>
        /// GetPowerLevel
        /// </summary>
        public Result GetPowerLevel(uint antennaPort, ref uint pwrlvl)
        {
            try
            {
                ThrowException(MacWriteRegister(MacRegister.HST_ANT_DESC_SEL, antennaPort));
                ThrowException(MacReadRegister(MacRegister.HST_ANT_DESC_RFPOWER, ref pwrlvl));
            }
            catch (ReaderException ex)
            {
#if DEBUG
                CSLibrary.Diagnostics.CoreDebug.Logger.ErrorException("HighLevelInterface.GetPowerLevel()", ex);
#endif
                return ex.ErrorCode;
            }
            catch (Exception ex)
            {
#if DEBUG
                CSLibrary.Diagnostics.CoreDebug.Logger.ErrorException("HighLevelInterface.GetPowerLevel()", ex);
#endif
                return Result.SYSTEM_CATCH_EXCEPTION;
            }
            return m_Result;
        }

        /// <summary>
        /// Set Power Level(Max 300)...
        /// </summary>
        /// <param name="pwrlevel">Power Level Max. 300</param>
        /// <returns></returns>
        public Result SetPowerLevel(uint pwrlevel)
        {
            DEBUG_WriteLine(DEBUGLEVEL.API, "HighLevelInterface.SetPowerLevel(uint pwrlevel)");

            return SetPowerLevel(m_save_antenna_port, pwrlevel);
        }

        /// <summary>
        /// Set Power Level...
        /// </summary>
        /// <param name="antennaPort">Antenna Port number</param>
        /// <param name="pwrlevel">Power Level</param>
        /// <returns></returns>
        public Result SetPowerLevel(uint antennaPort, uint pwrlevel)
        {
            //ushort HST_ANT_DESC_SEL = 0x701, HST_ANT_DESC_RFPOWER = 0x706;

            try
            {
                if (pwrlevel > GetSoftwareMaxPowerLevel(m_save_region_code))
                    return Result.INVALID_PARAMETER;

                ThrowException(MacWriteRegister(MacRegister.HST_ANT_DESC_SEL, antennaPort));
                ThrowException(MacWriteRegister(MacRegister.HST_ANT_DESC_RFPOWER, pwrlevel));
                m_save_power_level = pwrlevel;
            }
            catch (ReaderException ex)
            {
#if DEBUG
                CSLibrary.Diagnostics.CoreDebug.Logger.ErrorException("HighLevelInterface.SetPowerLevel()", ex);
#endif
                return ex.ErrorCode;
            }
            catch (Exception ex)
            {
#if DEBUG
                CSLibrary.Diagnostics.CoreDebug.Logger.ErrorException("HighLevelInterface.SetPowerLevel()", ex);
#endif
                return Result.SYSTEM_CATCH_EXCEPTION;
            }
            return m_Result;
        }

        /// <summary>
        /// Specifies the maximum number of inventory cycles to 
        /// attempt on the antenna port during a tag-protocol-
        /// operation cycle before switching to the next enabled 
        /// antenna port.  An inventory cycle consists of one or more 
        /// executions of the singulation algorithm for a particular 
        /// inventory-session target (i.e., A or B).  If the singulation 
        /// algorithm [SING-ALG] is configured to toggle the 
        /// inventory-session, executing the singulation algorithm for 
        /// inventory session A and inventory session B counts as 
        /// two inventory cycles.  A value of zero indicates that there 
        /// is no maximum number of inventory cycles for this 
        /// antenna port.  If this parameter is zero, then dwellTime 
        /// may not be zero. 
        /// See  for the effect of antenna-port dwell time and number 
        /// of inventory cycles on the amount of time spent on an 
        /// antenna port during a single tag-protocol-operation cycle. 
        /// NOTE:  when performing any non-inventory ISO 18000-
        /// 6C tag access operation (i.e., read, write, kill, or lock), the 
        /// radio module ignores the number of inventory cycles for 
        /// the antenna port which is used for the tag-protocol 
        /// operation. 
        /// </summary>
        /// <param name="cycle">Inventory Cycle count, if this is zero, InventoryDuration can't set to zero.</param>
        /// <returns></returns>
        public Result SetInventoryCycle(uint cycle)
        {
            return SetInventoryCycle(m_save_antenna_port, cycle);
        }

        /// <summary>
        /// Specifies the maximum number of inventory cycles to 
        /// attempt on the antenna port during a tag-protocol-
        /// operation cycle before switching to the next enabled 
        /// antenna port.  An inventory cycle consists of one or more 
        /// executions of the singulation algorithm for a particular 
        /// inventory-session target (i.e., A or B).  If the singulation 
        /// algorithm [SING-ALG] is configured to toggle the 
        /// inventory-session, executing the singulation algorithm for 
        /// inventory session A and inventory session B counts as 
        /// two inventory cycles.  A value of zero indicates that there 
        /// is no maximum number of inventory cycles for this 
        /// antenna port.  If this parameter is zero, then dwellTime 
        /// may not be zero. 
        /// See  for the effect of antenna-port dwell time and number 
        /// of inventory cycles on the amount of time spent on an 
        /// antenna port during a single tag-protocol-operation cycle. 
        /// NOTE:  when performing any non-inventory ISO 18000-
        /// 6C tag access operation (i.e., read, write, kill, or lock), the 
        /// radio module ignores the number of inventory cycles for 
        /// the antenna port which is used for the tag-protocol 
        /// operation. 
        /// </summary>
        /// <param name="antennaPort">Antenna Port number</param>
        /// <param name="cycle">Inventory Cycle count, if this is zero, InventoryDuration can't set to zero.</param>
        /// <returns></returns>
        public Result SetInventoryCycle(uint antennaPort, uint cycle)
        {
            try
            {
                ThrowException(MacWriteRegister(MacRegister.HST_ANT_DESC_SEL, m_save_antenna_port = antennaPort));

/*                if (cycle == 0)
                {
                    uint value = 0;

                    MacReadRegister(MacRegister.HST_ANT_DESC_DWELL, ref value);
                    if (value == 0)
                        cycle = 65535;
                }
*/
                ThrowException(MacWriteRegister(MacRegister.HST_ANT_DESC_INV_CNT, m_save_inventory_cycle = cycle));
            }
            catch (ReaderException ex)
            {
#if DEBUG
                CSLibrary.Diagnostics.CoreDebug.Logger.ErrorException("HighLevelInterface.SetInventoryCycle()", ex);
#endif
                return ex.ErrorCode;
            }
            catch (Exception ex)
            {
#if DEBUG
                CSLibrary.Diagnostics.CoreDebug.Logger.ErrorException("HighLevelInterface.SetInventoryCycle()", ex);
#endif
                return Result.SYSTEM_CATCH_EXCEPTION;
            }
            return m_Result;
        }

        /// <summary>
        /// This is used to set inventory duration
        /// </summary>
        /// <param name="duration"></param>
        /// <returns></returns>
        public Result SetInventoryDuration(uint duration)
        {
            return SetInventoryDuration(m_save_antenna_port, duration);
        }
        /// <summary>
        /// This is used to set inventory duration
        /// </summary>
        /// <param name="antennaPort">Antenna Port number</param>
        /// <param name="duration"></param>
        /// <returns></returns>
        public Result SetInventoryDuration(uint antennaPort, uint duration)
        {
            uint value = 0;

            try
            {
                ThrowException(MacWriteRegister(MacRegister.HST_ANT_DESC_SEL, m_save_antenna_port = antennaPort));

/*                if (duration == 0)
                {
                    ThrowException(MacReadRegister(MacRegister.HST_ANT_DESC_INV_CNT, ref value));
                    if (value == 0)
                        ThrowException(MacWriteRegister(MacRegister.HST_ANT_DESC_INV_CNT, m_save_inventory_cycle = 65535));
                }
*/
                ThrowException(MacWriteRegister(MacRegister.HST_ANT_DESC_DWELL, m_save_inventory_duration = duration));
            }
            catch (ReaderException ex)
            {
#if DEBUG
                CSLibrary.Diagnostics.CoreDebug.Logger.ErrorException("HighLevelInterface.SetInventoryDuration()", ex);
#endif
                return ex.ErrorCode;
            }
            catch (Exception ex)
            {
#if DEBUG
                CSLibrary.Diagnostics.CoreDebug.Logger.ErrorException("HighLevelInterface.SetInventoryDuration()", ex);
#endif
                return Result.SYSTEM_CATCH_EXCEPTION;
            }
            return m_Result;
        }
        /// <summary>
        /// Get the maximum number of inventory cycles to 
        /// attempt on the antenna port during a tag-protocol-
        /// operation cycle before switching to the next enabled 
        /// antenna port.  An inventory cycle consists of one or more 
        /// executions of the singulation algorithm for a particular 
        /// inventory-session target (i.e., A or B).  If the singulation 
        /// algorithm [SING-ALG] is configured to toggle the 
        /// inventory-session, executing the singulation algorithm for 
        /// inventory session A and inventory session B counts as 
        /// two inventory cycles.  A value of zero indicates that there 
        /// is no maximum number of inventory cycles for this 
        /// antenna port.  If this parameter is zero, then dwellTime 
        /// may not be zero. 
        /// See  for the effect of antenna-port dwell time and number 
        /// of inventory cycles on the amount of time spent on an 
        /// antenna port during a single tag-protocol-operation cycle. 
        /// NOTE:  when performing any non-inventory ISO 18000-
        /// 6C tag access operation (i.e., read, write, kill, or lock), the 
        /// radio module ignores the number of inventory cycles for 
        /// the antenna port which is used for the tag-protocol 
        /// operation. 
        /// </summary>
        /// <param name="count"></param>
        /// <returns></returns>
        public Result GetInventoryCount(ref uint count)
        {
            return GetInventoryCount(m_save_antenna_port, ref count);
        }

        /// <summary>
        /// Get the maximum number of inventory cycles to 
        /// attempt on the antenna port during a tag-protocol-
        /// operation cycle before switching to the next enabled 
        /// antenna port.  An inventory cycle consists of one or more 
        /// executions of the singulation algorithm for a particular 
        /// inventory-session target (i.e., A or B).  If the singulation 
        /// algorithm [SING-ALG] is configured to toggle the 
        /// inventory-session, executing the singulation algorithm for 
        /// inventory session A and inventory session B counts as 
        /// two inventory cycles.  A value of zero indicates that there 
        /// is no maximum number of inventory cycles for this 
        /// antenna port.  If this parameter is zero, then dwellTime 
        /// may not be zero. 
        /// See  for the effect of antenna-port dwell time and number 
        /// of inventory cycles on the amount of time spent on an 
        /// antenna port during a single tag-protocol-operation cycle. 
        /// NOTE:  when performing any non-inventory ISO 18000-
        /// 6C tag access operation (i.e., read, write, kill, or lock), the 
        /// radio module ignores the number of inventory cycles for 
        /// the antenna port which is used for the tag-protocol 
        /// operation. 
        /// </summary>
        /// <param name="antennaPort">Antenna Port number</param>
        /// <param name="count"></param>
        /// <returns></returns>
        public Result GetInventoryCount(uint antennaPort, ref uint count)
        {
            try
            {
                ThrowException(MacWriteRegister(MacRegister.HST_ANT_DESC_SEL, antennaPort));
                ThrowException(MacReadRegister(MacRegister.HST_ANT_DESC_INV_CNT, ref count));
            }
            catch (ReaderException ex)
            {
#if DEBUG
                CSLibrary.Diagnostics.CoreDebug.Logger.ErrorException("HighLevelInterface.GetInventoryCycle()", ex);
#endif
                return ex.ErrorCode;
            }
            catch (Exception ex)
            {
#if DEBUG
                CSLibrary.Diagnostics.CoreDebug.Logger.ErrorException("HighLevelInterface.GetInventoryCycle()", ex);
#endif
                return Result.SYSTEM_CATCH_EXCEPTION;
            }
            return m_Result;
        }

        /// <summary>
        /// This is used to get inventory duration
        /// </summary>
        /// <param name="duration"></param>
        /// <returns></returns>
        public Result GetInventoryDuration(ref uint duration)
        {
            return GetInventoryDuration(m_save_antenna_port, ref duration);
        }
        /// <summary>
        /// This is used to get inventory duration
        /// </summary>
        /// <param name="antennaPort"></param>
        /// <param name="duration"></param>
        /// <returns></returns>
        public Result GetInventoryDuration(uint antennaPort, ref uint duration)
        {
            try
            {
                ThrowException(MacWriteRegister(MacRegister.HST_ANT_DESC_SEL, antennaPort));
                ThrowException(MacReadRegister(MacRegister.HST_ANT_DESC_DWELL, ref duration));
            }
            catch (ReaderException ex)
            {
#if DEBUG
                CSLibrary.Diagnostics.CoreDebug.Logger.ErrorException("HighLevelInterface.GetInventoryDuration()", ex);
#endif
                return ex.ErrorCode;
            }
            catch (Exception ex)
            {
#if DEBUG
                CSLibrary.Diagnostics.CoreDebug.Logger.ErrorException("HighLevelInterface.GetInventoryDuration()", ex);
#endif
                return Result.SYSTEM_CATCH_EXCEPTION;
            }
            return m_Result;
        }

        /// <summary>
        /// Set connection intergface
        /// </summary>
        /// <param name="Mode"></param>
        /// <returns></returns>
        public Result SetInterface(INTERFACETYPE Mode)
        {
            UInt32 value = 0;
            bool portipv4 = false;
            bool portusb = false;
            bool portserial = false;

            switch (m_oem_machine)
            {
                case Machine.CS203:
                    portipv4 = true;
                    break;

                case Machine.CS468:
                case Machine.CS468INT:
                    portipv4 = true;
                    portusb = true;
                    break;

                case Machine.CS469:
                    portipv4 = true;
                    portusb = true;
                    portserial = true;
                    break;

                case Machine.CS468XJ:
                case Machine.CS463:
                    portipv4 = true;
                    portusb = true;
                    portserial = true;
                    break;

                case Machine.CS206:
                    portipv4 = true;
                    portusb = true;
                    portserial = true;
                    break;

                case Machine.CS468X:
                    portipv4 = true;
                    portusb = true;
                    portserial = true;
                    break;

                case Machine.CS203X:
                    portipv4 = true;
                    portusb = true;
                    portserial = true;
                    break;

                default:
                    return Result.DEVICE_NOT_SUPPORT;
            }

            switch (Mode)
            {
                case INTERFACETYPE.USB:
                    if (portusb == false)
                        return Result.DEVICE_NOT_SUPPORT;
                    return MacWriteOemData(0xa0, 0x00);

                case INTERFACETYPE.IPV4:
                    if (portipv4 == false)
                        return Result.DEVICE_NOT_SUPPORT;
                    return MacWriteOemData(0xa0, 0x01);

                case INTERFACETYPE.SERIAL:
                    if (portserial == false)
                        return Result.DEVICE_NOT_SUPPORT;
                    return MacWriteOemData(0xa0, 0x02);
            }
            return Result.INVALID_PARAMETER;
        }
        #endregion

        #region ====================== Set Criteria ======================
        
        const UInt32 RFID_18K6C_MAX_SELECT_CRITERIA_CNT = 8;

        /// <summary>
        /// Get Current Criteria Settings
        /// </summary>
        /// <param name="pCrit"></param>
        /// <returns></returns>
        public Result GetSelectCriteria(ref SelectCriteria pCrit)
        {
		    UInt32 bankSize = RFID_18K6C_MAX_SELECT_CRITERIA_CNT;
		    UInt32 countCriteria = 0;
		    bool   arrayIsTooSmall  = false;

            uint index, index1;


            // Go through the criteria and pick out the enabled ones
		    for (index = 0; index<bankSize; index++)
		    {
			    UInt32  registerValue = 0;

			    // Instruct the MAC as to which criteria we want to work with
                MacWriteRegister(MacRegister.HST_TAGMSK_DESC_SEL, index);

			    // Read the criteria configuration and determine if it is enabled
                MacReadRegister(MacRegister.HST_TAGMSK_DESC_CFG, ref registerValue);
			
                //if (HST_TAGMSK_DESC_CFG_IS_ENABLED(registerValue))
                if ((registerValue & 0x01) != 0x00)
                {
				    // If the array is large enough, copy the criteria information
                    if (countCriteria < pCrit.countCriteria)
				    {
					    SelectCriterion pCriterion = pCrit.pCriteria[countCriteria];
                        SelectMask      pMask       = pCriterion.mask;
					    SelectAction    pAction     = pCriterion.action;

					    // Fill out the action portion of the criterion
					    pAction.target         = (Target)((registerValue >> 1) & 0x07);
					    pAction.action         = (CSLibrary.Constants.Action)((registerValue >> 4) & 0x07);
                        pAction.enableTruncate = (int)((registerValue >> 7) & 0x01);

					    // Fill out the mask bank, offset, and length for the criterion
                        MacReadRegister(MacRegister.HST_TAGMSK_BANK, ref registerValue);
                        pMask.bank = (MemoryBank)registerValue;
                        MacReadRegister(MacRegister.HST_TAGMSK_PTR, ref pMask.offset);
                        MacReadRegister(MacRegister.HST_TAGMSK_LEN, ref pMask.count);

					    // Clear out the mask first
                        Array.Clear(pMask.mask, 0, pMask.mask.Length); 

					    // Now read the selector mask
		                UInt32 byteCount = (pMask.count + 7) / 8;
		                UInt32 lastCount = byteCount;
                        	
                        for (index1 = 0; index1 < byteCount; index1++)
                        {
			                int rightShift       = 0;
			                UInt32 loopCount        = (lastCount > 4 ? 4 : lastCount);

                            MacReadRegister((MacRegister.HST_TAGMSK_0_3 + (UInt16)index1), ref registerValue);

                            lastCount -= loopCount;
                            index1 += loopCount;

			                // Add the register bytes to the mask
			                for ( ; loopCount != 0; --loopCount, rightShift += 8)
			                {
				                pMask.mask[index1] = (byte)((registerValue >> rightShift) & 0x000000FF);
			                }
                        }

                        // If the last byte isn't complete, mask off the unneeded bits
                        if ((pMask.count % 8) > 0)
		                {
                            pMask.mask[index1] &= (byte)(0xFF << (int)(8 - (pMask.count % 8)));
		                }
	                } // Radio::ReadMacMaskRegisters
				    else
				    {
					    arrayIsTooSmall = true;
				    }

				    // Increment the number of enabled criteria found
				    ++countCriteria;
			    }
		    } // for (each criterion)

		    // Set the caller's criteria count
            pCrit.countCriteria = countCriteria;

            return Result.OK;
        }

	    ////////////////////////////////////////////////////////////////////////////////
	    // Name:        Radio::WriteMacMaskRegisters
	    // Description: Writes the MAC mask registers (select or post-singulation).
	    ////////////////////////////////////////////////////////////////////////////////
	    void WriteMacMaskRegisters(UInt16 registerAddress, UInt32 bitCount, byte [] pMask)
	    {
            const int BITS_PER_BYTE = 8;
            const int BYTES_PER_REGISTER = 4;
            const int BITS_PER_REGISTER = BITS_PER_BYTE * BYTES_PER_REGISTER;
            int pcnt = 0;

		    // Figure out how many bytes are in the mask
            UInt32 byteCount = (bitCount + 7) / 8;

		    // Now write each MAC mask register
		    while (byteCount > 0)
		    {
			    UInt32 registerValue = 0;
			    int leftShift     = 0;
			    UInt32 loopCount     = (byteCount > BYTES_PER_REGISTER ? BYTES_PER_REGISTER : byteCount);

			    // Decrement the byte count by the number of bytes put into the register
			    byteCount -= loopCount;

			    // Build up the register value
                for (int cnt = 0; cnt < loopCount; cnt++)
                {
                    registerValue |= ((uint)pMask[pcnt++] << leftShift);
                    leftShift += BITS_PER_BYTE;
			    }

			    // If it is the last byte of the mask, then we are going to zero out
			    // any bits not in the mask
			    if (byteCount == 0 && (bitCount % BITS_PER_BYTE) != 0)
			    {
				    UInt32 mask = 0xFFFFFFFF;
				    mask <<= (int)(BITS_PER_REGISTER - (BITS_PER_BYTE - (bitCount % BITS_PER_BYTE)));
                    mask >>= (int)(BITS_PER_REGISTER - (leftShift - (bitCount % BITS_PER_BYTE)));
				    registerValue &=  ~mask;
			    }

			    // Write the register
                MacWriteRegister((MacRegister)(registerAddress++), registerValue);
		    }
	    } // Radio::WriteMacMaskRegisters

        /// <summary>
        /// Configures the tag-selection criteria for the ISO 18000-6C select 
        /// command.  The supplied tag-selection criteria will be used for any 
        /// tag-protocol operations (i.e., Inventory, etc.) in 
        /// which the application specifies that an ISO 18000-6C select 
        /// command should be issued prior to executing the tag-protocol 
        /// operation (i.e., the SelectFlags.SELECT flag is provided to 
        /// the appropriate RFID_18K6CTag* function).  The tag-selection 
        /// criteria will stay in effect until the next call to 
        /// SetSelectCriteria.  Tag-selection criteria may not 
        /// be changed while a radio module is executing a tag-protocol 
        /// operation. 
        /// </summary>
        /// <param name="critlist">
        /// SelectCriteria array, containing countCriteria entries, of selection 
        /// criterion structures that are to be applied sequentially, beginning with 
        /// pCriteria[0], to the tag population.  If this field is NULL, 
        /// countCriteria must be zero. 
        ///</param>
        /// <returns></returns>
        public Result SetSelectCriteria(SelectCriterion[] critlist)
        {
            uint index;
            uint registerValue;

            if (critlist == null || critlist.Length == 0)
                return Result.INVALID_PARAMETER;

            try
            {
                SelectCriteria SC = new SelectCriteria();
                SC.countCriteria = (uint)critlist.Length;
                SC.pCriteria = (SelectCriterion[])critlist.Clone();

                for (index = 0; index < SC.countCriteria; index++)
                {
                    SelectCriterion pCriterion = SC.pCriteria[index];
                    SelectMask pMask = pCriterion.mask;
                    SelectAction pAction = pCriterion.action;

                    /*
                     * switch (p.mask.bank)
                                        {
                                            // Valid memory banks
                                            case RFID_18K6C_MEMORY_BANK_EPC:
                                            case RFID_18K6C_MEMORY_BANK_TID:
                                            case RFID_18K6C_MEMORY_BANK_USER:
                                                break;

                                            // Invalid memory banks
                                            case RFID_18K6C_MEMORY_BANK_RESERVED:
                                            default:
                                                return Result.INVALID_PARAMETER;
                                        }

                                        if (RFID_18K6C_MAX_SELECT_MASK_CNT < p.mask.count)
                                            return Result.INVALID_PARAMETER;
                
                                        // Validate the action target
                                        switch (p.action.target)
                                        {
                                    // Valid targets
                                            case RFID_18K6C_TARGET_INVENTORY_S0:
                                            case RFID_18K6C_TARGET_INVENTORY_S1:
                                            case RFID_18K6C_TARGET_INVENTORY_S2:
                                            case RFID_18K6C_TARGET_INVENTORY_S3:
                                            case RFID_18K6C_TARGET_SELECTED:
                                            {
                                                break;
                                            }
                                            // Invalid targets
			            
                                            default:
                                            {
                                                return Result.INVALID_PARAMETER;
                                            }
                                        } // switch (pAction->target)
                    */
                    // Instruct the MAC as to which select mask we want to work with
                    MacWriteRegister(MacRegister.HST_TAGMSK_DESC_SEL, index);

                    // Create the HST_TAGMSK_DESC_CFG register value and write it to the MAC
                    registerValue = (0x01 |
                        (((uint)(pAction.target) & 0x07) << 1) |
                        (((uint)(pAction.action) & 0x07) << 4) |
                        (pAction.enableTruncate != 0x00 ? (uint)(1 << 7) : 0));
                    MacWriteRegister(MacRegister.HST_TAGMSK_DESC_CFG, registerValue);

                    // Create the HST_TAGMSK_BANK register value and write it to the MAC
                    registerValue = (uint)pMask.bank;
                    MacWriteRegister(MacRegister.HST_TAGMSK_BANK, registerValue);

                    // Write the mask offset to the HST_TAGMSK_PTR register
                    MacWriteRegister(MacRegister.HST_TAGMSK_PTR, (uint)pMask.offset);

                    // Create the HST_TAGMSK_LEN register and write it to the MAC
                    registerValue = (uint)(pMask.count);
                    MacWriteRegister(MacRegister.HST_TAGMSK_LEN, registerValue);

                    // Now write the MAC's mask registers
                    WriteMacMaskRegisters((ushort)MacRegister.HST_TAGMSK_0_3, pMask.count, pMask.mask);
                    // Set up the selection criteria
                }

                while (index < RFID_18K6C_MAX_SELECT_CRITERIA_CNT)
                {
                    // Instruct the MAC as to which select mask we want to work with
                    MacWriteRegister(MacRegister.HST_TAGMSK_DESC_SEL, index);

                    // Set the descriptor to disabled
                    MacWriteRegister(MacRegister.HST_TAGMSK_DESC_CFG, 0);

                    index++;
                }
            }
            catch (System.Exception ex)
            {
#if DEBUG
                CSLibrary.Diagnostics.CoreDebug.Logger.ErrorException("HighLevelInterface.SetSelectCriteria()", ex);
#endif
                return Result.SYSTEM_CATCH_EXCEPTION;
            }
            return m_Result;
        }

        public Result SetSelectCriteria(uint index, SelectCriterion crit)
        {
            uint registerValue;

            // Instruct the MAC as to which select mask we want to work with
            MacWriteRegister(MacRegister.HST_TAGMSK_DESC_SEL, index);

            if (crit == null)
            {
                MacWriteRegister(MacRegister.HST_TAGMSK_DESC_CFG, 0x0000);
                //MacWriteRegister(MacRegister.HST_TAGMSK_BANK, 0x0000);
                //MacWriteRegister(MacRegister.HST_TAGMSK_PTR, 0x0000);
                //MacWriteRegister(MacRegister.HST_TAGMSK_LEN, 0x0000);
                return Result.OK;
            }

            try
            {
                {
                    SelectCriterion pCriterion = crit;
                    SelectMask pMask = pCriterion.mask;
                    SelectAction pAction = pCriterion.action;

                    // Create the HST_TAGMSK_DESC_CFG register value and write it to the MAC
                    registerValue = (0x01 |
                        (((uint)(pAction.target) & 0x07) << 1) |
                        (((uint)(pAction.action) & 0x07) << 4) |
                        (pAction.enableTruncate != 0x00 ? (uint)(1 << 7) : 0));
                    MacWriteRegister(MacRegister.HST_TAGMSK_DESC_CFG, registerValue);

                    // Create the HST_TAGMSK_BANK register value and write it to the MAC
                    registerValue = (uint)pMask.bank;
                    MacWriteRegister(MacRegister.HST_TAGMSK_BANK, registerValue);

                    // Write the mask offset to the HST_TAGMSK_PTR register
                    MacWriteRegister(MacRegister.HST_TAGMSK_PTR, (uint)pMask.offset);

                    // Create the HST_TAGMSK_LEN register and write it to the MAC
                    registerValue = (uint)(pMask.count);
                    MacWriteRegister(MacRegister.HST_TAGMSK_LEN, registerValue);

                    // Now write the MAC's mask registers
                    WriteMacMaskRegisters((ushort)MacRegister.HST_TAGMSK_0_3, pMask.count, pMask.mask);
                    // Set up the selection criteria
                }
            }
            catch (System.Exception ex)
            {
#if DEBUG
                //				CSLibrary.Diagnostics.CoreDebug.Logger.ErrorException("HighLevelInterface.SetSelectCriteria()", ex);
#endif
                return Result.SYSTEM_CATCH_EXCEPTION;
            }
            return m_Result;
        }

        public Result CancelAllSelectCriteria()
        {
            for (uint cnt = 0; cnt < 7; cnt++)
            {
                SetSelectCriteria(cnt, null);
            }

            return Result.OK;
        }

        /// <summary>
        /// Configures the post-singulation match criteria to be used by the 
        /// RFID radio module.  The supplied post-singulation match criteria 
        /// will be used for any tag-protocol operations (i.e., 
        /// Inventory, etc.) in which the application specifies 
        /// that a post-singulation match should be performed on the tags 
        /// that are singulated by the tag-protocol operation (i.e., the 
        /// SelectFlags.POST_MATCH flag is provided to the 
        /// appropriate RFID_18K6CTag* function).  The post-singulation 
        /// match criteria will stay in effect until the next call to 
        /// SetPostMatchCriteria.  Post-singulation match 
        /// criteria may not be changed while a radio module is executing a 
        /// tag-protocol operation. 
        /// </summary>
        /// <param name="postmatch"> An array that specifies the post-
        /// singulation match criteria that are to be 
        /// applied to the tag's Electronic Product Code 
        /// after it is singulated to determine if it is to 
        /// have the tag-protocol operation applied to it.  
        /// If the countCriteria field is zero, all post-
        /// singulation criteria will be disabled.  This 
        /// parameter must not be NULL. </param>
        /// <returns></returns>
        public Result SetPostMatchCriteria(SingulationCriterion[] postmatch)
        {
            UInt32 registerValue;

            try
            {
                if (postmatch.Length != 0)
                {
                    // Set up the post-singulation match criteria
//                    pCriterion = pParms->pCriteria;
//                    const RFID_18K6C_SINGULATION_MASK* pMask = &pCriterion->mask;

                    SingulationMask pMask = postmatch[0].mask;

                    // Set up the HST_INV_EPC_MATCH_CFG register and write it to the MAC.
                    // For now, we are going to assume that the singulation match should be
                    // enabled (if the application so desires, we can turn it off when we
                    // actually do the tag-protocol operation).
                    registerValue =
                        ((uint)(postmatch[0].match != 0 ? 0 : 2) | 
                        (uint)(postmatch[0].mask.count << 2) |
                        (uint)(postmatch[0].mask.offset << 11));

                    MacWriteRegister(MacRegister.HST_INV_EPC_MATCH_SEL, 0X00);
                    MacWriteRegister(MacRegister.HST_INV_EPC_MATCH_CFG, registerValue);

                    // Now write the MAC's mask registers
                    WriteMacMaskRegisters((UInt16)MacRegister.HST_INV_EPCDAT_0_3, pMask.count, pMask.mask);
                }
                else // must be calling to disable criteria
                {
                    MacWriteRegister(MacRegister.HST_INV_EPC_MATCH_CFG, 0);
                }
            }
            catch (System.Exception ex)
            {
#if DEBUG
                CSLibrary.Diagnostics.CoreDebug.Logger.ErrorException("HighLevelInterface.SetSelectCriteria()", ex);
#endif
                return Result.SYSTEM_CATCH_EXCEPTION;
            }
            return m_Result;
        }
        #endregion

        #region ====================== Set Singulation Algorithm Function ======================
        /// <summary>
        /// Allows the application to set the currently-active singulation 
        /// algorithm (i.e., the one that is used when performing a tag-
        /// protocol operation (e.g., inventory, tag read, etc.)).  The 
        /// currently-active singulation algorithm may not be changed while a 
        /// radio module is executing a tag-protocol operation. 
        /// </summary>
        /// <param name="SingulationAlgorithm">
        /// The singulation algorithm that is to be used for 
        /// subsequent tag-access operations.  If this 
        /// parameter does not represent a valid 
        /// singulation algorithm, 
        /// RFID_ERROR_INVALID_PARAMETER is returned. </param>
        public Result SetCurrentSingulationAlgorithm(SingulationAlgorithm SingulationAlgorithm)
        {
            Result ret;
            UInt32 value = 0;

            if (SingulationAlgorithm == SingulationAlgorithm.UNKNOWN)
                return Result.INVALID_PARAMETER;

            m_save_singulation = SingulationAlgorithm;

            if ((ret = MacReadRegister (MacRegister.HST_INV_CFG, ref value)) != Result.OK)
                return ret;

            value &= ~0x3fU;
            value |= (UInt32)SingulationAlgorithm;

            return MacWriteRegister(MacRegister.HST_INV_CFG, value);
        }

        /// <summary>
        /// Get Current Singulation Algorithm
        /// </summary>
        /// <param name="SingulationAlgorithm"></param>
        /// <returns></returns>
        public Result GetCurrentSingulationAlgorithm(ref SingulationAlgorithm SingulationAlgorithm)
        {
            UInt32 value = 0;

            MacReadRegister(MacRegister.HST_INV_CFG, ref value);
            value &= 0x3fU;
            SingulationAlgorithm = (SingulationAlgorithm)value;
            return Result.OK;
        }

        /// <summary>
        /// SetSingulationAlgorithmParms
        /// </summary>
        /// <param name="alg"></param>
        /// <param name="parms"></param>
        /// <returns></returns>
        public Result SetSingulationAlgorithmParms(SingulationAlgorithm alg, SingulationAlgorithmParms parms)
        {
            const uint RFID_18K6C_SINGULATION_ALGORITHM_FIXEDQ = 0;
            //const uint RFID_18K6C_SINGULATION_ALGORITHM_DYNAMICQ_1 = 1;
            //const uint RFID_18K6C_SINGULATION_ALGORITHM_DYNAMICQ_2 = 2;
            const uint RFID_18K6C_SINGULATION_ALGORITHM_DYNAMICQ = 3;

            if (alg == SingulationAlgorithm.UNKNOWN)
                return Result.INVALID_PARAMETER;

            m_save_SingulationAlg = parms;

            try
            {
                switch (alg)
                {
                    case SingulationAlgorithm.FIXEDQ:
                        {
                            FixedQParms p = (FixedQParms)parms;
                            // Write the inventory algorithm parameter registers
                            MacWriteRegister(MacRegister.HST_INV_SEL, RFID_18K6C_SINGULATION_ALGORITHM_FIXEDQ);
                            MacWriteRegister(MacRegister.HST_INV_ALG_PARM_0, p.qValue);
                            MacWriteRegister(MacRegister.HST_INV_ALG_PARM_1, p.retryCount);
                            MacWriteRegister(MacRegister.HST_INV_ALG_PARM_2,
                                (uint)(p.toggleTarget != 0 ? 1 : 0) |
                                (uint)(p.repeatUntilNoTags != 0 ? 2 : 0));
                            MacWriteRegister(MacRegister.HST_INV_ALG_PARM_3, 0);
                        }
                        break;

/*                    case SingulationAlgorithm.DYNAMICQ_1:
                        {
                            DynamicQ_1Parms p = (DynamicQ_1Parms)parms;
                            // Write the inventory algorithm parameter registers.  For register
                            // zero, remember to preserve values that we aren't exposing
                            MacWriteRegister(MacRegister.HST_INV_SEL, RFID_18K6C_SINGULATION_ALGORITHM_DYNAMICQ_1);
                            MacWriteRegister(MacRegister.HST_INV_ALG_PARM_0, p.startQValue | (p.maxQValue << 4) | (p.minQValue << 8) | (p.maxReps << 12) | (p.HighThres << 20) | (p.LowThres << 24));
                            MacWriteRegister(MacRegister.HST_INV_ALG_PARM_1, p.retryCount);
                            MacWriteRegister(MacRegister.HST_INV_ALG_PARM_2, (uint)(p.toggleTarget != 0 ? 1 : 0));
                            MacWriteRegister(MacRegister.HST_INV_ALG_PARM_3, 0);
                        }
                        break;

                    case SingulationAlgorithm.DYNAMICQ_2:
                        {
                            DynamicQ_2Parms p = (DynamicQ_2Parms)parms;
                            // Write the inventory algorithm parameter registers.  For register
                            // zero, remember to preserve values that we aren't exposing
                            MacWriteRegister(MacRegister.HST_INV_SEL, RFID_18K6C_SINGULATION_ALGORITHM_DYNAMICQ_2);
                            MacWriteRegister(MacRegister.HST_INV_ALG_PARM_0, p.startQValue | (p.maxQValue << 4) | (p.minQValue << 8) | (p.maxReps << 12) | (p.HighThres << 20) | (p.LowThres << 24));
                            MacWriteRegister(MacRegister.HST_INV_ALG_PARM_1, p.retryCount);
                            MacWriteRegister(MacRegister.HST_INV_ALG_PARM_2, (uint)(p.toggleTarget != 0 ? 1 : 0));
                            MacWriteRegister(MacRegister.HST_INV_ALG_PARM_3, 0);
                        }
                        break;
*/
                    case SingulationAlgorithm.DYNAMICQ:
                        {
                            DynamicQParms p = (DynamicQParms)parms;
                            // Write the inventory algorithm parameter registers.  For register
                            // zero, remember to preserve values that we aren't exposing
                            MacWriteRegister(MacRegister.HST_INV_SEL, RFID_18K6C_SINGULATION_ALGORITHM_DYNAMICQ);
                            MacWriteRegister(MacRegister.HST_INV_ALG_PARM_0, p.startQValue | (p.maxQValue << 4) | (p.minQValue << 8) | (p.thresholdMultiplier << 12));
                            MacWriteRegister(MacRegister.HST_INV_ALG_PARM_1, p.retryCount);
                            MacWriteRegister(MacRegister.HST_INV_ALG_PARM_2, (uint)(p.toggleTarget != 0 ? 1 : 0));
                            MacWriteRegister(MacRegister.HST_INV_ALG_PARM_3, 0);
                        }
                        break;

                    default:
                        return Result.INVALID_PARAMETER;
                } // switch (algorithm)
            }
            catch (Exception ex)
            {

            }

            return (m_Result = SetCurrentSingulationAlgorithm(alg));
        }
        /// <summary>
        /// GetSingulationAlgorithmParms
        /// </summary>
        /// <param name="alg"></param>
        /// <param name="parms"></param>
        /// <returns></returns>
        public Result GetSingulationAlgorithmParms(SingulationAlgorithm alg, SingulationAlgorithmParms parms)
        {
            const int RFID_18K6C_SINGULATION_ALGORITHM_FIXEDQ   = 0;
            const int RFID_18K6C_SINGULATION_ALGORITHM_DYNAMICQ = 3;
            UInt32 parm0Register = 0;
            UInt32 parm1Register = 0;
            UInt32 parm2Register = 0;

            switch (alg)
            {
                case SingulationAlgorithm.FIXEDQ:
                    FixedQParms m_fixedQ = (FixedQParms)parms;

                    // Tell the MAC which singulation algorithm selector to use and then
                    // read the singulation algorithm registers
                    MacWriteRegister(MacRegister.HST_INV_SEL, RFID_18K6C_SINGULATION_ALGORITHM_FIXEDQ);
                    MacReadRegister(MacRegister.HST_INV_ALG_PARM_0, ref parm0Register);
                    MacReadRegister(MacRegister.HST_INV_ALG_PARM_1, ref parm1Register);
                    MacReadRegister(MacRegister.HST_INV_ALG_PARM_2, ref parm2Register);

                    // Set up the fixed Q singulation algorithm structure
                    //m_fixedQ.length = sizeof(FixedQParms);
                    m_fixedQ.qValue = parm0Register & 0x0f;
                    m_fixedQ.retryCount = parm1Register & 0xff;
                    m_fixedQ.toggleTarget = (parm2Register & 0x01) != 0 ? (uint)1 : (uint)0;
                    m_fixedQ.repeatUntilNoTags = (parm2Register & 0x02) != 0 ? (uint)1 : (uint)0;
                    break;

                case SingulationAlgorithm.DYNAMICQ:
                    DynamicQParms m_dynQ = (DynamicQParms)parms;

                    // Tell the MAC which singulation algorithm selector to use and then
                    // read the singulation algorithm registers
                    MacWriteRegister(MacRegister.HST_INV_SEL, RFID_18K6C_SINGULATION_ALGORITHM_DYNAMICQ);

                    MacReadRegister(MacRegister.HST_INV_ALG_PARM_0, ref parm0Register);
                    MacReadRegister(MacRegister.HST_INV_ALG_PARM_1, ref parm1Register);
                    MacReadRegister(MacRegister.HST_INV_ALG_PARM_2, ref parm2Register);

                    // Extract the dynamic-Q with Q-adjustment threshold singulation algorithm
                    // parameters
                    //m_dynQ.length = sizeof(DynamicQParms);
                    m_dynQ.startQValue = parm0Register & 0x0f;
                    m_dynQ.minQValue = (parm0Register >> 8) & 0x0f;
                    m_dynQ.maxQValue = (parm0Register >> 4) & 0x0f;
                    m_dynQ.thresholdMultiplier = (parm0Register >> 12) & 0x3f;
                    m_dynQ.retryCount = parm1Register;
                    m_dynQ.toggleTarget = (parm2Register & 0x01) != 0 ? (uint)1 : (uint)0;
                    break;

                default:
                    return Result.INVALID_PARAMETER;
            }

            return Result.OK;
        }
        /// <summary>
        /// Get FixedQ Singulation Algorithm
        /// </summary>
        /// <param name="fixedQ"></param>
        /// <returns></returns>
        public Result GetFixedQParms(FixedQParms fixedQ)
        {
            return (m_Result = GetSingulationAlgorithmParms(SingulationAlgorithm.FIXEDQ, fixedQ));
        }
        /// <summary>
        /// The  parameters  for  the  fixed-Q  algorithm,  MAC  singulation  algorithm  0
        /// If running a same operation, it only need to config once times
        /// </summary>
        /// <param name="QValue">The Q value to use.  Valid values are 0-15, inclusive.</param>
        /// <param name="RetryCount">Specifies the number of times to try another execution 
        /// of the singulation algorithm for the specified 
        /// session/target before either toggling the target (if 
        /// toggleTarget is non-zero) or terminating the 
        /// inventory/tag access operation.  Valid values are 0-
        /// 255, inclusive. Valid values are 0-255, inclusive.</param>
        /// <param name="ToggleTarget"> A non-zero value indicates that the target should
        /// be toggled.A zero value indicates that the target should not be toggled.
        /// Note that if the target is toggled, retryCount and repeatUntilNoTags will also apply
        /// to the new target. </param>
        /// <param name="RepeatUnitNoTags">A flag that indicates whether or not the singulation 
        /// algorithm should continue performing inventory rounds 
        /// until no tags are singulated.  A non-zero value indicates 
        /// that, for each execution of the singulation algorithm, 
        /// inventory rounds should be performed until no tags are 
        /// singulated.  A zero value indicates that a single 
        /// inventory round should be performed for each 
        /// execution of the singulation algorithm.</param>
        public Result SetFixedQParms(uint QValue, uint RetryCount, uint ToggleTarget, uint RepeatUnitNoTags)
        {
            FixedQParms FixedQParm = new FixedQParms();
            FixedQParm.qValue = QValue;      //if only 1 tag read and write, otherwise use 7
            FixedQParm.retryCount = RetryCount;
            FixedQParm.toggleTarget = ToggleTarget;
            FixedQParm.repeatUntilNoTags = RepeatUnitNoTags;

            return (m_Result = SetSingulationAlgorithmParms(SingulationAlgorithm.FIXEDQ, FixedQParm));
        }
        /// <summary>
        /// The  parameters  for  the  fixed-Q  algorithm,  MAC  singulation  algorithm  0
        /// If running a same operation, it only need to config once times
        /// </summary>
        /// <returns></returns>
        public Result SetFixedQParms(FixedQParms fixedQParm)
        {
            return (m_Result = SetSingulationAlgorithmParms(SingulationAlgorithm.FIXEDQ, fixedQParm));
        }
        /// <summary>
        /// The  parameters  for  the  fixed-Q  algorithm,  MAC  singulation  algorithm  0
        /// If running a same operation, it only need to config once times
        /// </summary>
        /// <returns></returns>
        public Result SetFixedQParms()
        {
            FixedQParms FixedQParm = new FixedQParms();
            FixedQParm.qValue = 7;      //if only 1 tag read and write, otherwise use 7
            FixedQParm.retryCount = 0;
            FixedQParm.toggleTarget = 1;
            FixedQParm.repeatUntilNoTags = 1;

            return (m_Result = SetSingulationAlgorithmParms(SingulationAlgorithm.FIXEDQ, FixedQParm));
        }

        /// <summary>
        /// Get DynamicQ Singulation Algorithm
        /// </summary>
        /// <param name="parms"></param>
        /// <returns></returns>
        public Result GetDynamicQParms(DynamicQParms parms)
        {
            return (m_Result = GetSingulationAlgorithmParms(SingulationAlgorithm.DYNAMICQ, parms));
        }
        /// <summary>
        /// The parameters for the dynamic-Q algorithm with application-controlled Q-adjustment-threshold, MAC singulation algorithm 3
        /// </summary>
        /// <param name="StartQValue">The starting Q value to use.  Valid values are 0-15, inclusive.  
        /// startQValue must be greater than or equal to minQValue and 
        /// less than or equal to maxQValue. </param>
        /// <param name="MinQValue">The minimum Q value to use.  Valid values are 0-15, inclusive.  
        /// minQValue must be less than or equal to startQValue and 
        /// maxQValue. </param>
        /// <param name="MaxQValue">The maximum Q value to use.  Valid values are 0-15, inclusive.  
        /// maxQValue must be greater than or equal to startQValue and 
        /// minQValue. </param>
        /// <param name="RetryCount">Specifies the number of times to try another execution of 
        /// the singulation algorithm for the specified session/target 
        /// before either toggling the target (if toggleTarget is non-
        /// zero) or terminating the inventory/tag access operation.  
        /// Valid values are 0-255, inclusive. </param>
        /// <param name="ThresholdMultiplier">The multiplier, specified in units of fourths (i.e., 0.25), that will be 
        /// applied to the Q-adjustment threshold as part of the dynamic-Q 
        /// algorithm.  For example, a value of 7 represents a multiplier of 
        /// 1.75.  See [MAC-EDS] for specifics on how the Q-adjustment 
        /// threshold is used in the dynamic Q algorithm.  Valid values are 0-
        /// 255, inclusive. </param>
        /// <param name="ToggleTarget">A flag that indicates if, after performing the inventory cycle for the 
        /// specified target (i.e., A or B), if the target should be toggled (i.e., 
        /// A to B or B to A) and another inventory cycle run.  A non-zero 
        /// value indicates that the target should be toggled.  A zero value 
        /// indicates that the target should not be toggled.  Note that if the 
        /// target is toggled, retryCount and maxQueryRepCount will 
        /// also apply to the new target. </param>
        public Result SetDynamicQParms(uint StartQValue, uint MinQValue, uint MaxQValue, uint RetryCount, uint ThresholdMultiplier, uint ToggleTarget)
        {
            DynamicQParms dynParm = new DynamicQParms();
            dynParm.startQValue = StartQValue;
            dynParm.maxQValue = MaxQValue;      //if only 1 tag read and write, otherwise use 7
            dynParm.minQValue = MinQValue;
            dynParm.retryCount = RetryCount;
            dynParm.thresholdMultiplier = ThresholdMultiplier;
            dynParm.toggleTarget = ToggleTarget;

            return (m_Result = SetSingulationAlgorithmParms(SingulationAlgorithm.DYNAMICQ, dynParm));
        }
        /// <summary>
        /// The parameters for the dynamic-Q algorithm with application-controlled Q-adjustment-threshold, MAC singulation algorithm 3
        /// </summary>
        /// <returns></returns>
        public Result SetDynamicQParms(DynamicQParms dynParm)
        {
            return (m_Result = SetSingulationAlgorithmParms(SingulationAlgorithm.DYNAMICQ, dynParm));
        }
        /// <summary>
        /// The parameters for the dynamic-Q algorithm with application-controlled Q-adjustment-threshold, MAC singulation algorithm 3
        /// </summary>
        /// <returns></returns>
        public Result SetDynamicQParms()
        {
            DynamicQParms dynParm = new DynamicQParms();
            dynParm.startQValue = 7;
            dynParm.maxQValue = 15;      //if only 1 tag read and write, otherwise use 7
            dynParm.minQValue = 0;
            dynParm.retryCount = 0;
            dynParm.thresholdMultiplier = 4;
            dynParm.toggleTarget = 1;

            return (m_Result = SetSingulationAlgorithmParms(SingulationAlgorithm.DYNAMICQ, dynParm));
        }
#if __NO_DYNAMICQ_
        /// <summary>
        /// Get DynamicQ Singulation Algorithm
        /// </summary>
        /// <param name="dyn"></param>
        /// <returns></returns>
        public Result GetDynamicQParms(DynamicQParms dyn)
        {
            return (m_Result = GetSingulationAlgorithmParms(SingulationAlgorithm.DYNAMICQ, dyn));
        }
        /// <summary>
        /// The parameters for the dynamic-Q algorithm, MAC singulation algorithm 1
        /// </summary>
        /// <param name="StartQValue">The starting Q value to use.  Valid values are 0-15, inclusive. 
        /// startQValue must be greater than or equal to minQValue and 
        /// less than or equal to maxQValue. </param>
        /// <param name="MinQValue">The minimum Q value to use.  Valid values are 0-15, inclusive.  
        /// minQValue must be less than or equal to startQValue and 
        /// maxQValue. </param>
        /// <param name="MaxQValue">The maximum Q value to use.  Valid values are 0-15, inclusive.
        /// maxQValue must be greater than or equal to startQValue and 
        /// minQValue. </param>
        /// <param name="RetryCount">Specifies the number of times to try another execution 
        /// of the singulation algorithm for the specified 
        /// session/target before either toggling the target (if 
        /// toggleTarget is non-zero) or terminating the 
        /// inventory/tag access operation.  Valid values are 0-255, 
        /// inclusive. </param>
        /// <param name="MaxQueryRepCount">The maximum number of ISO 18000-6C QueryRep 
        /// commands that will follow the ISO 18000-6C Query 
        /// command during a single inventory round.  Valid values 
        /// are 0-255, inclusive. </param>
        /// <param name="ToggleTarget"> A flag that indicates if, after performing the inventory cycle for the 
        /// specified target (i.e., A or B), if the target should be toggled (i.e., 
        /// A to B or B to A) and another inventory cycle run.  A non-zero 
        /// value indicates that the target should be toggled.  A zero value 
        /// indicates that the target should not be toggled.  Note that if the 
        /// target is toggled, retryCount and maxQueryRepCount will 
        /// also apply to the new target. </param>
        /// <returns></returns>
        public Result SetDynamicQParms(uint StartQValue, uint MinQValue, uint MaxQValue, uint RetryCount, uint MaxQueryRepCount, uint ToggleTarget)
        {
            DynamicQParms dynParm = new DynamicQParms();
            dynParm.maxQValue = MaxQValue;      //if only 1 tag read and write, otherwise use 7
            dynParm.minQValue = MinQValue;
            dynParm.retryCount = RetryCount;
            dynParm.startQValue = StartQValue;
            dynParm.maxQueryRepCount = MaxQueryRepCount;
            dynParm.toggleTarget = ToggleTarget;

            return (m_Result = SetSingulationAlgorithmParms(SingulationAlgorithm.DYNAMICQ, dynParm));
        }

        /// <summary>
        /// The parameters for the dynamic-Q algorithm, MAC singulation algorithm 1
        /// </summary>
        /// <returns></returns>
        public Result SetDynamicQParms(DynamicQParms dynParm)
        {

            return (m_Result = SetSingulationAlgorithmParms(SingulationAlgorithm.DYNAMICQ, dynParm));
        }
        /// <summary>
        /// The parameters for the dynamic-Q algorithm, MAC singulation algorithm 1
        /// </summary>
        /// <returns></returns>
        public Result SetDynamicQParms()
        {
            DynamicQParms dynParm = new DynamicQParms();
            dynParm.maxQValue = 15;      //if only 1 tag read and write, otherwise use 7
            dynParm.minQValue = 0;
            dynParm.retryCount = 0;
            dynParm.startQValue = 7;
            dynParm.maxQueryRepCount = 10;
            dynParm.toggleTarget = 1;

            return (m_Result = SetSingulationAlgorithmParms(SingulationAlgorithm.DYNAMICQ, dynParm));
        }
#endif
#if __NO_DYNAMIC_ADJQ_
        /// <summary>
        /// Get DynamicQ Adjust Singulation Algorithm
        /// </summary>
        /// <param name="dyn"></param>
        /// <returns></returns>
        public Result GetDynamicQAdjustParms(DynamicQAdjustParms dyn)
        {
            return (m_Result = GetSingulationAlgorithmParms(SingulationAlgorithm.DYNAMICQ_ADJUST, dyn));
        }
        /// <summary>
        /// The parameters for the dynamic-Q algorithm that uses the ISO 18000-6C Query Adjust command, MAC singulation algorithm 2
        /// </summary>
        /// <param name="StartQValue">The starting Q value to use.  Valid values are 0-15, inclusive. 
        /// startQValue must be greater than or equal to minQValue and 
        /// less than or equal to maxQValue. </param>
        /// <param name="MinQValue">The minimum Q value to use.  Valid values are 0-15, inclusive.  
        /// minQValue must be less than or equal to startQValue and 
        /// maxQValue. </param>
        /// <param name="MaxQValue">The maximum Q value to use.  Valid values are 0-15, inclusive.  
        /// maxQValue must be greater than or equal to startQValue and 
        /// minQValue. </param>
        /// <param name="RetryCount">Specifies the number of times to try another execution of 
        /// the singulation algorithm for the specified session/target 
        /// before either toggling the target (if toggleTarget is non-
        /// zero) or terminating the inventory/tag access operation.  
        /// Valid values are 0-255, inclusive. </param>
        /// <param name="MaxQueryRepCount">The maximum number of ISO 18000-6C QueryRep 
        /// commands that will follow the ISO 18000-6C Query 
        /// command during a single inventory round.  Valid values 
        /// are 0-255, inclusive.</param>
        /// <param name="ToggleTarget">A flag that indicates if, after performing the inventory cycle for the 
        /// specified target (i.e., A or B), if the target should be toggled (i.e., A 
        /// to B or B to A) and another inventory cycle run.  A non-zero value 
        /// indicates that the target should be toggled.  A zero value indicates 
        /// that the target should not be toggled.  Note that if the target is 
        /// toggled, retryCount and maxQueryRepCount will also apply to the 
        /// new target. </param>
        public Result SetDynamicQAdjustParms(uint StartQValue, uint MinQValue, uint MaxQValue, uint RetryCount, uint MaxQueryRepCount, uint ToggleTarget)
        {
            DynamicQAdjustParms dynParm = new DynamicQAdjustParms();
            dynParm.maxQValue = MaxQValue;      //if only 1 tag read and write, otherwise use 7
            dynParm.minQValue = MinQValue;
            dynParm.retryCount = RetryCount;
            dynParm.startQValue = StartQValue;
            dynParm.maxQueryRepCount = MaxQueryRepCount;
            dynParm.toggleTarget = ToggleTarget;
            m_Result = SetCurrentSingulationAlgorithm(SingulationAlgorithm.DYNAMICQ_ADJUST);
            if (m_Result != Result.OK) return m_Result;
            return (m_Result = SetSingulationAlgorithmParms(SingulationAlgorithm.DYNAMICQ_ADJUST, dynParm));
        }
        /// <summary>
        /// The parameters for the dynamic-Q algorithm that uses the ISO 18000-6C Query Adjust command, MAC singulation algorithm 2
        /// </summary>
        public Result SetDynamicQAdjustParms(DynamicQAdjustParms dynParm)
        {
            m_Result = SetCurrentSingulationAlgorithm(SingulationAlgorithm.DYNAMICQ_ADJUST);
            if (m_Result != Result.OK) return m_Result;
            return (m_Result = SetSingulationAlgorithmParms(SingulationAlgorithm.DYNAMICQ_ADJUST, dynParm));
        }
        /// <summary>
        /// The parameters for the dynamic-Q algorithm that uses the ISO 18000-6C Query Adjust command, MAC singulation algorithm 2
        /// </summary>
        public Result SetDynamicQAdjustParms()
        {
            DynamicQAdjustParms dynParm = new DynamicQAdjustParms();
            dynParm.maxQValue = 15;      //if only 1 tag read and write, otherwise use 7
            dynParm.minQValue = 0;
            dynParm.retryCount = 0;
            dynParm.startQValue = 7;
            dynParm.maxQueryRepCount = 10;
            dynParm.toggleTarget = 1;
            m_Result = SetCurrentSingulationAlgorithm(SingulationAlgorithm.DYNAMICQ_ADJUST);
            if (m_Result != Result.OK) return m_Result;
            return (m_Result = SetSingulationAlgorithmParms(SingulationAlgorithm.DYNAMICQ_ADJUST, dynParm));
        }
#endif
        #endregion

        #region ====================== Set Tag Group ======================
        /// <summary>
        /// Get Tag Group
        /// </summary>
        /// <param name="gpSelect"></param>
        /// <returns></returns>
        public Result SetTagGroup(Selected gpSelect)
        {
            UInt32 value = 0;

            MacReadRegister(MacRegister.HST_QUERY_CFG, ref value);

            value &= ~0x0180U;
            value |= ((uint)gpSelect << 7);

            MacWriteRegister(MacRegister.HST_QUERY_CFG, value);

            return Result.OK;
        }

        /// <summary>
        /// Once the tag population has been partitioned into disjoint groups, a subsequent 
        /// tag-protocol operation (i.e., an inventory operation or access command) is then 
        /// applied to one of the tag groups. 
        /// </summary>
        /// <param name="gpSelect">Specifies the state of the selected (SL) flag for tags that will have 
        /// the operation applied to them. </param>
        /// <param name="gpSession">Specifies which inventory session flag (i.e., S0, S1, S2, or S3) 
        /// will be matched against the inventory state specified by target. </param>
        /// <param name="gpSessionTarget">Specifies the state of the inventory session flag (i.e., A or B),
        /// specified by session, for tags that will have the operation 
        /// applied to them. </param>
        public Result SetTagGroup(Selected gpSelect, Session gpSession, SessionTarget gpSessionTarget)
        {
            UInt32 value = 0;

            DEBUG_WriteLine(DEBUGLEVEL.API, "HighLevelInterface.SetTagGroup(Selected gpSelect, Session gpSession, SessionTarget gpSessionTarget)");

            if ((m_Result = MacReadRegister(MacRegister.HST_QUERY_CFG, ref value)) != Result.OK)
                return m_Result;

            value &= ~0x01f0U;
            value |= ((uint)gpSessionTarget << 4) | ((uint)gpSession << 5) | ((uint)gpSelect << 7);

            m_save_taggroup.selected = gpSelect;
            m_save_taggroup.session = gpSession;
            m_save_taggroup.target = gpSessionTarget;

            return (m_Result = MacWriteRegister(MacRegister.HST_QUERY_CFG, value));

/*            return (m_Result = MacWriteRegister(MacRegister.HST_QUERY_CFG,
                (uint)gpSessionTarget << 4 |
                (uint)gpSession << 5 |
                (uint)gpSelect << 7));*/
        }
        /// <summary>
        /// Once the tag population has been partitioned into disjoint groups, a subsequent 
        /// tag-protocol operation (i.e., an inventory operation or access command) is then 
        /// applied to one of the tag groups. 
        /// </summary>
        /// <param name="tagGroup"><see cref="TagGroup"/></param>
        /// <returns></returns>
        public Result SetTagGroup(TagGroup tagGroup)
        {
            UInt32 value = 0;

            DEBUG_WriteLine(DEBUGLEVEL.API, "HighLevelInterface.SetTagGroup(TagGroup tagGroup)");

            if ((m_Result = MacReadRegister(MacRegister.HST_QUERY_CFG, ref value)) != Result.OK)
                return m_Result;

            value &= ~0x01f0U;
            value |= ((uint)tagGroup.target << 4) | ((uint)tagGroup.session << 5) | ((uint)tagGroup.selected << 7);

            m_save_taggroup.selected = tagGroup.selected;
            m_save_taggroup.session = tagGroup.session;
            m_save_taggroup.target = tagGroup.target;

            return (m_Result = MacWriteRegister(MacRegister.HST_QUERY_CFG, value));

/*            return (m_Result = MacWriteRegister(MacRegister.HST_QUERY_CFG,
                (uint)tagGroup.target << 4 |
                (uint)tagGroup.session << 5 |
                (uint)tagGroup.selected << 7));
*/
        }
        /// <summary>
        /// Get Tag Group
        /// </summary>
        /// <param name="tagGroup"></param>
        /// <returns></returns>
        public Result GetTagGroup(TagGroup tagGroup)
        {
            //UInt16 HST_QUERY_CFG = 0x0900;

            UInt32 registerValue = 0;

            DEBUG_WriteLine(DEBUGLEVEL.API, "HighLevelInterface.GetTagGroup(TagGroup tagGroup)");

            if (MacReadRegister(MacRegister.HST_QUERY_CFG, ref registerValue) != Result.OK)
                return Result.FAILURE;

            tagGroup.selected = (Selected)((registerValue >> 7) & 0x03);
            tagGroup.session = (Session)((registerValue >> 5) & 0x03);
            tagGroup.target = (SessionTarget)((registerValue >> 4) & 0x01);

            return (m_Result = Result.OK);
        }
        #endregion

        #region ====================== Set Power Level ======================
        /// <summary>
        /// Get measurement of the PA output power level measured in 0.1 dBm units. 
        /// </summary>
        /// <returns>Result</returns>
        public Result GetForwardPowerLevel(ref uint PowerLevel)
        {
            //return (m_Result = MacReadRegister(MAC_RFTC_PAPWRLEV, ref PowerLevel));
            return (m_Result = MacReadRegister(MacRegister.MAC_RFTC_PAPWRLEV, ref PowerLevel));
        }
        /// <summary>
        /// Get measurement of the reflected RF power measured in 0.1 dBm units.
        /// </summary>
        /// <returns>Result</returns>
        public Result GetReversedPowerLevel(ref int PowerLevel)
        {
            uint value = 0;
            Result result;

            result = MacReadRegister(MacRegister.MAC_RFTC_REVPWRLEV, ref value);
            PowerLevel = (int)value; // convert to int
            return result;

            //return (m_Result = MacReadRegister(MAC_RFTC_REVPWRLEV, ref PowerLevel));
            //return (m_Result = MacReadRegister(MacRegister.MAC_RFTC_REVPWRLEV, ref (uint)PowerLevel));
        }
        /// <summary>
        /// Get the maximum PA output power level measured in 0.1 dBm units. 
        /// </summary>
        /// <param name="MaxPowerLevel"></param>
        /// <returns></returns>
        public Result GetMaxForwardPowerLevel(ref uint MaxPowerLevel)
        {
            return ReturnState(MacReadOemData (0xA5, ref MaxPowerLevel));
        }
        /// <summary>
        /// Set the maximum PA output power level measured in 0.1 dBm units. 
        /// </summary>
        /// <param name="MaxPowerLevel"></param>
        /// <returns></returns>
        public Result SetMaxForwardPowerLevel(uint MaxPowerLevel)
        {
            return ReturnState(MacWriteOemData(0xA5, MaxPowerLevel));
        }
        /// <summary>
        /// SetTemperatureCompensation
        /// </summary>
        /// <param name="coeff0">
        /// Temperature compensation coeff0. temperature_error = (coeff1 * temp) + 
        /// coeff0.
        /// <para>bit 31 – sign bit (0=pos, 1=neg)</para>
        /// <para>bits 30:0 – coeff0, in units of 0.001dB </para></param>
        /// <param name="coeff1">
        /// Temperature compensation coeff1. temp_induced_error =
        /// (coeff1 * temperature) + coeff0 
        /// <para>bit 31 – sign bit (0=pos, 1=neg) </para>
        /// <para>bits 30:0 – coeff1, in units of 0.001dB/°C </para></param>
        /// <returns></returns>
        public Result SetTemperatureCompensation(uint coeff0, uint coeff1)
        {
            if (ReturnState(MacWriteOemData(0xAF, coeff0)) != Result.OK)
                return m_Result;
            return ReturnState(MacWriteOemData(0xB0, coeff1));
        }
        /// <summary>
        /// GetTemperatureCompensation
        /// </summary>
        /// <param name="coeff0">
        /// Temperature compensation coeff0. temperature_error = (coeff1 * temp) + 
        /// coeff0.
        /// <para>bit 31 – sign bit (0=pos, 1=neg)</para>
        /// <para>bits 30:0 – coeff0, in units of 0.001dB</para></param>
        /// <param name="coeff1">
        /// Temperature compensation coeff1. temp_induced_error =
        /// (coeff1 * temperature) + coeff0 
        /// <para>bit 31 – sign bit (0=pos, 1=neg) </para>
        /// <para>bits 30:0 – coeff1, in units of 0.001dB/°C </para></param>
        /// <returns></returns>
        public Result GetTemperatureCompensation(ref uint coeff0, ref uint coeff1)
        {
            if (ReturnState(MacReadOemData(0xAF, ref coeff0)) != Result.OK)
                return m_Result;
            return ReturnState(MacReadOemData(0xB0, ref coeff1));
        }
        /// <summary>
        /// SetFrequencyCompensation
        /// </summary>
        /// <param name="coeff0">
        /// Frequency compensation coeff0. freq_induced_error = (coeff1 * freq) + 
        /// coeff0.
        /// <para>bit 31 – sign bit (0=pos, 1=neg) </para>
        /// <para>bits 30:0 – coeff0, in units of 0.001dB </para></param>
        /// <param name="coeff1">
        /// Frequency compensation coeff1. freq_induced_error =
        /// (coeff1 * freq) + coeff0 
        /// <para>bit 31 – sign bit (0=pos, 1=neg) </para>
        /// <para>bits 30:0 – coeff1, in units of 0.000001dB/MHz </para></param>
        /// <returns></returns>
        public Result SetFrequencyCompensation(uint coeff0, uint coeff1)
        {
            if (ReturnState(MacWriteOemData(0xB2, coeff0)) != Result.OK)
                return m_Result;
            return ReturnState(MacWriteOemData(0xB3, coeff1));
        }
        /// <summary>
        /// GetFrequencyCompensation
        /// </summary>
        /// <param name="coeff0">
        /// Frequency compensation coeff0. freq_induced_error = (coeff1 * freq) + 
        /// coeff0.
        /// <para>bit 31 – sign bit (0=pos, 1=neg) </para>
        /// <para>bits 30:0 – coeff0, in units of 0.001dB </para></param>
        /// <param name="coeff1">
        /// Frequency compensation coeff1. freq_induced_error =
        /// (coeff1 * freq) + coeff0 
        /// <para>bit 31 – sign bit (0=pos, 1=neg) </para>
        /// <para>bits 30:0 – coeff1, in units of 0.000001dB/MHz </para></param>
        /// <returns></returns>
        public Result GetFrequencyCompensation(ref uint coeff0, ref uint coeff1)
        {
            if (ReturnState(MacReadOemData(0xB2, ref coeff0)) != Result.OK)
                return m_Result;
            return ReturnState(MacReadOemData(0xB3, ref coeff1));
        }
        /// <summary>
        /// Turn on/off compensate for temperature and frequency
        /// </summary>
        /// <returns></returns>
        public Result SetRfidCompensation(bool enable)
        {
            return ReturnState(MacWriteOemData(0xA1, (uint)(enable ? 0x3 : 0x0)));
        }
        /// <summary>
        /// Get Compensate status
        /// </summary>
        /// <returns></returns>
        public Result GetRfidCompensation(ref bool enable)
        {
            uint data = 0;
            ReturnState(MacReadOemData(0xA1, ref data));
            enable = data == 0x3 ? true : false;
            return m_Result;
        }
        #endregion

        #region ====================== Set Temperature ======================
        /// <summary>
        /// GetThresholdTemperature
        /// </summary>
        /// <returns>Result</returns>
        public Result GetThresholdTemperature(ref ThresholdTemperatureParms temp)
        {
            uint value = 0;
            ThresholdTemperatureParms Temp = new ThresholdTemperatureParms();
            m_Result = MacReadRegister(MacRegister.HST_RFTC_AMBIENTTEMPTHRSH, ref value);//ambient
            if (m_Result != Result.OK)
            {
                return m_Result;
            }
            Temp.amb = value;
            m_Result = MacReadRegister(MacRegister.HST_RFTC_XCVRTEMPTHRSH, ref value);//ambient
            if (m_Result != Result.OK)
            {
                return m_Result;
            }
            Temp.xcvr = value;
            m_Result = MacReadRegister(MacRegister.HST_RFTC_PATEMPTHRSH, ref value);//ambient
            if (m_Result != Result.OK)
            {
                return m_Result;
            }
            Temp.pwramp = value;
            m_Result = MacReadRegister(MacRegister.HST_RFTC_PADELTATEMPTHRSH, ref value);//ambient
            if (m_Result != Result.OK)
            {
                return m_Result;
            }
            Temp.delta = value;
            temp = Temp;
            return Result.OK;
        }
        /// <summary>
        /// No Use, use system default value instead
        /// SetThresholdTemperature
        /// </summary>
        /// <param name="ambient"></param>
        /// <param name="xcver"></param>
        /// <param name="patemp"></param>
        /// <param name="padeta"></param>
        /// <returns></returns>
        public Result SetThresholdTemperature(uint ambient, uint xcver, uint patemp, uint padeta)
        {
            m_Result = MacWriteRegister(MacRegister.HST_RFTC_AMBIENTTEMPTHRSH, ambient);
            if (m_Result != Result.OK)
                return m_Result; //throw new ReaderException(iRes.ToString());
            m_Result = MacWriteRegister(MacRegister.HST_RFTC_XCVRTEMPTHRSH, xcver);
            if (m_Result != Result.OK)
                return m_Result; //throw new ReaderException(iRes.ToString());
            m_Result = MacWriteRegister(MacRegister.HST_RFTC_PATEMPTHRSH, patemp);
            if (m_Result != Result.OK)
                return m_Result; //throw new ReaderException(iRes.ToString());
            m_Result = MacWriteRegister(MacRegister.HST_RFTC_PADELTATEMPTHRSH, padeta);
            if (m_Result != Result.OK)
                return m_Result; //throw new ReaderException(iRes.ToString());
            return Result.OK;
        }

        /// <summary>
        /// Get Current System Temperature
        /// Don't get temperature during operation starting
        /// </summary>
        public Result GetCurrentTemperature(ref TemperatureParms temp)
        {
            uint value = 0;
            TemperatureParms Temp = new TemperatureParms();
            m_Result = MacReadRegister(MacRegister.MAC_RFTC_AMBIENTTEMP, ref value);//ambient
            if (m_Result != Result.OK)
            {
                return m_Result;
            }
            Temp.amb = value;
            m_Result = MacReadRegister(MacRegister.MAC_RFTC_XCVRTEMP, ref value);//ambient
            if (m_Result != Result.OK)
            {
                return m_Result;
            }
            Temp.xcvr = value;
            m_Result = MacReadRegister(MacRegister.MAC_RFTC_PATEMP, ref value);//ambient
            if (m_Result != Result.OK)
            {
                return m_Result;
            }
            Temp.pwramp = value;
            temp = Temp;
            return m_Result;
        }
        #endregion

        #region ====================== Frequency function ======================

        #region Private Function
        //private readonly UInt16 SELECTOR_ADDRESS = 0xC01;
        //private readonly UInt16 CONFIG_ADDRESS = 0xC02;
        //private readonly UInt16 MULTDIV_ADDRESS = 0xC03;
        //private readonly UInt16 PLLCC_ADDRESS = 0xC04;
        private readonly Double ClockKHz = 24000.0;

        //uint pllvalue = 0x14070700;

        private uint GetPllcc(RegionCode prof)
        {
            switch (prof)
            {
                case RegionCode.G800:
                case RegionCode.ETSI:
                case RegionCode.IN:
                    return 0x14070400;
            }

            return 0x14070200;
            //return pllvalue;

#if NO_USE
            switch (prof)
            {
                case RegionCode.G800:
                case RegionCode.ETSI:
                case RegionCode.IN:
                    return 0x14040400;
                /*case RegionCode.JP:
                case RegionCode.FCC:
                case RegionCode.CN:
                case RegionCode.TW:
                case RegionCode.KR:
                case RegionCode.HK:
                case RegionCode.SG:
                case RegionCode.AU:
                case RegionCode.MY:
                case RegionCode.ZA:
                case RegionCode.BR1:
                case RegionCode.BR2:*/
                default:
                    return 0x14020200;
            }
#endif
        }

        private uint FreqChnCnt(RegionCode prof)
        {
            switch (prof)
            {
                case RegionCode.AR:
                case RegionCode.CL:
                case RegionCode.CO:
                case RegionCode.CR:
                case RegionCode.DO:
                case RegionCode.PA:
                case RegionCode.UY:
                case RegionCode.MX:
                case RegionCode.FCC:
                    return FCC_CHN_CNT;
                case RegionCode.CN:
                    return CN_CHN_CNT;
                case RegionCode.TW:
                    return TW_CHN_CNT;
                case RegionCode.TW2:
                    return TW2_CHN_CNT;
                case RegionCode.KR:
                    return KR_CHN_CNT;
                case RegionCode.HK:
                case RegionCode.HK50:
                    return OFCA_CHN_CNT;
                case RegionCode.SG:
                case RegionCode.TH:
                case RegionCode.VI:
                case RegionCode.HK8:
                    return HK_CHN_CNT;
                case RegionCode.AU:
                    return AUS_CHN_CNT;
                case RegionCode.MY:
                    return MYS_CHN_CNT;
                case RegionCode.G800:
                case RegionCode.ETSI:
                    return ETSI_CHN_CNT;
                case RegionCode.IN:
                    return IDA_CHN_CNT;
                case RegionCode.JP:
                    if (m_oem_special_country_version != 0x2A4A5036)
//                    if (OEMChipSetID == ChipSetID.R1000)
                        return JPN2012_CHN_CNT;
                    else
                        return JPN2019_CHN_CNT;
                case RegionCode.ZA:
                    return ZA_CHN_CNT;
                case RegionCode.BR1:
                    return BR1_CHN_CNT;
                case RegionCode.PE:
                case RegionCode.BR2:
                    return BR2_CHN_CNT;
                case RegionCode.BR3:
                    return BR3_CHN_CNT;
                case RegionCode.BR4:
                    return BR4_CHN_CNT;
                case RegionCode.BR5:
                    return BR5_CHN_CNT;
                case RegionCode.ID:
                    return ID_CHN_CNT;
                case RegionCode.JE:
                    return JE_CHN_CNT;
                case RegionCode.PH:
                    return PH_CHN_CNT;
                case RegionCode.ETSIUPPERBAND:
                    return ETSIUPPERBAND_CHN_CNT;
                case RegionCode.NZ:
                    return NZ_CHN_CNT;
                case RegionCode.UH1:
                    return UH1_CHN_CNT;
                case RegionCode.UH2:
                    return UH2_CHN_CNT;
                case RegionCode.LH:
                    return LH_CHN_CNT;
                case RegionCode.LH1:
                    return LH1_CHN_CNT;
                case RegionCode.LH2:
                    return LH2_CHN_CNT;
                case RegionCode.VE:
                    return VE_CHN_CNT;
                case RegionCode.SAHOPPING:
                    return SAHopping_CHN_CNT;
                default:
                    return 0;
                //break;
            }
            //return 0;
        }

#if R1000
        private uint FreqChnCnt(RegionCode prof)
        {
            switch (prof)
            {
                case RegionCode.FCC:
                    return FCC_CHN_CNT;
                case RegionCode.CN:
                    return CN_CHN_CNT;
                case RegionCode.CN1:
                case RegionCode.CN2:
                case RegionCode.CN3:
                case RegionCode.CN4:
                case RegionCode.CN5:
                case RegionCode.CN6:
                case RegionCode.CN7:
                case RegionCode.CN8:
                    return CN1_CHN_CNT;
                case RegionCode.CN9:
                case RegionCode.CN10:
                case RegionCode.CN11:
                case RegionCode.CN12:
                    return CN9_CHN_CNT;
                case RegionCode.TW:
                    return TW_CHN_CNT;
                case RegionCode.KR:
                    return KR_CHN_CNT;
                case RegionCode.HK:
                case RegionCode.SG:
                case RegionCode.TH:
                    return HK_CHN_CNT;
                case RegionCode.AU:
                    return AUS_CHN_CNT;
                case RegionCode.MY:
                    return MYS_CHN_CNT;
                case RegionCode.G800:
                case RegionCode.ETSI:
                    return ETSI_CHN_CNT;
                case RegionCode.IN:
                    return IDA_CHN_CNT;
                case RegionCode.JP:
                    switch (m_oem_machine)
                    {
                        case Machine.CS101:
                        case Machine.CS203:
                            return JPN_CHN_CNT28;
                        case Machine.CS468:
                            return JPN_CHN_CNT29;

                    }
                    return JPN_CHN_CNT29;

                case RegionCode.JP2012:
                    return JPN2012_CHN_CNT;
                case RegionCode.ZA:
                    return ZA_CHN_CNT;
                case RegionCode.BR1:
                    return BR1_CHN_CNT;
                case RegionCode.BR2:
                    return BR2_CHN_CNT;
                case RegionCode.ID:
                    return ID_CHN_CNT;
                case RegionCode.UH1:
                    return UH1_CHN_CNT;
                case RegionCode.UH2:
                    return UH2_CHN_CNT;
                case RegionCode.LH:
                    return LH_CHN_CNT;
                case RegionCode.JE:
                    return JE_CHN_CNT;
                case RegionCode.PH:
                    return PH_CHN_CNT;
                case RegionCode.ETSIUPPERBAND:
                    return ETSIUPPERBAND_CHN_CNT;
                case RegionCode.NZ:
                    return NZ_CHN_CNT;
                default:
                    return 0;
                //break;
            }
            //return 0;
        }
#endif

        private bool FreqChnWithinRange(uint Channel, RegionCode region)
        {
            uint ChnCnt = FreqChnCnt(region);
            if (ChnCnt < 0)
                return false;
            if (Channel >= 0 && Channel < ChnCnt)
            {
                return true;
            }
            return false;
        }

        private uint[] FreqTable(RegionCode prof)
        {
            switch (prof)
            {
                case RegionCode.AR:
                case RegionCode.CL:
                case RegionCode.CO:
                case RegionCode.CR:
                case RegionCode.DO:
                case RegionCode.PA:
                case RegionCode.UY:
                case RegionCode.MX:
                case RegionCode.FCC:
                    switch (m_oem_table_version)
                    {
                        default:
                            return fccFreqTable;

                        case 0x20170001:
                            return fccFreqTable_Ver20170001;
                    }
                case RegionCode.CN:
                    return cnFreqTable;
                    return cn12FreqTable;
                case RegionCode.TW:
                    return twFreqTable;
                case RegionCode.TW2:
                    return tw2FreqTable;
                case RegionCode.KR:
                    return krFreqTable;
                case RegionCode.HK:
                case RegionCode.HK50:
                    return ofcaFreqTable;
                case RegionCode.SG:
                case RegionCode.TH:
                case RegionCode.VI:
                case RegionCode.HK8:
                    return hkFreqTable;
                case RegionCode.AU:
                    return AusFreqTable;
                case RegionCode.MY:
                    return mysFreqTable;
                case RegionCode.G800:
                case RegionCode.ETSI:
                    return etsiFreqTable;
                case RegionCode.IN:
                    return indiaFreqTable;
                case RegionCode.JP:
                    if (m_oem_special_country_version != 0x2A4A5036)
//                    if (OEMChipSetID == ChipSetID.R1000)
                        return jpn2012FreqTable;
                    else
                        return jpn2019FreqTable;
                case RegionCode.ZA:
                    return zaFreqTable;
                case RegionCode.BR1:
                    return br1FreqTable;
                case RegionCode.PE:
                case RegionCode.BR2:
                    return br2FreqTable;
                case RegionCode.BR3:
                    return br3FreqTable;
                case RegionCode.BR4:
                    return br4FreqTable;
                case RegionCode.BR5:
                    return br5FreqTable;
                case RegionCode.ID:
                    return indonesiaFreqTable;
                case RegionCode.JE:
                    return jeFreqTable;
                case RegionCode.PH:
                    return phFreqTable;
                case RegionCode.ETSIUPPERBAND:
                    return etsiupperbandFreqTable;
                case RegionCode.NZ:
                    return nzFreqTable;
                case RegionCode.UH1:
                    return uh1FreqTable;
                case RegionCode.UH2:
                    return uh2FreqTable;
                case RegionCode.LH:
                    return lhFreqTable;
                case RegionCode.LH1:
                    return lh1FreqTable;
                case RegionCode.LH2:
                    return lh2FreqTable;
                case RegionCode.VE:
                    return veFreqTable;
                case RegionCode.SAHOPPING:
                    return SAHoppingFreqTable;
                default:
                    return null;
                //break;
            }
            //return null;
        }

#if R1000
        private uint[] FreqTable(RegionCode prof)
        {
            switch (prof)
            {
                case RegionCode.FCC:
                    return fccFreqTable;
                case RegionCode.CN:
                    return cnFreqTable;
                case RegionCode.CN1:
                    return cn1FreqTable;
                case RegionCode.CN2:
                    return cn2FreqTable;
                case RegionCode.CN3:
                    return cn3FreqTable;
                case RegionCode.CN4:
                    return cn4FreqTable;
                case RegionCode.CN5:
                    return cn5FreqTable;
                case RegionCode.CN6:
                    return cn6FreqTable;
                case RegionCode.CN7:
                    return cn7FreqTable;
                case RegionCode.CN8:
                    return cn8FreqTable;
                case RegionCode.CN9:
                    return cn9FreqTable;
                case RegionCode.CN10:
                    return cn10FreqTable;
                case RegionCode.CN11:
                    return cn11FreqTable;
                case RegionCode.CN12:
                    return cn12FreqTable;
                case RegionCode.TW:
                    return twFreqTable;
                case RegionCode.KR:
                    return krFreqTable;
                case RegionCode.HK:
                case RegionCode.SG:
                case RegionCode.TH:
                    return hkFreqTable;
                case RegionCode.AU:
                    return AusFreqTable;
                case RegionCode.MY:
                    return mysFreqTable;
                case RegionCode.G800:
                case RegionCode.ETSI:
                    return etsiFreqTable;
                case RegionCode.IN:
                    return indiaFreqTable;
                case RegionCode.JP:
                    switch (m_oem_machine)
                    {
                        case Machine.CS101:
                        case Machine.CS203:
                            return jpnFreqTable28;
                        case Machine.CS468:
                            return jpnFreqTable29;
                    }
                    return jpnFreqTable;
                case RegionCode.JP2012:
                    return jpn2012FreqTable;
                case RegionCode.ZA:
                    return zaFreqTable;
                case RegionCode.BR1:
                    return br1FreqTable;
                case RegionCode.BR2:
                    return br2FreqTable;
                case RegionCode.ID:
                    return indonesiaFreqTable;
                case RegionCode.UH1:
                    return uh1FreqTable;
                case RegionCode.UH2:
                    return uh2FreqTable;
                case RegionCode.LH:
                    return lhFreqTable;
                case RegionCode.JE:
                    return jeFreqTable;
                case RegionCode.PH:
                    return phFreqTable;
                case RegionCode.ETSIUPPERBAND:
                    return etsiupperbandFreqTable;
                case RegionCode.NZ:
                    return nzFreqTable;
                default:
                    return null;
                //break;
            }
            //return null;
        }
#endif

        private uint[] RevFreqIndex(RegionCode prof)
        {
            uint[] FreRevqSortedIdx = new uint[FreqChnCnt(prof)];
            uint[] FreqSortedIdx = FreqIndex(prof);

            for (int cnt = 0; cnt < FreRevqSortedIdx.Length; cnt ++)
            {
                FreRevqSortedIdx[FreqSortedIdx[cnt]] = (uint)cnt;
            }

            return FreRevqSortedIdx;
        }

        private uint[] FreqIndex(RegionCode prof)
        {
            switch (prof)
            {
                case RegionCode.AR:
                case RegionCode.CL:
                case RegionCode.CO:
                case RegionCode.CR:
                case RegionCode.DO:
                case RegionCode.PA:
                case RegionCode.UY:
                case RegionCode.MX:
                case RegionCode.FCC:
                    switch (m_oem_table_version)
                    {
                        default:
                            return fccFreqSortedIdx;

                        case 0x20170001:
                            return fccFreqSortedIdx_Ver20170001;
                    }
                case RegionCode.CN:
                    return cnFreqSortedIdx;
                case RegionCode.TW:
                    return twFreqSortedIdx;
                case RegionCode.TW2:
                    return tw2FreqSortedIdx;
                case RegionCode.KR:
                    return krFreqSortedIdx;
                case RegionCode.HK:
                case RegionCode.HK50:
                    return ofcaFreqSortedIdx;
                case RegionCode.SG:
                case RegionCode.TH:
                case RegionCode.VI:
                case RegionCode.HK8:
                    return hkFreqSortedIdx;
                case RegionCode.AU:
                    return ausFreqSortedIdx;
                case RegionCode.MY:
                    return mysFreqSortedIdx;
                case RegionCode.G800:
                case RegionCode.ETSI:
                    return etsiFreqSortedIdx;
                case RegionCode.IN:
                    return indiaFreqSortedIdx;
                case RegionCode.JP:
                    if (m_oem_special_country_version != 0x2A4A5036)
//                    if (OEMChipSetID == ChipSetID.R1000)
                        return jpn2012FreqSortedIdx;
                    else
                        return jpn2019FreqSortedIdx;
                case RegionCode.ZA:
                    return zaFreqSortedIdx;
                case RegionCode.BR1:
                    return br1FreqSortedIdx;
                case RegionCode.PE:
                case RegionCode.BR2:
                    return br2FreqSortedIdx;
                case RegionCode.BR3:
                    return br3FreqSortedIdx;
                case RegionCode.BR4:
                    return br4FreqSortedIdx;
                case RegionCode.BR5:
                    return br5FreqSortedIdx;
                case RegionCode.ID:
                    return indonesiaFreqSortedIdx;
                case RegionCode.JE:
                    return jeFreqSortedIdx;
                case RegionCode.PH:
                    return phFreqSortedIdx;
                case RegionCode.ETSIUPPERBAND:
                    return etsiupperbandFreqSortedIdx;
                case RegionCode.NZ:
                    return nzFreqSortedIdx;
                case RegionCode.UH1:
                    return uh1FreqSortedIdx;
                case RegionCode.UH2:
                    return uh2FreqSortedIdx;
                case RegionCode.LH:
                    return lhFreqSortedIdx;
                case RegionCode.LH1:
                    return lh1FreqSortedIdx;
                case RegionCode.LH2:
                    return lh2FreqSortedIdx;
                case RegionCode.VE:
                    return veFreqSortedIdx;
                case RegionCode.SAHOPPING:
                    return SAHoppingFreqSortedIdx;
            }

            return null;
        }

#if R1000
        private uint[] FreqIndex(RegionCode prof)
        {
            switch (prof)
            {
                case RegionCode.FCC:
                    return fccFreqSortedIdx;
                case RegionCode.CN:
                    return cnFreqSortedIdx;
                case RegionCode.CN1:
                case RegionCode.CN2:
                case RegionCode.CN3:
                case RegionCode.CN4:
                case RegionCode.CN5:
                case RegionCode.CN6:
                case RegionCode.CN7:
                case RegionCode.CN8:
                    return cn1FreqSortedIdx;
                case RegionCode.CN9:
                case RegionCode.CN10:
                case RegionCode.CN11:
                case RegionCode.CN12:
                    return cn9FreqSortedIdx;
                case RegionCode.TW:
                    return twFreqSortedIdx;
                case RegionCode.KR:
                    return krFreqSortedIdx;
                case RegionCode.HK:
                case RegionCode.SG:
                case RegionCode.TH:
                    return hkFreqSortedIdx;
                case RegionCode.AU:
                    return ausFreqSortedIdx;
                case RegionCode.MY:
                    return mysFreqSortedIdx;
                case RegionCode.G800:
                case RegionCode.ETSI:
                    return etsiFreqSortedIdx;
                case RegionCode.IN:
                    return indiaFreqSortedIdx;
                case RegionCode.JP:
                    switch (m_oem_machine)
                    {
                        case Machine.CS101:
                        case Machine.CS203:
                            return jpnFreqSortedIdx28;
                        case Machine.CS468:
                            return jpnFreqSortedIdx29;
                    }
                    return jpnFreqSortedIdx;

                case RegionCode.JP2012:
                    return jpn2012FreqSortedIdx;

                case RegionCode.ZA:
                    return zaFreqSortedIdx;
                case RegionCode.BR1:
                    return br1FreqSortedIdx;
                case RegionCode.BR2:
                    return br2FreqSortedIdx;
                case RegionCode.ID:
                    return indonesiaFreqSortedIdx;
                case RegionCode.UH1:
                    return uh1FreqSortedIdx;
                case RegionCode.UH2:
                    return uh2FreqSortedIdx;
                case RegionCode.LH:
                    return lhFreqSortedIdx;
                case RegionCode.JE:
                    return jeFreqSortedIdx;
                case RegionCode.PH:
                    return phFreqSortedIdx;
                case RegionCode.ETSIUPPERBAND:
                    return etsiupperbandFreqSortedIdx;
                case RegionCode.NZ:
                    return nzFreqSortedIdx;
            }

            return null;
        }
#endif

        private int FreqSortedIdxTbls(RegionCode Prof, uint Channel)
        {
            uint TotalCnt = FreqChnCnt(Prof);
            uint[] freqIndex = FreqIndex(Prof);
            if (!FreqChnWithinRange(Channel, Prof) || freqIndex == null)
                return -1;
            for (int i = 0; i < TotalCnt; i++)
            {
                if (freqIndex[i] == Channel)
                {
                    return i;
                }
            }
            return -1;
        }

        private Result CheckPowerLvl(RegionCode prof)
        {
            uint pwr = 0;
            Result m_Result = GetPowerLevel(ref pwr);

            if (m_Result != Result.OK)
                return m_Result;

            if (pwr > GetSoftwareMaxPowerLevel (prof))
                return SetPowerLevel(GetSoftwareMaxPowerLevel (prof));

            return Result.OK;
        }

        private void GenCountryList()
        {
            m_save_country_list.Clear();


            // CS203-2 set to CS203-2RW frequency set 
            if (m_oem_machine == Machine.CS203 && m_save_country_code == 2 && m_save_country_code == 0)
            {
                m_save_country_list.Add(RegionCode.AR);
                m_save_country_list.Add(RegionCode.BR1);
                m_save_country_list.Add(RegionCode.BR2);
                m_save_country_list.Add(RegionCode.BR3);
                m_save_country_list.Add(RegionCode.BR4);
                m_save_country_list.Add(RegionCode.BR5);
                m_save_country_list.Add(RegionCode.CL);
                m_save_country_list.Add(RegionCode.CO);
                m_save_country_list.Add(RegionCode.CR);
                m_save_country_list.Add(RegionCode.DO);
                m_save_country_list.Add(RegionCode.JE);  // 915-917 MHz
                m_save_country_list.Add(RegionCode.PA);
                m_save_country_list.Add(RegionCode.PE);
                m_save_country_list.Add(RegionCode.PH);  // 918-920 MHz
                m_save_country_list.Add(RegionCode.SG);
                m_save_country_list.Add(RegionCode.ZA);
                m_save_country_list.Add(RegionCode.UY);
                m_save_country_list.Add(RegionCode.VE);
                m_save_country_list.Add(RegionCode.AU);
                m_save_country_list.Add(RegionCode.HK);
                m_save_country_list.Add(RegionCode.HK8);
                m_save_country_list.Add(RegionCode.HK50);
                m_save_country_list.Add(RegionCode.MY);
                m_save_country_list.Add(RegionCode.TH);
                m_save_country_list.Add(RegionCode.ID);
                m_save_country_list.Add(RegionCode.FCC);
                m_save_country_list.Add(RegionCode.VI);
                m_save_country_list.Add(RegionCode.LH1);  // 
                m_save_country_list.Add(RegionCode.LH2);  // 
                m_save_country_list.Add(RegionCode.UH1); // 915-920 MHz
                m_save_country_list.Add(RegionCode.UH2); // 920-928 MHz
            }
            else
            {
                switch (m_save_country_code)
                {
                    case 1:
                        m_save_country_list.Add(RegionCode.ETSI);
                        m_save_country_list.Add(RegionCode.IN);
                        m_save_country_list.Add(RegionCode.G800);
                        break;
                    case 2:
                        { // HK USA AU ZA
                            switch (m_oem_special_country_version)
                            {
                                case 0x2A2A5257: // RW
                                    m_save_country_list.Add(RegionCode.AR);
                                    m_save_country_list.Add(RegionCode.BR1);
                                    m_save_country_list.Add(RegionCode.BR2);
                                    m_save_country_list.Add(RegionCode.BR3);
                                    m_save_country_list.Add(RegionCode.BR4);
                                    m_save_country_list.Add(RegionCode.BR5);
                                    m_save_country_list.Add(RegionCode.CL);
                                    m_save_country_list.Add(RegionCode.CO);
                                    m_save_country_list.Add(RegionCode.CR);
                                    m_save_country_list.Add(RegionCode.DO);
                                    m_save_country_list.Add(RegionCode.JE);  // 915-917 MHz
                                    m_save_country_list.Add(RegionCode.PA);
                                    m_save_country_list.Add(RegionCode.PE);
                                    m_save_country_list.Add(RegionCode.PH);  // 918-920 MHz
                                    m_save_country_list.Add(RegionCode.SG);
                                    m_save_country_list.Add(RegionCode.ZA);
                                    m_save_country_list.Add(RegionCode.UY);
                                    m_save_country_list.Add(RegionCode.VE);
                                    m_save_country_list.Add(RegionCode.AU);
                                    m_save_country_list.Add(RegionCode.HK);
                                    m_save_country_list.Add(RegionCode.HK8);
                                    m_save_country_list.Add(RegionCode.HK50);
                                    m_save_country_list.Add(RegionCode.MY);
                                    m_save_country_list.Add(RegionCode.TH);
                                    m_save_country_list.Add(RegionCode.ID);
                                    m_save_country_list.Add(RegionCode.FCC);
                                    m_save_country_list.Add(RegionCode.VI);
                                    m_save_country_list.Add(RegionCode.LH1);  // 
                                    m_save_country_list.Add(RegionCode.LH2);  // 
                                    m_save_country_list.Add(RegionCode.UH1); // 915-920 MHz
                                    m_save_country_list.Add(RegionCode.UH2); // 920-928 MHz
                                    break;

                                case 0x2A525753: // RWS
                                    m_save_country_list.Add(RegionCode.FCC);
                                    m_save_country_list.Add(RegionCode.CN);
                                    m_save_country_list.Add(RegionCode.TW);
                                    m_save_country_list.Add(RegionCode.JP);
                                    m_save_country_list.Add(RegionCode.AU);
                                    m_save_country_list.Add(RegionCode.SG);
                                    m_save_country_list.Add(RegionCode.TH);
                                    m_save_country_list.Add(RegionCode.KR);
                                    m_save_country_list.Add(RegionCode.HK50);
                                    m_save_country_list.Add(RegionCode.PH);
                                    m_save_country_list.Add(RegionCode.MX);
                                    m_save_country_list.Add(RegionCode.ID);
                                    m_save_country_list.Add(RegionCode.ETSIUPPERBAND);
                                    m_save_country_list.Add(RegionCode.MY);
                                    m_save_country_list.Add(RegionCode.VI);
                                    m_save_country_list.Add(RegionCode.BR1);
                                    m_save_country_list.Add(RegionCode.BR2);
                                    break;

                                default: // and case 0x2a555341
                                    m_save_country_list.Add(RegionCode.FCC);
                                    break;
                                case 0x4f464341:
                                    m_save_country_list.Add(RegionCode.HK);
                                    break;
                                case 0x2a2a4153:
                                    m_save_country_list.Add(RegionCode.AU);
                                    break;
                                case 0x2a2a4e5a:
                                    m_save_country_list.Add(RegionCode.NZ);
                                    break;
                                case 0x2A2A5347:
                                    m_save_country_list.Add(RegionCode.SG);
                                    break;
                                case 0x2A2A5448:
                                    m_save_country_list.Add(RegionCode.TH);
                                    break;
                                case 0x2A2A5A41:
                                    m_save_country_list.Add(RegionCode.SAHOPPING);
                                    break;
                            }
                        }
                        break;
                    case 4:
                        m_save_country_list.Add(RegionCode.AU);
                        m_save_country_list.Add(RegionCode.MY);
                        m_save_country_list.Add(RegionCode.HK);
                        m_save_country_list.Add(RegionCode.SG);
                        m_save_country_list.Add(RegionCode.TW);
                        m_save_country_list.Add(RegionCode.TW2);
                        m_save_country_list.Add(RegionCode.ID);
                        m_save_country_list.Add(RegionCode.CN);
                        break;
                    case 6:
                        m_save_country_list.Add(RegionCode.KR);
                        break;
                    case 7:
                        m_save_country_list.Add(RegionCode.AU);
                        m_save_country_list.Add(RegionCode.HK);
                        m_save_country_list.Add(RegionCode.TH);
                        m_save_country_list.Add(RegionCode.SG);
                        m_save_country_list.Add(RegionCode.MY);
                        m_save_country_list.Add(RegionCode.ID);
                        m_save_country_list.Add(RegionCode.CN);
                        break;
                    case 8:
                        m_save_country_list.Add(RegionCode.JP);
                        break;
                    case 9:
                        m_save_country_list.Add(RegionCode.ETSIUPPERBAND);
                        break;
                        //default:
                        //throw new ReaderException(Result.INVALID_PARAMETER);
                }
            }
        }

#if R1000
        private void GenCountryList()
        {
            m_save_country_list.Clear();

            switch (m_save_country_code)
            {
                case 1:
                    m_save_country_list.Add(RegionCode.ETSI);
                    m_save_country_list.Add(RegionCode.IN);
                    m_save_country_list.Add(RegionCode.G800);
                    break;
                case 2:
                    if (m_oem_freq_modification_flag == 0x00)
                    {
                        m_save_country_list.Add(RegionCode.AU);
                        m_save_country_list.Add(RegionCode.BR1);
                        m_save_country_list.Add(RegionCode.BR2);
                        m_save_country_list.Add(RegionCode.FCC);
                        m_save_country_list.Add(RegionCode.HK);
                        m_save_country_list.Add(RegionCode.TW);
                        m_save_country_list.Add(RegionCode.SG);
                        m_save_country_list.Add(RegionCode.MY);
                        m_save_country_list.Add(RegionCode.ZA);
                        m_save_country_list.Add(RegionCode.TH);
                        m_save_country_list.Add(RegionCode.ID);
                        m_save_country_list.Add(RegionCode.UH1); // 915-920 MHz
                        m_save_country_list.Add(RegionCode.UH2); // 920-928 MHz
                        m_save_country_list.Add(RegionCode.LH);  // 
                        m_save_country_list.Add(RegionCode.JE);  // 915-917 MHz
                        m_save_country_list.Add(RegionCode.PH);  // 918-920 MHz
                    }
                    else
                    {
                        m_save_country_list.Add(RegionCode.HK);
                    }
                    break;
                case 3:
                    m_save_country_list.Add(RegionCode.JP);
                    break;
                case 4:
                    m_save_country_list.Add(RegionCode.AU);
                    m_save_country_list.Add(RegionCode.MY);
                    m_save_country_list.Add(RegionCode.HK);
                    m_save_country_list.Add(RegionCode.SG);
                    m_save_country_list.Add(RegionCode.TW);
                    m_save_country_list.Add(RegionCode.ID);
                    m_save_country_list.Add(RegionCode.CN);
                    m_save_country_list.Add(RegionCode.CN1);
                    m_save_country_list.Add(RegionCode.CN2);
                    m_save_country_list.Add(RegionCode.CN3);
                    m_save_country_list.Add(RegionCode.CN4);
                    m_save_country_list.Add(RegionCode.CN5);
                    m_save_country_list.Add(RegionCode.CN6);
                    m_save_country_list.Add(RegionCode.CN7);
                    m_save_country_list.Add(RegionCode.CN8);
                    m_save_country_list.Add(RegionCode.CN9);
                    m_save_country_list.Add(RegionCode.CN10);
                    m_save_country_list.Add(RegionCode.CN11);
                    m_save_country_list.Add(RegionCode.CN12);
                    break;
                case 6:
                    m_save_country_list.Add(RegionCode.KR);
                    break;
                case 7:
                    m_save_country_list.Add(RegionCode.AU);
                    m_save_country_list.Add(RegionCode.HK);
                    m_save_country_list.Add(RegionCode.TH);
                    m_save_country_list.Add(RegionCode.SG);
                    m_save_country_list.Add(RegionCode.MY);
                    m_save_country_list.Add(RegionCode.ID);
                    m_save_country_list.Add(RegionCode.CN);
                    m_save_country_list.Add(RegionCode.CN1);
                    m_save_country_list.Add(RegionCode.CN2);
                    m_save_country_list.Add(RegionCode.CN3);
                    m_save_country_list.Add(RegionCode.CN4);
                    m_save_country_list.Add(RegionCode.CN5);
                    m_save_country_list.Add(RegionCode.CN6);
                    m_save_country_list.Add(RegionCode.CN7);
                    m_save_country_list.Add(RegionCode.CN8);
                    m_save_country_list.Add(RegionCode.CN9);
                    m_save_country_list.Add(RegionCode.CN10);
                    m_save_country_list.Add(RegionCode.CN11);
                    m_save_country_list.Add(RegionCode.CN12);
                    break;
                case 8:
                    m_save_country_list.Add(RegionCode.JP2012);
                    break;
                case 9:
                    m_save_country_list.Add(RegionCode.ETSIUPPERBAND);
                    break;
                default:
                    throw new ReaderException(Result.INVALID_PARAMETER);
            }
        }
#endif


        private void SetRadioExtenalLO(bool enable)
        {
            ushort value = 0x0;
            value = (ushort)(enable == true ? 0x04 : 0x08);
            ThrowException(MacBypassWriteRegister(0xF7, value));
        }

        private Result SetRadioSpecialLBTForJapan(LBT enable)
        {
            uint temp = 0;
            //set LBT mode
            //if ((m_Result = MacWriteRegister(MacRegister.HST_PROTSCH_SMIDX /*0x300*/, 1)) != Result.OK)
            //    return m_Result;

            if ((m_Result = MacWriteRegister(MacRegister.HST_PROTSCH_SMCFG_SEL /*0x304*/, 1)) != Result.OK)
                return m_Result;
            if ((m_Result = MacReadRegister(MacRegister.HST_PROTSCH_SMCFG /*0x301*/, ref temp)) != Result.OK)
                return m_Result;
            temp &= 0xFFFFFFFC;
            temp |= (uint)enable;
            //temp |= 0x8;
            if ((m_Result = MacWriteRegister(MacRegister.HST_PROTSCH_SMCFG /*0x301*/, temp)) != Result.OK)
                return m_Result;
            //write LBT timming
            for (uint i = 0; i < 11; i++)
            {
                if ((m_Result = MacWriteRegister(MacRegister.HST_PROTSCH_FTIME_SEL /*0x302*/, i)) != Result.OK)
                    return m_Result;
//                if ((m_Result = MacWriteRegister(0x303, japanBackoffTable[i])) != Result.OK)
//                    return m_Result;
            }

            //set LBT off time
            //if ((m_Result = MacWriteRegister(MacRegister.HST_PROTSCH_TXTIME_SEL /*0x305*/, 1)) != Result.OK)
            //    return m_Result;
            //if ((m_Result = MacWriteRegister(MacRegister.HST_PROTSCH_TXTIME_OFF /*0x307*/, 0x3c)) != Result.OK) //measured value for 60ms
            //    return m_Result;

            m_save_enable_lbt = enable;
            return Result.OK;
        }

        private Result SetRadioSpecialLBTForETSI(LBT enable)
        {
            uint temp = 0;
            //set LBT mode
            //if ((m_Result = MacWriteRegister(MacRegister.HST_PROTSCH_SMIDX /*0x300*/, 1)) != Result.OK)
            //    return m_Result;

            if ((m_Result = MacWriteRegister(MacRegister.HST_PROTSCH_SMCFG_SEL /*0x304*/, 1)) != Result.OK)
                return m_Result;
            if ((m_Result = MacReadRegister(MacRegister.HST_PROTSCH_SMCFG /*0x301*/, ref temp)) != Result.OK)
                return m_Result;
            temp &= 0xFFFFFFFC;
            temp |= (uint)enable;
            temp |= 0x8;
            if ((m_Result = MacWriteRegister(MacRegister.HST_PROTSCH_SMCFG /*0x301*/, temp)) != Result.OK)
                return m_Result;

            //write LBT timming
            for (uint i = 0; i < 11; i++)
            {
                if ((m_Result = MacWriteRegister(MacRegister.HST_PROTSCH_FTIME_SEL /*0x302*/, i)) != Result.OK)
                    return m_Result;

//                if ((m_Result = MacWriteRegister(0x303, japanBackoffTable[i])) != Result.OK)
//                    return m_Result;
            }

            //set LBT off time
            //if ((m_Result = MacWriteRegister(MacRegister.HST_PROTSCH_TXTIME_SEL /*0x305*/, 1)) != Result.OK)
            //    return m_Result;

            //if ((m_Result = MacWriteRegister(MacRegister.HST_PROTSCH_TXTIME_ON /*0x306*/, 0xfa0)) != Result.OK)
            //    return m_Result;

            //if ((m_Result = MacWriteRegister(MacRegister.HST_PROTSCH_TXTIME_OFF /*0x307*/, 0x6E)) != Result.OK) //measured value for 110ms
            //    return m_Result;

            m_save_enable_lbt = enable;

            return m_Result;
        }

        private Result SetRadioLBT(LBT enable)
        {
            //ushort Reg = 0x0301; // HST_PROTSCH_SMCFG
            uint Val = 0;
            if ((m_Result = MacReadRegister(MacRegister.HST_PROTSCH_SMCFG /*Reg*/, ref Val)) != Result.OK)
                return m_Result;

            if (enable == LBT.ON) /* Bit 0 */
                Val |= 0x00000001;
            else
                Val &= 0xFFFFFFFE;

            if ((m_Result = MacWriteRegister(MacRegister.HST_PROTSCH_SMCFG /*Reg*/, Val)) != Result.OK)
                return m_Result;

            m_save_enable_lbt = enable;
            return Result.OK;
        }

        private Result GetRadioLBT(ref bool lbt)
        {
            //ushort Reg = 0x0301; // HST_PROTSCH_SMCFG
            uint Val = 0;
            if ((m_Result = MacReadRegister(MacRegister.HST_PROTSCH_SMCFG /*Reg*/, ref Val)) != Result.OK)
                return m_Result;
            lbt = ((Val & 0x00000001) != 0);
            return Result.OK;
        }

        /// <summary>
        /// Set Frequency Band - Basic Function
        /// </summary>
        /// <param name="m_radioIndex"></param>
        /// <param name="frequencySelector"></param>
        /// <param name="config"></param>
        /// <param name="multdiv"></param>
        /// <param name="pllcc"></param>
        /// <returns></returns>
        private Result SetFrequencyBand
            (
                UInt32 frequencySelector,
                BandState config,
                UInt32 multdiv,
                UInt32 pllcc
            )
        {
            // test by mephist
            // return Result.OK;

            m_Result = MacWriteRegister(MacRegister.HST_RFTC_FRQCH_SEL /*SELECTOR_ADDRESS*/, frequencySelector);
            if (m_Result != Result.OK) return m_Result;

            m_Result = MacWriteRegister(MacRegister.HST_RFTC_FRQCH_CFG /*CONFIG_ADDRESS*/, (uint)config);
            if (m_Result != Result.OK) return m_Result;

            if (config == BandState.ENABLE)
            {
                m_Result = MacWriteRegister(MacRegister.HST_RFTC_FRQCH_DESC_PLLDIVMULT /*MULTDIV_ADDRESS*/, multdiv);
                if (m_Result != Result.OK) return m_Result;

                m_Result = MacWriteRegister(MacRegister.HST_RFTC_FRQCH_DESC_PLLDACCTL /*PLLCC_ADDRESS*/, pllcc);
                if (m_Result != Result.OK) return m_Result;
            }

            return m_Result;
        }

#if nouse
        /// <summary>
        /// Set Frequency Band - Basic Function
        /// </summary>
        /// <param name="m_radioIndex"></param>
        /// <param name="frequencySelector"></param>
        /// <param name="config"></param>
        /// <param name="multdiv"></param>
        /// <param name="pllcc"></param>
        /// <returns></returns>
        private Result SetFrequencyBand
            (
                UInt32 frequencySelector,
                BandState config,
                UInt32 multdiv
            )
        {
            UInt32 pllcc;

            // test by mephist
            // return Result.OK;

            m_Result = MacWriteRegister(MacRegister.HST_RFTC_FRQCH_SEL /*SELECTOR_ADDRESS*/, frequencySelector);
            if (m_Result != Result.OK) return m_Result;

            m_Result = MacWriteRegister(MacRegister.HST_RFTC_FRQCH_CFG /*CONFIG_ADDRESS*/, (uint)config);
            if (m_Result != Result.OK) return m_Result;

            if (config == BandState.ENABLE)
            {
                m_Result = MacWriteRegister(MacRegister.HST_RFTC_FRQCH_DESC_PLLDIVMULT /*MULTDIV_ADDRESS*/, multdiv);
                if (m_Result != Result.OK) return m_Result;

                if (multdiv == 0)
                    pllcc = 0;
                else if (multdiv >= 0x00180e1b && multdiv <= 0x00180e4b)
                    pllcc = 0x14020200;
                else if (multdiv >= 0x00180e4d && multdiv <= 0x00180e7d)
                    pllcc = 0x14010100;
                else
                    pllcc = 0x14070700;
                
                m_Result = MacWriteRegister(MacRegister.HST_RFTC_FRQCH_DESC_PLLDACCTL /*PLLCC_ADDRESS*/, pllcc);
                if (m_Result != Result.OK) return m_Result;
            }

            return m_Result;
        }
#endif

        public UInt32 GetPllValue(uint channel)
        {
            RegionCode prof = m_save_region_code;
            //Result status = Result.OK;
            uint TotalCnt = FreqChnCnt(prof);
            uint[] freqTable = FreqTable(prof);
            uint i = 0;

            // Check Parameters
            if (!FreqChnWithinRange(channel, prof) || freqTable == null)
                return 0;

            int Index = FreqSortedIdxTbls(prof, channel);
            if (Index < 0)
                return 0;

            return freqTable[Index];
        }

        /// <summary>
        /// Get Frequency band - Basic function
        /// </summary>
        /// <param name="m_radioIndex">Input radio index</param>
        /// <param name="frequencySelector">frequencySelector</param>
        /// <param name="freq">frequencySelector</param>
        /// <returns>FrequencyBandParms</returns>
        private Result GetFrequencyBand (UInt32 frequencySelector, ref FrequencyBandParms freq)
        {
            UInt32 config = 0;
            UInt32 multdiv = 0;
            UInt32 pllcc = 0;
            FrequencyBandParms frqband = new FrequencyBandParms();
            m_Result = MacWriteRegister(MacRegister.HST_RFTC_FRQCH_SEL /*SELECTOR_ADDRESS*/, frequencySelector);

            if (CSLibrary.Constants.Result.OK != m_Result)
            {
                return m_Result;
            }

            m_Result = MacReadRegister
            (
                MacRegister.HST_RFTC_FRQCH_CFG /*CONFIG_ADDRESS*/,
                ref config
            );

            if (CSLibrary.Constants.Result.OK != m_Result)
            {
                return m_Result;
            }

            m_Result = MacReadRegister
            (

                MacRegister.HST_RFTC_FRQCH_DESC_PLLDIVMULT /*MULTDIV_ADDRESS*/,
                ref multdiv
            );

            if (CSLibrary.Constants.Result.OK != m_Result)
            {
                return m_Result;
            }

            m_Result = MacReadRegister
            (
                MacRegister.HST_RFTC_FRQCH_DESC_PLLDACCTL /*PLLCC_ADDRESS*/,
                ref pllcc
            );

            if (CSLibrary.Constants.Result.OK != m_Result)
            {
                return m_Result;
            }

            frqband.State = (BandState)(config == 0 ? 0 : 1);
            frqband.MultiplyRatio = (UInt16)((multdiv >> 0) & 0xffff);
            frqband.DivideRatio = (UInt16)((multdiv >> 16) & 0xff);

            frqband.MinimumDACBand = (UInt16)((pllcc >> 0) & 0xff);
            frqband.AffinityBand = (UInt16)((pllcc >> 8) & 0xff);
            frqband.MaximumDACBand = (UInt16)((pllcc >> 16) & 0xff);
            frqband.GuardBand = (UInt16)((pllcc >> 24) & 0xff);
            frqband.Frequency = (ClockKHz / (4 * frqband.DivideRatio)) * frqband.MultiplyRatio / 1000;
            freq = frqband;
            return m_Result;
        }
        #endregion


        /// <summary>
        /// Set 1 Frequency Channel
        /// All region can be used to set a fixed channel
        /// </summary>
        /// <param name="prof">Region Code</param>
        /// <param name="channel">Channel number start from zero, you can get the available channels 
        /// from CSLibrary.HighLevelInterface.AvailableFrequencyTable(CSLibrary.Constants.RegionCode)</param>
        /// <param name="LBTcfg">This is only used when JPN is set</param>
        /// <returns>Result</returns>
        public Result SetFixedChannel(RegionCode prof, uint channel, LBT LBTcfg)
        {
            uint Reg0x700 = 0;

            DEBUG_WriteLine(DEBUGLEVEL.API, "HighLevelInterface.SetFixedChannel(RegionCode prof, uint channel, LBT LBTcfg)");

            if (IsHoppingChannelOnly)
                return Result.INVALID_PARAMETER;

            // disable agile mode
            if ((m_Result = MacReadRegister(MacRegister.HST_ANT_CYCLES /*0x700*/, ref Reg0x700)) == Result.OK)
            {
                Reg0x700 &= ~0x01000000U;
                m_Result = MacWriteRegister(MacRegister.HST_ANT_CYCLES /*0x700*/, Reg0x700);
            }

            if (m_Result != Result.OK)
                return m_Result;

        AGAIN:
            try
            {
                if (!GetActiveRegionCode().Contains(prof))
                    return Result.INVALID_PARAMETER;

                //Result status = Result.OK;
                uint TotalCnt = FreqChnCnt(prof);
                uint[] freqTable = FreqTable(prof);
                uint i = 0;

                // Check Parameters
                if (!FreqChnWithinRange(channel, prof) || freqTable == null)
                    return Result.INVALID_PARAMETER;

                int Index = FreqSortedIdxTbls(prof, channel);
                if (Index < 0)
                    return Result.INVALID_PARAMETER;

                //Enable channel
                //Abert request to hardcode to scan mode for jp only.
                //if (prof == RegionCode.JP)
                if (false)
                {
                    if (LBTcfg == LBT.OFF)
                    {
                        ThrowException(SetFrequencyBand((uint)Index, BandState.ENABLE, freqTable[Index], GetPllcc(prof)));
                        //ThrowException(SetFrequencyBand(0, BandState.ENABLE, freqTable[Index]));
                        i = 0;
                    }
                    else
                    {
                        //Enable all four channel
                        for (i = 0; i < freqTable.Length; i++)
                        {
                            ThrowException(SetFrequencyBand(i, BandState.ENABLE, freqTable[i], GetPllcc(prof)));
                            //ThrowException(SetFrequencyBand(i, BandState.ENABLE, freqTable[i]));
                        }

                        ThrowException(MacWriteRegister(MacRegister.HST_RFTC_FRQCH_CMDSTART /*0xc08*/, channel + 0x100));

                        i = (uint)freqTable.Length;
                    }
                }
                else
                {
                    if (LBTcfg == LBT.SCAN && (prof == RegionCode.ETSI || prof == RegionCode.JP))
                    {
                        //Enable all four channel
                        for (i = 0; i < freqTable.Length; i++)
                        {
                            ThrowException(SetFrequencyBand(i, BandState.ENABLE, freqTable[i], GetPllcc(prof)));
                            //ThrowException(SetFrequencyBand(i, BandState.ENABLE, freqTable[i]));
                        }

                        ThrowException(MacWriteRegister(MacRegister.HST_RFTC_FRQCH_CMDSTART /*0xc08*/, channel + 0x100));

                        i = (uint)freqTable.Length;
                    }
                    else
                    {
                        ThrowException(SetFrequencyBand(0, BandState.ENABLE, freqTable[Index], GetPllcc(prof)));
                        //ThrowException(SetFrequencyBand((uint)Index, BandState.ENABLE, freqTable[Index], GetPllcc(prof)));
                        //ThrowException(SetFrequencyBand(0, BandState.ENABLE, freqTable[Index]));
                        i = 1;
                    }
                }
                //Disable channels
                for (uint j = i; j < MAXFRECHANNEL; j++)
                {
                    //if (j != Index)
                        ThrowException(SetFrequencyBand(j, BandState.DISABLE, 0, 0));
                    //ThrowException(SetFrequencyBand(j, BandState.DISABLE, 0, 0));
                }

                /*if (prof == RegionCode.ETSI)
                {
                    m_Result = SetRadioSpecialLBTForETSI(LBTcfg);
                }
                else*/
                if (prof == RegionCode.JP)
                {
                    ThrowException(SetRadioSpecialLBTForJapan(LBTcfg));
                }
                else
                {
                    ThrowException(SetRadioLBT(LBT.OFF));
                }                //Save settings

                m_save_region_code = prof;
                m_save_freq_channel = channel;
                m_save_fixed_channel = true;
                m_save_agile_channel = false;
                m_save_selected_freq = GetAvailableFrequencyTable(prof)[channel];
                ThrowException(CheckPowerLvl(prof));
            }
            catch (ReaderException ex)
            {
                if (FireIfReset(ex.ErrorCode) == Result.OK)
                {
                    goto AGAIN;
                }
            }
            catch
            {
                m_Result = Result.SYSTEM_CATCH_EXCEPTION;
            }
            return m_Result;
        }


#if nouse        
        /// <summary>
        /// Set 1 Frequency Channel
        /// All region can be used to set a fixed channel
        /// </summary>
        /// <param name="prof">Region Code</param>
        /// <param name="channel">Channel number start from zero, you can get the available channels 
        /// from CSLibrary.HighLevelInterface.AvailableFrequencyTable(CSLibrary.Constants.RegionCode)</param>
        /// <param name="LBTcfg">This is only used when JPN is set</param>
        /// <returns>Result</returns>
        public Result SetFixedChannel(RegionCode prof, uint channel, LBT LBTcfg)
        {
            uint Reg0x700 = 0;

            // disable agile mode
            if ((m_Result = MacReadRegister(MacRegister.HST_ANT_CYCLES /*0x700*/, ref Reg0x700)) == Result.OK)
            {
                Reg0x700 &= ~0x01000000U;
                m_Result = MacWriteRegister(MacRegister.HST_ANT_CYCLES /*0x700*/, Reg0x700);
            }

            if (m_Result != Result.OK)
                return m_Result;

            AGAIN:
            try
            {
                if (!GetActiveRegionCode().Contains(prof))
                    return Result.INVALID_PARAMETER;

                //Result status = Result.OK;
                uint TotalCnt = FreqChnCnt(prof);
                uint[] freqTable = FreqTable(prof);
                uint i = 0;

                // Check Parameters
                if (!FreqChnWithinRange(channel, prof) || freqTable == null)
                    return Result.INVALID_PARAMETER;

                int Index = FreqSortedIdxTbls(prof, channel);
                if (Index < 0)
                    return Result.INVALID_PARAMETER;

                //Enable channel
                //Abert request to hardcode to scan mode for jp only.
                if (prof == RegionCode.JP || prof == RegionCode.JP2012)
                {
                    if (LBTcfg == LBT.OFF)
                    {
                        ThrowException(SetFrequencyBand(0, BandState.ENABLE, freqTable[Index], GetPllcc(prof)));
                        //ThrowException(SetFrequencyBand(0, BandState.ENABLE, freqTable[Index]));
                        i = 1;
                    }
                    else
                    {
                        //Enable all four channel
                        for (i = 0; i < freqTable.Length; i++)
                        {
                            ThrowException(SetFrequencyBand(i, BandState.ENABLE, freqTable[i], GetPllcc(prof)));
                            //ThrowException(SetFrequencyBand(i, BandState.ENABLE, freqTable[i]));
                        }

                        ThrowException(MacWriteRegister(MacRegister.HST_RFTC_FRQCH_CMDSTART /*0xc08*/, channel + 0x100));

                        i = (uint)freqTable.Length;
                    }
                }
                else
                {
                    if (LBTcfg == LBT.SCAN && (prof == RegionCode.ETSI || prof == RegionCode.JP))
                    {
                        //Enable all four channel
                        for (i = 0; i < freqTable.Length; i++)
                        {
                            ThrowException(SetFrequencyBand(i, BandState.ENABLE, freqTable[i], GetPllcc(prof)));
                            //ThrowException(SetFrequencyBand(i, BandState.ENABLE, freqTable[i]));
                        }

                        ThrowException(MacWriteRegister(MacRegister.HST_RFTC_FRQCH_CMDSTART /*0xc08*/, channel + 0x100));

                        i = (uint)freqTable.Length;
                    }
                    else
                    {
                        ThrowException(SetFrequencyBand(0, BandState.ENABLE, freqTable[Index], GetPllcc(prof)));
                        //ThrowException(SetFrequencyBand(0, BandState.ENABLE, freqTable[Index]));
                        i = 1;
                    }
                }
                //Disable channels
                for (uint j = i; j < MAXFRECHANNEL; j++)
                {
                    ThrowException(SetFrequencyBand(j, BandState.DISABLE, 0, 0));
                    //ThrowException(SetFrequencyBand(j, BandState.DISABLE, 0));
                }
                /*if (prof == RegionCode.ETSI)
                {
                    m_Result = SetRadioSpecialLBTForETSI(LBTcfg);
                }
                else*/
                if (prof == RegionCode.JP || prof == RegionCode.JP2012)
                {
                   ThrowException(SetRadioSpecialLBTForJapan(LBTcfg));
                }
                else
                {
                    ThrowException(SetRadioLBT(LBT.OFF));
                }                //Save settings
                m_save_region_code = prof;
                m_save_freq_channel = channel;
                m_save_fixed_channel = true;
                m_save_selected_freq = GetAvailableFrequencyTable(prof)[channel];
                ThrowException(CheckPowerLvl(prof));
            }
            catch (ReaderException ex)
            {
                if (FireIfReset(ex.ErrorCode) == Result.OK)
                {
                    goto AGAIN;
                }
            }
            catch 
            {
                m_Result = Result.SYSTEM_CATCH_EXCEPTION;
            }
            return m_Result;
        }
#endif
        
        /// <summary>
        /// Set Fixed Frequency Channel on current region
        /// </summary>
        /// <param name="channel">frequency channel number</param>
        /// <param name="LBTcfg">LBT options</param>
        /// <returns></returns>
        public Result SetFixedChannel(uint channel, LBT LBTcfg)
        {
            return SetFixedChannel(m_save_region_code, channel, LBTcfg);
        }
        /// <summary>
        /// Set Fixed Frequency Channel on current region
        /// </summary>
        /// <param name="channel">frequency channel number</param>
        /// <returns></returns>
        public Result SetFixedChannel(uint channel)
        {
            return SetFixedChannel(m_save_region_code, channel, m_save_enable_lbt);
        }

        /// <summary>
        /// Reset Fixed Frequency Channel on current region
        /// </summary>
        /// <returns></returns>
        public Result SetFixedChannel()
        {
            return SetFixedChannel(m_save_region_code, m_save_freq_channel, m_save_enable_lbt);
        }
        /// <summary>
        /// Set to frequency agile mode
        /// </summary>
        /// <param name="prof">Country Profile</param>
        /// <returns>Result</returns>
        public Result SetAgileChannels(RegionCode prof)
        {
            uint Reg0x700 = 0;
        
            AGAIN:
            try
            {
                if (!GetActiveRegionCode().Contains(prof) || (prof != RegionCode.ETSI && prof != RegionCode.JP))
                    return Result.INVALID_PARAMETER;

                uint TotalCnt = FreqChnCnt(prof);
                uint[] freqTable = FreqTable(prof);

                //Enable channels
                for (uint i = 0; i < TotalCnt; i++)
                {
                    ThrowException(SetFrequencyBand(i, BandState.ENABLE, freqTable[i], GetPllcc(prof)));
                    //ThrowException(SetFrequencyBand(i, BandState.ENABLE, freqTable[i]));
                }
                //Disable channels
                for (uint i = TotalCnt; i < 50; i++)
                {
                    ThrowException(SetFrequencyBand(i, BandState.DISABLE, 0, 0));
                    //ThrowException(SetFrequencyBand(i, BandState.DISABLE, 0));
                }

                ThrowException(SetRadioLBT(LBT.OFF));

                m_save_region_code = prof;
                m_save_fixed_channel = false;
                m_save_agile_channel = true;
                ThrowException(CheckPowerLvl(prof));

/*
 * if ((m_Result = MacWriteRegister(0x306, 0x64)) != Result.OK)
                    return m_Result;

                if ((m_Result = MacWriteRegister(0x307, 0x1)) != Result.OK) //measured value for 110ms
                    return m_Result;
*/

                ThrowException(MacReadRegister(MacRegister.HST_ANT_CYCLES /*0x700*/, ref Reg0x700));
                Reg0x700 |= 0x01000000U;
                ThrowException(MacWriteRegister(MacRegister.HST_ANT_CYCLES /*0x700*/, Reg0x700));

                if ((m_Result = MacReadRegister(MacRegister.HST_ANT_CYCLES /*0x700*/, ref Reg0x700)) == Result.OK)
                {
                    Reg0x700 |= 0x01000000U;
                    m_Result = MacWriteRegister(MacRegister.HST_ANT_CYCLES /*0x700*/, Reg0x700);
                }
            }
            catch (ReaderException ex)
            {
                if (FireIfReset(ex.ErrorCode) == Result.OK)
                {
                    goto AGAIN;
                }
            }
            catch
            {
                m_Result = Result.SYSTEM_CATCH_EXCEPTION;
            }

            return m_Result;
        }

        /// <summary>
        /// Set to the specific frequency profile
        /// </summary>
        /// <param name="prof">Country Profile</param>
        /// <returns>Result</returns>
        public Result SetHoppingChannels(RegionCode prof)
        {
            DEBUG_WriteLine(DEBUGLEVEL.API, "HighLevelInterface.SetHoppingChannels(RegionCode prof)");

            AGAIN:
            try
            {
                if (IsFixedChannelOnly || !GetActiveRegionCode().Contains(prof))
                    return Result.INVALID_PARAMETER;

                uint TotalCnt = FreqChnCnt(prof);
                uint[] freqTable = FreqTable(prof);

                        //Enable channels
                        for (uint i = 0; i < TotalCnt; i++)
                        {
                            ThrowException(SetFrequencyBand(i, BandState.ENABLE, freqTable[i], GetPllcc(prof)));
                            //ThrowException(SetFrequencyBand(i, BandState.ENABLE, freqTable[i]));
                        }
                        //Disable channels
                        for (uint i = TotalCnt; i < 50; i++)
                        {
                            ThrowException(SetFrequencyBand(i, BandState.DISABLE, 0, 0));
                            //ThrowException(SetFrequencyBand(i, BandState.DISABLE, 0));
                        }

                ThrowException(SetRadioLBT(LBT.OFF));

                m_save_region_code = prof;
                m_save_fixed_channel = false;
                ThrowException(CheckPowerLvl(prof));
                m_Result = Result.OK;
            }
            catch (ReaderException ex)
            {
                if (FireIfReset(ex.ErrorCode) == Result.OK)
                {
                    goto AGAIN;
                }
            }
            catch
            {
                m_Result = Result.SYSTEM_CATCH_EXCEPTION;
            }
            return m_Result;
        }

        /// <summary>
        /// Reset current frequency profile
        /// </summary>
        /// <returns></returns>
        public Result SetHoppingChannels()
        {
            return SetHoppingChannels(m_save_region_code);
        }
#if ENGINEERING_DEBUG

        /// <summary>
        /// Reset current frequency profile
        /// </summary>
        /// <returns></returns>
        public Result SetHoppingChannels(uint plloc)
        {
            return SetHoppingChannels(m_save_region_code, plloc);
        }

        /// <summary>
        /// Set to the specific frequency profile
        /// </summary>
        /// <param name="prof">Country Profile</param>
        /// <param name="plloc">plloc</param>
        /// <returns>Result</returns>
        public Result SetHoppingChannels(RegionCode prof, uint plloc)
        {

            if (IsFixedChannelOnly || !GetActiveRegionCode().Contains(prof))
                return Result.INVALID_PARAMETER;

            uint TotalCnt = FreqChnCnt(prof);
            uint[] freqTable = FreqTable(prof);

            switch (prof)
            {
                case RegionCode.FCC:
                case RegionCode.CN:
                case RegionCode.TW:
                case RegionCode.KR:
                case RegionCode.HK:
                case RegionCode.SG:
                case RegionCode.AU:
                case RegionCode.MY:
                case RegionCode.ZA:
                case RegionCode.BR1:
                case RegionCode.BR2:
                    //Enable channels
                    for (uint i = 0; i < TotalCnt; i++)
                    {
                        m_Result = SetFrequencyBand(i, BandState.ENABLE, freqTable[i], plloc/*GetPllcc(prof)*/);
                        if (m_Result != Result.OK) return m_Result;
                    }
                    //Disable channels
                    for (uint i = TotalCnt; i < 50; i++)
                    {
                        m_Result = SetFrequencyBand(i, BandState.DISABLE, 0, 0);
                        if (m_Result != Result.OK) return m_Result;
                    }
                    break;
            }

            if ((m_Result = SetRadioLBT(LBT.OFF)) != Result.OK)
                return m_Result;

            m_save_region_code = prof;
            m_save_fixed_channel = false;
            return (m_Result = CheckPowerLvl(prof));
        }
#endif


        /// <summary>
        /// Get frequency table on specific region
        /// </summary>
        /// <param name="region">Region Code</param>
        /// <returns></returns>
        public double[] GetAvailableFrequencyTable(RegionCode region)
        {
            switch (region)
            {
                case RegionCode.AU:
                    return AUSTableOfFreq;
                case RegionCode.CN:
                    return CHNTableOfFreq;
                case RegionCode.ETSI:
                case RegionCode.G800:
                    return ETSITableOfFreq;
                case RegionCode.IN:
                    return IDATableOfFreq;
                case RegionCode.AR:
                case RegionCode.CL:
                case RegionCode.CO:
                case RegionCode.CR:
                case RegionCode.DO:
                case RegionCode.PA:
                case RegionCode.UY:
                case RegionCode.MX:
                case RegionCode.FCC:
                    return FCCTableOfFreq;
                case RegionCode.HK:
                case RegionCode.HK50:
                    return OFCATableOfFreq;

                case RegionCode.SG:
                case RegionCode.TH:
                case RegionCode.VI:
                case RegionCode.HK8:
                    return HKTableOfFreq;

                case RegionCode.JP:
                    if (m_oem_special_country_version != 0x2A4A5036)
//                    if (OEMChipSetID == ChipSetID.R1000)
                        return JPN2012TableOfFreq;
                    else
                        return JPN2019TableOfFreq;
                case RegionCode.KR:
                    return KRTableOfFreq;
                case RegionCode.MY:
                    return MYSTableOfFreq;
                case RegionCode.TW:
                    return TWTableOfFreq;
                case RegionCode.TW2:
                    return TW2TableOfFreq;
                case RegionCode.ZA:
                    return ZATableOfFreq;
                case RegionCode.BR1:
                    return BR1TableOfFreq;
                case RegionCode.PE:
                case RegionCode.BR2:
                    return BR2TableOfFreq;
                case RegionCode.BR3:
                    return BR3TableOfFreq;
                case RegionCode.BR4:
                    return BR4TableOfFreq;
                case RegionCode.BR5:
                    return BR5TableOfFreq;
                case RegionCode.ID:
                    return IDTableOfFreq;
                case RegionCode.JE:
                    return JETableOfFreq;
                case RegionCode.PH:
                    return PHTableOfFreq;
                case RegionCode.ETSIUPPERBAND:
                    return ETSIUPPERBANDTableOfFreq;
                case RegionCode.NZ:
                    return NZTableOfFreq;
                case RegionCode.UH1:
                    return UH1TableOfFreq;
                case RegionCode.UH2:
                    return UH2TableOfFreq;
                case RegionCode.LH:
                    return LHTableOfFreq;
                case RegionCode.LH1:
                    return LH1TableOfFreq;
                case RegionCode.LH2:
                    return LH2TableOfFreq;
                case RegionCode.VE:
                    return VETableOfFreq;
                case RegionCode.SAHOPPING:
                    return SAHoppingTableOfFreq;
                default:
                    return new double[0];
            }
        }

#if R1000
        /// <summary>
        /// Get frequency table on specific region
        /// </summary>
        /// <param name="region">Region Code</param>
        /// <returns></returns>
        public double[] GetAvailableFrequencyTable(RegionCode region)
        {
            switch (region)
            {
                case RegionCode.AU:
                    return AUSTableOfFreq;
                case RegionCode.CN:
                    return CHNTableOfFreq;
                case RegionCode.CN1:
                    return CHN1TableOfFreq;
                case RegionCode.CN2:
                    return CHN2TableOfFreq;
                case RegionCode.CN3:
                    return CHN3TableOfFreq;
                case RegionCode.CN4:
                    return CHN4TableOfFreq;
                case RegionCode.CN5:
                    return CHN5TableOfFreq;
                case RegionCode.CN6:
                    return CHN6TableOfFreq;
                case RegionCode.CN7:
                    return CHN7TableOfFreq;
                case RegionCode.CN8:
                    return CHN8TableOfFreq;
                case RegionCode.CN9:
                    return CHN9TableOfFreq;
                case RegionCode.CN10:
                    return CHN10TableOfFreq;
                case RegionCode.CN11:
                    return CHN11TableOfFreq;
                case RegionCode.CN12:
                    return CHN12TableOfFreq;

                case RegionCode.ETSI:
                case RegionCode.G800:
                    return ETSITableOfFreq;
                case RegionCode.IN:
                    return IDATableOfFreq;
                case RegionCode.FCC:
                    return FCCTableOfFreq;
                case RegionCode.HK:
                case RegionCode.SG:
                case RegionCode.TH:
                    return HKTableOfFreq;
                case RegionCode.JP:
                    return JPNTableOfFreq;
                case RegionCode.JP2012:
                    return JPN2012TableOfFreq;
                case RegionCode.KR:
                    return KRTableOfFreq;
                case RegionCode.MY:
                    return MYSTableOfFreq;
                case RegionCode.TW:
                    return TWTableOfFreq;
                case RegionCode.ZA:
                    return ZATableOfFreq;
                case RegionCode.BR1:
                    return BR1TableOfFreq;
                case RegionCode.BR2:
                    return BR2TableOfFreq;
                case RegionCode.ID:
                    return IDTableOfFreq;
                case RegionCode.UH1:
                    return UH1TableOfFreq;
                case RegionCode.UH2:
                    return UH2TableOfFreq;
                case RegionCode.LH:
                    return LHTableOfFreq;
                case RegionCode.JE:
                    return JETableOfFreq;
                case RegionCode.PH:
                    return PHTableOfFreq;
                default:
                    return new double[0];
            }
        }
#endif


        /// <summary>
        /// Get frequency table on current region
        /// </summary>
        /// <returns></returns>
        public double[] GetAvailableFrequencyTable()
        {
            return GetAvailableFrequencyTable(m_save_region_code);
        }

        /// <summary>
        /// Get frequency table on current region
        /// </summary>
        /// <returns></returns>
        public double[] GetCurrentFrequencyTable()
        {
            return GetAvailableFrequencyTable(m_save_region_code);
        }

        public uint[] GetCurrentFrequencySortTable ()
        {
            return FreqIndex(m_save_region_code);
        }
        
        #endregion

        #region ====================== Frequency Table ======================

        #region FCC
        /// <summary>
        /// FCC Frequency Table
        /// </summary>
        private readonly double[] FCCTableOfFreq = new double[]
            {
                902.75,//1
                903.25,
                903.75,
                904.25,
                904.75,//5
                905.25,
                905.75,
                906.25,
                906.75,
                907.25,//10
                907.75,
                908.25,
                908.75,
                909.25,
                909.75,//15
                910.25,
                910.75,
                911.25,
                911.75,
                912.25,//20
                912.75,
                913.25,
                913.75,
                914.25,
                914.75,//25
                915.25,
                915.75,
                916.25,
                916.75,
                917.25,
                917.75,
                918.25,
                918.75,
                919.25,
                919.75,
                920.25,
                920.75,
                921.25,
                921.75,
                922.25,
                922.75,
                923.25,
                923.75,
                924.25,
                924.75,
                925.25,
                925.75,
                926.25,
                926.75,
                927.25,
            };
        /*OK*/
        private uint[] fccFreqTable = new uint[]
        {
            0x00180E4F, /*915.75 MHz   */
            0x00180E4D, /*915.25 MHz   */
            0x00180E1D, /*903.25 MHz   */
            0x00180E7B, /*926.75 MHz   */
            0x00180E79, /*926.25 MHz   */
            0x00180E21, /*904.25 MHz   */
            0x00180E7D, /*927.25 MHz   */
            0x00180E61, /*920.25 MHz   */
            0x00180E5D, /*919.25 MHz   */
            0x00180E35, /*909.25 MHz   */
            0x00180E5B, /*918.75 MHz   */
            0x00180E57, /*917.75 MHz   */
            0x00180E25, /*905.25 MHz   */
            0x00180E23, /*904.75 MHz   */
            0x00180E75, /*925.25 MHz   */
            0x00180E67, /*921.75 MHz   */
            0x00180E4B, /*914.75 MHz   */
            0x00180E2B, /*906.75 MHz   */
            0x00180E47, /*913.75 MHz   */
            0x00180E69, /*922.25 MHz   */
            0x00180E3D, /*911.25 MHz   */
            0x00180E3F, /*911.75 MHz   */
            0x00180E1F, /*903.75 MHz   */
            0x00180E33, /*908.75 MHz   */
            0x00180E27, /*905.75 MHz   */
            0x00180E41, /*912.25 MHz   */
            0x00180E29, /*906.25 MHz   */
            0x00180E55, /*917.25 MHz   */
            0x00180E49, /*914.25 MHz   */
            0x00180E2D, /*907.25 MHz   */
            0x00180E59, /*918.25 MHz   */
            0x00180E51, /*916.25 MHz   */
            0x00180E39, /*910.25 MHz   */
            0x00180E3B, /*910.75 MHz   */
            0x00180E2F, /*907.75 MHz   */
            0x00180E73, /*924.75 MHz   */
            0x00180E37, /*909.75 MHz   */
            0x00180E5F, /*919.75 MHz   */
            0x00180E53, /*916.75 MHz   */
            0x00180E45, /*913.25 MHz   */
            0x00180E6F, /*923.75 MHz   */
            0x00180E31, /*908.25 MHz   */
            0x00180E77, /*925.75 MHz   */
            0x00180E43, /*912.75 MHz   */
            0x00180E71, /*924.25 MHz   */
            0x00180E65, /*921.25 MHz   */
            0x00180E63, /*920.75 MHz   */
            0x00180E6B, /*922.75 MHz   */
            0x00180E1B, /*902.75 MHz   */
            0x00180E6D, /*923.25 MHz   */
        };
        /// <summary>
        /// FCC Frequency Channel number
        /// </summary>
        private const uint FCC_CHN_CNT = 50;
        private readonly uint[] fccFreqSortedIdx = new uint[]{
            /*48, 2, 22, 5, 13, 
            12, 24, 26,17, 29,
            34, 41, 23, 9, 36, 
            32, 33, 20, 21, 25, 
            43, 39, 18, 28, 16, 
            1, 0, 31, 38, 27,
            11, 30, 10, 8, 37,
            7, 46, 45, 15, 19,
            47,49,40, 44, 35,
            14, 42, 4, 3, 6,*/
            26, 25, 1, 48, 47,
            3, 49, 35, 33, 13,
            32, 30, 5, 4, 45,
            38, 24, 8, 22, 39,
            17, 18, 2, 12, 6,
            19, 7, 29, 23, 9,
            31, 27, 15, 16, 10,
            44, 14, 34, 28, 21,
            42, 11, 46, 20, 43,
            37, 36, 40, 0, 41,
        };

        private uint[] fccFreqTable_Ver20170001 = new uint[]
        {
            0x00180E4D, /*915.25 MHz  25 */
            0x00180E63, /*920.75 MHz  36 */
            0x00180E35, /*909.25 MHz  13 */
            0x00180E41, /*912.25 MHz  19 */
            0x00180E59, /*918.25 MHz  31 */
            0x00180E61, /*920.25 MHz  35 */
            0x00180E37, /*909.75 MHz  14 */
            0x00180E39, /*910.25 MHz  15 */
            0x00180E5F, /*919.75 MHz  34 */
            0x00180E6B, /*922.75 MHz  40 */
            0x00180E33, /*908.75 MHz  12 */
            0x00180E47, /*913.75 MHz  22 */
            0x00180E1F, /*903.75 MHz  2 */
            0x00180E5D, /*919.25 MHz  33 */
            0x00180E69, /*922.25 MHz  39 */
            0x00180E2F, /*907.75 MHz  10 */
            0x00180E3F, /*911.75 MHz  18 */
            0x00180E6F, /*923.75 MHz  42 */
            0x00180E53, /*916.75 MHz  28 */
            0x00180E79, /*926.25 MHz  47 */
            0x00180E31, /*908.25 MHz  11 */
            0x00180E43, /*912.75 MHz  20 */
            0x00180E71, /*924.25 MHz  43 */
            0x00180E51, /*916.25 MHz  27 */
            0x00180E7D, /*927.25 MHz  49 */
            0x00180E2D, /*907.25 MHz  9 */
            0x00180E3B, /*910.75 MHz  16 */
            0x00180E1D, /*903.25 MHz  1 */
            0x00180E57, /*917.75 MHz  30 */
            0x00180E7B, /*926.75 MHz  48 */
            0x00180E25, /*905.25 MHz  5 */
            0x00180E3D, /*911.25 MHz  17 */
            0x00180E73, /*924.75 MHz  44 */
            0x00180E55, /*917.25 MHz  29 */
            0x00180E77, /*925.75 MHz  46 */
            0x00180E2B, /*906.75 MHz  8 */
            0x00180E49, /*914.25 MHz  23 */
            0x00180E23, /*904.75 MHz  4 */
            0x00180E5B, /*918.75 MHz  32 */
            0x00180E6D, /*923.25 MHz  41 */
            0x00180E1B, /*902.75 MHz  0 */
            0x00180E4B, /*914.75 MHz  24 */
            0x00180E27, /*905.75 MHz  6 */
            0x00180E4F, /*915.75 MHz  26 */
            0x00180E75, /*925.25 MHz  45 */
            0x00180E29, /*906.25 MHz  7 */
            0x00180E65, /*921.25 MHz  37 */
            0x00180E45, /*913.25 MHz  21 */
            0x00180E67, /*921.75 MHz  38 */
            0x00180E21, /*904.25 MHz  3 */
        };

        private readonly uint[] fccFreqSortedIdx_Ver20170001 = new uint[]{
            25, 36, 13, 19, 31,
            35, 14, 15, 34, 40,
            12, 22, 2, 33, 39,
            10, 18, 42, 28, 47,
            11, 20, 43, 27, 49,
            9, 16, 1, 30, 48,
            5, 17, 44, 29, 46,
            8, 23, 4, 32, 41,
            0, 24, 6, 26, 45,
            7, 37, 21, 38, 3
        };

        #endregion

        #region South Africa
        /// <summary>
        /// South Africa Frequency Table
        /// </summary>
        private readonly double[] ZATableOfFreq = new double[]
            {
                915.7,
                915.9,
                916.1,
                916.3,
                916.5,
                916.7,
                916.9,
                917.1,
                917.3,
                917.5,
                917.7,
                917.9,
                918.1,
                918.3,
                918.5,
                918.7,
            };
        /*OK*/
        private uint[] zaFreqTable = new uint[]
        {
            0x003C23C5, /*915.7 MHz   */ 
            0x003C23C7, /*915.9 MHz   */
            0x003C23C9, /*916.1 MHz   */
            0x003C23CB, /*916.3 MHz   */
            0x003C23CD, /*916.5 MHz   */
            0x003C23CF, /*916.7 MHz   */
            0x003C23D1, /*916.9 MHz   */
            0x003C23D3, /*917.1 MHz   */
            0x003C23D5, /*917.3 MHz   */
            0x003C23D7, /*917.5 MHz   */
            0x003C23D9, /*917.7 MHz   */
            0x003C23DB, /*917.9 MHz   */
            0x003C23DD, /*918.1 MHz   */
            0x003C23DF, /*918.3 MHz   */
            0x003C23E1, /*918.5 MHz   */
            0x003C23E3, /*918.7 MHz   */
        };
        /// <summary>
        /// FCC Frequency Channel number
        /// </summary>
        private const uint ZA_CHN_CNT = 16;
        private readonly uint[] zaFreqSortedIdx = new uint[]{
            0,1,2,3,
            4,5,6,7,
            8,9,10,11,
            12,13,14,15
        };
        #endregion

        #region ETSI, G800
        /// <summary>
        /// ETSI, G800 and India Frequency Table
        /// </summary>
        private readonly double[] ETSITableOfFreq = new double[]
        {
            865.70,
            866.30,
            866.90,
            867.50,
        };

        /*OK*/
        private readonly uint[] etsiFreqTable = new uint[]
        {
            0x003C21D1, /*865.700MHz   */
            0x003C21D7, /*866.300MHz   */
            0x003C21DD, /*866.900MHz   */
            0x003C21E3, /*867.500MHz   */
        };
        /// <summary>
        /// ETSI Frequency Channel number
        /// </summary>
        private const uint ETSI_CHN_CNT = 4;
        private readonly uint[] etsiFreqSortedIdx = new uint[]{
	        0, 
	        1,
	        2,
	        3
        };

        #endregion

        #region India
        /// <summary>
        /// India Frequency Table
        /// </summary>
        private readonly double[] IDATableOfFreq = new double[]
        {
            865.70,
            866.30,
            866.90,
        };

        /*OK*/
        private readonly uint[] indiaFreqTable = new uint[]
        {
            0x003C21D1, /*865.700MHz   */
            0x003C21D7, /*866.300MHz   */
            0x003C21DD, /*866.900MHz   */
        };
        /// <summary>
        /// India Frequency Channel number
        /// </summary>
        private const uint IDA_CHN_CNT = 3;
        private readonly uint[] indiaFreqSortedIdx = new uint[]{
	        0, 
	        1,
	        2,
        };

        #endregion

        #region Australia
        /// <summary>
        /// Australia Frequency Table
        /// </summary>
        private readonly double[] AUSTableOfFreq = new double[]
        {
            920.75,
            921.25,
            921.75,
            922.25,
            922.75,
            923.25,
            923.75,
            924.25,
            924.75,
            925.25,
        };
        /*OK*/
        private readonly uint[] AusFreqTable = new uint[]
        {
            0x00180E63, /* 920.75MHz   */
            0x00180E69, /* 922.25MHz   */
            0x00180E6F, /* 923.75MHz   */
            0x00180E73, /* 924.75MHz   */
            0x00180E65, /* 921.25MHz   */
            0x00180E6B, /* 922.75MHz   */
            0x00180E71, /* 924.25MHz   */
            0x00180E75, /* 925.25MHz   */
            0x00180E67, /* 921.75MHz   */
            0x00180E6D, /* 923.25MHz   */
        };
        /// <summary>
        /// Australia Frequency Channel number
        /// </summary>
        private const uint AUS_CHN_CNT = 10;

        private readonly uint[] ausFreqSortedIdx = new uint[]{
                                                    0, 3, 6, 8, 1,
                                                    4, 7, 9, 2, 5,};
        #endregion

        #region China
        /// <summary>
        /// China Frequency Table
        /// </summary>
        private readonly double[] CHNTableOfFreq = new double[]
        {
            920.625,
            920.875,
            921.125,
            921.375,
            921.625,
            921.875,
            922.125,
            922.375,
            922.625,
            922.875,
            923.125,
            923.375,
            923.625,
            923.875,
            924.125,
            924.375,
        };
        /*OK*/
        private readonly uint[] cnFreqTable = new uint[]
        {
            0x00301CD3, /*922.375MHz   */
            0x00301CD1, /*922.125MHz   */
            0x00301CCD, /*921.625MHz   */
            0x00301CC5, /*920.625MHz   */
            0x00301CD9, /*923.125MHz   */
            0x00301CE1, /*924.125MHz   */
            0x00301CCB, /*921.375MHz   */
            0x00301CC7, /*920.875MHz   */
            0x00301CD7, /*922.875MHz   */
            0x00301CD5, /*922.625MHz   */
            0x00301CC9, /*921.125MHz   */
            0x00301CDF, /*923.875MHz   */
            0x00301CDD, /*923.625MHz   */
            0x00301CDB, /*923.375MHz   */
            0x00301CCF, /*921.875MHz   */
            0x00301CE3, /*924.375MHz   */
        };
        /// <summary>
        /// China Frequency Channel number
        /// </summary>
        private const uint CN_CHN_CNT = 16;
        private readonly uint[] cnFreqSortedIdx = new uint[]{
                                                7, 6, 4, 0,
                                                10, 14, 3, 1,
                                                9, 8, 2, 13,
                                                12, 11, 5, 15,
                                                };
        #endregion

        #region China1
        /// <summary>
        /// China Frequency Table
        /// </summary>
        private readonly double[] CHN1TableOfFreq = new double[]
        {
            920.625,
            920.875,
            921.125,
            921.375,
        };
        private readonly uint[] cn1FreqTable = new uint[]
        {
            0x00301CC5, /*920.625MHz   */
            0x00301CC7, /*920.875MHz   */
            0x00301CC9, /*921.125MHz   */
            0x00301CCB, /*921.375MHz   */
        };
        /// <summary>
        /// China Frequency Channel number
        /// </summary>
        private const uint CN1_CHN_CNT = 4;
        private readonly uint[] cn1FreqSortedIdx = new uint[]{
                                                0,1,2,3
                                                };
        #endregion
        #region China2
        /// <summary>
        /// China Frequency Table
        /// </summary>
        private readonly double[] CHN2TableOfFreq = new double[]
        {
            921.625,
            921.875,
            922.125,
            922.375,
        };
        private readonly uint[] cn2FreqTable = new uint[]
        {
            0x00301CCD, /*921.625MHz   */
            0x00301CCF, /*921.875MHz   */
            0x00301CD1, /*922.125MHz   */
			0x00301CD3, /*922.375MHz   */
        };
        #endregion
        #region China3
        /// <summary>
        /// China Frequency Table
        /// </summary>
        private readonly double[] CHN3TableOfFreq = new double[]
        {
            922.625,
            922.875,
            923.125,
            923.375,
        };
        private readonly uint[] cn3FreqTable = new uint[]
        {
            0x00301CD5, /*922.625MHz   */
            0x00301CD7, /*922.875MHz   */
            0x00301CD9, /*923.125MHz   */
            0x00301CDB, /*923.375MHz   */
        };
        #endregion
        #region China4
        /// <summary>
        /// China Frequency Table
        /// </summary>
        private readonly double[] CHN4TableOfFreq = new double[]
        {
            923.625,
            923.875,
            924.125,
            924.375,
        };
        private readonly uint[] cn4FreqTable = new uint[]
        {
            0x00301CDD, /*923.625MHz   */
            0x00301CDF, /*923.875MHz   */
            0x00301CE1, /*924.125MHz   */
            0x00301CE3, /*924.375MHz   */
        };
        #endregion
        #region China5
        /// <summary>
        /// China Frequency Table
        /// </summary>
        private readonly double[] CHN5TableOfFreq = new double[]
        {
            920.625,
            921.625,
            922.625,
            923.625,
        };
        private readonly uint[] cn5FreqTable = new uint[]
        {
            0x00301CC5, /*920.625MHz   */
            0x00301CCD, /*921.625MHz   */
            0x00301CD5, /*922.625MHz   */
            0x00301CDD, /*923.625MHz   */
        };
        #endregion
        #region China6
        /// <summary>
        /// China Frequency Table
        /// </summary>
        private readonly double[] CHN6TableOfFreq = new double[]
        {
            920.875,
            921.875,
            922.875,
            923.875,
        };
        private readonly uint[] cn6FreqTable = new uint[]
        {
            0x00301CC7, /*920.875MHz   */
            0x00301CCF, /*921.875MHz   */
            0x00301CD7, /*922.875MHz   */
            0x00301CDF, /*923.875MHz   */
        };
        #endregion
        #region China7
        /// <summary>
        /// China Frequency Table
        /// </summary>
        private readonly double[] CHN7TableOfFreq = new double[]
        {
            921.125,
            922.125,
            923.125,
            924.125,
        };
        private readonly uint[] cn7FreqTable = new uint[]
        {
             0x00301CC9, /*921.125MHz   */
             0x00301CD1, /*922.125MHz   */
             0x00301CD9, /*923.125MHz   */
             0x00301CE1, /*924.125MHz   */
        };
        #endregion
        #region China8
        /// <summary>
        /// China Frequency Table
        /// </summary>
        private readonly double[] CHN8TableOfFreq = new double[]
        {
            921.375,
            922.375,
            923.375,
            924.375,
        };
        private readonly uint[] cn8FreqTable = new uint[]
        {
            0x00301CCB, /*921.375MHz   */
            0x00301CD3, /*922.375MHz   */
            0x00301CDB, /*923.375MHz   */
            0x00301CE3, /*924.375MHz   */
        };
        #endregion
        #region China9
        /// <summary>
        /// China Frequency Table
        /// </summary>
        private readonly double[] CHN9TableOfFreq = new double[]
        {
            920.625,
            920.875,
            921.125,
        };
        private readonly uint[] cn9FreqTable = new uint[]
        {
            0x00301CC5, /*920.625MHz   */
            0x00301CC7, /*920.875MHz   */
            0x00301CC9, /*921.125MHz   */
        };
        /// <summary>
        /// China Frequency Channel number
        /// </summary>
        private const uint CN9_CHN_CNT = 3;
        private readonly uint[] cn9FreqSortedIdx = new uint[]{
                                                0,1,2
                                                };
        #endregion
        #region China10
        /// <summary>
        /// China Frequency Table
        /// </summary>
        private readonly double[] CHN10TableOfFreq = new double[]
        {
            921.625,
            921.875,
            922.125,
        };
        private readonly uint[] cn10FreqTable = new uint[]
        {
            0x00301CCD, /*921.625MHz   */
            0x00301CCF, /*921.875MHz   */
            0x00301CD1, /*922.125MHz   */
        };
        #endregion
        #region China11
        /// <summary>
        /// China Frequency Table
        /// </summary>
        private readonly double[] CHN11TableOfFreq = new double[]
        {
            922.625,
            922.875,
            923.125,
        };
        private readonly uint[] cn11FreqTable = new uint[]
        {
            0x00301CD5, /*922.625MHz   */
            0x00301CD7, /*922.875MHz   */
            0x00301CD9, /*923.125MHz   */
        };
        #endregion
        #region China12
        /// <summary>
        /// China Frequency Table
        /// </summary>
        private readonly double[] CHN12TableOfFreq = new double[]
        {
            923.625,
            923.875,
            924.125,
        };
        private readonly uint[] cn12FreqTable = new uint[]
        {
            0x00301CDD, /*923.625MHz   */
            0x00301CDF, /*923.875MHz   */
            0x00301CE1, /*924.125MHz   */
        };
        #endregion
        #region Singapo
        /// <summary>
        /// Hong Kong and Singapo Frequency Table
        /// </summary>
        private readonly double[] HKTableOfFreq = new double[]
        {
            920.75,
            921.25,
            921.75,
            922.25,
            922.75,
            923.25,
            923.75,
            924.25,
        };
        /*OK*/
        private readonly uint[] hkFreqTable = new uint[]
        {
            0x00180E63, /*920.75MHz   */
            0x00180E69, /*922.25MHz   */
            0x00180E71, /*924.25MHz   */
            0x00180E65, /*921.25MHz   */
            0x00180E6B, /*922.75MHz   */
            0x00180E6D, /*923.25MHz   */
            0x00180E6F, /*923.75MHz   */
            0x00180E67, /*921.75MHz   */
        };
        /// <summary>
        /// Hong Kong Frequency Channel number
        /// </summary>
        private const uint HK_CHN_CNT = 8;
        private readonly uint[] hkFreqSortedIdx = new uint[]{
            0, 3, 7, 1,
            4, 5, 6, 2,
        };
        #endregion

        #region OFCA (Hong Kong)
        /// <summary>
        /// Hong Kong and Singapo Frequency Table
        /// </summary>
        private readonly double[] OFCATableOfFreq = new double[]
        {
 920.416, // CH1
 920.500, // CH2
 920.583, // CH3	
 920.666, // CH4
 920.750, // CH5
 920.833, // CH6
 920.916, // CH7
 921.000, // CH8
 921.083, // CH9
 921.166, // CH10
 921.250, // CH11	
 921.333, // CH12
 921.416, // CH13	
 921.500, // CH14	
 921.583, // CH15	
 921.666, // CH16 	
 921.750, // CH17
 921.833, // CH18
 921.916, // CH19
 922.000, // CH20	
 922.083, // CH21
 922.166, // CH22
 922.250, // CH23	
 922.333, // CH24
 922.416, // CH25
 922.500, // CH26 
 922.583, // CH27
 922.666, // CH28
 922.750, // CH29
 922.833, // CH30
 922.916, // CH31
 923.000, // CH32 	
 923.083, // CH33
 923.166, // CH34	
 923.250, // CH35	
 923.333, // CH36 	
 923.416, // CH37	
 923.500, // CH38
 923.583, // CH39
 923.666, // CH40	
 923.750, // CH41	
 923.833, // CH42
 923.916, // CH43
 924.000, // CH44
 924.083, // CH45
 924.166, // CH46
 924.250, // CH47
 924.333, // CH48
 924.416, // CH49
 924.500 // CH50
        };
        /*OK*/
        private readonly uint[] ofcaFreqTable = new uint[]
        {
0x00482B3E,// 922.500 MHz CH26 
0x00482B49,// 923.416 MHz CH37	
0x00482B32,// 921.500 MHz CH14	
0x00482B38,// 922.000 MHz CH20	
0x00482B44,// 923.000 MHz CH32 	
0x00482B48,// 923.333 MHz CH36 	
0x00482B33,// 921.583 MHz CH15	
0x00482B34,// 921.666 MHz CH16 	
0x00482B47,// 923.250 MHz CH35	
0x00482B4D,// 923.750 MHz CH41	
0x00482B31,// 921.416 MHz CH13	
0x00482B3B,// 922.250 MHz CH23	
0x00482B27,// 920.583 MHz CH3	
0x00482B46,// 923.166 MHz CH34	
0x00482B4C,// 923.666 MHz CH40	
0x00482B2F,// 921.250 MHz CH11	
0x00482B37,// 921.916 MHz CH19
0x00482B4F,// 923.916 MHz CH43
0x00482B41,// 922.750 MHz CH29
0x00482B54,// 924.333 MHz CH48
0x00482B30,// 921.333 MHz CH12
0x00482B39,// 922.083 MHz CH21
0x00482B50,// 924.000 MHz CH44
0x00482B40,// 924.666 MHz CH28
0x00482B56,// 924.500 MHz CH50
0x00482B2E,// 921.166 MHz CH10
0x00482B35,// 921.750 MHz CH17
0x00482B26,// 920.500 MHz CH2
0x00482B43,// 922.916 MHz CH31
0x00482B55,// 924.416 MHz CH49
0x00482B2A,// 920.833 MHz CH6
0x00482B36,// 921.833 MHz CH18
0x00482B51,// 924.083 MHz CH45
0x00482B42,// 922.833 MHz CH30
0x00482B53,// 924.250 MHz CH47
0x00482B2D,// 921.083 MHz CH9
0x00482B3C,// 922.333 MHz CH24
0x00482B29,// 920.750 MHz CH5
0x00482B45,// 923.083 MHz CH33
0x00482B4E,// 923.833 MHz CH42
0x00482B25,// 920.416 MHz CH1
0x00482B3D,// 922.416 MHz CH25
0x00482B2B,// 920.916 MHz CH7
0x00482B3F,// 922.583 MHz CH27
0x00482B52,// 924.166 MHz CH46
0x00482B2C,// 921.000 MHz CH8
0x00482B4A,// 923.500 MHz CH38
0x00482B3A,// 922.166 MHz CH22
0x00482B4B,// 923.583 MHz CH39
0x00482B28 // 920.666 MHz CH4
        };
        /// <summary>
        /// Hong Kong Frequency Channel number
        /// </summary>
        private const uint OFCA_CHN_CNT = 50;
        private readonly uint[] ofcaFreqSortedIdx = new uint[]{
25, 36,	13,	19,	31,	
35, 14, 15, 34, 40,
12, 22, 2, 33, 39,
10, 18, 42, 28, 47,
11, 20, 43, 27, 49,
9, 16, 1, 30, 48,
5, 17, 44, 29, 46,
8, 23, 4, 32, 41,
0, 24, 6, 26, 45,
7, 37, 21, 38, 3
        };
        #endregion








        #region Japan
        /// <summary>
        /// Japan Frequency Table
        /// </summary>
        private readonly double[] JPNTableOfFreq = new double[]
        {
            952.20,
            952.40,
            952.60,
            952.80,
            953.00,
            953.20,
            953.40,
            953.60,
            953.80,
        };
        private readonly double[] JPNTableOfFreq28 = new double[]
        {
            //952.20,
            952.40,
            952.60,
            952.80,
            953.00,
            953.20,
            953.40,
            953.60,
            //953.80,
        };
        /// <summary>
        /// Japan Frequency Table
        /// </summary>
        private readonly double[] JPNTableOfFreq29 = new double[]
        {
            //952.20,
            952.40,
            952.60,
            952.80,
            953.00,
            953.20,
            953.40,
            953.60,
            953.80,
        };
        /*OK*/
        private readonly uint[] jpnFreqTable = new uint[]
        {
            0x003C2534, /*952.400MHz   Channel 2*/
            0x003C2542, /*953.800MHz   Channel 9*/
            0x003C253A, /*953.000MHz   Channel 5*/
            0x003C2540, /*953.600MHz   Channel 8*/
            0x003C2536, /*952.600MHz   Channel 3*/
            0x003C253C, /*953.200MHz   Channel 6*/
            0x003C2538, /*952.800MHz   Channel 4*/
            0x003C253E, /*953.400MHz   Channel 7*/
            0x003C2532, /*952.200MHz   Channel 1*/
        };
        private readonly uint[] jpnFreqTable28 = new uint[]
        {
            0x003C2534, /*952.400MHz   Channel 2*/
            0x003C253A, /*953.000MHz   Channel 5*/
            0x003C2540, /*953.600MHz   Channel 8*/
            0x003C2536, /*952.600MHz   Channel 3*/
            0x003C253C, /*953.200MHz   Channel 6*/
            0x003C2538, /*952.800MHz   Channel 4*/
            0x003C253E, /*953.400MHz   Channel 7*/
        };
        private readonly uint[] jpnFreqTable29 = new uint[]
        {
            0x003C2534, /*952.400MHz   Channel 2*/
            0x003C2542, /*953.800MHz   Channel 9*/
            0x003C253A, /*953.000MHz   Channel 5*/
            0x003C2540, /*953.600MHz   Channel 8*/
            0x003C2536, /*952.600MHz   Channel 3*/
            0x003C253C, /*953.200MHz   Channel 6*/
            0x003C2538, /*952.800MHz   Channel 4*/
            0x003C253E, /*953.400MHz   Channel 7*/
        };

        /// <summary>
        /// Japan Frequency Channel number
        /// </summary>
        private const uint JPN_CHN_CNT = 9;
        private const uint JPN_CHN_CNT28 = 7;
        private const uint JPN_CHN_CNT29 = 8;
        private readonly uint[] jpnFreqSortedIdx = new uint[]{
	        0, 4, 6, 2, 5, 7, 3, 1, 8
        };
        private readonly uint[] jpnFreqSortedIdx28 = new uint[]{
	        0, 4, 6, 2, 5, 3, 1
        };
        private readonly uint[] jpnFreqSortedIdx29 = new uint[]{
	        0, 4, 6, 2, 5, 7, 3, 1
        };

#if nouse
        private readonly uint[] jpnFreqTable = new uint[]
        {
            //0x003C2532, /*952.200MHz   */
            0x003C2534, /*952.400MHz   */
            0x003C2536, /*952.600MHz   */
            0x003C2538, /*952.800MHz   */
            0x003C253A, /*953.000MHz   */
            0x003C253C, /*953.200MHz   */
            0x003C253E, /*953.400MHz   */
            0x003C2540, /*953.600MHz   */
            //0x003C2542, /*953.800MHz   */
        };

        /// <summary>
        /// Japan Frequency Channel number
        /// </summary>
        private const uint JPN_CHN_CNT = 7;// CS203 is not supported channel 1 and 9;
        private readonly uint[] jpnFreqSortedIdx = new uint[]{
	        0, 1, 2,
            3, 4, 5,
            6, //7, 8,
        };
#endif
        #endregion

        #region Japan 2012
        /// <summary>
        /// Japan 2012 Frequency Table
        /// </summary>
        private readonly double[] JPN2012TableOfFreq = new double[]
        {
            916.80,
            918.00,
            919.20,
            920.40,
            //920.60,
            //920.80,
        };
        /*OK*/
        private readonly uint[] jpn2012FreqTable = new uint[]
        {
            0x003C23D0, /*916.800MHz   Channel 1*/
            0x003C23DC, /*918.000MHz   Channel 2*/
            0x003C23E8, /*919.200MHz   Channel 3*/
            0x003C23F4, /*920.400MHz   Channel 4*/
            //0x003C23F6, /*920.600MHz   Channel 5*/
            //0x003C23F8, /*920.800MHz   Channel 6*/
        };
        /// <summary>
        /// Japan Frequency Channel number
        /// </summary>
        private const uint JPN2012_CHN_CNT = 4;
        private readonly uint[] jpn2012FreqSortedIdx = new uint[]{
	        0, 1, 2, 3
        };

        #endregion

        #region Japan 2019
        /// <summary>
        /// Japan 2012 Frequency Table
        /// </summary>
        private readonly double[] JPN2019TableOfFreq = new double[]
        {
            916.80,
            918.00,
            919.20,
            920.40,
            920.60,
            920.80,
        };
        /*OK*/
        private readonly uint[] jpn2019FreqTable = new uint[]
        {
            0x003C23D0, /*916.800MHz   Channel 1*/
            0x003C23DC, /*918.000MHz   Channel 2*/
            0x003C23E8, /*919.200MHz   Channel 3*/
            0x003C23F4, /*920.400MHz   Channel 4*/
            0x003C23F6, /*920.600MHz   Channel 5*/
            0x003C23F8, /*920.800MHz   Channel 6*/
        };
        /// <summary>
        /// Japan Frequency Channel number
        /// </summary>
        private const uint JPN2019_CHN_CNT = 6;
        private readonly uint[] jpn2019FreqSortedIdx = new uint[]{
	        0, 1, 2, 3, 4, 5
        };

        #endregion



        #region Korea
        /// <summary>
        /// Korea Frequency Table
        /// </summary>
        private double[] KRTableOfFreq = new double[]
        {
            917.30,
            917.90,
            918.50,
            919.10,
            919.70,
            920.30
        };

        /*Not same as CS101???*/
        private uint[] krFreqTable = new uint[]
        {
            0x003C23E7, /*919.1 MHz   */
            0x003C23D5, /*917.3 MHz   */
            0x003C23F3, /*920.3 MHz   */
            0x003C23DB, /*917.9 MHz   */
            0x003C23ED, /*919.7 MHz   */
            0x003C23E1, /*918.5 MHz   */
        };

        /// <summary>
        /// Korea Frequency Channel number
        /// </summary>
        private const uint KR_CHN_CNT = 6;
        private readonly uint[] krFreqSortedIdx = new uint[]{
            3, 0, 5, 1, 4, 2
        };



#if oldkoreafreq
      /// <summary>
        /// Korea Frequency Table
        /// </summary>
        private double[] KRTableOfFreq = new double[]
        {
            910.20,
            910.40,
            910.60,
            910.80,
            911.00,
            911.20,
            911.40,
            911.60,
            911.80,
            912.00,
            912.20,
            912.40,
            912.60,
            912.80,
            913.00,
            913.20,
            913.40,
            913.60,
            913.80,
        };

        /*Not same as CS101???*/
        private uint[] krFreqTable = new uint[]
        {
            0x003C23A8, /*912.8MHz   13*/
            0x003C23A0, /*912.0MHz   9*/
            0x003C23AC, /*913.2MHz   15*/
            0x003C239E, /*911.8MHz   8*/
            0x003C23A4, /*912.4MHz   11*/
            0x003C23B2, /*913.8MHz   18*/
            0x003C2392, /*910.6MHz   2*/
            0x003C23B0, /*913.6MHz   17*/
            0x003C2390, /*910.4MHz   1*/
            0x003C239C, /*911.6MHz   7*/
            0x003C2396, /*911.0MHz   4*/
            0x003C23A2, /*912.2MHz   10*/
            0x003C238E, /*910.2MHz   0*/
            0x003C23A6, /*912.6MHz   12*/
            0x003C2398, /*911.2MHz   5*/
            0x003C2394, /*910.8MHz   3*/
            0x003C23AE, /*913.4MHz   16*/
            0x003C239A, /*911.4MHz   6*/
            0x003C23AA, /*913.0MHz   14*/
        };

        /// <summary>
        /// Korea Frequency Channel number
        /// </summary>
        private const uint KR_CHN_CNT = 19;
        private readonly uint[] krFreqSortedIdx = new uint[]{
	        13, 9, 15, 8, 11,
	        18, 2, 17, 1, 7,
	        4, 10, 0, 12, 5,
	        3, 16, 6, 14
        };
#endif
        /*private const uint VIRTUAL_KR_DIVRAT = 0x001E0000;
        private const uint VIRTUAL_KR_CHN_CNT = 19;
        private readonly uint[] Virtual_krFreqMultRat = new uint[]{ // with 0x001E as DIVRat
            0x11CA,0x11CE,0x11D2,0x11CD,0x11D6,
            0x11D8,0x11D3,0x11CF,0x11CB,0x11C9,
            0x11C7,0x11D1,0x11D4,0x11D9,0x11D7,
            0x11D5,0x11D0,0x11CC,0x11C8
        };
        private readonly uint[] Virtual_krFreqSortedIdx = new uint[]{
            10, 18, 9, 0, 8,
            17, 3, 1, 7, 16,
            11, 2, 6, 12, 15,
            4, 14, 5, 13
        };*/


        #endregion

        #region Malaysia
        /// <summary>
        /// Malaysia Frequency Table
        /// </summary>
        private double[] MYSTableOfFreq = new double[]
        {
            919.75,
            920.25,
            920.75,
            921.25,
            921.75,
            922.25,
        };

        private uint[] mysFreqTable = new uint[]
        {
            0x00180E5F, /*919.75MHz   */
            0x00180E65, /*921.25MHz   */
            0x00180E61, /*920.25MHz   */
            0x00180E67, /*921.75MHz   */
            0x00180E63, /*920.75MHz   */
            0x00180E69, /*922.25MHz   */
        };

        /// <summary>
        /// Malaysia Frequency Channel number
        /// </summary>
        private const uint MYS_CHN_CNT = 6;
        private readonly uint[] mysFreqSortedIdx = new uint[]{
                                                    0, 3, 1,
                                                    4, 2, 5,
                                                    };

        #endregion

        #region Taiwan
        /// <summary>
        /// Taiwan Frequency Table
        /// </summary>
        private double[] TWTableOfFreq = new double[]
        {
            920.25,
            920.75,
            921.25,
            921.75,
            922.25,
            922.75,
            923.25,
            923.75,
            924.25,
            924.75,
            925.25,
            925.75,
            926.25,
            926.75,
            927.25,
            927.75,
        };

        private uint[] twFreqTable = new uint[]
        {
            0x00180E6F, /*923.75MHz   3*/
            0x00180E7B, /*926.75MHz   9*/
            0x00180E65, /*921.25 MHz   */
            0x00180E6D, /*923.25MHz   2*/
            0x00180E7F, /*927.75MHz   11*/
            0x00180E69, /*922.25MHz   0*/
            0x00180E75, /*925.25MHz   6*/
            0x00180E63, /*920.75 MHz   */
            0x00180E73, /*924.75MHz   5*/
            0x00180E7D, /*927.25MHz   10*/
            0x00180E71, /*924.25MHz   4*/
            0x00180E61, /*920.25 MHz   */
            0x00180E79, /*926.25MHz   8*/
            0x00180E6B, /*922.75MHz   1*/
            0x00180E67, /*921.75 MHz   */
            0x00180E77, /*925.75MHz   7*/
        };
        /// <summary>
        /// Taiwan Frequency Channel number
        /// </summary>
        private const uint TW_CHN_CNT = 16;
        private readonly uint[] twFreqSortedIdx = new uint[]{
            7, 13, 2, 6, 15, 4, 10, 1, 9, 14, 8, 0, 12, 5, 3, 11
        };

        private double[] TW2TableOfFreq = new double[]
        {
            921.00,
            921.40,
            921.80,
            922.20,
            922.60,
            923.00,
            923.40,
            923.80,
            924.20,
            924.60,
            925.00,
            925.40,
            925.80,
            926.20,
            926.60,
            927.00,
        };

        private uint[] tw2FreqTable = new uint[]
        {
            0x00482B2C,// 921.000 MHz 
            0x00D27DF9, //  921.4
            0x00D27E07, //  921.8
            0x00C87814, //  922.2
            0x00D27E23, //  922.6
            0x00482B44,// 923.000 MHz	
            0x00C8783C, //  923.4
            0x00D27E4D, //  923.8
            0x00D27E5B, //  924.2
            0x00D27E69, //  924.6
            0x00482B5C,// 925.000 MHz	
            0x00D27E85, //  925.4
            0x00D27E93, //  925.8
            0x00D27EA1, //  926.2
            0x00D27EAF, //  926.6
            0x001009a8, // 927.000
        };
        /// <summary>
        /// Taiwan Frequency Channel number
        /// </summary>
        private const uint TW2_CHN_CNT = 16;
        private readonly uint[] tw2FreqSortedIdx = new uint[]{
            0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15
        };

#if OLD_TW

        /// <summary>
        /// Taiwan Frequency Table
        /// </summary>
        private double[] TWTableOfFreq = new double[]
        {
#if R1000
            922.25,
            922.75,
            923.25,
            923.75,
            924.25,
            924.75,
            925.25,
            925.75,
            926.25,
            926.75,
            927.25,
            927.75,
#else
    922.875,
    923.250,
    923.625,
    924.000,
    924.375,
    924.750,
    925.125,
    925.500,
    925.875,
    926.250,
    926.625,
    927.000
#endif
        };

        /*Not same as CS101*/
        private uint[] twFreqTable = new uint[]
        {
#if R1000
            0x00180E7D, /*927.25MHz   10*/
            0x00180E73, /*924.75MHz   5*/
            0x00180E6B, /*922.75MHz   1*/
            0x00180E75, /*925.25MHz   6*/
            0x00180E7F, /*927.75MHz   11*/
            0x00180E71, /*924.25MHz   4*/
            0x00180E79, /*926.25MHz   8*/
            0x00180E6D, /*923.25MHz   2*/
            0x00180E7B, /*926.75MHz   9*/
            0x00180E69, /*922.25MHz   0*/
            0x00180E77, /*925.75MHz   7*/
            0x00180E6F, /*923.75MHz   3*/
#else
            0x001009a7, /* 926.625 10 */
            0x001009a2, /* 924.750 5 */
            0x0010099e, /* 923.250 1 */
            0x001009a3, /* 925.125 6 */
            0x001009a8, /* 927.000 11 */
            0x001009a1, /* 924.375 4 */
            0x001009a5, /* 925.875 8 */
            0x0010099f, /* 923.625 2 */
            0x001009a6, /* 926.250 9 */
            0x0010099d, /* 922.875 0 */
            0x001009a4, /* 925.500 7 */
            0x001009a0, /* 924.000 3 */
#endif
        };
        /// <summary>
        /// Taiwan Frequency Channel number
        /// </summary>
        private const uint TW_CHN_CNT = 12;
        private readonly uint[] twFreqSortedIdx = new uint[]{
	        10, 5, 1, 6,
            11, 4, 8, 2,
            9, 0, 7, 3,
        };
#endif

        #endregion

        #region Brazil

        private double[] BR1TableOfFreq = new double[]
            {
                /*902.75,
                903.25,
                903.75,
                904.25,
                904.75,
                905.25,
                905.75,
                906.25,
                906.75,
                907.25,
                907.75,
                908.25,
                908.75,
                909.25,
                909.75,
                910.25,
                910.75,
                911.25,
                911.75,
                912.25,
                912.75,
                913.25,
                913.75,
                914.25,
                914.75,
                915.25,*/
                915.75,
                916.25,
                916.75,
                917.25,
                917.75,
                918.25,
                918.75,
                919.25,
                919.75,
                920.25,
                920.75,
                921.25,
                921.75,
                922.25,
                922.75,
                923.25,
                923.75,
                924.25,
                924.75,
                925.25,
                925.75,
                926.25,
                926.75,
                927.25,
            };
        private uint[] br1FreqTable = new uint[]
        {
            0x00180E4F, /*915.75 MHz   */
            //0x00180E4D, /*915.25 MHz   */
            //0x00180E1D, /*903.25 MHz   */
            0x00180E7B, /*926.75 MHz   */
            0x00180E79, /*926.25 MHz   */
            //0x00180E21, /*904.25 MHz   */
            0x00180E7D, /*927.25 MHz   */
            0x00180E61, /*920.25 MHz   */
            0x00180E5D, /*919.25 MHz   */
            //0x00180E35, /*909.25 MHz   */
            0x00180E5B, /*918.75 MHz   */
            0x00180E57, /*917.75 MHz   */
            //0x00180E25, /*905.25 MHz   */
            //0x00180E23, /*904.75 MHz   */
            0x00180E75, /*925.25 MHz   */
            0x00180E67, /*921.75 MHz   */
            //0x00180E4B, /*914.75 MHz   */
            //0x00180E2B, /*906.75 MHz   */
            //0x00180E47, /*913.75 MHz   */
            0x00180E69, /*922.25 MHz   */
            //0x00180E3D, /*911.25 MHz   */
            //0x00180E3F, /*911.75 MHz   */
            //0x00180E1F, /*903.75 MHz   */
            //0x00180E33, /*908.75 MHz   */
            //0x00180E27, /*905.75 MHz   */
            //0x00180E41, /*912.25 MHz   */
            //0x00180E29, /*906.25 MHz   */
            0x00180E55, /*917.25 MHz   */
            //0x00180E49, /*914.25 MHz   */
            //0x00180E2D, /*907.25 MHz   */
            0x00180E59, /*918.25 MHz   */
            0x00180E51, /*916.25 MHz   */
            //0x00180E39, /*910.25 MHz   */
            //0x00180E3B, /*910.75 MHz   */
            //0x00180E2F, /*907.75 MHz   */
            0x00180E73, /*924.75 MHz   */
            //0x00180E37, /*909.75 MHz   */
            0x00180E5F, /*919.75 MHz   */
            0x00180E53, /*916.75 MHz   */
            //0x00180E45, /*913.25 MHz   */
            0x00180E6F, /*923.75 MHz   */
            //0x00180E31, /*908.25 MHz   */
            0x00180E77, /*925.75 MHz   */
            //0x00180E43, /*912.75 MHz   */
            0x00180E71, /*924.25 MHz   */
            0x00180E65, /*921.25 MHz   */
            0x00180E63, /*920.75 MHz   */
            0x00180E6B, /*922.75 MHz   */
            //0x00180E1B, /*902.75 MHz   */
            0x00180E6D, /*923.25 MHz   */
        };
        /// <summary>
        /// Brazil1 Frequency Channel number
        /// </summary>
        private const uint BR1_CHN_CNT = 24;
        private readonly uint[] br1FreqSortedIdx = new uint[]{
	        0, 22, 21, 23,
            9, 7, 6, 4,
            19, 12, 13, 3,
            5, 1, 18, 8,
            2, 16, 20, 17,
            11, 10, 14, 15,
        };

        private double[] BR2TableOfFreq = new double[]
            {
                902.75,
                903.25,
                903.75,
                904.25,
                904.75,
                905.25,
                905.75,
                906.25,
                906.75,
                /*907.25,
                907.75,
                908.25,
                908.75,
                909.25,
                909.75,
                910.25,
                910.75,
                911.25,
                911.75,
                912.25,
                912.75,
                913.25,
                913.75,
                914.25,
                914.75,
                915.25,*/
                915.75,
                916.25,
                916.75,
                917.25,
                917.75,
                918.25,
                918.75,
                919.25,
                919.75,
                920.25,
                920.75,
                921.25,
                921.75,
                922.25,
                922.75,
                923.25,
                923.75,
                924.25,
                924.75,
                925.25,
                925.75,
                926.25,
                926.75,
                927.25,
            };

        private uint[] br2FreqTable = new uint[]
            {
                0x00180E4F, /*915.75 MHz   */
                //0x00180E4D, /*915.25 MHz   */
                0x00180E1D, /*903.25 MHz   */
                0x00180E7B, /*926.75 MHz   */
                0x00180E79, /*926.25 MHz   */
                0x00180E21, /*904.25 MHz   */
                0x00180E7D, /*927.25 MHz   */
                0x00180E61, /*920.25 MHz   */
                0x00180E5D, /*919.25 MHz   */
                //0x00180E35, /*909.25 MHz   */
                0x00180E5B, /*918.75 MHz   */
                0x00180E57, /*917.75 MHz   */
                0x00180E25, /*905.25 MHz   */
                0x00180E23, /*904.75 MHz   */
                0x00180E75, /*925.25 MHz   */
                0x00180E67, /*921.75 MHz   */
                //0x00180E4B, /*914.75 MHz   */
                0x00180E2B, /*906.75 MHz   */
                //0x00180E47, /*913.75 MHz   */
                0x00180E69, /*922.25 MHz   */
                //0x00180E3D, /*911.25 MHz   */
                //0x00180E3F, /*911.75 MHz   */
                0x00180E1F, /*903.75 MHz   */
                //0x00180E33, /*908.75 MHz   */
                0x00180E27, /*905.75 MHz   */
                //0x00180E41, /*912.25 MHz   */
                0x00180E29, /*906.25 MHz   */
                0x00180E55, /*917.25 MHz   */
                //0x00180E49, /*914.25 MHz   */
                //0x00180E2D, /*907.25 MHz   */
                0x00180E59, /*918.25 MHz   */
                0x00180E51, /*916.25 MHz   */
                //0x00180E39, /*910.25 MHz   */
                //0x00180E3B, /*910.75 MHz   */
                //0x00180E2F, /*907.75 MHz   */
                0x00180E73, /*924.75 MHz   */
                //0x00180E37, /*909.75 MHz   */
                0x00180E5F, /*919.75 MHz   */
                0x00180E53, /*916.75 MHz   */
                //0x00180E45, /*913.25 MHz   */
                0x00180E6F, /*923.75 MHz   */
                //0x00180E31, /*908.25 MHz   */
                0x00180E77, /*925.75 MHz   */
                //0x00180E43, /*912.75 MHz   */
                0x00180E71, /*924.25 MHz   */
                0x00180E65, /*921.25 MHz   */
                0x00180E63, /*920.75 MHz   */
                0x00180E6B, /*922.75 MHz   */
                0x00180E1B, /*902.75 MHz   */
                0x00180E6D, /*923.25 MHz   */
            };

        /// <summary>
        /// Brazil2 Frequency Channel number
        /// </summary>
        private const uint BR2_CHN_CNT = 33;
        private readonly uint[] br2FreqSortedIdx = new uint[]{
	        9, 1, 31,
            30, 3, 32,
            18, 16, 15,
            13, 5, 4,
            28, 21, 8,
            22, 2, 6,
            7, 12, 14,
            10, 27, 17,
            11, 25, 29,
            26, 20, 19,
            23, 0, 24,
        };

        private double[] BR3TableOfFreq = new double[]
            {
                902.75, // 0
                903.25, // 1
                903.75, // 2
                904.25, // 3
                904.75, // 4
                905.25, // 5
                905.75, // 6
                906.25, // 7
                906.75, // 8
            };
        private uint[] br3FreqTable = new uint[]
            {
                0x00180E1D, /*903.25 MHz   */
                0x00180E21, /*904.25 MHz   */
                0x00180E25, /*905.25 MHz   */
                0x00180E23, /*904.75 MHz   */
                0x00180E2B, /*906.75 MHz   */
                0x00180E1F, /*903.75 MHz   */
                0x00180E27, /*905.75 MHz   */
                0x00180E29, /*906.25 MHz   */
                0x00180E1B, /*902.75 MHz   */
            };
        /// <summary>
        /// Brazil3 Frequency Channel number
        /// </summary>
        private const uint BR3_CHN_CNT = 9;
        private readonly uint[] br3FreqSortedIdx = new uint[]{
            1, 3, 5, 4, 8, 2, 6, 7, 0
        };

        private double[] BR4TableOfFreq = new double[]
            {
                902.75,
                903.25,
                903.75,
                904.25,
            };
        private uint[] br4FreqTable = new uint[]
            {
                0x00180E1D, /*903.25 MHz   */
                0x00180E21, /*904.25 MHz   */
                0x00180E1F, /*903.75 MHz   */
                0x00180E1B, /*902.75 MHz   */
            };
        /// <summary>
        /// Brazil2 Frequency Channel number
        /// </summary>
        private const uint BR4_CHN_CNT = 4;
        private readonly uint[] br4FreqSortedIdx = new uint[]{
            1, 3, 2, 0
        };

        private double[] BR5TableOfFreq = new double[]
            {
                917.75, // 0
                918.25, // 1
                918.75, // 2
                919.25, // 3
                919.75, // 4
                920.25, // 5
                920.75, // 6
                921.25, // 7
                921.75, // 8
                922.25, // 9
                922.75, // 10
                923.25, // 11
                923.75, // 12
                924.25, // 13
            };
        private uint[] br5FreqTable = new uint[]
            {
                0x00180E61, /*920.25 MHz   */
                0x00180E5D, /*919.25 MHz   */
                0x00180E5B, /*918.75 MHz   */
                0x00180E57, /*917.75 MHz   */
                0x00180E67, /*921.75 MHz   */
                0x00180E69, /*922.25 MHz   */
                0x00180E59, /*918.25 MHz   */
                0x00180E5F, /*919.75 MHz   */
                0x00180E6F, /*923.75 MHz   */
                0x00180E71, /*924.25 MHz   */
                0x00180E65, /*921.25 MHz   */
                0x00180E63, /*920.75 MHz   */
                0x00180E6B, /*922.75 MHz   */
                0x00180E6D, /*923.25 MHz   */

            };
        /// <summary>
        /// Brazil2 Frequency Channel number
        /// </summary>
        private const uint BR5_CHN_CNT = 14;
        private readonly uint[] br5FreqSortedIdx = new uint[]{
            5, 3, 2, 0, 8, 9, 1, 4, 12, 13, 7, 6, 10, 11
        };

#endregion

#region Indonesia
        /// <summary>
        /// Indonesia Frequency Table
        /// </summary>
        private readonly double[] IDTableOfFreq = new double[]
        {
            923.25,
            923.75,
            924.25,
            924.75,
        };

        /*OK*/
        private readonly uint[] indonesiaFreqTable = new uint[]
        {
            0x00180E6D, /*923.25 MHz    */
            0x00180E6F,/*923.75 MHz    */
            0x00180E71,/*924.25 MHz    */
            0x00180E73,/*924.75 MHz    */

        };
        /// <summary>
        /// Indonesia Frequency Channel number
        /// </summary>
        private const uint ID_CHN_CNT = 4;
        private readonly uint[] indonesiaFreqSortedIdx = new uint[]{
	        0, 
	        1,
	        2,
            3
        };

#region UH1
        /// <summary>
        /// FCC UH Frequency Table 915-920
        /// </summary>
        private readonly double[] UH1TableOfFreq = new double[]
            {
                915.25,     // 0
                915.75,     // 1
                916.25,     // 2
                916.75,     // 3
                917.25,     // 4
                917.75,     // 5
                918.25,     // 6
                918.75,     // 7
                919.25,     // 8
                919.75,     // 9
            };
        /*OK*/
        private uint[] uh1FreqTable = new uint[]
        {
            0x00180E4F, /*915.75 MHz   */
            0x00180E4D, /*915.25 MHz   */
            0x00180E5D, /*919.25 MHz   */
            0x00180E5B, /*918.75 MHz   */
            0x00180E57, /*917.75 MHz   */
            0x00180E55, /*917.25 MHz   */
            0x00180E59, /*918.25 MHz   */
            0x00180E51, /*916.25 MHz   */
            0x00180E5F, /*919.75 MHz   */
            0x00180E53, /*916.75 MHz   */
        };
        /// <summary>
        /// FCC UH Frequency Channel number
        /// </summary>
        private const uint UH1_CHN_CNT = 10;
        private readonly uint[] uh1FreqSortedIdx = new uint[]{
	        1, 0, 8, 7, 5, 4, 6, 2, 9, 3
        };
#endregion

#region UH2
        /// <summary>
        /// FCC UH Frequency Table 920-928
        /// </summary>
        private readonly double[] UH2TableOfFreq = new double[]
            {
                920.25,   // 0
                920.75,   // 1
                921.25,   // 2
                921.75,   // 3
                922.25,   // 4
                922.75,   // 5
                923.25,   // 6
                923.75,   // 7
                924.25,   // 8
                924.75,   // 9
                925.25,   // 10
                925.75,   // 11
                926.25,   // 12
                926.75,   // 13
                927.25,   // 14
            };
        /*OK*/
        private uint[] uh2FreqTable = new uint[]
        {
            0x00180E7B, /*926.75 MHz   */
            0x00180E79, /*926.25 MHz   */
            0x00180E7D, /*927.25 MHz   */
            0x00180E61, /*920.25 MHz   */
            0x00180E75, /*925.25 MHz   */
            0x00180E67, /*921.75 MHz   */
            0x00180E69, /*922.25 MHz   */
            0x00180E73, /*924.75 MHz   */
            0x00180E6F, /*923.75 MHz   */
            0x00180E77, /*925.75 MHz   */
            0x00180E71, /*924.25 MHz   */
            0x00180E65, /*921.25 MHz   */
            0x00180E63, /*920.75 MHz   */
            0x00180E6B, /*922.75 MHz   */
            0x00180E6D, /*923.25 MHz   */
        };
        /// <summary>
        /// FCC UH Frequency Channel number
        /// </summary>
        private const uint UH2_CHN_CNT = 15;
        private readonly uint[] uh2FreqSortedIdx = new uint[]{
            13, 12, 14, 0, 10, 3, 4, 9, 7, 11, 8, 2, 1, 5, 6, 
        };
#endregion

#region LH

        private double[] LHTableOfFreq = new double[]
            {
                902.75, // 0
                903.25, // 1
                903.75, // 2
                904.25, // 3
                904.75, // 4
                905.25, // 5
                905.75, // 6
                906.25, // 7
                906.75, // 8
                907.25, // 9
                907.75, // 10
                908.25, // 11
                908.75, // 12
                909.25, // 13
                909.75, // 14
                910.25, // 15
                910.75, // 16
                911.25, // 17
                911.75, // 18
                912.25, // 19
                912.75, // 20
                913.25, // 21
                913.75, // 22
                914.25, // 23
                914.75, // 24
                915.25, // 25
                
                /*915.75,
                916.25,
                916.75,
                917.25,
                917.75,
                918.25,
                918.75,
                919.25,
                919.75,
                920.25,
                920.75,
                921.25,
                921.75,
                922.25,
                922.75,
                923.25,
                923.75,
                924.25,
                924.75,
                925.25,
                925.75,
                926.25,
                926.75,
                927.25,*/
            };
        private uint[] lhFreqTable = new uint[]
        {
            0x00180E1B, /*902.75 MHz   */
            0x00180E35, /*909.25 MHz   */
            0x00180E1D, /*903.25 MHz   */
            0x00180E37, /*909.75 MHz   */
            0x00180E1F, /*903.75 MHz   */
            0x00180E39, /*910.25 MHz   */
            0x00180E21, /*904.25 MHz   */
            0x00180E3B, /*910.75 MHz   */
            0x00180E23, /*904.75 MHz   */
            0x00180E3D, /*911.25 MHz   */
            0x00180E25, /*905.25 MHz   */
            0x00180E3F, /*911.75 MHz   */
            0x00180E27, /*905.75 MHz   */
            0x00180E41, /*912.25 MHz   */
            0x00180E29, /*906.25 MHz   */
            0x00180E43, /*912.75 MHz   */
            0x00180E2B, /*906.75 MHz   */
            0x00180E45, /*913.25 MHz   */
            0x00180E2D, /*907.25 MHz   */
            0x00180E47, /*913.75 MHz   */
            0x00180E2F, /*907.75 MHz   */
            0x00180E49, /*914.25 MHz   */
            0x00180E31, /*908.25 MHz   */
            0x00180E4B, /*914.75 MHz   */
            0x00180E33, /*908.75 MHz   */
            0x00180E4D, /*915.25 MHz   */


            //0x00180E4F, /*915.75 MHz   */
            //0x00180E7B, /*926.75 MHz   */
            //0x00180E79, /*926.25 MHz   */
            //0x00180E7D, /*927.25 MHz   */
            //0x00180E61, /*920.25 MHz   */
            //0x00180E5D, /*919.25 MHz   */
            //0x00180E5B, /*918.75 MHz   */
            //0x00180E57, /*917.75 MHz   */
            //0x00180E75, /*925.25 MHz   */
            //0x00180E67, /*921.75 MHz   */
            //0x00180E69, /*922.25 MHz   */
            //0x00180E55, /*917.25 MHz   */
            //0x00180E59, /*918.25 MHz   */
            //0x00180E51, /*916.25 MHz   */
            //0x00180E73, /*924.75 MHz   */
            //0x00180E5F, /*919.75 MHz   */
            //0x00180E53, /*916.75 MHz   */
            //0x00180E6F, /*923.75 MHz   */
            //0x00180E77, /*925.75 MHz   */
            //0x00180E71, /*924.25 MHz   */
            //0x00180E65, /*921.25 MHz   */
            //0x00180E63, /*920.75 MHz   */
            //0x00180E6B, /*922.75 MHz   */
            //0x00180E6D, /*923.25 MHz   */
        };
        /// <summary>
        /// Brazil1 Frequency Channel number
        /// </summary>
        private const uint LH_CHN_CNT = 26;
        private readonly uint[] lhFreqSortedIdx = new uint[]{
        0, 13, 1, 14, 2, 15, 3, 16, 4, 17, 5, 18, 6, 19, 7, 20, 8, 21, 9, 22, 10, 23, 11, 24, 12, 25 
            /*
 * 0, 22, 21, 23,
            9, 7, 6, 4,
            19, 12, 13, 3,
            5, 1, 18, 8,
            2, 16, 20, 17,
            11, 10, 14, 15,
*/
        };

        private double[] LH1TableOfFreq = new double[]
            {
                902.75, // 0
                903.25, // 1
                903.75, // 2
                904.25, // 3
                904.75, // 4
                905.25, // 5
                905.75, // 6
                906.25, // 7
                906.75, // 8
                907.25, // 9
                907.75, // 10
                908.25, // 11
                908.75, // 12
                909.25, // 13
            };
        private uint[] lh1FreqTable = new uint[]
        {
            0x00180E1B, /*902.75 MHz   */
            0x00180E35, /*909.25 MHz   */
            0x00180E1D, /*903.25 MHz   */
            0x00180E1F, /*903.75 MHz   */
            0x00180E21, /*904.25 MHz   */
            0x00180E23, /*904.75 MHz   */
            0x00180E25, /*905.25 MHz   */
            0x00180E27, /*905.75 MHz   */
            0x00180E29, /*906.25 MHz   */
            0x00180E2B, /*906.75 MHz   */
            0x00180E2D, /*907.25 MHz   */
            0x00180E2F, /*907.75 MHz   */
            0x00180E31, /*908.25 MHz   */
            0x00180E33, /*908.75 MHz   */
        };
        /// <summary>
        /// Brazil1 Frequency Channel number
        /// </summary>
        private const uint LH1_CHN_CNT = 14;
        private readonly uint[] lh1FreqSortedIdx = new uint[]{
        0, 13, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 
        };


        private double[] LH2TableOfFreq = new double[]
            {
                909.75, // 0
                910.25, // 1
                910.75, // 2
                911.25, // 3
                911.75, // 4
                912.25, // 5
                912.75, // 6
                913.25, // 7
                913.75, // 8
                914.25, // 9
                914.75, // 10
            };

        private uint[] lh2FreqTable = new uint[]
        {
            0x00180E37, /*909.75 MHz   */
            0x00180E39, /*910.25 MHz   */
            0x00180E3B, /*910.75 MHz   */
            0x00180E3D, /*911.25 MHz   */
            0x00180E3F, /*911.75 MHz   */
            0x00180E41, /*912.25 MHz   */
            0x00180E43, /*912.75 MHz   */
            0x00180E45, /*913.25 MHz   */
            0x00180E47, /*913.75 MHz   */
            0x00180E49, /*914.25 MHz   */
            0x00180E4B, /*914.75 MHz   */
        };
        /// <summary>
        /// Brazil1 Frequency Channel number
        /// </summary>
        private const uint LH2_CHN_CNT = 11;
        private readonly uint[] lh2FreqSortedIdx = new uint[]{
        0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10
        };

#endregion
        
#endregion

#region JE

        private double[] JETableOfFreq = new double[]
        {
                915.25, // 0
                915.5,  // 1
                915.75, // 2
                916.0,  // 3
                916.25, // 4
                916.5,  // 5
                916.75, // 6
            };
        private uint[] jeFreqTable = new uint[]
        {
            0x00180E4D, /*915.25 MHz   */
            0x00180E51, /*916.25 MHz   */
            0x00180E4E, /*915.5 MHz   */
            0x00180E52, /*916.5 MHz   */
            0x00180E4F, /*915.75 MHz   */
            0x00180E53, /*916.75 MHz   */
            0x00180E50, /*916.0 MHz   */
        };
        /// <summary>
        /// Brazil1 Frequency Channel number
        /// </summary>
        private const uint JE_CHN_CNT = 7;
        private readonly uint[] jeFreqSortedIdx = new uint[]{
        0, 4, 1, 5, 2, 6, 3
        };

#endregion

#region BackoffTable
        private UInt32[] etsiBackoffTable = new uint[]
        {
            0x0000175a, /*       5978 usecs */
            0x000016d5, /*       5845 usecs */
            0x0000225a, /*       8794 usecs */
            0x0000219f, /*       8607 usecs */
            0x00001fdd, /*       8157 usecs */
            0x00001cb4, /*       7348 usecs */
            0x000026c9, /*       9929 usecs */
            0x000026c6, /*       9926 usecs */
            0x00001e66, /*       7782 usecs */
            0x0000140d, /*       5133 usecs */
            0x00001ead  /*       7853 usecs */
        };

        private UInt32[] japanBackoffTable = new uint[]
        {
            0x00001388, /*       5978 usecs */
            0x00001388, /*       5845 usecs */
            0x00001388, /*       8794 usecs */
            0x00001388, /*       8607 usecs */
            0x00001388, /*       8157 usecs */
            0x00001388, /*       7348 usecs */
            0x00001388, /*       9929 usecs */
            0x00001388, /*       9926 usecs */
            0x00001388, /*       7782 usecs */
            0x00001388, /*       5133 usecs */
            0x00001388  /*       7853 usecs */
        };
#endregion

#region PH

        private double[] PHTableOfFreq = new double[]
            {
                918.125, // 0
                918.375, // 1
                918.625, // 2
                918.875, // 3
                919.125, // 5
                919.375, // 6
                919.625, // 7
                919.875, // 8
            };
        private uint[] phFreqTable = new uint[]
        {
            0x00301CB1, /*918.125MHz   Channel 0*/
            0x00301CBB, /*919.375MHz   Channel 5*/
            0x00301CB7, /*918.875MHz   Channel 3*/
            0x00301CBF, /*919.875MHz   Channel 7*/
            0x00301CB3, /*918.375MHz   Channel 1*/
            0x00301CBD, /*919.625MHz   Channel 6*/
            0x00301CB5, /*918.625MHz   Channel 2*/
            0x00301CB9, /*919.125MHz   Channel 4*/
        };
        /// <summary>
        /// Brazil1 Frequency Channel number
        /// </summary>
        private const uint PH_CHN_CNT = 8;
        private readonly uint[] phFreqSortedIdx = new uint[]{
            0, 5, 3, 7, 1, 6, 2, 4
        };

#region PH

        private double[] ETSIUPPERBANDTableOfFreq = new double[]
        {
            916.3,
            917.5,
            918.7,
            919.9,
        };
        private uint[] etsiupperbandFreqTable = new uint[]
        {
            0x003C23CB, /*916.3 MHz   */
            0x003C23D7, /*917.5 MHz   */
            0x003C23E3, /*918.7 MHz   */
            0x003C23EF, /*919.9 MHz   */
        };
        /// <summary>
        /// Brazil1 Frequency Channel number
        /// </summary>
        private const uint ETSIUPPERBAND_CHN_CNT = 4;
        private readonly uint[] etsiupperbandFreqSortedIdx = new uint[]{
            0, 1, 2, 3
        };

#endregion

#region NZ

        private double[] NZTableOfFreq = new double[]
        {
/*
            922.25,// 0
            922.75,// 1
            923.25,// 2
            923.75,// 3
            924.25,// 4
            924.75,// 5
            925.25,// 6
            925.75,// 7
            926.25,// 8
            926.75,// 9
            927.25,// 10
*/
            920.25,
            920.75,
            921.25,
            921.75,
            922.25,
            922.75,
            923.25,
            923.75,
            924.25,
            924.75,
            925.25,
            925.75,
            926.25,
            926.75,
            927.25,
            927.75
        };
        private uint[] nzFreqTable = new uint[]
        {
/*
            0x00180E71, //924.25 MHz   /
            0x00180E77, //925.75 MHz   /
            0x00180E69, //922.25 MHz   /
            0x00180E7B, //926.75 MHz   /
            0x00180E6D, //923.25 MHz   /
            0x00180E7D, //927.25 MHz   /
            0x00180E75, //25.25 MHz   /
            0x00180E6B, //922.75 MHz   /
            0x00180E79, //926.25 MHz   /
            0x00180E6F, //923.75 MHz   /
            0x00180E73, //924.75 MHz   /
*/

    0x00180E65, /*921.25 MHz   2*/
    0x00180E73, /*924.75MHz   9*/
    0x00180E69, /*922.25MHz   4*/
    0x00180E79, /*926.25MHz   12*/
    0x00180E6B, /*922.75MHz   5*/
    0x00180E6F, /*923.75MHz   7*/
    0x00180E67, /*921.75 MHz   3*/
    0x00180E7B, /*926.75MHz   13*/
    0x00180E77, /*925.75MHz   11*/
    0x00180E71, /*924.25MHz   8*/
    0x00180E7D, /*927.25MHz   14*/
    0x00180E61, /*920.25 MHz   0*/
    0x00180E7F, /*927.75MHz   15*/
    0x00180E75, /*925.25MHz   10*/
    0x00180E63, /*920.75 MHz   1*/
    0x00180E6D, /*923.25MHz   6*/

            };
        /// <summary>
        /// Brazil1 Frequency Channel number
        /// </summary>
        private const uint NZ_CHN_CNT = 16;
        private readonly uint[] nzFreqSortedIdx = new uint[]{
            2, 9, 4, 12, 5, 7, 3, 13, 11, 8, 14, 0, 15, 10, 1, 6
        };

#endregion

#region VE

        private readonly double[] VETableOfFreq = new double[]
        {
            922.75,// 0
            923.25,
            923.75,
            924.25,
            924.75,
            925.25,// 5
            925.75,
            926.25,
            926.75,
            927.25,// 9
        };

        private uint[] veFreqTable = new uint[]
        {
            0x00180E77, /*925.75 MHz  6 */
            0x00180E6B, /*922.75 MHz  0 */
            0x00180E7D, /*927.25 MHz  9 */
            0x00180E75, /*925.25 MHz  5 */
            0x00180E6D, /*923.25 MHz  1 */
            0x00180E7B, /*926.75 MHz  8 */
            0x00180E73, /*924.75 MHz  4 */
            0x00180E6F, /*923.75 MHz  2 */
            0x00180E79, /*926.25 MHz  7 */
            0x00180E71, /*924.25 MHz  3 */
};
        /// <summary>
        /// FCC Frequency Channel number
        /// </summary>
        private const uint VE_CHN_CNT = 10;
        private readonly uint[] veFreqSortedIdx = new uint[]{
            6, 0, 9, 5, 1,
            8, 4, 2, 7, 3
        };

        private readonly double[] SAHoppingTableOfFreq = new double[]
        {
            915.6,
            915.8,
            916,
            916.2,
            916.4,
            916.6,
            916.8,
            917,
            917.2,
            917.4,
            917.6,
            917.8,
            918,
            918.2,
            918.4,
            918.6,
            918.8
        };

        private uint[] SAHoppingFreqTable = new uint[]
        {
            0x001E11EC,/* 10    917.6 */
            0x001E11E4,/* 2     916 */
            0x001E11EB,/* 9     917.4 */
            0x001E11EE,/* 12    918 */
            0x001E11E3,/* 1     915.8 */
            0x001E11E6,/* 4     916.4 */
            0x001E11E9,/* 7     917 */
            0x001E11E5,/* 3     916.2 */
            0x001E11F2,/* 16    918.8 */
            0x001E11EF,/* 13    918.2 */
            0x001E11ED,/* 11    917.8 */
            0x001E11E7,/* 5     916.6 */
            0x001E11E8,/* 6     916.8 */
            0x001E11F1,/* 15    918.6 */
            0x001E11E2,/* 0     915.6 */
            0x001E11EA,/* 8     917.2 */
            0x001E11F0,/* 14    918.4 */
        };

        /// <summary>
        /// FCC Frequency Channel number
        /// </summary>
        private const uint SAHopping_CHN_CNT = 17;
        private readonly uint[] SAHoppingFreqSortedIdx = new uint[]{
             10, 2, 9, 12, 1, 4, 7, 3, 16, 13, 11, 5, 6, 15, 0, 8, 14
        };
        
        #endregion

#if nouse
        struct FreqItem
        {
            public double Freq;
            public uint pllvalue;
        };

        private FreqItem[] PHFreqTable = new FreqItem [8]
        { new FreqItem ( 918.125, 0x00301CB1, 0 ),
          new FreqItem ( 918.375, 0x00301CB3, 4 ),
          new FreqItem ( 918.625, 0x00301CB5, 6 ),
          new FreqItem ( 918.875, 0x00301CB7, 2 ),
          new FreqItem ( 918.125, 0x00301CB9, 7 ),
          new FreqItem ( 918.375, 0x00301CBB, 1 ),
          new FreqItem ( 918.625, 0x00301CBD, 5 ),
          new FreqItem ( 918.825, 0x00301CBF, 3 )};
#endif


        #endregion

        #endregion

        #region ====================== MAC Operation ======================

        public Result NetWakeup()
        {
            byte[] CMDBuf = new byte[10];
            byte[] RecvBuf = new byte[10];

            if (m_DeviceInterfaceType != INTERFACETYPE.IPV4)
                return Result.OK;

            try
            {
                // Send Abort Command

                if (!COMM_AdaprtCommand(READERCMD.ABORT, ref CMDBuf))
                    return Result.FAILURE;

RETRYSENDABORT:
                //if (!TCP_Send(IntelCMD, CMDBuf, 0, 8))
                if (!COMM_READER_Send(CMDBuf, 0, 8, 1000))
                    throw new ReaderException(m_Result = Result.NETWORK_LOST);

RETRYRECVABORTRESP:
                //if (!TCP_Recv(IntelCMD, RecvBuf, 0, 8, 400))
                if (!COMM_READER_Recv(RecvBuf, 0, 8, 400))
                    goto RETRYSENDABORT;

                if (RecvBuf[0] != 0x40 || RecvBuf[1] != 0x03)
                    goto RETRYRECVABORTRESP;
            }
            catch (Exception ex)
            {
                return Result.FAILURE;
            }

            return Result.OK;
        }



#if WIP
        /// <summary>
        /// Causes the MAC to perform the specified reset.  Note that any 
        /// currently-executing operations are aborted and unconsumed data 
        /// is discarded.  The MAC resets itself to a well-known initialization 
        /// state and the radio is placed in an idle state.  If the radio module 
        /// is executing a tag-protocol operation, the tag-protocol operation is 
        /// aborted and the tag-protocol operation function returns with an 
        /// error code of RFID_ERROR_OPERATION_CANCELLED. 
        /// </summary>
        public Result MacReset()
        {
            // Cancel any current operation
            this->AbortOperation();

            // Simply tell the MAC to reset
            m_pMac->Reset(type);


//            return COMM_HostCommand(HST_CMD.CLRERR);
            return MacReset();
        }
#endif

        /// <summary>
        ///  Attempts to clear the error state for the radio module's MAC 
        ///  firmware.  The MAC's error state may not be cleared while a radio 
        ///  module is executing a tag-protocol operation. 
        /// </summary>
        private Result MacClearError()
        {
            return COMM_HostCommand(HST_CMD.CLRERR);
        }

        /// <summary>
        /// Read Register
        /// </summary>
        static private readonly object _RegisterAccessLock = new object();
        private Result MacReadRegister(MacRegister add, ref UInt32 value)
        {
            //DEBUGT_WriteLine(DEBUGLEVEL.PERFORMANCE, "Enter function : " + System.Reflection.MethodBase.GetCurrentMethod().Name);
            //DEBUGT_WriteLine(DEBUGLEVEL.PERFORMANCE, "Parameter add.name add.value (hex): " + add.ToString() + " " + ((UInt16)(add)).ToString("X4"));

            try
            {
                byte[] CMDBuf = new byte[8];
                byte[] RecvBuf = new byte[8];

                DEBUGT_Write(DEBUGLEVEL.REGISTER, "Read Reg " + add);

                try
                {
                    //CMDBuf[0] = 0x70;   // Low Level API mode
                    CMDBuf[0] = 0x00;   // Intel API mode
                    CMDBuf[1] = 0x00;
                    CMDBuf[2] = (byte)add;
                    CMDBuf[3] = (byte)((uint)add >> 8);
                    CMDBuf[4] = 0x00;
                    CMDBuf[5] = 0x00;
                    CMDBuf[6] = 0x00;
                    CMDBuf[7] = 0x00;

                    lock (_RegisterAccessLock)
                    {
                        int retry = 2;

                        while (retry > 0)
                        {
                            bool found = false;

                            //COMM_READER_ClearRecvBuf();
                            COMM_READER_Send(CMDBuf, 0, 8, 5000);
                            while (COMM_READER_Recv(RecvBuf, 0, 8, 5000) == true)
                            {
                                if ((RecvBuf[0] == 0x00 || RecvBuf[0] == 0x70) && RecvBuf[1] == 0x00 && ((RecvBuf[2] == CMDBuf[2] && RecvBuf[3] == CMDBuf[3]) || (RecvBuf[2] == 0xff && RecvBuf[3] == 0xff)))
                                {
                                    found = true;
                                    break;
                                }
                            }

                            if (found)
                                break;

                            retry--;
                        }

                        
                        
                        /*                        
 *                      while (retry > 0)
                        {
                            COMM_READER_Send(CMDBuf, 0, 8, 5000);
                            if (COMM_READER_Recv(RecvBuf, 0, 8, 5000) == true)
                                break;
                            retry--;
                        }
*/

                        if (retry == 0)
                        {
                            DEBUG_WriteLine(DEBUGLEVEL.REGISTER, " Failure");
                            return Result.FAILURE;
                        }
                    }
                }
                catch (Exception ex)
                {
                    //FireStateChangedEvent(RFState.RESET);
                    DEBUG_WriteLine(DEBUGLEVEL.REGISTER, " Failure");
                    return Result.FAILURE;
                }

                value = (UInt32)(RecvBuf[7] << 24 | RecvBuf[6] << 16 | RecvBuf[5] << 8 | RecvBuf[4]);

                DEBUG_WriteLine(DEBUGLEVEL.REGISTER, " OK value 0x" + value.ToString("X4"));

                return Result.OK;
            }
            finally
            {
                //DEBUGT_WriteLine(DEBUGLEVEL.PERFORMANCE, "Exit function : " + System.Reflection.MethodBase.GetCurrentMethod().Name);
                //DEBUGT_WriteLine(DEBUGLEVEL.PERFORMANCE, "Parameter value (hex)" + value.ToString("X8"));
            }
        }

        private Result MacWriteRegister(MacRegister add, UInt32 value)
        {
            //DEBUGT_WriteLine(DEBUGLEVEL.PERFORMANCE, "Enter function : " + System.Reflection.MethodBase.GetCurrentMethod().Name);
            //DEBUGT_WriteLine(DEBUGLEVEL.PERFORMANCE, "Parameter add.name add.value (hex): " +add.ToString () + " " + ((UInt16)(add)).ToString ("X4"));

            try
            {
                byte[] CMDBuf = new byte[8];

                DEBUGT_Write(DEBUGLEVEL.REGISTER, "Write Reg " + add + " value 0x" + value.ToString("X4"));

                try
                {
                    //CMDBuf[0] = 0x70;   // Low Level API mode
                    //CMDBuf[1] = 0x01;
                    CMDBuf[0] = 0x01;   // Intel API mode
                    CMDBuf[1] = 0x00;
                    CMDBuf[2] = (byte)add;
                    CMDBuf[3] = (byte)((uint)add >> 8);
                    CMDBuf[4] = (byte)value;
                    CMDBuf[5] = (byte)(value >> 8);
                    CMDBuf[6] = (byte)(value >> 16);
                    CMDBuf[7] = (byte)(value >> 24);

                    COMM_READER_Send(CMDBuf, 0, 8, 5000);
                }
                catch (Exception ex)
                {
                    //FireStateChangedEvent(RFState.RESET);
                    DEBUG_WriteLine(DEBUGLEVEL.REGISTER, " Failure");
                    return Result.FAILURE;
                }

                DEBUG_WriteLine(DEBUGLEVEL.REGISTER, " OK");

                return Result.OK;
            }
            finally
            {
                //DEBUGT_WriteLine(DEBUGLEVEL.PERFORMANCE, "Exit function : " + System.Reflection.MethodBase.GetCurrentMethod().Name);
            }
        }

        /// <summary>
        /// Reads one or more 32-bit words from the MAC's OEM configuration data
        /// area.
        /// </summary>
        /// <param name="address">The address of the first 32-bit value to read from the MAC's OEM configuration data area.
        /// Note that the address is a 32-bit word address, and not a byte address</param>
        /// <param name="data">An array of count 32-bit unsigned integers that will receive the data from MAC's OEM 
        /// configuration data area. The 32-bit values returned are in the MAC's native format (i.e., little endian). 
        /// This parameter must not be NULL.</param>
        /// <returns></returns>
        private Result MacReadOemData(UInt32 address, UInt32[] data)
        {
            return MacReadOemData(address, (uint)data.Length, data);
        }

        /// <summary>
        /// Writes one or more 32-bit words to the MAC's OEM configuration data
        /// area.
        /// </summary>
        /// <param name="address">The 32-bit address into the MAC’s OEM configuration data 
        /// area where the first 32-bit data word is to be written. Note that the address is
        /// a 32-bit address, and not a byte address</param>
        /// <param name="data">An array of count 32-bit unsigned integers that 
        /// contains the data to be written into the MAC’s OEM configuration data area.
        /// The 32-bit values provided must be in the MAC’s native format (i.e., little endian). 
        /// This parameter must not be NULL.</param>
        /// <returns></returns>
        private Result MacWriteOemData(UInt32 address, UInt32[] data)
        {
            return MacWriteOemData(address, (UInt32)data.Length, data);
        }

        private Result MacReadOemData(UInt32 address, UInt32 count, UInt32[] pData)
        {
            for (int cnt = 0; cnt < count; cnt++)
            {
                if (MacReadOemData(address++, ref pData[cnt]) != Result.OK)
                    return Result.FAILURE;
            }

            return Result.OK;
        }

        private Result MacWriteOemData(UInt32 address, UInt32 count, UInt32[] pData)
        {
            for (int cnt = 0; cnt < count; cnt++)
                if (MacWriteOemData(address++, pData[cnt]) != Result.OK)
                    return Result.FAILURE;

            return Result.OK;
        }

        private Result MacReadOemData(UInt32 address, ref UInt32 value)
        {
            if ((m_Result = MacWriteRegister(MacRegister.HST_OEM_ADDR, address)) != Result.OK)
                return m_Result;

            if ((m_Result = COMM_HostCommand(HST_CMD.RDOEM)) != Result.OK)
                return m_Result;

            value = m_OEMReadData;

            return Result.OK;
        }

        private Result MacWriteOemData(uint address, uint value)
        {
            UInt32 readvalue = 0;
            int retry = 3;


            if (NetWakeup() != Result.OK)
                return Result.FAILURE;

            for (; retry > 0; retry--)
            {
                if (MacWriteRegister(MacRegister.HST_OEM_ADDR, address) != Result.OK)
                    return Result.FAILURE;

                if (MacWriteRegister(MacRegister.HST_OEM_DATA, value) != Result.OK)
                    return Result.FAILURE;

                Thread.Sleep(100);

                if (COMM_HostCommand(HST_CMD.WROEM) != Result.OK)
                    return Result.FAILURE;

                Thread.Sleep(100);

                if (MacReadOemData(address, ref readvalue) != Result.OK)
                    return Result.FAILURE;

                if (readvalue == value)
                    return Result.OK;
            }
            
            return Result.FAILURE;
        }

        private void EngTest_RSSIStreamingTestProc()
        {
        AGAIN:
            try
            {

                MacWriteRegister(MacRegister.HST_ENGTST_ARG0, 0x11a);
                MacWriteRegister(MacRegister.HST_ENGTST_ARG1, 0x0);
                COMM_HostCommand(HST_CMD.ENGTST1);

                MacWriteRegister(MacRegister.HST_ENGTST_ARG0, 0x05);
                //MacWriteRegister(MacRegister.HST_ENGTST_ARG1, 0x2ffff);
                MacWriteRegister(MacRegister.HST_ENGTST_ARG1, 0x200ff);
                COMM_HostCommand(HST_CMD.ENGTST1);

                m_Result = Result.OK;
            }
            catch (ReaderException ex)
            {
                if (FireIfReset(ex.ErrorCode) == Result.OK)
                {
                    goto AGAIN;
                }
            }
            catch
            {
                m_Result = Result.SYSTEM_CATCH_EXCEPTION;
            }
        }

        private int _randomPattern = 0;
        private void EngTest_TransmitRandomDataProc()
        {
        AGAIN:
            try
            {
                switch (_randomPattern)
                {
                    case 1:
                        MacWriteRegister(MacRegister.HST_TX_RANDOM_DATA_CONTROL, 0x02);
                        break;

                    default:
                        MacWriteRegister(MacRegister.HST_TX_RANDOM_DATA_CONTROL, 0x00);
                    break;
                }
                COMM_HostCommand(HST_CMD.CMD_TX_RANDOM_DATA);

                m_Result = Result.OK;
            }
            catch (ReaderException ex)
            {
                if (FireIfReset(ex.ErrorCode) == Result.OK)
                {
                    goto AGAIN;
                }
            }
            catch
            {
                m_Result = Result.SYSTEM_CATCH_EXCEPTION;
            }
        }

        public Result EngTest_RSSIStreamingTest()
        {
            g_hWndThread = new Thread(new ThreadStart(EngTest_RSSIStreamingTestProc));
            g_hWndThread.Priority = ThreadPriority.Normal;
            g_hWndThread.IsBackground = true;
            g_hWndThread.Name = "EngTest_RSSIStreamingTestProc";
            g_hWndThread.Start();
            WaitToBusy();
            return Result.OK;
        }

        /// <summary>
        /// randomPattern = 0 : 4096 bit
        ///                 1 : 511 bit
        /// </summary>
        /// <param name="enable"></param>
        /// <param name="randomPattern"></param>
        /// <returns></returns>
        public Result EngTest_TransmitRandomData(bool enable, int randomPattern)
        {
            _EngineeringTest_Operation = 1;

            if (enable)
            {
                _randomPattern = randomPattern;

                g_hWndThread = new Thread(new ThreadStart(EngTest_TransmitRandomDataProc));
                g_hWndThread.Priority = ThreadPriority.Normal;
                g_hWndThread.IsBackground = true;
                g_hWndThread.Name = "EngTest_TransmitRandomDataProc";
                g_hWndThread.Start();
                WaitToBusy();
            }
            else
            {
                RadioAbortOperation();
                _EngineeringTest_Operation = 0;
            }
            return Result.OK;
        }

        public Result EngTest_TransmitRandomData(bool enable)
        {
            return EngTest_TransmitRandomData (enable, 0);
        }

        /// <summary>
        /// Reads directly from a radio-module hardware register.  The radio 
        /// module's hardware registers may not be read while a radio module 
        /// is executing a tag-protocol operation. 
        /// </summary>
        /// <param name="address">The 16-bit address of the radio-module hardware 
        /// register to be read.  An address that is beyond the end 
        /// of the radio module's register set Results in an invalid-
        /// parameter return status.</param>
        /// <param name="value">A 16-bit value that will receive the value 
        /// in the radio-module hardware register specified by 
        /// address. </param>
        /// <returns></returns>
        public Result MacBypassReadRegister(ushort address, ref ushort value)
        {
        AGAIN:
            try
            {
                //ThrowException(MacBypassWriteRegister(address, value));
                MacWriteRegister(MacRegister.HST_MBP_ADDR, address);
                COMM_HostCommand(HST_CMD.MBPRDREG);

                value = m_TildenReadValue;

                m_Result = Result.OK;
            }
            catch (ReaderException ex)
            {
                if (FireIfReset(ex.ErrorCode) == Result.OK)
                {
                    goto AGAIN;
                }
            }
            catch
            {
                m_Result = Result.SYSTEM_CATCH_EXCEPTION;
            }

            return m_Result;
        }

        /// <summary>
        /// Writes directly to a radio-module hardware register.  The radio 
        /// module's hardware registers may not be written while a radio 
        /// module is executing a tag-protocol operation. 
        /// </summary>
        /// <param name="address">The 16-bit address of the radio-module hardware 
        /// register to be written.  An address that is beyond the 
        /// end of the radio module's register set Results in an 
        /// invalid-parameter return status. </param>
        /// <param name="value">The 16-bit value to write to the radio-module 
        /// hardware register specified by address. </param>
        /// <returns></returns>
        public Result MacBypassWriteRegister(ushort address, ushort value)
        {
        AGAIN:
            try
            {
                //ThrowException(MacBypassWriteRegister(address, value));
                if (MacWriteRegister(MacRegister.HST_MBP_ADDR, address) != Result.OK)
                    return Result.FAILURE;

                if (MacWriteRegister(MacRegister.HST_MBP_DATA, value) != Result.OK)
                    return Result.FAILURE;
                    
                COMM_HostCommand(HST_CMD.MBPWRREG);
            }
            catch (ReaderException ex)
            {
                if (FireIfReset(ex.ErrorCode) == Result.OK)
                {
                    goto AGAIN;
                }
            }
            catch
            {
                m_Result = Result.SYSTEM_CATCH_EXCEPTION;
            }
            return m_Result;
        }

#endregion

#region ====================== Thread Process function ======================

        Int32 EmptyCallback
        (
            [In]      UInt32 bufferLength,
            [In]      Byte[] pBuffer
        )
        {
            return 0;
        }

        private void CustTagReadProtectThreadProc()
        {
            try
            {
                FireStateChangedEvent(RFState.BUSY);

                InternalCustCmdTagReadProtectParms parms = new InternalCustCmdTagReadProtectParms();
                parms.accessPassword = m_rdr_opt_parms.TagReadProtect.accessPassword;
                parms.retry = m_rdr_opt_parms.TagReadProtect.retryCount;

            CONTINUOUS:
                //m_Result = CustTagReadProtect (parms, m_rdr_opt_parms.TagReadProtect.flags);

                switch (m_Result)
                {
                    case Result.OK:
                        {
                            LastMacErrorCode = 0;
                            ThrowException(GetMacErrorCode(ref LastMacErrorCode));
                            if (LastMacErrorCode > 0)
                            {
                                throw new ReaderException(Result.MAC_ERROR);
                            }
                        }

                        
#if nouse
                        
                        {
                            uint macErr = 0;
                            if ((m_Result = GetMacErrorCode(ref macErr)) != Result.OK)
                            {
                                throw new ReaderException(m_Result, "GetMacErrorCode failed");
                            }
                            if (macErr > 0)
                            {
#if DEBUG
                                CSLibrary.Diagnostics.CoreDebug.Logger.Warn(string.Format("HighLevelInterface.CustTagReadProtectThreadProc() MAC ERROR CODE {0}", macErr));
#endif

#if HIDDEN_MACERROR
                                if (MacErrorIsOverheat(macErr) || MacErrorIsNegligible(macErr))
                                {
                                    ThrowException(MacClearError());
                                    goto CONTINUOUS;
                                }
#else

#endif

                            }
                        }
#endif
                        break;
                    case Result.OPERATION_CANCELLED:
                        ThrowException(MacClearError());
                        break;
                    default:
                        FireStateChangedEvent(RFState.RESET);
                        return;
                }
            }
            catch (ReaderException e)
            {
#if DEBUG
                CSLibrary.Diagnostics.CoreDebug.Logger.ErrorException("HighLevelInterface.CustTagReadProtectThreadProc()", e);
#endif
            }
            catch (System.Exception ex)
            {
#if DEBUG
                CSLibrary.Diagnostics.CoreDebug.Logger.ErrorException("HighLevelInterface.CustTagReadProtectThreadProc()", ex);
#endif
            }
            FireStateChangedEvent(RFState.IDLE);
        }

        private void CustTagResetReadProtectThreadProc()
        {
            try
            {
                FireStateChangedEvent(RFState.BUSY);

                InternalCustCmdTagReadProtectParms parms = new InternalCustCmdTagReadProtectParms();
                parms.accessPassword = m_rdr_opt_parms.TagResetReadProtect.accessPassword;
                parms.retry = m_rdr_opt_parms.TagResetReadProtect.retryCount;
            CONTINUOUS:
                //m_Result = CustTagResetReadProtect(parms, m_rdr_opt_parms.TagResetReadProtect.flags);

                switch (m_Result)
                {
                    case Result.OK:
                        {
                            uint macErr = 0;
                            if ((m_Result = GetMacErrorCode(ref macErr)) != Result.OK)
                            {
                                throw new ReaderException(m_Result, "GetMacErrorCode failed");
                            }
                            if (macErr > 0)
                            {
#if DEBUG
                                CSLibrary.Diagnostics.CoreDebug.Logger.Warn(string.Format("HighLevelInterface.CustTagResetReadProtectThreadProc() MAC ERROR CODE {0}", macErr));
#endif

                                if (MacErrorIsOverheat(macErr) || MacErrorIsNegligible(macErr))
                                {
                                    ThrowException(MacClearError());
                                    goto CONTINUOUS;
                                }
                           }
                        }
                        break;
                    case Result.OPERATION_CANCELLED:
                        ThrowException(MacClearError());
                        break;
                    default:
                        FireStateChangedEvent(RFState.RESET);
                        return;
                }
            }
            catch (ReaderException e)
            {
#if DEBUG
                CSLibrary.Diagnostics.CoreDebug.Logger.ErrorException("HighLevelInterface.CustTagResetReadProtectThreadProc()", e);
#endif
            }
            catch (System.Exception ex)
            {
#if DEBUG
                CSLibrary.Diagnostics.CoreDebug.Logger.ErrorException("HighLevelInterface.CustTagResetReadProtectThreadProc()", ex);
#endif
            }

            FireStateChangedEvent(RFState.IDLE);
        }

        private  void TagResetReadProtectThreadProc()
        {
            try
            {
                FireStateChangedEvent(RFState.BUSY);

                ReadProtectParms parms = new ReadProtectParms();
                parms.accessPassword = m_rdr_opt_parms.TagResetReadProtect.accessPassword;
                //parms.common.callback = new Native.CallbackDelegate(EmptyCallback);
                parms.common.tagStopCount = 0;
                parms.common.callbackCode = IntPtr.Zero;
                //parms.common.context = IntPtr.Zero;
            
                CONTINUOUS:
//                m_Result = TagResetReadProtect(parms, m_rdr_opt_parms.TagResetReadProtect.flags);

                // Perform the common 18K6C tag operation setup
                Start18K6CRequest(parms.common.tagStopCount, m_rdr_opt_parms.TagResetReadProtect.flags);

		        // Set the tag access descriptor to the first one just to be safe
                MacWriteRegister(MacRegister.HST_TAGACC_DESC_SEL, 0);

		        // Set up the access password register
                MacWriteRegister(MacRegister.HST_TAGACC_ACCPWD, parms.accessPassword);

		        // Set up the HST_TAGACC_DESC_CFG register (controls the verify and retry
		        // count) and write it to the MAC
		        /*UInt32  registerValue = HST_TAGACC_DESC_CFG_RETRY(7);

		        m_pMac->WriteRegister(HST_TAGACC_DESC_CFG, registerValue);*/

		        // Issue the lock command
                m_Result = COMM_HostCommand(HST_CMD.CUSTOMG2XRESETREADPROTECT);

                switch (m_Result)
                {
                    case Result.OK:
                        {
                            uint macErr = 0;
                            if ((m_Result = GetMacErrorCode(ref macErr)) != Result.OK)
                            {
                                throw new ReaderException(m_Result, "GetMacErrorCode failed");
                            }
                            if (macErr > 0)
                            {
#if DEBUG
                                CSLibrary.Diagnostics.CoreDebug.Logger.Warn(string.Format("HighLevelInterface.TagResetReadProtectThreadProc() MAC ERROR CODE {0}", macErr));
#endif

                                if (MacErrorIsOverheat(macErr) || MacErrorIsNegligible(macErr))
                                {
                                    ThrowException(MacClearError());
                                    goto CONTINUOUS;
                                }
                            }
                        }
                        break;
                    case Result.OPERATION_CANCELLED:
                        MacClearError();
                        break;
                    default:
                        FireStateChangedEvent(RFState.RESET);
                        return;
                }
            }
            catch (ReaderException e)
            {
#if DEBUG
                CSLibrary.Diagnostics.CoreDebug.Logger.ErrorException("HighLevelInterface.TagResetReadProtectThreadProc()", e);
#endif
            }
            catch (System.Exception ex)
            {
#if DEBUG
                CSLibrary.Diagnostics.CoreDebug.Logger.ErrorException("HighLevelInterface.TagResetReadProtectThreadProc()", ex);
#endif
            }

            FireStateChangedEvent(RFState.IDLE);
        }

        private void CustTagEASConfigThreadProc()
        {
            try
            {
                /*FireStateChangedEvent(RFState.BUSY);

                InternalCustCmdEASParms parms = new InternalCustCmdEASParms();
                parms.accessPassword = m_rdr_opt_parms.EASConfig.accessPassword;
                parms.retry = m_rdr_opt_parms.EASConfig.retryCount;
                parms.enable = m_rdr_opt_parms.EASConfig.enable;
            CONTINUOUS:
                m_Result = CustTagEASConfig(parms, m_rdr_opt_parms.EASConfig.flags);

                switch (m_Result)
                {
                    case Result.OK:
                        {
                            uint macErr = 0;
                            if ((m_Result = GetMacErrorCode(ref macErr)) != Result.OK)
                            {
                                throw new ReaderException(m_Result, "GetMacErrorCode failed");
                            }
                            if (macErr > 0)
                            {
                                CSLibrary.Diagnostics.CoreDebug.Logger.Warn(string.Format("HighLevelInterface.CustTagEASConfigThreadProc() MAC ERROR CODE {0}", macErr));

                                if (MacErrorIsOverheat(macErr))
                                {
                                    ThrowException(MacClearError());
                                    goto CONTINUOUS;
                                }
                            }
                        }
                        break;
                    case Result.NETWORK_LOST:
                    case Result.NETWORK_RESET:
                        FireStateChangedEvent(RFState.RESET);
                        return;
                }*/
            }
            catch (ReaderException e)
            {
#if DEBUG
                CSLibrary.Diagnostics.CoreDebug.Logger.ErrorException("HighLevelInterface.CustTagEASConfigThreadProc()", e);
#endif
            }
            catch (System.Exception ex)
            {
#if DEBUG
                CSLibrary.Diagnostics.CoreDebug.Logger.ErrorException("HighLevelInterface.CustTagEASConfigThreadProc()", ex);
#endif
            }

            FireStateChangedEvent(RFState.IDLE);
        }

/*
        private void CustTagEASAlarmThreadProc()
        {
            try
            {
                FireStateChangedEvent(RFState.BUSY);

                InternalCustCmdEASParms parms = new InternalCustCmdEASParms();
            CONTINUOUS:
                m_Result = CustTagEASAlarm(parms, m_rdr_opt_parms.EASConfig.flags);

                switch (m_Result)
                {
                    case Result.OK:
                        {
                            uint macErr = 0;
                            if ((m_Result = GetMacErrorCode(ref macErr)) != Result.OK)
                            {
                                throw new ReaderException(m_Result, "GetMacErrorCode failed");
                            }
                            if (macErr > 0)
                            {
                                CSLibrary.Diagnostics.CoreDebug.Logger.Warn(string.Format("HighLevelInterface.CustTagEASAlarmThreadProc() MAC ERROR CODE {0}", macErr));

                                if (MacErrorIsOverheat(macErr))
                                {
                                    MacClearError();
                                    goto CONTINUOUS;
                                }
                            }
                        }
                        break;
                    case Result.NETWORK_LOST:
                    case Result.NETWORK_RESET:
                        FireStateChangedEvent(RFState.RESET);
                        return;
                }
            }
            catch (ReaderException e)
            {
#if DEBUG
                CSLibrary.Diagnostics.CoreDebug.Logger.ErrorException("HighLevelInterface.CustTagEASAlarmThreadProc()", e);
#endif
            }
            catch (System.Exception ex)
            {
#if DEBUG
                CSLibrary.Diagnostics.CoreDebug.Logger.ErrorException("HighLevelInterface.CustTagEASAlarmThreadProc()", ex);
#endif
            }

            FireStateChangedEvent(RFState.IDLE);
        }
*/

#if false
        private  void TagEASConfigThreadProc()
        {
            try
            {
                FireStateChangedEvent(RFState.BUSY);

                EASParms parms = new EASParms();
                parms.accessPassword = m_rdr_opt_parms.EASConfig.accessPassword;
                parms.enable = m_rdr_opt_parms.EASConfig.enable;
                //parms.common.callback = new Native.CallbackDelegate(EmptyCallback);
                parms.common.tagStopCount = 0;
                parms.common.callbackCode = IntPtr.Zero;
                //parms.common.context = IntPtr.Zero;
            CONTINUOUS:
                m_Result = TagEASConfig(parms, m_rdr_opt_parms.EASConfig.flags);

                switch (m_Result)
                {
                    case Result.OK:
                        {
                            uint macErr = 0;
                            if ((m_Result = GetMacErrorCode(ref macErr)) != Result.OK)
                            {
                                throw new ReaderException(m_Result, "GetMacErrorCode failed");
                            }
                            if (macErr > 0)
                            {
                                CSLibrary.Diagnostics.CoreDebug.Logger.Warn(string.Format("HighLevelInterface.TagEASConfigThreadProc() MAC ERROR CODE {0}", macErr));

                                if (MacErrorIsOverheat(macErr))
                                {
                                    MacClearError();
                                    goto CONTINUOUS;
                                }
                            }
                        }
                        break;
                    case Result.NETWORK_LOST:
                    case Result.NETWORK_RESET:
                        FireStateChangedEvent(RFState.RESET);
                        return;
                }
            }
            catch (ReaderException e)
            {
                CSLibrary.Diagnostics.CoreDebug.Logger.ErrorException("HighLevelInterface.TagEASConfigThreadProc()", e);
            }
            catch (System.Exception ex)
            {
                CSLibrary.Diagnostics.CoreDebug.Logger.ErrorException("HighLevelInterface.TagEASConfigThreadProc()", ex);
            }

            FireStateChangedEvent(RFState.IDLE);
        }

        private  void TagEASAlarmThreadProc()
        {
            try
            {
                /*FireStateChangedEvent(RFState.BUSY);

                EASParms parms = new EASParms();
                //parms.common.callback = new Native.CallbackDelegate(EmptyCallback);
                parms.common.tagStopCount = 0;
                parms.common.callbackCode = IntPtr.Zero;
                //parms.common.context = IntPtr.Zero;
            CONTINUOUS:
                m_Result = TagEASAlarm(parms, m_rdr_opt_parms.EASConfig.flags);

                switch (m_Result)
                {
                    case Result.OK:
                        {
                            uint macErr = 0;
                            if ((m_Result = GetMacErrorCode(ref macErr)) != Result.OK)
                            {
                                throw new ReaderException(m_Result, "GetMacErrorCode failed");
                            }
                            if (macErr > 0)
                            {
                                CSLibrary.Diagnostics.CoreDebug.Logger.Warn(string.Format("HighLevelInterface.TagEASAlarmThreadProc() MAC ERROR CODE {0}", macErr));

                                if (MacErrorIsOverheat(macErr))
                                {
                                    MacClearError();
                                    goto CONTINUOUS;
                                }
                            }
                        }
                        break;
                    case Result.NETWORK_LOST:
                    case Result.NETWORK_RESET:
                        FireStateChangedEvent(RFState.RESET);
                        return;
                }*/
            }
            catch (ReaderException e)
            {
                CSLibrary.Diagnostics.CoreDebug.Logger.ErrorException("HighLevelInterface.TagEASAlarmThreadProc()", e);
            }
            catch (System.Exception ex)
            {
                CSLibrary.Diagnostics.CoreDebug.Logger.ErrorException("HighLevelInterface.TagEASAlarmThreadProc()", ex);
            }

            FireStateChangedEvent(RFState.IDLE);
        }
#endif

        private void TagRangingThreadProc()
        {
            uint Value = 0;

            try
            {
                FireStateChangedEvent(RFState.BUSY);
#if DEBUG
                CSLibrary.Diagnostics.CoreDebug.Logger.Warn("HighLevelInterface.TagRangingThreadProc() START");
#endif

                InternalTagRangingParms parms = new InternalTagRangingParms();
                parms.flags = m_rdr_opt_parms.TagRanging.flags;
                parms.tagStopCount = m_rdr_opt_parms.TagRanging.tagStopCount;

                // Set MultiBanks Info
                MacReadRegister(MacRegister.HST_INV_CFG, ref Value);
                
                Value &= 0xfff4fcff;
                if (m_rdr_opt_parms.TagRanging.multibanks != 0)
                    Value |= (m_rdr_opt_parms.TagRanging.multibanks & (uint)0x03) << 16;

                if (m_rdr_opt_parms.TagRanging.QTMode == true)
                    Value |= 0x00080000; // bit 19
                
                MacWriteRegister(MacRegister.HST_INV_CFG, Value);

                Value = 0;
                if (m_rdr_opt_parms.TagRanging.focus)
                    Value |= 0x10;
                if (m_rdr_opt_parms.TagRanging.fastid)
                    Value |= 0x20;
                MacWriteRegister(MacRegister.HST_IMPINJ_EXTENSIONS, Value);

                // Set up the access bank register
                Value = (UInt32)(m_rdr_opt_parms.TagRanging.bank1) | (UInt32)(((int)m_rdr_opt_parms.TagRanging.bank2) << 2);
                MacWriteRegister(MacRegister.HST_TAGACC_BANK, Value);

                // Set up the access pointer register (tells the offset)
                Value = (UInt32)((m_rdr_opt_parms.TagRanging.offset1 & 0xffff) | ((m_rdr_opt_parms.TagRanging.offset2 & 0xffff) << 16));
                MacWriteRegister(MacRegister.HST_TAGACC_PTR, Value);

                // Set up the access count register (i.e., number values to read)
                Value = (UInt32)((0xFF & m_rdr_opt_parms.TagRanging.count1) | ((0xFF & m_rdr_opt_parms.TagRanging.count2) << 8));
                MacWriteRegister(MacRegister.HST_TAGACC_CNT, Value);

                // Set up the access password
                Value = (UInt32)(m_rdr_opt_parms.TagRanging.accessPassword);
                MacWriteRegister(MacRegister.HST_TAGACC_ACCPWD, Value);

                // Set Toggle off, if QT Mode. 
                if (m_rdr_opt_parms.TagRanging.QTMode == true)
                {
                    uint RegValue = 0;

                    for (uint cnt = 0; cnt < 4; cnt++)
                    {
                        MacWriteRegister(MacRegister.HST_INV_SEL, cnt);
                        MacReadRegister(MacRegister.HST_INV_ALG_PARM_2, ref RegValue);
                        Value &= 0xfffffffe;
                        MacWriteRegister(MacRegister.HST_INV_ALG_PARM_2, Value);
                    }
                }




                Start18K6CRequest(m_rdr_opt_parms.TagRanging.tagStopCount, parms.flags);

                UInt32 v = 0;
                MacReadRegister(MacRegister.HST_TAGACC_DESC_CFG, ref v);
                v &= ~0x3eU;
                v |= 0x01;
                if (m_rdr_opt_parms.TagRanging.retry > 0x1f)
                    v |= (0x1fU << 1);
                else
                    v |= (uint)(m_rdr_opt_parms.TagRanging.retry << 1);
                MacWriteRegister(MacRegister.HST_TAGACC_DESC_CFG, v);

            CONTINUOUS:
                m_Result = COMM_HostCommand(HST_CMD.INV);

                switch (m_Result)
                {
                    case Result.OK:
                        {
                            if ((m_Result = GetMacErrorCode(ref LastMacErrorCode)) != Result.OK)
                            {
                                throw new ReaderException(m_Result, "GetMacErrorCode failed");
                            }

                            if (LastMacErrorCode > 0)
                            {
                                throw new ReaderException(Result.MAC_ERROR, LastMacErrorCode.ToString("D"));
                            }
                        }
                        break;

                    case Result.OPERATION_CANCELLED:
                        ThrowException(MacClearError());
                        break;

                    default:
                        FireStateChangedEvent(RFState.RESET);
                        return;
                }
            }
            catch (ReaderException e)
            {
//                FireStateChangedEvent(RFState.RESET);
//                return;

#if DEBUG
                CSLibrary.Diagnostics.CoreDebug.Logger.ErrorException("HighLevelInterface.TagRangingThreadProc()", e);
#endif
            }
            catch (System.Exception ex)
            {
#if DEBUG
                CSLibrary.Diagnostics.CoreDebug.Logger.ErrorException("HighLevelInterface.TagRangingThreadProc()", ex);
#endif
            }
#if DEBUG
            CSLibrary.Diagnostics.CoreDebug.Logger.Warn("HighLevelInterface.TagRangingThreadProc() END");
#endif
            FireStateChangedEvent(RFState.IDLE);
        }

        private void CLSetPasswordThreadProc()
        {
            try
            {
                FireStateChangedEvent(RFState.BUSY);

//                m_Result = RadioCLSetPassword(m_rdr_opt_parms.CLSetPassword.InternalRegister1, m_rdr_opt_parms.CLSetPassword.InternalRegister2);
            }
            catch (System.Exception ex)
            {
#if DEBUG
                CSLibrary.Diagnostics.CoreDebug.Logger.ErrorException("HighLevelInterface.ClSetPasswordThreadProc()", ex);
#endif
            }

            FireStateChangedEvent(RFState.IDLE);
        }

        private void CLSetLogModeThreadProc()
        {
            try
            {
                FireStateChangedEvent(RFState.BUSY);

//                m_Result = RadioCLSetLogMode (m_rdr_opt_parms.CLSetLogMode.InternalRegister1);

            }
            catch (ReaderException e)
            {
#if DEBUG
                CSLibrary.Diagnostics.CoreDebug.Logger.ErrorException("HighLevelInterface.ClSetLogModeThreadProc()", e);
#endif
            }
            catch (System.Exception ex)
            {
#if DEBUG
                CSLibrary.Diagnostics.CoreDebug.Logger.ErrorException("HighLevelInterface.ClSetLogModeThreadProc()", ex);
#endif
            }


            FireStateChangedEvent(RFState.IDLE);
        }

        private void CLSetLogLimitsThreadProc()
        {
            try
            {
                FireStateChangedEvent(RFState.BUSY);

//                m_Result = RadioCLSetLogLimits(m_rdr_opt_parms.CLSetLogLimits.InternalRegister1, m_rdr_opt_parms.CLSetLogLimits.InternalRegister2);
            }
            catch (ReaderException e)
            {
#if DEBUG
                CSLibrary.Diagnostics.CoreDebug.Logger.ErrorException("HighLevelInterface.ClSetLogLimitsThreadProc()", e);
#endif
            }
            catch (System.Exception ex)
            {
#if DEBUG
                CSLibrary.Diagnostics.CoreDebug.Logger.ErrorException("HighLevelInterface.ClSetLogLimitsThreadProc()", ex);
#endif
            }


            FireStateChangedEvent(RFState.IDLE);
        }

        private void CLGetMeasurementSetupThreadProc()
        {
            try
            {
                FireStateChangedEvent(RFState.BUSY);

//                m_Result = RadioCLGetMeasurementSetup(m_rdr_opt_parms.CLGetMesurementSetup.MesurementSetupData);
            }
            catch (ReaderException e)
            {
#if DEBUG
                CSLibrary.Diagnostics.CoreDebug.Logger.ErrorException("HighLevelInterface.ClGetMeasurementSetupThreadProc()", e);
#endif
            }
            catch (System.Exception ex)
            {
#if DEBUG
                CSLibrary.Diagnostics.CoreDebug.Logger.ErrorException("HighLevelInterface.ClGetMeasurementSetupThreadProc()", ex);
#endif
            }

            FireStateChangedEvent(RFState.IDLE);
        }

        private void CLSetSfeParaThreadProc()
        {
            try
            {
                FireStateChangedEvent(RFState.BUSY);

//                m_Result = RadioCLSetSFEParameters(m_rdr_opt_parms.CLSetSFEPara.InternalRegister1);
            }
            catch (ReaderException e)
            {
#if DEBUG
                CSLibrary.Diagnostics.CoreDebug.Logger.ErrorException("HighLevelInterface.ClSetSfeParaThreadProc()", e);
#endif
            }
            catch (System.Exception ex)
            {
#if DEBUG
                CSLibrary.Diagnostics.CoreDebug.Logger.ErrorException("HighLevelInterface.ClSetSfeParaThreadProc()", ex);
#endif
            }

            FireStateChangedEvent(RFState.IDLE);
        }

        private void CLSetCalDataThreadProc()
        {
            try
            {
                FireStateChangedEvent(RFState.BUSY);

//                m_Result = RadioCLSetCalibrationData(m_rdr_opt_parms.CLSetCalData.InternalRegister1, m_rdr_opt_parms.CLSetCalData.InternalRegister2);
            }
            catch (ReaderException e)
            {
#if DEBUG
                CSLibrary.Diagnostics.CoreDebug.Logger.ErrorException("HighLevelInterface.ClSetCalDataThreadProc()", e);
#endif
            }
            catch (System.Exception ex)
            {
#if DEBUG
                CSLibrary.Diagnostics.CoreDebug.Logger.ErrorException("HighLevelInterface.ClSetCalDataThreadProc()", ex);
#endif
            }


            FireStateChangedEvent(RFState.IDLE);
        }

        private void CLEndLogThreadProc()
        {
            try
            {
                FireStateChangedEvent(RFState.BUSY);

//                m_Result = RadioCLEndLog();
            }
            catch (ReaderException e)
            {
#if DEBUG
                CSLibrary.Diagnostics.CoreDebug.Logger.ErrorException("HighLevelInterface.ClEndLogThreadProc()", e);
#endif
            }
            catch (System.Exception ex)
            {
#if DEBUG
                CSLibrary.Diagnostics.CoreDebug.Logger.ErrorException("HighLevelInterface.ClEndLogThreadProc()", ex);
#endif
            }


            FireStateChangedEvent(RFState.IDLE);
        }

        private void CLStartLogThreadProc()
        {
            try
            {
                FireStateChangedEvent(RFState.BUSY);

//                m_Result = RadioCLStartLog(m_rdr_opt_parms.CLStartLog.InternalRegister1);
            }
            catch (ReaderException e)
            {
#if DEBUG
                CSLibrary.Diagnostics.CoreDebug.Logger.ErrorException("HighLevelInterface.ClStartLogThreadProc()", e);
#endif
            }
            catch (System.Exception ex)
            {
#if DEBUG
                CSLibrary.Diagnostics.CoreDebug.Logger.ErrorException("HighLevelInterface.ClStartLogThreadProc()", ex);
#endif
            }


            FireStateChangedEvent(RFState.IDLE);
        }

        private void CLGetLogStateThreadProc()
        {
            try
            {
                FireStateChangedEvent(RFState.BUSY);

//                m_Result = RadioCLGetLogState(m_rdr_opt_parms.CLGetLogState.InternalRegister1, m_rdr_opt_parms.CLGetLogState.LogStateData);
            }
            catch (ReaderException e)
            {
#if DEBUG
                CSLibrary.Diagnostics.CoreDebug.Logger.ErrorException("HighLevelInterface.ClGetLogStateThreadProc()", e);
#endif
            }
            catch (System.Exception ex)
            {
#if DEBUG
                CSLibrary.Diagnostics.CoreDebug.Logger.ErrorException("HighLevelInterface.ClGetLogStateThreadProc()", ex);
#endif
            }


            FireStateChangedEvent(RFState.IDLE);
        }

        private void CLGetCalDataThreadProc()
        {
            try
            {
                FireStateChangedEvent(RFState.BUSY);

//                m_Result = RadioCLGetCalibrationData(m_rdr_opt_parms.CLGetCalData.CalData);
            }
            catch (ReaderException e)
            {
#if DEBUG
                CSLibrary.Diagnostics.CoreDebug.Logger.ErrorException("HighLevelInterface.ClGetCalDataThreadProc()", e);
#endif
            }
            catch (System.Exception ex)
            {
#if DEBUG
                CSLibrary.Diagnostics.CoreDebug.Logger.ErrorException("HighLevelInterface.ClGetCalDataThreadProc()", ex);
#endif
            }


            FireStateChangedEvent(RFState.IDLE);
        }

        private void CLGetBatLvThreadProc()
        {
            try
            {
                FireStateChangedEvent(RFState.BUSY);

//                m_Result = RadioCLGetBatteryLevel(m_rdr_opt_parms.CLGetBatLv.InternalRegister1, m_rdr_opt_parms.CLGetBatLv.BatLvData);
            }
            catch (ReaderException e)
            {
#if DEBUG
                CSLibrary.Diagnostics.CoreDebug.Logger.ErrorException("HighLevelInterface.ClGetBatLvThreadProc()", e);
#endif
            }
            catch (System.Exception ex)
            {
#if DEBUG
                CSLibrary.Diagnostics.CoreDebug.Logger.ErrorException("HighLevelInterface.ClGetBatLvThreadProc()", ex);
#endif
            }


            FireStateChangedEvent(RFState.IDLE);
        }

        private void CLSetShelfLifeThreadProc()
        {
            try
            {
                FireStateChangedEvent(RFState.BUSY);

//                m_Result = RadioCLSetShelfLife(m_rdr_opt_parms.CLSetShelfLife.InternalRegister1, m_rdr_opt_parms.CLSetShelfLife.InternalRegister2);
            }
            catch (ReaderException e)
            {
#if DEBUG
                CSLibrary.Diagnostics.CoreDebug.Logger.ErrorException("HighLevelInterface.ClSetShelfLifeThreadProc()", e);
#endif
            }
            catch (System.Exception ex)
            {
#if DEBUG
                CSLibrary.Diagnostics.CoreDebug.Logger.ErrorException("HighLevelInterface.ClSetShelfLifeThreadProc()", ex);
#endif
            }


            FireStateChangedEvent(RFState.IDLE);
        }

        private void CLInitThreadProc()
        {
            try
            {
                FireStateChangedEvent(RFState.BUSY);

//                m_Result = RadioCLInitialize(m_rdr_opt_parms.CLInit.InternalRegister1);
            }
            catch (ReaderException e)
            {
#if DEBUG
                CSLibrary.Diagnostics.CoreDebug.Logger.ErrorException("HighLevelInterface.ClInitThreadProc()", e);
#endif
            }
            catch (System.Exception ex)
            {
#if DEBUG
                CSLibrary.Diagnostics.CoreDebug.Logger.ErrorException("HighLevelInterface.ClInitThreadProc()", ex);
#endif
            }


            FireStateChangedEvent(RFState.IDLE);
        }

        private void CLGetSensorValueThreadProc()
        {
            try
            {
                FireStateChangedEvent(RFState.BUSY);

//                m_Result = RadioCLGetSensorValue(m_rdr_opt_parms.CLGetSensorValue.InternalRegister1, m_rdr_opt_parms.CLGetSensorValue.SensorValueData);
            }
            catch (ReaderException e)
            {
#if DEBUG
                CSLibrary.Diagnostics.CoreDebug.Logger.ErrorException("HighLevelInterface.ClGetSensorValueThreadProc()", e);
#endif
            }
            catch (System.Exception ex)
            {
#if DEBUG
                CSLibrary.Diagnostics.CoreDebug.Logger.ErrorException("HighLevelInterface.ClGetSensorValueThreadProc()", ex);
#endif
            }


            FireStateChangedEvent(RFState.IDLE);
        }

        private void CLOpenAreaThreadProc()
        {
            try
            {
                FireStateChangedEvent(RFState.BUSY);

//                m_Result = RadioCLOpenArea(m_rdr_opt_parms.CLOpenArea.InternalRegister1, m_rdr_opt_parms.CLOpenArea.InternalRegister2);
            }
            catch (ReaderException e)
            {
#if DEBUG
                CSLibrary.Diagnostics.CoreDebug.Logger.ErrorException("HighLevelInterface.ClOpenAreaThreadProc()", e);
#endif
            }
            catch (System.Exception ex)
            {
#if DEBUG
                CSLibrary.Diagnostics.CoreDebug.Logger.ErrorException("HighLevelInterface.ClOpenAreaThreadProc()", ex);
#endif
            }

            FireStateChangedEvent(RFState.IDLE);
        }

        private void CLAccessFifoThreadProc()
        {
            try
            {
                FireStateChangedEvent(RFState.BUSY);

//                m_Result = RadioCLAccessFifo(m_rdr_opt_parms.CLAccessFifo.InternalRegister1, m_rdr_opt_parms.CLAccessFifo.InternalRegister2, m_rdr_opt_parms.CLAccessFifo.InternalRegister3, m_rdr_opt_parms.CLAccessFifo.AccessFIFOData);
            }
            catch (ReaderException e)
            {
#if DEBUG
                CSLibrary.Diagnostics.CoreDebug.Logger.ErrorException("HighLevelInterface.ClAccessFifoThreadProc()", e);
#endif
            }
            catch (System.Exception ex)
            {
#if DEBUG
                CSLibrary.Diagnostics.CoreDebug.Logger.ErrorException("HighLevelInterface.ClAccessFifoThreadProc()", ex);
#endif
            }

            FireStateChangedEvent(RFState.IDLE);
        }

        
        public Result CholdChain_GetTemperature(out UInt16 RawTemp)
        {
            uint value = 0;

            RawTemp = 0;

            if (MacReadRegister (MacRegister.HST_TAGACC_DESC_CFG, ref value) != Result.OK)
                return Result.FAILURE;

            value = value & (~((uint)1 << 23));
            value = value | (1 << 24);

            if (MacWriteRegister(MacRegister.HST_TAGACC_DESC_CFG, value) != Result.OK)
                return Result.FAILURE;

            CurrentOperation = Operation.EM_GetSensorData;

            switch (COMM_HostCommand(HST_CMD.CUSTOMEMGETSENSORDATA))
            {
                case Result.OK:
                    {
                        uint macErr = 0;
                        if ((m_Result = GetMacErrorCode(ref macErr)) != Result.OK)
                        {
                            throw new ReaderException(m_Result, "GetMacErrorCode failed");
                        }

                        if (macErr > 0)
                        {
#if DEBUG
                                CSLibrary.Diagnostics.CoreDebug.Logger.Warn(string.Format("HighLevelInterface.EM4325SPIThreadProc() MAC ERROR CODE {0}", macErr));
#endif

                            if (MacErrorIsOverheat(macErr) || MacErrorIsNegligible(macErr))
                            {
                                ThrowException(MacClearError());
                            }
                        }

                        if (m_TagAccessStatus == 0)
                        {
                            m_Result = Result.NO_TAG_FOUND;
                        }
                        else if (m_TagAccessStatus == 1)
                        {
                            m_Result = Result.RADIO_FAILURE;
                        }

                        RawTemp = (UInt16)(tagreadbuf[0] & 0x1ffU);
                        
                        return m_Result;
                    }
                    break;
                case Result.OPERATION_CANCELLED:
                    ThrowException(MacClearError());
                    break;
                default:
                    FireStateChangedEvent(RFState.RESET);
                    return Result.FAILURE;
            }

            return Result.OK;
        }
        
        private void G2X_Change_EASThreadProc()
        {
            UInt32 value = 0;

            try
            {
                FireStateChangedEvent(RFState.BUSY);

                CurrentOperationResult = Result.NO_TAG_FOUND;

                //m_Result = RadioG2X_Change_EAS();
                MacReadRegister(MacRegister.HST_TAGACC_DESC_CFG, ref value);
                if (m_rdr_opt_parms.ChangeEAS.enableEAS)
                    value |= (1 << 22);
                else
                    value &= (~(1U << 22));
                MacWriteRegister(MacRegister.HST_TAGACC_DESC_CFG, value);

                MacWriteRegister(MacRegister.HST_TAGACC_ACCPWD, m_rdr_opt_parms.ChangeEAS.accessPassword);

                Start18K6CRequest(7, SelectFlags.SELECT);

                if (COMM_HostCommand(HST_CMD.CUSTOMG2XCHANGEEAS) != Result.OK || CurrentOperationResult != Result.OK)
                    m_Result = Result.FAILURE;
            }
            catch (System.Exception ex)
            {
#if DEBUG
                CSLibrary.Diagnostics.CoreDebug.Logger.ErrorException("HighLevelInterface.G2X_EAS_AlarmThreadProc()", ex);
#endif
            }

            FireAccessCompletedEvent(
                new OnAccessCompletedEventArgs(
                    m_Result == Result.OK,
                    Bank.EPC,
                    TagAccess.CHANGEEAS,
                    m_rdr_opt_parms.TagReadEPC.epc));

            FireStateChangedEvent(RFState.IDLE);
        }

        private void G2X_EAS_AlarmThreadProc()
        {
            UInt32 value = 0;

            try
            {
                FireStateChangedEvent(RFState.BUSY);

                MacReadRegister(MacRegister.HST_TAGACC_DESC_CFG, ref value);
                value |= (1 << 22);
                MacWriteRegister(MacRegister.HST_TAGACC_DESC_CFG, value);
                MacWriteRegister(MacRegister.HST_MBP_RFU_0x0402, 0xe0);

		        //MacWriteRegister(HST_CMD, CMD_CUSTOM_G2XEASALARM);
                //m_Result = RadioG2X_EAS_Alarm();
                m_Result = COMM_HostCommand(HST_CMD.CUSTOMG2XEASALARM);
            }
            catch (System.Exception ex)
            {
#if DEBUG
                CSLibrary.Diagnostics.CoreDebug.Logger.ErrorException("HighLevelInterface.G2X_EAS_AlarmThreadProc()", ex);
#endif
            }

            FireStateChangedEvent(RFState.IDLE);
        }

        private void G2X_ChangeConfigThreadProc()
        {
            //const ushort HST_TAGACC_ACCPWD = 0xa06;
            //const ushort HST_G2IL_REGISTER = 0x402;

            try
            {
                FireStateChangedEvent(RFState.BUSY);

                // Set up the access password
                m_Result = MacWriteRegister(MacRegister.HST_TAGACC_ACCPWD, m_rdr_opt_parms.G2Config.accessPassword);

                if (m_Result == Result.OK)
                {
                    UInt32 configword = 0;

                    if (m_rdr_opt_parms.G2Config.TempetrAlarm)
                        configword |= 1;

                    if (m_rdr_opt_parms.G2Config.ExternalSupply)
                        configword |= 1 << 1;

                    if (m_rdr_opt_parms.G2Config.InvertDigitalOutput)
                        configword |= 1 << 4;

                    if (m_rdr_opt_parms.G2Config.TransparentMode)
                        configword |= 1 << 5;

                    if (m_rdr_opt_parms.G2Config.TransparentModeData)
                        configword |= 1 << 6;

                    if (m_rdr_opt_parms.G2Config.MaxBackscatterStrength)
                        configword |= 1 << 9;

                    if (m_rdr_opt_parms.G2Config.DigitalOutput)
                        configword |= 1 << 10;

                    if (m_rdr_opt_parms.G2Config.ReadRangeReduction)
                        configword |= 1 << 11;

                    if (m_rdr_opt_parms.G2Config.ReadProtectEPC)
                        configword |= 1 << 13;

                    if (m_rdr_opt_parms.G2Config.ReadProtectTID)
                        configword |= 1 << 14;

                    if (m_rdr_opt_parms.G2Config.PSFAlarm)
                        configword |= 1 << 15;

                    m_Result = MacWriteRegister(MacRegister.HST_MBP_RFU_0x0402, configword);

                    //                    m_Result = RadioG2X_ChangeConfig();
                    m_Result = COMM_HostCommand(HST_CMD.CUSTOMG2XCHANGECONFIG);
                }
            }
            catch (System.Exception ex)
            {
#if DEBUG
                CSLibrary.Diagnostics.CoreDebug.Logger.ErrorException("HighLevelInterface.G2X_ChangeConfigProc()", ex);
#endif
            }

            FireStateChangedEvent(RFState.IDLE);
        }

        private void QT_CommandProc()
        {
            UInt32 value = 0;

            try
            {
                FireStateChangedEvent(RFState.BUSY);

                // Set up the access password
                m_Result = MacWriteRegister(MacRegister.HST_TAGACC_ACCPWD, m_rdr_opt_parms.QTCommand.accessPassword);
                if (m_Result != Result.OK)
                    return;

                //m_Result = RadioQT_Command(m_rdr_opt_parms.QTCommand.RW, m_rdr_opt_parms.QTCommand.TP, m_rdr_opt_parms.QTCommand.SR, m_rdr_opt_parms.QTCommand.MEM);

                m_Result = MacReadRegister(MacRegister.HST_INV_CFG, ref value);
		        value |= (1 << 14);
                m_Result = MacWriteRegister(MacRegister.HST_INV_CFG, value);

                m_Result = MacReadRegister(MacRegister.HST_TAGACC_DESC_CFG, ref value);
		        value &= 0xFFC0000F;  // 1111111111 000000000000000000 1111
                if (m_rdr_opt_parms.QTCommand.RW != 0) value |= 1 << 4;
                if (m_rdr_opt_parms.QTCommand.TP != 0) value |= 1 << 5;
                if (m_rdr_opt_parms.QTCommand.SR != 0) value |= 1 << 21;
                if (m_rdr_opt_parms.QTCommand.MEM != 0) value |= 1 << 20;
                m_Result = MacWriteRegister(MacRegister.HST_TAGACC_DESC_CFG, value);

//                m_pMac->WriteRegister(HST_CMD, CMD_CUSTOM_M4QT);
                m_Result = COMM_HostCommand(HST_CMD.CUSTOMM4QT);
            }
            catch (System.Exception ex)
            {
#if DEBUG
                CSLibrary.Diagnostics.CoreDebug.Logger.ErrorException("HighLevelInterface.QT_CommandProc()", ex);
#endif
            }

            FireStateChangedEvent(RFState.IDLE);
        }

        private void EM4325GetUidThreadProc()
        {
            UInt32 value;

            try
            {
                FireStateChangedEvent(RFState.BUSY);

                m_Result = COMM_HostCommand(HST_CMD.CUSTOMEM4325GETUID);

                switch (m_Result)
                {
                    case Result.OK:
                        {
                            uint macErr = 0;
                            if ((m_Result = GetMacErrorCode(ref macErr)) != Result.OK)
                            {
                                throw new ReaderException(m_Result, "GetMacErrorCode failed");
                            }
                            if (macErr > 0)
                            {
#if DEBUG
                                CSLibrary.Diagnostics.CoreDebug.Logger.Warn(string.Format("HighLevelInterface.EM4325SPIThreadProc() MAC ERROR CODE {0}", macErr));
#endif
                                if (MacErrorIsOverheat(macErr) || MacErrorIsNegligible(macErr))
                                {
                                    ThrowException(MacClearError());
                                }
                            }

                            if (m_TagAccessStatus == 0)
                            {
                                m_Result = Result.NO_TAG_FOUND;
                            }
                            else if (m_TagAccessStatus == 1)
                            {
                                m_Result = Result.RADIO_FAILURE;
                            }
                        }
                        break;
                    case Result.OPERATION_CANCELLED:
                        ThrowException(MacClearError());
                        break;
                    default:
                        FireStateChangedEvent(RFState.RESET);
                        return;
                }
            }
            catch (System.Exception ex)
            {
#if DEBUG
                CSLibrary.Diagnostics.CoreDebug.Logger.ErrorException("HighLevelInterface.QT_CommandProc()", ex);
#endif
            }

            FireStateChangedEvent(RFState.IDLE);
        }

        private void EMResetAlarmsThreadProc()
        {
            UInt32 value;

            try
            {
                FireStateChangedEvent(RFState.BUSY);

                m_Result = COMM_HostCommand(HST_CMD.CUSTOMEMRESETALARMS);

                switch (m_Result)
                {
                    case Result.OK:
                        {
                            uint macErr = 0;
                            if ((m_Result = GetMacErrorCode(ref macErr)) != Result.OK)
                            {
                                throw new ReaderException(m_Result, "GetMacErrorCode failed");
                            }
                            if (macErr > 0)
                            {
#if DEBUG
                                CSLibrary.Diagnostics.CoreDebug.Logger.Warn(string.Format("HighLevelInterface.EM4325SPIThreadProc() MAC ERROR CODE {0}", macErr));
#endif
                                if (MacErrorIsOverheat(macErr) || MacErrorIsNegligible(macErr))
                                {
                                    ThrowException(MacClearError());
                                }
                            }

                            if (m_TagAccessStatus == 0)
                            {
                                m_Result = Result.NO_TAG_FOUND;
                            }
                            else if (m_TagAccessStatus == 1)
                            {
                                m_Result = Result.RADIO_FAILURE;
                            }
                        }
                        break;
                    case Result.OPERATION_CANCELLED:
                        ThrowException(MacClearError());
                        break;
                    default:
                        FireStateChangedEvent(RFState.RESET);
                        return;
                }
            }
            catch (System.Exception ex)
            {
#if DEBUG
                CSLibrary.Diagnostics.CoreDebug.Logger.ErrorException("HighLevelInterface.QT_CommandProc()", ex);
#endif
            }

            FireStateChangedEvent(RFState.IDLE);
        }

        private void EMGetSensorDataThreadProc()
        {
            UInt32 value;

            try
            {
                FireStateChangedEvent(RFState.BUSY);

                m_Result = COMM_HostCommand(HST_CMD.CUSTOMEMGETSENSORDATA);

                switch (m_Result)
                {
                    case Result.OK:
                        {
                            uint macErr = 0;
                            if ((m_Result = GetMacErrorCode(ref macErr)) != Result.OK)
                            {
                                throw new ReaderException(m_Result, "GetMacErrorCode failed");
                            }

                            if (macErr > 0)
                            {
#if DEBUG
                                CSLibrary.Diagnostics.CoreDebug.Logger.Warn(string.Format("HighLevelInterface.EM4325SPIThreadProc() MAC ERROR CODE {0}", macErr));
#endif

                                if (MacErrorIsOverheat(macErr) || MacErrorIsNegligible(macErr))
                                {
                                    ThrowException(MacClearError());
                                }
                            }

                            if (m_TagAccessStatus == 0)
                            {
                                m_Result = Result.NO_TAG_FOUND;
                            }
                            else if (m_TagAccessStatus == 1)
                            {
                                m_Result = Result.RADIO_FAILURE;
                            }

                            m_Result = Result.OK;
                        }
                        break;
                    case Result.OPERATION_CANCELLED:
                        ThrowException(MacClearError());
                        break;
                    default:
                        FireStateChangedEvent(RFState.RESET);
                        return;
                }
            }
            catch (System.Exception ex)
            {
#if DEBUG
                CSLibrary.Diagnostics.CoreDebug.Logger.ErrorException("HighLevelInterface.QT_CommandProc()", ex);
#endif
            }

            FireStateChangedEvent(RFState.IDLE);
        }
    
/*
 * private void EM4325SPIThreadProc()
        {
            UInt32 value;

            try
            {
                FireStateChangedEvent(RFState.BUSY);

                value = (UInt32)(m_rdr_opt_parms.EM4325SPI.ByteDelay | (m_rdr_opt_parms.EM4325SPI.InitialDelay << 2) | (m_rdr_opt_parms.EM4325SPI.SClk << 4) | (m_rdr_opt_parms.EM4325SPI.ResponseSize << 6) | (m_rdr_opt_parms.EM4325SPI.CommandSize << 9));

                m_Result = MacWriteRegister(MacRegister.HST_RFTC_RFU_0x0B2C, value);

                m_Result = MacWriteRegister(MacRegister.HST_RFTC_RFU_0x0B2D, (UInt32)(m_rdr_opt_parms.EM4325SPI.SpiCommand >> 32));
                
                m_Result = MacWriteRegister(MacRegister.HST_RFTC_RFU_0x0B2E, (UInt32)(m_rdr_opt_parms.EM4325SPI.SpiCommand));

                m_Result = COMM_HostCommand(HST_CMD.CUSTOMEMSENDSPI);

                switch (m_Result)
                {
                    case Result.OK:
                        {
                            uint macErr = 0;
                            if ((m_Result = GetMacErrorCode(ref macErr)) != Result.OK)
                            {
                                throw new ReaderException(m_Result, "GetMacErrorCode failed");
                            }
                            if (macErr > 0)
                            {
#if DEBUG
                                CSLibrary.Diagnostics.CoreDebug.Logger.Warn(string.Format("HighLevelInterface.EM4325SPIThreadProc() MAC ERROR CODE {0}", macErr));
#endif

                                if (MacErrorIsOverheat(macErr) || MacErrorIsNegligible(macErr))
                                {
                                    ThrowException(MacClearError());
                                }
                            }

                            if (m_TagAccessStatus == 0)
                            {
                                m_Result = Result.NO_TAG_FOUND;
                            }
                            else if (m_TagAccessStatus == 1)
                            {
                                m_Result = Result.RADIO_FAILURE;
                            }
                        }
                        break;
                    case Result.OPERATION_CANCELLED:
                        ThrowException(MacClearError());
                        break;
                    default:
                        FireStateChangedEvent(RFState.RESET);
                        return;
                }
            }
            catch (System.Exception ex)
            {
#if DEBUG
                CSLibrary.Diagnostics.CoreDebug.Logger.ErrorException("HighLevelInterface.QT_CommandProc()", ex);
#endif
            }

            FireStateChangedEvent(RFState.IDLE);
        }
*/

        // Compilerable with .net 2.0
        public delegate void Action();

        private void OperationProcess(Action routine, bool wait)
        {
            if (wait)
                routine ();
            else
            {
                g_hWndThread = new Thread(new ThreadStart(routine));
                g_hWndThread.Priority = ThreadPriority.Normal;
                g_hWndThread.IsBackground = true;
                g_hWndThread.Name = routine.ToString ();
                g_hWndThread.Start();
                WaitToBusy();
            }
        }

        void LBTPortBusy(UInt16 Port)
        {
            RFState TempState = State;

            LBTChannelBusy = Port;
            ChannelStatus[Port] = RFState.CH_BUSY;
            FireStateChangedEvent(RFState.CH_BUSY);
            State = TempState;
        }

        void LBTPortClear(UInt16 Port)
        {
            RFState TempState = State;

            LBTChannelClear = Port;
            ChannelStatus[Port] = RFState.CH_CLEAR;
            FireStateChangedEvent(RFState.CH_CLEAR);
            State = TempState;
        }

#if oldcode
        private Int32 TagRangingCallback(
            [In]    bool crcInvalid,
            [In]    Single rssi,
            [In]    UInt32 antennaPort,
            [In]    byte freqChannel,
            [In]    UInt16 pc,
            [In]    UInt32 epcLength,
            [In]    byte[] epc,
            [In]    UInt32 ms_ctr,
            [In]    UInt16 crc16
            )
        {
            if (crcInvalid == true)
                return (int)(Result.OK);

            try
            {
                if (Interlocked.Equals(bStop, 1))
                    return (int)(Result.OPERATION_CANCELLED);
                    
                byte[] byteEpc = new byte[epcLength];
                Array.Copy(epc, 22, byteEpc, 0, (int)epcLength);


                if (m_save_fixed_channel)
                {
                    return FireCallbackEvent(
                            new OnAsyncCallbackEventArgs(
                            new TagCallbackInfo(
                            crcInvalid,
                            rssi,
                            antennaPort,
                            (byte)m_save_freq_channel,
                            new S_PC(pc),
                            new S_EPC(byteEpc),
                            ms_ctr,
                            crc16,
                            Name),
                            CallbackType.TAG_RANGING));
                }
                else
                {
                    return FireCallbackEvent(
                            new OnAsyncCallbackEventArgs(
                            new TagCallbackInfo(
                            crcInvalid,
                            rssi,
                            antennaPort,
                            (byte)currentInventoryFreqRevIndex[freqChannel],
                            new S_PC(pc),
                            new S_EPC(byteEpc),
                            ms_ctr,
                            crc16,
                            Name),
                            CallbackType.TAG_RANGING));
                }
            }
            catch (System.Exception ex)
            {
#if DEBUG
                CSLibrary.Diagnostics.CoreDebug.Logger.ErrorException("HighLevelInterface.TagRangingCallback()", ex);
#endif
                return (int)(Result.SYSTEM_CATCH_EXCEPTION);
            }
        }
#endif

        // New Format
        private Int32 TagRangingCallback(
            [In]    bool crcInvalid,
            [In]    Single rssi,
            [In]    UInt32 antennaPort,
            [In]    byte freqChannel,
            [In]    UInt32 ms_ctr,
            [In]    UInt16 crc16,
            [In]    UInt16 pc,
            [In]    byte[] epcData,
            [In]    UInt16[] Bank1Data,
            [In]    UInt16[] Bank2Data
            )
        {
            byte realFreqChannel;

            if (crcInvalid == true)
                return (int)(Result.OK);

            try
            {
                if (Interlocked.Equals(bStop, 1))
                    return (int)(Result.OPERATION_CANCELLED);

                if (m_save_fixed_channel)
                    realFreqChannel = (byte)m_save_freq_channel;
                else
                    realFreqChannel = (byte)currentInventoryFreqRevIndex[freqChannel];

                return FireCallbackEvent(
                        new OnAsyncCallbackEventArgs(
                        new TagCallbackInfo(
                        0,
                        rssi,
                        antennaPort,
                        realFreqChannel,
                        new S_PC(pc),
                        new S_EPC(epcData),
                        (UInt16 [])Bank1Data.Clone(),
                        (UInt16[])Bank2Data.Clone(),
                        ms_ctr,
                        crc16,
                        Name),
                        CallbackType.TAG_RANGING));
            }
            catch (System.Exception ex)
            {
#if DEBUG
                CSLibrary.Diagnostics.CoreDebug.Logger.ErrorException("HighLevelInterface.TagRangingCallback()", ex);
#endif
                return (int)(Result.SYSTEM_CATCH_EXCEPTION);
            }
        }

        private Int32 TagRangingCallback(
            [In]    bool crcInvalid,
            [In]    Single rssi,
            [In]    UInt32 antennaPort,
            [In]    byte freqChannel,
            [In]    UInt32 ms_ctr,
            [In]    UInt16 crc16,
            [In]    UInt16 pc,
            [In]    byte[] epcData,
            [In]    S_XPC_W1 xpc_W1,
            [In]    S_XPC_W2 xpc_W2,
            [In]    UInt16[] Bank1Data,
            [In]    UInt16[] Bank2Data
            )
        {
            byte realFreqChannel;

            if (crcInvalid == true)
                return (int)(Result.OK);

            try
            {
                if (Interlocked.Equals(bStop, 1))
                    return (int)(Result.OPERATION_CANCELLED);

                if (m_save_fixed_channel)
                    realFreqChannel = (byte)m_save_freq_channel;
                else
                    realFreqChannel = (byte)currentInventoryFreqRevIndex[freqChannel];

                return FireCallbackEvent(
                        new OnAsyncCallbackEventArgs(
                        new TagCallbackInfo(
                        0,
                        rssi,
                        antennaPort,
                        realFreqChannel,
                        new S_PC(pc),
                        xpc_W1,
                        xpc_W2,
                        new S_EPC(epcData),
                        (UInt16[])Bank1Data.Clone(),
                        (UInt16[])Bank2Data.Clone(),
                        ms_ctr,
                        crc16,
                        Name),
                        CallbackType.TAG_RANGING));
            }
            catch (System.Exception ex)
            {
#if DEBUG
                CSLibrary.Diagnostics.CoreDebug.Logger.ErrorException("HighLevelInterface.TagRangingCallback()", ex);
#endif
                return (int)(Result.SYSTEM_CATCH_EXCEPTION);
            }
        }

        private Int32 QTTagRangingCallback(
            [In]    bool crcInvalid,
            [In]    Single rssi,
            [In]    UInt32 antennaPort,
            [In]    byte freqChannel,
            [In]    UInt16 pc,
            [In]    UInt32 epcLength,
            [In]    byte [] epc,
            [In]    UInt32 ms_ctr,
            [In]    UInt16 crc16
            )
        {
            if (crcInvalid == true)
                return (int)(Result.OK);

            try
            {
                if (Interlocked.Equals(bStop, 1))
                    return (int)(Result.OPERATION_CANCELLED);

//                byte[] tagData = new byte[epcLength];
//                Marshal.Copy(epc, tagData, 0, (int)epcLength);

                UInt16 PcPublic = pc;
                int EpcPublicLength = (pc >> 11) * 2;
                byte[] EpcPublic = new byte[EpcPublicLength];
                Array.Copy(epc, 0, EpcPublic, 0, EpcPublicLength);

                int PrivateOffset = EpcPublicLength;
                UInt16 PcPrivate = (UInt16)(epc[PrivateOffset] << 8 | epc[PrivateOffset + 1]);
                int EpcPrivateLength = (PcPrivate >> 11) * 2;
                byte[] EpcPrivate = new byte[EpcPrivateLength];
                Array.Copy(epc, PrivateOffset + 2, EpcPrivate, 0, EpcPrivateLength);

                return FireCallbackEvent(
                    new OnAsyncCallbackEventArgs(
                    new TagCallbackInfo(
                    crcInvalid,
                    rssi,
                    antennaPort,
                    freqChannel,
                    new S_PC(PcPrivate),
                    new S_EPC(EpcPrivate),
                    new S_PC(PcPublic),
                    new S_EPC(EpcPublic),
                    ms_ctr,
                    Name),
                    CallbackType.TAG_RANGING));
            }
            catch (System.Exception ex)
            {
#if DEBUG
                CSLibrary.Diagnostics.CoreDebug.Logger.ErrorException("HighLevelInterface.TagRangingCallback()", ex);
#endif
                return (int)(Result.SYSTEM_CATCH_EXCEPTION);
            }
        }

        private void InventoryResponseCallBack(byte[] InventoryPacket)
        {
            // packet information
            byte flags = InventoryPacket[1];
            UInt16 pkt_len = (UInt16)(InventoryPacket[5] << 8 | InventoryPacket[4]);  // (UInt16)(RecvBuf[4] + (RecvBuf[5] << 8));
            UInt32 ms_ctr = (UInt32)(InventoryPacket[8] | InventoryPacket[9] << 8 | InventoryPacket[10] << 16 | InventoryPacket[11] << 24);
            byte freqChannel = InventoryPacket[15];
            int data1len = InventoryPacket[16]; // Bank 1 word length
            int data2len = InventoryPacket[17]; // Bank 2 word length
            UInt16 antennaPort = (UInt16)(InventoryPacket[18] | InventoryPacket[19] << 8);
            UInt16 pc = (UInt16)(InventoryPacket[20] << 8 | InventoryPacket[21]);
            S_XPC_W1 xpc_w1 = null;
            S_XPC_W2 xpc_w2 = null;

            // other information
            bool crcInvalid = ((flags & 0x01) == 0x01);
            UInt16 datalen = (UInt16)(((pkt_len - 3) * 4) - ((flags >> 6) & 3) - 4); // byte langth
            int epclen = (pc >> 11) * 2; // epc byte length
            UInt16 crc16 = (UInt16)(InventoryPacket[22 + datalen] << 8 | InventoryPacket[23 + datalen]);
            Single rssi;
            byte[] EpcData = new byte[epclen];
            UInt16[] Bank1Data = new UInt16[data1len];
            UInt16[] Bank2Data = new UInt16[data2len];

            int epcoffset = 22;

            if ((pc & 0x0200) != 0x0000)
            {
                xpc_w1 = new S_XPC_W1((UInt16)(InventoryPacket[22] << 8 | InventoryPacket[23]));
                epcoffset += 2;
                epclen -= 2;

                if (xpc_w1.XEB)
                {
                    xpc_w2 = new S_XPC_W2((UInt16)(InventoryPacket[24] << 8 | InventoryPacket[25]));
                    epcoffset += 2;
                    epclen -= 2;
                }
            }


            Array.Copy(InventoryPacket, epcoffset, EpcData, 0, epclen);
            if (data1len > 0)
            {
                //byte[] data = new byte[data1len];
                //Array.Copy(InventoryPacket, 22 + epclen, data, 0, data1len * 2);
                HexEncoding.Copy(InventoryPacket, epcoffset + epclen, Bank1Data, 0, data1len);
            }
            if (data2len > 0)
            {
                //byte[] data = new byte[data2len];
                //Array.Copy(InventoryPacket, 22 + epclen + (data1len * 2), data, 0, data2len * 2);
                HexEncoding.Copy(InventoryPacket, epcoffset + epclen + (data1len * 2), Bank2Data, 0, data2len);
            }

            switch (OEMChipSetID)
            {
                default:
                    rssi = (Single)(InventoryPacket[13] * 0.8);
                    break;

                case ChipSetID.R2000:
                    {
                        int iMantissa = InventoryPacket[13] & 0x07;
                        int iExponent = (InventoryPacket[13] >> 3) & 0x1F;

                        double dRSSI = 20.0 * Math.Log10(Math.Pow(2.0, (double)iExponent) * (1.0 + ((double)iMantissa / 8.0)));
                        rssi = (Single)dRSSI;
                    }
                    break;
            }

            if (ChannelStatus[antennaPort] == RFState.CH_BUSY)
                LBTPortClear(antennaPort);

            switch (CurrentOperation)
            {
                case Operation.TAG_INVENTORY:
                    TagSeachAnyCallback(rssi, pc, datalen, InventoryPacket, ms_ctr, antennaPort);
                    break;

                case Operation.TAG_RANGING:
                    if (m_rdr_opt_parms.TagRanging.QTMode == true)
                    {
                        QTTagRangingCallback(crcInvalid, rssi, antennaPort, freqChannel, pc, datalen, InventoryPacket, ms_ctr, crc16);
                    }
                    else
                    {
                        TagRangingCallback(crcInvalid, rssi, antennaPort, freqChannel, ms_ctr, crc16, pc, EpcData, xpc_w1, xpc_w2, Bank1Data, Bank2Data);
                    }
                    break;

                case Operation.TAG_SEARCHING:
                    TagSeachOneCallback(rssi, pc, datalen, InventoryPacket, ms_ctr);
                    break;

                case Operation.TAG_READ_EPC:
                    break;
                case Operation.TAG_READ_ACC_PWD:
                    break;
                case Operation.TAG_READ_KILL_PWD:
                    break;
                case Operation.TAG_READ_PC:
                    break;
                case Operation.TAG_READ_PROTECT:
                    break;
                case Operation.TAG_READ_TID:
                    break;
                case Operation.TAG_READ_USER:
                    break;
                case Operation.G2_CHANGE_EAS:
                    break;
            }
        }

#if oldcode
        private void InventoryResponseCallBack(byte [] InventoryPacket)
        {
            byte flags = InventoryPacket[1];
            bool crcInvalid = ((flags & 0x01) == 0x01);
            UInt16 pkt_len = (UInt16)(InventoryPacket[5] << 8 | InventoryPacket[4]);  // (UInt16)(RecvBuf[4] + (RecvBuf[5] << 8));
            UInt32 ms_ctr = (UInt32)(InventoryPacket[8] | InventoryPacket[9] << 8 | InventoryPacket[10] << 16 | InventoryPacket[11] << 24);
            //Single rssi = (Single)(InventoryPacket[13] * 0.8);
            Single rssi;
            byte freqChannel = InventoryPacket[15];
            UInt16 antennaPort = (UInt16)(InventoryPacket[18] | InventoryPacket[19] << 8);
            UInt16 pc = (UInt16)(InventoryPacket[20] << 8 | InventoryPacket[21]);
            UInt16 datalen = (UInt16)(((pkt_len - 3) * 4) - ((flags >> 6) & 3) - 4); // byte langth
            UInt16 crc16 = (UInt16)(InventoryPacket[22 + datalen] << 8 | InventoryPacket[23 + datalen]);
            //UInt16 datalen = (UInt16)(((pkt_len - 3) * 4) - ((flags >> 6) & 3)); // byte langth
            int data1len = InventoryPacket[16];
            int data2len = InventoryPacket[17];

            UInt16[] Bank1Data = new byte[data1len];
            UInt16[] Bank2Data = new byte[data2len];

            if (data2len > 0)
            {
                byte[] data = new byte[data2len];
                Array.Copy(InventoryPacket, 22 + datalen + data1len , data, 0, data2len * 2);
            }

            if (data1len > 0)
            {
                byte[] data = new byte[data1len];
                Array.Copy(InventoryPacket, 22 + datalen, data, 0, data1len * 2);
            }

            switch (OEMChipSetID)
            {
                default:
                    rssi = (Single)(InventoryPacket[13] * 0.8);
                    break;

                case ChipSetID.R2000:
                    {
                        int iMantissa = InventoryPacket[13] & 0x07;
                        int iExponent = (InventoryPacket[13] >> 3) & 0x1F;

                        double dRSSI = 20.0 * Math.Log10(Math.Pow(2.0, (double)iExponent) * (1.0 + ((double)iMantissa / 8.0)));
                        rssi = (Single)dRSSI;
                    }
                    break;
            }


            if (ChannelStatus[antennaPort] == RFState.CH_BUSY)
                LBTPortClear(antennaPort);


            if (InventoryPacket[20] > 0) // Check Multi-Bank 1 data length
            {
            }

            if (InventoryPacket[20] > 0) // Check Multi-Bank 2 data length
            {
            }

            UInt16 pc = (UInt16)(InventoryPacket[20] << 8 | InventoryPacket[21]);
            UInt16 pc = (UInt16)(InventoryPacket[20] << 8 | InventoryPacket[21]);

            switch (CurrentOperation)
            {
                case Operation.TAG_INVENTORY:
                    TagSeachAnyCallback(rssi, pc, datalen, InventoryPacket, ms_ctr, antennaPort);
                    break;

                case Operation.TAG_RANGING:
                    if (m_rdr_opt_parms.TagRanging.QTMode == true)
                    {
                        //QTTagRangingCallback(crcInvalid, rssi, antennaPort, freqChannel, pc, datalen, InventoryPacket, ms_ctr, crc16);
                        QTTagRangingCallback(crcInvalid, rssi, antennaPort, freqChannel, pc, datalen, InventoryPacket, ms_ctr, crc16, Bank1DataLen, Bank2DataLen);
                    }
                    else
                    {
                        TagRangingCallback(crcInvalid, rssi, antennaPort, freqChannel, pc, datalen, InventoryPacket, ms_ctr, crc16);
                        TagRangingCallback(crcInvalid, rssi, antennaPort, freqChannel, pc, datalen, InventoryPacket, ms_ctr, crc16, Bank1DataLen, Bank2DataLen);
                    }
                    break;

                case Operation.TAG_SEARCHING:
                    TagSeachOneCallback(rssi, pc, datalen, InventoryPacket, ms_ctr);
                    break;

                case Operation.TAG_READ_EPC:
                    break;
                case Operation.TAG_READ_ACC_PWD:
                    break;
                case Operation.TAG_READ_KILL_PWD:
                    break;
                case Operation.TAG_READ_PC:
                    break;
                case Operation.TAG_READ_PROTECT:
                    break;
                case Operation.TAG_READ_TID:
                    break;
                case Operation.TAG_READ_USER:
                    break;
                case Operation.G2_CHANGE_EAS:
                    break;
            }
        }
#endif


        private void TagAccessCallBack(byte[] TagAccessPacket)
        {
            bool Invalid = ((TagAccessPacket[1] & 0x01) == 0x01);

            byte command = TagAccessPacket[12];
            byte error_code = TagAccessPacket[13];
            int len = (((TagAccessPacket[4] | TagAccessPacket[5] << 8) - 3) * 4) - ((TagAccessPacket[1] >> 6) & 3);

            if (Invalid == true)
            {
                switch (error_code)
                {
                    case 00:
                        CurrentOperationResult = Result.FAILURE;
                        break;
                    case 03:
                        CurrentOperationResult = Result.NOT_SUPPORTED;
                        break;
                    case 04:
                        CurrentOperationResult = Result.CURRENTLY_NOT_ALLOWED;
                        break;
                    case 0x0b:
                        CurrentOperationResult = Result.NONVOLATILE_WRITE_FAILED;
                        break;
                    case 0x0f:
                        CurrentOperationResult = Result.NOT_SUPPORTED;
                        break;
                                          
                }

                return;
            }

            CurrentOperationResult = Result.OK;
            switch (command)
            {
                case 0xc2: // Read/Perm Lock
                    switch (CurrentOperation)
                    {
                        case Operation.EM_GetSensorData:
                            Win32.memcpy(tagreadbuf, TagAccessPacket, 20, 2);
                            break;

                        default:
                            Win32.memcpy(tagreadbuf, TagAccessPacket, 20, (uint)len);
                            break;
                    }
                    break;

                case 0xc3: // Write
                    break;

                case 0xc4: // Kill
                    break;

                case 0xc5: // Lock
                    Win32.memcpy(tagreadbuf, TagAccessPacket, 20, (uint)len);
                    break;

                case 0x04: // EAS Alarm
                    {
                        UInt16 antennaPort = (UInt16)(TagAccessPacket[14] | TagAccessPacket[15] << 8);

                        FireCallbackEvent(
                            new OnAsyncCallbackEventArgs(
                            new TagCallbackInfo(
                            false,
                            0,
                            antennaPort,
                            0,
                            null,
                            null,
                            null,
                            null,
                            0,
                            Name),
                            CallbackType.TAG_EASALARM));
                    }
                    break;

                case 0xff: // unknown
                    break;


            }

#if nouse
//            switch (CurrentOperation)
//            {
//                case Operation.TAG_READ_PC:
//                case Operation.TAG_READ_EPC:
//                case Operation.TAG_READ_TID:
//                case Operation.TAG_READ_USER:
//                case Operation.TAG_READ_ACC_PWD:
//                case Operation.TAG_READ_KILL_PWD:
//                case Operation.TAG_READ_PROTECT:
                    if (command == 0xc2)
                    {
                    }
                    break;
            }

            return;

            switch (command)
            {
                case 0xc2: // Read
                    break;

                case 0xc3: // Write
                    break;

                case 0xc4: // Kill
                    break;

                case 0xc5: // Lock
                    break;

                case 0x04: // EAS
                    break;
            }
#endif
        }

        private void TagInventoryThreadProc()
        {
            //const ushort INV_CFG = 0x0901;
            //const ushort HST_TAGACC_BANK = 0x0a02;
            //const ushort HST_TAGACC_PTR = 0x0a03;
            //const ushort HST_TAGACC_CNT = 0x0a04;
            //const ushort HST_TAGACC_ACCPWD = 0x0a06;
            uint Value = 0;

            try
            {
                FireStateChangedEvent(RFState.BUSY);

                InternalTagInventoryParms parms = new InternalTagInventoryParms();
                parms.flags = m_rdr_opt_parms.TagInventory.flags;
                parms.tagStopCount = m_rdr_opt_parms.TagInventory.tagStopCount;

                if (m_rdr_opt_parms.TagInventory.multibanks == 0)
                {
                    MacReadRegister(MacRegister.HST_INV_CFG, ref Value);
                    Value &= 0xfcff;
                    MacWriteRegister(MacRegister.HST_INV_CFG, Value);
                }
                else
                {
                    // Set MultiBanks Info
                    MacReadRegister(MacRegister.HST_INV_CFG, ref Value);
                    Value &= 0xfcff;
                    Value |= (m_rdr_opt_parms.TagInventory.multibanks & (uint)0x03) << 16;
                    MacWriteRegister(MacRegister.HST_INV_CFG, Value);

                    // Set up the access bank register
                    Value = (UInt32)(m_rdr_opt_parms.TagInventory.bank1) | (UInt32)(((int)m_rdr_opt_parms.TagInventory.bank2) << 2);
                    MacWriteRegister(MacRegister.HST_TAGACC_BANK, Value);

                    // Set up the access pointer register (tells the offset)
                    Value = (UInt32)((m_rdr_opt_parms.TagInventory.offset1 & 0xffff) | ((m_rdr_opt_parms.TagInventory.offset2 & 0xffff) << 16));
                    MacWriteRegister(MacRegister.HST_TAGACC_PTR, Value);

                    // Set up the access count register (i.e., number values to read)
                    Value = (UInt32)((0xFF & m_rdr_opt_parms.TagInventory.count1) | ((0xFF & m_rdr_opt_parms.TagInventory.count2) << 8));
                    MacWriteRegister(MacRegister.HST_TAGACC_CNT, Value);

                    // Set up the access password
                    Value = (UInt32)(m_rdr_opt_parms.TagInventory.accessPassword);
                    MacWriteRegister(MacRegister.HST_TAGACC_ACCPWD, Value);
                }

                Start18K6CRequest(m_rdr_opt_parms.TagInventory.tagStopCount, parms.flags);

            CONTINUOUS:
                m_Result = COMM_HostCommand(HST_CMD.INV);

                switch (m_Result)
                {
                    case Result.OK:
                        {
                            if ((m_Result = GetMacErrorCode(ref LastMacErrorCode)) != Result.OK)
                            {
                                throw new ReaderException(m_Result, "GetMacErrorCode failed");
                            }
                            if (LastMacErrorCode > 0)
                            {
                                throw new ReaderException(Result.MAC_ERROR, LastMacErrorCode.ToString("D"));
                            }
                        }
                        break;

                    case Result.OPERATION_CANCELLED:
                        ThrowException(MacClearError());
                        break;

                    default:
                        FireStateChangedEvent(RFState.RESET);
                        return;
                }
            }
            catch (ReaderException e)
            {
#if DEBUG
                CSLibrary.Diagnostics.CoreDebug.Logger.ErrorException("HighLevelInterface.TagInventoryThreadProc()", e);
#endif
            }
            catch (System.Exception ex)
            {
#if DEBUG
                CSLibrary.Diagnostics.CoreDebug.Logger.ErrorException("HighLevelInterface.TagInventoryThreadProc()", ex);
#endif
            }

            FireStateChangedEvent(RFState.IDLE);
        }


        private Int32 TagSeachAnyCallback(
                [In]      float rssi,
                [In]      UInt16 pc,
                [In]      UInt32 epcLength,
                [In]      byte[] epc,
                [In]      UInt32 ms_ctr
            )
        {
            try
            {
                if (Interlocked.Equals(bStop, 1))
                    return (int)(Result.OPERATION_CANCELLED);

                byte[] byteEpc = new byte[epcLength];
                //Marshal.Copy(epc, byteEpc, 0, (int)epcLength);
                Array.Copy(epc, 22, byteEpc, 0, (int)epcLength);

                S_EPC epcrec = new S_EPC(byteEpc);

                // Check max records
//                if ((m_oem_machine == Machine.CS101 && m_sorted_epc_records.Count > 1200) ||
//                    m_sorted_epc_records.Count > 10000)
                if (m_sorted_epc_records.Count > 10000) // Max 10000 tag
                {
                    FireStateChangedEvent(RFState.BUFFER_FULL);
                    State = RFState.BUSY;
                    return (int)(Result.BUFFER_TOO_SMALL);
                }

                foreach (S_EPC m_epc_data in m_sorted_epc_records)
                {
                    if (m_epc_data.CompareTo (epcrec) == 0)
                        return 0;
                }

                m_sorted_epc_records.Add(epcrec);
                return FireCallbackEvent(
                    new OnAsyncCallbackEventArgs(
                    new TagCallbackInfo(
                    rssi,
                    new S_PC(pc),
                    epcrec,
                    ms_ctr),
                    CallbackType.TAG_INVENTORY
                    ));
            }
            catch (System.Exception ex)
            {
#if DEBUG
                CSLibrary.Diagnostics.CoreDebug.Logger.ErrorException("HighLevelInterface.TagSeachAnyCallback()", ex);
#endif
                return (int)(Result.SYSTEM_CATCH_EXCEPTION);
            }
        }

        private Int32 TagSeachAnyCallback(
                [In]      float rssi,
                [In]      UInt16 pc,
                [In]      UInt32 epcLength,
                [In]      byte[] epc,
                [In]      UInt32 ms_ctr,
                [In]      UInt16 AntennaPort
            )
        {
            try
            {
                if (Interlocked.Equals(bStop, 1))
                    return (int)(Result.OPERATION_CANCELLED);

                byte[] byteEpc = new byte[epcLength];
                //Marshal.Copy(epc, byteEpc, 0, (int)epcLength);
                Array.Copy(epc, 22, byteEpc, 0, (int)epcLength);

                S_EPC epcrec = new S_EPC(byteEpc);

                // Check max records
                //                if ((m_oem_machine == Machine.CS101 && m_sorted_epc_records.Count > 1200) ||
                //                    m_sorted_epc_records.Count > 10000)
                if (m_sorted_epc_records.Count > 10000) // Max 10000 tag
                {
                    FireStateChangedEvent(RFState.BUFFER_FULL);
                    State = RFState.BUSY;
                    return (int)(Result.BUFFER_TOO_SMALL);
                }

                foreach (S_EPC m_epc_data in m_sorted_epc_records)
                {
                    if (m_epc_data.CompareTo(epcrec) == 0)
                        return 0;
                }

                m_sorted_epc_records.Add(epcrec);
                return FireCallbackEvent(
                    new OnAsyncCallbackEventArgs(
                    new TagCallbackInfo(
                    rssi,
                    new S_PC(pc),
                    epcrec,
                    ms_ctr,
                    AntennaPort
                    ),
                    CallbackType.TAG_INVENTORY
                    ));
            }
            catch (System.Exception ex)
            {
#if DEBUG
                CSLibrary.Diagnostics.CoreDebug.Logger.ErrorException("HighLevelInterface.TagSeachAnyCallback()", ex);
#endif
                return (int)(Result.SYSTEM_CATCH_EXCEPTION);
            }
        }

        private void TagSearchOneTagThreadProc()
        {
            try
            {
                FireStateChangedEvent(RFState.BUSY);

                InternalTagSearchOneParms parms = new InternalTagSearchOneParms();
                parms.avgRssi = m_rdr_opt_parms.TagSearchOne.avgRssi;

            CONTINUOUS:
//                m_Result =  TagSearchOne(parms);
                Start18K6CRequest(0, SelectFlags.SELECT);

                // Issue the inventory command to the MAC
                m_Result = COMM_HostCommand(HST_CMD.INV);

                switch (m_Result)
                {
                    case Result.OK:
                        {
                            if ((m_Result = GetMacErrorCode(ref LastMacErrorCode)) != Result.OK)
                            {
                                throw new ReaderException(m_Result, "GetMacErrorCode failed");
                            }
                            if (LastMacErrorCode > 0)
                            {
                                throw new ReaderException(Result.MAC_ERROR, LastMacErrorCode.ToString("D"));
                            }
                        }
                        break;

                    case Result.OPERATION_CANCELLED:
                        ThrowException(MacClearError());
                        break;
                    default:
                        FireStateChangedEvent(RFState.RESET);
                        return;
                }

            }
            catch (ReaderException e)
            {
#if DEBUG
                CSLibrary.Diagnostics.CoreDebug.Logger.ErrorException("HighLevelInterface.TagSearchOneTagThreadProc()", e);
#endif
            }
            catch (System.Exception ex)
            {
#if DEBUG
                CSLibrary.Diagnostics.CoreDebug.Logger.ErrorException("HighLevelInterface.TagSearchOneTagThreadProc()", ex);
#endif
            }

            FireStateChangedEvent(RFState.IDLE);
        }

        private Int32 TagSeachOneCallback(
                [In]      float rssi,
                [In]      UInt16 pc,
                [In]      UInt32 epcLength,
                [In]      byte [] epc,
                [In]      UInt32 ms_ctr

            )
        {
            try
            {
                if (Interlocked.Equals(bStop, 1))
                    return (int)(Result.OPERATION_CANCELLED);
                byte[] byteEpc = new byte[epcLength];
                Array.Copy(epc, 22, byteEpc, 0, (int)epcLength);
//                Marshal.Copy(epc, byteEpc, 0, (int)epcLength);

                return FireCallbackEvent(
                    new OnAsyncCallbackEventArgs(
                    new TagCallbackInfo(
                    rssi,
                    new S_PC(pc),
                    new S_EPC(byteEpc)),
                    CallbackType.TAG_SEARCHING));
            }
            catch (System.Exception ex)
            {
#if DEBUG
                CSLibrary.Diagnostics.CoreDebug.Logger.ErrorException("HighLevelInterface.TagSeachOneCallback()", ex);
#endif
                return (int)(Result.SYSTEM_CATCH_EXCEPTION);
            }
        }

        private void TagReadKillPwdThreadProc()
        {
            ushort[] readbuf = new ushort[2];

            try
            {
                FireStateChangedEvent(RFState.BUSY);

                //                m_Result = TagReadKillPwd(m_rdr_opt_parms.TagReadKillPwd);
                m_Result = Result.OK;

                if (CUST_18K6CTagRead(
                    MemoryBank.RESERVED,
                    KILL_PWD_START_OFFSET,
                    TWO_WORD_LEN,
                    readbuf,
                    m_rdr_opt_parms.TagReadKillPwd.accessPassword,
                    m_rdr_opt_parms.TagReadKillPwd.retryCount,
                    SelectFlags.SELECT) == true)
                    m_rdr_opt_parms.TagReadKillPwd.m_password = (uint)(readbuf[0] << 16 | readbuf[1]);
                else
                    m_Result = Result.FAILURE;

            }
            catch (System.Exception ex)
            {
#if DEBUG
                CSLibrary.Diagnostics.CoreDebug.Logger.ErrorException("HighLevelInterface.TagReadKillPwdThreadProc()", ex);
#endif
            }
            finally
            {
                FireAccessCompletedEvent(
                    new OnAccessCompletedEventArgs(
                    m_Result == Result.OK,
                    Bank.KILL_PWD,
                    TagAccess.READ,
                    m_rdr_opt_parms.TagReadKillPwd.password));

                FireStateChangedEvent(RFState.IDLE);
            }
        }

        void Start18K6CRequest(uint tagStopCount, SelectFlags flags)
        {
            // Set up the rest of the HST_INV_CFG register.  First, we have to read its
            // current value
            UInt32 registerValue = 0;

            MacReadRegister(MacRegister.HST_INV_CFG, ref registerValue);
            registerValue &= ~0x0000FFC0U;  // reserver bit 0:5 ~ 16:31

            // TBD - an optimization could be to only write back the register if
            // the value changes

            // Set the tag stop count and enabled flags and then write the register
            // back
            if ((flags & SelectFlags.SELECT) != 0)
            {
                registerValue |= (1 << 14);
            }
            if ((flags &  SelectFlags.DISABLE_INVENTORY) != 0)
            {
                registerValue |= (1 << 15);
            }
            registerValue |= tagStopCount << 6;
            MacWriteRegister(MacRegister.HST_INV_CFG, registerValue);

            // Set the enabled flag in the HST_INV_EPC_MATCH_CFG register properly.  To
            // do so, have to read the register value first.  Then set the bit properly
            // and then write the register value back to the MAC.
            MacReadRegister(MacRegister.HST_INV_EPC_MATCH_CFG, ref registerValue);
            if ((flags &  SelectFlags.POST_MATCH) != 0)
            {
                registerValue |= 0x01;
            }
            else
            {
                registerValue &= ~(uint)0x01; ;
            }
            MacWriteRegister(MacRegister.HST_INV_EPC_MATCH_CFG, registerValue);
        } // Radio::Start18K6CRequest
        
        /*        
                void Start18K6CRequest(uint tagStopCount, UInt32 flags)
                {
                    // Set up the rest of the HST_INV_CFG register.  First, we have to read its
                    // current value
                    UInt32 registerValue = 0;

                    MacReadRegister(MacRegister.HST_INV_CFG, ref registerValue);
                    registerValue &= ~0x0000FFC0U;  // reserver bit 0:5 ~ 16:31

                    // TBD - an optimization could be to only write back the register if
                    // the value changes

                    // Set the tag stop count and enabled flags and then write the register
                    // back
                    if ((flags & SelectFlags.SELECT) != 0)
                    {
                        registerValue |= (1 << 14);
                    }
                    if ((flags & RFID_FLAG_DISABLE_INVENTORY) != 0)
                    {
                        registerValue |= (1 << 15);
                    }
                    registerValue |= tagStopCount << 6;
                    MacWriteRegister(MacRegister.HST_INV_CFG, registerValue);

                    // Set the enabled flag in the HST_INV_EPC_MATCH_CFG register properly.  To
                    // do so, have to read the register value first.  Then set the bit properly
                    // and then write the register value back to the MAC.
                    MacReadRegister(MacRegister.HST_INV_EPC_MATCH_CFG, ref registerValue);
                    if ((flags & RFID_FLAG_PERFORM_POST_MATCH) != 0)
                    {
                        registerValue |= 0x01;
                    }
                    else
                    {
                        registerValue &= ~(uint)0x01;;
                    }
                    MacWriteRegister(MacRegister.HST_INV_EPC_MATCH_CFG, registerValue);
                } // Radio::Start18K6CRequest
        */

    	void Setup18K6CReadRegisters (MemoryBank bank, uint offset, uint count)
	    {
		    // Set up the access bank register
            MacWriteRegister(MacRegister.HST_TAGACC_BANK, (uint)bank);

		    // Set up the access pointer register (tells the offset)
            MacWriteRegister(MacRegister.HST_TAGACC_PTR, offset);

		    // Set up the access count register (i.e., number values to read)
            MacWriteRegister(MacRegister.HST_TAGACC_CNT, count);
	    }

        bool Start18K6CRead(MemoryBank bank, uint offset, uint count, UInt16[] data, uint accessPassword, uint retry, SelectFlags flags)
	    {
            // Perform the common 18K6C tag operation setup
            Start18K6CRequest(retry, flags);

            Setup18K6CReadRegisters(bank, offset, count);

		    // Set up the access password register
            MacWriteRegister(MacRegister.HST_TAGACC_ACCPWD, accessPassword);

		    // Issue the read command
            if (COMM_HostCommand(HST_CMD.READ) != Result.OK || CurrentOperationResult != Result.OK)
                return false;
            
            return true;
	    } //  Radio::Start18K6CRead

        bool CUST_18K6CTagRead (MemoryBank bank, int offset, int count, UInt16 [] data, UInt32 password, UInt32 retry, SelectFlags flags)
        {
            int rdCycle = count / MAX_RD_CNT;
		    int rdReminder = count % MAX_RD_CNT;
		    int index, i;

            retry += 30; // minimum retry 30 times

            for (index = 0; index < rdCycle; index++)
		    {
                for(i = 0; i < retry;i++)
                    if (Start18K6CRead(bank, (uint)(offset + index * MAX_RD_CNT), MAX_RD_CNT, tagreadbuf, password, 1, flags) == true)
                    {
                        if (m_TagAccessStatus == 2)
                        {
                            Array.Copy(tagreadbuf, 0, data, index * MAX_RD_CNT, MAX_RD_CNT);
                            break;
                        }
                    }
                if (i == retry)
		            return false;
		    }

		    if(rdReminder > 0)
		    {
                for (i = 0; i < retry; i++)
                    if (Start18K6CRead(bank, (uint)(offset + index * MAX_RD_CNT), (uint)rdReminder, tagreadbuf, password, 1, flags) == true)
                    {
                        if (m_TagAccessStatus == 2)
                        {
                            Array.Copy(tagreadbuf, 0, data, index * MAX_RD_CNT, rdReminder);
                            break;
                        }
                    }
				if (i == retry)
			        return false;
		    }

		    return true;
        }

        private void TagReadThreadProc()
        {
            try
            {
                FireStateChangedEvent(RFState.BUSY);

                CurrentOperationResult = Result.NO_TAG_FOUND;

                m_rdr_opt_parms.TagRead.m_pData = new UInt16[m_rdr_opt_parms.TagRead.count];

                m_Result = Result.OK;

                if (CUST_18K6CTagRead(
                    m_rdr_opt_parms.TagRead.bank,
                    m_rdr_opt_parms.TagRead.offset,
                    m_rdr_opt_parms.TagRead.count,
                    m_rdr_opt_parms.TagRead.m_pData,
                    m_rdr_opt_parms.TagRead.accessPassword,
                    m_rdr_opt_parms.TagRead.retryCount,
                    SelectFlags.SELECT) != true)
                    m_Result = Result.FAILURE;
            }
            catch (System.Exception ex)
            {
#if DEBUG
                CSLibrary.Diagnostics.CoreDebug.Logger.ErrorException("HighLevelInterface.TagReadUsrMemThreadProc()", ex);
#endif
            }
            finally
            {
                FireAccessCompletedEvent(
                    new OnAccessCompletedEventArgs(
                    m_Result == Result.OK,
                    Bank.SPECIAL,
                    TagAccess.READ,
                    m_rdr_opt_parms.TagRead.pData));

                FireStateChangedEvent(RFState.IDLE);
            }
        }

        private void TagReadAccPwdThreadProc()
        {
            ushort[] readbuf = new ushort[2];

            try
            {
                FireStateChangedEvent(RFState.BUSY);

                m_Result = Result.OK;

                if (CUST_18K6CTagRead(
                    MemoryBank.RESERVED,
                    ACC_PWD_START_OFFSET,
                    TWO_WORD_LEN,
                    readbuf,
                    m_rdr_opt_parms.TagReadAccPwd.accessPassword,
                    m_rdr_opt_parms.TagReadAccPwd.retryCount,
                    SelectFlags.SELECT) == true)
                    m_rdr_opt_parms.TagReadAccPwd.m_password = (uint)(readbuf[0] << 16 | readbuf[1]);
                else
                    m_Result = Result.FAILURE;
            }
            catch (System.Exception ex)
            {
#if DEBUG
                CSLibrary.Diagnostics.CoreDebug.Logger.ErrorException("HighLevelInterface.TagReadAccPwdThreadProc()", ex);
#endif
            }
            finally
            {
                FireAccessCompletedEvent(
                    new OnAccessCompletedEventArgs(
                    m_Result == Result.OK,
                    Bank.ACC_PWD,
                    TagAccess.READ,
                    m_rdr_opt_parms.TagReadAccPwd.password));

                FireStateChangedEvent(RFState.IDLE);
            }
        }

        private void TagReadPCThreadProc()
        {

            ushort[] readbuf = new ushort[1];

            try
            {
                FireStateChangedEvent(RFState.BUSY);

                CurrentOperationResult = Result.NO_TAG_FOUND;

                m_Result = Result.OK;

                if (CUST_18K6CTagRead(
                    MemoryBank.BANK1, 
                    PC_START_OFFSET, 
			        ONE_WORD_LEN,
                    readbuf,
			        m_rdr_opt_parms.TagReadPC.accessPassword, 
			        m_rdr_opt_parms.TagReadPC.retryCount,
			        SelectFlags.SELECT) == true)
                    m_rdr_opt_parms.TagReadPC.m_pc = readbuf[0];
                else
                    m_Result = Result.FAILURE;
            }
            catch (System.Exception ex)
            {
#if DEBUG
                CSLibrary.Diagnostics.CoreDebug.Logger.ErrorException("HighLevelInterface.TagReadPCThreadProc()", ex);
#endif
            }
            finally
            {
                FireAccessCompletedEvent(
                    new OnAccessCompletedEventArgs(
                    m_Result == Result.OK,
                    Bank.PC,
                    TagAccess.READ,
                    m_rdr_opt_parms.TagReadPC.pc));

                FireStateChangedEvent(RFState.IDLE);
            }
        }

        private void TagReadEPCThreadProc()
        {
            try
            {
                FireStateChangedEvent(RFState.BUSY);

                CurrentOperationResult = Result.NO_TAG_FOUND;

                m_Result = Result.OK;

                if (CUST_18K6CTagRead(
                    MemoryBank.EPC,
                    (ushort)(EPC_START_OFFSET + m_rdr_opt_parms.TagReadEPC.offset),
                    m_rdr_opt_parms.TagReadEPC.count,
                    m_rdr_opt_parms.TagReadEPC.m_epc,
                    m_rdr_opt_parms.TagReadEPC.accessPassword,
                    m_rdr_opt_parms.TagReadEPC.retryCount,
                    SelectFlags.SELECT) != true)
                    m_Result = Result.FAILURE;
            }
            catch (System.Exception ex)
            {
#if DEBUG
                CSLibrary.Diagnostics.CoreDebug.Logger.ErrorException("HighLevelInterface.TagReadEPCThreadProc()", ex);
#endif
            }
            finally
            {
                FireAccessCompletedEvent(
                    new OnAccessCompletedEventArgs(
                    m_Result == Result.OK,
                    Bank.EPC,
                    TagAccess.READ,
                    m_rdr_opt_parms.TagReadEPC.epc));

                FireStateChangedEvent(RFState.IDLE);
            }
        }

        private void TagReadTidThreadProc()
        {
            try
            {
                FireStateChangedEvent(RFState.BUSY);

                CurrentOperationResult = Result.NO_TAG_FOUND;

                m_Result = Result.OK;
		        
                if (CUST_18K6CTagRead(
			        MemoryBank.TID,
			        m_rdr_opt_parms.TagReadTid.offset, 
			        m_rdr_opt_parms.TagReadTid.count, 
			        m_rdr_opt_parms.TagReadTid.pData,
			        m_rdr_opt_parms.TagReadTid.accessPassword, 
			        m_rdr_opt_parms.TagReadTid.retryCount,
			        SelectFlags.SELECT) != true)
                    m_Result = Result.FAILURE;
            }
            catch (System.Exception ex)
            {
#if DEBUG
                CSLibrary.Diagnostics.CoreDebug.Logger.ErrorException("HighLevelInterface.TagReadTidThreadProc()", ex);
#endif
            }
            finally
            {
                FireAccessCompletedEvent(
                    new OnAccessCompletedEventArgs(
                    m_Result == Result.OK,
                    Bank.TID,
                    TagAccess.READ,
                    m_rdr_opt_parms.TagReadTid.tid));

                FireStateChangedEvent(RFState.IDLE);
            }
        }

        private void TagReadUsrMemThreadProc()
        {
            try
            {
                FireStateChangedEvent(RFState.BUSY);

                CurrentOperationResult = Result.NO_TAG_FOUND;

                m_rdr_opt_parms.TagReadUser.m_pData = new UInt16[m_rdr_opt_parms.TagReadUser.count];

                m_Result = Result.OK;

                if (CUST_18K6CTagRead(
                    MemoryBank.USER,
                    m_rdr_opt_parms.TagReadUser.offset,
                    m_rdr_opt_parms.TagReadUser.count,
                    m_rdr_opt_parms.TagReadUser.m_pData,
                    m_rdr_opt_parms.TagReadUser.accessPassword,
                    m_rdr_opt_parms.TagReadUser.retryCount,
			        SelectFlags.SELECT) != true)
                    m_Result = Result.FAILURE;
            }
            catch (System.Exception ex)
            {
#if DEBUG
                CSLibrary.Diagnostics.CoreDebug.Logger.ErrorException("HighLevelInterface.TagReadUsrMemThreadProc()", ex);
#endif
            }
            finally
            {
                FireAccessCompletedEvent(
                    new OnAccessCompletedEventArgs(
                    m_Result == Result.OK,
                    Bank.USER,
                    TagAccess.READ,
                    m_rdr_opt_parms.TagReadUser.pData));

                FireStateChangedEvent(RFState.IDLE);
            }
        }

        void Setup18K6CWriteRegisters (MemoryBank WriteBank, uint WriteOffset, uint WriteSize, UInt16[] WriteBuf, uint BufOffset)
        {
            int offset;
            int pcnt = 0;

            // Set up the tag bank register (tells where to write the data)
            MacWriteRegister(MacRegister.HST_TAGACC_BANK, (uint)WriteBank);

            // Set the offset
            //MacWriteRegister(MacRegister.HST_TAGACC_PTR, WriteOffset);
            MacWriteRegister(MacRegister.HST_TAGACC_PTR, 0);

            // Set up the access count register (i.e., number of words to write)
            MacWriteRegister(MacRegister.HST_TAGACC_CNT, WriteSize);

            // Set up the HST_TAGWRDAT_N registers.  Fill up a bank at a time.
            for (UInt32 registerBank = 0; WriteSize > 0; registerBank++)
            {
                uint value = 0;

                // Indicate which bank of tag write registers we are going to fill
                MacWriteRegister(MacRegister.HST_TAGWRDAT_SEL, registerBank);

                MacReadRegister(MacRegister.MAC_ERROR, ref value);

                if (value == HOSTIF_ERR_SELECTORBNDS)
                {
                    MacClearError();
                    return;
                }

                // Write the values to the bank until either the bank is full or we run out of data
                UInt16 registerAddress = (UInt16)MacRegister.HST_TAGWRDAT_0;
                offset = 0;

                while ((WriteSize > 0) && (offset < 16 /*RFID_NUM_TAGWRDAT_REGS_PER_BANK*/))
                {
                    // Set up the register and then write it to the MAC
                    UInt32 registerValue = (uint)(WriteBuf[BufOffset + pcnt] | ((WriteOffset + pcnt) << 16));

                    MacWriteRegister((MacRegister)(registerAddress), registerValue);

                    pcnt++;
                    registerAddress++;
                    offset++;
                    WriteSize--;
                }
            }
        }

        /// <summary>
        /// WriteSize = word count max 256
        /// </summary>
        /// <param name="WriteBank"></param>
        /// <param name="WriteOffset"></param>
        /// <param name="WriteSize"></param>
        /// <param name="WriteBuf"></param>
        /// <param name="BufOffset"></param>
        /// <returns></returns>
        bool Setup18K6CBlockWriteRegisters(MemoryBank WriteBank, uint WriteOffset, uint WriteSize, UInt16[] WriteBuf, uint BufOffset)
        {
            int offset;
            int pcnt = 0;

            if (WriteSize > 256)
                return false;
            
            // Set up the tag bank register (tells where to write the data)
            MacWriteRegister(MacRegister.HST_TAGACC_BANK, (uint)WriteBank);

            // Set the offset
            MacWriteRegister(MacRegister.HST_TAGACC_PTR, WriteOffset);

            // Set up the access count register (i.e., number of words to write)
            MacWriteRegister(MacRegister.HST_TAGACC_CNT, WriteSize);

            // Set up the HST_TAGWRDAT_N registers.  Fill up a bank at a time.
            for (UInt32 registerBank = 0; WriteSize > 0; registerBank++)
            {
                uint value = 0;

                // Indicate which bank of tag write registers we are going to fill
                MacWriteRegister(MacRegister.HST_TAGWRDAT_SEL, registerBank);

                MacReadRegister(MacRegister.MAC_ERROR, ref value);

                if (value == HOSTIF_ERR_SELECTORBNDS)
                {
                    MacClearError();
                    return false;
                }

                // Write the values to the bank until either the bank is full or we run out of data
                UInt16 registerAddress = (UInt16)MacRegister.HST_TAGWRDAT_0;
                offset = 0;

                while (WriteSize > 0 && offset < 16)
                {
                    // Set up the register and then write it to the MAC
                    UInt32 registerValue = (uint)(WriteBuf[BufOffset + pcnt++]) << 24;
                    WriteSize --;
                    
                    if (WriteSize > 0)
                    {
                        registerValue |= (uint)(WriteBuf[BufOffset + pcnt++]) << 16;
                        WriteSize --;
                    }
   
                    if (WriteSize > 0)
                    {
                        registerValue |= (uint)(WriteBuf[BufOffset + pcnt++]) << 8;
                        WriteSize --;
                    }

                    if (WriteSize > 0)
                    {
                        registerValue |= (uint)(WriteBuf[BufOffset + pcnt++]);
                        WriteSize --;
                    }

                    MacWriteRegister((MacRegister)(registerAddress), registerValue);
                    registerAddress++;
                    offset++;
                }
            }

            return true;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="bank"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>            // byte length 
        /// <param name="data"></param>
        /// <param name="password"></param>
        /// <param name="retry"></param>
        /// <param name="flags"></param>
        /// <returns></returns>
        private Result CUST_18K6CTagBlockWrite(
            MemoryBank bank,
            UInt32 offset,
            UInt32 count,
            UInt16[] data,
            UInt32 password,
            UInt32 retry,
            UInt32 writeretry,
            SelectFlags flags
        )
        {
            const int MAX_BLKWR_CNT = 256;

            int index;
            uint wrCycle = (uint)(count / MAX_BLKWR_CNT);
            uint wrReminder = (uint)(count % MAX_BLKWR_CNT);
            Result status;
            UInt32 i;

            MacWriteRegister(MacRegister.HST_TAGACC_DESC_CFG, 0x1ff);
            MacWriteRegister(MacRegister.HST_TAGACC_ACCPWD, password);
            Start18K6CRequest(1, flags);

            MacWriteRegister(MacRegister.HST_TAGACC_DESC_CFG  /*0xA01*/, (writeretry << 1) | 0x01); // Enable write verify and set retry count

            retry ++;

            for (index = 0; index < wrCycle; index++)
            {
                Setup18K6CBlockWriteRegisters(bank, (uint)(offset + index * MAX_BLKWR_CNT), MAX_BLKWR_CNT, data, (uint)(index * MAX_BLKWR_CNT));

                for (i = retry; i > 0; i--)
                {
                    // Issue the write command to the MAC
                    status = COMM_HostCommand(HST_CMD.BLOCKWRITE);

                    if (status != Result.OK)
                        return status;

                    //MacClearError();

                    if (m_TagAccessStatus == 2)
                        break;

                    System.Threading.Thread.Sleep(100);
                }

                if (i == 0)
                    return Result.MAX_RETRY_EXIT;
            }
            if (wrReminder > 0)
            {
                Setup18K6CBlockWriteRegisters(bank, (uint)(offset + index * MAX_BLKWR_CNT), wrReminder, data, (uint)(index * MAX_BLKWR_CNT));

                for (i = retry; i > 0; i--)
                {
                    // Issue the write command to the MAC
                    status = COMM_HostCommand(HST_CMD.BLOCKWRITE);

                    if (status != Result.OK)
                        return status;

                    //MacClearError();

                    if (m_TagAccessStatus == 2)
                        break;

                    System.Threading.Thread.Sleep(100);
                }

                if (i == 0)
                    return Result.MAX_RETRY_EXIT;
            }

            return Result.OK;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="bank"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        /// <param name="data"></param>
        /// <param name="password"></param>
        /// <param name="retry"></param>
        /// <param name="flags"></param>
        /// <returns></returns>
        private Result CUST_18K6CTagWrite(
            MemoryBank bank,
            UInt32 offset,
            UInt32 count,
            UInt16[] data,
            UInt32 password,
            UInt32 retry,
            UInt32 writeretry,
            SelectFlags flags
        )
        {
            int index;
            uint wrCycle = (uint)(count / MAX_WR_CNT);
            uint wrReminder = (uint)(count % MAX_WR_CNT);
            Result status;
            UInt32 i;

            //MacWriteRegister(MacRegister.HST_TAGACC_DESC_CFG, 0x1ff);
            MacWriteRegister(MacRegister.HST_TAGACC_ACCPWD, password);
            Start18K6CRequest(1, flags);

            MacWriteRegister(MacRegister.HST_TAGACC_DESC_CFG  /*0xA01*/, (writeretry << 1) | 0x01); // Enable write verify and set retry count
            //MacWriteRegister(MacRegister.HST_TAGACC_DESC_CFG  /*0xA01*/, 0x1ff); // Enable write verify and set retry count

            if (retry == 0xffffffff)
                retry = 1;
            else
                retry += 1;

            for (index = 0; index < wrCycle; index++)
            {
                Setup18K6CWriteRegisters(bank, (uint)(offset + index * MAX_WR_CNT), MAX_WR_CNT, data, (uint)(index * MAX_WR_CNT));

                for (i = retry; i > 0; i--)
                {
                    // Issue the write command to the MAC
                    status = COMM_HostCommand(HST_CMD.WRITE);

                    if (status != Result.OK)
                        return status;

                    //MacClearError();

                    if (m_TagAccessStatus == 2)
                        break;

                    System.Threading.Thread.Sleep(100);
                }

                if (i == 0)
                    return Result.MAX_RETRY_EXIT;
            }
            if (wrReminder > 0)
            {
                Setup18K6CWriteRegisters(bank, (uint)(offset + index * MAX_WR_CNT), wrReminder, data, (uint)(index * MAX_WR_CNT));

                for (i = retry; i > 0; i--)
                {
                    // Issue the write command to the MAC
                    status = COMM_HostCommand(HST_CMD.WRITE);

                    if (status != Result.OK)
                        return status;

                    //MacClearError();

                    if (m_TagAccessStatus == 2)
                        break;

                    System.Threading.Thread.Sleep(100);
                }

                if (i == 0)
                    return Result.MAX_RETRY_EXIT;
            }

            return Result.OK;
        }

        

        /*
                /// <summary>
                /// 
                /// </summary>
                /// <param name="bank"></param>
                /// <param name="offset"></param>
                /// <param name="count"></param>
                /// <param name="data"></param>
                /// <param name="password"></param>
                /// <param name="retry"></param>
                /// <param name="flags"></param>
                /// <returns></returns>
                private Result CUST_18K6CTagWrite(
                    MemoryBank bank,
                    UInt32 offset,
                    UInt32 count,
                    UInt16[] data,
                    UInt32 password,
                    UInt32 retry,
                    UInt32 flags
                )
                {
                    int index;
                    uint wrCycle = (uint)(count / MAX_WR_CNT);
                    uint wrReminder = (uint)(count % MAX_WR_CNT);
                    Result status;
                    UInt32 i;

                    retry += 30; // minimum retry 30 times

                    for (index = 0; index < wrCycle; index++)
                    {
                        for (i = 0; i < retry; i++)
                        {
                            Start18K6CRequest(1, flags);

                            Setup18K6CWriteRegisters(bank, (uint)(offset + index * MAX_WR_CNT), MAX_WR_CNT, data, (uint)(index * MAX_WR_CNT));

                            MacWriteRegister(MacRegister.HST_TAGACC_ACCPWD, password);

                            // Issue the write command to the MAC
                            status = COMM_HostCommand(HST_CMD.WRITE);

                            if (status != Result.OK)
                                return status;

                            //MacClearError();

                            if (m_TagAccessStatus == 2)
                                break;
                        }

                        if (i == retry)
                            return Result.MAX_RETRY_EXIT;
                    }
                    if (wrReminder > 0)
                    {
                        for (i = 0; i < retry; i++)
                        {
                            Start18K6CRequest(0, flags);

                            Setup18K6CWriteRegisters(bank, (uint)(offset + index * MAX_WR_CNT), wrReminder, data, (uint)(index * MAX_WR_CNT));

                            MacWriteRegister(MacRegister.HST_TAGACC_ACCPWD, password);

                            // Issue the write command to the MAC
                            status = COMM_HostCommand(HST_CMD.WRITE);

                            if (status != Result.OK)
                                return status;

                            //MacClearError();

                            if (m_TagAccessStatus == 2)
                                break;
                        }

                        if (i == retry)
                            return Result.MAX_RETRY_EXIT;
                    }

                    return Result.OK;
                }
        */

#if nouse
        Result CUST_18K6CTagWrite(
            MemoryBank  bank, 
            INT16U      offset, 
            INT16U      count, 
            INT16U[]	data,
		    INT32U		password,
		    INT32U		retry,
		    INT32U		flags
		)
	    {
            int wrCycle = m_rdr_opt_parms.TagWriteEPC.count / MAX_WR_CNT;
            int wrReminder = m_rdr_opt_parms.TagWriteEPC.count % MAX_WR_CNT;
            
            Result status = Result.OK;


            
            
            for(UINT32 i = 0; i <= retry;i++)
		    {
                Setup18K6CWriteRegisters(bank, data, offset, count);

                MacWriteRegister(MacRegister.HST_TAGACC_ACCPWD, password);

		        // Issue the write command to the MAC
                status = COMM_HostCommand(HST_CMD.WRITE);

			    //MacClearError();

			    if(status == Result.OK)
				    return Result.OK;
            }





            for (int index = 0; index < wrCycle; index++)
            {
                Setup18K6CWriteRegisters(bank, data, offset, count);

                
                {
                    if ((status = CUST_18K6CTagWrite(
                        MemoryBank.EPC,
                        EPC_START_OFFSET + m_rdr_opt_parms.TagWriteEPC.offset + index * MAX_WR_CNT,
                        MAX_WR_CNT,
                        &parms->pData[index * MAX_WR_CNT],
                        m_rdr_opt_parms.TagWriteEPC.accessPassword,
                        m_rdr_opt_parms.TagWriteEPC.retryCount,
                        0)) != RFID_STATUS_OK)
                    {
                        return;
                    }
                }
            }
            if (wrReminder > 0)
            {
                Setup18K6CWriteRegisters(bank, data, offset, count);

                {
                    if ((status = CUST_18K6CTagWrite(
                        MemoryBank.EPC,
                        EPC_START_OFFSET + m_rdr_opt_parms.TagWriteEPC.offset + wrCycle * MAX_WR_CNT,
                        wrReminder,
                        &parms->pData[wrCycle * MAX_WR_CNT],
                        m_rdr_opt_parms.TagWriteEPC.accessPassword,
                        m_rdr_opt_parms.TagWriteEPC.retryCount,
                        0)) != RFID_STATUS_OK)
                    {
                        return;
                    }
                }
            }
            
            return  Result.MAX_RETRY_EXIT;
	    }//CUST_18K6CTagWrite
#endif

        private void TagWriteThreadProc()
        {
            try
            {
                FireStateChangedEvent(RFState.BUSY);

                UInt16[] readData = new UInt16[m_rdr_opt_parms.TagWrite.count];
                UInt16[] writeData = m_rdr_opt_parms.TagWrite.pData;
                UInt16[] readCmp = new UInt16[MAX_WR_CNT];
                bool status;

                m_Result = Result.OK;

                /*                if ((status = CUST_18K6CTagRead(
                                    MemoryBank.USER,
                                    m_rdr_opt_parms.TagWriteUser.offset,
                                    m_rdr_opt_parms.TagWriteUser.count,
                                    readData,
                                    m_rdr_opt_parms.TagWriteUser.accessPassword,
                                    m_rdr_opt_parms.TagWriteUser.retryCount,
                                    SelectFlags.SELECT)) != true)
                                {
                                    m_Result = Result.NO_TAG_FOUND;
                                    return;
                                }
                */
                m_Result = CUST_18K6CTagWrite(
                    m_rdr_opt_parms.TagWrite.bank,
                    (uint)(m_rdr_opt_parms.TagWrite.offset),
                    m_rdr_opt_parms.TagWrite.count,
                    writeData,
                    m_rdr_opt_parms.TagWrite.accessPassword,
                    m_rdr_opt_parms.TagWrite.retryCount,
                    m_rdr_opt_parms.TagWrite.writeRetryCount,
                    SelectFlags.SELECT);
            }
            catch (System.Exception ex)
            {
#if DEBUG
                CSLibrary.Diagnostics.CoreDebug.Logger.ErrorException("HighLevelInterface.TagWriteUsrMemThreadProc()", ex);
#endif
            }
            finally
            {
                FireAccessCompletedEvent(
                    new OnAccessCompletedEventArgs(
                    m_Result == Result.OK,
                    Bank.SPECIAL,
                    TagAccess.WRITE,
                    new S_DATA(m_rdr_opt_parms.TagWrite.pData)));

                FireStateChangedEvent(RFState.IDLE);
            }
        }

        private void TagWriteKillPwdThreadProc()
        {
	        UInt16 [] writeData = new UInt16 [2];

            try
            {
                FireStateChangedEvent(RFState.BUSY);

                //m_Result = TagWriteKillPwd(m_rdr_opt_parms.TagWriteKillPwd);

                writeData[0] = (UInt16)(m_rdr_opt_parms.TagWriteKillPwd.password >> 16);
		        writeData[1] = (UInt16)(m_rdr_opt_parms.TagWriteKillPwd.password);

			    m_Result = CUST_18K6CTagWrite(
                    MemoryBank.RESERVED,
				    KILL_PWD_START_OFFSET, 
				    TWO_WORD_LEN, 
				    writeData,
                    m_rdr_opt_parms.TagWriteKillPwd.accessPassword,
                    m_rdr_opt_parms.TagWriteKillPwd.retryCount,
                    m_rdr_opt_parms.TagWriteKillPwd.writeRetryCount,
				    SelectFlags.SELECT);
            }
            catch (System.Exception ex)
            {
#if DEBUG
                CSLibrary.Diagnostics.CoreDebug.Logger.ErrorException("HighLevelInterface.TagWriteKillPwdThreadProc()", ex);
#endif
            }
            finally
            {
                FireAccessCompletedEvent(
                    new OnAccessCompletedEventArgs(
                    m_Result == Result.OK,
                    Bank.KILL_PWD,
                    TagAccess.WRITE,
                    new S_PWD(m_rdr_opt_parms.TagWriteKillPwd.password)));

                FireStateChangedEvent(RFState.IDLE);
            }
        }

        private void TagWriteAccPwdThreadProc()
        {
            UInt16[] writeData = new UInt16 [2];
            
            try
            {
                FireStateChangedEvent(RFState.BUSY);

//                m_Result = TagWriteAccPwd(m_rdr_opt_parms.TagWriteAccPwd);
        		writeData[0] = (ushort)(m_rdr_opt_parms.TagWriteAccPwd.password >> 16);
        		writeData[1] = (ushort)m_rdr_opt_parms.TagWriteAccPwd.password;

                m_Result = CUST_18K6CTagWrite(
				    MemoryBank.RESERVED,
				    ACC_PWD_START_OFFSET, 
				    TWO_WORD_LEN, 
				    writeData,
                    m_rdr_opt_parms.TagWriteAccPwd.accessPassword,
                    m_rdr_opt_parms.TagWriteAccPwd.retryCount,
                    m_rdr_opt_parms.TagWriteAccPwd.writeRetryCount,
				    SelectFlags.SELECT);
            }
            catch (System.Exception ex)
            {
#if DEBUG
                CSLibrary.Diagnostics.CoreDebug.Logger.ErrorException("HighLevelInterface.TagWriteAccPwdThreadProc()", ex);
#endif
            }
            finally
            {
                FireAccessCompletedEvent(
                    new OnAccessCompletedEventArgs(
                    m_Result == Result.OK,
                    Bank.ACC_PWD,
                    TagAccess.WRITE,
                    new S_PWD(m_rdr_opt_parms.TagWriteAccPwd.password)));

                FireStateChangedEvent(RFState.IDLE);
            }
        }

        private void TagWritePCThreadProc()
        {
            UInt16[] readData = new UInt16[1];
            UInt16[] writeData = new UInt16[1];

            try
            {
                FireStateChangedEvent(RFState.BUSY);

//                m_Result = TagWritePC(m_rdr_opt_parms.TagWritePC);

                writeData[0] = m_rdr_opt_parms.TagWritePC.pc;

/*
                if (CUST_18K6CTagRead(
                    MemoryBank.EPC,
                    PC_START_OFFSET,
                    ONE_WORD_LEN,
                    readData,
                    m_rdr_opt_parms.TagWritePC.accessPassword,
                    m_rdr_opt_parms.TagWritePC.retryCount,
                    SelectFlags.SELECT) != true)
                {
                    m_Result = Result.FAILURE;
                    return;
                }

                if (readData[0] == writeData[0])
                    return;

                System.Threading.Thread.Sleep(100);
*/
                m_Result = CUST_18K6CTagWrite(
                    MemoryBank.EPC,
                    PC_START_OFFSET,
                    ONE_WORD_LEN,
                    writeData,
                    m_rdr_opt_parms.TagWritePC.accessPassword,
                    m_rdr_opt_parms.TagWritePC.retryCount,
                    m_rdr_opt_parms.TagWritePC.writeRetryCount,
                    SelectFlags.SELECT);
            }
            catch (System.Exception ex)
            {
#if DEBUG
                CSLibrary.Diagnostics.CoreDebug.Logger.ErrorException("HighLevelInterface.TagWritePCThreadProc()", ex);
#endif
            }
            finally
            {
                FireAccessCompletedEvent(
                    new OnAccessCompletedEventArgs(
                    m_Result == Result.OK,
                    Bank.PC,
                    TagAccess.WRITE,
                    new S_PC(m_rdr_opt_parms.TagWritePC.pc)));

                FireStateChangedEvent(RFState.IDLE);
            }
        }

        private void TagWriteEPCThreadProc()
        {
            try
            {
                FireStateChangedEvent(RFState.BUSY);

                UInt16[] readData = new UInt16[m_rdr_opt_parms.TagWriteEPC.count];
                UInt16[] writeData = m_rdr_opt_parms.TagWriteEPC.epc.ToUshorts ();
                UInt16[] readCmp = new UInt16[MAX_WR_CNT];
                bool status;

                m_Result = Result.OK;

                /*		        if((status = CUST_18K6CTagRead(
                                    MemoryBank.EPC,
                                    EPC_START_OFFSET + m_rdr_opt_parms.TagWriteEPC.offset,
                                    m_rdr_opt_parms.TagWriteEPC.count, 
                                    readData,
                                    m_rdr_opt_parms.TagWriteEPC.accessPassword,
                                    m_rdr_opt_parms.TagWriteEPC.retryCount,
                                    SelectFlags.SELECT)) != true)
                                {
                                    m_Result = Result.NO_TAG_FOUND;
                                    return;
                                }

                                if (Win32.memcmp(readData, writeData, m_rdr_opt_parms.TagWriteEPC.count) == 0)
                                {
                                    return;
                                }
                                System.Threading.Thread.Sleep(100);
                */

                m_Result = CUST_18K6CTagWrite(
                    MemoryBank.EPC,
                    (uint)(EPC_START_OFFSET + m_rdr_opt_parms.TagWriteEPC.offset),
                    m_rdr_opt_parms.TagWriteEPC.count,
                    writeData,
                    m_rdr_opt_parms.TagWriteEPC.accessPassword,
                    m_rdr_opt_parms.TagWriteEPC.retryCount,
                    m_rdr_opt_parms.TagWriteEPC.writeRetryCount,
                    SelectFlags.SELECT);
            }
            catch (System.Exception ex)
            {
#if DEBUG
                CSLibrary.Diagnostics.CoreDebug.Logger.ErrorException("HighLevelInterface.TagWriteEPCThreadProc()", ex);
#endif
            }
            finally
            {
                FireAccessCompletedEvent(
                    new OnAccessCompletedEventArgs(
                    m_Result == Result.OK,
                    Bank.EPC,
                    TagAccess.WRITE,
                    m_rdr_opt_parms.TagWriteEPC.epc));

                FireStateChangedEvent(RFState.IDLE);
            }
        }

#if nouse
        private bool CUST_18K6CTagRead(MemoryBank memoryBank, int p, ushort p_3, ushort[] readData, uint p_5, uint p_6, uint SelectFlags.SELECT)
        {
            throw new NotImplementedException();
        }

        private object CUST_18K6CTagRead(MemoryBank memoryBank, int p, ushort p_3, ushort[] readData, uint p_5, uint p_6, uint SelectFlags.SELECT)
        {
            throw new NotImplementedException();
        }
#endif

        private void TagWriteUsrMemThreadProc()
        {
           try
            {
                FireStateChangedEvent(RFState.BUSY);

                UInt16[] readData = new UInt16[m_rdr_opt_parms.TagWriteUser.count];
                UInt16[] writeData = m_rdr_opt_parms.TagWriteUser.pData;
                UInt16[] readCmp = new UInt16[MAX_WR_CNT];
                bool status;

                m_Result = Result.OK;

/*                if ((status = CUST_18K6CTagRead(
                    MemoryBank.USER,
                    m_rdr_opt_parms.TagWriteUser.offset,
                    m_rdr_opt_parms.TagWriteUser.count,
                    readData,
                    m_rdr_opt_parms.TagWriteUser.accessPassword,
                    m_rdr_opt_parms.TagWriteUser.retryCount,
                    SelectFlags.SELECT)) != true)
                {
                    m_Result = Result.NO_TAG_FOUND;
                    return;
                }
*/
                m_Result = CUST_18K6CTagWrite(
                    MemoryBank.USER,
                    (uint)(m_rdr_opt_parms.TagWriteUser.offset),
                    m_rdr_opt_parms.TagWriteUser.count,
                    writeData,
                    m_rdr_opt_parms.TagWriteUser.accessPassword,
                    m_rdr_opt_parms.TagWriteUser.retryCount,
                    m_rdr_opt_parms.TagWriteUser.writeRetryCount,
                    SelectFlags.SELECT);
            }
            catch (System.Exception ex)
            {
#if DEBUG
                CSLibrary.Diagnostics.CoreDebug.Logger.ErrorException("HighLevelInterface.TagWriteUsrMemThreadProc()", ex);
#endif
            }
            finally
            {
                FireAccessCompletedEvent(
                    new OnAccessCompletedEventArgs(
                    m_Result == Result.OK,
                    Bank.USER,
                    TagAccess.WRITE,
                    new S_DATA(m_rdr_opt_parms.TagWriteUser.pData)));

                FireStateChangedEvent(RFState.IDLE);
            }
        }

        private void BlockWriteThreadProc()
        {
            if (m_rdr_opt_parms.TagBlockWrite.retryCount > 31)
            {
                m_Result = Constants.Result.INVALID_PARAMETER;
                return;
            }

            if (m_rdr_opt_parms.TagBlockWrite.count > 255)
            {
                m_Result = Constants.Result.INVALID_PARAMETER;
                return;
            }

            try
            {
                FireStateChangedEvent(CSLibrary.Constants.RFState.BUSY);

                m_Result = CSLibrary.Constants.Result.FAILURE;

                MacWriteRegister(MacRegister.HST_TAGACC_ACCPWD, m_rdr_opt_parms.TagBlockWrite.accessPassword);
                Start18K6CRequest(1, m_rdr_opt_parms.TagBlockWrite.flags);
                MacWriteRegister(MacRegister.HST_TAGACC_DESC_CFG, 0x01 | (m_rdr_opt_parms.TagBlockWrite.retryCount << 1)); // Enable write verify and set retry count

                // Set up the tag bank register (tells where to write the data)
                MacWriteRegister(MacRegister.HST_TAGACC_BANK, (uint)m_rdr_opt_parms.TagBlockWrite.bank);

                // Set the offset
                MacWriteRegister(MacRegister.HST_TAGACC_PTR, m_rdr_opt_parms.TagBlockWrite.offset);

                // Set up the access count register (i.e., number of words to write)
                MacWriteRegister(MacRegister.HST_TAGACC_CNT, m_rdr_opt_parms.TagBlockWrite.count);

                ushort DataSize = m_rdr_opt_parms.TagBlockWrite.count;
                ushort WriteSize = DataSize;
                // Write the values to the bank until either the bank is full or we run out of data
                ushort registerAddress = (UInt16)MacRegister.HST_TAGWRDAT_0;
                UInt32 registerBank = 0;
                int pcnt = 0;

                // Set up the HST_TAGWRDAT_N registers.  Fill up a bank at a time.
                for (registerBank = 0; DataSize > 0; registerBank++)
                {
                    // Indicate which bank of tag write registers we are going to fill
                    MacWriteRegister(MacRegister.HST_TAGWRDAT_SEL, registerBank);

                    // Write the values to the bank until either the bank is full or we run out of data
                    registerAddress = (UInt16)MacRegister.HST_TAGWRDAT_0;

                    //Debug.WriteLine("1. datasize:" + DataSize + " writesieze:" + WriteSize);
                    if (DataSize >= 32)
                    {
                        WriteSize = 32;
                        DataSize -= 32;
                    }
                    else
                    {
                        WriteSize = DataSize;
                        DataSize = 0;
                    }
                    //Debug.WriteLine("2. datasize:" + DataSize + " writesieze:" + WriteSize);

                    while (WriteSize > 1)
                    {
                        // Set up the register and then write it to the MAC
                        UInt32 registerValue = (UInt32)((UInt32)m_rdr_opt_parms.TagBlockWrite.pData[pcnt + 1] | (((UInt32)m_rdr_opt_parms.TagBlockWrite.pData[pcnt]) << 16));

                        MacWriteRegister((MacRegister)(registerAddress), registerValue);

                        pcnt += 2;
                        registerAddress++;
                        WriteSize -= 2;
                    }

                    if (WriteSize == 1)
                    {
                        // Set up the register and then write it to the MAC
                        UInt32 registerValue = (uint)((UInt32)m_rdr_opt_parms.TagBlockWrite.pData[pcnt] << 16);

                        MacWriteRegister((MacRegister)(registerAddress), registerValue);
                    }
                }

                m_Result = COMM_HostCommand(HST_CMD.BLOCKWRITE);

                if (m_TagAccessStatus != 2)
                    m_Result = Result.FAILURE;

                //return m_Result;

                //_deviceHandler.SendAsync(0, 0, DOWNLINKCMD.RFIDCMD, PacketData(0xf000, (UInt32)HST_CMD.BLOCKWRITE), HighLevelInterface.BTWAITCOMMANDRESPONSETYPE.WAIT_BTAPIRESPONSE_COMMANDENDRESPONSE, (UInt32)CurrentOperation);

                //m_Result = CSLibrary.Constants.Result.OK;
            }
            catch (System.Exception ex)
            {
            }
            finally
            {
                FireAccessCompletedEvent(
                    new OnAccessCompletedEventArgs(
                    m_Result == Result.OK,
                    Bank.SPECIAL,
                    TagAccess.WRITE,
                    new S_DATA(m_rdr_opt_parms.TagBlockWrite.pData)));

                FireStateChangedEvent(RFState.IDLE);
            }
        }
        
        const UInt32 RFID_NUM_TAGWRDAT_REGS_PER_BANK = 16;

        private void TagBlockLockThreadProc()
        {
            try
            {
                FireStateChangedEvent(RFState.BUSY);

                for (UInt32 i = 0; i <= Options.TagBlockLock.retryCount; i++)
                {
                    // Perform the common 18K6C tag operation setup
                    //this->Start18K6CRequest(&pBWParms->common, flags);
                    Start18K6CRequest(1, Options.TagBlockLock.flags);

                    // Set the tag access descriptor to the first one just to be safe
                    MacWriteRegister(MacRegister.HST_TAGACC_DESC_SEL, 0);

                    // Set the tag write data select register to zero
                    MacWriteRegister(MacRegister.HST_TAGWRDAT_SEL, 0x0000);

                    // Set up the HST_TAGACC_DESC_CFG register (controls the verify and retry
                    // count) and write it to the MAC
                    /*INT32U  registerValue = 
                    (pBWParms->verify ? HST_TAGACC_DESC_CFG_VERIFY_ENABLED :
                    HST_TAGACC_DESC_CFG_VERIFY_DISABLED)  |
                    HST_TAGACC_DESC_CFG_RETRY(pBWParms->verifyRetryCount)     | 
                    HST_TAGACC_DESC_CFG_RFU1(0);
                    m_pMac->WriteRegister(HST_TAGACC_DESC_CFG, registerValue);*/

                    //INT16U count = pBWParms->permalockCmdParms.count;
                    //INT16U offset = pBWParms->permalockCmdParms.offset;
                    //BOOL32 readOrLock = pBWParms->permalockCmdParms.readOrLock;
                    //const INT16U* pData = pBWParms->permalockCmdParms.pData;

                    // Set up the tag bank register (tells where to write the data)
                    MacWriteRegister(MacRegister.HST_TAGACC_BANK,0x03);

                    //Set up the access offset register (i.e., number of words to lock)
                    MacWriteRegister(MacRegister.HST_TAGACC_PTR, Options.TagBlockLock.offset);

                    // Set up the access count register (i.e., number of words to lock)
                    MacWriteRegister(MacRegister.HST_TAGACC_CNT, Options.TagBlockLock.count);

                    // Set up the tag access password
                    MacWriteRegister(MacRegister.HST_TAGACC_ACCPWD, Options.TagBlockLock.accessPassword);

                    MacWriteRegister(MacRegister.HST_TAGACC_LOCKCFG, (Options.TagBlockLock.setPermalock ? (1U << 20) : 0x0000U));

                    UInt16 count = 0;
                    UInt16 offset = Options.TagBlockLock.offset;

                    if (Options.TagBlockLock.setPermalock)
                    {
                        // Set up the HST_TAGWRDAT_N registers.  Fill up a bank at a time.
                        for (UInt32 registerBank = 0; count < Options.TagBlockLock.count; ++registerBank)
                        {
                            // Indicate which bank of tag write registers we are going to fill
                            MacWriteRegister(MacRegister.HST_TAGWRDAT_SEL, registerBank);

                            /*
                            if (HOSTIF_ERR_SELECTORBNDS == MacReadRegister(MAC_ERROR))
                            {
                                this->ClearMacError();
                                throw RfidErrorException(RFID_ERROR_INVALID_PARAMETER);
                            }
                            */

                            // Write the values to the bank until either the bank is full or we get to
                            // a point where we cannot fill a register (i.e., we have 0 or 1 words left)
                            offset = 0;
                            for (; (offset < RFID_NUM_TAGWRDAT_REGS_PER_BANK) && (count < (Options.TagBlockLock.count - 1)); ++offset)
                            {
                                MacWriteRegister((MacRegister)((int)MacRegister.HST_TAGWRDAT_0 + offset), (uint)((Options.TagBlockLock.mask[count] << 16) | Options.TagBlockLock.mask[count + 1]));
                                count += 2;
                            }

                            // If we didn't use all registers in the bank and count is non-zero, it means
                            // that the request was for an odd number of words to be written.  Make sure
                            // that the last word is written.
                            if ((offset < RFID_NUM_TAGWRDAT_REGS_PER_BANK) && (count < Options.TagBlockLock.count))
                            {
                                MacWriteRegister((MacRegister)((int)MacRegister.HST_TAGWRDAT_0 + offset), (uint)((Options.TagBlockLock.mask[count] << 16)));
                                //MacWriteRegister(MacRegister.HST_TAGWRDAT_0 + offset, HST_TAGWRDAT_N_DATA0(*pData) | HST_TAGWRDAT_N_DATA1(0));
                                break;
                            }
                        }
                    }

                    // Issue the write command to the MAC
                    m_Result = COMM_HostCommand(HST_CMD.BLOCKPERMALOCK);

                    if (m_Result == Result.OK)
                        break;
                }

                if (m_Result == Result.OK && !Options.TagBlockLock.setPermalock)
                {
                    Options.TagBlockLock.mask = new ushort[Options.TagBlockLock.count];
                    Array.Copy(tagreadbuf, Options.TagBlockLock.mask, Options.TagBlockLock.count);
                }
            }
            catch (System.Exception ex)
            {
                DEBUGT_WriteLine(DEBUGLEVEL.API, "HighLevelInterface.TagBlockLockThreadProc()\n" + ex.Message);
                //CSLibrary.Diagnostics.CoreDebug.Logger.ErrorException("HighLevelInterface.TagBlockLockThreadProc()", ex);
            }
            finally
            {
                FireAccessCompletedEvent(
                    new OnAccessCompletedEventArgs(
                    m_Result == Result.OK,
                    Bank.USER,
                    TagAccess.LOCK,
                    new S_DATA(m_rdr_opt_parms.TagBlockLock.mask)));

                FireStateChangedEvent(RFState.IDLE);
            }
        }

        private void TagLockThreadProc()
        {
            const uint HST_TAGACC_LOCKCFG_MASK_USE_PWD_ACTION = 0x1;
            const uint HST_TAGACC_LOCKCFG_MASK_USE_PERMA_ACTION = 0x2;

            /* HST_TAGACC_LOCKCFG register helper macros                                */
            /* The size of the bit fields in the HST_TAGACC_LOCKCFG register.           */
            const byte HST_TAGACC_LOCKCFG_ACTION_USER_SIZE = 2;
            const byte HST_TAGACC_LOCKCFG_ACTION_TID_SIZE = 2;
            const byte HST_TAGACC_LOCKCFG_ACTION_EPC_SIZE = 2;
            const byte HST_TAGACC_LOCKCFG_ACTION_ACC_SIZE = 2;
            const byte HST_TAGACC_LOCKCFG_ACTION_KILL_SIZE = 2;
            const byte HST_TAGACC_LOCKCFG_MASK_USER_SIZE = 2;
            const byte HST_TAGACC_LOCKCFG_MASK_TID_SIZE = 2;
            const byte HST_TAGACC_LOCKCFG_MASK_EPC_SIZE = 2;
            const byte HST_TAGACC_LOCKCFG_MASK_ACC_SIZE = 2;
            const byte HST_TAGACC_LOCKCFG_MASK_KILL_SIZE = 2;
            const byte HST_TAGACC_LOCKCFG_RFU1_SIZE = 12;

            const byte HST_TAGACC_LOCKCFG_ACTION_USER_SHIFT = 0;
            const byte HST_TAGACC_LOCKCFG_ACTION_TID_SHIFT = (HST_TAGACC_LOCKCFG_ACTION_USER_SHIFT + HST_TAGACC_LOCKCFG_ACTION_USER_SIZE);
            const byte HST_TAGACC_LOCKCFG_ACTION_EPC_SHIFT = (HST_TAGACC_LOCKCFG_ACTION_TID_SHIFT + HST_TAGACC_LOCKCFG_ACTION_TID_SIZE);
            const byte HST_TAGACC_LOCKCFG_ACTION_ACC_SHIFT = (HST_TAGACC_LOCKCFG_ACTION_EPC_SHIFT + HST_TAGACC_LOCKCFG_ACTION_EPC_SIZE);
            const byte HST_TAGACC_LOCKCFG_ACTION_KILL_SHIFT = (HST_TAGACC_LOCKCFG_ACTION_ACC_SHIFT + HST_TAGACC_LOCKCFG_ACTION_ACC_SIZE);
            const byte HST_TAGACC_LOCKCFG_MASK_USER_SHIFT = (HST_TAGACC_LOCKCFG_ACTION_KILL_SHIFT + HST_TAGACC_LOCKCFG_ACTION_KILL_SIZE);
            const byte HST_TAGACC_LOCKCFG_MASK_TID_SHIFT = (HST_TAGACC_LOCKCFG_MASK_USER_SHIFT + HST_TAGACC_LOCKCFG_MASK_USER_SIZE);
            const byte HST_TAGACC_LOCKCFG_MASK_EPC_SHIFT = (HST_TAGACC_LOCKCFG_MASK_TID_SHIFT + HST_TAGACC_LOCKCFG_MASK_TID_SIZE);
            const byte HST_TAGACC_LOCKCFG_MASK_ACC_SHIFT = (HST_TAGACC_LOCKCFG_MASK_EPC_SHIFT + HST_TAGACC_LOCKCFG_MASK_EPC_SIZE);
            const byte HST_TAGACC_LOCKCFG_MASK_KILL_SHIFT = (HST_TAGACC_LOCKCFG_MASK_ACC_SHIFT + HST_TAGACC_LOCKCFG_MASK_ACC_SIZE);
            const byte HST_TAGACC_LOCKCFG_RFU1_SHIFT = (HST_TAGACC_LOCKCFG_MASK_KILL_SHIFT + HST_TAGACC_LOCKCFG_MASK_KILL_SIZE);
            
            /* Constants for HST_TAGACC_LOCKCFG register bit fields (note that the      */
            /* values are already shifted into the low-order bits of the constant.      */
            const uint HST_TAGACC_LOCKCFG_ACTION_MEM_WRITE         = 0x0;
            const uint HST_TAGACC_LOCKCFG_ACTION_MEM_PERM_WRITE    = 0x1;
            const uint HST_TAGACC_LOCKCFG_ACTION_MEM_SEC_WRITE     = 0x2;
            const uint HST_TAGACC_LOCKCFG_ACTION_MEM_NO_WRITE      = 0x3;
            const uint HST_TAGACC_LOCKCFG_ACTION_PWD_ACCESS        = 0x0;
            const uint HST_TAGACC_LOCKCFG_ACTION_PWD_PERM_ACCESS   = 0x1;
            const uint HST_TAGACC_LOCKCFG_ACTION_PWD_SEC_ACCESS    = 0x2;
            const uint HST_TAGACC_LOCKCFG_ACTION_PWD_NO_ACCESS     = 0x3;
            const uint HST_TAGACC_LOCKCFG_MASK_USE_NO_ACTION       = 0x0;

            const uint HST_TAGACC_LOCKCFG_MASK_USE_BOTH_ACTION = (HST_TAGACC_LOCKCFG_MASK_USE_PWD_ACTION | HST_TAGACC_LOCKCFG_MASK_USE_PERMA_ACTION);

            const uint RFID_18K6C_TAG_PWD_PERM_ACCESSIBLE = 0x0;
            const uint RFID_18K6C_TAG_PWD_PERM_ALWAYS_NOT_ACCESSIBLE = 0x1;
            const uint RFID_18K6C_TAG_PWD_PERM_ALWAYS_ACCESSIBLE = 0x2;
            const uint RFID_18K6C_TAG_PWD_PERM_SECURED_ACCESSIBLE = 0x3;
            const uint RFID_18K6C_TAG_PWD_PERM_NO_CHANGE = 0x4;

            const uint RFID_18K6C_TAG_MEM_PERM_WRITEABLE = 0x0;				//unlock		00
            const uint RFID_18K6C_TAG_MEM_PERM_ALWAYS_NOT_WRITEABLE = 0x1;	//permlock		01
            const uint RFID_18K6C_TAG_MEM_PERM_ALWAYS_WRITEABLE = 0x2;		//permunlock	10
            const uint RFID_18K6C_TAG_MEM_PERM_SECURED_WRITEABLE = 0x3;		//lock			11
            const uint RFID_18K6C_TAG_MEM_PERM_NO_CHANGE = 0x4;

            try
            {
                FireStateChangedEvent(RFState.BUSY);


                m_Result = Result.FAILURE;

                //TagLockParms parms = new TagLockParms();

/*              parms.accessPassword = m_rdr_opt_parms.TagLock.accessPassword;
                parms.retryCount = 15;
                parms.accessPasswordPermissions = m_rdr_opt_parms.TagLock.accessPasswordPermissions;
                parms.epcMemoryBankPermissions = m_rdr_opt_parms.TagLock.epcMemoryBankPermissions;
                parms.killPasswordPermissions = m_rdr_opt_parms.TagLock.killPasswordPermissions;
                parms.tidMemoryBankPermissions = m_rdr_opt_parms.TagLock.tidMemoryBankPermissions;
                parms.userMemoryBankPermissions = m_rdr_opt_parms.TagLock.userMemoryBankPermissions;
                parms.flags = m_rdr_opt_parms.TagLock.flags;
*/


/* HST_TAGACC_LOCKCFG register value modification macros (i.e., will modify */
/* the field specified within an already existing register value).          */
/* rv - register value that will have field set                             */
/* va - value to set the bit field to                                       */
/*
#define HST_TAGACC_LOCKCFG_SET_ACTION_USER(rv, va)                          \
    REGISTER_SET_VALUE(rv,                                                  \
                       va,                                                  \
                       HST_TAGACC_LOCKCFG_ACTION_USER_SIZE,                 \
                       HST_TAGACC_LOCKCFG_ACTION_USER_SHIFT)

#define HST_TAGACC_LOCKCFG_SET_ACTION_TID(rv, va)                           \
    REGISTER_SET_VALUE(rv,                                                  \
                       va,                                                  \
                       HST_TAGACC_LOCKCFG_ACTION_TID_SIZE,                  \
                       HST_TAGACC_LOCKCFG_ACTION_TID_SHIFT)

#define HST_TAGACC_LOCKCFG_SET_ACTION_EPC(rv, va)                           \
    REGISTER_SET_VALUE(rv,                                                  \
                       va,                                                  \
                       HST_TAGACC_LOCKCFG_ACTION_EPC_SIZE,                  \
                       HST_TAGACC_LOCKCFG_ACTION_EPC_SHIFT)

#define HST_TAGACC_LOCKCFG_SET_ACTION_ACC(rv, va)                           \
    REGISTER_SET_VALUE(rv,                                                  \
                       va,                                                  \
                       HST_TAGACC_LOCKCFG_ACTION_ACC_SIZE,                  \
                       HST_TAGACC_LOCKCFG_ACTION_ACC_SHIFT)

#define HST_TAGACC_LOCKCFG_SET_ACTION_KILL(rv, va)                          \
    REGISTER_SET_VALUE(rv,                                                  \
                       va,                                                  \
                       HST_TAGACC_LOCKCFG_ACTION_KILL_SIZE,                 \
                       HST_TAGACC_LOCKCFG_ACTION_KILL_SHIFT)

#define HST_TAGACC_LOCKCFG_SET_MASK_USER(rv, va)                            \
    REGISTER_SET_VALUE(rv,                                                  \
                       va,                                                  \
                       HST_TAGACC_LOCKCFG_MASK_USER_SIZE,                   \
                       HST_TAGACC_LOCKCFG_MASK_USER_SHIFT)

#define HST_TAGACC_LOCKCFG_SET_MASK_TID(rv, va)                             \
    REGISTER_SET_VALUE(rv,                                                  \
                       va,                                                  \
                       HST_TAGACC_LOCKCFG_MASK_TID_SIZE,                    \
                       HST_TAGACC_LOCKCFG_MASK_TID_SHIFT)

#define HST_TAGACC_LOCKCFG_SET_MASK_EPC(rv, va)                             \
    REGISTER_SET_VALUE(rv,                                                  \
                       va,                                                  \
                       HST_TAGACC_LOCKCFG_MASK_EPC_SIZE,                    \
                       HST_TAGACC_LOCKCFG_MASK_EPC_SHIFT)

#define HST_TAGACC_LOCKCFG_SET_MASK_ACC(rv, va)                             \
    REGISTER_SET_VALUE(rv,                                                  \
                       va,                                                  \
                       HST_TAGACC_LOCKCFG_MASK_ACC_SIZE,                    \
                       HST_TAGACC_LOCKCFG_MASK_ACC_SHIFT)

#define HST_TAGACC_LOCKCFG_SET_MASK_KILL(rv, va)                            \
    REGISTER_SET_VALUE(rv,                                                  \
                       va,                                                  \
                       HST_TAGACC_LOCKCFG_MASK_KILL_SIZE,                   \
                       HST_TAGACC_LOCKCFG_MASK_KILL_SHIFT)

#define HST_TAGACC_LOCKCFG_SET_RFU1(rv, va)                                 \
    REGISTER_SET_VALUE(rv,                                                  \
                       va,                                                  \
                       HST_TAGACC_LOCKCFG_RFU1_SIZE,                        \
                       HST_TAGACC_LOCKCFG_RFU1_SHIFT)

#define HST_TAGACC_BLOCKLOCKCFG_SET_RL(rv, va)                                 \
    REGISTER_SET_VALUE(rv,                                                  \
                       va,                                                  \
                       HST_TAGACC_LOCKCFG_ACTION_USER_SIZE,                        \
                       HST_TAGACC_LOCKCFG_RFU1_SHIFT)
*/

                //                m_Result = TagRawLock(parms);
		        UInt32 registerValue = 0;

		        // Perform the common 18K6C tag operation setup
                //Start18K6CRequest(&pParms->common, flags);
                Start18K6CRequest(1, m_rdr_opt_parms.TagLock.flags);
                
                if (RFID_18K6C_TAG_PWD_PERM_NO_CHANGE == (uint)m_rdr_opt_parms.TagLock.killPasswordPermissions)
                {
//                    HST_TAGACC_LOCKCFG_SET_MASK_KILL(registerValue, HST_TAGACC_LOCKCFG_MASK_USE_NO_ACTION);
                    registerValue |= (HST_TAGACC_LOCKCFG_MASK_USE_NO_ACTION  << HST_TAGACC_LOCKCFG_MASK_KILL_SHIFT);
                }
                // Otherwise, indicate to look at the kill password bits and set the
                // persmission for it
                else
                {
//                    HST_TAGACC_LOCKCFG_SET_MASK_KILL(registerValue, HST_TAGACC_LOCKCFG_MASK_USE_BOTH_ACTION);
//                    HST_TAGACC_LOCKCFG_SET_ACTION_KILL(registerValue, m_rdr_opt_parms.TagLock.killPasswordPermissions);
                    registerValue |= (HST_TAGACC_LOCKCFG_MASK_USE_BOTH_ACTION << HST_TAGACC_LOCKCFG_MASK_KILL_SHIFT);
                    registerValue |= ((uint)m_rdr_opt_parms.TagLock.killPasswordPermissions << HST_TAGACC_LOCKCFG_ACTION_KILL_SHIFT);
                }

                // If the access password access permissions are not to change, then
                // indicate to ignore those bits.
                if (RFID_18K6C_TAG_PWD_PERM_NO_CHANGE == (uint)m_rdr_opt_parms.TagLock.accessPasswordPermissions)
                {
//                    HST_TAGACC_LOCKCFG_SET_MASK_ACC(registerValue, HST_TAGACC_LOCKCFG_MASK_USE_NO_ACTION);
                    registerValue |= HST_TAGACC_LOCKCFG_MASK_USE_NO_ACTION << HST_TAGACC_LOCKCFG_MASK_ACC_SHIFT;
                }
                // Otherwise, indicate to look at the access password bits and set the
                // persmission for it
                else
                {
                    //HST_TAGACC_LOCKCFG_SET_MASK_ACC(registerValue, HST_TAGACC_LOCKCFG_MASK_USE_BOTH_ACTION);
                    //HST_TAGACC_LOCKCFG_SET_ACTION_ACC(registerValue, m_rdr_opt_parms.TagLock.killPasswordPermissions);
                    registerValue |= HST_TAGACC_LOCKCFG_MASK_USE_BOTH_ACTION << HST_TAGACC_LOCKCFG_MASK_ACC_SHIFT;
                    registerValue |= (uint)m_rdr_opt_parms.TagLock.accessPasswordPermissions << HST_TAGACC_LOCKCFG_ACTION_ACC_SHIFT;
                }

                // If the EPC memory access permissions are not to change, then indicate
                // to ignore those bits.
                if (RFID_18K6C_TAG_MEM_PERM_NO_CHANGE == (uint)m_rdr_opt_parms.TagLock.epcMemoryBankPermissions)
                {
                    //HST_TAGACC_LOCKCFG_SET_MASK_EPC(registerValue, HST_TAGACC_LOCKCFG_MASK_USE_NO_ACTION);
                    registerValue |= HST_TAGACC_LOCKCFG_MASK_USE_NO_ACTION << HST_TAGACC_LOCKCFG_MASK_EPC_SHIFT;
                }
                // Otherwise, indicate to look at the EPC memory bits and set the
                // persmission for it
                else
                {
                    //HST_TAGACC_LOCKCFG_SET_MASK_EPC(registerValue, HST_TAGACC_LOCKCFG_MASK_USE_BOTH_ACTION);
                    //HST_TAGACC_LOCKCFG_SET_ACTION_EPC(registerValue, m_rdr_opt_parms.TagLock.epcMemoryBankPermissions);
                    registerValue |= HST_TAGACC_LOCKCFG_MASK_USE_BOTH_ACTION << HST_TAGACC_LOCKCFG_MASK_EPC_SHIFT;
                    registerValue |= (uint)m_rdr_opt_parms.TagLock.epcMemoryBankPermissions << HST_TAGACC_LOCKCFG_ACTION_EPC_SHIFT;
                }

                // If the TID memory access permissions are not to change, then indicate
                // to ignore those bits.
                if (RFID_18K6C_TAG_MEM_PERM_NO_CHANGE == (uint)m_rdr_opt_parms.TagLock.tidMemoryBankPermissions)
                {
                    //HST_TAGACC_LOCKCFG_SET_MASK_TID(registerValue, HST_TAGACC_LOCKCFG_MASK_USE_NO_ACTION);
                    registerValue |= HST_TAGACC_LOCKCFG_MASK_USE_NO_ACTION << HST_TAGACC_LOCKCFG_MASK_TID_SHIFT;
                }
                // Otherwise, indicate to look at the TID memory bits and set the
                // persmission for it
                else
                {
                    //HST_TAGACC_LOCKCFG_SET_MASK_TID(registerValue, HST_TAGACC_LOCKCFG_MASK_USE_BOTH_ACTION);
                    //HST_TAGACC_LOCKCFG_SET_ACTION_TID(registerValue, m_rdr_opt_parms.TagLock.tidMemoryBankPermissions);
                    registerValue |= HST_TAGACC_LOCKCFG_MASK_USE_BOTH_ACTION << HST_TAGACC_LOCKCFG_MASK_TID_SHIFT;
                    registerValue |= (uint)m_rdr_opt_parms.TagLock.tidMemoryBankPermissions << HST_TAGACC_LOCKCFG_ACTION_TID_SHIFT;
                }

                // If the user memory access permissions are not to change, then indicate
                // to ignore those bits.
                if (RFID_18K6C_TAG_MEM_PERM_NO_CHANGE == (uint)m_rdr_opt_parms.TagLock.userMemoryBankPermissions)
                {
                    //HST_TAGACC_LOCKCFG_SET_MASK_USER(registerValue, HST_TAGACC_LOCKCFG_MASK_USE_NO_ACTION);
                    registerValue |= HST_TAGACC_LOCKCFG_MASK_USE_NO_ACTION << HST_TAGACC_LOCKCFG_MASK_USER_SHIFT;
                }
                // Otherwise, indicate to look at the user memory bits and set the
                // persmission for it
                else
                {
                    //HST_TAGACC_LOCKCFG_SET_MASK_USER(registerValue, HST_TAGACC_LOCKCFG_MASK_USE_BOTH_ACTION);
                    //HST_TAGACC_LOCKCFG_SET_ACTION_USER(registerValue, m_rdr_opt_parms.TagLock.userMemoryBankPermissions);
                    registerValue |= HST_TAGACC_LOCKCFG_MASK_USE_BOTH_ACTION << HST_TAGACC_LOCKCFG_MASK_USER_SHIFT;
                    registerValue |= (uint)m_rdr_opt_parms.TagLock.userMemoryBankPermissions << HST_TAGACC_LOCKCFG_ACTION_USER_SHIFT;
                }

		        // Set up the lock configuration register
		        MacWriteRegister(MacRegister.HST_TAGACC_LOCKCFG, registerValue);

		        // Set up the access password register
		        MacWriteRegister(MacRegister.HST_TAGACC_ACCPWD, m_rdr_opt_parms.TagLock.accessPassword);

		        // Set up the HST_TAGACC_DESC_CFG register (controls the verify and retry
		        // count) and write it to the MAC
		        //m_pMac->WriteRegister(HST_TAGACC_DESC_CFG, HST_TAGACC_DESC_CFG_RETRY(0));

		        // Issue the lock command
		        //m_pMac->WriteRegister(HST_CMD, CMD_18K6CLOCK);
                if (COMM_HostCommand(HST_CMD.LOCK) != Result.OK)
                    return;
                    
                //MacClearError();

                if (m_TagAccessStatus == 2)
                    m_Result = Result.OK;

		        // Perform the common 18K6C tag-operation request post setup
		        //this->PostMacCommandIssue();
	        } //  Radio::Start18K6CLock

                
                

#if nouse
                ////////////////////////////////////////////////////////////////////////////////
	// Name:        Start18K6CBlockWrite
	// Description: Requests that an ISO 18000-6C block write be started on the
	//              radio module.
	////////////////////////////////////////////////////////////////////////////////
	void Radio::Start18K6CPermalock(
		const RFID_18K6C_PERMALOCK_PARMS*       pBWParms,
		INT32U                                    flags
		)
	{
		assert(NULL != pBWParms);

		// Perform the common 18K6C tag operation setup
		this->Start18K6CRequest(&pBWParms->common, flags);

		// Set the tag access descriptor to the first one just to be safe
		m_pMac->WriteRegister(HST_TAGACC_DESC_SEL, 0);

		// Set the tag write data select register to zero
		m_pMac->WriteRegister(HST_TAGWRDAT_SEL, 
			HST_TAGWRDAT_SEL_SELECTOR(0) | 
			HST_TAGWRDAT_SEL_RFU1(0));    

		// Set up the HST_TAGACC_DESC_CFG register (controls the verify and retry
		// count) and write it to the MAC
		/*INT32U  registerValue = 
		(pBWParms->verify ? HST_TAGACC_DESC_CFG_VERIFY_ENABLED :
		HST_TAGACC_DESC_CFG_VERIFY_DISABLED)  |
		HST_TAGACC_DESC_CFG_RETRY(pBWParms->verifyRetryCount)     | 
		HST_TAGACC_DESC_CFG_RFU1(0);
		m_pMac->WriteRegister(HST_TAGACC_DESC_CFG, registerValue);*/

		INT16U                  count   = pBWParms->permalockCmdParms.count;
		INT16U                  offset   = pBWParms->permalockCmdParms.offset;
		BOOL32					readOrLock = pBWParms->permalockCmdParms.readOrLock;
		const INT16U*           pData   = pBWParms->permalockCmdParms.pData;

		// Set up the tag bank register (tells where to write the data)
		m_pMac->WriteRegister(HST_TAGACC_BANK,
			HST_TAGACC_BANK_BANK(RFID_18K6C_MEMORY_BANK_USER) |
			HST_TAGACC_BANK_RFU1(0));

		//Set up the access offset register (i.e., number of words to lock)
		m_pMac->WriteRegister(HST_TAGACC_PTR, offset);

		// Set up the access count register (i.e., number of words to lock)
		m_pMac->WriteRegister(HST_TAGACC_CNT, count);

		// Set up the tag access password
		m_pMac->WriteRegister(HST_TAGACC_ACCPWD,
			pBWParms->accessPassword);

		m_pMac->WriteRegister(HST_TAGACC_LOCKCFG, readOrLock << 20);

		if(readOrLock)
		{
			// Set up the HST_TAGWRDAT_N registers.  Fill up a bank at a time.
			for (INT32U registerBank = 0; count; ++registerBank)
			{
				// Indicate which bank of tag write registers we are going to fill
				m_pMac->WriteRegister(HST_TAGWRDAT_SEL, HST_TAGWRDAT_SEL_SELECTOR(registerBank) | HST_TAGWRDAT_SEL_RFU1(0));

				if (HOSTIF_ERR_SELECTORBNDS == m_pMac->ReadRegister(MAC_ERROR))
				{
					this->ClearMacError();
					throw RfidErrorException(RFID_ERROR_INVALID_PARAMETER);
				}

				// Write the values to the bank until either the bank is full or we get to
				// a point where we cannot fill a register (i.e., we have 0 or 1 words left)
				offset = 0;
				for ( ; (offset < RFID_NUM_TAGWRDAT_REGS_PER_BANK) && (count > 1); count -= 2, pData += 2, ++offset)
				{
					m_pMac->WriteRegister(HST_TAGWRDAT_0 + offset, HST_TAGWRDAT_N_DATA0(*pData) | HST_TAGWRDAT_N_DATA1(*(pData + 1)));
				}

				// If we didn't use all registers in the bank and count is non-zero, it means
				// that the request was for an odd number of words to be written.  Make sure
				// that the last word is written.
				if ((offset < RFID_NUM_TAGWRDAT_REGS_PER_BANK) && (1 == count)) 
				{
					m_pMac->WriteRegister(HST_TAGWRDAT_0 + offset, HST_TAGWRDAT_N_DATA0(*pData) | HST_TAGWRDAT_N_DATA1(0));
					break;
				}
			}
		}

		// Issue the write command to the MAC
		m_pMac->WriteRegister(HST_CMD, CMD_18K6CBLOCKLOCK);

		// Perform the common 18K6C tag-operation request post setup
		this->PostMacCommandIssue();
	} //  Radio::Start18K6CPermalock
#endif


#if WIP
                	try
	{
		Validate18K6CTagCustBlockLock(parms);

		CONTEXT_PARMS		context;

		ZeroMemory(&context, sizeof(CONTEXT_PARMS));
		context.pReadData = parms->pData;

		RFID_18K6C_PERMALOCK_PARMS lparms;
		lparms.length = sizeof(RFID_18K6C_PERMALOCK_PARMS);
		lparms.accessPassword = parms->accessPassword;
		lparms.permalockCmdParms.length = sizeof(RFID_18K6C_PERMALOCK_CMD_PARMS);
		lparms.permalockCmdParms.readOrLock = parms->readOrLock;
		lparms.permalockCmdParms.count = parms->count;
		lparms.permalockCmdParms.offset = parms->offset;
		//lparms.permalockCmdParms.verify = 1;
		//lparms.verifyRetryCount = 7;
		lparms.permalockCmdParms.pData = parms->pData;
		lparms.common.tagStopCount = 1;
		lparms.common.context = &context;
		lparms.common.pCallbackCode = NULL;
		lparms.common.pCallback = fnCb_TagAccess;
		for(UINT32 i = 0; i <= parms->retry; i++)
		{
			if(((status = RFID_18K6CTagPermaLock(pHandle, &lparms, parms->flags)) == RFID_STATUS_OK) && context.operationSucceeded)
			{
				return RFID_STATUS_OK;
			}
		}
		return RFID_ERROR_MAX_RETRY_EXIT;
	}
	catch (rfid::RfidErrorException& error)
	{
		status = error.GetError();
	}
	catch (...)
	{
		status = RFID_ERROR_FAILURE;
	}
#endif


            catch (System.Exception ex)
            {
#if DEBUG
                CSLibrary.Diagnostics.CoreDebug.Logger.ErrorException("HighLevelInterface.TagLockThreadProc()", ex);
#endif
            }
            finally
            {
                FireAccessCompletedEvent(
                    new OnAccessCompletedEventArgs(
                    m_Result == Result.OK,
                    Bank.UNKNOWN,
                    TagAccess.LOCK,
                    null));

                FireStateChangedEvent(RFState.IDLE);
            }
        }

        private void TagSelected()
        {
            try
            {
                FireStateChangedEvent(RFState.BUSY);

                if (m_rdr_opt_parms.TagSelected.ParallelEncoding)
                {
                    UInt32 value = 0;

                    MacReadRegister(MacRegister.HST_TAGACC_DESC_CFG, ref value);
                    value &= ~0x0001U; // Disable Verify after write
                    MacWriteRegister(MacRegister.HST_TAGACC_DESC_CFG, value);

                    MacReadRegister(MacRegister.HST_QUERY_CFG, ref value);
                    value |= 0x0200; // Enable Ucode Parallel encoding
                    MacWriteRegister(MacRegister.HST_QUERY_CFG, value);
                }
                else
                {
                    UInt32 value = 0;

                    MacReadRegister(MacRegister.HST_TAGACC_DESC_CFG, ref value);
                    value |= 0x0001U; // Enable Verify after write
                    MacWriteRegister(MacRegister.HST_TAGACC_DESC_CFG, value);

                    MacReadRegister(MacRegister.HST_QUERY_CFG, ref value);
                    value &= ~0x0200U; // Enable Ucode Parallel encoding
                    MacWriteRegister(MacRegister.HST_QUERY_CFG, value);
                }

                if ((m_Result = SetOperationMode(RadioOperationMode.NONCONTINUOUS)) != Result.OK)
                {
                    goto EXIT;
                }

                if ((m_Result = SetTagGroup(Selected.ASSERTED, Session.S0, SessionTarget.A)) != Result.OK)
                {
                    goto EXIT;
                }

                if ((m_Result = SetSingulationAlgorithmParms(SingulationAlgorithm.FIXEDQ, new FixedQParms
                    (
                    m_rdr_opt_parms.TagSelected.Qvalue,//QValue
                    0x5, //Retry
                    (uint)((m_rdr_opt_parms.TagSelected.flags & SelectMaskFlags.ENABLE_TOGGLE) == SelectMaskFlags.ENABLE_TOGGLE ? 1 : 0),//toggle
                    0)//repeatUntilNoUnit
                    )) != Result.OK)
                {
                    goto EXIT;
                }

                SelectCriterion[] sel = new SelectCriterion[1];
                sel[0] = new SelectCriterion();
                sel[0].action = new SelectAction(CSLibrary.Constants.Target.SELECTED,
                    (m_rdr_opt_parms.TagSelected.flags & SelectMaskFlags.ENABLE_NON_MATCH) == SelectMaskFlags.ENABLE_NON_MATCH ?
                    CSLibrary.Constants.Action.DSLINVB_ASLINVA : CSLibrary.Constants.Action.ASLINVA_DSLINVB, 0);

                
                if (m_rdr_opt_parms.TagSelected.bank == MemoryBank.EPC)
                {
                    sel[0].mask = new SelectMask(
                        m_rdr_opt_parms.TagSelected.bank,
                        (uint)((m_rdr_opt_parms.TagSelected.flags & SelectMaskFlags.ENABLE_PC_MASK) == SelectMaskFlags.ENABLE_PC_MASK ? 16 : 32 + m_rdr_opt_parms.TagSelected.epcMaskOffset),
                        m_rdr_opt_parms.TagSelected.epcMaskLength,
                        m_rdr_opt_parms.TagSelected.epcMask.ToBytes());
                }
                else
                {
                    sel[0].mask = new SelectMask(
                        m_rdr_opt_parms.TagSelected.bank,
                        m_rdr_opt_parms.TagSelected.MaskOffset,
                        m_rdr_opt_parms.TagSelected.MaskLength,
                        m_rdr_opt_parms.TagSelected.Mask);
                }
                if ((m_Result = SetSelectCriteria(sel)) != Result.OK)
                {
                    goto EXIT;
                }
            }
            catch (System.Exception ex)
            {
#if DEBUG
                CSLibrary.Diagnostics.CoreDebug.Logger.ErrorException("HighLevelInterface.TagSelected()", ex);
#endif
                m_Result = Result.SYSTEM_CATCH_EXCEPTION;
            }

            EXIT:

            FireStateChangedEvent(RFState.IDLE);
        }

        private void PreFilterThreadProc()
        {
            try
            {
                UInt32 value = 0;

                MacReadRegister(MacRegister.HST_TAGACC_DESC_CFG, ref value);
                value |= 0x0001U; // Enable Verify after write
                MacWriteRegister(MacRegister.HST_TAGACC_DESC_CFG, value);

                MacReadRegister(MacRegister.HST_QUERY_CFG, ref value);
                value &= ~0x0200U; // Enable Ucode Parallel encoding
                MacWriteRegister(MacRegister.HST_QUERY_CFG, value);

                CSLibrary.Structures.SelectCriterion[] sel = new CSLibrary.Structures.SelectCriterion[1];
                sel[0] = new CSLibrary.Structures.SelectCriterion();
                sel[0].action = new CSLibrary.Structures.SelectAction(CSLibrary.Constants.Target.SELECTED,
                    (m_rdr_opt_parms.TagSelected.flags & CSLibrary.Constants.SelectMaskFlags.ENABLE_NON_MATCH) == CSLibrary.Constants.SelectMaskFlags.ENABLE_NON_MATCH ?
                    CSLibrary.Constants.Action.DSLINVB_ASLINVA : CSLibrary.Constants.Action.ASLINVA_DSLINVB, 0);

                //SetTagGroup(CSLibrary.Constants.Selected.ASSERTED, CSLibrary.Constants.Session.S0, CSLibrary.Constants.SessionTarget.A);
                SetTagGroup(CSLibrary.Constants.Selected.ASSERTED);

                if (m_rdr_opt_parms.TagSelected.bank == CSLibrary.Constants.MemoryBank.EPC)
                {
                    sel[0].mask = new CSLibrary.Structures.SelectMask(
                        m_rdr_opt_parms.TagSelected.bank,
                        (uint)((m_rdr_opt_parms.TagSelected.flags & CSLibrary.Constants.SelectMaskFlags.ENABLE_PC_MASK) == CSLibrary.Constants.SelectMaskFlags.ENABLE_PC_MASK ? 16 : 32 + m_rdr_opt_parms.TagSelected.epcMaskOffset),
                        m_rdr_opt_parms.TagSelected.epcMaskLength,
                        m_rdr_opt_parms.TagSelected.epcMask.ToBytes());
                }
                else
                {
                    sel[0].mask = new CSLibrary.Structures.SelectMask(
                        m_rdr_opt_parms.TagSelected.bank,
                        m_rdr_opt_parms.TagSelected.MaskOffset,
                        m_rdr_opt_parms.TagSelected.MaskLength,
                        m_rdr_opt_parms.TagSelected.Mask);
                }
                if ((m_Result = SetSelectCriteria(sel)) != CSLibrary.Constants.Result.OK)
                {
                    //goto EXIT;
                }
            }
            catch (System.Exception ex)
            {
#if DEBUG
                //CSLibrary.Diagnostics.CoreDebug.Logger.ErrorException("HighLevelInterface.TagSelected()", ex);
#endif
                m_Result = CSLibrary.Constants.Result.SYSTEM_CATCH_EXCEPTION;
            }
        }

        private void SetMaskThreadProc()
        {
            try
            {
                FireStateChangedEvent(RFState.BUSY);

                if (m_rdr_opt_parms.TagGeneralSelected.ParallelEncoding)
                {
                    UInt32 value = 0;

                    MacReadRegister(MacRegister.HST_TAGACC_DESC_CFG, ref value);
                    value &= ~0x0001U; // Disable Verify after write
                    MacWriteRegister(MacRegister.HST_TAGACC_DESC_CFG, value);

                    MacReadRegister(MacRegister.HST_QUERY_CFG, ref value);
                    value |= 0x0200; // Enable Ucode Parallel encoding
                    MacWriteRegister(MacRegister.HST_QUERY_CFG, value);
                }
                else
                {
                    UInt32 value = 0;

                    MacReadRegister(MacRegister.HST_TAGACC_DESC_CFG, ref value);
                    value |= 0x0001U; // Enable Verify after write
                    MacWriteRegister(MacRegister.HST_TAGACC_DESC_CFG, value);

                    MacReadRegister(MacRegister.HST_QUERY_CFG, ref value);
                    value &= ~0x0200U; // Enable Ucode Parallel encoding
                    MacWriteRegister(MacRegister.HST_QUERY_CFG, value);
                }

                SelectCriterion[] sel = new SelectCriterion[1];
                sel[0] = new SelectCriterion();
                sel[0].action = new SelectAction(CSLibrary.Constants.Target.SELECTED,
                    (m_rdr_opt_parms.TagGeneralSelected.flags & SelectMaskFlags.ENABLE_NON_MATCH) == SelectMaskFlags.ENABLE_NON_MATCH ?
                    CSLibrary.Constants.Action.DSLINVB_ASLINVA : CSLibrary.Constants.Action.ASLINVA_DSLINVB, 0);


                if (m_rdr_opt_parms.TagGeneralSelected.bank == MemoryBank.EPC)
                {
                    sel[0].mask = new SelectMask(
                        m_rdr_opt_parms.TagGeneralSelected.bank,
                        (uint)((m_rdr_opt_parms.TagGeneralSelected.flags & SelectMaskFlags.ENABLE_PC_MASK) == SelectMaskFlags.ENABLE_PC_MASK ? 16 : 32 + m_rdr_opt_parms.TagSelected.epcMaskOffset),
                        m_rdr_opt_parms.TagGeneralSelected.epcMaskLength,
                        m_rdr_opt_parms.TagGeneralSelected.epcMask.ToBytes());
                }
                else
                {
                    sel[0].mask = new SelectMask(
                        m_rdr_opt_parms.TagGeneralSelected.bank,
                        m_rdr_opt_parms.TagGeneralSelected.MaskOffset,
                        m_rdr_opt_parms.TagGeneralSelected.MaskLength,
                        m_rdr_opt_parms.TagGeneralSelected.Mask);
                }
                if ((m_Result = SetSelectCriteria(sel)) != Result.OK)
                {
                    goto EXIT;
                }
            }
            catch (System.Exception ex)
            {
#if DEBUG
                CSLibrary.Diagnostics.CoreDebug.Logger.ErrorException("HighLevelInterface.TagSelected()", ex);
#endif
                m_Result = Result.SYSTEM_CATCH_EXCEPTION;
            }

        EXIT:

            FireStateChangedEvent(RFState.IDLE);
        }

        private void TagPostMatch()
        {
            try
            {
                FireStateChangedEvent(RFState.BUSY);

                ////////////////////////////////
                //m_Result = TagSelected(m_rdr_opt_parms.TagSelected);
                ////////////////////////////////
                if ((m_Result = SetOperationMode(RadioOperationMode.NONCONTINUOUS)) != Result.OK)
                {
                    goto EXIT;
                }

                if ((m_Result = SetTagGroup(Selected.ASSERTED, Session.S0, SessionTarget.A)) != Result.OK)
                {
                    goto EXIT;
                }

                if ((m_Result = SetSingulationAlgorithmParms(SingulationAlgorithm.FIXEDQ, new FixedQParms
                (0, 3, (uint)(m_rdr_opt_parms.TagPostMatch.toggleTarget ? 1 : 0), 0))) != Result.OK)
                {
                    goto EXIT;
                }

                SingulationCriterion[] sel = new SingulationCriterion[1];
                sel[0] = new SingulationCriterion();
                sel[0].match = 1;
                sel[0].mask = new SingulationMask(
                    m_rdr_opt_parms.TagPostMatch.offset,
                    m_rdr_opt_parms.TagPostMatch.count,
                    m_rdr_opt_parms.TagPostMatch.mask);
                if ((m_Result = SetPostMatchCriteria(sel)) != Result.OK)
                {
                    goto EXIT;
                }

            }
            catch (System.Exception ex)
            {
#if DEBUG
                CSLibrary.Diagnostics.CoreDebug.Logger.ErrorException("HighLevelInterface.TagPostMatch()", ex);
#endif
                m_Result = Result.SYSTEM_CATCH_EXCEPTION;
            }
        EXIT:

            FireStateChangedEvent(RFState.IDLE);

        }

#if ENGINEERING_DEBUG
        /*public Result TagKill(KillParms parms, SelectFlags flags)
        {
            if (State == RFState.IDLE)
            {
                return ThrowException(TagKill(parms, flags));
            }
            return ThrowException(Result.RADIO_BUSY);
        }*/
#endif

        ////////////////////////////////////////////////////////////////////////////////
        // Name: RFID_18K6CTagKill
        //
        // Description:
        //   Executes a tag kill for the tags of interest.  If the
        //   RFID_FLAG_PERFORM_SELECT flag is specified, the tag population is
        //   partitioned (i.e., ISO 18000-6C select) prior to the tag-kill operation.
        //   If the RFID_FLAG_PERFORM_POST_MATCH flag is specified, the post-singulation
        //   match mask is applied to a singulated tag's EPC to determine if the tag
        //   will be killed.  The operation-response packets will be returned to the
        //   application via the application-supplied callback function.  Each tag-kill
        //   record is grouped with its corresponding tag-inventory record.  An
        //   application may prematurely stop a kill operation by calling
        //   RFID_Radio{Cancel|Aobrt}Operation on another thread or by returning a non-
        //   zero value from the callback function.
        ////////////////////////////////////////////////////////////////////////////////
        private bool RFID_18K6CTagKill()
        {
            // Perform the common 18K6C tag operation setup
            Start18K6CRequest(1, m_rdr_opt_parms.TagKill.flags);

            // Set up the access password register
            MacWriteRegister(MacRegister.HST_TAGACC_ACCPWD, m_rdr_opt_parms.TagKill.accessPassword);

            // Set up the kill password register
            MacWriteRegister(MacRegister.HST_TAGACC_KILLPWD, m_rdr_opt_parms.TagKill.killPassword);

            // Set up the kill extended register
            MacWriteRegister(MacRegister.HST_TAGACC_LOCKCFG, (0x7U & (uint)m_rdr_opt_parms.TagKill.extCommand) << 21);

            // Set up the HST_TAGACC_DESC_CFG register (controls the verify and retry
            // count) and write it to the MAC
            //m_pMac->WriteRegister(HST_TAGACC_DESC_CFG, HST_TAGACC_DESC_CFG_RETRY(7));

            // Issue the kill command
            if (COMM_HostCommand(HST_CMD.KILL) != Result.OK || CurrentOperationResult != Result.OK)
                return false;

            return true;
        } // RFID_18K6CTagKill


        private void TagKillThreadProc()
        {
            ushort[] tmp = new ushort[1];

            try
            {
                FireStateChangedEvent(RFState.BUSY);

                for (UInt32 i = 0; i <= m_rdr_opt_parms.TagKill.retryCount; i++)
                {
                    if (RFID_18K6CTagKill())
                    {
                        if (CUST_18K6CTagRead(
                            MemoryBank.EPC,
                            EPC_START_OFFSET,
                            1,
                            tmp,
                            m_rdr_opt_parms.TagKill.accessPassword,
                            m_rdr_opt_parms.TagKill.retryCount,
                            SelectFlags.SELECT) != true)
                        {
                            //can't read mean killed
                            m_Result = Result.OK;
                            return;
                        }
                    }
                }

                m_Result = Result.FAILURE;
                return;
            }
            catch (System.Exception ex)
            {
#if DEBUG
                CSLibrary.Diagnostics.CoreDebug.Logger.ErrorException("HighLevelInterface.TagKillThreadProc()", ex);
#endif
            }
            finally
            {
                FireAccessCompletedEvent(new OnAccessCompletedEventArgs(m_Result == Result.OK, Bank.UNKNOWN, TagAccess.KILL, null));
                FireStateChangedEvent(RFState.IDLE);
            }
        }

        private void RadioAbortOperation()
        {
            COMM_AdapterCommand(READERCMD.ABORT);
        }

        private void RadioCancelOperation()
        {
            COMM_AdapterCommand(READERCMD.CANCEL);
        }
#endregion

#region ====================== Start and Stop ======================
        /// <summary>
        /// Start operation
        /// </summary>
        /// <param name="opertion">operation type</param>
        /// <param name="bWait">blocking and waiting until user cancel or operation done</param>
        /// <returns><see cref="Result"/></returns>
        //[MethodImpl(MethodImplOptions.Synchronized)]
        public Result StartOperation(Operation opertion, bool bWait)
        {
            StopInventory = 0;

            if (State != RFState.IDLE/* || IsAlive()*/)
                return Result.RADIO_BUSY;

            CurrentOperation = opertion;
            m_save_blocking_mode = bWait;


/*            UInt32 x301 = 0;

            MacReadRegister(MacRegister.HST_PROTSCH_SMCFG, ref x301);
            x301 |= 0x03;
            MacWriteRegister(MacRegister.HST_PROTSCH_SMCFG, x301);
*/

//#if __NORMAL_MODE__
            //if (m_save_resp_mode != ResponseMode.COMPACT)
            //{
                //TurnOff Debug infomation to speed up inventory
                //ReturnState(SetRadioResponseDataMode(ResponseMode.COMPACT));
            //}
//#endif     

            switch (opertion)
            {
                case Operation.TAG_GENERALSELECTED:
                    OperationProcess(SetMaskThreadProc, bWait);
                    break;
                    
                case Operation.TAG_SELECTED:
                    {
                        if (bWait)
                        {
                            TagSelected();
                        }
                        else
                        {
                            g_hWndThread = new Thread(new ThreadStart(TagSelected));
                            g_hWndThread.Priority = ThreadPriority.Normal;
                            g_hWndThread.IsBackground = true;
                            g_hWndThread.Name = "TagSelected";
                            g_hWndThread.Start();
                            WaitToBusy();
                        }
                    }
                    break;
                
                case Operation.TAG_PREFILTER:
                    {
                        if (bWait)
                        {
                            PreFilterThreadProc();
                        }
                        else
                        {
                            g_hWndThread = new Thread(new ThreadStart(PreFilterThreadProc));
                            g_hWndThread.Priority = ThreadPriority.Normal;
                            g_hWndThread.IsBackground = true;
                            g_hWndThread.Name = "PreFilter";
                            g_hWndThread.Start();
                            WaitToBusy();
                        }
                    }
                    break;

                /*case Operation.TAG_LOCK_ACC_PWD:
                    {
                        if (bWait)
                        {
                            TagLockAccThreadProc();
                        }
                        else
                        {
                            g_hWndThread = new Thread(new ThreadStart(TagLockAccThreadProc));
                            g_hWndThread.Priority = ThreadPriority.Normal;
                            g_hWndThread.IsBackground = true;
                            g_hWndThread.Name = "LockAccThreadProc";
                            g_hWndThread.Start();
                            WaitToBusy();
                        }
                    } break;
                case Operation.TAG_LOCK_EPC:
                    {
                        if (bWait)
                        {
                            TagLockEPCThreadProc();
                        }
                        else
                        {
                            g_hWndThread = new Thread(new ThreadStart(TagLockEPCThreadProc));
                            g_hWndThread.Priority = ThreadPriority.Normal;
                            g_hWndThread.IsBackground = true;
                            g_hWndThread.Name = "LockEPCThreadProc";
                            g_hWndThread.Start();
                            WaitToBusy();
                        }
                    } break;
                case Operation.TAG_LOCK_KILL_PWD:
                    {
                        if (bWait)
                        {
                            TagLockKillThreadProc();
                        }
                        else
                        {
                            g_hWndThread = new Thread(new ThreadStart(TagLockKillThreadProc));
                            g_hWndThread.Priority = ThreadPriority.Normal;
                            g_hWndThread.IsBackground = true;
                            g_hWndThread.Name = "LockKillThreadProc";
                            g_hWndThread.Start();
                            WaitToBusy();
                        }
                    } break;
                case Operation.TAG_LOCK_TID:
                    {
                        if (bWait)
                        {
                            TagLockTIDThreadProc();
                        }
                        else
                        {
                            g_hWndThread = new Thread(new ThreadStart(TagLockTIDThreadProc));
                            g_hWndThread.Priority = ThreadPriority.Normal;
                            g_hWndThread.IsBackground = true;
                            g_hWndThread.Name = "LockTIDThreadProc";
                            g_hWndThread.Start();
                            WaitToBusy();
                        }
                    } break;
                case Operation.TAG_LOCK_USER:
                    {
                        if (bWait)
                        {
                            TagLockUSERThreadProc();
                        }
                        else
                        {
                            g_hWndThread = new Thread(new ThreadStart(TagLockUSERThreadProc));
                            g_hWndThread.Priority = ThreadPriority.Normal;
                            g_hWndThread.IsBackground = true;
                            g_hWndThread.Name = "LockUSERThreadProc";
                            g_hWndThread.Start();
                            WaitToBusy();
                        }
                    } break;*/

                case Operation.TAG_READ:
                    {
                        if (bWait)
                        {
                            TagReadThreadProc();
                        }
                        else
                        {
                            g_hWndThread = new Thread(new ThreadStart(TagReadThreadProc));
                            g_hWndThread.Priority = ThreadPriority.Normal;
                            g_hWndThread.IsBackground = true;
                            g_hWndThread.Name = "ReadThreadProc";
                            g_hWndThread.Start();
                            WaitToBusy();
                        }
                    } break;

                case Operation.TAG_READ_ACC_PWD:
                    {
                        if (bWait)
                        {
                            TagReadAccPwdThreadProc();
                        }
                        else
                        {
                            g_hWndThread = new Thread(new ThreadStart(TagReadAccPwdThreadProc));
                            g_hWndThread.Priority = ThreadPriority.Normal;
                            g_hWndThread.IsBackground = true;
                            g_hWndThread.Name = "ReadAccPwdThreadProc";
                            g_hWndThread.Start();
                            WaitToBusy();
                        }
                    } break;
                case Operation.TAG_READ_EPC:
                    {
                        if (bWait)
                        {
                            TagReadEPCThreadProc();
                        }
                        else
                        {
                            g_hWndThread = new Thread(new ThreadStart(TagReadEPCThreadProc));
                            g_hWndThread.Priority = ThreadPriority.Normal;
                            g_hWndThread.IsBackground = true;
                            g_hWndThread.Name = "ReadEPCThreadProc";
                            g_hWndThread.Start();
                            WaitToBusy();
                        }
                    } break;
                case Operation.TAG_READ_KILL_PWD:
                    {
                        if (bWait)
                        {
                            TagReadKillPwdThreadProc();
                        }
                        else
                        {
                            g_hWndThread = new Thread(new ThreadStart(TagReadKillPwdThreadProc));
                            g_hWndThread.Priority = ThreadPriority.Normal;
                            g_hWndThread.IsBackground = true;
                            g_hWndThread.Name = "ReadKillPwdThreadProc";
                            g_hWndThread.Start();
                            WaitToBusy();
                        }
                    } break;
                case Operation.TAG_READ_PC:
                    {
                        if (bWait)
                        {
                            TagReadPCThreadProc();
                        }
                        else
                        {
                            g_hWndThread = new Thread(new ThreadStart(TagReadPCThreadProc));
                            g_hWndThread.Priority = ThreadPriority.Normal;
                            g_hWndThread.IsBackground = true;
                            g_hWndThread.Name = "ReadPCThreadProc";
                            g_hWndThread.Start();
                            WaitToBusy();
                        }
                    } break;
                case Operation.TAG_READ_TID:
                    {
                        if (bWait)
                        {
                            TagReadTidThreadProc();
                        }
                        else
                        {
                            g_hWndThread = new Thread(new ThreadStart(TagReadTidThreadProc));
                            g_hWndThread.Priority = ThreadPriority.Normal;
                            g_hWndThread.IsBackground = true;
                            g_hWndThread.Name = "ReadTidThreadProc";
                            g_hWndThread.Start();
                            WaitToBusy();
                        }
                    } break;
                case Operation.TAG_READ_USER:
                    {
                        if (bWait)
                        {
                            TagReadUsrMemThreadProc();
                        }
                        else
                        {
                            g_hWndThread = new Thread(new ThreadStart(TagReadUsrMemThreadProc));
                            g_hWndThread.Priority = ThreadPriority.Normal;
                            g_hWndThread.IsBackground = true;
                            g_hWndThread.Name = "ReadUsrMemThreadProc";
                            g_hWndThread.Start();
                            WaitToBusy();
                        }
                    } break;
                case Operation.TAG_INVENTORY:
                    {
                        currentInventoryFreqRevIndex = FreqIndex(m_save_region_code);

                        for (int cnt = 0; cnt < 16; cnt++)
                            ChannelStatus[cnt] = RFState.UNKNOWN;

                        m_sorted_epc_records.Clear();

                        if (bWait)
                        {
                            TagInventoryThreadProc();
                        }
                        else
                        {
                            g_hWndThread = new Thread(new ThreadStart(TagInventoryThreadProc));
                            //g_hWndThread.Priority = ThreadPriority.Normal;
                            g_hWndThread.Priority = ThreadPriority.Normal;
                            g_hWndThread.IsBackground = true;
                            g_hWndThread.Name = "TagInventory";
                            g_hWndThread.Start();
                            WaitToBusy();
                        }
                    } break;
                case Operation.TAG_RANGING:
                    {
                        //currentInventoryFreqRevIndex = RevFreqIndex(m_save_region_code);
                        currentInventoryFreqRevIndex = FreqIndex(m_save_region_code);

                        for (int cnt = 0; cnt < 16; cnt++)
                            ChannelStatus[cnt] = RFState.UNKNOWN;

                        if (bWait)
                        {
                            TagRangingThreadProc();
                        }
                        else
                        {
                            g_hWndThread = new Thread(new ThreadStart(TagRangingThreadProc));
                            g_hWndThread.Priority = ThreadPriority.Normal;
                            g_hWndThread.IsBackground = true;
                            g_hWndThread.Name = "RangingAllThreadProc";
                            g_hWndThread.Start();
                            
                            WaitToBusy();
                        }

                    } 
                    
                    break;

                case Operation.TAG_SEARCHING:
                    {
                        if (bWait)
                        {
                            TagSearchOneTagThreadProc();
                        }
                        else
                        {
                            g_hWndThread = new Thread(new ThreadStart(TagSearchOneTagThreadProc));
                            g_hWndThread.Priority = ThreadPriority.Normal;
                            g_hWndThread.IsBackground = true;
                            g_hWndThread.Name = "SearchOneTagThreadProc";
                            g_hWndThread.Start();
                            WaitToBusy();
                        }
                    } break;
                case Operation.TAG_WRITE:
                    {
                        if (bWait)
                        {
                            TagWriteThreadProc();
                        }
                        else
                        {
                            g_hWndThread = new Thread(new ThreadStart(TagWriteThreadProc));
                            g_hWndThread.Priority = ThreadPriority.Normal;
                            g_hWndThread.IsBackground = true;
                            g_hWndThread.Name = "WriteThreadProc";
                            g_hWndThread.Start();
                            WaitToBusy();
                        }
                    } break;
                case Operation.TAG_WRITE_ACC_PWD:
                    {
                        if (bWait)
                        {
                            TagWriteAccPwdThreadProc();
                        }
                        else
                        {
                            g_hWndThread = new Thread(new ThreadStart(TagWriteAccPwdThreadProc));
                            g_hWndThread.Priority = ThreadPriority.Normal;
                            g_hWndThread.IsBackground = true;
                            g_hWndThread.Name = "WriteAccPwdThreadProc";
                            g_hWndThread.Start();
                            WaitToBusy();
                        }
                    } break;
                case Operation.TAG_WRITE_EPC:
                    {
                        if (bWait)
                        {
                            TagWriteEPCThreadProc();
                        }
                        else
                        {
                            g_hWndThread = new Thread(new ThreadStart(TagWriteEPCThreadProc));
                            g_hWndThread.Priority = ThreadPriority.Normal;
                            g_hWndThread.IsBackground = true;
                            g_hWndThread.Name = "TagWriteEPCThreadProc";
                            g_hWndThread.Start();
                            WaitToBusy();
                        }
                    } break;
                case Operation.TAG_WRITE_KILL_PWD:
                    {
                        if (bWait)
                        {
                            TagWriteKillPwdThreadProc();
                        }
                        else
                        {
                            g_hWndThread = new Thread(new ThreadStart(TagWriteKillPwdThreadProc));
                            g_hWndThread.Priority = ThreadPriority.Normal;
                            g_hWndThread.IsBackground = true;
                            g_hWndThread.Name = "WriteAccPwdThreadProc";
                            g_hWndThread.Start();
                            WaitToBusy();
                        }
                    } break;
                case Operation.TAG_WRITE_PC:
                    {
                        if (bWait)
                        {
                            TagWritePCThreadProc();
                        }
                        else
                        {
                            g_hWndThread = new Thread(new ThreadStart(TagWritePCThreadProc));
                            g_hWndThread.Priority = ThreadPriority.Normal;
                            g_hWndThread.IsBackground = true;
                            g_hWndThread.Name = "WritePCThreadProc";
                            g_hWndThread.Start();
                            WaitToBusy();
                        }
                    } break;
                /*case Operation.TAG_WRITE_TID:
                    break;*/
                case Operation.TAG_WRITE_USER:
                    {
                        if (bWait)
                        {
                            TagWriteUsrMemThreadProc();
                        }
                        else
                        {
                            g_hWndThread = new Thread(new ThreadStart(TagWriteUsrMemThreadProc));
                            g_hWndThread.Priority = ThreadPriority.Normal;
                            g_hWndThread.IsBackground = true;
                            g_hWndThread.Name = "TagWriteUsrMemThreadProc";
                            g_hWndThread.Start();
                            WaitToBusy();
                        }
                    } break;
                case Operation.TAG_BLOCK_WRITE:
                    {
                        if (bWait)
                        {
                            BlockWriteThreadProc();
                        }
                        else
                        {
                            g_hWndThread = new Thread(new ThreadStart(BlockWriteThreadProc));
                            g_hWndThread.Priority = ThreadPriority.Normal;
                            g_hWndThread.IsBackground = true;
                            g_hWndThread.Name = "BlockWrite";
                            g_hWndThread.Start();
                            WaitToBusy();
                        }
                    } break;
                case Operation.TAG_KILL:
                    {
                        if (bWait)
                        {
                            TagKillThreadProc();
                        }
                        else
                        {
                            g_hWndThread = new Thread(new ThreadStart(TagKillThreadProc));
                            g_hWndThread.Priority = ThreadPriority.Normal;
                            g_hWndThread.IsBackground = true;
                            g_hWndThread.Name = "TagKillThreadProc";
                            g_hWndThread.Start();
                            WaitToBusy();
                        }
                    } break;
                case Operation.TAG_LOCK:
                    {
                        if (bWait)
                        {
                            TagLockThreadProc();
                        }
                        else
                        {
                            g_hWndThread = new Thread(new ThreadStart(TagLockThreadProc));
                            g_hWndThread.Priority = ThreadPriority.Normal;
                            g_hWndThread.IsBackground = true;
                            g_hWndThread.Name = "TagLockThreadProc";
                            g_hWndThread.Start();
                            WaitToBusy();
                        }
                    } break;
                case Operation.TAG_BLOCK_PERMALOCK:
                    {
                        if (bWait)
                        {
                            TagBlockLockThreadProc();
                        }
                        else
                        {
                            g_hWndThread = new Thread(new ThreadStart(TagBlockLockThreadProc));
                            g_hWndThread.Priority = ThreadPriority.Normal;
                            g_hWndThread.IsBackground = true;
                            g_hWndThread.Name = "TagBlockLockThreadProc";
                            g_hWndThread.Start();
                            WaitToBusy();
                        }
                    }
                    break;
                case Operation.TAG_READ_PROTECT:
                    {
                        if (bWait)
                        {
                            CustTagReadProtectThreadProc();
                        }
                        else
                        {
                            g_hWndThread = new Thread(new ThreadStart(CustTagReadProtectThreadProc));
                            g_hWndThread.Priority = ThreadPriority.Normal;
                            g_hWndThread.IsBackground = true;
                            g_hWndThread.Name = "CustTagReadProtectThreadProc";
                            g_hWndThread.Start();
                            WaitToBusy();
                        }
                    }
                    break;
                case Operation.TAG_RESET_READ_PROTECT:
                    {
                        if (bWait)
                        {
                            CustTagResetReadProtectThreadProc();
                        }
                        else
                        {
                            g_hWndThread = new Thread(new ThreadStart(CustTagResetReadProtectThreadProc));
                            g_hWndThread.Priority = ThreadPriority.Normal;
                            g_hWndThread.IsBackground = true;
                            g_hWndThread.Name = "CustTagResetReadProtectThreadProc";
                            g_hWndThread.Start();
                            WaitToBusy();
                        }
                    }
                    break;
                case Operation.CL_SET_PASSWORD:
                    {
                        if (bWait)
                        {
                            CLSetPasswordThreadProc();
                        }
                        else
                        {
                            g_hWndThread = new Thread(new ThreadStart(CLSetPasswordThreadProc));
                            g_hWndThread.Priority = ThreadPriority.Normal;
                            g_hWndThread.IsBackground = true;
                            g_hWndThread.Name = "ClSetPasswordThreadProc";
                            g_hWndThread.Start();
                            WaitToBusy();
                        }
                    }
                    break;
                case Operation.CL_SET_LOG_MODE:
                    {
                        if (bWait)
                        {
                            CLSetLogModeThreadProc();
                        }
                        else
                        {
                            g_hWndThread = new Thread(new ThreadStart(CLSetLogModeThreadProc));
                            g_hWndThread.Priority = ThreadPriority.Normal;
                            g_hWndThread.IsBackground = true;
                            g_hWndThread.Name = "ClSetLogModeThreadProc";
                            g_hWndThread.Start();
                            WaitToBusy();
                        }
                    }
                    break;
                case Operation.CL_SET_LOG_LIMITS:
                    {
                        if (bWait)
                        {
                            CLSetLogLimitsThreadProc();
                        }
                        else
                        {
                            g_hWndThread = new Thread(new ThreadStart(CLSetLogLimitsThreadProc));
                            g_hWndThread.Priority = ThreadPriority.Normal;
                            g_hWndThread.IsBackground = true;
                            g_hWndThread.Name = "ClSetLogLimits";
                            g_hWndThread.Start();
                            WaitToBusy();
                        }
                    }
                    break;
                case Operation.CL_GET_MEASUREMENT_SETUP:
                    {
                        if (bWait)
                        {
                            CLGetMeasurementSetupThreadProc();
                        }
                        else
                        {
                            g_hWndThread = new Thread(new ThreadStart(CLSetPasswordThreadProc));
                            g_hWndThread.Priority = ThreadPriority.Normal;
                            g_hWndThread.IsBackground = true;
                            g_hWndThread.Name = "ClSetPasswordThreadProc";
                            g_hWndThread.Start();
                            WaitToBusy();
                        }
                    }
                    break;
                case Operation.CL_SET_SFE_PARA:
                    {
                        if (bWait)
                        {
                            CLSetSfeParaThreadProc();
                        }
                        else
                        {
                            g_hWndThread = new Thread(new ThreadStart(CLSetSfeParaThreadProc));
                            g_hWndThread.Priority = ThreadPriority.Normal;
                            g_hWndThread.IsBackground = true;
                            g_hWndThread.Name = "ClSetSfeParaThreadProc";
                            g_hWndThread.Start();
                            WaitToBusy();
                        }
                    }
                    break;
                case Operation.CL_SET_CAL_DATA:
                    {
                        if (bWait)
                        {
                            CLSetCalDataThreadProc();
                        }
                        else
                        {
                            g_hWndThread = new Thread(new ThreadStart(CLSetCalDataThreadProc));
                            g_hWndThread.Priority = ThreadPriority.Normal;
                            g_hWndThread.IsBackground = true;
                            g_hWndThread.Name = "ClSetCalDataThreadProc";
                            g_hWndThread.Start();
                            WaitToBusy();
                        }
                    }
                    break;
                case Operation.CL_END_LOG:
                    {
                        if (bWait)
                        {
                            CLEndLogThreadProc();
                        }
                        else
                        {
                            g_hWndThread = new Thread(new ThreadStart(CLEndLogThreadProc));
                            g_hWndThread.Priority = ThreadPriority.Normal;
                            g_hWndThread.IsBackground = true;
                            g_hWndThread.Name = "ClEndLogThreadProc";
                            g_hWndThread.Start();
                            WaitToBusy();
                        }
                    }
                    break;
                case Operation.CL_START_LOG:
                    {
                        if (bWait)
                        {
                            CLStartLogThreadProc();
                        }
                        else
                        {
                            g_hWndThread = new Thread(new ThreadStart(CLStartLogThreadProc));
                            g_hWndThread.Priority = ThreadPriority.Normal;
                            g_hWndThread.IsBackground = true;
                            g_hWndThread.Name = "ClStartLogThreadProc";
                            g_hWndThread.Start();
                            WaitToBusy();
                        }
                    }
                    break;
                case Operation.CL_GET_LOG_STATE:
                    {
                        if (bWait)
                        {
                            CLGetLogStateThreadProc();
                        }
                        else
                        {
                            g_hWndThread = new Thread(new ThreadStart(CLGetLogStateThreadProc));
                            g_hWndThread.Priority = ThreadPriority.Normal;
                            g_hWndThread.IsBackground = true;
                            g_hWndThread.Name = "ClGetLogStateThreadProc";
                            g_hWndThread.Start();
                            WaitToBusy();
                        }
                    }
                    break;
                case Operation.CL_GET_CAL_DATA:
                    {
                        if (bWait)
                        {
                            CLGetCalDataThreadProc();
                        }
                        else
                        {
                            g_hWndThread = new Thread(new ThreadStart(CLGetCalDataThreadProc));
                            g_hWndThread.Priority = ThreadPriority.Normal;
                            g_hWndThread.IsBackground = true;
                            g_hWndThread.Name = "ClGetCalDataThreadProc";
                            g_hWndThread.Start();
                            WaitToBusy();
                        }
                    }
                    break;
                case Operation.CL_GET_BAT_LV:
                    {
                        if (bWait)
                        {
                            CLGetBatLvThreadProc();
                        }
                        else
                        {
                            g_hWndThread = new Thread(new ThreadStart(CLGetBatLvThreadProc));
                            g_hWndThread.Priority = ThreadPriority.Normal;
                            g_hWndThread.IsBackground = true;
                            g_hWndThread.Name = "ClGetBatLvThreadProc";
                            g_hWndThread.Start();
                            WaitToBusy();
                        }
                    }
                    break;
                case Operation.CL_SET_SHELF_LIFE:
                    {
                        if (bWait)
                        {
                            CLSetShelfLifeThreadProc();
                        }
                        else
                        {
                            g_hWndThread = new Thread(new ThreadStart(CLSetShelfLifeThreadProc));
                            g_hWndThread.Priority = ThreadPriority.Normal;
                            g_hWndThread.IsBackground = true;
                            g_hWndThread.Name = "ClSetShelfLifeThreadProc";
                            g_hWndThread.Start();
                            WaitToBusy();
                        }
                    }
                    break;
                case Operation.CL_INIT:
                    {
                        if (bWait)
                        {
                            CLInitThreadProc();
                        }
                        else
                        {
                            g_hWndThread = new Thread(new ThreadStart(CLInitThreadProc));
                            g_hWndThread.Priority = ThreadPriority.Normal;
                            g_hWndThread.IsBackground = true;
                            g_hWndThread.Name = "ClInitThreadProc";
                            g_hWndThread.Start();
                            WaitToBusy();
                        }
                    }
                    break;
                case Operation.CL_GET_SENSOR_VALUE:
                    {
                        if (bWait)
                        {
                            CLGetSensorValueThreadProc();
                        }
                        else
                        {
                            g_hWndThread = new Thread(new ThreadStart(CLGetSensorValueThreadProc));
                            g_hWndThread.Priority = ThreadPriority.Normal;
                            g_hWndThread.IsBackground = true;
                            g_hWndThread.Name = "ClGetSensorValueThreadProc";
                            g_hWndThread.Start();
                            WaitToBusy();
                        }
                    }
                    break;
                case Operation.CL_OPEN_AREA:
                    {
                        if (bWait)
                        {
                            CLOpenAreaThreadProc();
                        }
                        else
                        {
                            g_hWndThread = new Thread(new ThreadStart(CLOpenAreaThreadProc));
                            g_hWndThread.Priority = ThreadPriority.Normal;
                            g_hWndThread.IsBackground = true;
                            g_hWndThread.Name = "ClOpenAreaThreadProc";
                            g_hWndThread.Start();
                            WaitToBusy();
                        }
                    }
                    break;
                case Operation.CL_ACCESS_FIFO:
                    {
                        if (bWait)
                        {
                            CLAccessFifoThreadProc();
                        }
                        else
                        {
                            g_hWndThread = new Thread(new ThreadStart(CLAccessFifoThreadProc));
                            g_hWndThread.Priority = ThreadPriority.Normal;
                            g_hWndThread.IsBackground = true;
                            g_hWndThread.Name = "ClAccessFifoThreadProc";
                            g_hWndThread.Start();
                            WaitToBusy();
                        }
                    }
                    break;

                case Operation.G2_READ_PROTECT:
                    break;

                case Operation.G2_RESET_READ_PROTECT:
                    break;

                case Operation.G2_CHANGE_EAS:
                    OperationProcess(G2X_Change_EASThreadProc, bWait);
                    break;

                case Operation.G2_EAS_ALARM:
                    OperationProcess(G2X_EAS_AlarmThreadProc, bWait);
                    break;

                case Operation.G2_CHANGE_CONFIG:
                    OperationProcess(G2X_ChangeConfigThreadProc, bWait);
                    break;

                case Operation.QT_COMMAND:
                    {
                        if (!bWait)
                            m_Result = Result.NOT_SUPPORTED;
                        else
                            QT_CommandProc();
                    }
                    break;

                case Operation.EM_ResetAlarms:
                    OperationProcess(EMResetAlarmsThreadProc, bWait);
                    break;

                case Operation.EM4325_GetUid:
                    OperationProcess(EM4325GetUidThreadProc, bWait);
                    break;

                case Operation.EM_GetSensorData:
                    OperationProcess(EMGetSensorDataThreadProc, bWait);
                    break;

/*                case Operation.EM_SPI:
                    // Verify
                    if (m_rdr_opt_parms.EM4325SPI.ByteDelay > 0x03 || m_rdr_opt_parms.EM4325SPI.InitialDelay > 0x03 || m_rdr_opt_parms.EM4325SPI.SClk > 0x03 || m_rdr_opt_parms.EM4325SPI.ResponseSize > 0x07 || m_rdr_opt_parms.EM4325SPI.CommandSize > 0x07)
                        return (m_Result = Result.INVALID_PARAMETER);

                    OperationProcess(EM4325SPIThreadProc, bWait);
                    break;
*/
#if WIP
        /// <summary>
        /// EM4325 Command
        /// </summary>
        EM4325_SPIBoot,
        /// <summary>
        /// EM4325 Command
        /// </summary>
        EM4325_SPITransponder,
        /// <summary>
        /// EM4325 Command
        /// </summary>
        EM4325_SPIGetSensorData,
        /// <summary>
        /// EM4325 Command
        /// </summary>
        EM4325_SPISetFlags,
        /// <summary>
        /// EM4325 Command
        /// </summary>
        EM4325_SPIReadWord,
        /// <summary>
        /// EM4325 Command
        /// </summary>
        EM4325_SPIWriteWord,
        /// <summary>
        /// EM4325 Command
        /// </summary>
        EM4325_SPIReadPage,
        /// <summary>
        /// EM4325 Command
        /// </summary>
        EM4325_SPIWritePage,
        /// <summary>
        /// EM4325 Command
        /// </summary>
        EM4325_SPISetClock,
        /// <summary>
        /// EM4325 Command
        /// </summary>
        EM4325_SPIAlarm,
        /// <summary>
        /// EM4325 Command
        /// </summary>
        EM4325_SPIReadRegisterFileWord,
        /// <summary>
        /// EM4325 Command
        /// </summary>
        EM4325_SPIWriteRegisterFileWord,
        /// <summary>
        /// EM4325 Command
        /// </summary>
        EM4325_SPIReqRN,
#endif

/*
                case Operation.EAS_CONFIG:
                    OperationProcess(CustTagEASConfigThreadProc, bWait);
                    break;

                case Operation.EAS_ALARM:
                    OperationProcess(CustTagEASAlarmThreadProc, bWait);
                    break;
*/

#if NOUSE
                case Operation.TAG_READ:
                    {
                        if (bWait)
                        {
                            ReadThreadProc();
                        }
                        else
                        {
                            g_hWndThread = new Thread(new ThreadStart(ReadThreadProc));
                            g_hWndThread.Priority = ThreadPriority.Normal;
                            g_hWndThread.IsBackground = true;
                            g_hWndThread.Name = "TagRead";
                            g_hWndThread.Start();
                            WaitToBusy();
                        }
                    } break;
                case Operation.TAG_WRITE:
                    {
                        if (bWait)
                        {
                            WriteThreadProc();
                        }
                        else
                        {
                            g_hWndThread = new Thread(new ThreadStart(WriteThreadProc));
                            g_hWndThread.Priority = ThreadPriority.Normal;
                            g_hWndThread.IsBackground = true;
                            g_hWndThread.Name = "TagWrite";
                            g_hWndThread.Start();
                            WaitToBusy();
                        }
                    } break;
#endif
                default: return Result.INVALID_PARAMETER;
            }

            if (ThreadErrorMsg.Length != 0)
                m_Result = Result.THREAD_ERROR;

            return m_Result;
        }

        public Result StopOperation(bool abort)
        {
            try
            {
                if (State == RFState.IDLE)
                    return Result.OK;

/*                if (State == RFState.ABORT || State == RFState.RESET || State == RFState.BUFFER_FULL)
                    return Result.RADIO_BUSY;
*/
                FireStateChangedEvent(RFState.ABORT);

                Interlocked.Exchange(ref bStop, 1);

                if (abort)
                    StopInventory = 1;
                else
                    StopInventory = 2;

//                if (abort)
//                    RadioAbortOperation();
//                else
//                    RadioCancelOperation();
            }
            catch (Exception ex)
            {
                return m_Result = Result.SYSTEM_CATCH_EXCEPTION;
            }

            return m_Result;
        }

        /// <summary>
        /// only for ColdChain IC card
        /// </summary>
        /// <param name="StartTime"></param>
        /// <param name="Interval"></param>
        /// <param name="Offset"></param>
        /// <returns></returns>
        public Result ColdChain_StartTemperatureLog (UInt32 StartTime,  UInt16 TimeInterval, byte TemperatureOffset)
        {
            UInt32 Cmd0, Cmd1, Setting = 0;

            try
            {
                Cmd0 = (0xe0U << 24) | (StartTime >> 8);
                Cmd1 = (UInt32)((StartTime << 24) | (TimeInterval << 8) | TemperatureOffset);


                Start18K6CRequest(1, SelectFlags.SELECT);

                if (MacReadRegister(MacRegister.HST_RFTC_RFU_0x0B2C, ref Setting) != Result.OK)
                    return m_Result;

                Setting &= 0xf03f; // 1111 000 000 11 11 11   
                Setting |= 0x07 << 9;
                Setting |= 0x01 << 6;

                if ((m_Result = MacWriteRegister(MacRegister.HST_RFTC_RFU_0x0B2C, Setting)) != Result.OK)
                    return m_Result;
                if ((m_Result = MacWriteRegister(MacRegister.HST_RFTC_RFU_0x0B2D, Cmd0)) != Result.OK)
                    return m_Result;
                if ((m_Result = MacWriteRegister(MacRegister.HST_RFTC_RFU_0x0B2E, Cmd1)) != Result.OK)
                    return m_Result;

                m_Result = COMM_HostCommand(HST_CMD.CUSTOMEMSENDSPI);
                switch (m_Result)
                {
                    case Result.OK:
                        {
                            uint macErr = 0;
                            
                            if ((m_Result = GetMacErrorCode(ref macErr)) != Result.OK)
                                throw new ReaderException(m_Result, "GetMacErrorCode failed");
                        }
                        break;
                    default:
                        FireStateChangedEvent(RFState.RESET);
                        return Result.FAILURE;
                }
            }
            catch (System.Exception ex)
            {
                FireStateChangedEvent(RFState.RESET);
            }
            return Result.OK;
        }

        public Result ColdChain_StopTemperatureLog ()
        {
            UInt32 Cmd0, Cmd1;

            try
            {
                Cmd0 = 0xe1U << 24;
                Cmd1 = 0x00U;

                if ((m_Result = MacWriteRegister(MacRegister.HST_RFTC_RFU_0x0B2D, Cmd0)) != Result.OK)
                    return m_Result;
                if ((m_Result = MacWriteRegister(MacRegister.HST_RFTC_RFU_0x0B2E, Cmd1)) != Result.OK)
                    return m_Result;

                m_Result = COMM_HostCommand(HST_CMD.CUSTOMEMSENDSPI);
                switch (m_Result)
                {
                    case Result.OK:
                        {
                            uint macErr = 0;
                            
                            if ((m_Result = GetMacErrorCode(ref macErr)) != Result.OK)
                                throw new ReaderException(m_Result, "GetMacErrorCode failed");
                        }
                        break;
                    default:
                        FireStateChangedEvent(RFState.RESET);
                        return Result.FAILURE;
                }
            }
            catch (System.Exception ex)
            {
                FireStateChangedEvent(RFState.RESET);
            }
            return Result.OK;
        }


        UInt16 ColdChainLogSize;
        public Result ColdChain_GetTemperatureLogSize (out UInt16 size)
        {
            UInt32 Cmd0, Cmd1;

            size = 0;

            try
            {
                Cmd0 = 0xe3U << 24;
                Cmd1 = 0x00;

                if ((m_Result = MacWriteRegister(MacRegister.HST_RFTC_RFU_0x0B2D, Cmd0)) != Result.OK)
                    return m_Result;
                if ((m_Result = MacWriteRegister(MacRegister.HST_RFTC_RFU_0x0B2E, Cmd1)) != Result.OK)
                    return m_Result;

                m_Result = COMM_HostCommand(HST_CMD.CUSTOMEMSENDSPI);
                switch (m_Result)
                {
                    case Result.OK:
                        size = ColdChainLogSize;
                        break;
                    default:
                        FireStateChangedEvent(RFState.RESET);
                        return Result.FAILURE;
                }
            }
            catch (System.Exception ex)
            {
                FireStateChangedEvent(RFState.RESET);
            }
            return Result.OK;
        }
         
        public Result ColdChain_GetTemperatureLogData (UInt16 StartIndex, UInt16 Size, bool bWait)
        {
            UInt32 Cmd0, Cmd1;

            try
            {
                Cmd0 = (UInt32)((0xe3U << 24) | (StartIndex << 8) | (Size >> 8));
                Cmd1 = (UInt32)(Size << 24);

                if ((m_Result = MacWriteRegister(MacRegister.HST_RFTC_RFU_0x0B2D, Cmd0)) != Result.OK)
                    return m_Result;
                if ((m_Result = MacWriteRegister(MacRegister.HST_RFTC_RFU_0x0B2E, Cmd1)) != Result.OK)
                    return m_Result;

                m_Result = COMM_HostCommand(HST_CMD.CUSTOMEMSENDSPI);
                switch (m_Result)
                {
                    case Result.OK:
                        break;
                    default:
                        FireStateChangedEvent(RFState.RESET);
                        return Result.FAILURE;
                }
            }
            catch (System.Exception ex)
            {
                FireStateChangedEvent(RFState.RESET);
            }
            return Result.OK;
        }

        public Result ColdChain_GetSensorData ()
        {
            uint value = 0;

            try
            {
                MacReadRegister ((MacRegister)0xa01, ref value);

                value &= ~((UInt32)(1 << 23));
                value |= 1 << 24;

                MacWriteRegister((MacRegister)0xa01, value);

                m_Result = COMM_HostCommand(HST_CMD.CUSTOMEMGETSENSORDATA);
                switch (m_Result)
                {
                    case Result.OK:
                        break;

                    default:
                        FireStateChangedEvent(RFState.RESET);
                        return Result.FAILURE;
                }
            }
            catch (System.Exception ex)
            {
                FireStateChangedEvent(RFState.RESET);
            }

            return Result.OK;
        }

#if nouse
        /// <summary>
        /// Stop current operation by abort or cancel
        /// </summary>
        /// <param name="abort"></param>
        /// <returns></returns>
        //[MethodImpl(MethodImplOptions.Synchronized)]
        public Result StopOperation(bool abort)
        {
            try
            {
                if (State == RFState.IDLE)
                    return Result.OK;

                if (State != RFState.BUSY)
                    return Result.RADIO_BUSY;

                if (m_save_blocking_mode && Interlocked.Equals(bStop, 0))
                {
                    FireStateChangedEvent(RFState.ABORT);

                    // set event "Stop"
                    Interlocked.Exchange(ref bStop, 1);

                    if (abort)
                    {
                        Thread stop = new Thread(new ThreadStart(RadioAbortOperation));
                        stop.Name = "StopOperation";
                        stop.IsBackground = true;
                        stop.Start();
                        //stop.Join();
                    }
                    else
                    {
                        Thread stop = new Thread(new ThreadStart(RadioCancelOperation));
                        stop.Name = "StopOperation";
                        stop.IsBackground = true;
                        stop.Start();
                        //stop.Join();
                    }

                }
                else
                {
//                    if (IsAlive() && Interlocked.Equals(bStop, 0))  // thread is active
                    {
                        FireStateChangedEvent(RFState.ABORT);

                        // set event "Stop"
                        Interlocked.Exchange(ref bStop, 1);

                        if (abort)
                        {
                            Thread stop = new Thread(new ThreadStart(RadioAbortOperation));
                            stop.Name = "StopOperation";
                            stop.IsBackground = true;
                            stop.Start();
                            //while (!stop.Join(10)) Thread.Sleep(1);*/
                            //RadioAbortOperation();
                        }
                        else
                        {
                            Thread stop = new Thread(new ThreadStart(RadioCancelOperation));
                            stop.Name = "StopOperation";
                            stop.IsBackground = true;
                            stop.Start();
                            //while (!stop.Join(10)) Thread.Sleep(1);*/
                            //RadioCancelOperation();
                        }

                        WaitToIdle();
                    }
                }
            }
            catch (System.Exception ex)
            {
#if DEBUG
                CSLibrary.Diagnostics.CoreDebug.Logger.ErrorException("HighLevelInterface.StopOperation()", ex);
#endif
                return Result.SYSTEM_CATCH_EXCEPTION;
            }
            return m_Result;
        }
#endif

#endregion

#region ====================== Mac Error Routines ======================

#if TEMPLOG

        private void TimeOverToCapture(DateTime Now, int time)
        {
            if (Math.Abs(((TimeSpan)DateTime.Now.Subtract(Now)).TotalMinutes) >= time)
                TemperatureLog();
        }

        private void TemperatureLog()
        {
            //uint a = 0, b = 0, c = 0;
            TemperatureParms temp = GetCurrentTemperature();
            Thread.Sleep(1);
            using (FileStream fileIO = new FileStream(TmpLogFile, FileMode.Append))
            using (StreamWriter sw = new StreamWriter(fileIO))
            {
                sw.WriteLine(String.Format("[{0}] {1} {2} {3}", DateTime.Now, temp.amb, temp.xcvr, temp.pwramp));
                sw.Flush();
            }
        }
#endif
        /// <summary>
        /// Check Mac error code
        /// </summary>
        /// <param name="macErrCode">Mac Error Code</param>
        /// <returns></returns>
        public bool MacErrorIsOverheat(uint macErrCode)
        {
            return (macErrCode == 0x0305 // RFTC_ERR_AMBIENTTEMPTOOHOT
                || macErrCode == 0x0306 // RFTC_ERR_XCVRTEMPTOOHOT
                || macErrCode == 0x0307 // RFTC_ERR_PATEMPTOOHOT
                || macErrCode == 0x0308 // PADELTATEMPTOOBIG
            );
        }

        /// <summary>
        /// Mac Error Is Fatal Error
        /// </summary>
        /// <param name="macErrCode">Mac Error Code</param>
        /// <returns></returns>
        public bool MacErrorIsFatal(uint macErrCode)
        {
            return (macErrCode == 0x0305 // RFTC_ERR_AMBIENTTEMPTOOHOT
               || macErrCode == 0x0306 // RFTC_ERR_XCVRTEMPTOOHOT
               || macErrCode == 0x0307 // RFTC_ERR_PATEMPTOOHOT
               || macErrCode == 0x0308 // RFTC_ERR_PADELTATEMPTOOBIG
               || macErrCode == 0x0309 // RFTC_ERR_REVPWRLEVTOOHIGH   
           );
        }

        /// <summary>
        /// Mac Error Is Negligible
        /// </summary>
        /// <param name="macErrCode">Mac Error Code</param>
        /// <returns></returns>
        public bool MacErrorIsNegligible(uint macErrCode)
        {
            // According to Release notes 1.1, 1.2, 1.3
            return (macErrCode == 0x0310 || macErrCode == 0x0312);
        }

#endregion

#region ====================== Create Parameters ======================
        /// <summary>
        /// Create TagGroup parameter
        /// </summary>
        /// <param name="select">Specifies the state of the selected (SL) flag for tags that will have 
        /// the operation applied to them. </param>
        /// <param name="session">Specifies which inventory session flag (i.e., S0, S1, S2, or S3) 
        /// will be matched against the inventory state specified by target. </param>
        /// <param name="target">Specifies the state of the inventory session flag (i.e., A or B), 
        /// specified by session, for tags that will have the operation 
        /// applied to them. </param>
        /// <returns>TagGroup</returns>
        public TagGroup CreateTagGroup(Selected select, Session session, SessionTarget target)
        {
            TagGroup tg = new TagGroup();
            tg.selected = select;
            tg.session = session;
            tg.target = target;
            return tg;
        }
        /// <summary>
        /// Create FixedQ parameter
        /// </summary>
        /// <param name="qValue">The Q value to use. Valid values are 0-15, inclusive. </param>
        /// <param name="retryCount">Specifies the number of times to try another execution 
        /// of the singulation algorithm for the specified 
        /// session/target before either toggling the target (if 
        /// toggleTarget is non-zero) or terminating the 
        /// inventory/tag access operation.  Valid values are 0-
        /// 255, inclusive. </param>
        /// <param name="toggleTarget">A flag that indicates if, after performing the inventory cycle for 
        /// the specified target (i.e., A or B), if the target should be toggled 
        /// (i.e., A to B or B to A) and another inventory cycle run.  A non-
        /// zero value indicates that the target should be toggled.  A zero 
        /// value indicates that the target should not be toggled.  Note that 
        /// if the target is toggled, retryCount and 
        /// repeatUntilNoTags will also apply to the new target. </param>
        /// <param name="repeatUntilNoTags">A flag that indicates whether or not the singulation 
        /// algorithm should continue performing inventory rounds 
        /// until no tags are singulated.  A non-zero value indicates 
        /// that, for each execution of the singulation algorithm, 
        /// inventory rounds should be performed until no tags are 
        /// singulated.  A zero value indicates that a single 
        /// inventory round should be performed for each 
        /// execution of the singulation algorithm. </param>
        /// <returns>FixedQParms</returns>
        public FixedQParms CreateFixedQParms(uint qValue, uint retryCount, uint toggleTarget, uint repeatUntilNoTags)
        {
            FixedQParms fixQ = new FixedQParms();
            fixQ.qValue = qValue;
            fixQ.retryCount = retryCount;
            fixQ.toggleTarget = toggleTarget;
            fixQ.repeatUntilNoTags = repeatUntilNoTags;
            return fixQ;
        }
        /// <summary>
        /// Create DynamicQ parameter
        /// </summary>
        /// <param name="startQValue">The starting Q value to use.  Valid values are 0-15, inclusive.  
        /// startQValue must be greater than or equal to minQValue and 
        /// less than or equal to maxQValue. </param>
        /// <param name="maxQValue">The maximum Q value to use.  Valid values are 0-15, inclusive.  
        /// maxQValue must be greater than or equal to startQValue and 
        /// minQValue. </param>
        /// <param name="minQValue">The minimum Q value to use.  Valid values are 0-15, inclusive.  
        /// minQValue must be less than or equal to startQValue and 
        /// maxQValue. </param>
        /// <param name="retryCount">Specifies the number of times to try another execution 
        /// of the singulation algorithm for the specified 
        /// session/target before either toggling the target (if 
        /// toggleTarget is non-zero) or terminating the 
        /// inventory/tag access operation.  Valid values are 0-255, 
        /// inclusive. </param>
        /// <param name="toggleTarget">A flag that indicates if, after performing the inventory cycle for the 
        /// specified target (i.e., A or B), if the target should be toggled (i.e., 
        /// A to B or B to A) and another inventory cycle run.  A non-zero 
        /// value indicates that the target should be toggled.  A zero value 
        /// indicates that the target should not be toggled.  Note that if the 
        /// target is toggled, retryCount and maxQueryRepCount will 
        /// also apply to the new target. </param>
        /// <param name="thresholdMultiplier">The multiplier, specified in units of fourths (i.e., 0.25), that will be 
        /// applied to the Q-adjustment threshold as part of the dynamic-Q 
        /// algorithm.  For example, a value of 7 represents a multiplier of 
        /// 1.75.  See [MAC-EDS] for specifics on how the Q-adjustment 
        /// threshold is used in the dynamic Q algorithm.  Valid values are 0-
        /// 255, inclusive. </param>
        /// <returns>DynamicQThresholdParms</returns>
        public DynamicQParms CreateDynamicQParms(uint startQValue, uint maxQValue, uint minQValue, uint retryCount, uint toggleTarget, uint thresholdMultiplier)
        {
            DynamicQParms fixQ = new DynamicQParms();
            fixQ.startQValue = startQValue;
            fixQ.maxQValue = maxQValue;
            fixQ.minQValue = minQValue;
            fixQ.retryCount = retryCount;
            fixQ.toggleTarget = toggleTarget;
            fixQ.thresholdMultiplier = thresholdMultiplier;
            return fixQ;
        }
        /// <summary>
        /// Create SelectCriteria parms
        /// </summary>
        /// <param name="Offset">The offset, in bits, from the start of the memory bank, of the 
        /// first bit that will be matched against the mask.  If offset falls 
        /// beyond the end of the memory bank, the tag is considered 
        /// non-matching. </param>
        /// <param name="Count">The number of bits in the mask.  A length of zero will cause all 
        /// tags to match.  If (offset+count) falls beyond the end of 
        /// the memory bank, the tag is considered non-matching.  Valid 
        /// values are 0 to 255, inclusive. </param>
        /// <param name="bnk">The memory bank that contains the bits that will be compared 
        /// against the bit pattern specified in mask.  For a tag mask, 
        /// RFID_18K6C_MEMORY_BANK_RESERVED is not a valid value. </param>
        /// <param name="action">Specifies the action that will be applied to the tag populations (i.e, the 
        /// matching and non-matching tags). </param>
        /// <param name="target">Specifies what flag, selected (i.e., SL) or one of the four inventory 
        /// flags (i.e., S0, S1, S2, or S3), will be modified by the action. </param>
        /// <param name="enableTruncate">Specifies if, during singulation, a tag will respond to a subsequent 
        /// inventory operation with its entire Electronic Product Code (EPC) or 
        /// will only respond with the portion of the EPC that immediately follows 
        /// the bit pattern (as long as the bit pattern falls within the EPC – if the 
        /// bit pattern does not fall within the tag's EPC, the tag ignores the tag 
        /// partitioning operation2).  If this parameter is non-zero: 
        /// ?     bank must be RFID_18K6C_MEMORY_BANK_EPC. 
        /// ?     target must be RFID_18K6C_TARGET_SELECTED_FLAG. 
        /// This action must correspond to the last tag select operation issued 
        /// before the inventory operation or access command. </param>
        /// <param name="bTarget">A buffer that contains a left-justified bit array that represents 
        /// that bit pattern to match – i.e., the most significant bit of the bit 
        /// array appears in the most-significant bit (i.e., bit 7) of the first 
        /// byte of the buffer (i.e., mask[0]).  All bits beyond count are 
        /// ignored.  </param>
        /// <returns>SelectCriteria</returns>
        public SelectCriteria CreateSelectCriteria(uint Offset, uint Count, MemoryBank bnk, CSLibrary.Constants.Action action, CSLibrary.Constants.Target target, bool enableTruncate, byte[] bTarget)
        {
            byte[] New;
            if (bTarget == null)
            {
                New = new byte[32];
            }
            else if (bTarget.Length != 32)
            {
                New = new byte[32];
                for (int i = 0; i < bTarget.Length; i++)
                {
                    New[i] = bTarget[i];
                }
            }
            else
                New = bTarget;

            SelectCriteria SC = new SelectCriteria();
            SC.countCriteria = 1;
            SC.pCriteria = new SelectCriterion[1];
            //Action
            SelectAction SelAct = new SelectAction();
            SelAct.action = action;
            SelAct.enableTruncate = enableTruncate ? 1 : 0;
            SelAct.target = target;
            //Mask
            SelectMask SelMask = new SelectMask();
            SelMask.bank = bnk;
            SelMask.count = Count;
            SelMask.offset = Offset;
            SelMask.mask = (byte[])New.Clone();

            SC.pCriteria[0] = new SelectCriterion();
            SC.pCriteria[0].action = SelAct;
            SC.pCriteria[0].mask = SelMask;

            return SC;
        }
        /// <summary>
        /// Create PostMatchCriteria parms
        /// </summary>
        /// <param name="Offset">The offset in bits, from the start of the Electronic Product 
        /// Code (EPC), of the first bit that will be matched against the 
        /// mask.  If offset falls beyond the end of EPC, the tag is 
        /// considered non-matching. </param>
        /// <param name="Count">The number of bits in the mask.  A length of zero will cause 
        /// all tags to match.  If (offset+count) falls beyond the end 
        /// of the EPC, the tag is considered non-matching.  Valid 
        /// values are 0 to 496, inclusive. </param>
        /// <param name="match">Determines if the associated tag-protocol operation will be 
        /// applied to tags that match the mask or not.  A non-zero 
        /// value indicates that the tag-protocol operation should be 
        /// applied to tags that match the mask.  A value of zero 
        /// indicates that the tag-protocol operation should be applied 
        /// to tags that do not match the mask. </param>
        /// <param name="bTarget">A buffer that contains a left-justified bit array that represents 
        /// that bit pattern to match – i.e., the most significant bit of the 
        /// bit array appears in the most-significant bit (i.e., bit 7) of the 
        /// first byte of the buffer (i.e., mask[0]).  All bits beyond count 
        /// are ignored.  For example, if the application wished to find 
        /// tags with the following 16 bits 1011.1111.1010.0101, 
        /// starting at offset 20 in the Electronic Product Code, then the 
        /// fields would be set as follows: 
        /// offset  = 20 
        /// count   = 16 
        /// mask[0] = 0xBF (1011.1111) 
        /// mask[1] = 0xA5 (1010.0101) </param>
        /// <returns></returns>
        public SingulationCriteria CreatePostMatchCriteria(uint Offset, uint Count, bool match, byte[] bTarget)
        {
            byte[] New;
            if (bTarget == null)
            {
                New = new byte[62];
            }
            else if (bTarget.Length != 62)
            {
                New = new byte[62];
                for (int i = 0; i < bTarget.Length; i++)
                {
                    New[i] = bTarget[i];
                }
            }
            else
                New = bTarget;

            SingulationCriteria SC = new SingulationCriteria();
            SC.countCriteria = 1;
            SC.pCriteria = new SingulationCriterion[1];
            SC.pCriteria[0] = new SingulationCriterion();
            SC.pCriteria[0].match = (uint)(match ? 1 : 0);
            SC.pCriteria[0].mask = new SingulationMask();
            SC.pCriteria[0].mask.count = Count;
            SC.pCriteria[0].mask.offset = Offset;
            SC.pCriteria[0].mask.mask = (Byte[])New.Clone();

            return SC;
        }
#endregion

#region ====================== Coredll PInvoke ======================
        string ThreadErrorMsg = "";
        private void WaitToBusy()
        {
            DateTime timeout = DateTime.Now.AddSeconds(5);

            while (m_state != RFState.BUSY && timeout > DateTime.Now) Thread.Sleep(1);

            if (m_state != RFState.BUSY)
                ThreadErrorMsg = "Thread open fail";
        }

        private void WaitToIdle()
        {
            while (m_state != RFState.IDLE) Thread.Sleep(1);
        }
#endregion

#region ====================== Silicon Lab (Network interface) Command ======================
        /// <summary>
        /// Get Silicon Lab Application Version
        /// Notes:Not support in future version
        /// </summary>
        /// <returns></returns>
        public CSLibrary.Structures.Version GetC51AppVersion()
        {
            uint value = 0;
            CSLibrary.Structures.Version vers = new CSLibrary.Structures.Version();
            uint cmd = 0x00060000;
            Thread.Sleep(100);

            ReturnState(RadioReadGpioPins(cmd, ref value));
            
            vers.major = (value & 0xFF000000) >> 24;
            vers.minor = (value & 0x00FF0000) >> 16;
            vers.patch = (value & 0x0000FF00) >> 8;
            
            return vers;
        }

        /// <summary>
        /// Get Silicon Lab Bootloader Version
        /// Notes:Not support in future version
        /// </summary>
        /// <returns></returns>
        public CSLibrary.Structures.Version GetC51BootLoaderVersion()
        {
            CSLibrary.Structures.Version vers = new CSLibrary.Structures.Version();
            uint value = 0;
            uint cmd = 0x000a0000;
            Thread.Sleep(100);

            ReturnState(RadioReadGpioPins(cmd, ref value));

            vers.major = (value & 0xFF000000) >> 24;
            vers.minor = (value & 0x00FF0000) >> 16;
            vers.patch = (value & 0x0000FF00) >> 8;

            return vers;
        }


        CSLibrary.Structures.Version BLV = new CSLibrary.Structures.Version ();
        CSLibrary.Structures.Version IMV = new CSLibrary.Structures.Version();

        /// 
        /// 
        /// <summary>
        /// Get Silicon Version
        /// </summary>
        /// <returns></returns>
        /// 
        private bool GetSiliconVersion()
        {
            byte[] value = new byte[4];
            bool udpCmd = true;

            if (m_DeviceInterfaceType != INTERFACETYPE.IPV4)
                return false;

            // Get BootLoader Version;
            if (COMM_INTBOARDALT_Cmd(hostIP.Address, UDP_CMD.GET_BLV, new byte[0], value) != true)
            {
                udpCmd = false;

                //if (!NETBOARD_Cmd(TCP_CMD.BLV, new byte[0], value))
                return false;
            }

            BLV.major = (uint)(value[0] & 0xFF);
            BLV.minor = (uint)(value[1] & 0xFF);
            BLV.patch = (uint)(value[2] & 0xFF);

            // Get Application Version;
            if (udpCmd)
            {
                if (!COMM_INTBOARDALT_Cmd(hostIP.Address, UDP_CMD.GET_IMV, new byte[0], value))
                    return false;
            }
            else
            {
                //if (!NETBOARD_Cmd(TCP_CMD.IMV, new byte[0], value))
                    return false;
            }

            IMV.major = (uint)(value[0] & 0xFF);
            IMV.minor = (uint)(value[1] & 0xFF);
            IMV.patch = (uint)(value[2] & 0xFF);
            return true;

#if nouse
            byte[] value = new byte[4];
            bool udpCmd = true;
            int udpRetry = 3;

            if (m_DeviceInterfaceType != INTERFACETYPE.IPV4)
                return false;

            // Get BootLoader Version;

            while (udpRetry > 0)
            {
                if (COMM_INTBOARDALT_Cmd(hostIP.Address, UDP_CMD.GET_BLV, new byte[0], value))
                    break;

                udpRetry--;
            }

            if (udpRetry == 0)
            {
                udpCmd = false;

                if (!NETBOARD_Cmd(TCP_CMD.BLV, new byte[0], value))
                    return false;
            }

            BLV.major = (uint)(value[0] & 0xFF);
            BLV.minor = (uint)(value[1] & 0xFF);
            BLV.patch = (uint)(value[2] & 0xFF);

            // Get Application Version;
            if (udpCmd)
            {
                if (!COMM_INTBOARDALT_Cmd(hostIP.Address, UDP_CMD.GET_IMV, new byte[0], value))
                    return false;
            }
            else
            {
                if (!NETBOARD_Cmd(TCP_CMD.IMV, new byte[0], value))
                    return false;
            }

            IMV.major = (uint)(value[0] & 0xFF);
            IMV.minor = (uint)(value[1] & 0xFF);
            IMV.patch = (uint)(value[2] & 0xFF);
            return true;
#endif
        }

#if nouse
        /// <summary>
        /// Get Silicon Lab Bootloader Version
        /// </summary>
        /// <returns></returns>
        private bool GetBootLoaderVers()
        {
            byte[] value = new byte[4];

#if PORT_1516
            if (m_DeviceInterfaceType == INTERFACETYPE.IPV4)
            {
                if (NETBOARD_Cmd(TCP_CMD.BLV, new byte[0], value))
                {
                    BLV.major = (uint)(value[0] & 0xFF);
                    BLV.minor = (uint)(value[1] & 0xFF);
                    BLV.patch = (uint)(value[2] & 0xFF);

                    return true;
                }
            }
            return false;
#else
            
            //if (COMM_INTBOARDALT_Cmd(System.Net.IPAddress.Parse(ip).Address, UDP_CMD.GET_BLV, new byte[0], value))
            if (COMM_INTBOARDALT_Cmd(hostIP.Address, UDP_CMD.GET_BLV, new byte[0], value))
            {
                BLV.major = (uint)(value[0] & 0xFF);
                BLV.minor = (uint)(value[1] & 0xFF);
                BLV.patch = (uint)(value[2] & 0xFF);
                return true;
            }

            return false;

#endif
        }
#endif

        public CSLibrary.Structures.Version GetBootLoaderVersion()
        {
            CSLibrary.Structures.Version vers = new CSLibrary.Structures.Version();
            vers = BLV;
            return vers;
        }

        /// <summary>
        /// Get Silicon Lab Bootloader Version
        /// </summary>
        /// <param name="version">version</param>
        /// <returns></returns>
        public Result GetBootLoaderVersion(CSLibrary.Structures.Version version)
        {
            version = BLV;
            return Result.OK;
        }

#if nouse
        /// <summary>
        /// Get Silicon Lab Application Version
        /// </summary>
        /// <returns></returns>
        private bool GetImageVers()
        {
            byte[] value = new byte[4];

#if PORT_1516
            if (m_DeviceInterfaceType == INTERFACETYPE.IPV4)
            {

                if (NETBOARD_Cmd(TCP_CMD.IMV, new byte[0], value))
                {
                    IMV.major = (uint)(value[0] & 0xFF);
                    IMV.minor = (uint)(value[1] & 0xFF);
                    IMV.patch = (uint)(value[2] & 0xFF);
                    
                    return true;
                }
            }
            return false;;
#else
            if (COMM_INTBOARDALT_Cmd(hostIP.Address, UDP_CMD.GET_IMV, new byte[0], value))
            {
                IMV.major = (uint)(value[0] & 0xFF);
                IMV.minor = (uint)(value[1] & 0xFF);
                IMV.patch = (uint)(value[2] & 0xFF);
                return true;
            }

            return false;

#endif
        }
#endif

        /// <summary>
        /// Get Silicon Lab Application Version
        /// </summary>
        /// <returns></returns>
        public CSLibrary.Structures.Version GetImageVersion()
        {
            CSLibrary.Structures.Version vers = new CSLibrary.Structures.Version();

            vers = IMV;
//            vers.major = (uint)(value[0] & 0xFF);
//                vers.minor = (uint)(value[1] & 0xFF);
//                vers.patch = (uint)(value[2] & 0xFF);

            return vers;
        }

        /// <summary>
        /// Get Silicon Lab Application Version
        /// </summary>
        /// <param name="version">version</param>
        /// <returns></returns>
        public Result GetImageVersion(CSLibrary.Structures.Version version)
        {
            version = IMV;

            return Result.OK;
        }

#if nouse
        /// <summary>
        /// Get Device MAC Address
        /// </summary>
        /// <returns></returns>
        private Result GetMacAddress([In, Out] byte[] address)
        {
            m_macAddress

            if (address == null || address.Length != 6)
                address = new byte[6];

            return (m_Result = GetMACAddress(address));

            return Result.OK;
        }
#endif

        //
        //      Get/Set GPIO (TCP Command)
        //  

#if PORT_1516
        /// <summary>
        /// Get GPI0 status
        /// </summary>
        /// <param name="HL"></param>
        /// <returns></returns>
        public Result GetGPI0(ref int HL)
        {
            return Result.NOT_SUPPORTED;

            if (m_DeviceInterfaceType != INTERFACETYPE.IPV4)
                return Result.FAILURE;

            byte[] status = new byte[1];

            if (NETBOARD_Cmd(TCP_CMD.GPI0, new byte[0], status) != true)
                return Result.FAILURE;

            HL = status[0];

            return Result.OK;
//            return ReturnState(Native.RFID_GetGPI0(m_save_tcp_ip, ref HL));
        }
#endif

        /// <summary>
        /// Get GPI0 status
        /// </summary>
        /// <param name="ip">Target IP</param>
        /// <param name="HL"></param>
        /// <returns></returns>
        public Result GetGPI0(string ip, ref int HL)
        {
            return Result.NOT_SUPPORTED;
            //            return ReturnState(Native.RFID_GetGPI0(ip, ref HL));
        }

#if PORT_1516
        /// <summary>
        /// Get GPI1 status
        /// </summary>
        /// <param name="HL"></param>
        /// <returns></returns>
        public Result GetGPI1(ref int HL)
        {
            return Result.NOT_SUPPORTED;

            if (m_DeviceInterfaceType != INTERFACETYPE.IPV4)
                return Result.FAILURE;

            byte[] status = new byte[1];

            if (NETBOARD_Cmd(TCP_CMD.GPI1, new byte[0], status) != true)
                return Result.FAILURE;

            HL = status[0];

            return Result.OK;
            //            return ReturnState(Native.RFID_GetGPI1(m_save_tcp_ip, ref HL));
        }

        /// <summary>
        /// Get GPI1 status
        /// </summary>
        /// <param name="ip">Target IP</param>
        /// <param name="HL"></param>
        /// <returns></returns>
        public Result GetGPI1(string ip, ref int HL)
        {
            return Result.NOT_SUPPORTED;
            //            return ReturnState(Native.RFID_GetGPI1(ip, ref HL));
        }
#endif

#if PORT_1516
        /// <summary>
        /// Get GPO0 status
        /// </summary>
        /// <param name="HL"></param>
        /// <returns></returns>
        public Result GetGPO0(ref int HL)
        {
            return Result.NOT_SUPPORTED;

            byte[] status = new byte[1];

            if (NETBOARD_Cmd(TCP_CMD.GPO0, new byte[0], status) != true)
                return Result.FAILURE;

            HL = status[0];

            return Result.OK;
            //            return ReturnState(Native.RFID_GetGPO0(m_save_tcp_ip, ref HL));
        }

        /// <summary>
        /// Get GPO0 status
        /// </summary>
        /// <param name="ip">Target IP</param>
        /// <param name="HL"></param>
        /// <returns></returns>
        public Result GetGPO0(string ip, ref int HL)
        {
            return Result.NOT_SUPPORTED;
        }
#endif

#if PORT_1516
        /// <summary>
        /// Get GPO1 status
        /// </summary>
        /// <param name="HL"></param>
        /// <returns></returns>
        public Result GetGPO1(ref int HL)
        {
            return Result.NOT_SUPPORTED;

/*            byte[] status = new byte[1];

            if (NETBOARD_Cmd(TCP_CMD.GPO1, new byte[0], status) != true)
                return Result.FAILURE;

            HL = status[0];

            return Result.OK;
            //            return ReturnState(Native.RFID_GetGPO1(m_save_tcp_ip, ref HL));
*/
        }

        /// <summary>
        /// Get GPO1 status
        /// </summary>
        /// <param name="ip">Target IP</param>
        /// <param name="HL"></param>
        /// <returns></returns>
        public Result GetGPO1(string ip, ref int HL)
        {
            return Result.NOT_SUPPORTED;
            //            return ReturnState(Native.RFID_GetGPO1(ip, ref HL));
        }
        
        /// <summary>
        /// Get LED status
        /// </summary>
        /// <param name="HL"></param>
        /// <returns></returns>
        public Result GetLED(ref int HL)
        {
            byte [] status = new byte[1];

            if (NETBOARD_Cmd(TCP_CMD.LED, new byte[0], status) != true)
                return Result.FAILURE;

            HL = status[0];

            return Result.OK;
            //            return ReturnState(Native.RFID_GetLED(m_save_tcp_ip, ref HL));
        }

        /// <summary>
        /// Get LED status
        /// </summary>
        /// <param name="ip">Target IP</param>
        /// <param name="HL"></param>
        /// <returns></returns>
        public Result GetLED(string ip, ref int HL)
        {
            return Result.NOT_SUPPORTED;
            //            return ReturnState(Native.RFID_GetLED(ip, ref HL));
        }
#endif

#if PORT_1516
        /// <summary>
        /// Set GPI0 status
        /// </summary>
        /// <param name="HL"></param>
        /// <returns></returns>
        public Result SetGPI0(int HL)
        {
            return Result.NOT_SUPPORTED;

            byte[] Emptybyte = new byte[0];
            byte[] status = new byte[1];

            if (HL == 0)
            {
                if (NETBOARD_Cmd(TCP_CMD.GPO0_L, Emptybyte, status) != true)
                    return Result.FAILURE;
            }
            else
            {
                if (NETBOARD_Cmd(TCP_CMD.GPO0_H, Emptybyte, status) != true)
                    return Result.FAILURE;
            }

            HL = status[0];

            return Result.OK;

            //            return ReturnState(Native.RFID_SetGPO0(m_save_tcp_ip, HL));
        }

        /// <summary>
        /// Set GPI0 status
        /// </summary>
        /// <param name="ip">Target IP</param>
        /// <param name="HL"></param>
        /// <returns></returns>
        public Result SetGPI0(string ip, int HL)
        {
            return Result.NOT_SUPPORTED;
            //            return ReturnState(Native.RFID_SetGPO0(ip, HL));
        }
#endif

#if PORT_1516
        /// <summary>
        /// Set GPI1 status
        /// </summary>
        /// <param name="HL"></param>
        /// <returns></returns>
        public Result SetGPI1(int HL)
        {
            return Result.NOT_SUPPORTED;

            byte[] Emptybyte = new byte[0];
            byte[] Status = new byte[1];

            if (HL == 0)
            {
                if (NETBOARD_Cmd(TCP_CMD.GPO1_L, Emptybyte, Status) != true)
                    return Result.FAILURE;
            }
            else
            {
                if (NETBOARD_Cmd(TCP_CMD.GPO1_H, Emptybyte, Status) != true)
                    return Result.FAILURE;
            }

            return Result.OK;
//            return ReturnState(Native.RFID_SetGPO1(m_save_tcp_ip, HL));
        }

        /// <summary>
        /// Set GPI1 status
        /// </summary>
        /// <param name="ip">Target IP</param>
        /// <param name="HL"></param>
        /// <returns></returns>
        public Result SetGPI1(string ip, int HL)
        {
            return Result.NOT_SUPPORTED;
//            return ReturnState(Native.RFID_SetGPO1(ip, HL));
        }
#endif

#if PORT_1516
        /// <summary>
        /// Set Led status
        /// </summary>
        /// <param name="HL"></param>
        /// <returns></returns>
        public Result SetLED(int HL)
        {
            byte[] Emptybyte = new byte[0];
            byte[] Status = new byte[1];

            if (HL == 0)
            {
                if (NETBOARD_Cmd(TCP_CMD.LED_L, Emptybyte, Status) != true)
                    return Result.FAILURE;
            }
            else
            {
                if (NETBOARD_Cmd(TCP_CMD.LED_H, Emptybyte, Status) != true)
                    return Result.FAILURE;
            }

            return Result.OK;
            //            return ReturnState(Native.RFID_SetLED(m_save_tcp_ip, HL));
        }

        /// <summary>
        /// Set Led status
        /// </summary>
        /// <param name="ip">Target IP</param>
        /// <param name="HL"></param>
        /// <returns></returns>
        public Result SetLED(string ip, int HL)
        {
            return Result.NOT_SUPPORTED;
//            return ReturnState(Native.RFID_SetLED(ip, HL));
        }
#endif

        //
        //      Interface Board (8051  Function)
        //  

#if PORT_1516
        /// <summary>
        /// goto Standby mode.
        /// </summary>
        /// <returns></returns>
        public Result StandbyMode()
        {
            byte [] status = new byte[1];

            if (NETBOARD_Cmd(TCP_CMD.STDBY, new byte[0], status) != true)
                return Result.FAILURE;

            return Result.OK;
#if USE_LOADLIBRARY
            return ReturnState(Native.RFID_StandbyMode(dllModule, m_save_tcp_ip));
#else
            return Result.OK;
//            return ReturnState(Native.RFID_StandbyMode(m_save_tcp_ip));
#endif
        }
        /// <summary>
        /// goto Standby mode.
        /// </summary>
        /// <returns></returns>
        public Result StandbyMode(string ip)
        {
            return Result.NOT_SUPPORTED;
#if USE_LOADLIBRARY
            return ReturnState(Native.RFID_StandbyMode(dllModule, m_save_tcp_ip));
#else
            return Result.OK;
//            return ReturnState(Native.RFID_StandbyMode(ip));
#endif
        }
#endif

#if PORT_1516
        /// <summary>
        /// AutoReset
        /// </summary>
        /// <returns></returns>
        public static Result AutoReset(string ip, bool on)
        {
            byte[] status = new byte[1];

            if (on)
                status[0] = 1;
            else
                status[0] = 0;

            if (NETBOARD_Cmd(System.Net.IPAddress.Parse(ip), TCP_CMD.AUTO_RESET, status, new byte[1]) != true)
                return Result.FAILURE;

            return Result.OK;
        }

        /// <summary>
        /// AutoReset
        /// </summary>
        /// <returns></returns>
        public Result AutoReset(bool on)
        {
            byte[] status = new byte[1];

            if (on)
                status[0] = 1;
            else
                status[0] = 0;

            if (NETBOARD_Cmd(TCP_CMD.AUTO_RESET, status, new byte[1]) != true)
                return Result.FAILURE;

            return Result.OK;
        }

        /// <summary>
        /// AutoReset
        /// </summary>
        /// <returns></returns>
        public static Result AutoResetOn(string ip)
        {
            return AutoReset(ip, true);
        }

        /// <summary>
        /// AutoReset
        /// </summary>
        /// <returns></returns>
        public static Result AutoResetOff(string ip)
        {
            return AutoReset (ip, false);
        }
#endif

        /// <summary>
        /// Check Status
        /// </summary>
        /// <returns></returns>
        public Result CheckStatus([In, Out] ref DEVICE_STATUS status)
        {
            byte[] revBuff = new byte[10];
            int retry = 3;

            while (retry > 3)
            {
                if (COMM_INTBOARDALT_Cmd(UDP_CMD.CHECK_STATUS, new byte[0], revBuff))
                {
                    status.IsPowerOn = revBuff[0] != 00;
                    status.IsErrorReset = revBuff[1] != 00;
                    status.IsKeepAlive = revBuff[2] != 00;
                    status.IsConnected = revBuff[3] != 00;
                    status.day = revBuff[4];
                    status.hrs = revBuff[5];
                    status.min = revBuff[6];
                    status.sec = revBuff[7];
                    status.IsCRCFilter = revBuff[8] != 00;

                    return Result.OK;
                }

                retry--;
                Thread.Sleep(1000);
            }

            return Result.FAILURE;

#if nouse
		    byte [] revBuff = new byte [10];

            if (COMM_INTBOARDALT_Cmd(UDP_CMD.CHECK_STATUS, new byte[0], revBuff) != true)
                return Result.FAILURE;

            status.IsPowerOn = revBuff[0] != 00;
            status.IsErrorReset = revBuff[1] != 00;
            status.IsKeepAlive = revBuff[2] != 00;
            status.IsConnected = revBuff[3] != 00;
            status.day = revBuff[4];
            status.hrs = revBuff[5];
            status.min = revBuff[6];
            status.sec = revBuff[7];
            status.IsCRCFilter = revBuff[8] != 00;

            return Result.OK;
#endif
        }

        /// <summary>
        /// Force reset device
        /// </summary>
        /// <returns></returns>
        public Result ForceReset()
        {
            if (COMM_INTBOARDALT_Cmd(UDP_CMD.FORCE_RESET, new byte[0], new byte[0]))
            {
                COMM_Disconnect();
                System.Threading.Thread.Sleep(6000);
                COMM_Connect(m_DeviceName);

                return Result.OK;
            }

            return Result.FAILURE;
        }

        /// <summary>
        /// TurnOn UDP Keep-Alive
        /// </summary>
        /// <returns></returns>
        public Result UDPKeepAliveOn()
        {
            if (COMM_INTBOARDALT_Cmd(UDP_CMD.UDP_KEEPALIVE_ON, new byte[0], new byte[0]))
                return Result.OK;

            return Result.FAILURE;
        }

        /// <summary>
        ///  TurnOff UDP Keep-Alive
        /// </summary>
        /// <returns></returns>
        public Result UDPKeepAliveOff()
        {
            if (COMM_INTBOARDALT_Cmd(UDP_CMD.UDP_KEEPALIVE_OFF, new byte[0], new byte[0]))
                return Result.OK;

            return Result.FAILURE;
        }

#if PORT_1516
        /// <summary>
        ///  TurnOn CRC Error Checking
        /// </summary>
        /// <returns></returns>
        public Result CrcFilterOn()
        {
            byte[] SendBuf = new byte[1];

            SendBuf[0] = 1;
            NETBOARD_Cmd(TCP_CMD.CRC_FILTER, SendBuf, new byte[1]);

            return Result.OK;
        }
        /// <summary>
        ///  TurnOff CRC Error Checking
        /// </summary>
        /// <returns></returns>
        public Result CrcFilterOff()
        {
            byte[] SendBuf = new byte[1];

            SendBuf[0] = 0;
            NETBOARD_Cmd(TCP_CMD.CRC_FILTER, SendBuf, new byte[1]);

            return Result.OK;
        }
#endif

        /// <summary>
        /// Check Status
        /// </summary>
        /// <returns></returns>
        public static Result CheckStatus(string devicIp, string comIp, [In, Out] ref DEVICE_STATUS status)
        {
            byte[] revBuff = new byte[10];
            int retry = 3;

            while (retry > 0)
            {
                if (COMM_INTBOARDALT_Cmd(System.Net.IPAddress.Parse(devicIp).Address, System.Net.IPAddress.Parse(comIp).Address, UDP_CMD.CHECK_STATUS, new byte[0], revBuff))
                {
                    status.IsPowerOn = revBuff[0] != 00;
                    status.IsErrorReset = revBuff[1] != 00;
                    status.IsKeepAlive = revBuff[2] != 00;
                    status.IsConnected = revBuff[3] != 00;
                    status.day = revBuff[4];
                    status.hrs = revBuff[5];
                    status.min = revBuff[6];
                    status.sec = revBuff[7];
                    status.IsCRCFilter = revBuff[8] != 00;

                    return Result.OK;
                }

                retry--;
                Thread.Sleep(1000);
            }

            return Result.FAILURE;
        }

        /// <summary>
        /// Check Status
        /// </summary>
        /// <returns></returns>
        public static Result CheckStatus(string ip, [In, Out] ref DEVICE_STATUS status)
        {
            byte[] revBuff = new byte[10];
            int retry = 3;

            while (retry > 0)
            {
                if (COMM_INTBOARDALT_Cmd(System.Net.IPAddress.Parse(ip).Address, UDP_CMD.CHECK_STATUS, new byte[0], revBuff))
                {
                    status.IsPowerOn = revBuff[0] != 00;
                    status.IsErrorReset = revBuff[1] != 00;
                    status.IsKeepAlive = revBuff[2] != 00;
                    status.IsConnected = revBuff[3] != 00;
                    status.day = revBuff[4];
                    status.hrs = revBuff[5];
                    status.min = revBuff[6];
                    status.sec = revBuff[7];
                    status.IsCRCFilter = revBuff[8] != 00;

                    return Result.OK;
                }

                retry--;
                Thread.Sleep(1000);
            }

            return Result.FAILURE;

#if nouse
            byte[] revBuff = new byte[10];

            if (COMM_INTBOARDALT_Cmd(System.Net.IPAddress.Parse(ip).Address, UDP_CMD.CHECK_STATUS, new byte[0], revBuff) != true)
                return Result.FAILURE;

            status.IsPowerOn = revBuff[0] != 00;
            status.IsErrorReset = revBuff[1] != 00;
            status.IsKeepAlive = revBuff[2] != 00;
            status.IsConnected = revBuff[3] != 00;
            status.day = revBuff[4];
            status.hrs = revBuff[5];
            status.min = revBuff[6];
            status.sec = revBuff[7];
            status.IsCRCFilter = revBuff[8] != 00;

            return Result.OK;
#endif
        }

        /// <summary>
        /// Force reset device
        /// </summary>
        /// <returns></returns>
        public static Result ForceReset(string devicIp, string comIp)
        {
            if (COMM_INTBOARDALT_Cmd(System.Net.IPAddress.Parse(devicIp).Address, System.Net.IPAddress.Parse(comIp).Address, UDP_CMD.FORCE_RESET, new byte[0], new byte[0]))
            {
                System.Threading.Thread.Sleep(4000);
                return Result.OK;
            }

            return Result.FAILURE;
        }

        /// <summary>
        /// Force reset device
        /// </summary>
        /// <returns></returns>
        public static Result ForceReset(string ip)
        {
            if (COMM_INTBOARDALT_Cmd(System.Net.IPAddress.Parse(ip).Address, UDP_CMD.FORCE_RESET, new byte[0], new byte[0]))
            {
                System.Threading.Thread.Sleep(4000);
                return Result.OK;
            }

            return Result.FAILURE;
        }

        /// <summary>
        /// TurnOn UDP Keep-Alive
        /// </summary>
        /// <returns></returns>
        public static Result UDPKeepAliveOn(string ip)
        {
            if (COMM_INTBOARDALT_Cmd(System.Net.IPAddress.Parse(ip).Address, UDP_CMD.UDP_KEEPALIVE_ON, new byte[0], new byte[0]))
                return Result.OK;

            return Result.FAILURE;
        }

        /// <summary>
        ///  TurnOff UDP Keep-Alive
        /// </summary>
        /// <returns></returns>
        public static Result UDPKeepAliveOff(string ip)
        {
            if (COMM_INTBOARDALT_Cmd(System.Net.IPAddress.Parse(ip).Address, UDP_CMD.UDP_KEEPALIVE_OFF, new byte[0], new byte[0]))
                return Result.OK;

            return Result.FAILURE;
        }

#if PORT_1516
        /// <summary>
        ///  TurnOo RFID
        /// </summary>
        /// <remarks>Don't call this after Reader startup</remarks>
        public static Result RFIDPowerOn(string ip)
        {
            return Result.NOT_SUPPORTED;
            //COMM_INTBOARDALT_Cmd(System.Net.IPAddress.Parse(ip).Address, UDP_CMD.   .UDP_KEEPALIVE_OFF, new byte[0], new byte[0]);
            //return Result.OK;
//            return ReturnState(Native.RFID_POWER(ip, 1));
        }

        /// <summary>
        ///  TurnOff RFID
        /// </summary>
        /// <remarks>Don't call this after Reader startup</remarks>
        public static Result RFIDPowerOff(string ip)
        {
            return Result.NOT_SUPPORTED;
            //return ReturnState(Native.RFID_POWER(ip, 0));
        }
        /// <summary>
        ///  TurnOn CRC Error Checking
        /// </summary>
        /// <returns></returns>
        public static Result CrcFilterOn(string ip)
        {
            byte[] SendBuf = new byte[1];
            SendBuf[0] = 1;
            
            if (NETBOARD_Cmd (System.Net.IPAddress.Parse(ip), TCP_CMD.CRC_FILTER, SendBuf, new byte[1]))
                return Result.OK;

            return Result.FAILURE;
        }
        /// <summary>
        ///  TurnOff CRC Error Checking
        /// </summary>
        /// <returns></returns>
        public static Result CrcFilterOff(string ip)
        {
            byte[] SendBuf = new byte[1];
            SendBuf[0] = 0;

            if (NETBOARD_Cmd(System.Net.IPAddress.Parse(ip), TCP_CMD.CRC_FILTER, SendBuf, new byte[1]))
                return Result.OK;

            return Result.FAILURE;
        }
#endif

        //
        //      Set/Get GPIO Status (UDP Command)
        //  

        /// <summary>
        ///  Check GPI port status
        /// </summary>
        /// <returns></returns>
        public static Result GetGPIStatus(string ip, ref bool GPI0, ref bool GPI1, ref bool GPI2, ref bool GPI3)
        {
            byte[] status = new byte[2];

            COMM_INTBOARDALT_Cmd(System.Net.IPAddress.Parse(ip).Address, UDP_CMD.GET_GPI_STATUS, new byte[0], status);

            GPI0 = (status[0] == 1);
            GPI1 = (status[1] == 1);

            COMM_INTBOARDALT_Cmd(System.Net.IPAddress.Parse(ip).Address, UDP_CMD.GET_GPI23_STATUS, new byte[0], status);

            GPI2 = (status[0] == 1);
            GPI3 = (status[1] == 1);

            return Result.OK;
        }
        /// <summary>
        ///  Check GPI port status
        /// </summary>
        /// <returns></returns>
        public static Result GetGPIStatus(string ip, ref bool GPI0, ref bool GPI1)
        {
            byte[] status = new byte[2];

            COMM_INTBOARDALT_Cmd(System.Net.IPAddress.Parse(ip).Address, UDP_CMD.GET_GPI_STATUS, new byte[0], status);

            GPI0 = (status[0] == 1);
            GPI1 = (status[1] == 1);

            return Result.OK;
        }
        /// <summary>
        ///  Check GPI0 port status
        /// </summary>
        /// <returns></returns>
        public static Result GetGPI0Status(string ip, ref bool GPI0)
        {
            byte[] status = new byte[2];

            COMM_INTBOARDALT_Cmd(System.Net.IPAddress.Parse(ip).Address, UDP_CMD.GET_GPI_STATUS, new byte[0], status);

            GPI0 = (status[0] == 1);

            return Result.OK;
        }
        /// <summary>
        ///  Check GPI1 port status
        /// </summary>
        /// <returns></returns>
        public static Result GetGPI1Status(string ip, ref bool GPI1)
        {
            byte[] status = new byte[2];

            COMM_INTBOARDALT_Cmd(System.Net.IPAddress.Parse(ip).Address, UDP_CMD.GET_GPI_STATUS, new byte[0], status);

            GPI1 = (status[1] == 1);

            return Result.OK;
        }
        /// <summary>
        ///  Check GPI0 port status
        /// </summary>
        /// <returns></returns>
        public static Result GetGPI2Status(string ip, ref bool GPI2)
        {
            byte[] status = new byte[2];

            COMM_INTBOARDALT_Cmd(System.Net.IPAddress.Parse(ip).Address, UDP_CMD.GET_GPI23_STATUS, new byte[0], status);

            GPI2 = (status[0] == 1);

            return Result.OK;
        }
        /// <summary>
        ///  Check GPI1 port status
        /// </summary>
        /// <returns></returns>
        public static Result GetGPI3Status(string ip, ref bool GPI3)
        {
            byte[] status = new byte[2];

            COMM_INTBOARDALT_Cmd(System.Net.IPAddress.Parse(ip).Address, UDP_CMD.GET_GPI23_STATUS, new byte[0], status);

            GPI3 = (status[1] == 1);

            return Result.OK;
        }
        /// <summary>
        ///  Check GPI port status
        /// </summary>
        /// <returns></returns>
        public Result GetGPIStatus(ref bool GPI0, ref bool GPI1, ref bool GPI2, ref bool GPI3)
        {
            if (m_DeviceInterfaceType != INTERFACETYPE.IPV4)
                return Result.CURRENTLY_NOT_ALLOWED;

            byte[] status = new byte[2];

            COMM_INTBOARDALT_Cmd(UDP_CMD.GET_GPI_STATUS, new byte[0], status);

            GPI0 = (status[0] == 1);
            GPI1 = (status[1] == 1);

            COMM_INTBOARDALT_Cmd(UDP_CMD.GET_GPI23_STATUS, new byte[0], status);

            GPI2 = (status[0] == 1);
            GPI3 = (status[1] == 1);

            return Result.OK;
        }
        /// <summary>
        ///  Check GPI port status
        /// </summary>
        /// <returns></returns>
        public Result GetGPIStatus(ref bool GPI0, ref bool GPI1)
        {
            if (m_DeviceInterfaceType != INTERFACETYPE.IPV4)
                return Result.CURRENTLY_NOT_ALLOWED;

            byte[] status = new byte[2];

            COMM_INTBOARDALT_Cmd(UDP_CMD.GET_GPI_STATUS, new byte[0], status);

            GPI0 = (status[0] == 1);
            GPI1 = (status[1] == 1);

            return Result.OK;
        }
        /// <summary>
        ///  Check GPI0 port status
        /// </summary>
        /// <returns></returns>
        public Result GetGPI0Status(ref bool GPI0)
        {
            if (m_DeviceInterfaceType != INTERFACETYPE.IPV4)
                return Result.CURRENTLY_NOT_ALLOWED;

            byte[] status = new byte[2];

            COMM_INTBOARDALT_Cmd(UDP_CMD.GET_GPI_STATUS, new byte[0], status);

            GPI0 = (status[0] == 1);

            return Result.OK;
        }
        /// <summary>
        ///  Check GPI1 port status
        /// </summary>
        /// <returns></returns>
        public Result GetGPI1Status(ref bool GPI1)
        {
            if (m_DeviceInterfaceType != INTERFACETYPE.IPV4)
                return Result.CURRENTLY_NOT_ALLOWED;

            byte[] status = new byte[2];

            COMM_INTBOARDALT_Cmd(UDP_CMD.GET_GPI_STATUS, new byte[0], status);

            GPI1 = (status[1] == 1);

            return Result.OK;
        }
        /// <summary>
        ///  Check GPI0 port status
        /// </summary>
        /// <returns></returns>
        public Result GetGPI2Status(ref bool GPI2)
        {
            if (m_DeviceInterfaceType != INTERFACETYPE.IPV4)
                return Result.CURRENTLY_NOT_ALLOWED;

            byte[] status = new byte[2];

            COMM_INTBOARDALT_Cmd(UDP_CMD.GET_GPI23_STATUS, new byte[0], status);

            GPI2 = (status[0] == 1);

            return Result.OK;
        }
        /// <summary>
        ///  Check GPI1 port status
        /// </summary>
        /// <returns></returns>
        public Result GetGPI3Status(ref bool GPI3)
        {
            if (m_DeviceInterfaceType != INTERFACETYPE.IPV4)
                return Result.CURRENTLY_NOT_ALLOWED;

            byte[] status = new byte[2];

            COMM_INTBOARDALT_Cmd(UDP_CMD.GET_GPI23_STATUS, new byte[0], status);

            GPI3 = (status[1] == 1);

            return Result.OK;
        }

        /// <summary>
        ///  Check GPO port status
        /// </summary>
        /// <returns></returns>
        public static Result GetGPOStatus(string ip, ref bool GPO0, ref bool GPO1, ref bool GPO2, ref bool GPO3)
        {
            byte[] status = new byte[2];

            COMM_INTBOARDALT_Cmd(System.Net.IPAddress.Parse(ip).Address, UDP_CMD.GET_GPO_STATUS, new byte[0], status);

            GPO0 = (status[0] == 1);
            GPO1 = (status[1] == 1);

            COMM_INTBOARDALT_Cmd(System.Net.IPAddress.Parse(ip).Address, UDP_CMD.GET_GPO23_STATUS, new byte[0], status);

            GPO2 = (status[0] == 1);
            GPO3 = (status[1] == 1);

            return Result.OK;
        }
        /// <summary>
        ///  Check GPO port status
        /// </summary>
        /// <returns></returns>
        public static Result GetGPOStatus(string ip, ref bool GPO0, ref bool GPO1)
        {
            byte[] status = new byte[2];

            COMM_INTBOARDALT_Cmd(System.Net.IPAddress.Parse(ip).Address, UDP_CMD.GET_GPO_STATUS, new byte[0], status);

            GPO0 = (status[0] == 1);
            GPO1 = (status[1] == 1);

            return Result.OK;
        }
        /// <summary>
        ///  Check GPO0 port status
        /// </summary>
        /// <returns></returns>
        public static Result GetGPO0Status(string ip, ref bool GPO0)
        {
            byte[] status = new byte[2];

            COMM_INTBOARDALT_Cmd(System.Net.IPAddress.Parse(ip).Address, UDP_CMD.GET_GPO_STATUS, new byte[0], status);

            GPO0 = (status[0] == 1);

            return Result.OK;
        }
        /// <summary>
        ///  Check GPO1 port status
        /// </summary>
        /// <returns></returns>
        public static Result GetGPO1Status(string ip, ref bool GPO1)
        {
            byte[] status = new byte[2];

            COMM_INTBOARDALT_Cmd(System.Net.IPAddress.Parse(ip).Address, UDP_CMD.GET_GPO_STATUS, new byte[0], status);

            GPO1 = (status[1] == 1);

            return Result.OK;
        }
        /// <summary>
        ///  Check GPO0 port status
        /// </summary>
        /// <returns></returns>
        public static Result GetGPO2Status(string ip, ref bool GPO2)
        {
            byte[] status = new byte[2];

            COMM_INTBOARDALT_Cmd(System.Net.IPAddress.Parse(ip).Address, UDP_CMD.GET_GPO23_STATUS, new byte[0], status);

            GPO2 = (status[0] == 1);

            return Result.OK;
        }
        /// <summary>
        ///  Check GPO1 port status
        /// </summary>
        /// <returns></returns>
        public static Result GetGPO3Status(string ip, ref bool GPO3)
        {
            byte[] status = new byte[2];

            COMM_INTBOARDALT_Cmd(System.Net.IPAddress.Parse(ip).Address, UDP_CMD.GET_GPO23_STATUS, new byte[0], status);

            GPO3 = (status[1] == 1);

            return Result.OK;
        }
        /// <summary>
        ///  Check GPO port status
        /// </summary>
        /// <returns></returns>
        public Result GetGPOStatus(ref bool GPO0, ref bool GPO1, ref bool GPO2, ref bool GPO3)
        {
            if (m_DeviceInterfaceType != INTERFACETYPE.IPV4)
                return Result.CURRENTLY_NOT_ALLOWED;

            byte[] status = new byte[2];

            COMM_INTBOARDALT_Cmd(UDP_CMD.GET_GPO_STATUS, new byte[0], status);

            GPO0 = (status[0] == 1);
            GPO1 = (status[1] == 1);

            COMM_INTBOARDALT_Cmd(UDP_CMD.GET_GPO23_STATUS, new byte[0], status);

            GPO2 = (status[0] == 1);
            GPO3 = (status[1] == 1);

            return Result.OK;
        }
        /// <summary>
        ///  Check GPO port status
        /// </summary>
        /// <returns></returns>
        public Result GetGPOStatus(ref bool GPO0, ref bool GPO1)
        {
            if (m_DeviceInterfaceType != INTERFACETYPE.IPV4)
                return Result.CURRENTLY_NOT_ALLOWED;

            byte[] status = new byte[2];

            COMM_INTBOARDALT_Cmd(UDP_CMD.GET_GPO_STATUS, new byte[0], status);

            GPO0 = (status[0] == 1);
            GPO1 = (status[1] == 1);

            return Result.OK;
        }
        /// <summary>
        ///  Check GPO0 port status
        /// </summary>
        /// <returns></returns>
        public Result GetGPO0Status(ref bool GPO0)
        {
            if (m_DeviceInterfaceType != INTERFACETYPE.IPV4)
                return Result.CURRENTLY_NOT_ALLOWED;

            byte[] status = new byte[2];

            COMM_INTBOARDALT_Cmd(UDP_CMD.GET_GPO_STATUS, new byte[0], status);

            GPO0 = (status[0] == 1);

            return Result.OK;
        }
        /// <summary>
        ///  Check GPO1 port status
        /// </summary>
        /// <returns></returns>
        public Result GetGPO1Status(ref bool GPO1)
        {
            if (m_DeviceInterfaceType != INTERFACETYPE.IPV4)
                return Result.CURRENTLY_NOT_ALLOWED;

            byte[] status = new byte[2];

            COMM_INTBOARDALT_Cmd(UDP_CMD.GET_GPO_STATUS, new byte[0], status);

            GPO1 = (status[1] == 1);

            return Result.OK;

            return Result.NOT_SUPPORTED;
        }
        /// <summary>
        ///  Check GPO0 port status
        /// </summary>
        /// <returns></returns>
        public Result GetGPO2Status(ref bool GPO2)
        {
            if (m_DeviceInterfaceType != INTERFACETYPE.IPV4)
                return Result.CURRENTLY_NOT_ALLOWED;

            byte[] status = new byte[2];

            COMM_INTBOARDALT_Cmd(UDP_CMD.GET_GPO23_STATUS, new byte[0], status);

            GPO2 = (status[0] == 1);

            return Result.OK;
        }
        /// <summary>
        ///  Check GPO1 port status
        /// </summary>
        /// <returns></returns>
        public Result GetGPO3Status(ref bool GPO3)
        {
            if (m_DeviceInterfaceType != INTERFACETYPE.IPV4)
                return Result.CURRENTLY_NOT_ALLOWED;

            byte[] status = new byte[2];

            COMM_INTBOARDALT_Cmd(UDP_CMD.GET_GPO23_STATUS, new byte[0], status);

            GPO3 = (status[1] == 1);

            return Result.OK;

            return Result.NOT_SUPPORTED;
        }
        /// <summary>
        ///  Set GPO0 port status
        /// </summary>
        /// <returns></returns>
        public Result SetGPO0Status(bool GPO0)
        {
            if (m_DeviceInterfaceType != INTERFACETYPE.IPV4)
                return Result.CURRENTLY_NOT_ALLOWED;

            byte[] status = new byte[1];

            if (GPO0 == true)
                status[0] = 1;

            COMM_INTBOARDALT_Cmd (UDP_CMD.SET_GPO0_STATUS, status, new byte[0]);

            return Result.OK;
        }
        /// <summary>
        ///  Set GPO0 port status
        /// </summary>
        /// <returns></returns>
        public static Result SetGPO0Status(string ip, bool GPO0)
        {
            byte[] status = new byte[1];

            if (GPO0 == true)
                status[0] = 1;

            COMM_INTBOARDALT_Cmd (System.Net.IPAddress.Parse(ip).Address, UDP_CMD.SET_GPO0_STATUS, status, new byte[0]);

            return Result.OK;
        }
        /// <summary>
        ///  Set GPO1 port status
        /// </summary>
        /// <returns></returns>
        public Result SetGPO1Status(bool GPO1)
        {
            if (m_DeviceInterfaceType != INTERFACETYPE.IPV4)
                return Result.CURRENTLY_NOT_ALLOWED;

            byte[] status = new byte[1];

            if (GPO1 == true)
                status[0] = 1;

            COMM_INTBOARDALT_Cmd(UDP_CMD.SET_GPO1_STATUS, status, new byte[0]);

            return Result.OK;
        }
        /// <summary>
        ///  Set GPO1 port status
        /// </summary>
        /// <returns></returns>
        public static Result SetGPO1Status(string ip, bool GPO1)
        {
            byte[] status = new byte[1];

            if (GPO1 == true)
                status[0] = 1;

            COMM_INTBOARDALT_Cmd(System.Net.IPAddress.Parse(ip).Address, UDP_CMD.SET_GPO1_STATUS, status, new byte[0]);

            return Result.OK;
        }
        /// <summary>
        ///  Set GPO0 port status
        /// </summary>
        /// <returns></returns>
        public Result SetGPO2Status(bool GPO2)
        {
            if (m_DeviceInterfaceType != INTERFACETYPE.IPV4)
                return Result.CURRENTLY_NOT_ALLOWED;

            byte[] status = new byte[1];

            if (GPO2 == true)
                status[0] = 1;

            COMM_INTBOARDALT_Cmd(UDP_CMD.SET_GPO2_STATUS, status, new byte[0]);

            return Result.OK;
        }
        /// <summary>
        ///  Set GPO0 port status
        /// </summary>
        /// <returns></returns>
        public static Result SetGPO2Status(string ip, bool GPO2)
        {
            byte[] status = new byte[1];

            if (GPO2 == true)
                status[0] = 1;

            COMM_INTBOARDALT_Cmd(System.Net.IPAddress.Parse(ip).Address, UDP_CMD.SET_GPO2_STATUS, status, new byte[0]);

            return Result.OK;
        }
        /// <summary>
        ///  Set GPO1 port status
        /// </summary>
        /// <returns></returns>
        public Result SetGPO3Status(bool GPO3)
        {
            if (m_DeviceInterfaceType != INTERFACETYPE.IPV4)
                return Result.CURRENTLY_NOT_ALLOWED;

            byte[] status = new byte[1];

            if (GPO3 == true)
                status[0] = 1;

            COMM_INTBOARDALT_Cmd(UDP_CMD.SET_GPO3_STATUS, status, new byte[0]);

            return Result.OK;
        }
        /// <summary>
        ///  Set GPO1 port status
        /// </summary>
        /// <returns></returns>
        public static Result SetGPO3Status(string ip, bool GPO3)
        {
            byte[] status = new byte[1];

            if (GPO3 == true)
                status[0] = 1;

            COMM_INTBOARDALT_Cmd(System.Net.IPAddress.Parse(ip).Address, UDP_CMD.SET_GPO3_STATUS, status, new byte[0]);

            return Result.OK;
        }


        public enum GPIOPROCTYPE
        {
            GETGPI,
            GETGPI0,
            GETGPI1,
            GETGPO,
            GETGPO0,
            GETGPO1,
            SETGPO0,
            SETGPO1,
            GETGPI2,
            GETGPI3,
            GETGPO2,
            GETGPO3,
            SETGPO2,
            SETGPO3,
        }


        public class GPIO_ASYNC_CALLBACK : EventArgs
        {
            public GPIOPROCTYPE Type;
            public Result ret;
            public bool[] Status;
        }

        public event EventHandler<GPIO_ASYNC_CALLBACK> GPIOCallBackEvent;

        public Result GetGPIOAsync(GPIOPROCTYPE Type)
        {
            Thread Process;

            Process = new Thread((ThreadStart)delegate
            {
                GPIO_ASYNC_CALLBACK Parms = new GPIO_ASYNC_CALLBACK();

                Parms.Type = Type;
                switch (Type)
                {
                    case GPIOPROCTYPE.GETGPI:
                        Parms.Status = new bool[2];
                        Parms.ret = GetGPIStatus(ref Parms.Status[0], ref Parms.Status[1]);
                        break;

                    case GPIOPROCTYPE.GETGPI0:
                        Parms.Status = new bool[1];
                        Parms.ret = GetGPI0Status(ref Parms.Status[0]);
                        break;

                    case GPIOPROCTYPE.GETGPI1:
                        Parms.Status = new bool[1];
                        Parms.ret = GetGPI1Status(ref Parms.Status[0]);
                        break;

                    case GPIOPROCTYPE.GETGPO:
                        Parms.Status = new bool[2];
                        Parms.ret = GetGPOStatus(ref Parms.Status[0], ref Parms.Status[0]);
                        break;

                    case GPIOPROCTYPE.GETGPO0:
                        Parms.Status = new bool[1];
                        Parms.ret = GetGPO0Status(ref Parms.Status[0]);
                        break;

                    case GPIOPROCTYPE.GETGPO1:
                        Parms.Status = new bool[1];
                        Parms.ret = GetGPO1Status(ref Parms.Status[0]);
                        break;

                    default:
                        Parms.ret = Result.NOT_SUPPORTED;
                        break;
                }

                if (GPIOCallBackEvent != null)
                    GPIOCallBackEvent(this, Parms);
            });
            Process.Start();

            return Result.OK;
        }

        public Result SetGPO0Async(bool P0)
        {
            Thread Process;

            Process = new Thread((ThreadStart)delegate
            {
                GPIO_ASYNC_CALLBACK Parms = new GPIO_ASYNC_CALLBACK();

                Parms.Type = GPIOPROCTYPE.SETGPO0;

                Parms.ret = SetGPO0Status(P0);

                if (GPIOCallBackEvent != null)
                    GPIOCallBackEvent(this, Parms);
            });
            Process.Start();

            return Result.OK;
        }

        public Result SetGPO1Async(bool P1)
        {
            Thread Process;

            Process = new Thread((ThreadStart)delegate
            {
                GPIO_ASYNC_CALLBACK Parms = new GPIO_ASYNC_CALLBACK();

                Parms.Type = GPIOPROCTYPE.SETGPO1;

                Parms.ret = SetGPO1Status(P1);

                if (GPIOCallBackEvent != null)
                    GPIOCallBackEvent(this, Parms);
            });
            Process.Start();

            return Result.OK;
        }

        /// <summary>
        /// Set GPIO 5V Power ON/OFF
        /// </summary>
        /// <param name="onoff"> true = on, false = off</param>
        /// <returns></returns>
        public Result Set5VPowerOut(bool onoff)
        {
            if ((m_DeviceInterfaceType != INTERFACETYPE.IPV4) || (m_oem_machine != Machine.CS469 && m_oem_machine != Machine.CS333 && m_oem_machine != Machine.CS463 && m_oem_machine != Machine.CS468XJ && m_oem_machine != Machine.CS206 && m_oem_machine != Machine.CS468X && m_oem_machine != Machine.CS203X))
                return Result.CURRENTLY_NOT_ALLOWED;

            byte[] status = new byte[1];

            if (onoff == true)
                status[0] = 1;

            // Set GPIO 3.3
            if (COMM_INTBOARDALT_Cmd(UDP_CMD.SET_GPO5V, status, new byte [0]))
                return Result.OK;

            return Result.FAILURE;
        }

        public Result Get5VPowerOut(ref bool value)
        {
            if ((m_DeviceInterfaceType != INTERFACETYPE.IPV4) || (m_oem_machine != Machine.CS469 && m_oem_machine != Machine.CS333 && m_oem_machine != Machine.CS463 && m_oem_machine != Machine.CS468XJ && m_oem_machine != Machine.CS206 && m_oem_machine != Machine.CS468X && m_oem_machine != Machine.CS203X))
                return Result.CURRENTLY_NOT_ALLOWED;

            byte[] status = new byte[1];

            // Get GPIO 3.3
            if (COMM_INTBOARDALT_Cmd(UDP_CMD.CHECK_GPO5V_STATUS, new byte[0], status) == true)
            {
                if (status[0] != 0)
                    value = true;
                else
                    value = false;

                return Result.OK;
            }

            return Result.FAILURE;
        }

        public static Result Set5VPowerOut(string ip, bool onoff)
        {
            byte[] status = new byte[1];

            if (onoff == true)
                status[0] = 1;

            // Set GPIO 3.3
            if (COMM_INTBOARDALT_Cmd(System.Net.IPAddress.Parse(ip).Address, UDP_CMD.SET_GPO5V, status, new byte[0]))
                return Result.OK;

            return Result.FAILURE;
        }

        public static Result Get5VPowerOut(string ip, ref bool value)
        {
            byte[] status = new byte[1];

            // Get GPIO 3.3
            if (COMM_INTBOARDALT_Cmd(System.Net.IPAddress.Parse(ip).Address, UDP_CMD.CHECK_GPO5V_STATUS, new byte[0], status) == true)
            {
                if (status[0] != 0)
                    value = true;
                else
                    value = false;

                return Result.OK;
            }

            return Result.FAILURE;
        }

        public Result GetApiMode(out ApiMode Mode)
        {
            Mode = ApiMode.UNKNOWN;

            if (m_DeviceInterfaceType != INTERFACETYPE.IPV4)
                return Result.CURRENTLY_NOT_ALLOWED;

            byte[] status = new byte[1];

            if (COMM_INTBOARDALT_Cmd(UDP_CMD.CHECK_API, new byte[0], status) == true)
            {
                if (status[0] == 1)
                    Mode = ApiMode.LOWLEVEL;
                else
                    Mode = ApiMode.HIGHLEVEL;

                return Result.OK;
            }

            return Result.FAILURE;
        }

        public Result SetApiMode(ApiMode Mode)
        {
            if (m_DeviceInterfaceType != INTERFACETYPE.IPV4)
                return Result.CURRENTLY_NOT_ALLOWED;

            switch (Mode)
            {
                case ApiMode.LOWLEVEL:
                    if (COMM_INTBOARDALT_Cmd(UDP_CMD.SET_LOW_LVL_API, new byte[0], new byte[0]) == true)
                        return Result.OK;
                    break;

                case ApiMode.HIGHLEVEL:
                    if (COMM_INTBOARDALT_Cmd(UDP_CMD.SET_HIGH_LVL_API, new byte[0], new byte[0]) == true)
                        return Result.OK;
                    break;
            }
            
            return Result.FAILURE;
        }

        /// <summary>
        /// Set tag read GPO indication
        /// </summary>
        /// <param name="mode"> 0 = OFF, 1 = GPO0, 2 = GPO1</param>
        /// <returns></returns>
        public Result SetReadTagInd (byte mode)
        {
            if (m_DeviceInterfaceType != INTERFACETYPE.IPV4)
                return Result.CURRENTLY_NOT_ALLOWED;

            byte[] status = new byte[1];

            status[0] = mode;

            if (COMM_INTBOARDALT_Cmd(UDP_CMD.SET_TAGREADIND, status, new byte[0]))
                return Result.OK;

            return Result.FAILURE;
        }


        /// <summary>
        /// Get tag read GPO indication status
        /// </summary>
        /// <param name="mode"> 0 = OFF, 1 = GPO0, 2 = GPO1</param>
        /// <returns></returns>
        public Result GetReadTagIndStatus(ref byte mode)
        {
            if (m_DeviceInterfaceType != INTERFACETYPE.IPV4)
                return Result.CURRENTLY_NOT_ALLOWED;

            byte[] status = new byte[1];

            if (COMM_INTBOARDALT_Cmd(UDP_CMD.CHECK_TAGREADIND_STATUS, new byte[0], status))
            {
                mode = status[0];
                return Result.OK;
            }

            return Result.FAILURE;
        }


        /*

         * New GPIO Interrupt

            HighLevelInterface.GetGPIInterrupt(tbIpAddress.Text, ref gpi0Trigger, ref gpi1Trigger)
            HighLevelInterface.SetGPI0Interrupt(tbIpAddress.Text, (GPIOTrigger)cbGPI0Interrupt.SelectedIndex)
            HighLevelInterface.SetGPI1Interrupt(tbIpAddress.Text, (GPIOTrigger)cbGPI1Interrupt.SelectedIndex)
            HighLevelInterface.StartPollGPIStatus(GPIStatusCallback)
            HighLevelInterface.StopPollGPIStatus()
        */


        /// <summary>
        ///  Get GPI0 trigger status
        /// </summary>
        /// <returns></returns>
        public static Result GetGPIInterrupt(string ip, ref GPIOTrigger gpi0Trigger, ref GPIOTrigger gpi1Trigger)
        {
            byte[] status = new byte[2];

            if (COMM_INTBOARDALT_Cmd(System.Net.IPAddress.Parse(ip).Address, UDP_CMD.GET_GPI_INTERRUPT, new byte[0], status))
            {
                gpi0Trigger = (GPIOTrigger)status[0];
                gpi1Trigger = (GPIOTrigger)status[1];

                return Result.OK;
            }

            return Result.FAILURE;
        }

        /// <summary>
        ///  Get GPI0 trigger status
        /// </summary>
        /// <returns></returns>
        public Result GetGPIInterrupt(ref GPIOTrigger gpi0Trigger, ref GPIOTrigger gpi1Trigger)
        {
            if (m_DeviceInterfaceType != INTERFACETYPE.IPV4)
                return Result.CURRENTLY_NOT_ALLOWED;

            return GetGPIInterrupt(m_DeviceName, ref gpi0Trigger, ref gpi1Trigger);
        }

        /// <summary>
        ///  Set GPI0 trigger status
        /// </summary>
        /// <returns></returns>
        public static Result SetGPI0Interrupt(string ip, GPIOTrigger trigger)
        {
            byte[] status = new byte[1];

            status[0] = (byte)trigger;

            if (COMM_INTBOARDALT_Cmd(System.Net.IPAddress.Parse(ip).Address, UDP_CMD.SET_GPI0_INTERRUPT, status, new byte[0]))
                return Result.OK;

            return Result.FAILURE;
        }
        /// <summary>
        ///  Set GPI1 trigger status
        /// </summary>
        /// <returns></returns>
        public static Result SetGPI1Interrupt(string ip, GPIOTrigger trigger)
        {
            byte[] status = new byte[1];

            status[0] = (byte)trigger;

            if (COMM_INTBOARDALT_Cmd(System.Net.IPAddress.Parse(ip).Address, UDP_CMD.SET_GPI1_INTERRUPT, status, new byte[0]))
                return Result.OK;

            return Result.FAILURE;
        }

        /// <summary>
        ///  Set GPI0 trigger status
        /// </summary>
        /// <returns></returns>
        public Result SetGPI0Interrupt(GPIOTrigger trigger)
        {
            if (m_DeviceInterfaceType != INTERFACETYPE.IPV4)
                return Result.CURRENTLY_NOT_ALLOWED;

            m_GPI0InterruptTrigger = trigger;

            return SetGPI0Interrupt(m_DeviceName, trigger);
        }

        /// <summary>
        ///  Set GPI1 trigger status
        /// </summary>
        /// <returns></returns>
        public Result SetGPI1Interrupt(GPIOTrigger trigger)
        {
            if (m_DeviceInterfaceType != INTERFACETYPE.IPV4)
                return Result.CURRENTLY_NOT_ALLOWED;

            m_GPI1InterruptTrigger = trigger;

            return SetGPI1Interrupt(m_DeviceName, trigger);
        }

        private static bool pollGPIStarted = false;

        /// <summary>
        ///  Start to poll GPI status
        /// </summary>
        /// <returns></returns>
        public static Result StartPollGPIStatus(GPI_INTERRUPT_CALLBACK callback)
        {
            if (!pollGPIStarted)
            {
                pollGPIStarted = StartUdpPollGPIStatus(callback);
            }
            return Result.OK;
        }

        /// <summary>
        /// StopPollGPIStatus
        /// </summary>
        /// <returns></returns>
        public static Result StopPollGPIStatus()
        {
            if (pollGPIStarted)
            {
//                if (pollGPIThread != null && pollGPIThread.IsAlive)
                if (pollGPIThread != null)
                {
                    Interlocked.Increment(ref stopPollGPI);

                    pollGPIThreadDead.WaitOne();

                    pollGPIThreadDead.Close();

                    pollGPIThread = null;
                }

                pollGPIStarted = false;
            }
            return Result.OK;
        }






#if GPIOINTERRUPT
        
        private static UdpControl pollGPIHandle = null;
        private static bool pollGPIStarted = false;

        
        
        //
        //      Set/Get GPIO Interrupt
        //  

        /// <summary>
        ///  Set GPI1 trigger status
        /// </summary>
        /// <returns></returns>
        public Result SetGPI1Interrupt(GPIOTrigger trigger)
        {
            if (m_DeviceInterfaceType != INTERFACETYPE.IPV4)
                return Result.CURRENTLY_NOT_ALLOWED;

            return SetGPI1Interrupt(m_DeviceName, trigger);
        }

        /// <summary>
        ///  Set GPI0 trigger status
        /// </summary>
        /// <returns></returns>
        public Result GetGPIInterrupt(ref GPIOTrigger gpi0Trigger, ref GPIOTrigger gpi1Trigger)
        {
            if (m_DeviceInterfaceType != INTERFACETYPE.IPV4)
                return Result.CURRENTLY_NOT_ALLOWED;

            return GetGPIInterrupt(m_DeviceName, ref gpi0Trigger, ref gpi1Trigger);
        }

        //
        //      Start/Stop Poll GPI Status
        //  

        //bool startPollGPI;
        //GPIInterruptCallback pollGPICallback;
        //int stopPollGPI = 0;
        //AutoResetEvent pollGPIThreadDead;
        //NativeSocket udpSocket = null;
        //Thread pollGPIThread = null;

        GPI_INTERRUPT_CALLBACK pollGPICallback;
        bool PollGPI = false;
        Thread pollGPIThread = null;

        /// <summary>
        ///  Start to poll GPI status
        /// </summary>
        /// <returns></returns>
        public static Result StartPollGPIStatus(GPI_INTERRUPT_CALLBACK callback)
        {
            if (pollGPIHandle == null)
                pollGPIHandle = new UdpControl();
            if (!pollGPIStarted)
            {

                pollGPIStarted = pollGPIHandle.StartPollGPIStatus(callback);
            }
            return Result.OK;
        }

        /// <summary>
        ///  Stop poll GPI status
        /// </summary>
        /// <returns></returns>
        public static Result StopPollGPIStatus()
        {
            if (pollGPIStarted)
            {
                if (pollGPIThread != null && pollGPIThread.IsAlive)
                {
                    Interlocked.Increment(ref stopPollGPI);

                    pollGPIThreadDead.WaitOne();

                    pollGPIThreadDead.Close();

                    pollGPIThread = null;
                }
                pollGPIStarted = false;
            }
            return Result.OK;
        }
        
        
 /*
 * public Result StartPollGPIStatus(GPI_INTERRUPT_CALLBACK callback)
        {
            if (startPollGPI)
                return true;

            try
            {
                pollGPIThreadDead = new AutoResetEvent(false);
                udpSocket = new NativeSocket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                pollGPICallback = callback;
                udpSocket.Bind(IPAddress.Any, 3041);
                udpSocket.Blocking = false;

                if (pollGPIThread == null)
                {
                    pollGPIThread = new Thread(new ThreadStart(StartPollGPIStatusThreadProc));
                }

                pollGPIThread.IsBackground = true;
                pollGPIThread.Start();

            }
            catch (Exception ex)
            {
                PrintMessage.Logger.DebugException(string.Format(
                    "{0},{1}",
                    "StartPollGPIStatus",
                    TransStatus.CPL_ERROR_NOTFOUND),
                    ex);
                goto ERROR_EXIT;
            }
            startPollGPI = true;

            return true;

        ERROR_EXIT:
            udpSocket.Close();
            pollGPIThreadDead.Close();
            return Result.FAILURE;
        }
*/






#if GPIO
        void StartPollGPIStatusThreadProc()
        {
            SocketError sockErr = SocketError.Success;

            while (Interlocked.Equals(stopPollGPI, 0))
            {
                SockAddrIPv4 SenderAddr = new SockAddrIPv4();

                Byte[] revBuff = new byte[6];

                if (udpSocket.ReceiveFrom(revBuff, 6, 0, SenderAddr) == NativeSocket.SOCKET_ERROR)
                {
                    if ((sockErr = udpSocket.GetLastError()) == SocketError.WouldBlock)
                        continue;
                    else
                        goto ERROR_EXIT;
                }

                if (revBuff[0] == 0x81 && revBuff[3] == 0x5/*GET_GPI_STATUS*/) // Vaild header
                {
                    if (pollGPICallback != null && revBuff[1] != 0)
                    {
                        if (!pollGPICallback(SenderAddr.Address, new GPIOStatus(revBuff[4] == 1, revBuff[5] == 1)))
                        {
                            break;
                        }
                    }
                }
            }

        ERROR_EXIT:
            udpSocket.Close();
            startPollGPI = false;
            stopPollGPI = 0;
            pollGPIThreadDead.Set();
        }
#endif

#if GPIO
        /// <summary>
        /// Stop Poll GPI Status
        /// </summary>
        /// <returns></returns>
        public static bool StopPollGPIStatus()
        {
            if (pollGPIThread != null)
            //if (pollGPIThread != null && pollGPIThread..IsAlive)
            {
                Interlocked.Increment(ref stopPollGPI);

                pollGPIThreadDead.WaitOne();

                pollGPIThreadDead.Close();

                pollGPIThread = null;
            }

            return true;
        }
#endif

        
        
#endif






#endregion

#region ====================== Fire Event ======================
        private void FireStateChangedEvent(RFState e)
        {
            State = e;
            switch (e)
            {
                case RFState.BUSY:
                    Interlocked.Exchange(ref bStop, 0);
                    break;
                case RFState.IDLE:
                    Interlocked.Exchange(ref bStop, 1);
                    break;
            }
#if __NOT_USED__
#if WindowsCE
#if !BizTalk
            if (mGuiMarshaller.InvokeRequired)
            {
                mGuiMarshaller.Invoke(new EventHandler<OnStateChangedEventArgs>(
                    TellThemOnStateChanged), new object[] { this, new OnStateChangedEventArgs(e) });
            }
            else
#endif
            {
                TellThemOnStateChanged(this, new OnStateChangedEventArgs(e));
            }
#else
            if (OnStateChanged != null)
            {
                foreach (Delegate d in OnStateChanged.GetInvocationList())
                {
                    System.ComponentModel.ISynchronizeInvoke s = d.Target as System.ComponentModel.ISynchronizeInvoke;

                    if (s != null && s.InvokeRequired)
                        s.Invoke(d, new object[] { this, new OnStateChangedEventArgs(e) });
                    else
                        d.DynamicInvoke(new object[] { this, new OnStateChangedEventArgs(e) });
                }
            }
#endif
#else
            TellThemOnStateChanged(this, new OnStateChangedEventArgs(e));
#endif
        }

        private void FireAccessCompletedEvent(OnAccessCompletedEventArgs args/*bool success, Bank bnk, TagAccess access, IBANK data*/)
        {
#if __NOT_USED__
#if WindowsCE
#if !BizTalk
            if (mGuiMarshaller.InvokeRequired)
            {
                mGuiMarshaller.Invoke(new EventHandler<OnAccessCompletedEventArgs>(
                    TellThemOnAccessCompleted), new object[] { this, args });
            }
            else
#endif
            {
                TellThemOnAccessCompleted(this, args);
            }
#else
            if (OnAccessCompleted != null)
            {
                foreach (Delegate d in OnAccessCompleted.GetInvocationList())
                {
                    System.ComponentModel.ISynchronizeInvoke s = d.Target as System.ComponentModel.ISynchronizeInvoke;

                    if (s != null && s.InvokeRequired)
                        s.Invoke(d, new object[] { this, args });
                    else
                        d.DynamicInvoke(new object[] { this, args });
                }
            }
#endif
#else
            TellThemOnAccessCompleted(this, args);
#endif
        }

        private Int32 FireCallbackEvent(OnAsyncCallbackEventArgs args)
        {
#if __NOT_USED__
#if WindowsCE
#if !BizTalk
            if (mGuiMarshaller.InvokeRequired)
            {
                mGuiMarshaller.Invoke(new EventHandler<OnAsyncCallbackEventArgs>(
                    TellThemOnCallback), new object[] { this, args });
            }
            else
#endif
            {
                TellThemOnCallback(this, args);
            }
#else
            if (OnAsyncCallback != null)
            {
                foreach (Delegate d in OnAsyncCallback.GetInvocationList())
                {
                    System.ComponentModel.ISynchronizeInvoke s = d.Target as System.ComponentModel.ISynchronizeInvoke;

                    if (s != null && s.InvokeRequired)
                        s.Invoke(d, new object[] { this, args });
                    else
                        d.DynamicInvoke(new object[] { this, args });
                }
            }
#endif
#else
            return TellThemOnCallback(this, args);
#endif
        }
#if true
        //[MethodImpl(MethodImplOptions.Synchronized)]
        private void TellThemOnStateChanged(object sender, OnStateChangedEventArgs e)
        {
            if (OnStateChanged != null)
            {
                try
                {
                    OnStateChanged(sender, e);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                }
            }
        }
        //[MethodImpl(MethodImplOptions.Synchronized)]
        private void TellThemOnAccessCompleted(object sender, OnAccessCompletedEventArgs e)
        {
            if (OnAccessCompleted != null)
            {
                try
                {
                    OnAccessCompleted(sender, e);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                }
            }
        }
        //[MethodImpl(MethodImplOptions.Synchronized)]
        private Int32 TellThemOnCallback(object sender, OnAsyncCallbackEventArgs e)
        {
            if (OnAsyncCallback != null)
            {
                try
                {
                    OnAsyncCallback(sender, e);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                }
            }
            return e.Cancel ? 1 : 0;
        }
#endif
#endregion

#region ====================== Performance Timer ======================
#if DEBUG
        private void TimeStart()
        {
            TimerCnt.Start();
        }

        private double TimeStop()
        {
            TimerCnt.Stop();
            return TimerCnt.Duration;
        }
#endif
#endregion

#region ====================== Customized function ======================
#if NOUSE
        private void SetSSBLinkProfile()
        {

            ushort HST_RFTC_CURRENT_PROFILE = 0x0B60,//0x00000002
                HST_RFTC_PROF_SEL = 0x0B61,//0x00000002
                MAC_RFTC_PROF_CFG = 0x0B62,//0x00000001
                MAC_RFTC_PROF_ID_HI = 0x0B63,//0x00000009
                MAC_RFTC_PROF_ID_LO = 0x0B64,//0x00010090
                MAC_RFTC_PROF_IDVER = 0x0B65,//see desc.
                MAC_RFTC_PROF_PROTOCOL = 0x0B66,//0x00000000
                MAC_RFTC_PROF_R2TMODTYPE = 0x0B67,//0x00000002
                MAC_RFTC_PROF_TARI = 0x0B68,//0x000061A8
                MAC_RFTC_PROF_X = 0x0B69,//0x00000000
                MAC_RFTC_PROF_PW = 0x0B6A,//0x000030D4
                MAC_RFTC_PROF_RTCAL = 0x0B6B,//0x0000F424
                MAC_RFTC_PROF_TRCAL = 0x0B6C,//0x00014582
                MAC_RFTC_PROF_DIVIDERATIO = 0x0B6D,//0x00000001
                MAC_RFTC_PROF_MILLERNUM = 0x0B6E,//0x00000002
                MAC_RFTC_PROF_T2RLINKFREQ = 0x0B6F,//0x0003D090
                MAC_RFTC_PROF_VART2DELAY = 0x0B70,//0x00000019
                MAC_RFTC_PROF_RXDELAY = 0x0B71,//0x000007FF
                MAC_RFTC_PROF_MINTOTT2DELAY = 0x0B72,//0x0000000C
                MAC_RFTC_PROF_TXPROPDELAY = 0x0B73,//0x0000000D
                MAC_RFTC_PROF_RSSIAVECFG = 0x0B74;//0x00000000

            ThrowException(RadioWriteLinkProfileRegister(8, MAC_RFTC_PROF_ID_HI, 0x0001));
            ThrowException(RadioWriteLinkProfileRegister(8, MAC_RFTC_PROF_ID_LO, 0x0003));
            //ThrowException(RadioWriteLinkProfileRegister(8, MAC_RFTC_PROF_IDVER, 0x58FE);
            ThrowException(RadioWriteLinkProfileRegister(8, MAC_RFTC_PROF_PROTOCOL, 0x0000));//0 indicates ISO 18000-6C 
            ThrowException(RadioWriteLinkProfileRegister(8, MAC_RFTC_PROF_R2TMODTYPE, 0x0001));//0=DSB-ASK, 1=SSB-ASK, 2=PR-ASK 
            ThrowException(RadioWriteLinkProfileRegister(8, MAC_RFTC_PROF_TARI, 0x61A8));//6250=0x186A(6.25usec), 12500=0x30D4 (12.5usec), or 25000=0x61A8 (25usec)
            ThrowException(RadioWriteLinkProfileRegister(8, MAC_RFTC_PROF_X, 0x0000));//0 means X=0.5, 1 means X=1 
            ThrowException(RadioWriteLinkProfileRegister(8, MAC_RFTC_PROF_PW, 0x30D4));//12500=0x30D4 (12.5usec), 6250=0x186A (6.25usec), or 3125=0x0C35 (3.13usec) 
            /*This register provides a report of the RTCal being used with the selected link profile. It cannot be set by the host – it 
            is for informational purposes only. It is reported in terms of nano-seconds. See the ISO 18000-6C spec for more 
            information. */
            //ThrowException(RadioWriteLinkProfileRegister(8, MAC_RFTC_PROF_RTCAL, 0x0000));
            /*This register provides a report of the TRCal being used with the selected link profile. It cannot be set by the host – it 
            is for informational purposes only. See the ISO 18000-6C spec for more information on TRCal. */
            //ThrowException(RadioWriteLinkProfileRegister(8, MAC_RFTC_PROF_TRCAL, 0x0000));

            //Bit   Name    Description 
            //0     DR      0 means DR=8, 1 means DR=64/3 
            //1     TRext   0: no pilot tone,  1: Pilot tone
            ThrowException(RadioWriteLinkProfileRegister(8, MAC_RFTC_PROF_DIVIDERATIO, 0x0003));
            
            //0 means FM0, 1 means M=2, 2 means M=4, 3 means M=8 
            ThrowException(RadioWriteLinkProfileRegister(8, MAC_RFTC_PROF_MILLERNUM, 0x0002));
            //This register provides a report of the tag-to-interrogator link frequency.
            //RadioWriteLinkProfileRegister(8, MAC_RFTC_PROF_T2RLINKFREQ, 0x0003D090));//250KHz
            ThrowException(RadioWriteLinkProfileRegister(8, MAC_RFTC_PROF_VART2DELAY, 0x0019));
            ThrowException(RadioWriteLinkProfileRegister(8, MAC_RFTC_PROF_RXDELAY, 0x07FF));
            ThrowException(RadioWriteLinkProfileRegister(8, MAC_RFTC_PROF_MINTOTT2DELAY, 0x000C));
            ThrowException(RadioWriteLinkProfileRegister(8, MAC_RFTC_PROF_TXPROPDELAY, 0x000D));
            ThrowException(RadioWriteLinkProfileRegister(8, MAC_RFTC_PROF_RSSIAVECFG, 0x0000));
        }

        private void GetSSBLinkProfile()
        {

            ushort HST_RFTC_CURRENT_PROFILE = 0x0B60,//0x00000002
                HST_RFTC_PROF_SEL = 0x0B61,//0x00000002
                MAC_RFTC_PROF_CFG = 0x0B62,//0x00000001
                MAC_RFTC_PROF_ID_HI = 0x0B63,//0x00000009
                MAC_RFTC_PROF_ID_LO = 0x0B64,//0x00010090
                MAC_RFTC_PROF_IDVER = 0x0B65,//see desc.
                MAC_RFTC_PROF_PROTOCOL = 0x0B66,//0x00000000
                MAC_RFTC_PROF_R2TMODTYPE = 0x0B67,//0x00000002
                MAC_RFTC_PROF_TARI = 0x0B68,//0x000061A8
                MAC_RFTC_PROF_X = 0x0B69,//0x00000000
                MAC_RFTC_PROF_PW = 0x0B6A,//0x000030D4
                MAC_RFTC_PROF_RTCAL = 0x0B6B,//0x0000F424
                MAC_RFTC_PROF_TRCAL = 0x0B6C,//0x00014582
                MAC_RFTC_PROF_DIVIDERATIO = 0x0B6D,//0x00000001
                MAC_RFTC_PROF_MILLERNUM = 0x0B6E,//0x00000002
                MAC_RFTC_PROF_T2RLINKFREQ = 0x0B6F,//0x0003D090
                MAC_RFTC_PROF_VART2DELAY = 0x0B70,//0x00000019
                MAC_RFTC_PROF_RXDELAY = 0x0B71,//0x000007FF
                MAC_RFTC_PROF_MINTOTT2DELAY = 0x0B72,//0x0000000C
                MAC_RFTC_PROF_TXPROPDELAY = 0x0B73,//0x0000000D
                MAC_RFTC_PROF_RSSIAVECFG = 0x0B74;//0x00000000
            ushort value = 0;
            ThrowException(RadioReadLinkProfileRegister(8, MAC_RFTC_PROF_ID_HI, ref value));
            Trace.WriteLine(string.Format("MAC_RFTC_PROF_ID_HI = {0}", value));
            ThrowException(RadioReadLinkProfileRegister(8, MAC_RFTC_PROF_ID_LO, ref value));
            Trace.WriteLine(string.Format("MAC_RFTC_PROF_ID_LO = {0}", value));
            ThrowException(RadioReadLinkProfileRegister(8, MAC_RFTC_PROF_IDVER, ref value));
            Trace.WriteLine(string.Format("MAC_RFTC_PROF_IDVER = {0}", value));
            ThrowException(RadioReadLinkProfileRegister(8, MAC_RFTC_PROF_PROTOCOL, ref value));//0 indicates ISO 18000-6C 
            Trace.WriteLine(string.Format("MAC_RFTC_PROF_PROTOCOL = {0}", value));
            ThrowException(RadioReadLinkProfileRegister(8, MAC_RFTC_PROF_R2TMODTYPE, ref value));//0=DSB-ASK, 1=SSB-ASK, 2=PR-ASK 
            Trace.WriteLine(string.Format("MAC_RFTC_PROF_R2TMODTYPE = {0}", value));
            ThrowException(RadioReadLinkProfileRegister(8, MAC_RFTC_PROF_TARI, ref value));//6250=0x186A(6.25usec), 12500=0x30D4 (12.5usec), or 25000=0x61A8 (25usec)
            Trace.WriteLine(string.Format("MAC_RFTC_PROF_TARI = {0}", value));
            ThrowException(RadioReadLinkProfileRegister(8, MAC_RFTC_PROF_X, ref value));//0 means X=0.5, 1 means X=1 
            Trace.WriteLine(string.Format("MAC_RFTC_PROF_X = {0}", value));
            ThrowException(RadioReadLinkProfileRegister(8, MAC_RFTC_PROF_PW, ref value));//12500=0x30D4 (12.5usec), 6250=0x186A (6.25usec), or 3125=0x0C35 (3.13usec) 
            Trace.WriteLine(string.Format("MAC_RFTC_PROF_PW = {0}", value));
            ThrowException(RadioReadLinkProfileRegister(8, MAC_RFTC_PROF_RTCAL, ref value));
            Trace.WriteLine(string.Format("MAC_RFTC_PROF_RTCAL = {0}", value));
            ThrowException(RadioReadLinkProfileRegister(8, MAC_RFTC_PROF_TRCAL, ref value));
            Trace.WriteLine(string.Format("MAC_RFTC_PROF_TRCAL = {0}", value));
            ThrowException(RadioReadLinkProfileRegister(8, MAC_RFTC_PROF_DIVIDERATIO, ref value));
            Trace.WriteLine(string.Format("MAC_RFTC_PROF_DIVIDERATIO = {0}", value));
            ThrowException(RadioReadLinkProfileRegister(8, MAC_RFTC_PROF_MILLERNUM, ref value));
            Trace.WriteLine(string.Format("MAC_RFTC_PROF_MILLERNUM = {0}", value));
            ThrowException(RadioReadLinkProfileRegister(8, MAC_RFTC_PROF_T2RLINKFREQ, ref value));//250KHz
            Trace.WriteLine(string.Format("MAC_RFTC_PROF_T2RLINKFREQ = {0}", value));
            ThrowException(RadioReadLinkProfileRegister(8, MAC_RFTC_PROF_VART2DELAY, ref value));
            Trace.WriteLine(string.Format("MAC_RFTC_PROF_VART2DELAY = {0}", value));
            ThrowException(RadioReadLinkProfileRegister(8, MAC_RFTC_PROF_RXDELAY, ref value));
            Trace.WriteLine(string.Format("MAC_RFTC_PROF_RXDELAY = {0}", value));
            ThrowException(RadioReadLinkProfileRegister(8, MAC_RFTC_PROF_MINTOTT2DELAY, ref value));
            Trace.WriteLine(string.Format("MAC_RFTC_PROF_MINTOTT2DELAY = {0}", value));
            ThrowException(RadioReadLinkProfileRegister(8, MAC_RFTC_PROF_TXPROPDELAY, ref value));
            Trace.WriteLine(string.Format("MAC_RFTC_PROF_TXPROPDELAY = {0}", value));
            ThrowException(RadioReadLinkProfileRegister(8, MAC_RFTC_PROF_RSSIAVECFG, ref value));
            Trace.WriteLine(string.Format("MAC_RFTC_PROF_RSSIAVECFG = {0}", value));
        }

        private void GetLinkProfile2()
        {

            ushort HST_RFTC_CURRENT_PROFILE = 0x0B60,//0x00000002
                HST_RFTC_PROF_SEL = 0x0B61,//0x00000002
                MAC_RFTC_PROF_CFG = 0x0B62,//0x00000001
                MAC_RFTC_PROF_ID_HI = 0x0B63,//0x00000009
                MAC_RFTC_PROF_ID_LO = 0x0B64,//0x00010090
                MAC_RFTC_PROF_IDVER = 0x0B65,//see desc.
                MAC_RFTC_PROF_PROTOCOL = 0x0B66,//0x00000000
                MAC_RFTC_PROF_R2TMODTYPE = 0x0B67,//0x00000002
                MAC_RFTC_PROF_TARI = 0x0B68,//0x000061A8
                MAC_RFTC_PROF_X = 0x0B69,//0x00000000
                MAC_RFTC_PROF_PW = 0x0B6A,//0x000030D4
                MAC_RFTC_PROF_RTCAL = 0x0B6B,//0x0000F424
                MAC_RFTC_PROF_TRCAL = 0x0B6C,//0x00014582
                MAC_RFTC_PROF_DIVIDERATIO = 0x0B6D,//0x00000001
                MAC_RFTC_PROF_MILLERNUM = 0x0B6E,//0x00000002
                MAC_RFTC_PROF_T2RLINKFREQ = 0x0B6F,//0x0003D090
                MAC_RFTC_PROF_VART2DELAY = 0x0B70,//0x00000019
                MAC_RFTC_PROF_RXDELAY = 0x0B71,//0x000007FF
                MAC_RFTC_PROF_MINTOTT2DELAY = 0x0B72,//0x0000000C
                MAC_RFTC_PROF_TXPROPDELAY = 0x0B73,//0x0000000D
                MAC_RFTC_PROF_RSSIAVECFG = 0x0B74;//0x00000000
            ushort value = 0;
            //ThrowException(RadioWriteLinkProfileRegister(2, HST_RFTC_PROF_SEL, 0x0002));
            ThrowException(RadioReadLinkProfileRegister(2, MAC_RFTC_PROF_ID_HI, ref value));
            Trace.WriteLine(string.Format("MAC_RFTC_PROF_ID_HI = {0}", value));
            ThrowException(RadioReadLinkProfileRegister(2, MAC_RFTC_PROF_ID_LO, ref value));
            Trace.WriteLine(string.Format("MAC_RFTC_PROF_ID_LO = {0}", value));
            ThrowException(RadioReadLinkProfileRegister(2, MAC_RFTC_PROF_IDVER, ref value));
            Trace.WriteLine(string.Format("MAC_RFTC_PROF_IDVER = {0}", value));
            ThrowException(RadioReadLinkProfileRegister(2, MAC_RFTC_PROF_PROTOCOL, ref value));//0 indicates ISO 18000-6C 
            Trace.WriteLine(string.Format("MAC_RFTC_PROF_PROTOCOL = {0}", value));
            ThrowException(RadioReadLinkProfileRegister(2, MAC_RFTC_PROF_R2TMODTYPE, ref value));//0=DSB-ASK, 1=SSB-ASK, 2=PR-ASK 
            Trace.WriteLine(string.Format("MAC_RFTC_PROF_R2TMODTYPE = {0}", value));
            ThrowException(RadioReadLinkProfileRegister(2, MAC_RFTC_PROF_TARI, ref value));//6250=0x186A(6.25usec), 12500=0x30D4 (12.5usec), or 25000=0x61A8 (25usec)
            Trace.WriteLine(string.Format("MAC_RFTC_PROF_TARI = {0}", value));
            ThrowException(RadioReadLinkProfileRegister(2, MAC_RFTC_PROF_X, ref value));//0 means X=0.5, 1 means X=1 
            Trace.WriteLine(string.Format("MAC_RFTC_PROF_X = {0}", value));
            ThrowException(RadioReadLinkProfileRegister(2, MAC_RFTC_PROF_PW, ref value));//12500=0x30D4 (12.5usec), 6250=0x186A (6.25usec), or 3125=0x0C35 (3.13usec) 
            Trace.WriteLine(string.Format("MAC_RFTC_PROF_PW = {0}", value));
            ThrowException(RadioReadLinkProfileRegister(2, MAC_RFTC_PROF_RTCAL, ref value));
            Trace.WriteLine(string.Format("MAC_RFTC_PROF_RTCAL = {0}", value));
            ThrowException(RadioReadLinkProfileRegister(2, MAC_RFTC_PROF_TRCAL, ref value));
            Trace.WriteLine(string.Format("MAC_RFTC_PROF_TRCAL = {0}", value));
            ThrowException(RadioReadLinkProfileRegister(2, MAC_RFTC_PROF_DIVIDERATIO, ref value));
            Trace.WriteLine(string.Format("MAC_RFTC_PROF_DIVIDERATIO = {0}", value));
            ThrowException(RadioReadLinkProfileRegister(2, MAC_RFTC_PROF_MILLERNUM, ref value));
            Trace.WriteLine(string.Format("MAC_RFTC_PROF_MILLERNUM = {0}", value));
            ThrowException(RadioReadLinkProfileRegister(2, MAC_RFTC_PROF_T2RLINKFREQ, ref value));//250KHz
            Trace.WriteLine(string.Format("MAC_RFTC_PROF_T2RLINKFREQ = {0}", value));
            ThrowException(RadioReadLinkProfileRegister(2, MAC_RFTC_PROF_VART2DELAY, ref value));
            Trace.WriteLine(string.Format("MAC_RFTC_PROF_VART2DELAY = {0}", value));
            ThrowException(RadioReadLinkProfileRegister(2, MAC_RFTC_PROF_RXDELAY, ref value));
            Trace.WriteLine(string.Format("MAC_RFTC_PROF_RXDELAY = {0}", value));
            ThrowException(RadioReadLinkProfileRegister(2, MAC_RFTC_PROF_MINTOTT2DELAY, ref value));
            Trace.WriteLine(string.Format("MAC_RFTC_PROF_MINTOTT2DELAY = {0}", value));
            ThrowException(RadioReadLinkProfileRegister(2, MAC_RFTC_PROF_TXPROPDELAY, ref value));
            Trace.WriteLine(string.Format("MAC_RFTC_PROF_TXPROPDELAY = {0}", value));
            ThrowException(RadioReadLinkProfileRegister(2, MAC_RFTC_PROF_RSSIAVECFG, ref value));
            Trace.WriteLine(string.Format("MAC_RFTC_PROF_RSSIAVECFG = {0}", value));
        }
#endif
        private void ThrowException(Result Result)
        {
            if ((m_Result = Result) != Result.OK) 
                throw new ReaderException(Result);
        }
        private Result ReturnState(Result Result)
        {
            return (m_Result = Result);
        }
        private Result FireIfReset(Result Result)
        {
            switch (Result)
            {
                case Result.DRIVER_LOAD:
                case Result.DRIVER_MISMATCH:
                case Result.FAILURE:
                case Result.INVALID_HANDLE:
                case Result.NETWORK_LOST:
                case Result.NETWORK_RESET:
                case Result.NO_SUCH_RADIO:
                case Result.NOT_INITIALIZED:
                case Result.NOT_SUPPORTED:
                case Result.OUT_OF_MEMORY:
                case Result.POWER_DOWN_FAIL:
                case Result.POWER_UP_FAIL:
                case Result.PREALLOCATED_BUFFER_FULL:
                case Result.RADIO_FAILURE:
                case Result.RADIO_NOT_PRESENT:
                case Result.RADIO_NOT_RESPONDING:
                case Result.RECEIVE_OVERFLOW:
                case Result.SYSTEM_CATCH_EXCEPTION:
                case Result.UNKNOWN_OPERATION:
                    FireStateChangedEvent(RFState.RESET);
                    //return (m_Result = Reconnect(-1));
                    break;
            }
            return (m_Result = Result);
        }
#endregion

#region ====================== Orginal Intel Function ======================
        /// <summary>
        /// Executes a tag read for the tags of interest.  If the
        /// SelectFlags.SELECT flag is specified, the tag population is
        /// partitioned (i.e., ISO 18000-6C select) prior to the tag-read operation.
        /// If the RFID_FLAG_PERFORM_POST_MATCH flag is specified, the post-singulation
        /// match mask is applied to a singulated tag's EPC to determine if the tag
        /// will be read from.  Reads may only be performed on 16-bit word boundaries
        /// and for multiples of 16-bit words.  If one or more of the memory words
        /// specified by the offset/count combination do not exist or are read-locked,
        /// the read from the tag will fail and this failure will be reported through
        /// the operation response packet.    The operation-response packets will
        /// be returned to the application via the application-supplied callback
        /// function.  Each tag-read record is grouped with its corresponding tag-
        /// inventory record.  An application may prematurely stop a read operation by
        /// calling RFID_Radio{Cancel|Abort}Operation on another thread or by returning
        /// a non-zero value from the callback function.  A tag read may not be
        /// issued while a radio module is executing a tag-protocol operation.
        /// 
        /// Note that read should not be confused with inventory.  A read allows for
        /// reading a sequence of one or more 16-bit words starting from an arbitrary
        /// 16-bit location in any of the tag's memory banks.
        /// </summary>
        /// <param name="offset">
        /// The offset of the first 16-bit word, where zero is the first 16-bit 
        /// word in the memory bank, to read from the specified memory 
        /// bank. 
        /// </param>
        /// <param name="count">
        /// The number of 16-bit words to read.  If this value is zero and 
        /// bank is MemoryBank.EPC, the read will return the 
        /// contents of the tag's EPC memory starting at the 16-bit word 
        /// specified by offset through the end of the EPC.  If this value is 
        /// zero and bank is not MemoryBank.EPC, the read 
        /// will return, for the tag's chosen memory bank, data starting from 
        /// the 16-bit word specified by offset to the end of the memory 
        /// bank.  If this value is non-zero, it must be in the range 1 to 255, 
        /// inclusive. 
        /// </param>
        /// <param name="bank">
        /// The memory bank from which to read.  Valid values are: 
        /// <para>MemoryBank.RESERVED </para>
        /// <para>MemoryBank.EPC  </para>
        /// <para>MemoryBank.TID </para>
        /// <para>MemoryBank.USER </para>
        /// </param>
        /// <param name="password">
        /// The access password for the tags.  A value of zero indicates no 
        /// access password. 
        /// </param>
        /// <param name="tagStopCount">
        /// The maximum number of tags to which the tag-protocol operation 
        /// will be applied.  If this number is zero, then the operation is applied 
        /// to all tags that match the selection, and optionally post-singulation, 
        /// match criteria (within the constraints of the antenna-port dwell time 
        /// and inventory cycle count – see ).  If this number is non-zero, the 
        /// antenna-port dwell-time and inventory-cycle-count constraints still 
        /// apply, however the operation will be prematurely terminated if the 
        /// maximum number of tags have the tag-protocol operation applied to 
        /// them.  For version 1.3, this field may have a maximum value 255. 
        /// </param>
        /// <param name="callback">callback function</param>
        /// <param name="flags">read flags.  May be zero or a combination of the following:
        /// SelectFlags.SELECT - perform one or more selects before performing
        ///   the read.
        /// SelectFlags.POST_MATCH - perform post-singulation mask match on
        ///   singulated tags.</param>
        /// <returns></returns>
        public Result SyncRead(
            ushort offset,
            ushort count,
            MemoryBank bank,
            UInt32 password,
            byte tagStopCount,
            TagAccessCallbackDelegate callback,
            SelectFlags flags)
        {
            if (State != RFState.IDLE)
                return Result.RADIO_BUSY;

            try
            {
#if __NORMAL_MODE__
                if (m_save_resp_mode != ResponseMode.EXTENDED)
                {
                    //TurnOn Debug infomation
                    ThrowException(SetRadioResponseDataMode(ResponseMode.EXTENDED));
                }
#endif
                accessCallback = callback;
                ReadParms parms = new ReadParms(true);

//                Native.CallbackDelegate cb = new Native.CallbackDelegate(InternalCallback);
//                parms.common.callback = Marshal.GetFunctionPointerForDelegate(cb);

                parms.common.callbackCode = IntPtr.Zero;
                parms.common.context = IntPtr.Zero;
                parms.common.tagStopCount = (uint)tagStopCount;

                parms.readCmdParms.count = count;
                parms.readCmdParms.offset = offset;
                parms.readCmdParms.bank = bank;
                parms.accessPassword = password;

//                ThrowException(TagRead(parms, flags));
            }
            catch (ReaderException ex)
            {
#if DEBUG
                CSLibrary.Diagnostics.CoreDebug.Logger.DebugException("HighLevelInterface.SyncTagRead()", ex);
#endif
            }
            catch (Exception ex)
            {
#if DEBUG
                CSLibrary.Diagnostics.CoreDebug.Logger.DebugException("HighLevelInterface.SyncTagRead()", ex);
#endif
            }
            return m_Result;
        }
        /// <summary>
        /// Executes a tag write for the tags of interest.  If the
        /// SelectFlags.SELECT flag is specified, the tag population is
        /// partitioned (i.e., ISO 18000-6C select) prior to the tag-write operation.
        /// If the RFID_FLAG_PERFORM_POST_MATCH flag is specified, the post-singulation
        /// match mask is applied to a singulated tag's EPC to determine if the tag
        /// will be written to.  Writes may only be performed on 16-bit word boundaries
        /// and for multiples of 16-bit words.  If one or more of the specified memory
        /// words do not exist or are write-locked, the write to the tag will fail and
        /// this failure will be reported through the operation-response packet.  The
        /// operation-response packets will be returned to the application via
        /// the application-supplied callback function.  Each tag-write record is
        /// grouped with its corresponding tag-inventory record.  An application may
        /// prematurely stop a write operation by calling
        /// RFID_Radio{Cancel|Abort}Operation on another thread or by returning a non-
        /// zero value from the callback function.  A tag write may not be
        /// issued while a radio module is executing a tag-protocol operation.
        /// </summary>
        /// <param name="offset">
        /// The offset of the first 16-bit word, where zero is the first 16-bit 
        /// word in the memory bank, to write in the specified memory bank. 
        /// </param>
        /// <param name="count">
        /// The number of 16-bit words to be written to the tag's specified 
        /// memory bank.  For version 1.1 of the RFID Reader Library, this 
        /// parameter must contain a value between 1 and 8, inclusive. 
        /// </param>
        /// <param name="bank">
        /// The memory bank from which to read.  Valid values are: 
        /// <para>MemoryBank.RESERVED </para>
        /// <para>MemoryBank.EPC  </para>
        /// <para>MemoryBank.TID </para>
        /// <para>MemoryBank.USER </para>
        /// </param>
        /// <param name="password">
        /// The access password for the tags.  A value of zero indicates no 
        /// access password. 
        /// </param>
        /// <param name="tagStopCount">
        /// The maximum number of tags to which the tag-protocol operation 
        /// will be applied.  If this number is zero, then the operation is applied 
        /// to all tags that match the selection, and optionally post-singulation, 
        /// match criteria (within the constraints of the antenna-port dwell time 
        /// and inventory cycle count – see ).  If this number is non-zero, the 
        /// antenna-port dwell-time and inventory-cycle-count constraints still 
        /// apply, however the operation will be prematurely terminated if the 
        /// maximum number of tags have the tag-protocol operation applied to 
        /// them.  For version 1.3, this field may have a maximum value 255. 
        /// </param>
        /// <param name="data">
        /// A buffer of count 16-bit values to be written 
        /// sequentially to the tag's specified memory bank.  The high-order 
        /// byte of pData[n] will be written to the tag's memory-bank byte at 
        /// 16-bit offset (offset+n).  The low-order byte will be written to the 
        /// next byte.  For example, if offset is 2 and pData[0] is 0x1122, 
        /// then the tag-memory byte at 16-bit offset 2 (byte offset 4) will have 
        /// 0x11 written to it and the next byte (byte offset 5) will have 0x22 
        /// written to it.  This field must not be NULL. 
        /// </param>
        /// <param name="callback">callback function</param>
        /// <param name="flags">read flags.  May be zero or a combination of the following:
        /// SelectFlags.SELECT - perform one or more selects before performing
        ///   the read.
        /// SelectFlags.POST_MATCH - perform post-singulation mask match on
        ///   singulated tags.</param>
        /// <returns></returns>
        public Result SyncWrite(
            ushort offset,
            ushort count,
            MemoryBank bank,
            UInt32 password,
            byte tagStopCount,
            UInt16[] data,
            TagAccessCallbackDelegate callback,
            SelectFlags flags)
        {
            if (State != RFState.IDLE)
                return Result.RADIO_BUSY;

            try
            {
                if (data == null || data.Length == 0)
                    return ReturnState(Result.INVALID_PARAMETER);
#if __NORMAL_MODE__
                if (m_save_resp_mode != ResponseMode.EXTENDED)
                {
                    //TurnOn Debug infomation
                    ThrowException(SetRadioResponseDataMode(ResponseMode.EXTENDED));
                }
#endif
                accessCallback = callback;
                WriteParms parms = new WriteParms(true);

//                Native.CallbackDelegate cb = new Native.CallbackDelegate(InternalCallback);
//                parms.common.callback = Marshal.GetFunctionPointerForDelegate(cb);

                parms.common.callbackCode = IntPtr.Zero;
                parms.common.context = IntPtr.Zero;
                parms.common.tagStopCount = (uint)tagStopCount;
                parms.writeType = WriteType.SEQUENTIAL;
                parms.accessPassword = password;

                using (parms.writeSequentialParms = new WriteSequentialParms(bank, count, offset, data))
                {
//                    ReturnState(TagWrite(parms, flags));
                }

            }
            catch (ReaderException ex)
            {
#if DEBUG
                CSLibrary.Diagnostics.CoreDebug.Logger.DebugException("HighLevelInterface.SyncTagWrite()", ex);
#endif
            }
            catch (Exception ex)
            {
#if DEBUG
                CSLibrary.Diagnostics.CoreDebug.Logger.DebugException("HighLevelInterface.SyncTagWrite()", ex);
#endif
            }
            return m_Result;
        }
        /// <summary>
        /// Executes a tag write for the tags of interest.  If the
        /// SelectFlags.SELECT flag is specified, the tag population is
        /// partitioned (i.e., ISO 18000-6C select) prior to the tag-write operation.
        /// If the RFID_FLAG_PERFORM_POST_MATCH flag is specified, the post-singulation
        /// match mask is applied to a singulated tag's EPC to determine if the tag
        /// will be written to.  Writes may only be performed on 16-bit word boundaries
        /// and for multiples of 16-bit words.  If one or more of the specified memory
        /// words do not exist or are write-locked, the write to the tag will fail and
        /// this failure will be reported through the operation-response packet.  The
        /// operation-response packets will be returned to the application via
        /// the application-supplied callback function.  Each tag-write record is
        /// grouped with its corresponding tag-inventory record.  An application may
        /// prematurely stop a write operation by calling
        /// RFID_Radio{Cancel|Abort}Operation on another thread or by returning a non-
        /// zero value from the callback function.  A tag write may not be
        /// issued while a radio module is executing a tag-protocol operation.
        /// </summary>
        /// <param name="offset">
        /// An array of count 16-bit values that specify 16-bit tag-
        /// memory-bank offsets, with zero being the first 16-bit word in the 
        /// memory bank, where the corresponding 16-bit words in the pData 
        /// array will be written.  i.e., the 16-bit word in pData[n] will be written to 
        /// the 16-bit tag-memory-bank offset contained in pOffset[n].  This 
        /// field must not be NULL. 
        /// </param>
        /// <param name="count">
        /// The number of 16-bit words to be written to the tag's specified 
        /// memory bank.  For version 1.1 of the RFID Reader Library, this 
        /// parameter must contain a value between 1 and 8, inclusive. 
        /// </param>
        /// <param name="bank">
        /// The memory bank from which to read.  Valid values are: 
        /// <para>MemoryBank.RESERVED </para>
        /// <para>MemoryBank.EPC  </para>
        /// <para>MemoryBank.TID </para>
        /// <para>MemoryBank.USER </para>
        /// </param>
        /// <param name="password">
        /// The access password for the tags.  A value of zero indicates no 
        /// access password. 
        /// </param>
        /// <param name="tagStopCount">
        /// The maximum number of tags to which the tag-protocol operation 
        /// will be applied.  If this number is zero, then the operation is applied 
        /// to all tags that match the selection, and optionally post-singulation, 
        /// match criteria (within the constraints of the antenna-port dwell time 
        /// and inventory cycle count – see ).  If this number is non-zero, the 
        /// antenna-port dwell-time and inventory-cycle-count constraints still 
        /// apply, however the operation will be prematurely terminated if the 
        /// maximum number of tags have the tag-protocol operation applied to 
        /// them.  For version 1.3, this field may have a maximum value 255. 
        /// </param>
        /// <param name="data">
        /// A buffer of count 16-bit values to be written 
        /// sequentially to the tag's specified memory bank.  The high-order 
        /// byte of pData[n] will be written to the tag's memory-bank byte at 
        /// 16-bit offset (offset+n).  The low-order byte will be written to the 
        /// next byte.  For example, if offset is 2 and pData[0] is 0x1122, 
        /// then the tag-memory byte at 16-bit offset 2 (byte offset 4) will have 
        /// 0x11 written to it and the next byte (byte offset 5) will have 0x22 
        /// written to it.  This field must not be NULL. 
        /// </param>
        /// <param name="callback">callback function</param>
        /// <param name="flags">read flags.  May be zero or a combination of the following:
        /// SelectFlags.SELECT - perform one or more selects before performing
        ///   the read.
        /// SelectFlags.POST_MATCH - perform post-singulation mask match on
        ///   singulated tags.</param>
        /// <returns></returns>
        public Result SyncWrite(
            UInt16[] offset,
            ushort count,
            MemoryBank bank,
            UInt32 password,
            byte tagStopCount,
            UInt16[] data,
            TagAccessCallbackDelegate callback,
            SelectFlags flags)
        {
            if (State != RFState.IDLE)
                return Result.RADIO_BUSY;

            try
            {
                if (data == null || data.Length == 0 || offset == null || offset.Length == 0 || offset.Length != data.Length)
                    return ReturnState(Result.INVALID_PARAMETER);
#if __NORMAL_MODE__
                if (m_save_resp_mode != ResponseMode.EXTENDED)
                {
                    //TurnOn Debug infomation
                    ThrowException(SetRadioResponseDataMode(ResponseMode.EXTENDED));
                }
#endif
                accessCallback = callback;
                WriteParms parms = new WriteParms(true);

//                Native.CallbackDelegate cb = new Native.CallbackDelegate(InternalCallback);
//                parms.common.callback = Marshal.GetFunctionPointerForDelegate(cb);

                parms.common.callbackCode = IntPtr.Zero;
                parms.common.context = IntPtr.Zero;
                parms.common.tagStopCount = (uint)tagStopCount;
                parms.writeType = WriteType.RANDOM;
                parms.accessPassword = password;

                using (parms.writeRandomParms = new WriteRandomParms(bank, count, offset, data))
                {
//                    ReturnState(TagWrite(parms, flags));
                }

            }
            catch (ReaderException ex)
            {
#if DEBUG
                CSLibrary.Diagnostics.CoreDebug.Logger.DebugException("HighLevelInterface.SyncTagWrite()", ex);
#endif
            }
            catch (Exception ex)
            {
#if DEBUG
                CSLibrary.Diagnostics.CoreDebug.Logger.DebugException("HighLevelInterface.SyncTagWrite()", ex);
#endif
            }
            return m_Result;
        }
        /// <summary>
        /// Executes a tag lock for the tags of interest.  If the
        /// SelectFlags.SELECT flag is specified, the tag population is
        /// partitioned (i.e., ISO 18000-6C select) prior to the tag-lock operation.
        /// If the SelectFlags.POST_MATCH flag is specified, the post-singulation
        /// match mask is applied to a singulated tag's EPC to determine if the tag
        /// will be locked.  The operation-response packets will be returned to the
        /// application via the application-supplied callback function.  Each tag-lock
        /// record is grouped with its corresponding tag-inventory record.  An
        /// application may prematurely stop a lock operation by calling
        /// RFID_Radio{Cancel|Abort}Operation on another thread or by returning a non-
        /// zero value from the callback function.  A tag lock may not be
        /// issued while a radio module is executing a tag-protocol operation.
        /// </summary>
        /// <param name="password">
        /// The access password for the tags.  A value of zero indicates no 
        /// access password. 
        /// </param>
        /// <param name="kilMemoryBank">
        /// The access permissions for the tag's kill password.  
        /// </param>
        /// <param name="accMemoryBank">
        /// The access permissions for the tag's access password. 
        /// </param>
        /// <param name="epcMemoryBank">
        /// The access permissions for the tag's EPC memory bank.  
        /// </param>
        /// <param name="tidMemoryBank">
        /// The access permissions for the tag's TID memory bank.
        /// </param>
        /// <param name="usrMemoryBank">
        /// The access permissions for the tag's user memory bank.
        /// </param>
        /// <param name="tagStopCount">
        /// The maximum number of tags to which the tag-protocol operation 
        /// will be applied.  If this number is zero, then the operation is applied 
        /// to all tags that match the selection, and optionally post-singulation, 
        /// match criteria (within the constraints of the antenna-port dwell time 
        /// and inventory cycle count – see ).  If this number is non-zero, the 
        /// antenna-port dwell-time and inventory-cycle-count constraints still 
        /// apply, however the operation will be prematurely terminated if the 
        /// maximum number of tags have the tag-protocol operation applied to 
        /// them.  For version 1.3, this field may have a maximum value 255. 
        /// </param>
        /// <param name="callback">callback function</param>
        /// <param name="flags">read flags.  May be zero or a combination of the following:
        /// SelectFlags.SELECT - perform one or more selects before performing
        ///   the read.
        /// SelectFlags.POST_MATCH - perform post-singulation mask match on
        ///   singulated tags.</param>
        /// <returns></returns>
        public Result SyncLock(
            UInt32 password,
            Permission kilMemoryBank,
            Permission accMemoryBank,
            Permission epcMemoryBank,
            Permission tidMemoryBank,
            Permission usrMemoryBank,
            byte tagStopCount,
            TagAccessCallbackDelegate callback,
            SelectFlags flags)
        {
            if (State != RFState.IDLE)
                return Result.RADIO_BUSY;

            try
            {
#if __NORMAL_MODE__
                if (m_save_resp_mode != ResponseMode.EXTENDED)
                {
                    //TurnOn Debug infomation
                    ThrowException(SetRadioResponseDataMode(ResponseMode.EXTENDED));
                }
#endif
                accessCallback = callback;
                LockParms parms = new LockParms(true);

//                Native.CallbackDelegate cb = new Native.CallbackDelegate(InternalCallback);
//                parms.common.callback = Marshal.GetFunctionPointerForDelegate(cb);
                parms.common.callbackCode = IntPtr.Zero;
                parms.common.context = IntPtr.Zero;
                parms.common.tagStopCount = (uint)tagStopCount;

                parms.lockCmdParms.permissions.accessPasswordPermissions = accMemoryBank;
                parms.lockCmdParms.permissions.epcMemoryBankPermissions = epcMemoryBank;
                parms.lockCmdParms.permissions.killPasswordPermissions = kilMemoryBank;
                parms.lockCmdParms.permissions.tidMemoryBankPermissions = tidMemoryBank;
                parms.lockCmdParms.permissions.userMemoryBankPermissions = usrMemoryBank;

                parms.accessPassword = password;

//                ReturnState(TagLock(parms, flags));
            }
            catch (ReaderException ex)
            {
#if DEBUG
                CSLibrary.Diagnostics.CoreDebug.Logger.DebugException("HighLevelInterface.SyncTagLock()", ex);
#endif
            }
            catch (Exception ex)
            {
#if DEBUG
                CSLibrary.Diagnostics.CoreDebug.Logger.DebugException("HighLevelInterface.SyncTagLock()", ex);
#endif
            }
            return m_Result;
        }
        /// <summary>
        /// Executes a tag kill for the tags of interest.  If the
        /// SelectFlags.SELECT flag is specified, the tag population is
        /// partitioned (i.e., ISO 18000-6C select) prior to the tag-kill operation.
        /// If the RFID_FLAG_PERFORM_POST_MATCH flag is specified, the post-singulation
        /// match mask is applied to a singulated tag's EPC to determine if the tag
        /// will be killed.  The operation-response packets will be returned to the
        /// application via the application-supplied callback function.  Each tag-kill
        /// record is grouped with its corresponding tag-inventory record.  An
        /// application may prematurely stop a kill operation by calling
        /// RFID_Radio{Cancel|Abort}Operation on another thread or by returning a non-
        /// zerovalue from the callback function.  A tag kill may not be
        /// issued while a radio module is executing a tag-protocol operation.
        /// </summary>
        /// <param name="killPassword">
        /// The kill password for the tags. 
        /// </param>
        /// <param name="killCmd">
        /// Extended Kill command for UHF class 1 gen-2 version 1.2
        /// </param>
        /// <param name="tagStopCount">
        /// <c>Please use 1 for tag kill.</c>
        /// <para>The maximum number of tags to which the tag-protocol operation 
        /// will be applied.  If this number is zero, then the operation is applied 
        /// to all tags that match the selection, and optionally post-singulation, 
        /// match criteria (within the constraints of the antenna-port dwell time 
        /// and inventory cycle count – see ).  If this number is non-zero, the 
        /// antenna-port dwell-time and inventory-cycle-count constraints still 
        /// apply, however the operation will be prematurely terminated if the 
        /// maximum number of tags have the tag-protocol operation applied to 
        /// them.  For version 1.3, this field may have a maximum value 255. </para>
        /// </param>
        /// <param name="callback">callback function</param>
        /// <param name="flags">read flags.  May be zero or a combination of the following:
        /// SelectFlags.SELECT - perform one or more selects before performing
        ///   the read.
        /// SelectFlags.POST_MATCH - perform post-singulation mask match on
        ///   singulated tags.</param>
        /// <returns></returns>
        public Result SyncKill(
            UInt32 killPassword,
            ExtendedKillCommand killCmd,
            byte tagStopCount,
            TagAccessCallbackDelegate callback,
            SelectFlags flags)
        {
            if (State != RFState.IDLE)
                return Result.RADIO_BUSY;

            try
            {
#if __NORMAL_MODE__
                if (m_save_resp_mode != ResponseMode.EXTENDED)
                {
                    //TurnOn Debug infomation
                    ThrowException(SetRadioResponseDataMode(ResponseMode.EXTENDED));
                }
#endif
                accessCallback = callback;
                KillParms parms = new KillParms(true);

//                Native.CallbackDelegate cb = new Native.CallbackDelegate(InternalCallback);
//                parms.common.callback = Marshal.GetFunctionPointerForDelegate(cb);

                parms.common.callbackCode = IntPtr.Zero;
                parms.common.context = IntPtr.Zero;
                parms.common.tagStopCount = (uint)tagStopCount;
                parms.killCmdParms.killPassword = killPassword;
                parms.killCmdParms.exCommand = killCmd;

//                ReturnState(TagKill(parms, flags));

            }
            catch (ReaderException ex)
            {
#if DEBUG
                CSLibrary.Diagnostics.CoreDebug.Logger.DebugException("HighLevelInterface.SyncTagKill()", ex);
#endif
            }
            catch (Exception ex)
            {
#if DEBUG
                CSLibrary.Diagnostics.CoreDebug.Logger.DebugException("HighLevelInterface.SyncTagKill()", ex);
#endif
            }

            return m_Result;
        }

        /// <summary>
        /// Executes a tag inventory for the tags of interest.  If the
        /// SelectFlags.SELECT flag is specified, the tag population is
        /// partitioned (i.e., ISO 18000-6C select) prior to the inventory operation.
        /// If the RFID_FLAG_PERFORM_POST_MATCH flag is specified, the post-singulation
        /// match mask is applied to a singulated tag's EPC to determine if the tag
        /// will be returned to the application.  The operation-response packets
        /// will be returned to the application via the application-supplied callback
        /// function.  An application may prematurely stop an inventory operation by
        /// calling RFID_Radio{Cancel|Abort}Operation on another thread or by returning
        /// a non-zero value from the callback function.  A tag inventory may not be
        /// issued while a radio module is executing a tag-protocol operation.
        /// </summary>
        /// <param name="tagStopCount">
        /// The maximum number of tags to which the tag-protocol operation 
        /// will be applied.  If this number is zero, then the operation is applied 
        /// to all tags that match the selection, and optionally post-singulation, 
        /// match criteria (within the constraints of the antenna-port dwell time 
        /// and inventory cycle count – see ).  If this number is non-zero, the 
        /// antenna-port dwell-time and inventory-cycle-count constraints still 
        /// apply, however the operation will be prematurely terminated if the 
        /// maximum number of tags have the tag-protocol operation applied to 
        /// them.  For version 1.3, this field may have a maximum value 255. 
        /// </param>
        /// <param name="callback">callback function</param>
        /// <param name="flags">read flags.  May be zero or a combination of the following:
        /// SelectFlags.SELECT - perform one or more selects before performing
        ///   the read.
        /// SelectFlags.POST_MATCH - perform post-singulation mask match on
        ///   singulated tags.</param>
        /// <returns></returns>
        public Result SyncInventory(
            byte tagStopCount,
            InventoryCallbackDelegate callback,
            SelectFlags flags)
        {
            if (State != RFState.IDLE)
                return Result.RADIO_BUSY;

            try
            {
#if __NORMAL_MODE__
                if (m_save_resp_mode != ResponseMode.COMPACT)
                {
                    //TurnOff Debug infomation to speed up inventory
                    ThrowException(SetRadioResponseDataMode(ResponseMode.COMPACT));
                }
#endif
                inventoryCallback = callback;
//                Native.CallbackDelegate cb = new Native.CallbackDelegate(InternalCallback);
                
                InventoryParms parms = new InventoryParms(true);
//                parms.common.callback = Marshal.GetFunctionPointerForDelegate(cb);
                parms.common.callbackCode = IntPtr.Zero;
                parms.common.context = IntPtr.Zero;
                parms.common.tagStopCount = (uint)tagStopCount;

//                ThrowException(TagInventory(parms, flags));

            }
            catch (ReaderException ex)
            {
#if DEBUG
                CSLibrary.Diagnostics.CoreDebug.Logger.DebugException("HighLevelInterface.SyncTagLock()", ex);
#endif
            }
            catch (Exception ex)
            {
#if DEBUG
                CSLibrary.Diagnostics.CoreDebug.Logger.DebugException("HighLevelInterface.SyncTagLock()", ex);
#endif
            }

            return m_Result;
        }
        public Result SyncBlockWrite(
            ushort offset,
            ushort count,
            MemoryBank bank,
            UInt32 password,
            byte tagStopCount,
            UInt16[] data,
            TagAccessCallbackDelegate callback,
            SelectFlags flags)
        {
            if (State != RFState.IDLE)
                return Result.RADIO_BUSY;

            try
            {
                if (data == null || data.Length == 0 || count == 0)
                    return Result.INVALID_PARAMETER;

#if __NORMAL_MODE__
                if (m_save_resp_mode != ResponseMode.EXTENDED)
                {
                    //TurnOn Debug infomation
                    ThrowException(SetRadioResponseDataMode(ResponseMode.EXTENDED));
                }
#endif
                accessCallback = callback;
                BlockWriteParms parms = new BlockWriteParms(true);

//                Native.CallbackDelegate cb = new Native.CallbackDelegate(InternalCallback);
//                parms.common.callback = Marshal.GetFunctionPointerForDelegate(cb);

                parms.common.callbackCode = IntPtr.Zero;
                parms.common.context = IntPtr.Zero;
                parms.common.tagStopCount = (uint)tagStopCount;
                parms.accessPassword = password;

                using (parms.blockWriteCmdParms = new BlockWriteCmdParms(bank, count, offset, data))
                {
//                    ReturnState(TagBlockWrite(parms, flags));
                }
            }
            catch (ReaderException ex)
            {
#if DEBUG
                CSLibrary.Diagnostics.CoreDebug.Logger.DebugException("HighLevelInterface.SyncTagBlockWrite()", ex);
#endif
            }
            catch (Exception ex)
            {
#if DEBUG
                CSLibrary.Diagnostics.CoreDebug.Logger.DebugException("HighLevelInterface.SyncTagBlockWrite()", ex);
#endif
            }
            return m_Result;
        }
        public Result SyncBlockErase(
            UInt16 offset,
            UInt16 count,
            MemoryBank bank,
            UInt32 password,
            byte tagStopCount,
            TagAccessCallbackDelegate callback,
            SelectFlags flags)
        {
            if (State != RFState.IDLE)
                return Result.RADIO_BUSY;

            try
            {
                if (count == 0)
                    return Result.INVALID_PARAMETER;

#if __NORMAL_MODE__
                if (m_save_resp_mode != ResponseMode.EXTENDED)
                {
                    //TurnOn Debug infomation
                    ThrowException(SetRadioResponseDataMode(ResponseMode.EXTENDED));
                }
#endif
                accessCallback = callback;
                BlockEraseParms parms = new BlockEraseParms(true);

//                Native.CallbackDelegate cb = new Native.CallbackDelegate(InternalCallback);
//                parms.common.callback = Marshal.GetFunctionPointerForDelegate(cb);

                parms.common.callbackCode = IntPtr.Zero;
                parms.common.context = IntPtr.Zero;
                parms.common.tagStopCount = (uint)tagStopCount;
                parms.accessPassword = password;
                parms.blockEraseCmdParms = new BlockEraseCmdParms(bank, count, offset);

//                ReturnState(TagBlockErase(parms, flags));
            }
            catch (ReaderException ex)
            {
#if DEBUG
                CSLibrary.Diagnostics.CoreDebug.Logger.DebugException("HighLevelInterface.SyncTagBlockErase()", ex);
#endif
            }
            catch (Exception ex)
            {
#if DEBUG
                CSLibrary.Diagnostics.CoreDebug.Logger.DebugException("HighLevelInterface.SyncTagBlockErase()", ex);
#endif
            }
            return m_Result;
        }

        public Result SyncPermalock(
            ushort offset,
            ushort count,
            UInt32 password,
            ushort[] mask,
            PermalockFlags permalockFlag,
            byte tagStopCount,
            TagAccessCallbackDelegate callback,
            SelectFlags flags)
        {
            if (State != RFState.IDLE)
                return Result.RADIO_BUSY;

            try
            {
#if __NORMAL_MODE__
                if (m_save_resp_mode != ResponseMode.EXTENDED)
                {
                    //TurnOn Debug infomation
                    ThrowException(SetRadioResponseDataMode(ResponseMode.EXTENDED));
                }
#endif
                accessCallback = callback;
                PermalockParms parms = new PermalockParms(true);

//                Native.CallbackDelegate cb = new Native.CallbackDelegate(InternalCallback);
//                parms.common.callback = Marshal.GetFunctionPointerForDelegate(cb);

                parms.common.callbackCode = IntPtr.Zero;
                parms.common.context = IntPtr.Zero;
                parms.common.tagStopCount = (uint)tagStopCount;
                parms.accessPassword = password;

                using (parms.permalockCmdParms = new PermalockCmdParms(permalockFlag, count, offset, mask.Clone() as ushort[]))
                {
//                    ReturnState(TagPermalock(parms, flags));
                }
            }
            catch (ReaderException ex)
            {
#if DEBUG
                CSLibrary.Diagnostics.CoreDebug.Logger.DebugException("HighLevelInterface.SyncPermalock()", ex);
#endif
            }
            catch (Exception ex)
            {
#if DEBUG
                CSLibrary.Diagnostics.CoreDebug.Logger.DebugException("HighLevelInterface.SyncPermalock()", ex);
#endif
            }
            return m_Result;
        }
        /// <summary>
        /// An application may require that the tag population be logically partitioned into 
        /// disjoint groups prior to issuing an inventory operation or access command.  After 
        /// the tags are partitioned, the specified operation may then be applied to one of 
        /// the groups.  Two pieces of information are used to partition a tag population into 
        /// disjoint groups and control to which tags an operation is applied.  These pieces 
        /// of information, which are explained in subsequent sections, are:  a tag mask
        /// and the action that is to be performed on partitioned tag populations 
        /// </summary>
        /// <param name="offset">
        /// The offset, in bits, from the start of the memory bank, of the 
        /// first bit that will be matched against the mask.  If offset falls 
        /// beyond the end of the memory bank, the tag is considered 
        /// non-matching. 
        /// </param>
        /// <param name="count">
        /// The number of bits in the mask.  A length of zero will cause all 
        /// tags to match.  If (offset+count) falls beyond the end of 
        /// the memory bank, the tag is considered non-matching.  Valid 
        /// values are 0 to 255, inclusive. 
        /// </param>
        /// <param name="bank">
        /// The memory bank that contains the bits that will be compared 
        /// against the bit pattern specified in mask.  For a tag mask, 
        /// <see cref="MemoryBank.RESERVED"/> is not a valid value. 
        /// </param>
        /// <param name="target">
        /// Specifies what flag, selected (i.e., SL) or one of the four inventory 
        /// flags (i.e., S0, S1, S2, or S3), will be modified by the action. 
        /// </param>
        /// <param name="action">
        /// Specifies the action that will be applied to the tag populations (i.e, the 
        /// matching and non-matching tags). 
        /// </param>
        /// <param name="enableTruncate">
        /// Specifies if, during singulation, a tag will respond to a subsequent 
        /// inventory operation with its entire Electronic Product Code (EPC) or 
        /// will only respond with the portion of the EPC that immediately follows 
        /// the bit pattern (as long as the bit pattern falls within the EPC – if the 
        /// bit pattern does not fall within the tag’s EPC, the tag ignores the tag 
        /// partitioning operation2).  If this parameter is true: 
        /// <para>      bank must be <see cref="MemoryBank.EPC"/>. </para>
        /// <para>      target must be <see cref="Target.SELECTED"/>. </para>
        /// This action must correspond to the last tag select operation issued 
        /// before the inventory operation or access command. 
        /// </param>
        /// <param name="mask">
        /// A mask that contains a left-justified bit array that represents 
        /// that bit pattern to match 
        /// </param>
        /// <returns></returns>
        public Result SyncMatch(
            UInt16 offset,
            UInt16 count,
            MemoryBank bank,
            Target target,
            CSLibrary.Constants.Action action,
            bool enableTruncate,
            S_MASK mask)
        {
            try
            {
                SelectCriterion[] sel = new SelectCriterion[1];
                sel[0] = new SelectCriterion();
                sel[0].action = new SelectAction(
                    target,
                    action,
                    enableTruncate ? 1 : 0);
                sel[0].mask = new SelectMask(
                    bank,
                    offset,
                    count,
                    mask.ToBytes());
                if ((m_Result = SetSelectCriteria(sel)) != Result.OK)
                {
                    goto EXIT;
                }

            }
            catch (System.Exception ex)
            {
#if DEBUG
                CSLibrary.Diagnostics.CoreDebug.Logger.ErrorException("HighLevelInterface.SyncMatch()", ex);
#endif
                m_Result = Result.SYSTEM_CATCH_EXCEPTION;
            }
        EXIT:
            return m_Result;
        }

        private byte RFID_18K6C_TAG_ACCESS_PADDING_BYTES(byte f)
        {
            return (byte)(((f) >> 6) & 0x3);
        }

        private  void PrintReadData(Byte[] pData, UInt32 length)
        {
#if DEBUG
            Debug.WriteLine(Hex.ToString(pData, 0, length));
#endif
        }
#endregion

#region ====================== ReaderBase Function ======================

        const uint HOSTIF_ERR_SELECTORBNDS = 0x010E;
        public Result AntennaPortSetState(UInt32 antennaPort, AntennaPortState state)
	    {
            uint registerValue = 0;

            // First, tell the MAC which antenna descriptors we'll be reading and
		    // verify that it was a valid selector
            MacWriteRegister(MacRegister.HST_ANT_DESC_SEL, antennaPort);

            MacReadRegister(MacRegister.MAC_ERROR, ref registerValue);

            if (registerValue == HOSTIF_ERR_SELECTORBNDS)
		    {
			    MacClearError();
			    return Result.INVALID_PARAMETER;
		    }

		    // Read the current value of the anteann port configuration
            MacReadRegister(MacRegister.HST_ANT_DESC_CFG, ref registerValue);

		    // Now set the enabled bit appropriately
		    switch (state)
		    {
                case AntennaPortState.DISABLED:
			    {
				    registerValue &= ~((uint)1);
				    break;
			    } // case RFID_ANTENNA_PORT_STATE_DISABLED
                case AntennaPortState.ENABLED:
			    {
                    registerValue |= 1;
				    break;
			    } // case RFID_ANTENNA_PORT_STATE_ENABLED
		        default:
				    return Result.INVALID_PARAMETER;
		    } // switch (state)

		    // Write back the configuration register
            return MacWriteRegister(MacRegister.HST_ANT_DESC_CFG, registerValue);

	    } // Radio::SetAntennaPortState

        public Result AntennaPortSetStatus(uint port, AntennaPortStatus portStatus)
        {
		    UInt32 registerValue = 0;

            // First, tell the MAC which antenna descriptors we'll be reading and
		    // verify that it was a valid selector
            MacWriteRegister(MacRegister.HST_ANT_DESC_SEL, port);

            MacReadRegister(MacRegister.MAC_ERROR, ref registerValue);
            
            if (registerValue == HOSTIF_ERR_SELECTORBNDS)
            {
			    MacClearError();
			    return Result.INVALID_PARAMETER;
		    }

		    // Read the current value of the anteann port configuration
            MacReadRegister(MacRegister.HST_ANT_DESC_CFG, ref registerValue);

            registerValue &= 0xfff00000;

		    // Now set the enabled bit appropriately
		    switch (portStatus.state)
		    {
                case AntennaPortState.DISABLED:
			        {
				        //HST_ANT_DESC_CFG_SET_DISABLED//(registerValue);
                        registerValue &= ~((uint)1);
				        break;
			        } // case RFID_ANTENNA_PORT_STATE_DISABLED
                case AntennaPortState.ENABLED:
			        {
				        //HST_ANT_DESC_CFG_SET_ENABLED(registerValue);
                        registerValue |= 1;
				        break;
			        } // case RFID_ANTENNA_PORT_STATE_ENABLED
		        default:
			        {
				        return Result.INVALID_PARAMETER;
				        break;
			        } // default
		    } // switch (state)

            if (portStatus.easAlarm)
            {
                registerValue |= 1U << 20;
            }
            else
            {
                registerValue &= ~(1U << 20);
            }

            if (portStatus.enableLocalInv)
		    {
			    //HST_ANT_DESC_CFG_SET_LOCAL_INV(registerValue);
			    //HST_ANT_DESC_CFG_SET_INV_ALGO(registerValue, pStatus->inv_algo);
			    //HST_ANT_DESC_CFG_SET_STARTQ(registerValue, pStatus->startQ);
                registerValue |= 1 << 1;
                registerValue |= (uint)portStatus.inv_algo << 2;
                registerValue |= (uint)portStatus.startQ << 4;
            }
		    else
		    {
			    //HST_ANT_DESC_CFG_SET_GLOBAL_INV(registerValue);
                registerValue &= ~((uint)1 << 1);
		    }

            if (portStatus.enableLocalProfile)
		    {
			    //HST_ANT_DESC_CFG_SET_LOCAL_PROFILE(registerValue);
			    //HST_ANT_DESC_CFG_SET_PROFILE(registerValue, pStatus->profile);
                registerValue |= (uint)1 << 8;
                registerValue |= (uint)portStatus.profile << 9;
            }
		    else
		    {
			    //HST_ANT_DESC_CFG_SET_GLOBAL_PROFILE(registerValue);
                registerValue &= ~((uint)1 << 8);
            }

            if (portStatus.enableLocalFreq)
		    {
			    //HST_ANT_DESC_CFG_SET_LOCAL_FREQ(registerValue);
			    //HST_ANT_DESC_CFG_SET_FREQ_CHN(registerValue, pStatus->freqChn);
                registerValue |= (uint)1 << 13;
                registerValue |= (uint)portStatus.freqChn << 14;
            }
		    else
		    {
			    //HST_ANT_DESC_CFG_SET_GLOBAL_FREQ(registerValue);
                registerValue &= ~((uint)1 << 13);
            }

		    // Write back the configuration register
            return MacWriteRegister(MacRegister.HST_ANT_DESC_CFG, registerValue);
        }

        public Result AntennaPortGetStatus(uint port, AntennaPortStatus portStatus)
        {
		    UInt32 registerValue = 0;

            // First, tell the MAC which antenna descriptors we'll be reading and
		    // verify that it was a valid selector
            MacWriteRegister(MacRegister.HST_ANT_DESC_SEL, port);

            MacReadRegister(MacRegister.MAC_ERROR, ref registerValue);
            
            if (registerValue == HOSTIF_ERR_SELECTORBNDS)
            {
			    MacClearError();
			    return Result.INVALID_PARAMETER;
		    }

		    // Get the state of the antenna
            MacReadRegister(MacRegister.HST_ANT_DESC_CFG, ref registerValue);

            portStatus.state = ((registerValue & 0x01) != 0x00 ? AntennaPortState.ENABLED : AntennaPortState.DISABLED);
            portStatus.enableLocalInv = (registerValue & (1 << 1)) != 00;
            portStatus.inv_algo = (SingulationAlgorithm)(registerValue >> 2 & 0x03);
            portStatus.startQ = registerValue >> 4 & 0x0f;
            portStatus.enableLocalProfile = (registerValue & (1 << 8)) != 00;
            portStatus.profile = registerValue >> 9 & 0x0f;
            portStatus.enableLocalFreq = (registerValue & (1 << 13)) != 00;
            portStatus.freqChn = registerValue >> 14 & 0x3f;

    	    // Now read the anteanna sense value
            MacReadRegister(MacRegister.MAC_ANT_DESC_STAT, ref registerValue);
            portStatus.antennaSenseValue = registerValue;
            
            return Result.OK;
        }

        Result AntennaPortSetConfiguration(uint port, AntennaPortConfig antenna)
        {
		    UInt32 registerValue = 0;

            // First, tell the MAC which antenna descriptors we'll be reading and
		    // verify that it was a valid selector
            MacWriteRegister(MacRegister.HST_ANT_DESC_SEL, port);

            MacReadRegister(MacRegister.MAC_ERROR, ref registerValue);
            
            if (registerValue == HOSTIF_ERR_SELECTORBNDS)
            {
			    MacClearError();
			    return Result.INVALID_PARAMETER;
		    }

		    // Write the antenna dwell, RF power, inventory cycle count, and sense
		    // resistor threshold registers
            MacWriteRegister(MacRegister.HST_ANT_DESC_DWELL, antenna.dwellTime);

            //uint abc = 3;

            //if (port == 0)
            //    abc = 4;

            MacWriteRegister(MacRegister.HST_ANT_DESC_RFPOWER, antenna.powerLevel);

            //MacReadRegister(MacRegister.HST_ANT_DESC_RFPOWER, ref abc);

            //MacWriteRegister(MacRegister.HST_ANT_DESC_RFPOWER, abc);



/*            if (antenna.dwellTime == 0 && antenna.numberInventoryCycles == 0)
                MacWriteRegister(MacRegister.HST_ANT_DESC_INV_CNT, 65535);
            else */
                MacWriteRegister(MacRegister.HST_ANT_DESC_INV_CNT, antenna.numberInventoryCycles);
            //MacWriteRegister(MacRegister.HST_RFTC_ANTSENSRESTHRSH, antenna.antennaSenseThreshold);

            return Result.OK;
        }
            
        Result AntennaPortGetConfiguration (uint port, AntennaPortConfig antenna)
        {
		    UInt32 registerValue = 0;

            // First, tell the MAC which antenna descriptors we'll be reading and
		    // verify that it was a valid selector
            MacWriteRegister(MacRegister.HST_ANT_DESC_SEL, port);

            MacReadRegister(MacRegister.MAC_ERROR, ref registerValue);
            
            if (registerValue == HOSTIF_ERR_SELECTORBNDS)
            {
			    MacClearError();
			    return Result.INVALID_PARAMETER;
		    }

		    // Read the physical port mapping register
            MacReadRegister(MacRegister.HST_ANT_DESC_PORTDEF, ref registerValue);

            antenna.physicalTxPort = registerValue & 0x03;
            antenna.physicalRxPort = (registerValue >> 16) & 0x03;

		    // Read the antenna dwell time, RF power, inventory cycle count, and 
		    // sense resistor registers
            MacReadRegister(MacRegister.HST_ANT_DESC_DWELL, ref antenna.dwellTime);
            MacReadRegister(MacRegister.HST_ANT_DESC_RFPOWER, ref antenna.powerLevel);
            MacReadRegister(MacRegister.HST_ANT_DESC_INV_CNT, ref antenna.numberInventoryCycles);
            //MacReadRegister(MacRegister.HST_RFTC_ANTSENSRESTHRSH, ref antenna.antennaSenseThreshold);

            return Result.OK;
        }

#endregion

#region ====================== Engineering Function ======================

#region ====================== Private Variable ======================

        private const string ENGINEERING_PASSWORD = "CSL2006";
        private bool EngineeringMode = false;
        private int m_stop_cw = 1;

#endregion

#region ====================== Engineering Functions ======================

        public Result GetTxOnTime(ref UInt16 ms)
        {
            uint value = 0;

            if (m_save_country_code == 2 || m_save_country_code == 4)
            {
                if ((m_Result = MacWriteRegister(MacRegister.HST_PROTSCH_TXTIME_SEL /*0x305*/, 0)) != Result.OK)
                    return m_Result;
            }
            else
            {
                if ((m_Result = MacWriteRegister(MacRegister.HST_PROTSCH_TXTIME_SEL /*0x305*/, 1)) != Result.OK)
                    return m_Result;
            }
                
            if ((m_Result = MacReadRegister(MacRegister.HST_PROTSCH_TXTIME_ON, ref value)) == Result.OK)
                ms = (UInt16)value;

            return m_Result;
        }

        public Result SetTxOnTime(UInt16 ms)
        {
            if (m_save_country_code == 2 || m_save_country_code == 4)
            {
                if ((m_Result = MacWriteRegister(MacRegister.HST_PROTSCH_TXTIME_SEL /*0x305*/, 0)) != Result.OK)
                    return m_Result;
            }
            else
            {
                if ((m_Result = MacWriteRegister(MacRegister.HST_PROTSCH_TXTIME_SEL /*0x305*/, 1)) != Result.OK)
                    return m_Result;
            }

            return MacWriteRegister(MacRegister.HST_PROTSCH_TXTIME_ON, ms);
        }

        //Reverse Power Threshold
        public Result SetReversePowerThreshold(UInt16 value)
        {
            return MacWriteRegister(MacRegister.HST_RFTC_REVPWRTHRSH, value);
        }

        const uint HST_CMNDIAGS_INVRESP_ENABLED = 0x10, HST_CMNDIAGS_STATUS_ENABLED = 0x02, HST_CMNDIAGS_DIAGS_ENABLED = 0x01;
        /// <summary>
        /// Allows the application to retrieve the mode of data reporting for 
        /// tag-access operations.  The data-reporting mode may not be 
        /// retrieved while a radio module is executing a tag-protocol 
        /// operation. 
        /// </summary>
        /// <param name="mode">return will contain  
        /// the operation-response data reporting 
        /// mode for the data type specifed by 
        /// responseType.</param>
        /// <returns></returns>
        public Result EngGetReaderDataFormat(ref ResponseMode mode)
        {
            uint value = 0;

            if (!EngineeringMode)
                return Result.NOT_SUPPORTED;

            if (MacReadRegister(MacRegister.HST_CMNDIAGS /*0x0201 HST_CMNDIAGS*/, ref value) != Result.OK)
                return Result.FAILURE;

            if (value == HST_CMNDIAGS_INVRESP_ENABLED)
            {
                mode = ResponseMode.COMPACT;
            }
            else if (value == (HST_CMNDIAGS_INVRESP_ENABLED | HST_CMNDIAGS_STATUS_ENABLED))
            {
                mode = ResponseMode.NORMAL;
            }
            else if (value == (HST_CMNDIAGS_INVRESP_ENABLED | HST_CMNDIAGS_STATUS_ENABLED | HST_CMNDIAGS_DIAGS_ENABLED))
            {
                mode = ResponseMode.EXTENDED;
            }
            else
            {
                mode = ResponseMode.UNKNOWN;
            }

            return Result.OK;
        }

        /// <summary>
        /// Allows the application to control the mode of data reporting for 
        /// tag-access operations.  By default, when an application opens a 
        /// radio, the RFID Reader Library sets the reporting mode to 
        /// "normal".  The data-reporting mode will remain in effect until a 
        /// subsequent call to RFID_RadioSetResponseDataMode, or the radio 
        /// is closed and re-opened (at which point the data mode is set to 
        /// normal).  The data-reporting mode may not be changed while a 
        /// radio module is executing a tag-protocol operation. 
        /// </summary>
        /// <param name="mode">The requested data-reporting mode for 
        /// the data type specified by 
        /// responseType</param>
        /// <returns></returns>
        public Result EngSetReaderDataFormat(ResponseMode mode)
        {
            uint value = 0;

            if (!EngineeringMode)
                return Result.NOT_SUPPORTED;

            if (mode == ResponseMode.UNKNOWN)
                return Result.INVALID_PARAMETER;

            if (MacReadRegister(MacRegister.HST_CMNDIAGS /*0x0201 HST_CMNDIAGS*/, ref value) != Result.OK)
                return Result.FAILURE;

            value &= ~(HST_CMNDIAGS_INVRESP_ENABLED | HST_CMNDIAGS_STATUS_ENABLED | HST_CMNDIAGS_DIAGS_ENABLED);

            switch (mode)
            {
                case ResponseMode.EXTENDED:
                    {
                        // Set the diagnostics bit in the register
                        //HST_CMNDIAGS_SET_DIAGS_ENABLED(registerValue);
                        value |= HST_CMNDIAGS_INVRESP_ENABLED | HST_CMNDIAGS_STATUS_ENABLED | HST_CMNDIAGS_DIAGS_ENABLED;
                        // Fall through on purpose
                    } // case RFID_RESPONSE_MODE_EXTENDED
                    break;

                case ResponseMode.NORMAL:
                    {
                        // Set the status bit in the register
                        //HST_CMNDIAGS_SET_STATUS_ENABLED(registerValue);
                        value |= HST_CMNDIAGS_INVRESP_ENABLED | HST_CMNDIAGS_STATUS_ENABLED;
                        // Fall through on purpose
                    } // case RFID_RESPONSE_MODE_NORMAL
                    break;

                case ResponseMode.COMPACT:
                    {
                        // Set the inventory response bit in the register
                        //HST_CMNDIAGS_SET_INVRESP_ENABLED(registerValue);
                        value |= HST_CMNDIAGS_INVRESP_ENABLED;
                        // Set the RFU fields properly
                        // HST_CMNDIAGS_SET_RFU1(registerValue, 0);
                    } // case RFID_RESPONSE_MODE_COMPACT
                    break;

                default:
                    {
                        return Result.INVALID_PARAMETER;
                        break;
                    } // default
            } // switch (mode)

            return MacWriteRegister(MacRegister.HST_CMNDIAGS, value);
        }

        /// <summary>
        /// Enable Engineering Mode
        /// </summary>
        /// <param name="Password"></param>
        /// <returns></returns>
        public void EngModeEnable(string Password)
        {
            EngineeringMode = (Password == ENGINEERING_PASSWORD);
        }

        /// <summary>
        /// Fast connect to reader (only for R&D)
        /// </summary>
        /// <param name="DeviceName">Destination Device Name</param>
        /// <value="x.x.x.x">Destination IP Address</value>
        /// <value="USB">Destination First USB Reader</value>
        /// <value="USB<sn>">Destination specified USB Reader</value>
        /// <value="COM<x>">Destination specified Serial Reader *** Not support "Detect connection break down" feature</value>
        /// <param name="TimeOut">Connection Timeout</param>
        /// <returns></returns>
        public Result EngConnect(string DeviceName)
        {
            if (!EngineeringMode)
                return Result.NOT_SUPPORTED;

            if (bShutdownRequired)
                return Result.ALREADY_OPEN;

            try
            {
                if (COMM_Connect(DeviceName) != true)
                    return Result.FAILURE;

                bShutdownRequired = true;

                ThrowException(MacClearError());

                // Get OEM data
                // 0x0000 : date
                // 0x0002 : Region Code
                // 0x00a0 : Get Device Interface // no use
                // 0x00a3 : Get API Mode
                // 0x00a4 : Get Device Type
                // 0x00a5 : Max Out Power
                // 0x00a6 : Max Traget Power

                m_oem_machine = GetOEMDeviceType;          // First OEM Action
                //                m_save_country_code = OEMCountryCode;   // Second OEM Action
                //                m_oem_hipower = OEMHiPower;
                //                m_oem_maxpower = OEMMaxPower; // last OEM action
                //                m_save_readerName = m_oem_machine.ToString() + " RFID Reader";

                FireStateChangedEvent(RFState.IDLE);
            }
            catch (ReaderException ex)
            {
                Disconnect();
#if DEBUG
                CSLibrary.Diagnostics.CoreDebug.Logger.ErrorException("HighLevelInterface.Connect()", ex);
#endif
                m_Result = ex.ErrorCode;
            }
            catch (System.Exception ex)
            {
#if DEBUG
                CSLibrary.Diagnostics.CoreDebug.Logger.ErrorException("HighLevelInterface.Connect()", ex);
#endif
                m_Result = Result.SYSTEM_CATCH_EXCEPTION;
            }
            return m_Result;
        }

        /// <summary>
        /// Set connection intergface
        /// </summary>
        /// <param name="Mode"></param>
        /// <returns></returns>
        public Result EngSetInterface(INTERFACETYPE Mode)
        {
            switch (Mode)
            {
                case INTERFACETYPE.USB:
                    return MacWriteOemData(0xa0, 0x00);

                case INTERFACETYPE.IPV4:
                    return MacWriteOemData(0xa0, 0x01);

                case INTERFACETYPE.SERIAL:
                    return MacWriteOemData(0xa0, 0x02);
            }
            return Result.INVALID_PARAMETER;
        }

        /// <summary>
        /// Read Register
        /// </summary>
        public Result EngReadRegister(UInt16 add, ref UInt32 value)
        {
            if (!EngineeringMode)
                return Result.NOT_SUPPORTED;

            return MacReadRegister((MacRegister)(add), ref value);
        }

        /// <summary>
        /// Write Register
        /// </summary>
        public Result EngWriteRegister(UInt16 add, UInt32 value)
        {
            if (!EngineeringMode)
                return Result.NOT_SUPPORTED;

            return MacWriteRegister((MacRegister)(add), value);
        }

        /// <summary>
        /// Reads one or more 32-bit values from the MAC's OEM 
        /// configuration data area.  Note that the the 32-bit values read from 
        /// the OEM configuration data area are in the R1000 Firmware-
        /// processor endian format and it is the responsibility of the 
        /// application to convert to the endian format of the host processor.  
        /// The MAC's OEM configuration data area may not be read while a 
        /// radio module is executing a tag-protocol operation. 
        /// </summary>
        /// <param name="address">
        /// The address of the first 32-bit value to read from the 
        /// MAC's OEM configuration data area.  Note that the 
        /// address is a 32-bit word address, and not a byte 
        /// address – i.e., address 1 is actually byte 4, address 2 
        /// is actually byte 8, etc.  An address that is beyond the 
        /// end of the OEM configuration data area Results in an 
        /// invalid-parameter error. </param>
        /// <param name="value">Return  the data from MAC's OEM 
        /// configuration data area.  The 32-bit values returned 
        /// are in the MAC's native format (i.e., little endian).  
        /// </param>
        /// <returns></returns>
        public Result EngReadOemData(UInt32 address, ref UInt32 value)
        {
            if (!EngineeringMode)
                return Result.NOT_SUPPORTED;

            return MacReadOemData(address, ref value);
        }

        /// <summary>
        /// Writes one or more 32-bit values to the MAC's OEM configuration 
        /// data area.  Note that it is the responsibility of the application 
        /// programmer to ensure that the 32-bit values written to the OEM 
        /// configuration data area areconverted from the host-processor 
        /// endian format to the MAC-processor endian format before they are 
        /// written.  The MAC's OEM configuration data area may not be 
        /// written while a radio module is executing a tag-protocol operation. 
        /// </summary>
        /// <param name="address">
        /// The 32-bit address into the MAC's OEM configuration 
        /// data area where the first 32-bit data word is to be 
        /// written.  Note that the address is a 32-bit address, and 
        /// not a byte address – i.e., address 1 is actually byte 4, 
        /// address 2 is actually byte 8, etc.  An address that is 
        /// beyond the end of the OEM configuration data area 
        /// Results in an invalid-parameter error. </param>
        /// <param name="value">
        /// A 32-bit unsigned integers 
        /// that contains the data to be written into the MAC's 
        /// OEM configuration data area.  The 32-bit values 
        /// provided must be in the MAC's native format (i.e., 
        /// little endian).  This parameter must not be NULL. </param>
        /// <returns></returns>
        public Result EngWriteOemData(uint address, uint value)
        {
            if (!EngineeringMode)
                return Result.NOT_SUPPORTED;

            return MacWriteOemData(address, value);
        }

        public Result EngReadOemData(UInt32 address, UInt32 count, UInt32[] pData)
        {
            if (!EngineeringMode)
                return Result.NOT_SUPPORTED;

            return MacReadOemData(address, count, pData);
        }

        public Result EngWriteOemData(UInt32 address, UInt32 count, UInt32[] pData)
        {
            if (!EngineeringMode)
                return Result.NOT_SUPPORTED;

            return MacWriteOemData(address, count, pData);
        }

        /// <summary>
        /// Reads directly from a radio-module hardware register.  The radio 
        /// module's hardware registers may not be read while a radio module 
        /// is executing a tag-protocol operation. 
        /// </summary>
        /// <param name="address">The 16-bit address of the radio-module hardware 
        /// register to be read.  An address that is beyond the end 
        /// of the radio module's register set Results in an invalid-
        /// parameter return status.</param>
        /// <param name="value">A 16-bit value that will receive the value 
        /// in the radio-module hardware register specified by 
        /// address. </param>
        /// <returns></returns>
        public Result EngBypassReadRegister(ushort address, ref ushort value)
        {
            if (!EngineeringMode)
                return Result.NOT_SUPPORTED;

            return MacBypassReadRegister(address, ref value);
        }

        /// <summary>
        /// Writes directly to a radio-module hardware register.  The radio 
        /// module's hardware registers may not be written while a radio 
        /// module is executing a tag-protocol operation. 
        /// </summary>
        /// <param name="address">The 16-bit address of the radio-module hardware 
        /// register to be written.  An address that is beyond the 
        /// end of the radio module's register set Results in an 
        /// invalid-parameter return status. </param>
        /// <param name="value">The 16-bit value to write to the radio-module 
        /// hardware register specified by address. </param>
        /// <returns></returns>
        public Result EngBypassWriteRegister(ushort address, ushort value)
        {
            if (!EngineeringMode)
                return Result.NOT_SUPPORTED;

            return MacBypassWriteRegister(address, value);
        }

        /// <summary>
        /// Writes the specified data to the radio module's nonvolatile-
        /// memory block(s).  After a successful update, the RFID radio 
        /// module resets itself and the RFID Reader Library closes and 
        /// invalidates the radio m_radioIndex so that it may no longer be used by 
        /// the application.  To obtain control of the radio again, the 
        /// application must re-enumerate, via 
        /// RFID_RetrieveAttachedRadiosList, the radio modules in the 
        /// system and request control of the radio again via 
        /// RFID_RadioOpen. 
        /// In the case of an unsuccessful update and depending upon the 
        /// underlying cause for the returned failure status, the radio 
        /// module's nonvolatile memory may be left in an undefined state, 
        /// which means that the radio module may be in an unusable state.  
        /// In this situation, the RFID Reader Library does not invalidate the 
        /// radio m_radioIndex – i.e., it is the application's responsibility to close the 
        /// m_radioIndex. 
        /// Alternatively, an application can perform the update in "test" 
        /// mode.  An application uses the "test" mode, by checking the 
        /// returned status, to verify that the update would succeed before 
        /// performing the destructive update of the radio module's 
        /// nonvolatile memory.  When a "test" update has completed, either 
        /// successfully or unsuccessfully, the MAC firmware returns to its 
        /// normal idle state and the radio m_radioIndex remains valid (indicating 
        /// that the application is still responsible for closing it). 
        /// The radio module's nonvolatile memory may not be updated while 
        /// a radio module is executing a tag-protocol operation. 
        /// </summary>
        /// <param name="length">
        /// The number of nonvolatile memory blocks in 
        /// the array pointed to by pBlocks.  This value 
        /// must be greater than zero. </param>
        /// <param name="pImage">
        /// An array of countBlocks nonvolatile 
        /// memory block structures that are used to control the update 
        /// of the radio module's nonvolatile memory.  This 
        /// pointer must not be NULL. </param>   
        /// <param name="flags">
        /// Firmware update flags </param>
        /// <returns></returns>
        public Result EngUpdateFirmware(uint length, NonVolatileMemoryBlock[] pImage, FwUpdateFlags flags)
        {
            const UInt16 NVMEMUPD_PKT_MAGIC = 0xF00D;
            const UInt16 NVMEMUPD_CMD_UPD_RANGE = 0x0001;

            byte[] RecvBuf = new byte[16];
            byte[] SendBuf = new byte[64];  // NVMEMPKT_UPD_RANGE
            UInt16 magic;
            UInt16 cmd;
            UInt16 pkt_len;
            UInt16 res0;
            UInt32 re_cmd;
            UInt32 status = 0;
            int chunkSize = 64 - 20;
            int offset = 0;

            if (!EngineeringMode)
                return Result.NOT_SUPPORTED;

            if (length == 0 || pImage == null)
                return Result.INVALID_PARAMETER;

            switch (flags)
            {
                case FwUpdateFlags.NVMEM_UPDATE_BL:
                case FwUpdateFlags.NVMEM_UPDATE_APP:
                case FwUpdateFlags.NVMEM_UPDATE_BL_TEST:
                case FwUpdateFlags.NVMEM_UPDATE_APP_TEST:
                    if (MacWriteOemData(0x3C4, 0) == Result.OK)
                        FireOnFirmwareUpgradeEvent(0xfffffffffffffffc);  // Write OK
                    else
                    {
                        FireOnFirmwareUpgradeEvent(0xfffffffffffffffd);  // Write Fail
                        return Result.FAILURE;
                    }

                    FireOnFirmwareUpgradeEvent(0xfffffffffffffffa);  // Write OK
                    COMM_Reboot();
                    //COMM_Disconnect();
                    //COMM_Connect(m_DeviceName, 3000);
                    // Get Bootloader version
                    //
                    break;
            }

            //Thread.Sleep(7000);

            m_Result = COMM_HostCommand(HST_CMD.NV_MEM_UPDATE);

            // Ready for Update FW
            if (m_Result != Result.OK)
                return Result.FAILURE;

            // Check Ready Packet
            if (COMM_READER_Recv(RecvBuf, 0, 16, 3000) == false)
                return Result.FAILURE;

            status = (UInt32)(RecvBuf[15] << 24 | RecvBuf[14] << 16 | RecvBuf[13] << 8 | RecvBuf[12]);
            if (status != 0x0e)
                return Result.FAILURE;

            // Start Send Data
            for (int block = 0; block < length; block++)
            {
                UInt32 address = pImage[block].address;
                UInt32 datalength = pImage[block].length;

                SendBuf[0] = 0x0d;  // magic ID
                SendBuf[1] = 0xf0;
                SendBuf[2] = 0x01;  // Command
                SendBuf[3] = 0x00;
                SendBuf[4] = 0x0e; // Packet Length
                SendBuf[5] = 0x00;
                SendBuf[6] = 0x00; // Reserved. read / write as zero
                SendBuf[7] = 0x00;
                SendBuf[12] = 0x00; // Reserved. read / write as zero
                SendBuf[13] = 0x00;
                SendBuf[14] = 0x00;
                SendBuf[15] = 0x00;
                switch (flags)
                {
                    case FwUpdateFlags.NVMEM_UPDATE:
                    case FwUpdateFlags.NVMEM_UPDATE_BL:
                    case FwUpdateFlags.NVMEM_UPDATE_APP:
                        SendBuf[16] = 0x00;
                        break;

                    default:
                        SendBuf[16] = 0x01;  // test mode
                        break;
                }
                SendBuf[17] = 0x00;
                SendBuf[18] = 0x00;
                SendBuf[19] = 0x00;

                while (offset < datalength)
                {
                    long realaddress = address + offset;
                    SendBuf[8] = (byte)(realaddress & 0xff);
                    SendBuf[9] = (byte)(realaddress >> 8 & 0xff);
                    SendBuf[10] = (byte)(realaddress >> 16 & 0xff);
                    SendBuf[11] = (byte)(realaddress >> 24 & 0xff);

                    Thread.Sleep(10); // Delay 10ms

                    if ((offset + chunkSize) > datalength)
                    {
                        int last_data_len = (int)(datalength - offset);
                        int sendpkt_len = 0x03 + ((last_data_len + 3) / 4);

                        SendBuf[4] = (byte)(sendpkt_len & 0xff); // packet length
                        SendBuf[16] |= (byte)((last_data_len % 4) << 1);

                        Marshal.Copy(new IntPtr(pImage[block].pData.ToInt64() + offset), SendBuf, 20, (int)(datalength - offset));
                        COMM_READER_Send(SendBuf, 0, (int)(datalength - offset) + 20, 1000);
                        offset = (int)datalength;
                    }
                    else
                    {
                        Marshal.Copy(new IntPtr(pImage[block].pData.ToInt64() + offset), SendBuf, 20, chunkSize);
                        COMM_READER_Send(SendBuf, 0, chunkSize + 20, 1000);
                        offset += chunkSize;
                    }

                    FireOnFirmwareUpgradeEvent((UInt64)offset);

                    if (COMM_READER_Recv(RecvBuf, 0, 16, 3000) == false)
                        break;

                    magic = (UInt16)(RecvBuf[1] << 8 | RecvBuf[0]);
                    cmd = (UInt16)(RecvBuf[3] << 8 | RecvBuf[2]);
                    pkt_len = (UInt16)(RecvBuf[5] << 8 | RecvBuf[4]);
                    res0 = (UInt16)(RecvBuf[7] << 8 | RecvBuf[6]);
                    re_cmd = (UInt32)(RecvBuf[11] << 24 | RecvBuf[10] << 16 | RecvBuf[9] << 8 | RecvBuf[8]);
                    status = (UInt32)(RecvBuf[15] << 24 | RecvBuf[14] << 16 | RecvBuf[13] << 8 | RecvBuf[12]);

                    if (magic != NVMEMUPD_PKT_MAGIC)
                        break;

                    // Act upon the return status
                    if (status != 0)
                        break;
                }

                m_Result = Result.FAILURE;

                // Send finish command
                if (offset == datalength)
                {
                    SendBuf[0] = 0x0d;  // magic ID
                    SendBuf[1] = 0xf0;
                    SendBuf[2] = 0x02;  // Command
                    SendBuf[3] = 0x00;
                    SendBuf[4] = 0x00; // Packet Length
                    SendBuf[5] = 0x00;
                    SendBuf[6] = 0x00; // Reserved. read / write as zero
                    SendBuf[7] = 0x00;

                    COMM_READER_Send(SendBuf, 0, 8, 1000);

                    if (COMM_READER_Recv(RecvBuf, 0, 16, 1000) == true)
                        m_Result = Result.OK;
                }
            }

            FireOnFirmwareUpgradeEvent(0xfffffffffffffffb);  // First time reboot
            switch (m_Result)
            {
                case Result.OK:
                    switch (flags)
                    {
                        case FwUpdateFlags.NVMEM_UPDATE_APP:
                            COMM_Reboot();
                            //Thread.Sleep(5000);
                            if (MacWriteOemData(0x3C4, 0x80000000) == Result.OK)
                                FireOnFirmwareUpgradeEvent(0xfffffffffffffffe);  // Send Write OEM 0x3c4 success to App
                            else
                                FireOnFirmwareUpgradeEvent(0xffffffffffffffff);  // Send Write OEM 0x3c4 fail to App
                            break;
                    }
                    break;
            }

            COMM_Reboot();

            return m_Result;
        }

        /// <summary>
        /// if 0 = First time reboot, 0xffffffffffffffff = second time reboot, write offset address
        /// </summary>
        /// <param name="Offset"></param>
        void FireOnFirmwareUpgradeEvent(UInt64 Offset)
        {
            if (OnFirmwareUpgrade != null)
            {
                OnFirmwareUpgradeEventArgs CurrentAddress = new OnFirmwareUpgradeEventArgs(Offset);

                try
                {
                    OnFirmwareUpgrade(this, CurrentAddress);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                }
            }
        }
        
        /// <summary>
        /// Turn on Carrier Wave
        /// </summary>
        /// <returns></returns>
        //[MethodImpl(MethodImplOptions.Synchronized)]
        public Result TurnCarrierWaveOn(bool isDataMode)
        {
            _EngineeringTest_Operation = 1;

            //            if (State == RFState.IDLE && !IsAlive())
            if (State == RFState.IDLE)
            {
                bDataMode = isDataMode;
                g_hWndThread = new Thread(new ThreadStart(TurnCarrierWaveOnThreadProc));
                g_hWndThread.Name = "TurnCarrierWaveOn";
                g_hWndThread.IsBackground = true;
                g_hWndThread.Start();
                WaitToBusy();
                return Result.OK;
            }

            return Result.RADIO_BUSY;
        }

        /// <summary>
        /// Turn off Carrier Wave
        /// </summary>
        /// <returns></returns>
        //[MethodImpl(MethodImplOptions.Synchronized)]
        public Result TurnCarrierWaveOff()
        {
            if (State != RFState.BUSY)
                return Result.OK;

            Interlocked.Exchange(ref m_stop_cw, 1);

            WaitToIdle();
            _EngineeringTest_Operation = 0;

            return Result.OK;
        }

        public Result EngDumpAllReg()
        {
            UInt16 [,] GRegSet = new UInt16[,] 
            {
                {0x000, 0x005},
                {0x100, 0x103},
                {0x200, 0x20B},
                {0x300, 0x30A},
                {0x400, 0x40A},
                {0x500, 0x501},
                {0x600, 0x603},
                {0x700, 0x707},
                {0x800, 0x80C},
                {0x900, 0x921},
                {0xA00, 0xA0F},
                {0xB00, 0xB84},
                {0xC00, 0xC08}
            };
            int cnt1, cnt2;
            UInt32 value = 0;

            //if (!EngineeringMode)
            //    return Result.NOT_SUPPORTED;

            if (State != RFState.IDLE)
                return Result.CURRENTLY_NOT_ALLOWED;


            TextWriter tw = new StreamWriter("REGDUMP.TXT", true);
            tw.WriteLine(DateTime.Now);

            // General Register
            for (cnt1 = 0; cnt1 < 13; cnt1++)
            {
                for (cnt2 = GRegSet[cnt1, 0]; cnt2 <= GRegSet[cnt1, 1]; cnt2++)
                {
                    if (MacReadRegister((MacRegister)cnt2, ref value) == Result.OK)
                        tw.WriteLine("Register 0x{0} = 0x{1}", cnt2.ToString("x4"), value.ToString("x8"));
                }
            }

            tw.Close();

            return Result.OK;
        }

#if nouse
        public Result EngDumpAllReg()
        {
            const UInt16 [,] GRegSet = new UInt16[,] 
            {
                {0xC00, 0xC08},
                {0xB00, 0xB84},
                {0xA00, 0xA0F},
                {0x900, 0x921},
                {0x800, 0x80C},
                {0x700, 0x707},
                {0x600, 0x603},
                {0x500, 0x501},
                {0x400, 0x40A},
                {0x300, 0x30A},
                {0x200, 0x20B},
                {0x100, 0x103},
                {0x000, 0x005}
            };
            const UInt16 [,,,] IRegSet = new UInt16[,,,] 
            {
                {0x302, 0x303, 0x000, 0x00a},
                {0x308, 0x309, 0x000, 0x007},
                {0x701, 0x702, 0x000, 0x00f},
                {0x701, 0x703, 0x000, 0x00f},
                {0x701, 0x704, 0x000, 0x00f},
                {0x701, 0x705, 0x000, 0x00f},
                {0x701, 0x706, 0x000, 0x00f},
                {0x701, 0x707, 0x000, 0x00f},
                {0x902, 0x903, 0x000, 0x003},
                {0x902, 0x904, 0x000, 0x003},
                {0x902, 0x905, 0x000, 0x003},
                {0x902, 0x906, 0x000, 0x003},
            };
            int cnt1, cnt2;
            UInt32 value = 0;


            if (!EngineeringMode)
                return Result.NOT_SUPPORTED;

            if (State != RFState.IDLE)
                return Result.CURRENTLY_NOT_ALLOWED;

            
            // General Register
            for (cnt1 = 0; cnt1 < GRegSet.Length; cnt1++)
            {
                for (cnt2 = GRegSet[cnt1, 0], cnt2 <= GRegSet[cnt1, 1]; cnt2++)
                {
                    if (MacReadRegister (cnt2, ref value) == Result.OK)
                        Console.WriteLine ();
                }
            }

            // Index Register
            for (cnt1 = 0; cnt1 < IRegSet.Length; cnt1++)
            {
                for (cnt2 = IRegSet[cnt1, 2], cnt2 <= GRegSet[cnt1, 3]; cnt2++)
                {
                    if (MacWriteRegister (IRegSet[cnt1, 0], cnt2) == Result.OK)

                    if (MacReadRegister (IRegSet[cnt1, 1], ref value) == Result.OK)
                        Console.WriteLine ();
                }
            }
        }
#endif

        ////////////////////////////////////////////////////////////////////////////////
        // Name:        ToggleCarrierWave
        // Description: Turns the radio's carrier wave on or off
        ////////////////////////////////////////////////////////////////////////////////
        private Result RadioTurnCarrierWaveOn()
        {
            return COMM_HostCommand(HST_CMD.CWON);
        }

        private Result RadioTurnCarrierWaveOff()
        {
            return COMM_HostCommand(HST_CMD.CWOFF);
        }

        private void TurnCarrierWaveOnThreadProc()
        {
            FireStateChangedEvent(RFState.BUSY);
            Interlocked.Exchange(ref m_stop_cw, 0);
            ushort fifostatus = 0;

            try
            {
                if ((m_Result = RadioTurnCarrierWaveOn()) != Result.OK)
                {
                    throw new ReaderException(m_Result);
                }

                while (true)
                {
                    if (Interlocked.Equals(m_stop_cw, 1))
                    {
                        break;
                    }
                    if (bDataMode)
                    {
                        if ((m_Result = MacBypassReadRegister(0x106, ref fifostatus)) != Result.OK)
                        {
                            throw new ReaderException(m_Result);
                        }
                        if ((fifostatus & 0xf) < 2 && !Interlocked.Equals(m_stop_cw, 1))
                        {
                            if ((m_Result = MacBypassWriteRegister(0x105, 0xe)) != Result.OK)
                            {
                                throw new ReaderException(m_Result);
                            }
                            if ((m_Result = MacBypassWriteRegister(0x105, 0xe)) != Result.OK)
                            {
                                throw new ReaderException(m_Result);
                            }
                            if ((m_Result = MacBypassWriteRegister(0x105, 0xe)) != Result.OK)
                            {
                                throw new ReaderException(m_Result);
                            }
                            if ((m_Result = MacBypassWriteRegister(0x105, 0xe)) != Result.OK)
                            {
                                throw new ReaderException(m_Result);
                            }
                            if ((m_Result = MacBypassWriteRegister(0x105, 0xe)) != Result.OK)
                            {
                                throw new ReaderException(m_Result);
                            }
                        }
                    }
                    else
                    {
                        Thread.Sleep(1);
                    }
                }
            }
            catch (ReaderException ex)
            {
#if DEBUG
                CSLibrary.Diagnostics.CoreDebug.Logger.ErrorException("HighLevelInterface.TurnCarrierWaveOnThreadProc()", ex);
#endif
            }
            catch (Exception e)
            {
                m_Result = Result.SYSTEM_CATCH_EXCEPTION;
#if DEBUG
                CSLibrary.Diagnostics.CoreDebug.Logger.ErrorException("HighLevelInterface.TurnCarrierWaveOnThreadProc()", e);
#endif
            }
            finally
            {
                m_Result = RadioTurnCarrierWaveOff();
                /*if (bDataMode)
                    Thread.Sleep(4000);*/
            }
            FireStateChangedEvent(RFState.IDLE);
        }

#endregion

#endregion

#region ====================== For Testing ======================
#if test
        void Set_FCC_Freq_Channel()
        {
            System.IO.StreamReader file;
            string[] line = new string[50];
            uint[] freqorder = new uint[50];
            int cnt;

            uint[] orgfccFreqTable = new uint[]
        {

            0x00180E1B, /*902.75 MHz   0*/
            0x00180E1D, /*903.25 MHz   1*/
            0x00180E1F, /*903.75 MHz   2*/
            0x00180E21, /*904.25 MHz   3*/
            0x00180E23, /*904.75 MHz   4*/
            0x00180E25, /*905.25 MHz   5*/
            0x00180E27, /*905.75 MHz   6*/
            0x00180E29, /*906.25 MHz   7*/
            0x00180E2B, /*906.75 MHz   8*/
            0x00180E2D, /*907.25 MHz   9*/
            0x00180E2F, /*907.75 MHz   10*/
            0x00180E31, /*908.25 MHz   11*/
            0x00180E33, /*908.75 MHz   12*/
            0x00180E35, /*909.25 MHz   13*/
            0x00180E37, /*909.75 MHz   14*/
            0x00180E39, /*910.25 MHz   15*/
            0x00180E3B, /*910.75 MHz   16*/
            0x00180E3D, /*911.25 MHz   17*/
            0x00180E3F, /*911.75 MHz   18*/
            0x00180E41, /*912.25 MHz   19*/
            0x00180E43, /*912.75 MHz   20*/
            0x00180E45, /*913.25 MHz   21*/
            0x00180E47, /*913.75 MHz   22*/
            0x00180E49, /*914.25 MHz   23*/
            0x00180E4B, /*914.75 MHz   24*/
            
            0x00180E4D, /*915.25 MHz   25*/
            0x00180E4F, /*915.75 MHz   26*/
            0x00180E51, /*916.25 MHz   27*/
            0x00180E53, /*916.75 MHz   28*/
            0x00180E55, /*917.25 MHz   29*/
            0x00180E57, /*917.75 MHz   30*/
            0x00180E59, /*918.25 MHz   31*/
            0x00180E5B, /*918.75 MHz   32*/
            0x00180E5D, /*919.25 MHz   33*/
            0x00180E5F, /*919.75 MHz   34*/
            0x00180E61, /*920.25 MHz   35*/
            0x00180E63, /*920.75 MHz   36*/
            0x00180E65, /*921.25 MHz   37*/
            0x00180E67, /*921.75 MHz   38*/
            0x00180E69, /*922.25 MHz   39*/
            0x00180E6B, /*922.75 MHz   40*/
            0x00180E6D, /*923.25 MHz   41*/
            0x00180E6F, /*923.75 MHz   42*/
            0x00180E71, /*924.25 MHz   43*/
            0x00180E73, /*924.75 MHz   44*/
            0x00180E75, /*925.25 MHz   45*/
            0x00180E77, /*925.75 MHz   46*/
            0x00180E79, /*926.25 MHz   47*/
            0x00180E7B, /*926.75 MHz   48*/
            0x00180E7D, /*927.25 MHz   49*/
        };

            try
            {
                file = new System.IO.StreamReader("fccHoppingSequence.txt");

                for (cnt = 0; cnt < 50; cnt++)
                {
                    line[cnt] = file.ReadLine();
                    freqorder[cnt] = uint.Parse(line[cnt]) - 1;
                }

                file.Close();

                for (cnt = 0; cnt < 50; cnt++)
                {
                    fccFreqSortedIdx[cnt] = freqorder[cnt];
                    fccFreqTable[cnt] = orgfccFreqTable[fccFreqSortedIdx[cnt]];
                }

                cnt = 0;
            }
            catch (Exception ex)
            {
            }
        }

        void set_pll()
        {
            string line;
            System.IO.StreamReader file;

            try
            {
                file = new System.IO.StreamReader("pll.txt");

                line = file.ReadLine();

                pllvalue = uint.Parse(line, System.Globalization.NumberStyles.AllowHexSpecifier);
                
                file.Close();
            }
            catch (Exception ex)
            {

            }
        }
#endif
#endregion
    }
}
