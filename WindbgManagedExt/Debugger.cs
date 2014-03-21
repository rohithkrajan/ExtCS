using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DotNetDbg;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Globalization;

namespace ExtCS.Debugger
{
    public unsafe partial class Debugger
    {
        private static Debugger _current;
        const int S_OK = 0;
        private static Dictionary<string, UInt64> _loadedExtensions = new Dictionary<string, ulong>(10);
        public StringBuilder debugOutput = new StringBuilder(10);
        public const int E_FAIL = unchecked((int)0x80004005);
        public const int ERROR_INVALID_PARAMETER = unchecked((int)0x80070057);
        private ScriptContext _context;
        public Debugger(IDebugClient debugClient)
        {
            _DebugClient = debugClient as IDebugClient5;
            _current = this;
        }
        public Debugger(IDebugClient debugClient,ScriptContext  context)
        {
            _DebugClient = debugClient as IDebugClient5;
            _current = this;
            _context = context;
        }
        public static Debugger Current
        {
            get {return _current; }
        }
        public ScriptContext Context { get { return _context; } internal set { _context = value; } }
        
        private IDebugClient5 _DebugClient;
		/// <summary>IDebugClient5</summary>
		public IDebugClient5 DebugClient
		{
			get
			{
				return _DebugClient;
			}
		}

		private IDebugControl4 _DebugControl;
		/// <summary>IDebugControl4</summary>
		public IDebugControl4 DebugControl
		{
			get
			{
				if (_DebugControl == null)
				{
					_DebugControl = _DebugClient as IDebugControl4;
				}
				return _DebugControl;
			}
		}
		private IDebugControl6 _DebugControl6;
		/// <summary>IDebugControl5</summary>
		public IDebugControl6 DebugControl6
		{
			get
			{
				if (_DebugControl6 == null)
				{
					_DebugControl6 = _DebugClient as IDebugControl6;
				}
				return _DebugControl6;
			}
		}

		private IDebugDataSpaces4 _DebugDataSpaces;
		/// <summary>IDebugDataSpaces4</summary>
		public IDebugDataSpaces4 DebugDataSpaces
		{
			get
			{
				if (_DebugDataSpaces == null)
				{
					_DebugDataSpaces = _DebugClient as IDebugDataSpaces4;
				}
				return _DebugDataSpaces;
			}
		}

		private IDebugRegisters2 _DebugRegisters;
		/// <summary>IDebugRegisters2</summary>
		public IDebugRegisters2 DebugRegisters
		{
			get
			{
				if (_DebugRegisters == null)
				{
					_DebugRegisters = _DebugClient as IDebugRegisters2;
				}
				return _DebugRegisters;
			}
		}

		private IDebugSymbols3 _DebugSymbols;
		/// <summary>IDebugSymbols3</summary>
		public IDebugSymbols3 DebugSymbols
		{
			get
			{
				if (_DebugSymbols == null)
				{
					_DebugSymbols = _DebugClient as IDebugSymbols3;
				}
				return _DebugSymbols;
			}
		}

		private IDebugSymbols5 _DebugSymbols5;
		/// <summary>IDebugSymbols5</summary>
		public IDebugSymbols5 DebugSymbols5
		{
			get
			{
				if (_DebugSymbols5 == null)
				{
					_DebugSymbols5 = _DebugClient as IDebugSymbols5;
				}
				return _DebugSymbols5;
			}
		}

		private IDebugSystemObjects2 _DebugSystemObjects;
		/// <summary>IDebugSystemObjects2</summary>
		public IDebugSystemObjects2 DebugSystemObjects
		{
			get
			{
				if (_DebugSystemObjects == null)
				{
					_DebugSystemObjects = _DebugClient as IDebugSystemObjects2;
				}
				return _DebugSystemObjects;
			}
		}

