#r "C:\Program Files\Debugging Tools for Windows (x64)\ExtCS.Debugger.dll"
#r "System.Data"
#r "System.Xml"
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ExtCS.Debugger;
using System.Data;
using System.Globalization;

var d = Debugger.Current;
var Args = d.Context.Args;

if (Args.HasArgument("-count"))
	d.Output(Args["-count"]);

if (Args.HasArgument("-maxvalue"))
	d.Output(Args["-maxvalue"]);


DataTable dt = new DataTable();
dt.Columns.Add(new DataColumn("Name"));
dt.Columns.Add(new DataColumn("Details"));
dt.Columns.Add(new DataColumn("Address"));
dt.Columns.Add(new DataColumn("Address2"));

dt.Rows.Add("User 1", "Support Escalation engineer", "bangalore 39", "BTm first stage");
dt.Rows.Add("User", "Support Escalation engineer 1234", "bangalore 393433", "BTm first stage,512060");
dt.Rows.Add("User User", "Support Escalation engineer", "bangalore 39", "BTm first stage");
dt.Rows.Add("Rohith", "Escalation engineer", "bangalore 39", "BTm first stage");
dt.Rows.Add("User 3373737", "Support Escalation engineer", "bangalore 39", "BTm first stage 832377363737");
dt.Rows.Add("223232 ", "Support Escalation ", "bangalore 39282828", "BTm first stage");
d.Output(Helpers.GetTableData(dt));

public static class Helpers
{

	public static string GetContextMethodTable ()
	{

		var d = Debugger.Current;
		var sos = new Extension("sos.dll");
		string sMethodTable = sos.Call("!Name2EE System.Web.dll!System.Web.HttpContext");
		var rgMt = new System.Text.RegularExpressions.Regex(@"MethodTable:\W(?<methodtable>\S*)", System.Text.RegularExpressions.RegexOptions.Multiline);
		var matches = rgMt.Match(sMethodTable);

		if (matches.Success)

			//d.Output("matched");
			return matches.Groups["methodtable"].Value;

		throw new Exception("ünable to get Method table of HttpContext\n");

	}

	public static string GetPaddedString (char ch, int count)
	{
		Char[] strReturn = new char[count];
		for (int i = 0; i < count; i++)
		{
			strReturn[i] = ch;
		}
		return new String(strReturn);
	}

	/// <summary>
	/// this method returns the pad requesred for each row value
	/// this functions should have intergaretd to calculating columns counts for perf improvement.
	/// </summary>
	public static string[]GetColumnPadStrings (DataRow currentRow, int[] columnPaddings)
	{
		string[] rowPaddings = new string[columnPaddings.Length];

		for (int cCount = 0; cCount < columnPaddings.Length; cCount++)
		{
			rowPaddings[cCount] = GetPaddedString(' ', (columnPaddings[cCount] - currentRow[cCount].ToString().Length) + 2);
		}
		return rowPaddings;
	}
	public static string[] GetHeaderPadStrings(DataTable table, int[] columnPaddings)
	{
		string[] rowPaddings = new string[columnPaddings.Length];

		for (int cCount = 0; cCount < columnPaddings.Length; cCount++)
		{
			rowPaddings[cCount] = GetPaddedString(' ', (columnPaddings[cCount] - table.Columns[cCount].ColumnName.Length) + 2);
		}
		return rowPaddings;
	}

