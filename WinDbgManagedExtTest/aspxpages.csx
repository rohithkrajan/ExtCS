#r "C:\Program Files\Debugging Tools for Windows (x64)\ExtCS.Debugger.dll"
#r "System.Data"
#r "System.Xml"

using System;
using System.Collections.Generic;
using System.Text;
using System.Data;
using ExtCS.Debugger;

var d = Debugger.Current;
Utilities.LoadSOSorPSSCOR();
	Dictionary<string, Dictionary<string, object>> outputData = new Dictionary<string, Dictionary<string, object>>();
var mt = Utilities.GetHttpContextMT();
	var currentContexts = d.Execute("!dumpheap -short -mt " + mt);

DataTable table = new DataTable();
table.Columns.Add("Context");
table.Columns.Add("Url");
table.Columns.Add("Errors");

foreach (string context in currentContexts.GetLines())
	{
		var contextObject = new CLRObject(context);

	var rContext = "<b>" + context + "</b>";

	var rurl = contextObject["_request"]["_rawUrl"].Value;
		
		var errors = contextObject["_errors"];
	object exceptionMessage;
	if (errors.HasValue)
		{

			var items = contextObject["_errors"]["_items"];
			var length = contextObject["_errors"]["_size"].Value;
			string arrayOutput = d.Execute("!da " + items.Address.ToHex());

			//array output shows each item in the array.
			//it starts with 6th line
			//Context.Debug=true;
			string[] lines = arrayOutput.Split(new char[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
			//d.OutputDebugInfo(length.ToString());

			
			for (int i = 6; i < 7; i++)
			{
				string[] httpException = lines[i].Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

				exceptionMessage = new CLRObject(httpException[1])["_message"].Value;

			}
			//(exceptionMessage);
		}
	table.Rows.Add(rContext, rurl, exceptionMessage);

	}

d.Output(table.GetFormattedString());