		private IDebugAdvanced3 _DebugAdvanced;
		/// <summary>IDebugAdvanced3</summary>
		public IDebugAdvanced3 DebugAdvanced
		{
			get
			{
				if (_DebugAdvanced == null)
				{
					_DebugAdvanced = _DebugClient as IDebugAdvanced3;
				}
				return _DebugAdvanced;
			}
		}

        private bool firstCommand = false;
        private DEBUG_OUTCTL OutCtl = DEBUG_OUTCTL.THIS_CLIENT | DEBUG_OUTCTL.DML;


        public string Execute(string command)
        {
            //create a new debug control and assign the new ouput handler
            IntPtr outputCallbacks;
            IntPtr PreviousCallbacks;
            //IDebugClient executionClient;
            //int hr = this.DebugClient.CreateClient(out executionClient);
            //var newDebugger = new Debugger(executionClient);
            string output = null;
            //if (FAILED(hr))
            //{
            //    this.OutputVerboseLine("SimpleOutputHandler.Install Failed creating a new debug client for execution: {0:x8}", hr);
            //    outputCallbacks = IntPtr.Zero;

            //    return null;
            //}

            //save previous callbacks
            OutputDebugInfo("executing command {0} \n", command);
            PreviousCallbacks = SavePreviousCallbacks();

            int InstallationHRESULT = InitialiezOutputHandler();

            if (FAILED(InstallationHRESULT))
            {
                this.OutputVerboseLine("Failed installing a new outputcallbacks client for execution");
                outputCallbacks = IntPtr.Zero;
                return null;
            }
            //set the previous callback handler
            var hrExecution = this.DebugControl.Execute(DEBUG_OUTCTL.THIS_CLIENT, command, DEBUG_EXECUTE.DEFAULT | DEBUG_EXECUTE.NO_REPEAT);           
            if (FAILED(hrExecution))
            {
                this.OutputVerboseLine("Failed creating a new debug client for execution:");
                outputCallbacks = IntPtr.Zero;
                return null;
            }

            //revert previous callbacks
            InstallationHRESULT = RevertCallBacks(PreviousCallbacks);
            //getting the output from the buffer.
            output = OutHandler.ToString();
            OutputDebugInfo("command output:\n"+ output);
            OutHandler.stbOutPut.Length = 0;
            //releaseing the COM object.
            //Marshal.ReleaseComObject(outHandler);
            return output;
        }

        /// <summary>
        /// Returns true if a HRESULT indicates failure.
        /// </summary>
        /// <param name="hr">HRESULT</param>
        /// <returns>True if hr indicates failure</returns>
        public static bool FAILED(int hr)
        {
            return (hr < 0);
        }

        /// <summary>
        /// Returns true if a HRESULT indicates success.
        /// </summary>
        /// <param name="hr">HRESULT</param>
        /// <returns>True if hr indicates success</returns>
        public static bool SUCCEEDED(int hr)
        {
            return (hr >= 0);
        }



        private void OutputVerboseLine(string p)
        {
            OutputHelper(p, DEBUG_OUTPUT.VERBOSE);
        }
        public void OutputDebugInfo(string format,params object[] args)
        {
            if(this.Context.Debug)
                Output(string.Format("\ndebuginfo:"+format,args));
        }
        public void Output(object output)
        {
            // if (args != null && args.Length > 0)
            //   OutputHelper(output, args, DEBUG_OUTPUT.NORMAL | DEBUG_OUTPUT.VERBOSE);

            OutputHelper(output.ToString(), DEBUG_OUTPUT.NORMAL | DEBUG_OUTPUT.VERBOSE);
        }
       
        public void Output(string output)
        {
           // if (args != null && args.Length > 0)
             //   OutputHelper(output, args, DEBUG_OUTPUT.NORMAL | DEBUG_OUTPUT.VERBOSE);

            OutputHelper(output, DEBUG_OUTPUT.NORMAL|DEBUG_OUTPUT.VERBOSE);
        }
        //public void Output(string format,params string[] args)
        //{
        //    OutputHelper(output, DEBUG_OUTPUT.NORMAL | DEBUG_OUTPUT.VERBOSE);
        //}

