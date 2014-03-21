using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExtCS.Debugger
{
    public class Address
    {
        private UInt64 _address;
        private string _hexaddress;
        public Address(string address)
        {
            address = FormatAddress(address);
            _hexaddress = address;
            if (!UInt64.TryParse(address, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out _address))
                throw new Exception("invalid address: " + address);
        }

        public Address(string address,string offset)
        {
            address = FormatAddress(address);
            UInt64 off;
            if (!UInt64.TryParse(offset, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out off))
                throw new Exception("invalid address: " + offset);
            
            if (!UInt64.TryParse(address, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out _address))
                throw new Exception("invalid address: " + address);
            _address = _address + off;
            _hexaddress = _address.ToString("X");
        }

        public Address(UInt64 address)
        {
            _address = address;
            _hexaddress = address.ToString("X");
        }
        public Address(UInt64 address,UInt64 offset)
        {
            _address = address+offset;
            _hexaddress = address.ToString("X");
        }
        private string FormatAddress(string address)
        {
            if (address[0]=='0' && address[1]=='x')
            {
                //removing 0x from address
                return address.Substring(2);
            }
            return address;
        }
        public bool HasValue
        {
            get 
            {
                int chkVal;
                //chaning the address to a integer value
                //if this anything other than zero,this is vakid address
                //possible values come like 00000000000,000000
                if (Int32.TryParse(_hexaddress,out chkVal))
                {
                    if (chkVal==0)
                    {
                        return false;
                    }
                    return true;
                    
                }
                else
                    return true;
            }
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
        public string ToHex()
        {
            return _hexaddress;
        }

        public override string ToString()
        {
            return ToHex();
        }
        
        public uint GetInt32Value()
        {
            uint output;
            if(!SUCCEEDED(Debugger.Current.ReadVirtual32(_address,out output)))
                throw new Exception("unable to get the int32 from address " + _address);

            return output;

        }
        public Int16 GetInt16Value()
        {
            Int16 output;
            if (!SUCCEEDED(Debugger.Current.ReadVirtual16(_address, out output)))
                throw new Exception("unable to get the int16 from address " + _address);

            return output;

        }
        public Byte GetByte()
        {
            Byte output;
            if (!SUCCEEDED(Debugger.Current.ReadVirtual8(_address, out output)))
                throw new Exception("unable to get the byte from address " + _address);

            return output;
        }

        private int GetString(UInt64 address, UInt32 maxSize, out string output)
        {
            return Debugger.Current.GetUnicodeString(address, maxSize, out output);
        }

        public string GetManagedString()
        {
            string strOut;
            ulong offset=Debugger.Current.IsPointer64Bit() ? 12UL:8UL;
            if (SUCCEEDED(GetString(_address+offset, 2000, out strOut)))
            {
                return strOut;
            }
            throw new Exception("unable to get the string from address " + _address);
        }

        public string GetString()
        {
            string strOut;
            if (SUCCEEDED(GetString(_address, 2000, out strOut)))
            {
                return strOut;
            }
            throw new Exception("unable to get the string from address " + _address);
        }
        public string GetString(uint maxlength)
        {
            string strOut;
            if (SUCCEEDED(GetString(_address, maxlength, out strOut)))
            {
                return strOut;
            }
            throw new Exception("unable to get the string from address " + _address);
        }
        
    }
}
