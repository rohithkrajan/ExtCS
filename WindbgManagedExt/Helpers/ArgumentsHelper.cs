using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ExtCS
{
    public class ArgumentsHelper
    {
        //old regex @"(?<argname>\-\w+)\s(?<argvalue>\S+)?",
        private string _args;
        Dictionary<string, string> _lstArgs;
        Regex regex = new Regex(
            @"(\s|^)(?<argname>\-\w+\s?)(?<argvalue>\s[^-]\S+)?",
            RegexOptions.IgnoreCase
            | RegexOptions.Multiline
            | RegexOptions.Singleline
            | RegexOptions.RightToLeft
            | RegexOptions.IgnorePatternWhitespace
            | RegexOptions.Compiled
            );
        public ArgumentsHelper(string args)
        {
            _args = args;
            IntArgs();
        }

        private void IntArgs()
        {
            if (string.IsNullOrEmpty(_args))
            {
                return;
            } 
            _lstArgs=new Dictionary<string,string>();
            foreach (Match item in regex.Matches(_args))
            {
                _lstArgs.Add(item.Groups["argname"].Value.Trim().ToUpperInvariant(), item.Groups["argvalue"].Value);
            }
        }
        public bool HasArgument(string argname)
        {
            return _lstArgs.ContainsKey(argname.ToUpperInvariant());
        }
        public bool IsNullOrEmpty(string argname)
        {
            if (HasArgument(argname))
            {
                return string.IsNullOrEmpty(_lstArgs[argname]);
            }
            else
                return false;
        }
        public string this[string argname]
        {
            get
            {
                if (_lstArgs == null)
                {
                    return string.Empty;
                }
                else
                {
                    return _lstArgs[argname.ToUpperInvariant()];
                }

            }
        }
        public IDictionary<string, string> ArgsCollection
        {
            get { return _lstArgs; }
        }
    }
}