	public static string GetTableData (DataTable table)
	{
		StringBuilder stbTable = new StringBuilder();
		//int[] columnFrontPaddings = new int[table.Columns.Count];
		//padding count to make the space between colunmns even
		//even if the lenght of each column value is different,padding makes it equal
		//find the largest length
		int[] columnEndPaddings = new int[table.Columns.Count];
		int columnCount=0;

		StringBuilder stbColumnFormat = new StringBuilder();
		//each column's format value
		//this will be made to a format string.
		Dictionary<string, string> dictColumnFormats = new Dictionary<string, string>();
		//this will make a format string columnname1{0}columnname2{1}comunname3{2}
		//{0},{1} will hold the paadded string for each column
		foreach (DataColumn column in table.Columns)
		{

			stbColumnFormat.Append("<b>"+column.ColumnName+"</b>").Append("{" + columnCount.ToString() + "}");
			dictColumnFormats.Add(column.ColumnName, string.Empty);
			columnEndPaddings[columnCount] = column.ColumnName.Length;
			columnCount++;

		}
		int rowCount=0;
		List<string> lstRows = new List<string>();
		columnCount = 0;
		//lstRows will hold the format string for each rows
		//rowsvalue1{0}rowvalue{1}rowvalue{2}
		StringBuilder stbRows = new StringBuilder(table.Columns.Count);
		foreach (DataRow row in table.Rows)
		{
			foreach (DataColumn rowColumn in table.Columns)
			{
				string value = row[rowColumn].ToString();
				stbRows.Append(value + "{" + columnCount + "}");
				if (columnEndPaddings[columnCount] < value.Length)
					columnEndPaddings[columnCount] = value.Length;
				columnCount++;
			}
			lstRows.Add(stbRows.ToString());
			stbRows.Length = 0;
			columnCount = 0;
		}
		//columnendpadding will contain maximum length of charcters in each column
		//columnEndPaddings[0] will contain the maximum length of column
		string[] paddedstrings = new string[columnEndPaddings.Length];
		stbTable.AppendFormat(stbColumnFormat.ToString(), GetHeaderPadStrings(table, columnEndPaddings));
		stbTable.AppendLine();
		stbTable.Append(GetPaddedString('=', stbTable.Length));
		stbTable.AppendLine();
		for (int rCount = 0; rCount < lstRows.Count; rCount++)
		{
			stbTable.AppendFormat(lstRows[rCount], GetColumnPadStrings(table.Rows[rCount], columnEndPaddings));
			stbTable.AppendLine();
		}



		return stbTable.ToString();
		//for (int i = 0; i < columnEndPaddings.Length; i++)
		//{
		//	paddedstrings[i] = GetPaddedString(' ', columnEndPaddings[i]);
		//}
		//StringBuilder stbFinalTableFormat = new StringBuilder(columnEndPaddings.Length);

		//stbFinalTableFormat.Append(stbColumnFormat.ToString()).AppendLine();

		//foreach(string rowstring in lstRows)
		//{
		//	//rowstring =rowstring+GetPaddedString
		//       stbFinalTableFormat.Append(rowstring).AppendLine();
		//}

		//return string.Format(stbFinalTableFormat.ToString(), paddedstrings);
	}


}

public class AddressInfo
{
	private UInt64 _address;
	private string _hexaddress;
	public AddressInfo(string address)
	{
		address = FormatAddress(address);
		_hexaddress = address;
		if (!UInt64.TryParse(address, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out _address))
			throw new Exception("invalid address: " + address);
	}

	public AddressInfo(string address, string offset)
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

