using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExtCS.Debugger
{
    public class ScriptContext
    {
        public string ScriptPath { get; internal set; }
        public string[] ArgsKeys { get; internal set; }
        public bool Debug { get; set; }
        public string ScriptLocation { get; internal set; }
        public ArgumentsHelper Args { get; internal set; }
        
    }
}
