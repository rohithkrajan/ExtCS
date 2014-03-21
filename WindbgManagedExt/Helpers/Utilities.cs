using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ExtCS.Debugger
{
    public class DllVersionInfo
    {
        public int Major;
        public int Minor;
        public int Version;
        public int BuildNumber;
    }

    public static class Utilities
    {

        public static void LoadSOSorPSSCOR()
        {
            var d=Debugger.Current;
            string clr = d.Execute("lmvm clr");

            Regex regex = new Regex(
                @"File\sversion\:\s+(?<major>\d)\.(?<minor>\d)\.(?<ver>\d+)\.("
                + @"?<build>\d+)",
                RegexOptions.IgnoreCase
                | RegexOptions.Multiline
                | RegexOptions.Singleline
                | RegexOptions.IgnorePatternWhitespace
                | RegexOptions.Compiled
                );
            Match match = regex.Match(clr);
            DllVersionInfo ver;
            if (!match.Success)
            {
                var mscorwks = d.Execute("lmvm mscorwks");
                match = regex.Match(mscorwks);
                if (!match.Success)
                    throw new Exception("unable to find mscorwks or clr dll");
            }
            ver = new DllVersionInfo()
            {
                Major = Convert.ToInt32(match.Groups["major"].Value),
                Minor = Convert.ToInt32(match.Groups["minor"].Value),
                Version = Convert.ToInt32(match.Groups["ver"].Value),
                BuildNumber = Convert.ToInt32(match.Groups["build"].Value)
            };

            if (ver.Major > 2 && ver.Version > 300)
            {
                d.Require("sos.dll");
            }
            else
            {
                switch (ver.Major)
                {
                    case 1:
                        d.Require("psscor.dll");
                        break;
                    case 2:
                        d.Require("psscor2.dll");
                        break;
                    case 4:
                        d.Require("psscor4.dll");
                        break;
                    default:
                        d.Require("sos.dll");
                        break;
                }
                
            }
            
        }
        /// <summary>
        /// get a string array split into lines
        /// </summary>
        /// <param name="strData"></param>
        /// <returns></returns>
        public static string[] GetLines(this string strData)
        {
            string[] linefeed = new string[] { "\n", "\r\n", "\r" };
            return strData.Split(linefeed, StringSplitOptions.RemoveEmptyEntries);
        }
        
        /// <summary>
        /// pass the type string GetMT("System.Web.dll!System.Web.HttpContext");
        /// </summary>
        /// <param name="type"></param>
        /// <returns>Method table</returns>
        public static string GetMT(string type)
        {

            var d = Debugger.Current;
            var sos = new Extension("sos.dll");
            string sMethodTable = sos.Call("!Name2EE "+type);
            var rgMt = new System.Text.RegularExpressions.Regex(@"MethodTable:\W(?<methodtable>\S*)", System.Text.RegularExpressions.RegexOptions.Multiline);
            var matches = rgMt.Match(sMethodTable);

            if (matches.Success)

                //d.Output("matched");
                return matches.Groups["methodtable"].Value;

            throw new Exception("unable to get Method table of HttpContext\n");

        }
        /// <summary>
        /// Get the methodtable HttpContext
        /// </summary>
        /// <returns></returns>
        public static string GetHttpContextMT()
        {
            return GetMT("System.Web.dll!System.Web.HttpContext");
        }

        public static string GetPaddedString(char ch, int count)
        {
            Char[] strReturn = new char[count];
            for (int i = 0; i < count; i++)
            {
                strReturn[i] = ch;
            }
            return new String(strReturn);
        }

        /// <summary>
        /// this method returns the pad requesred for each row value
        /// this functions should have intergaretd to calculating columns counts for perf improvement.
        /// </summary>
        public static string[] GetColumnPadStrings(DataRow currentRow, int[] columnPaddings)
        {
            string[] rowPaddings = new string[columnPaddings.Length];

            for (int cCount = 0; cCount < columnPaddings.Length; cCount++)
            {
                rowPaddings[cCount] = GetPaddedString(' ', (columnPaddings[cCount] - currentRow[cCount].ToString().Length) + 2);
            }
            return rowPaddings;
        }
        public static string[] GetHeaderPadStrings(DataTable table, int[] columnPaddings)
        {
            string[] rowPaddings = new string[columnPaddings.Length];

            for (int cCount = 0; cCount < columnPaddings.Length; cCount++)
            {
                rowPaddings[cCount] = GetPaddedString(' ', (columnPaddings[cCount] - table.Columns[cCount].ColumnName.Length) + 2);
            }
            return rowPaddings;
        }

        public static string GetTableData(DataTable table)
        {
            StringBuilder stbTable = new StringBuilder();
            //int[] columnFrontPaddings = new int[table.Columns.Count];
            //padding count to make the space between colunmns even
            //even if the lenght of each column value is different,padding makes it equal
            //find the largest length
            int[] columnEndPaddings = new int[table.Columns.Count];
            int columnCount = 0;

            StringBuilder stbColumnFormat = new StringBuilder();
            //each column's format value
            //this will be made to a format string.
            Dictionary<string, string> dictColumnFormats = new Dictionary<string, string>();
            //this will make a format string columnname1{0}columnname2{1}comunname3{2}
            //{0},{1} will hold the paadded string for each column
            foreach (DataColumn column in table.Columns)
            {

                stbColumnFormat.Append("<b>" + column.ColumnName + "</b>").Append("{" + columnCount.ToString() + "}");
                dictColumnFormats.Add(column.ColumnName, string.Empty);
                columnEndPaddings[columnCount] = column.ColumnName.Length;
                columnCount++;

            }
            int rowCount = 0;
            List<string> lstRows = new List<string>();
            columnCount = 0;
            //lstRows will hold the format string for each rows
            //rowsvalue1{0}rowvalue{1}rowvalue{2}
            StringBuilder stbRows = new StringBuilder(table.Columns.Count);
            foreach (DataRow row in table.Rows)
            {
                foreach (DataColumn rowColumn in table.Columns)
                {
                    string value = row[rowColumn].ToString();
                    stbRows.Append(value + "{" + columnCount + "}");
                    if (columnEndPaddings[columnCount] < value.Length)
                        columnEndPaddings[columnCount] = value.Length;
                    columnCount++;
                }
                lstRows.Add(stbRows.ToString());
                stbRows.Length = 0;
                columnCount = 0;
            }
            //columnendpadding will contain maximum length of charcters in each column
            //columnEndPaddings[0] will contain the maximum length of column
            string[] paddedstrings = new string[columnEndPaddings.Length];
            stbTable.AppendFormat(stbColumnFormat.ToString(), GetHeaderPadStrings(table, columnEndPaddings));
            stbTable.AppendLine();
            stbTable.Append(GetPaddedString('=', stbTable.Length));
            stbTable.AppendLine();
            for (int rCount = 0; rCount < lstRows.Count; rCount++)
            {
                stbTable.AppendFormat(lstRows[rCount], GetColumnPadStrings(table.Rows[rCount], columnEndPaddings));
                stbTable.AppendLine();
            }



            return stbTable.ToString();
            //for (int i = 0; i < columnEndPaddings.Length; i++)
            //{
            //	paddedstrings[i] = GetPaddedString(' ', columnEndPaddings[i]);
            //}
            //StringBuilder stbFinalTableFormat = new StringBuilder(columnEndPaddings.Length);

            //stbFinalTableFormat.Append(stbColumnFormat.ToString()).AppendLine();

            //foreach(string rowstring in lstRows)
            //{
            //	//rowstring =rowstring+GetPaddedString
            //       stbFinalTableFormat.Append(rowstring).AppendLine();
            //}

            //return string.Format(stbFinalTableFormat.ToString(), paddedstrings);
        }
        public static string GetFormattedString(this DataTable table)
        {
            return GetTableData(table);
        }



    }

}
