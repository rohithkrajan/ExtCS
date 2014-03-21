//#r "C:\Program Files (x86)\Windows Kits\8.0\Debuggers\x86\ExtCS.Debugger.dll"


using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using ExtCS.Debugger;
using System.Linq;

public class RunawayThread 
{
	public static Regex Pattern = new Regex(@"(\d)\sdays\s(\d{1,2}):(\d{1,2}):(\d{1,2})\.(\d{1,3})", RegexOptions.Compiled);
    public string Number { get;set; }
	public string Hex { get; set; }
	public TimeSpan Time { get; set; }

}

Func<string,string, RunawayThread> ExtractThreadInfo = delegate (string sThread,string sTime)
{
	var match=RunawayThread.Pattern.Match(sTime);

	RunawayThread thread=null;
	TimeSpan time;
	int days,hours, minutes, seconds, milliseconds;
	if (match.Success)
	{
		int.TryParse(match.Groups[0].Value, out days);
		int.TryParse(match.Groups[1].Value,out hours);
		int.TryParse(match.Groups[2].Value, out minutes);
		int.TryParse(match.Groups[3].Value, out seconds);
		int.TryParse(match.Groups[4].Value, out milliseconds);
		time = new TimeSpan();
		thread = new RunawayThread();
		string[] arTInfo = sThread.Split(':');
		thread.Number = arTInfo[0];
		thread.Hex = arTInfo[1];
		thread.Time = time;
	}
	OutputDebugInfo("From Func call:"+sThread+"\n"+sTime+"\n");
	return thread;

};


var d = Debugger.Current;

string runway = d.Execute("!runaway");

// Get a collection of matches.
MatchCollection matches = Regex.Matches(runway, @"(?<thread>\d{1,3}:\w+)\s+(?<time>\d\sdays.*)", RegexOptions.Multiline);

var threads = new List<RunawayThread>();
RunawayThread thread;
// Use foreach loop.
OutputDebugInfo(matches.Count.ToString());
foreach (Match match in matches)
{
	var newthread = ExtractThreadInfo(match.Groups["thread"].Value, match.Groups["time"].Value);
	OutputDebugInfo("stack threads time"+match.Groups["time"].Value+"\n");
	threads.Add(newthread);	
}
OutputDebugInfo("stack threads length"+threads.Count.ToString());
//toggling source code
var s=d.Execute(".lines");
if(!s.Contains("not"))
	s = d.Execute(".lines");
StringBuilder stb = new StringBuilder(10);
//print the stack of 4 time consuming threads
int i = 0;
string Format = "Thread <exec cmd=\"~{0}s\">{0}</exec>stack: Time Spent in UserMode:{1}\n";
stb.Append("Stack of high CPU threads\n");
var sortedList=threads.OrderBy(o=>o.Time).ToList();

foreach (RunawayThread thread in sortedList)
{
	i++;
	d.Execute("~" + thread.Number + "s");
	stb.AppendFormat(Format, thread.Number,thread.Time.ToString());
	stb.Append(d.Execute("kpn"));
	stb.Append("\n");	
	if(i==5)
	break;
}
d.Output(stb.ToString());