        private int OutputHelper(string formattedString, DEBUG_OUTPUT outputType)
        {
           
            //formattedString = EscapePercents(formattedString);
            //debugOutput.Append(formattedString);
            
            return DebugControl.ControlledOutput(DEBUG_OUTCTL.ALL_OTHER_CLIENTS|DEBUG_OUTCTL.DML,outputType, formattedString);
            //return DebugControl.ControlledOutputWide(OutCtl, outputType, formattedString);
        }
        public string GetString(UInt64 address)
        {
            string strOut;
            if (SUCCEEDED(GetString(address,2000,out strOut)))
            {
                return strOut;
            }
            throw new Exception("unable to get the string from address " + address);
        }
        public string GetString(object objaddress)
        {
            UInt64 address;
            string strOut;
            if (UInt64.TryParse(objaddress.ToString(),NumberStyles.HexNumber,CultureInfo.InvariantCulture,out address))
            {
                return GetString(address);
            }
            throw new Exception("unable to get the string from address " + address);
        }

        /// <summary>
        /// Reads a null-terminated ANSI or Multi-byte string from the target.
        /// </summary>
        /// <param name="address">Address of the string</param>
        /// <param name="maxSize">Maximum number of bytes to read</param>
        /// <param name="output">The string</param>
        /// <returns>Last HRESULT received while retrieving the string</returns>
        public int GetString(UInt64 address, UInt32 maxSize, out string output)
        {
            return GetUnicodeString(address, maxSize, out output);
            //StringBuilder sb = new StringBuilder((int)maxSize + 1);
            //uint bytesRead;
            //int hr = DebugDataSpaces.ReadMultiByteStringVirtual(address, maxSize, sb, maxSize, &bytesRead);
            //if (SUCCEEDED(hr))
            //{
            //    if (bytesRead > maxSize)
            //    {
            //        sb.Length = (int)maxSize;
            //    }
            //    output = sb.ToString();
            //}
            //else
            //{
            //    output = null;
            //}
            //return hr;
        }
        /// <summary>
        /// Throws a new exception with the name of the parent function and value of the HR
        /// </summary>
        /// <param name="hr">Error # to include</param>
        internal void ThrowExceptionHere(int hr)
        {
            StackTrace stackTrace = new StackTrace();
            System.Diagnostics.StackFrame stackFrame = stackTrace.GetFrame(1);
            throw new Exception(String.Format("Error in {0}: {1}", stackFrame.GetMethod().Name, hr));
        }

        /// <summary>
        /// Reads a single pointer from the target.
        /// NOTE: POINTER VALUE IS SIGN EXTENDED TO 64-BITS WHEN NECESSARY!
        /// </summary>
        /// <param name="offset">The address to read the pointer from</param>
        /// <returns>The pointer</returns>
        public UInt64 ReadPointer(string offset)
        {
            UInt64 address;

            if (UInt64.TryParse(offset, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out address))
            {
                return ReadPointer(address);
            }
            throw new Exception("unable to convert address "+offset);

        }
        public UInt64 POI(object address)
        {
            return ReadPointer(address.ToString());
        }
        public UInt64 POI(object address, object offset)
        {
            return ReadPointer(address.ToString(), offset.ToString());
        }

