using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExtCS.Debugger.ListCommandHelper
{
    public class ListCommand:ICommand
    {
        public IResult Execute(params string[] args)
        {
            throw new NotImplementedException();
        }

        public string Args
        {
            get { throw new NotImplementedException(); }
        }

        public string ScriptName
        {
            get { throw new NotImplementedException(); }
        }
    }
}
