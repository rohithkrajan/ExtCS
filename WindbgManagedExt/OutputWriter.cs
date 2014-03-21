using System;
using System.IO;
using System.Text;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using DotNetDbg;

namespace WindbgManagedExt
{
	public unsafe partial class DebuggerWriter:Debugger
	{
        public const int S_OK = 0;
        private const int S_FALSE = 1;
        private static string LastStatusText = null;
        /// <summary>
        /// Returns true if a HRESULT indicates failure.
        /// </summary>
        /// <param name="hr">HRESULT</param>
        /// <returns>True if hr indicates failure</returns>
        public static bool FAILED(int hr)
        {
            return (hr < 0);
        }

        /// <summary>
        /// Returns true if a HRESULT indicates success.
        /// </summary>
        /// <param name="hr">HRESULT</param>
        /// <returns>True if hr indicates success</returns>
        public static bool SUCCEEDED(int hr)
        {
            return (hr >= 0);
        }


		private int OutputHelper(string formattedString, DEBUG_OUTPUT outputType)
		{
            
			formattedString = EscapePercents(formattedString);
            return DebugControl.ControlledOutputWide(OutCtl, outputType, formattedString);
		}

		private int OutputLineHelper(string formattedString, DEBUG_OUTPUT outputType)
		{
			formattedString = EscapePercents(formattedString);
            int hr = DebugControl.ControlledOutputWide(OutCtl, outputType, formattedString);
            return FAILED(hr) ? hr : DebugControl.ControlledOutputWide(OutCtl, outputType, "\n");
		}

		/// <summary>
		/// Outputs a string that contains one or more DML block
		/// </summary>
		/// <param name="formattedDmlString">String containing DML</param>
		/// <returns>HRESULT of the IDebugControl4::ControlledOutputWide or IDebugControl::ControlledOutput call</returns>
		public int OutputDMLPreformatted(string formattedDmlString)
		{
			formattedDmlString = EscapePercents(formattedDmlString);
            return DebugControl.ControlledOutputWide(OutCtl | DEBUG_OUTCTL.DML, DEBUG_OUTPUT.NORMAL, formattedDmlString);
		}

        /// <summary>
        /// Outputs a string that contains one or more DML block
        /// </summary>
        /// <param name="formattedDmlString">String containing DML</param>
        /// <returns>HRESULT of the IDebugControl4::ControlledOutputWide or IDebugControl::ControlledOutput call</returns>
        public int OutputErrorDMLPreformatted(string formattedDmlString)
        {
            formattedDmlString = EscapePercents(formattedDmlString);
            return DebugControl.ControlledOutputWide(OutCtl | DEBUG_OUTCTL.DML, DEBUG_OUTPUT.ERROR, formattedDmlString);
        }


        /// <summary>
        /// Outputs a string that contains one or more DML block
        /// </summary>
        /// <param name="formattedDmlString">String containing DML</param>
        /// <returns>HRESULT of the IDebugControl4::ControlledOutputWide or IDebugControl::ControlledOutput call</returns>
        public int OutputWarningDMLPreformatted(string formattedDmlString)
        {
            formattedDmlString = EscapePercents(formattedDmlString);
            return DebugControl.ControlledOutputWide(OutCtl | DEBUG_OUTCTL.DML, DEBUG_OUTPUT.WARNING, formattedDmlString);
        }

        /// <summary>
        /// Outputs a string that contains one or more DML block
        /// </summary>
        /// <param name="formattedDmlString">String containing DML</param>
        /// <returns>HRESULT of the IDebugControl4::ControlledOutputWide or IDebugControl::ControlledOutput call</returns>
        public int OutputVerboseDMLPreformatted(string formattedDmlString)
        {
            formattedDmlString = EscapePercents(formattedDmlString);
            return DebugControl.ControlledOutputWide(OutCtl | DEBUG_OUTCTL.DML, DEBUG_OUTPUT.VERBOSE, formattedDmlString);
        }

		/// <summary>
		/// Outputs a string that contains one or more DML block
		/// </summary>
		/// <param name="formattedDmlString">String containing DML</param>
		/// <returns>HRESULT of the IDebugControl4::ControlledOutputWide or IDebugControl::ControlledOutput call</returns>
		public int OutputDMLPreformattedLine(string formattedDmlString)
		{
			formattedDmlString = EscapePercents(formattedDmlString);
            int hr = DebugControl.ControlledOutput(OutCtl | DEBUG_OUTCTL.DML, DEBUG_OUTPUT.NORMAL, formattedDmlString);
            return FAILED(hr) ? hr : DebugControl.ControlledOutput(OutCtl, DEBUG_OUTPUT.NORMAL, "\n");
		}
        
