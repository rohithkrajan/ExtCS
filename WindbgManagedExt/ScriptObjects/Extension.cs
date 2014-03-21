using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Dynamic;

namespace ExtCS.Debugger
{
   public class Extension:DynamicObject
    {
       const int S_OK = 0;
       private OutputHandler outHandler = new OutputHandler();
       public Extension(string extensionName)
       {
           debugger = Debugger.Current;
           extensionHandle = debugger.GetExtensionHandle(extensionName);
       }
       public string Call(string commandname, params string[] args)
       {
           //return CallExtensionMethod(commandname, CombineArgs(args));
           return this.debugger.Execute(commandname + "" + CombineArgs(args));
       }

       private string CallExtensionMethod(string method, string args)
       {
           IntPtr previousHandler;
           this.debugger.DebugClient.FlushCallbacks();
           debugger.InstallCustomHandler(outHandler, out previousHandler);

           int hr = debugger.DebugControl.CallExtensionWide(extensionHandle, method, args);
           this.debugger.DebugClient.FlushCallbacks();
           if (hr != S_OK)
           {
               debugger.OutputError("unable to call extension method {0} with args {1}", method, args);
               return null;
           }
           debugger.RevertCallBacks(previousHandler);

           return outHandler.ToString();

       }
       public override bool TryGetMember(GetMemberBinder binder, out object result)
       {
           result= CallExtensionMethod(binder.Name,null);
           if (result==null)
           {
               return false;
           }
           return true;
       }

       public override bool TryInvokeMember(InvokeMemberBinder binder, object[] args, out object result)
       {
           string combinedArg = CombineArgs(args);
           
           result=CallExtensionMethod(binder.Name,combinedArg);
           if (result == null)
               return false;

            return true;
       }

       private static string CombineArgs(object[] args)
       {
           string[] arguments = null;

           string combinedArg = string.Empty;

           if (args.Length > 0)
               arguments = new string[args.Length];

           foreach (var item in args)
           {
               if (!string.IsNullOrEmpty(item.ToString()))
                   combinedArg += " " + item.ToString();
           }
           return combinedArg;
       }
       

       public ulong extensionHandle { get; set; }

       public unsafe Debugger debugger { get; set; }
    }
}