        public UInt64 ReadPointer(string objaddress, string offset)
        {
            UInt64 address,off;

            if (!UInt64.TryParse(objaddress, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out address))
            {
                throw new Exception("unable to convert address " + objaddress);
                
            }

            if (!UInt64.TryParse(offset, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out off))
            {
                throw new Exception("unable to convert address " + offset);

            }


            return ReadPointer(address+off);
        }
        public UInt64 ReadPointer(UInt64 objaddress, string offset)
        {
            UInt64 address, off;

            if (!UInt64.TryParse(offset, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out off))
            {
                throw new Exception("unable to convert address " + offset);

            }


            return ReadPointer(objaddress + off);
        }
        public UInt64 ReadPointer(UInt64 objaddress, UInt64 offset)
        {
           
            return ReadPointer(objaddress + offset);
        }
        /// <summary>
        /// Reads a single pointer from the target.
        /// NOTE: POINTER VALUE IS SIGN EXTENDED TO 64-BITS WHEN NECESSARY!
        /// </summary>
        /// <param name="offset">The address to read the pointer from</param>
        /// <returns>The pointer</returns>
        public UInt64 ReadPointer(UInt64 offset)
        {
            UInt64[] pointerArray = new UInt64[1];
            int hr = DebugDataSpaces.ReadPointersVirtual(1, offset, pointerArray);
            if (FAILED(hr))
                ThrowExceptionHere(hr);

            return pointerArray[0];
        }


        /// <summary>
        /// Used to determine whether the debug target has a 64-bit pointer size
        /// </summary>
        /// <returns>True if 64-bit, otherwise false</returns>
        public bool IsPointer64Bit()
        {
            return (DebugControl.IsPointer64Bit() == S_OK) ? true : false;
        }
        /// <summary>
        /// Reads a 32-bit value from the target's virtual address space.
        /// </summary>
        /// <param name="address">Address to read from</param>
        /// <param name="value">UInt32 to receive the value</param>
        /// <returns>HRESULT</returns>
        public unsafe int ReadVirtual32(UInt64 address, out UInt32 value)
        {
            UInt32 tempValue;
            int hr = DebugDataSpaces.ReadVirtual(address, (IntPtr)(&tempValue), (uint)sizeof(UInt32), null);
            value = SUCCEEDED(hr) ? tempValue : 0;
            return hr;
        }

        /// <summary>
        /// Reads a 8-bit value from the target's virtual address space.
        /// </summary>
        /// <param name="address">Address to read from</param>
        /// <param name="value">Byte to receive the value</param>
        /// <returns>HRESULT</returns>
        public unsafe int ReadVirtual8(UInt64 address, out Byte value)
        {
            Byte tempValue;
            int hr = DebugDataSpaces.ReadVirtual(address, (IntPtr)(&tempValue), (uint)sizeof(Byte), null);
            value = SUCCEEDED(hr) ? tempValue : ((Byte)0);
            return hr;
        }

        /// <summary>
        /// Reads a 16-bit value from the target's virtual address space.
        /// </summary>
        /// <param name="address">Address to read from</param>
        /// <param name="value">Int16 to receive the value</param>
        /// <returns>HRESULT</returns>
        public unsafe int ReadVirtual16(UInt64 address, out Int16 value)
        {
            Int16 tempValue;
            int hr = DebugDataSpaces.ReadVirtual(address, (IntPtr)(&tempValue), (uint)sizeof(Int16), null);
            value = SUCCEEDED(hr) ? tempValue : ((Int16)0);
            return hr;
        }

        /// <summary>
        /// Reads a 64-bit value from the target's virtual address space.
        /// </summary>
        /// <param name="address">Address to read from</param>
        /// <param name="value">Int64 to receive the value</param>
        /// <returns>HRESULT</returns>
        public unsafe int ReadVirtual64(UInt64 address, out Int64 value)
        {
            Int64 tempValue;
            int hr = DebugDataSpaces.ReadVirtual(address, (IntPtr)(&tempValue), (uint)sizeof(Int64), null);
            value = SUCCEEDED(hr) ? tempValue : 0;
            return hr;
        }