        /// <summary>
        /// Outputs a string that contains one or more DML block
        /// </summary>
        /// <param name="formattedDmlString">String containing DML</param>
        /// <returns>HRESULT of the IDebugControl4::ControlledOutputWide or IDebugControl::ControlledOutput call</returns>
        public int OutputErrorDMLPreformattedLine(string formattedDmlString)
        {
            formattedDmlString = EscapePercents(formattedDmlString);
			int hr = DebugControl.ControlledOutputWide(OutCtl | DEBUG_OUTCTL.DML, DEBUG_OUTPUT.ERROR, formattedDmlString);
            return FAILED(hr) ? hr : DebugControl.ControlledOutputWide(OutCtl, DEBUG_OUTPUT.ERROR, "\n");
        }

        /// <summary>
        /// Outputs a string that contains one or more DML block
        /// </summary>
        /// <param name="formattedDmlString">String containing DML</param>
        /// <returns>HRESULT of the IDebugControl4::ControlledOutputWide or IDebugControl::ControlledOutput call</returns>
        public int OutputVerboseDMLPreformattedLine(string formattedDmlString)
        {
            formattedDmlString = EscapePercents(formattedDmlString);
			int hr = DebugControl.ControlledOutputWide(OutCtl | DEBUG_OUTCTL.DML, DEBUG_OUTPUT.VERBOSE, formattedDmlString);
            return FAILED(hr) ? hr : DebugControl.ControlledOutputWide(OutCtl, DEBUG_OUTPUT.VERBOSE, "\n");
        }

        /// <summary>
        /// Outputs a string that contains one or more DML block
        /// </summary>
        /// <param name="formattedDmlString">String containing DML</param>
        /// <returns>HRESULT of the IDebugControl4::ControlledOutputWide or IDebugControl::ControlledOutput call</returns>
        public int OutputWarningDMLPreformattedLine(string formattedDmlString)
        {
            formattedDmlString = EscapePercents(formattedDmlString);
			int hr = DebugControl.ControlledOutputWide(OutCtl | DEBUG_OUTCTL.DML, DEBUG_OUTPUT.WARNING, formattedDmlString);
            return FAILED(hr) ? hr : DebugControl.ControlledOutputWide(OutCtl, DEBUG_OUTPUT.WARNING, "\n");
        }

		/// <summary>
		/// Builds a formatted string and outputs the result as normal text
		/// </summary>
		/// <param name="format">Format string using C# string formatting syntax</param>
		/// <param name="parameters">Optional parameters for the string format</param>
		/// <returns>HRESULT of the IDebugControl4::OutputWide or IDebugControl::Output call</returns>
		public int Output(string format, params object[] parameters)
		{
			ScanForPointerArguments(format, parameters, out format);
			string formattedString = ((parameters != null) && (parameters.Length != 0)) ? String.Format(CultureInfo.InvariantCulture, format, parameters) : format;
			return OutputHelper(formattedString, DEBUG_OUTPUT.NORMAL);
		}

		/// <summary>
		/// Builds a formatted string and outputs the result as normal text
		/// </summary>
		/// <param name="format">Format string using C# string formatting syntax</param>
		/// <param name="parameters">Optional parameters for the string format</param>
		/// <returns>HRESULT of the IDebugControl4::OutputWide or IDebugControl::Output call</returns>
		public int OutputLine(string format, params object[] parameters)
		{
			ScanForPointerArguments(format, parameters, out format);
			string formattedString = ((parameters != null) && (parameters.Length != 0)) ? String.Format(CultureInfo.InvariantCulture, format, parameters) : format;
			return OutputLineHelper(formattedString, DEBUG_OUTPUT.NORMAL);
		}

		/// <summary>
		/// Output a CR/LF pair
		/// </summary>
		/// <returns>HRESULT of the IDebugControl4::OutputWide or IDebugControl::Output call</returns>
		public int OutputLine()
		{
			return OutputHelper("\n", DEBUG_OUTPUT.NORMAL);
		}

        public void SetStatusBar(string text)
        {
            if (text != LastStatusText)
            {
                LastStatusText = text;
                OutputHelper(text, DEBUG_OUTPUT.STATUS);
                //SetWindowTextAnsi(FindWindbgStatusBarWindow(), text);
            }
        }

		/// <summary>
		/// Builds a formatted string and outputs the result as verbose text
		/// </summary>
		/// <param name="format">Format string using C# string formatting syntax</param>
		/// <param name="parameters">Optional parameters for the string format</param>
		/// <returns>HRESULT of the IDebugControl4::OutputWide or IDebugControl::Output call</returns>
		public int OutputVerbose(string format, params object[] parameters)
		{
			ScanForPointerArguments(format, parameters, out format);
			string formattedString = ((parameters != null) && (parameters.Length != 0)) ? String.Format(CultureInfo.InvariantCulture, format, parameters) : format;

			SetStatusBar("Mex: " + formattedString);

			return OutputHelper(formattedString, DEBUG_OUTPUT.VERBOSE);
		}

