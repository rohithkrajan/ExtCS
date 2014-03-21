using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography;

namespace ExtCS.Debugger
{
    public static class StringExtensions
    {
        public static String GetHashString(this string inputString)
        {
            StringBuilder stb = new StringBuilder();
            
            foreach (byte item in GetHash(inputString))
            {
                stb.Append(item.ToString("X2"));
            }
            return stb.ToString();

        }
        public static byte[] GetHash(string inputString)
        {
            HashAlgorithm alg = MD5.Create();
            return alg.ComputeHash(Encoding.UTF8.GetBytes(inputString));

        }

        public static UInt64 ToUInt64(this string address)
        {
            address = FormatAddress(address);
            UInt64 hexAddress;
            if (UInt64.TryParse(address, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out hexAddress))
                return hexAddress;
            return UInt64.MinValue;
        }

        private static string FormatAddress(string address)
        {
            if (string.IsNullOrEmpty(address))
            {
                return string.Empty;
            }
            if (address[0] == '0' && address[1] == 'x')
            {
                //removing 0x from address
                return address.Substring(2);
            }
            return address;
        }
         public static UInt64 ToUInt64(this string address,string offset)
        {
            address = FormatAddress(address);
            UInt64 off,_address;
            if (!UInt64.TryParse(offset, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out off))
                throw new Exception("invalid address: " + offset);

            if (!UInt64.TryParse(address, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out _address))
                throw new Exception("invalid address: " + address);
            return _address + off;
            
        }
         public static string ToHex(this Int32 address)
         {
             return address.ToString("X");
         }
         public static string ToHex(this UInt64 address)
         {
             return address.ToString("X");
         }
    }
}
