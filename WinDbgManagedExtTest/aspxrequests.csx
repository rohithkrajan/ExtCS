using System.Text.RegularExpressions;

var sos=new Extension("sos.dll");
string sMethodTable=sos.Call("!Name2EE System.Web.dll!System.Web.HttpContext");
var rgMt=new System.Text.RegularExpressions.Regex(@"MethodTable:\W(?<methodtable>\S*)",System.Text.RegularExpressions.RegexOptions.Multiline);
var matches=rgMt.Match(sMethodTable);
Dictionary<string,Dictionary<string,object>> outputData=new Dictionary<string,Dictionary<string,object>>();
if(matches.Success)
	{
	Output("matched");
    var mt=matches.Groups["methodtable"].Value;
    var currentContexts=sos.Call("!dumpheap -short -mt "+mt);
    string[] linefeed=new string[]{"\n","\r\n","\r"};
    String[] httpContexts=currentContexts.Split(linefeed, StringSplitOptions.RemoveEmptyEntries);
    //Output(httpContexts.Length.ToString());
    //Output(currentContexts);
    //!do poi(poi(06702ebc+0x20)+0x60)
	foreach(string context in httpContexts)
	{
	 var contextObject=new CLRObject(context);
	 var request=contextObject["_request"];
	 var response=contextObject["_response"];

	 var items=new Dictionary<string,object>();
	 items.Add("URL",request["_rawUrl"].Value);
	 items.Add("Status",response["_statusCode"].Value);
	 items.Add("ContentType",response["_contentType"].Value);

	 outputData.Add(context,items);



	 //Output(request.Value);
	//var po=ReadPointer(ReadPointer(context,"20"),"60");
	//Output(context+"=>");
	//Output(POI(context,"20"));
	//Output(po);
	//Output(GetString(po+0x8));
	//Output("\n");
	}
	Output("Context \t\t Status \t\t ContentType \t\t URL\n");
	foreach(string context in outputData.Keys)
	{
		Output(context);
		Output(" \t\t ");
		Output(outputData[context]["Status"]);
		Output(" \t\t ");
		Output(outputData[context]["ContentType"]);
		Output(" \t\t ");
		Output(outputData[context]["URL"]);
		Output("\n");
	}

}