		/// <summary>
		/// Builds a formatted string and outputs the result as verbose text
		/// </summary>
		/// <param name="format">Format string using C# string formatting syntax</param>
		/// <param name="parameters">Optional parameters for the string format</param>
		/// <returns>HRESULT of the IDebugControl4::OutputWide or IDebugControl::Output call</returns>
		public int OutputVerboseLine(string format, params object[] parameters)
		{
			ScanForPointerArguments(format, parameters, out format);
			string formattedString = ((parameters != null) && (parameters.Length != 0)) ? String.Format(CultureInfo.InvariantCulture, format, parameters) : format;

			SetStatusBar("Mex: " + formattedString);

			return OutputLineHelper(formattedString, DEBUG_OUTPUT.VERBOSE);
		}


        /// <summary>
        /// Builds a formatted string and outputs the result as verbose text, only called in debug builds
        /// </summary>
        /// <param name="format">Format string using C# string formatting syntax</param>
        /// <param name="parameters">Optional parameters for the string format</param>
        /// <returns>HRESULT of the IDebugControl4::OutputWide or IDebugControl::Output call</returns>

        [System.Diagnostics.Conditional("DEBUG")]
        public void OutputDebugLine(string format, params object[] parameters)
        {
#if DEBUG

            ScanForPointerArguments(format, parameters, out format);
            string formattedString = ((parameters != null) && (parameters.Length != 0)) ? String.Format(CultureInfo.InvariantCulture, format, parameters) : format;

            OutputLineHelper(formattedString, DEBUG_OUTPUT.VERBOSE);
#endif
        }

		/// <summary>
		/// Output a CR/LF pair
		/// </summary>
		/// <returns>HRESULT of the IDebugControl4::OutputWide or IDebugControl::Output call</returns>
		public int OutputVerboseLine()
		{
			return OutputHelper("\n", DEBUG_OUTPUT.VERBOSE);
		}

		/// <summary>
		/// Builds a formatted string and outputs the result as warning text
		/// </summary>
		/// <param name="format">Format string using C# string formatting syntax</param>
		/// <param name="parameters">Optional parameters for the string format</param>
		/// <returns>HRESULT of the IDebugControl4::OutputWide or IDebugControl::Output call</returns>
		public int OutputWarning(string format, params object[] parameters)
		{
			ScanForPointerArguments(format, parameters, out format);
			string formattedString = ((parameters != null) && (parameters.Length != 0)) ? String.Format(CultureInfo.InvariantCulture, format, parameters) : format;
			return OutputHelper(formattedString, DEBUG_OUTPUT.WARNING);
		}

		/// <summary>
		/// Builds a formatted string and outputs the result as warning text
		/// </summary>
		/// <returns>HRESULT of the IDebugControl4::OutputWide or IDebugControl::Output call</returns>
		public int OutputWarningLine()
		{
			return OutputLineHelper("\n", DEBUG_OUTPUT.WARNING);
		}

		/// <summary>
		/// Builds a formatted string and outputs the result as warning text
		/// </summary>
		/// <param name="format">Format string using C# string formatting syntax</param>
		/// <param name="parameters">Optional parameters for the string format</param>
		/// <returns>HRESULT of the IDebugControl4::OutputWide or IDebugControl::Output call</returns>
		public int OutputWarningLine(string format, params object[] parameters)
		{
			ScanForPointerArguments(format, parameters, out format);
			string formattedString = ((parameters != null) && (parameters.Length != 0)) ? String.Format(CultureInfo.InvariantCulture, format, parameters) : format;
			return OutputLineHelper(formattedString, DEBUG_OUTPUT.WARNING);
		}

		/// <summary>
		/// Builds a formatted string and outputs the result as error text
		/// </summary>
		/// <param name="format">Format string using C# string formatting syntax</param>
		/// <param name="parameters">Optional parameters for the string format</param>
		/// <returns>HRESULT of the IDebugControl4::OutputWide or IDebugControl::Output call</returns>
		public int OutputError(string format, params object[] parameters)
		{
			ScanForPointerArguments(format, parameters, out format);
			string formattedString = ((parameters != null) && (parameters.Length != 0)) ? String.Format(CultureInfo.InvariantCulture, format, parameters) : format;
			return OutputHelper(formattedString, DEBUG_OUTPUT.ERROR);
		}

		/// <summary>
		/// Builds a formatted string and outputs the result as error text
		/// </summary>
		/// <param name="format">Format string using C# string formatting syntax</param>
		/// <param name="parameters">Optional parameters for the string format</param>
		/// <returns>HRESULT of the IDebugControl4::OutputWide or IDebugControl::Output call</returns>
		public int OutputErrorLine(string format, params object[] parameters)
		{
			ScanForPointerArguments(format, parameters, out format);
			string formattedString = ((parameters != null) && (parameters.Length != 0)) ? String.Format(CultureInfo.InvariantCulture, format, parameters) : format;
			return OutputLineHelper(formattedString, DEBUG_OUTPUT.ERROR);
		}