	public AddressInfo(UInt64 address)
	{
		_address = address;
		_hexaddress = address.ToString("X");
	}
	public AddressInfo(UInt64 address, UInt64 offset)
	{
		_address = address + offset;
		_hexaddress = address.ToString("X");
	}
	private string FormatAddress(string address)
	{
		if (address[0] == '0' && address[1] == 'x')
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
			if (Int32.TryParse(_hexaddress, out chkVal))
			{
				if (chkVal == 0)
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
		if (!SUCCEEDED(Debugger.Current.ReadVirtual32(_address, out output)))
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
		ulong offset = Debugger.Current.IsPointer64Bit() ? 12UL : 8UL;
		if (SUCCEEDED(GetString(_address + offset, 2000, out strOut)))
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

public class ManagedObject
{
	private AddressInfo _address;
	private Dictionary<string, ManagedObject> _Fields;
	//private static Regex expression = new Regex(".*", RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.CultureInvariant);
	private string _dumpObjOutput;
	private string[] _properties;
	private static Extension _sos;
	private bool _isPointer;
	private string _offset;
	public bool _hasValue;
	public ManagedObject(bool isValueType, string typeName, object value)
	{
		this.IsValueType = isValueType;
		Name = typeName;
		_value = value;
		// Debugger.Current.OutputDebugInfo("created CLRObject {2} of valuetype with name:{0} and value:{1}\n", typeName, value,Name); 
	}
	public AddressInfo Address
	{
		get
		{
			InitIfPointer();
			return _address;
		}
	}

	public bool HasValue
	{
		get
		{
			if (IsValueType)
			{
				return true;
			}

			InitIfPointer();
			return _address.HasValue;

		}
	}
	/// <summary>
	/// This constuctor is used a s lazy initialization technique.
	/// we will not be reading the original address unitl any request comes for any field access.
	///
	/// </summary>
	/// <param name="isPointer"></param>
	/// <param name="offset"></param>
	public ManagedObject(bool isPointer, string offset)
	{
		_isPointer = isPointer;
		_offset = offset;
		// Debugger.Current.OutputDebugInfo("created CLRObject's {1} with offset:{0}\n",_offset,Name ); 

	}
	private void InitIfPointer()
	{
		if (_isPointer)
		{
			Debugger.Current.OutputDebugInfo("Reading pointer of CLRObject's {2} with name: {1} and offset:{0}\n", _offset, Type, Name);
			_address = new AddressInfo(Debugger.Current.ReadPointer(_offset));
			_isPointer = false;
		}

	}
	public ManagedObject(AddressInfo address)
	{
		_address = address;
		Debugger.Current.OutputDebugInfo("created CLRObject from address object ,address:{0}\n", address.ToHex());
	}
	public ManagedObject(string address)
	{
		_address = new AddressInfo(address);
		Debugger.Current.OutputDebugInfo("created CLRObject with address:{0}\n", address);
	}
	public ManagedObject(UInt64 address)
	{
		_address = new AddressInfo(address);
	}
	public bool IsValueType
		{
			get;
			private set;
		}
	public object Value
	{
		get
		{
			try
			{


				if (IsValueType)
				{
					Debugger.Current.OutputDebugInfo("Reading value of CLRObject of ValueType with Name:{2} and Type:{0} and Value {1}\n", Type, _value, Name);
					switch (Type)
					{
						case "System.Boolean":
							return (bool)_value;
						case "System.Int32":
							if (_value == null)
							{
								_value = _address.GetInt32Value();
							}

							return _value;
						default:
							return _value;

					}


				}

				InitIfPointer();
				Debugger.Current.OutputDebugInfo("Reading field {2} value of CLRObject with and Type:{0} and Value {1}\n", Type, _value, Name);
				switch (Type)
				{

					case "System.String":
						_value = _address.GetManagedString();
						Debugger.Current.OutputDebugInfo("Reading string value {0}\n", _value);
						break;
					default:
						Debugger.Current.OutputDebugInfo("Reading string value {0}\n", _value);
						_value = this;
						break;

				}

				return _value;
			}

			catch (Exception ex)
			{
				Debugger.Current.OutputDebugInfo("Could not read field '{0}' of CLRObject with type:{1} and MT {2}", Name, Type, MT);
				Debugger.Current.OutputDebugInfo(ex.Message);
				EmitParentInfo();
				throw ex;
			}
		}
	}
	private void Init()
	{
		if (!_initilized)
		{
			if (_sos == null)
				_sos = new Extension("sos.dll");

			InitIfPointer();
			_dumpObjOutput = _sos.Call("!do", _address.ToHex());
			_properties = _dumpObjOutput.Split('\n', '\r');
			_initilized = true;
			Name = string.Empty;
		}

	}
	public string Name { get; private set; }
	public string MT { get; private set; }
	public string EEclass { get; private set; }
	public string Field { get; private set; }
	public string offset { get; private set; }
	public string Type { get; private set; }
	private bool _initilized = false;
	public ManagedObject Parent { get; private set; }
	public ManagedObject[] Children
	{
		get
		{
			if (_Fields != null)
			{
				return _Fields.Values.ToArray<ManagedObject>();
			}
			else
				return null;

		}
	}

	public bool HasField(string field)
	{
		Init();
		InitFields();
		if (_Fields.ContainsKey(field.ToUpperInvariant()))
		{
			return true;
		}
		return false;

	}

	public ManagedObject this[string fieldName]
	{
		get
		{
			try
			{

				if (IsValueType)
				{
					throw new Exception(string.Format("value type {0} does not support field access", Name));
				}
				Debugger.Current.OutputDebugInfo("Beginning to read field of CLRObject's {0} fieldname {1}\n", Name, fieldName);
				fieldName = fieldName.ToUpperInvariant();
				if (_Fields != null && _Fields.ContainsKey(fieldName))
				{
					return _Fields[fieldName];
				}
				else
				{

					Init();
					InitFields();
					return _Fields[fieldName];
				}


			}
			catch (Exception ex)
			{

				Debugger.Current.OutputDebugInfo("Error:Could not find a field '{2}' for CLRObject with Name {0} and Type{1}\n", Name, Type, fieldName);
				Debugger.Current.OutputDebugInfo(ex.Message);
				throw ex;

			}

		}
	}

	private void InitFields()
	{


		if (_Fields != null)
		{
			return;
		}



		_Fields = new Dictionary<string, ManagedObject>(_properties.Length);

		//chose to do string split instead of regular expressions.
		//this may be faster than regular expressions
		//regular expression has lot of edge cases when i comes to matching geneic's notation
		//containing special charcters


		//name can be populated from the feild property.so this is not necessary
		if (string.IsNullOrEmpty(Name) && _properties[0].Contains("Name:"))
			Name = _properties[0].Substring(13);
		//getting the method table from the substring.
		if (string.IsNullOrEmpty(this.MT) && _properties[1].Contains("MethodTable:"))
			MT = _properties[1].Substring(13);
		if (_properties[2].Contains("EEClass:"))
			EEclass = _properties[2].Substring(13);

		Debugger.Current.OutputDebugInfo("Initializing fields of CLR Object Name:{0} and Type {1}\n", Name, Type);
		EmitParentInfo();

		Debugger.Current.OutputDebugInfo("MT \t\t Type \t\t valueType \t\t Name\n");
		for (int i = 7; i < _properties.Length; i++)
		{
			var strCurrentLine = _properties[i];
			//only if the filed contains instance,try to parse it.
			//we are skipping shared and static variable instances.
			if (strCurrentLine.Contains("instance"))
			{
				string[] arrFields = strCurrentLine.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
				//saw that some times the type field is empty string
				//we are skipping that field as well b taking more thn or equal to 8 strings.
				//0-MT
				//1-Field
				//2-offset
				//3-Type
				//4-VT
				//5-Attr
				//6-Value
				//7 =>Name
				if (arrFields.Length >= 8)
				{

					ManagedObject ObjCLR;
					//indexing from back to avoid problems when the type contains spaces.
					//if type contains spaces,it will cause length to be changed
					if (arrFields[arrFields.Length - 4].Trim() == "1")
					{

						//instantiaing value type.
						//no need to check for address and values.
						ObjCLR = new ManagedObject(true, arrFields[3].Trim(), arrFields[arrFields.Length - 2].Trim());
						ObjCLR.Name = arrFields[arrFields.Length - 1];
						ObjCLR.Parent = this;
						ObjCLR.MT = arrFields[0].Trim();
						ObjCLR.Type = arrFields[3].Trim();

					}
					else
					{
						string fieldAddress = new AddressInfo(_address.ToHex(), arrFields[2]).ToHex();
						//var pointer = Debugger.Current.ReadPointer(fieldAddress);
						ObjCLR = new ManagedObject(true, fieldAddress);
						ObjCLR.Name = arrFields[arrFields.Length - 1];
						ObjCLR.Parent = this;
						//ObjCLR.MT = arrFields[0];
						ObjCLR.MT = arrFields[0].Trim();
						ObjCLR.Type = arrFields[3].Trim();


					}
					Debugger.Current.OutputDebugInfo("{0} \t\t {1} \t\t {2} \t\t {3} \n", ObjCLR.MT, ObjCLR.Type, ObjCLR.IsValueType, ObjCLR.Name);
					_Fields.Add(arrFields[arrFields.Length - 1].Trim().ToUpperInvariant(), ObjCLR);
				}
			}

		}
	}

	private void EmitParentInfo()
	{

		if (this.Parent != null)
		{
			Debugger.Current.OutputDebugInfo("Parent CLRObject's  Name:{0} Type:{1} MT:{2}\n", this.Parent.Name, this.Parent.Type, this.Parent.MT);
			if (this.Parent.Parent != null)
			{
				Debugger.Current.OutputDebugInfo("GrandParent CLRObject's  Name:{0} Type:{1} MT:{2}\n", this.Parent.Parent.Name, this.Parent.Parent.Type, this.Parent.Parent.MT);
			}
		}
	}

	public object _value { get; set; }
}