        /// <summary>
        /// Reads a null-terminated Unicode string from the target.
        /// </summary>
        /// <param name="address">Address of the Unicode string</param>
        /// <param name="maxSize">Maximum number of bytes to read</param>
        /// <param name="output">The string</param>
        /// <returns>Last HRESULT received while retrieving the string</returns>
        public int GetUnicodeString(UInt64 address, UInt32 maxSize, out string output)
        {
            StringBuilder sb = new StringBuilder((int)maxSize + 1);
            uint bytesRead;
            int hr = DebugDataSpaces.ReadUnicodeStringVirtualWide(address, (maxSize * 2), sb, maxSize, &bytesRead);
            if (SUCCEEDED(hr))
            {
                if ((bytesRead / 2) > maxSize)
                {
                    sb.Length = (int)maxSize;
                }
                output = sb.ToString();
            }
            else if (ERROR_INVALID_PARAMETER == hr)
            {
                sb.Length = (int)maxSize;
                output = sb.ToString();
            }
            else
            {
                output = null;
            }
            return hr;
        }


        public void InstallCustomHandler(OutputHandler currentHandler, out IntPtr PreviousCallbacks)
        {
            
            PreviousCallbacks = SavePreviousCallbacks();

            IntPtr ThisIDebugOutputCallbacksPtr = Marshal.GetComInterfaceForObject(currentHandler, typeof(IDebugOutputCallbacks2));
            int InstallationHRESULT = this.DebugClient.SetOutputCallbacks(ThisIDebugOutputCallbacksPtr);
            
        }

        private int InitialiezOutputHandler()
        {
            //creating new output hanlder to redirect output.
            if (OutHandler == null)
            {
                OutHandler = new OutputHandler();
            }

            IntPtr ThisIDebugOutputCallbacksPtr = Marshal.GetComInterfaceForObject(OutHandler, typeof(IDebugOutputCallbacks2));
            int InstallationHRESULT = this.DebugClient.SetOutputCallbacks(ThisIDebugOutputCallbacksPtr);
            return InstallationHRESULT;
        }

        public bool Require(string extensionName)
        {
            extensionName = extensionName.ToLower().Trim();
            if (_loadedExtensions.ContainsKey(extensionName))
                return true;

            UInt64  handle;
            int hr=this.DebugControl.AddExtension(extensionName, 0, out handle);
            if (hr != S_OK)
            {
                OutputError("unable to load extension {0}", extensionName);
                return false;
            }
            _loadedExtensions.Add(extensionName, handle);
            OutputDebugInfo("loaded extension {0} \n", extensionName);
            return true;
        }

        public void OutputError(string p,params object[] args)
        {
            string formatted = String.Format(p, args);

            OutputHelper(formatted, DEBUG_OUTPUT.ERROR);

        }
        internal UInt64 GetExtensionHandle(string extensionName)
        {
            extensionName = extensionName.Trim().ToLower();
            if (_loadedExtensions.ContainsKey(extensionName))
            {
                return _loadedExtensions[extensionName];
            }
            else
            {
                Require(extensionName);
                return _loadedExtensions[extensionName];
            }
            

        }

        public int RevertCallBacks(IntPtr PreviousCallbacks)
        {

            this.DebugClient.FlushCallbacks();
            //restoring the previous callbacks
            var InstallationHRESULT = this.DebugClient.SetOutputCallbacks(PreviousCallbacks);

            return InstallationHRESULT;
        }

        private IntPtr SavePreviousCallbacks()
        {
            IntPtr PreviousCallbacks;
            this.DebugClient.FlushCallbacks();
            //get previous callbacks
            //saving the previous callbacks
            this.DebugClient.GetOutputCallbacks(out PreviousCallbacks); /* We will need to release this */
            
            return PreviousCallbacks;
        }


        public bool IsFirstCommand
        {
            get
            {
                return firstCommand;
            }
            set
            {
                firstCommand = value;
                if (value == false)
                {
                    OutCtl = DEBUG_OUTCTL.THIS_CLIENT|DEBUG_OUTCTL.NOT_LOGGED;
                }
                else
                {
                    OutCtl = 
                        DEBUG_OUTCTL.THIS_CLIENT;
                }
            }
        }




        private static OutputHandler OutHandler;

        //public OutputHandler OutHandler { get { return outHandler; } set { if(outHandler!=null) outHandler = value; } }

        internal void OutputError(string p, string method, string args)
        {
            throw new NotImplementedException();
        }
    }
}