		/// <summary>
		/// Outputs a formatted DML command block using the desired text and command
		/// </summary>
		/// <param name="text">Text which will appear as a clickable link</param>
		/// <param name="command">The command to run when the link is clicked</param>
		/// <returns>HRESULT of the IDebugControl4::ControlledOutputWide or IDebugControl::ControlledOutput call</returns>
		public int OutputDML(string text, string command)
		{
			return OutputDMLPreformatted(FormatDML(text, command));
		}

		/// <summary>
		/// Outputs a formatted DML command block using the desired text and command
		/// </summary>
		/// <param name="text">Text which will appear as a clickable link</param>
		/// <param name="command">The command to run when the link is clicked</param>
		/// <returns>HRESULT of the IDebugControl4::ControlledOutputWide or IDebugControl::ControlledOutput call</returns>
		public int OutputDMLLine(string text, string command)
		{
			return OutputDMLPreformattedLine(FormatDML(text, command));
		}

		/// <summary>
		/// Outputs a formatted DML command block using the desired text and command
		/// </summary>
		/// <param name="textFormatString">Format string which will appear as a clickable link</param>
		/// <param name="commandFormatString">Format string for the command to run when the link is clicked</param>
		/// <param name="parameters">Parameters to use when formatting the clickable text and the command string</param>
		/// <returns>HRESULT of the IDebugControl4::ControlledOutputWide or IDebugControl::ControlledOutput call</returns>
		public int OutputDML(string textFormatString, string commandFormatString, params object[] parameters)
		{
			return OutputDMLPreformatted(FormatDML(textFormatString, commandFormatString, parameters));
		}

		/// <summary>
		/// Outputs a formatted DML command block using the desired text and command
		/// </summary>
		/// <param name="textFormatString">Format string which will appear as a clickable link</param>
		/// <param name="commandFormatString">Format string for the command to run when the link is clicked</param>
		/// <param name="parameters">Parameters to use when formatting the clickable text and the command string</param>
		/// <returns>HRESULT of the IDebugControl4::ControlledOutputWide or IDebugControl::ControlledOutput call</returns>
		public int OutputDMLLine(string textFormatString, string commandFormatString, params object[] parameters)
		{
			return OutputDMLPreformattedLine(FormatDML(textFormatString, commandFormatString, parameters));
		}

        /// <summary>
        /// Outputs a formatted DML command block using the desired text and command
        /// </summary>
        /// <param name="textFormatString">Format string which will appear as a clickable link</param>
        /// <param name="commandFormatString">Format string for the command to run when the link is clicked</param>
        /// <param name="parameters">Parameters to use when formatting the clickable text and the command string</param>
        /// <returns>HRESULT of the IDebugControl4::ControlledOutputWide or IDebugControl::ControlledOutput call</returns>
        public int OutputDMLLineWithCommandIfDMLNotPossible(string textFormatString, string commandFormatString, params object[] parameters)
        {
            return OutputDMLPreformattedLine(FormatDMLWithDMLCheck(textFormatString, commandFormatString, parameters));
        }

        /// <summary>
        /// Outputs a formatted DML command block using the desired text and command
        /// </summary>
        /// <param name="text">Text which will appear as a clickable link</param>
        /// <param name="command">The command to run when the link is clicked</param>
        /// <returns>HRESULT of the IDebugControl4::ControlledOutputWide or IDebugControl::ControlledOutput call</returns>
        public int OutputErrorDML(string text, string command)
        {
            return OutputErrorDMLPreformatted(FormatDML(text, command));
        }

        /// <summary>
        /// Outputs a formatted DML command block using the desired text and command
        /// </summary>
        /// <param name="text">Text which will appear as a clickable link</param>
        /// <param name="command">The command to run when the link is clicked</param>
        /// <returns>HRESULT of the IDebugControl4::ControlledOutputWide or IDebugControl::ControlledOutput call</returns>
        public int OutputErrorDMLLine(string text, string command)
        {
            return OutputErrorDMLPreformattedLine(FormatDML(text, command));
        }

        /// <summary>
        /// Outputs a formatted DML command block using the desired text and command
        /// </summary>
        /// <param name="textFormatString">Format string which will appear as a clickable link</param>
        /// <param name="commandFormatString">Format string for the command to run when the link is clicked</param>
        /// <param name="parameters">Parameters to use when formatting the clickable text and the command string</param>
        /// <returns>HRESULT of the IDebugControl4::ControlledOutputWide or IDebugControl::ControlledOutput call</returns>
        public int OutputErrorDML(string textFormatString, string commandFormatString, params object[] parameters)
        {
            return OutputErrorDMLPreformatted(FormatDML(textFormatString, commandFormatString, parameters));
        }

