## EXTCS - c# extension to access windows debugengine
=====

### Purpose

This extension allows you to write C# scripts against windbg(or other debugging tools like cdb.exe ntsd.exe etc). With the help of this extension, Now you can write windbg scripts in C# utilizing the vast array of .net libraries available
Why do I need this?

If you want to automate the debugger but dislike the WinDbg built-in script ,now you can use C# language and all the framework librarires. Even if you don't want to create your own script, maybe some existing scripts will be of interest to you?

### Supported Features

- upports all the windows debugging tools (cdb.exe, ntsd.exe , windbg.exe etc.)
- Visual Studio Editor support –Write your scripts in VS with intellisense goodness
- Complete debugging support. Just give break point in the C# script and attach VS to windbg to do debugging.
- Support for kernel mode debugging(still in early beta )
- REPL support –You can use your debugger to execute C# statements write on the command window
- support for 32bit and 64 bit .Scripts require very minimal change or no change at all to run on 32 or 64 bit dumps

### Quick start

* Install debugging tools http://msdn.microsoft.com/en-us/library/windows/hardware/gg463009.aspx
* Install Visual C++ 2012 resitributable http://www.microsoft.com/en-us/download/details.aspx?id=30679
* Download and extract the extension dlls to windbg directory ExtCS 0.5 Beta
* Load extension in WinDbg: .load extcs
*Execute the script: !execute -file c:\scripts\aspxpages.csx

Here’s another simple example of what this extension does:

!execute –file c:\scripts\heapstat.csx

```cs
//contents of heapstat.csx
#r "C:\Program Files\Debugging Tools for Windows (x64)\ExtCS.Debugger.dll"
#r "System.Data"
#r "System.Xml"
using System;
using ExtCS.Debugger;

var d = Debugger.Current;
//load the sos.dll
var sos=new Extension("sos.dll");
//call the command on sos.dll and get tehe output to variable heapstat
var heapstat=sos.Call("!dumpheap -stat");
//output heapstat to debugger
d.Output(heapstat);

'''
