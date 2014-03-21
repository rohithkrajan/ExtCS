using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ExtCS.Debugger
{

    public class CLRObject
    {
        private Address _address;
        private Dictionary<string, CLRObject> _Fields;
        //private static Regex expression = new Regex(".*", RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.CultureInvariant);
        private string _dumpObjOutput;
        private string[] _properties;
        private static Extension _sos;
        private bool _isPointer;
        private string _offset;
        public bool _hasValue;
        public CLRObject(bool isValueType, string typeName, object value)
        {
            this.IsValueType = isValueType;
            Name = typeName;
            _value = value;
           // Debugger.Current.OutputDebugInfo("created CLRObject {2} of valuetype with name:{0} and value:{1}\n", typeName, value,Name); 
        }
        public Address Address { 
            get {
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
        public CLRObject(bool isPointer, string offset)
        {
            _isPointer = isPointer;
            _offset = offset;
           // Debugger.Current.OutputDebugInfo("created CLRObject's {1} with offset:{0}\n",_offset,Name ); 

        }
        private void InitIfPointer()
        {
            if (_isPointer)
            {
                Debugger.Current.OutputDebugInfo("Reading pointer of CLRObject's {2} with name: {1} and offset:{0}\n", _offset,Type,Name); 
                _address = new Address(Debugger.Current.ReadPointer(_offset));
                _isPointer = false;
            }

        }
        public CLRObject(Address address)
        {
            _address = address;
            Debugger.Current.OutputDebugInfo("created CLRObject from address object ,address:{0}\n", address.ToHex()); 
        }
        public CLRObject(string address)
        {
            _address = new Address(address);
            Debugger.Current.OutputDebugInfo("created CLRObject with address:{0}\n", address); 
        }
        public CLRObject(UInt64 address)
        {
            _address = new Address(address);
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
                            Debugger.Current.OutputDebugInfo("Reading string value {0}\n",  _value);
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
                    Debugger.Current.OutputDebugInfo("Could not read field '{0}' of CLRObject with type:{1} and MT {2}",Name,Type,MT);
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
        public string EEclass { get;private set; }
        public string Field { get; private set; }
        public string offset { get; private set; }
        public string Type { get; private set; }
        private bool _initilized=false;
        public CLRObject Parent { get; private set; }
        public CLRObject[] Children
        {
            get
            {
                if (_Fields != null)
                {
                   return _Fields.Values.ToArray<CLRObject>();
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

        public CLRObject this[string fieldName]
        {
            get
            {
                try
                {

                    if (IsValueType)
                    {
                        throw new Exception(string.Format("value type {0} does not support field access", Name));
                    }
                    Debugger.Current.OutputDebugInfo("Beginning to read field of CLRObject's {0} fieldname {1}\n",Name,fieldName);
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

                    Debugger.Current.OutputDebugInfo("Error:Could not find a field '{2}' for CLRObject with Name {0} and Type{1}\n", Name,Type,fieldName);
                    Debugger.Current.OutputDebugInfo(ex.Message);
                    throw ex;
                    
                }

            }
        }

        private void InitFields()
        {
            

            if (_Fields!=null)
            {
                return;
            }

            

            _Fields = new Dictionary<string, CLRObject>(_properties.Length);

            //chose to do string split instead of regular expressions.
            //this may be faster than regular expressions
            //regular expression has lot of edge cases when i comes to matching geneic's notation
            //containing special charcters
            

            //name can be populated from the feild property.so this is not necessary
            if (string.IsNullOrEmpty(Name)&&_properties[0].Contains("Name:"))
                Name = _properties[0].Substring(13);
            //getting the method table from the substring.
            if (string.IsNullOrEmpty(this.MT)&&_properties[1].Contains("MethodTable:"))
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
                       
                        CLRObject ObjCLR;
                        //indexing from back to avoid problems when the type contains spaces.
                        //if type contains spaces,it will cause length to be changed
                        if (arrFields[arrFields.Length - 4].Trim() == "1")
                        {
                            
                            //instantiaing value type.
                            //no need to check for address and values.
                            ObjCLR = new CLRObject(true, arrFields[3].Trim(), arrFields[arrFields.Length - 2].Trim());
                            ObjCLR.Name = arrFields[arrFields.Length-1];
                            ObjCLR.Parent = this;
                            ObjCLR.MT = arrFields[0].Trim();
                            ObjCLR.Type = arrFields[3].Trim();
                           
                        }
                        else
                        {
                            string fieldAddress = new Address(_address.ToHex(), arrFields[2]).ToHex();
                            //var pointer = Debugger.Current.ReadPointer(fieldAddress);
                            ObjCLR = new CLRObject(true, fieldAddress);
                            ObjCLR.Name = arrFields[arrFields.Length - 1];
                            ObjCLR.Parent = this;
                            //ObjCLR.MT = arrFields[0];
                            ObjCLR.MT = arrFields[0].Trim();
                            ObjCLR.Type = arrFields[3].Trim();


                        }
                        Debugger.Current.OutputDebugInfo("{0} \t\t {1} \t\t {2} \t\t {3} \n", ObjCLR.MT, ObjCLR.Type, ObjCLR.IsValueType, ObjCLR.Name);
                        _Fields.Add(arrFields[arrFields.Length-1].Trim().ToUpperInvariant(), ObjCLR);
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
}