        /// <summary>
        /// Outputs a formatted DML command block using the desired text and command
        /// </summary>
        /// <param name="textFormatString">Format string which will appear as a clickable link</param>
        /// <param name="commandFormatString">Format string for the command to run when the link is clicked</param>
        /// <param name="parameters">Parameters to use when formatting the clickable text and the command string</param>
        /// <returns>HRESULT of the IDebugControl4::ControlledOutputWide or IDebugControl::ControlledOutput call</returns>
        public int OutputErrorDMLLine(string textFormatString, string commandFormatString, params object[] parameters)
        {
            return OutputErrorDMLPreformattedLine(FormatDML(textFormatString, commandFormatString, parameters));
        }


        // ===
        /// <summary>
        /// Outputs a formatted DML command block using the desired text and command
        /// </summary>
        /// <param name="text">Text which will appear as a clickable link</param>
        /// <param name="command">The command to run when the link is clicked</param>
        /// <returns>HRESULT of the IDebugControl4::ControlledOutputWide or IDebugControl::ControlledOutput call</returns>
        public int OutputWarningDML(string text, string command)
        {
            return OutputWarningDMLPreformatted(FormatDML(text, command));
        }

        /// <summary>
        /// Outputs a formatted DML command block using the desired text and command
        /// </summary>
        /// <param name="text">Text which will appear as a clickable link</param>
        /// <param name="command">The command to run when the link is clicked</param>
        /// <returns>HRESULT of the IDebugControl4::ControlledOutputWide or IDebugControl::ControlledOutput call</returns>
        public int OutputWarningDMLLine(string text, string command)
        {
            return OutputWarningDMLPreformattedLine(FormatDML(text, command));
        }

        /// <summary>
        /// Outputs a formatted DML command block using the desired text and command
        /// </summary>
        /// <param name="textFormatString">Format string which will appear as a clickable link</param>
        /// <param name="commandFormatString">Format string for the command to run when the link is clicked</param>
        /// <param name="parameters">Parameters to use when formatting the clickable text and the command string</param>
        /// <returns>HRESULT of the IDebugControl4::ControlledOutputWide or IDebugControl::ControlledOutput call</returns>
        public int OutputWarningDML(string textFormatString, string commandFormatString, params object[] parameters)
        {
            return OutputWarningDMLPreformatted(FormatDML(textFormatString, commandFormatString, parameters));
        }

        /// <summary>
        /// Outputs a formatted DML command block using the desired text and command
        /// </summary>
        /// <param name="textFormatString">Format string which will appear as a clickable link</param>
        /// <param name="commandFormatString">Format string for the command to run when the link is clicked</param>
        /// <param name="parameters">Parameters to use when formatting the clickable text and the command string</param>
        /// <returns>HRESULT of the IDebugControl4::ControlledOutputWide or IDebugControl::ControlledOutput call</returns>
        public int OutputWarningDMLLine(string textFormatString, string commandFormatString, params object[] parameters)
        {
            return OutputWarningDMLPreformattedLine(FormatDML(textFormatString, commandFormatString, parameters));
        }

        // ===

        /// <summary>
        /// Outputs a formatted DML command block using the desired text and command
        /// </summary>
        /// <param name="text">Text which will appear as a clickable link</param>
        /// <param name="command">The command to run when the link is clicked</param>
        /// <returns>HRESULT of the IDebugControl4::ControlledOutputWide or IDebugControl::ControlledOutput call</returns>
        public int OutputVerboseDML(string text, string command)
        {
            return OutputVerboseDMLPreformatted(FormatDML(text, command));
        }

        /// <summary>
        /// Outputs a formatted DML command block using the desired text and command
        /// </summary>
        /// <param name="text">Text which will appear as a clickable link</param>
        /// <param name="command">The command to run when the link is clicked</param>
        /// <returns>HRESULT of the IDebugControl4::ControlledOutputWide or IDebugControl::ControlledOutput call</returns>
        public int OutputVerboseDMLLine(string text, string command)
        {
            return OutputVerboseDMLPreformattedLine(FormatDML(text, command));
        }

        /// <summary>
        /// Outputs a formatted DML command block using the desired text and command
        /// </summary>
        /// <param name="textFormatString">Format string which will appear as a clickable link</param>
        /// <param name="commandFormatString">Format string for the command to run when the link is clicked</param>
        /// <param name="parameters">Parameters to use when formatting the clickable text and the command string</param>
        /// <returns>HRESULT of the IDebugControl4::ControlledOutputWide or IDebugControl::ControlledOutput call</returns>
        public int OutputVerboseDML(string textFormatString, string commandFormatString, params object[] parameters)
        {
            return OutputVerboseDMLPreformatted(FormatDML(textFormatString, commandFormatString, parameters));
        }

