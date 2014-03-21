#r "C:\Program Files\Debugging Tools for Windows (x64)\ExtCS.Debugger.dll"
#r "c:\ilspy\ICSharpCode.Decompiler.dll"
#r "c:\ilspy\Mono.Cecil.dll"


using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ExtCS.Debugger;
using ICSharpCode.Decompiler;
using Mono.Cecil;

public class StackFrames
{
	List<string> _stackFrames = new List<string>();
	public StackFrames()
	{
		var d = Debugger.Current;
		Utilities.LoadSOSorPSSCOR();
		var clrstack = d.Execute("!clrstack");
		
	}
}
