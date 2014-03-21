var sos=new Extension("sos.dll");
string sMethodTable=sos.Call("!Name2EE System.Web.dll!System.Web.HttpContext");
var rgMt=new System.Text.RegularExpressions.Regex(@"MethodTable:\W(?<methodtable>\S*)",System.Text.RegularExpressions.RegexOptions.Multiline);
var matches=rgMt.Match(sMethodTable);
Dictionary<string,Dictionary<string,object>> outputData=new Dictionary<string,Dictionary<string,object>>();
if(matches.Success)
	{
		var mt=matches.Groups["methodtable"].Value;
    var currentContexts=sos.Call("!dumpheap -short -mt "+mt);
    string[] linefeed=new string[]{"\n","\r\n","\r"};
    String[] httpContexts=currentContexts.Split(linefeed, StringSplitOptions.RemoveEmptyEntries);
	foreach(string context in httpContexts)
	{
	
	 	var contextObject=new CLRObject(context);
	 	
	 	 Output("<b>"+context+"</b>");
	 	 Output("\t\t");
	 	 Output(contextObject["_request"]["_rawUrl"].Value);
		 Output("\t\t");
		 var errors=contextObject["_errors"];	
		 if(errors.HasValue)
		 {
	 	
	 		var items=contextObject["_errors"]["_items"];	 	
	 		var length=contextObject["_errors"]["_size"].Value;	 	
	 	 	string arrayOutput=sos.Call("!da "+items.Address.ToHex());
		
	 	 	//array output shows each item in the array.
	 	 	//it starts with 6th line
	 	 	//Context.Debug=true;
	 	 	string[] lines=arrayOutput.Split(new char[]{'\n','\r'},StringSplitOptions.RemoveEmptyEntries);
	 	 	OutputDebugInfo(length.ToString());
	 	
		 	object exceptionMessage;
	 	 for(int i=6;i<7;i++)
	 	 {
	 	 	string[] httpException=lines[i].Split(new char[]{' '},StringSplitOptions.RemoveEmptyEntries);
	 	
	 	 	exceptionMessage=new CLRObject(httpException[1])["_message"].Value;

	 	 }
		Output(exceptionMessage);
		}
		 Output("\n");

	 }

	}