        /// <summary>
        /// Outputs a formatted DML command block using the desired text and command
        /// </summary>
        /// <param name="textFormatString">Format string which will appear as a clickable link</param>
        /// <param name="commandFormatString">Format string for the command to run when the link is clicked</param>
        /// <param name="parameters">Parameters to use when formatting the clickable text and the command string</param>
        /// <returns>HRESULT of the IDebugControl4::ControlledOutputWide or IDebugControl::ControlledOutput call</returns>
        public int OutputVerboseDMLLine(string textFormatString, string commandFormatString, params object[] parameters)
        {
            return OutputVerboseDMLPreformattedLine(FormatDML(textFormatString, commandFormatString, parameters));
        }


		/// <summary>
		/// Returns a formatted DML command block
		/// </summary>
		/// <param name="text">The text that should appear as clickable when displayed</param>
		/// <param name="command">The command that should execute when the DML link is clicked</param>
		/// <returns>The formatted DML command block</returns>
		public string FormatDML(string text, string command)
		{
			return String.Format(CultureInfo.InvariantCulture, "<link cmd=\"{0}\">{1}</link>", EncodeTextForXml(command), EncodeTextForXml(text));
		}

		/// <summary>
		/// Returns a formatted DML command block
		/// </summary>
		/// <param name="textFormatString">Format string which will appear as a clickable link</param>
		/// <param name="commandFormatString">Format string for the command to run when the link is clicked</param>
		/// <param name="parameters">Parameters to use when formatting the clickable text and the command string</param>
		/// <returns>The formatted DML command block</returns>
		public string FormatDML(string textFormatString, string commandFormatString, params object[] parameters)
		{
			ScanForPointerArguments(commandFormatString, parameters, out commandFormatString);
			string formattedCommand = FormatString(commandFormatString, parameters);
			return String.Format(CultureInfo.InvariantCulture, "<link cmd=\"{0}\">{1}</link>", EncodeTextForXml(formattedCommand), EncodeTextForXml(FormatString(textFormatString, parameters)));
		}

        /// <summary>
        /// Returns a formatted DML command block
        /// </summary>
        /// <param name="textFormatString">Format string which will appear as a clickable link</param>
        /// <param name="commandFormatString">Format string for the command to run when the link is clicked</param>
        /// <param name="parameters">Parameters to use when formatting the clickable text and the command string</param>
        /// <returns>The formatted DML command block</returns>
        public string FormatDMLWithDMLCheck(string textFormatString, string commandFormatString, params object[] parameters)
        {
            if (IsDebuggerDMLCapable())
            {
                return FormatDML(textFormatString, commandFormatString, parameters);
            }
            ScanForPointerArguments(commandFormatString, parameters, out commandFormatString);
            string formattedCommand = FormatString(commandFormatString, parameters);
            return String.Format(CultureInfo.InvariantCulture, "<link cmd=\"{0}\">{1}[{0}]</link>", EncodeTextForXml(formattedCommand), EncodeTextForXml(FormatString(textFormatString, parameters)));
        }

		/// <summary>
		/// Formats a DML command block using the desired text and command, then appends it to a DbgStringBuilder
		/// </summary>
		/// <param name="sb">StringBuilder to which the DML command block is appended</param>
		/// <param name="text">Text which will appear as a clickable link</param>
		/// <param name="command">The command to run when the link is clicked</param>
		public void AppendDML(StringBuilder sb, string text, string command)
		{
			sb.Append("<link cmd=\"");
			sb.Append(EncodeTextForXml(command));
			sb.Append("\">");
			sb.Append(EncodeTextForXml(text));
			sb.Append("</link>");
		}

		/// <summary>
		/// Formats a DML command block using the desired text and command, then appends it to a DbgStringBuilder
		/// </summary>
		/// <param name="sb">DbgStringBuilder to which the DML command block is appended</param>
		/// <param name="textFormatString">Format string which will appear as a clickable link</param>
		/// <param name="commandFormatString">Format string for the command to run when the link is clicked</param>
		/// <param name="parameters">Parameters to use when formatting the clickable text and the command string</param>
		public void AppendDML(StringBuilder sb, string textFormatString, string commandFormatString, params object[] parameters)
		{
			ScanForPointerArguments(commandFormatString, parameters, out commandFormatString);
			sb.Append("<link cmd=\"");
			sb.Append(EncodeTextForXml(FormatString(commandFormatString, parameters)));
			sb.Append("\">");
			sb.Append(EncodeTextForXml(FormatString(textFormatString, parameters)));
			sb.Append("</link>");
		}

		/// <summary>
		/// Encodes a text string and replaces any special XML/DML characters with the appropriate replacement.
		/// </summary>
		/// <param name="input">String to modify</param>
		/// <returns>Encoded string</returns>
		public string EncodeTextForXml(string input)
		{
			//NOTE: Apparently DML doesn't want escaped apostrophes, because it isn't following the XML spec. Stupid.
			//return System.Security.SecurityElement.Escape(input);
			return input.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
		}
		
