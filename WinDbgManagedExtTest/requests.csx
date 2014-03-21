#r "C:\Program Files\Debugging Tools for Windows (x64)\ExtCS.Debugger.dll"
#r "System.Data"
#r "System.Xml"
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using ExtCS.Debugger;
using System.Data;
using System.Xml;
using System.Text;

var d = Debugger.Current;
var sos=new Extension("sos.dll");

Dictionary<string,Dictionary<string,object>> outputData=new Dictionary<string,Dictionary<string,object>>();

	//d.Output("matched");
	//get the http context methodtable
	var mt = GetMethodTable();
    var currentContexts=sos.Call("!dumpheap -short -mt "+mt);
    string[] linefeed=new string[]{"\n","\r\n","\r"};
    String[] httpContexts=currentContexts.Split(linefeed, StringSplitOptions.RemoveEmptyEntries);
	foreach(string context in httpContexts)
	{
	 var contextObject=new CLRObject(context);
	 var request=contextObject["_request"];
	d.Context.Debug = true;
	 var response=contextObject["_response"];

	 var items=new Dictionary<string,object>();

	 		
	 items.Add("URL",request["_rawUrl"].Value);
	 items.Add("Status",response["_statusCode"].Value);
	d.Context.Debug = false;
	 items.Add("ContentType",response["_contentType"].Value);
	 
	 if(contextObject["_thread"].HasValue)
		{
			//var thread=new CLRObject(contextObject["_response"]["_thread"][""])
			items.Add("Thread",contextObject["_thread"]["DONT_USE_InternalThread"].Value);
			//d.Output(contextObject["_thread"]["DONT_USE_InternalThread"].Value);
		}
		else
			items.Add("Thread","");

	 outputData.Add(context,items);
	}

	var clrThreads=sos.Call("!threads");

	Regex reg=new Regex(@"\s+\d{1,3}\s+\d{1,3}\s+(?<OSID>\w+)\s+(?<ThreadObj>\w+).*", RegexOptions.Compiled|RegexOptions.Multiline);
	MatchCollection tMatches = reg.Matches(clrThreads);
	Dictionary<UInt64,string> tDict=new Dictionary<UInt64,string>();
	
	foreach (Match match in tMatches)
	{		
		//d.Output(match.Groups["ThreadObj"].Value.ToUInt64());
		//d.Output("\n");
		tDict.Add(match.Groups["ThreadObj"].Value.ToUInt64(),match.Groups["OSID"].Value);
	}
	//d.Output(tDict.Count);

	Dictionary<UInt64,string> aspxThreads = new Dictionary<ulong, string>();

	d.Output("\nContext \t\t\t Thread \t\t Status \t\t ContentType \t\t URL\n");
	
	foreach(string context in outputData.Keys)
	{
		d.Output(context);
		d.Output(" \t\t ");
		//d.Output(outputData[context]["_thread"]);
		/*if(outputData[context]["_thread"].HasValue)
		{
			d.Output(outputData[context]["_thread"].Value);
		}
		*/
		string strThread=outputData[context]["Thread"].ToString();
		//d.Output(strThread.ToUInt64());
		//d.Output(.ToUInt64());
		if (tDict.ContainsKey(strThread.ToUInt64()))
		{
			d.Output(string.Format("<exec cmd=\"~~[{0}]s;!clrstack\">{1}</exec>", tDict[strThread.ToUInt64()], outputData[context]["Thread"]));
			aspxThreads.Add(strThread.ToUInt64(), outputData[context]["URL"].ToString());
        }
		else
			d.Output(" \t\t ");
		//d.Output(outputData[context]["Thread"]);
		d.Output(" \t\t ");
		d.Output(outputData[context]["Status"]);
		d.Output(" \t\t ");
		d.Output(outputData[context]["ContentType"]);
		d.Output(" \t\t ");
		d.Output(outputData[context]["URL"]);
		d.Output("\n");

	}

	d.Output("\n\n\nPossible aspx threads processing large collections :\n");
	d.Output("---------------------------------------------------\n\n");
	string dso;
	Match matThread;
	Regex regDSO = new Regex(@".*((\[\])|Collections|(\[(\.|\,|\w|\s)+\])).*",RegexOptions.Compiled|RegexOptions.Multiline);
	Regex regObject = new Regex(@"\w+\s+(?<Object>\w+)\s(?<Type>.*)",RegexOptions.Compiled);
	foreach (UInt64 thread in aspxThreads.Keys)
	{
		//d.Output(tDict[thread]);
        d.Execute("~~["+ tDict[thread] + "]s");
		dso = sos.Call("!dumpstackobjects");
		//d.Output(dso);
		matThread = regDSO.Match(dso);
		if (matThread.Success)
		{
			//d.Output(matThread.Value);
			var matObject = regObject.Match(matThread.Value);
			if (matObject.Success)
			{
				var objectAddress = matObject.Groups["Object"].Value;
				//d.Output(objectAddress);
				Address objAddress = new Address(objectAddress);
				var clrObject = new CLRObject(objAddress);
				var typeLarge = matObject.Groups["Type"].Value;
				int SizeCount = 0;
				
				if (clrObject.HasField("_size"))
				{
					Int32.TryParse(clrObject["_size"].Value.ToString(), out SizeCount);
					if (SizeCount > 500)
					{
						d.Output(string.Format("Request <b>'{0}'</b> runnig on <exec cmd=\"~~[{1}]s;!clrstack\">thread {1}</exec> has \n ",aspxThreads[thread],tDict[thread]));
						d.Output(string.Format("Object <exec cmd=\"!dumpobj {0}\">{1}</exec> of type '{2}'  with {3} Items  in it", objectAddress, objectAddress, typeLarge, SizeCount));
						d.Output(string.Format("\t<exec cmd=\"!gcroot {0}\">GCRoot</exec>\n", objectAddress));
					}
				}
			}
			d.Output("\n");
		}

	}



Func<string> GetMethodTable = delegate ()
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

};

Func<DataTable, string> GetTableData = delegate (DataTable table)
{
	StringBuilder stbTable = new StringBuilder();
	string[] columnFrontPaddings = new string[table.Columns.Count];
	string[] columnBackPaddings = new string[table.Columns.Count];
	int coumnCount;
	return string.Empty;
};