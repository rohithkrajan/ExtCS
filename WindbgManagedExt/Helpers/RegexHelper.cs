using ExtCS.Debugger;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExtCS.Helpers
{
    public class RegexHelper
    {
        
        private StringBuilder _pattern=new StringBuilder();

        public RegexHelper String(string pattern)
        {
            _pattern.Append(pattern);
            return this;
        }
        public RegexHelper Spaces(int count)
        {
            _pattern.Append(Utilities.GetPaddedString(' ',count));
            return this;
        }
        public RegexHelper Numbers(int count)
        {
            return this;
        }
        public RegexHelper AnyChar(int count)
        {
            return this;
        }
    }
}