		/// <summary>
		/// Decodes a text string and replaces any special XML/DML escape sequences with the appropriate character.
		/// </summary>
		/// <param name="input">String to modify</param>
		/// <returns>Decoded string</returns>
		public string DecodeTextFromXml(string input)
		{
			return input.Replace("&gt;", ">").Replace("&lt;", "<").Replace("&quot;", "\"").Replace("&apos;", "'").Replace("&amp;", "&");
		}

		/// <summary>
		/// Encodes a text string and replaces any special XML/DML characters with the appropriate replacement.
		/// </summary>
		/// <param name="input">String to modify</param>
		/// <returns>Encoded string</returns>
		public static string s_EncodeTextForXml(string input)
		{
			//NOTE: Apparently DML doesn't want escaped apostrophes, because it isn't following the XML spec. Stupid.
			//return System.Security.SecurityElement.Escape(input);
			return input.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
		}

		/// <summary>
		/// Decodes a text string and replaces any special XML/DML escape sequences with the appropriate character.
		/// </summary>
		/// <param name="input">String to modify</param>
		/// <returns>Decoded string</returns>
		public static string s_DecodeTextFromXml(string input)
		{
			return input.Replace("&gt;", ">").Replace("&lt;", "<").Replace("&quot;", "\"").Replace("&apos;", "'").Replace("&amp;", "&");
		}
        /// <summary>
        /// Converts an address to a 8 or 16 character hex string depending on target architecture.
        /// </summary>
        /// <param name="address">Address to convert to a string</param>
        /// <returns>8 or 16 character hex representation of address</returns>
        public string P2S(UInt64 address)
        {
            if (DebugControl.IsPointer64Bit() == S_OK)
            {
                return address.ToString("x16", CultureInfo.InvariantCulture);
            }
            else
            {
                return ((uint)address).ToString("x8", CultureInfo.InvariantCulture);
            }
        }

        /// <summary>
        /// Converts an address to a 8 or 16 character hex string depending on target architecture.
        /// This version uses uppercase alpha characters.
        /// </summary>
        /// <param name="address">Address to convert to a string</param>
        /// <returns>8 or 16 character hex representation of address</returns>
        public string P2SUC(UInt64 address)
        {
            if (DebugControl.IsPointer64Bit() == S_OK)
            {
                return address.ToString("X16", CultureInfo.InvariantCulture);
            }
            else
            {
                return ((uint)address).ToString("X8", CultureInfo.InvariantCulture);
            }
        }


		private static Regex PointerArgumentRegex = new Regex(@"{(\d+):([pP])(,-?\d+)?}", RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Singleline);
		private void ScanForPointerArguments(string inputString, object[] parameters, out string outputString)
		{
			if ((parameters == null) || (parameters.Length == 0))
			{
				outputString = inputString;
				return;
			}

			MatchCollection mc = PointerArgumentRegex.Matches(inputString);
			if ((mc == null) || (mc.Count == 0))
			{
				outputString = inputString;
				return;
			}

			StringBuilder sb = new StringBuilder(inputString.Length << 1);

			int endOfLastMatch = 0;
			for (int i = 0; i < mc.Count; ++i)
			{
				Match m = mc[i];
				if (m.Index != endOfLastMatch)
				{
					sb.Append(inputString.Substring(endOfLastMatch, m.Index - endOfLastMatch));
				}
				endOfLastMatch = m.Index + m.Length;

				int parameterIndex = int.Parse(m.Groups[1].Value);

				UInt64 pointer;
				object parameter = parameters[parameterIndex];
				if (parameter is UInt64)
				{
					pointer = (UInt64)parameter;
				}
				else
				{
					try { pointer = Convert.ToUInt64(parameter); }
					catch { throw; }
					//catch { pointer = 0xdeadbeef; }
				}

				string pointerAsString;
				if (m.Groups[2].Value == "p")
				{
					pointerAsString = P2S((UInt64)parameters[parameterIndex]);
				}
				else
				{
					pointerAsString = P2SUC((UInt64)parameters[parameterIndex]);
				}
				if ((m.Groups.Count >= 4) && (m.Groups[3] != null) && (m.Groups[3].Value != null) && (m.Groups[3].Value.Length != 0))
				{
					bool leftAlign;
					int desiredWidth = int.Parse(m.Groups[3].Value.Substring(1));
					if (desiredWidth < 0)
					{
						desiredWidth = -desiredWidth;
						leftAlign = true;
					}
					else
					{
						leftAlign = false;
					}

					int spacesToAdd = desiredWidth - pointerAsString.Length;
					if (spacesToAdd > 0)
					{
						if (leftAlign)
						{
							sb.Append(pointerAsString);
							sb.Append(' ', spacesToAdd);
						}
						else
						{
							sb.Append(' ', spacesToAdd);
							sb.Append(pointerAsString);
						}
					}
					else
					{
						sb.Append(pointerAsString);
					}
				}
				else
				{
					sb.Append(pointerAsString);
				}
			}
			if (endOfLastMatch < inputString.Length)
			{
				sb.Append(inputString.Substring(endOfLastMatch, inputString.Length - endOfLastMatch));
			}

			outputString = sb.ToString();
		}

