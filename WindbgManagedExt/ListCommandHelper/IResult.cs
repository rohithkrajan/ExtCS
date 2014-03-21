using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ExtCS.Debugger.ListCommandHelper
{
    public interface IResult
    {
        bool IsSuccess{get;set;}
        Exception LastError { get; set; }
        Object Value { get; set; }
    }
}
