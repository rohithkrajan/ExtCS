using Roslyn.Compilers.Common;
using Roslyn.Scripting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Roslyn.Compilers;
using Roslyn.Scripting.CSharp;
using Roslyn.Compilers;
using Roslyn.Compilers.Common;
using Roslyn.Compilers.CSharp;
using System.IO;
using System.Reflection;

namespace ExtCS.Debugger
{
    public class DebuggerScriptEngine
    {
        //Error	1	'ExtCS.Debugger.DebuggerScriptEngine' 
        //does not implement inherited abstract member 
        //'Roslyn.Scripting.CommonScriptEngine.CreateCompilation(Roslyn.Compilers.IText, string, bool, Roslyn.Scripting.Session, 
        //System.Type, Roslyn.Compilers.DiagnosticBag)'	C:\tfssourcecode\ExtCS\ExtCS\WindbgManagedExt\DebuggerScriptEngineSession.cs
        //10	18	WindbgManagedExt
        private const string CompiledScriptClass = "Submission#0";
        private const string CompiledScriptMethod = "<Factory>";
        private static AppDomain debuggerDomain;

        public static void Clear()
        {
            if (debuggerDomain != null)
            {
                if (!debuggerDomain.IsFinalizingForUnload())
                {
                    AppDomain.Unload(debuggerDomain);
                    debuggerDomain = null;
                }                

            }
            
        }

        public static Object Execute(Session  session, string path)
        {
            Submission<object> submission;
            object retrunValue=null;
            string code=null;
            try
            {
                code = System.IO.File.ReadAllText(path);
                submission = session.CompileSubmission<object>(code,path:path);
                
            }
            catch (Exception compileException)
            {
                throw compileException;
            }

            var exeBytes = new byte[0];
            var pdbBytes = new byte[0];
            var compileSuccess = false;

            using (var exeStream = new MemoryStream())
            using (var pdbStream = new MemoryStream())
            {
                var result = submission.Compilation.Emit(exeStream, pdbStream: pdbStream);
              
                compileSuccess = result.Success;

                //File.WriteAllBytes(@"c:\scripts\dynamic.dll", exeBytes.ToArray());

                if (result.Success)
                {
                    Debugger.Current.OutputDebugInfo("Compilation was successful.");
                    exeBytes = exeStream.ToArray();
                    pdbBytes = pdbStream.ToArray();
                }
                else
                {
                    var errors = String.Join(Environment.NewLine, result.Diagnostics.Select(x => x.ToString()));
                    Debugger.Current.OutputDebugInfo("Error occurred when compiling: {0})", errors);
                }
            }

            if (compileSuccess)
            {
                Debugger.Current.OutputDebugInfo("Loading assembly into appdomain.");
               // if(debuggerDomain==null)
                 //   debuggerDomain = AppDomain.CreateDomain("debuggerdomain");

                var assembly = AppDomain.CurrentDomain.Load(exeBytes, pdbBytes);
                Debugger.Current.OutputDebugInfo("Retrieving compiled script class (reflection).");
                var type = assembly.GetType(CompiledScriptClass);
                Debugger.Current.OutputDebugInfo("Retrieving compiled script method (reflection).");
                var method = type.GetMethod(CompiledScriptMethod, BindingFlags.Static | BindingFlags.Public);

                try
                {
                    Debugger.Current.OutputDebugInfo("Invoking method.");
                     retrunValue = method.Invoke(null, new[] { session });
                }
                catch (Exception executeException)
                {
                    
                    Debugger.Current.OutputDebugInfo("An error occurred when executing the scripts.");
                    var message =
                        string.Format(
                        "Exception Message: {0} {1}Stack Trace:{2}",
                        executeException.InnerException.Message,
                        Environment.NewLine,
                        executeException.InnerException.StackTrace);
                   // AppDomain.Unload(debuggerDomain);
                    debuggerDomain = null;
                    throw executeException;
                }
            }

            return retrunValue ;

        }

    }
}