		/// <summary>
		/// Like String.Format except it understands the special pointer identifier
		/// </summary>
		/// <param name="format">Format string</param>
		/// <param name="parameters">Parameters to use as data when formatingg</param>
		/// <returns>Formatted string</returns>
		public string FormatString(string format, params object[] parameters)
		{
			ScanForPointerArguments(format, parameters, out format);
			return String.Format(System.Globalization.CultureInfo.InvariantCulture, format, parameters);
		}

		private string EscapePercents(string input)
		{
			int index = input.IndexOf('%');
			if (index == -1) return input;

			int segmentStart = 0;
			StringBuilder sb = new StringBuilder(input.Length << 1);

			do
			{
				int prefixLength = index - segmentStart;
				if (prefixLength > 0)
				{
					sb.Append(input.Substring(segmentStart, prefixLength));
				}
				int percentCount = 1;

				char c;
				while ((++index < input.Length) && ((c = input[index]) == '%'))
				{
					++percentCount;
				}

				sb.Append('%', ((percentCount & 1) == 0) ? percentCount : percentCount + 1);

				segmentStart = index;

				index = input.IndexOf('%', segmentStart);
				if (index == -1)
				{
					sb.Append(input.Substring(segmentStart));
					break;
				}

			} while (index < input.Length);

			return sb.ToString();
		}

		/// <summary>
		/// Replaces all occurrences of a string in the debugger output with the contents of another
		/// </summary>
		/// <param name="variable">String to replace. Normally something like @#VariableName</param>
		/// <param name="replacement">Text that should be used in place of variable</param>
		/// <returns>HRESULT</returns>
		public int AliasSet(string variable, string replacement)
		{
			OutputVerboseLine("ALIAS: {0} = {1}", variable, replacement);

			DebugControl.SetTextReplacementWide(variable, null);
			return DebugControl.SetTextReplacementWide(variable, replacement);
		}

        public bool IsDebuggerDMLCapable()
        {
            if (FAILED(DebugAdvanced.Request(DEBUG_REQUEST.CURRENT_OUTPUT_CALLBACKS_ARE_DML_AWARE, null, 0, null, 0, null))){
                return false;
            }

            return true;
        }

		/// <summary>
		/// Stops replacing text for a specific string.
		/// </summary>
		/// <param name="variable">String to reset. Normally something like @#VariableName</param>
		/// <returns>HRESULT</returns>
		public int AliasClear(string variable)
		{
			return DebugControl.SetTextReplacementWide(variable, null);
		}

		/// <summary>
		/// Changes a DML block to plain text
		/// </summary>
		public static string DmlToText(string dml)
		{
			int openBracket = dml.IndexOf('<');
			if (openBracket < 0)
			{
				goto UnescapeAnyway;
			}

			int closeBracket = dml.IndexOf('>', openBracket);
			if (closeBracket < 0)
			{
				goto UnescapeAnyway;
			}

			StringBuilder sb = new StringBuilder();
			if (openBracket != 0)
			{
				sb.Append(dml, 0, openBracket);
			}

			for (; ; )
			{
				openBracket = dml.IndexOf('<', closeBracket);
				if (openBracket < 0)
				{
					sb.Append(dml, closeBracket + 1, dml.Length - closeBracket - 1);
					break;
				}

				int previousCloseBracket = closeBracket;
				closeBracket = dml.IndexOf('>', openBracket);
				if (closeBracket < 0)
				{
					sb.Append(dml, openBracket, dml.Length - openBracket);
					break;
				}

				sb.Append(dml, previousCloseBracket + 1, openBracket - previousCloseBracket - 1);
			}

			return sb.Replace("&quot;", "\"").Replace("&lt;", "<").Replace("&gt;", ">").Replace("&amp;", "&").Replace("&apos;", "'").ToString();

		UnescapeAnyway:
			return dml.Replace("&quot;", "\"").Replace("&lt;", "<").Replace("&gt;", ">").Replace("&amp;", "&").Replace("&apos;", "'");
		}
	
        public string FormatToHex(ulong addr)
        {            
            var output = String.Format("0x{0:X}", addr).ToLower();
            
            // If we have more than 8 hex characters, insert a backtick
            if (output.Length > 10)
                output = output.Insert(output.Length - 8, "`");

            return output;
        }
    
    }

}
