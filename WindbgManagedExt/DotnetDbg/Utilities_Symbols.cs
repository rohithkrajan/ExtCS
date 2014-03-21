using System;
using System.IO;
using System.Text;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Collections.Generic;

using System.Reflection;
using System.Diagnostics;
using System.Collections;

namespace DotNetDbg
{
	public unsafe partial class DebugUtilities
	{
		/// <summary>
		/// Sets whether the debugger engine should attempt to get symbols from network paths.
		/// Allows you to override scenarios when the enginer disables network shares by default.
		/// WARNING! Can cause the debugger to hang if used improperly! USE CAUTION!!!
		/// </summary>
		/// <param name="enabled">True to enable network paths, false to disable</param>
		/// <returns>Last HRESULT value returned</returns>
		public int SetNetworkSymbolsState(bool enabled)
		{
			/* This is the equivalent of executing .netsyms 0|1 */
			int hr;

			DEBUG_ENGOPT options;
			DebugControl.GetEngineOptions(out options);
			if (enabled)
			{
				options &= ~DEBUG_ENGOPT.DISALLOW_NETWORK_PATHS;
				options |= DEBUG_ENGOPT.ALLOW_NETWORK_PATHS;
			}
			else
			{
				options &= ~DEBUG_ENGOPT.ALLOW_NETWORK_PATHS;
				options |= DEBUG_ENGOPT.DISALLOW_NETWORK_PATHS;
			}
			hr = DebugControl.SetEngineOptions(options);

			return hr;

			//return RunCommandAndGo(enabled ? ".netsyms 1" : ".netsyms 0", (char**)NULL);
		}

		/// <summary>
		/// Load symbols for a specific module.
		/// </summary>
		/// <param name="moduleName">"Name of the module to load symbols for."</param>
		/// <param name="allowNetworkPaths">Whether network paths should be searched for the symbols. Defaults to true.</param>
		/// <returns>Last HRESULT value returned</returns>
		public unsafe int LoadSymbolFor(string moduleName, bool allowNetworkPaths = true)
		{
			/*
				Try to load the symbol. If it fails, add \\symbols\symbols and http://msdl.microsoft.com/download/symbols to the symbol path and try again.

				If we happen to be broken into services.exe/lsass.exe/csrss.exe we can only get symbols from the cache when attached interactively.
			*/

			if (moduleName == null) return E_FAIL;

			string appendedName = "/f " + moduleName;

			/* Ignoring the errors */
			//			#if DEBUG
			RunCommandInternal("!sym noisy", false, 500);
			//			#endif

			SetNetworkSymbolsState(allowNetworkPaths);

			string executingDirectory;
			if (GetDirectoryForModule(null, out executingDirectory) == false)
			{
				return E_FAIL;
			}
			StringBuilder cachePath = new StringBuilder("cache*").Append(executingDirectory);

			int hr;

			uint bufferSize;
			DebugSymbols.GetSymbolPathWide(null, 0, &bufferSize);
			if (bufferSize != 0)
			{
				++bufferSize;
				StringBuilder symbolPathBuffer = new StringBuilder((int)bufferSize);
				DebugSymbols.GetSymbolPathWide(symbolPathBuffer, symbolPathBuffer.Capacity, null);
				string existingSymbolPath = symbolPathBuffer.ToString();
				string existingSymbolPathUpper = existingSymbolPath.ToUpperInvariant();
				if (existingSymbolPathUpper.Contains("CACHE*") == false)
				{
					DebugSymbols.SetSymbolPathWide(cachePath + ";" + existingSymbolPath);
				}
				if (existingSymbolPathUpper.Contains(@"http://SYMWEB") == false)
				{
					DebugSymbols.AppendSymbolPathWide(@"srv*http://SYMWEB");
				}
				if (existingSymbolPathUpper.Contains("HTTP://MSDL.MICROSOFT.COM/DOWNLOAD/SYMBOLS") == false)
				{
					DebugSymbols.AppendSymbolPathWide("srv*http://msdl.microsoft.com/download/symbols");
				}
			}
			else
			{
				DebugSymbols.AppendSymbolPathWide(cachePath.ToString());
				DebugSymbols.AppendSymbolPathWide(@"srv*\\symbols\symbols");
				DebugSymbols.AppendSymbolPathWide(@"srv*http://msdl.microsoft.com/download/symbols");
			}

			hr = DebugSymbols.ReloadWide(appendedName);

			//			#if DEBUG
			//			RunCommand(debugClient, "!sym quiet", true, 500);
			//			#endif

			return hr;
		}


        /// <summary>
        /// Gets the ntdll name for the current effective architecture (x86, x64, etc).
        /// </summary>
        /// <returns>string</returns>
        public string GetNtdllName()
        {
            StringBuilder sb = new StringBuilder(64);
            DebugControl.GetTextReplacementWide("$ntdllsym", 0, null, 0, null, sb, 128, null);
            string ntdll = sb.ToString();
            if (ntdll.StartsWith("nt"))
                return ntdll;

            return "ntdll";
        }


        /// <summary>
        /// Gets the 32 bit ntdll name
        /// </summary>
        /// <returns>string</returns>
        public string GetNtdll32Name(DebugUtilities d)
        {
            uint wow64_NtDll32Base;
            d.ReadGlobalAsUInt32("wow64", "NtDll32Base", out wow64_NtDll32Base);
            return "_" + wow64_NtDll32Base.ToString("x");
        }

        /// <summary>
        /// Gets the correct ntdll name if moduleName is ntdll, returns moduleName module otherwise
        /// </summary>
        /// <param name="moduleName">Name of the module</param>
        /// <returns>string</returns>
        public string FixModuleName(string moduleName)
        {

            if (String.IsNullOrEmpty(moduleName))
            {
                return string.Empty;
            }

            if ((string.Compare("ntdll", moduleName, StringComparison.OrdinalIgnoreCase) == 0))
            {
                OutputDebugLine("Returning NTDLL name due to {0} matching ntdll", moduleName);
                return GetNtdllName();
            }

            return moduleName; 
        }


		/// <summary>
		/// Get the base address for a module
		/// </summary>
		/// <param name="moduleName">Name of the module</param>
		/// <param name="moduleBase">UInt64 to receive the base address</param>
		/// <returns>HRESULT</returns>
		public unsafe int GetModuleBase(string moduleName, out UInt64 moduleBase)
		{
            moduleName = FixModuleName(moduleName);

            //if ((string.Compare("ntdll", moduleName, StringComparison.OrdinalIgnoreCase) == 0) && (wow64exts.IsEffectiveProcessorSameAsActualProcessor(this) == false))
            //{
            //    moduleName = GetNtdllName();

            //    //List<ModuleInfo> moduleList = Modules.GetLoadedModuleList(this, "^ntdll_[0-9a-fA-F]+$");
            //    //if ((moduleList != null) && (moduleList.Count > 0))
            //    //{
            //    //    moduleName = moduleList[0].Name;
            //    //    OutputVerboseLine("DotNetDbg.GetModuleBase: Wow64 detected, changing name 'ntdll' to '{0}'", moduleName);
            //    //}
            //}

            if (Cache.GetModuleBase.TryGetValue(moduleName, out moduleBase))
            {
                return S_OK;
            }
            // Check Cache, return if cached.

			int hr;
			UInt64 tempModuleBase = 0;
			if (SUCCEEDED(hr = DebugSymbols.GetModuleByModuleNameWide(moduleName, 0, null, &tempModuleBase)))
			{
				if (tempModuleBase == 0)
				{
					moduleBase = 0;
					return E_FAIL;
				}
				moduleBase = IsPointer64Bit() ? tempModuleBase : SignExtendAddress(tempModuleBase);
                // Add to cache
                Cache.GetModuleBase.Add(moduleName, moduleBase);
			}
			else
			{
				moduleBase = 0;
			}
			return hr;
		}


        ///// <summary>
        ///// Gets the ID for a type in a module
        ///// </summary>
        ///// <param name="moduleBase">Base address of the module</param>
        ///// <param name="typeName">Name of the type</param>
        ///// <param name="typeId">UInt32 to receive the type's ID</param>
        ///// <returns>HRESULT</returns>
        //public int GetTypeId(UInt64 moduleBase, string typeName, out UInt32 typeId)
        //{
        //    int hr;

        //    if (Cache.GetTypeId.TryGetValue(moduleBase.ToString("x") + "#" + typeName, out typeId))
        //    {
        //        return Mex.S_OK;
        //    }

        //    if (FAILED(hr = DebugSymbols.GetTypeIdWide(moduleBase, typeName, out typeId)))
        //    {
        //        typeId = 0;
        //    }
        //    else
        //    {
        //        Cache.GetTypeId.Add(moduleBase.ToString("x") + "#" + typeName, typeId);
        //    }
        //    return hr;
        //}


        /// <summary>
        /// Determine if a type is valid
        /// </summary>
        /// <param name="moduleName">Base name of the module</param>
        /// <param name="typeName">Name of the type</param>
        /// <returns>bool</returns>
        public bool IsTypeValid(string moduleName, string typeName)
        {
            moduleName = FixModuleName(moduleName);

            int hr;
            uint typeId;
            string FQModule = moduleName + "!" + typeName;

            if (Cache.GetTypeId.TryGetValue(FQModule, out typeId))
            {
                return true;
            }

            if (Cache.GetTypeId.TryGetValue("-" + FQModule, out typeId))
            {
                return false;
            }

            if (FAILED(hr = DebugSymbols.GetTypeIdWide(0, FQModule, out typeId)))
            {
                //OutputErrorLine("Failed: {0} {1} {2}", moduleName, typeName, typeId);
                OutputVerboseLine("GetTypeIdWide failed for {0}", FQModule);
                Cache.GetTypeId.Add("-" + FQModule, typeId);
                return false;
            }
            else
            {
                //OutputErrorLine("Added: {0} {1} {2}", moduleName, typeName, typeId);
                Cache.GetTypeId.Add(FQModule, typeId);
                return true;
            }
        }

        /// <summary>
        /// Gets the ID for a type in a module
        /// </summary>
        /// <param name="moduleName">Base name of the module</param>
        /// <param name="typeName">Name of the type</param>
        /// <param name="typeId">UInt32 to receive the type's ID</param>
        /// <returns>HRESULT</returns>
        public int GetTypeId(string moduleName, string typeName, out UInt32 typeId)
        {           
            moduleName = FixModuleName(moduleName);

            int hr;

            string FQModule = moduleName + "!" + typeName;

            /// 
            /// Removing caching from this function as it causes unexplained errors on the second call to extensions (???)
            /// 

            //if (Cache.GetTypeId.TryGetValue(FQModule, out typeId))
            //{
            //    OutputErrorLine("Retrived: {0} {1} {2}", moduleName, typeName, typeId);
            //    return Mex.S_OK;
            //}

            if (Cache.GetTypeId.TryGetValue("-"+FQModule, out typeId))
            {
                typeId = 0;
                return Mex.E_FAIL;
            }

            if (FAILED(hr = DebugSymbols.GetTypeIdWide(0, FQModule, out typeId)))
            {
                //OutputErrorLine("Failed: {0} {1} {2}", moduleName, typeName, typeId);
                typeId = 0;
                OutputVerboseLine("GetTypeIdWide failed for {0}", FQModule);
                Cache.GetTypeId["-" + FQModule] = typeId;
            }
            else
            {
                //OutputErrorLine("Added: {0} {1} {2}", moduleName, typeName, typeId);
                Cache.GetTypeId[FQModule] = typeId;
            }
            

            return hr;
        }


        /// <summary>
        /// Gets the ID for a symbol type. Wrapper for GetSymbolTypeIdWide that caches.
        /// </summary>
        /// <param name="moduleName">Base name of the module</param>
        /// <param name="typeName">Name of the type</param>
        /// <param name="typeId">UInt32 to receive the type's ID</param>
        /// <returns>HRESULT</returns>
        public int GetSymbolTypeIdWide(string symbolName, out UInt32 typeId , out ulong moduleBase)
        {

            int hr;
            TypeInfoCache tInfo;
            symbolName = symbolName.TrimEnd();
            if (symbolName.EndsWith("*"))
            {
                symbolName = symbolName.Substring(0, symbolName.Length - 1).TrimEnd();
            }

            if (Cache.GetSymbolTypeIdWide.TryGetValue(symbolName, out tInfo))
            {
                typeId = tInfo.typeId;
                moduleBase = tInfo.modulebase;
                return Mex.S_OK;
            }

            if (Cache.GetSymbolTypeIdWide.TryGetValue("-" + symbolName, out tInfo))
            {
                typeId = 0;
                moduleBase = 0;
                return Mex.E_FAIL;
            }


            ulong Module;

            if (FAILED(hr = DebugSymbols.GetSymbolTypeIdWide(symbolName, out typeId, &Module)))
            {
                moduleBase = Module;
                OutputVerboseLine("GetSymbolTypeIdWide failed for {0} 0x{1:x}", symbolName,hr);
                if (ShouldBreak(true) == false)
                {
                    Cache.GetSymbolTypeIdWide.Add("-" + symbolName, tInfo);
                }
            }
            else
            {
                tInfo.modulebase = Module;
                moduleBase = Module;
                tInfo.typeId = typeId;
                Cache.GetSymbolTypeIdWide.Add(symbolName, tInfo);
            }
            return hr;
        }


        /// <summary>
        /// Gets the Name of a field by offset. Currently only work on SymbolNames (Like ntdll!LdrpLoaderLock).
        /// Email timhe if you need this expanded to work on a Type Name
        /// </summary>
        /// <param name="SymbolName">Name of the symbol (eg ntdll!LdrpLoaderLock, this)</param>
        /// <param name="Offset">Field Offset</param>
        /// <returns>String FieldName (includes [x].membername,or just .membername if not an array, or String.Empty on failure or fieldnotfound</returns>
        public string GetFieldNameByOffset(string SymbolName, uint Offset)
        {
          
            // Get the ModuleBase
            ulong ModuleBase;

            // Get the TypeID for the parent structure
            uint TypeId;
            uint TypeSize = 0;
            uint arraymember = 0;
            if (!SymbolName.Contains("!"))
            {
                return string.Empty;
            }
            using (var wrapper = InstallIgnoreFilter_WRAP_WITH_USING())
            {
                int hr = GetSymbolTypeIdWide(SymbolName, out TypeId, out ModuleBase);

                string Name;
                GetModuleName(ModuleBase, out Name);

                StringBuilder sb = new StringBuilder(256);

                hr = DebugSymbols.GetTypeName(ModuleBase, TypeId, sb, 256, null);

                bool array = false;
                if (SUCCEEDED(hr))
                {
                    if (sb.ToString().Contains("[]") && !sb.ToString().Contains("char[]"))
                        array = true;
                }

                if (array)
                {
                    DebugSymbols.GetTypeSize(ModuleBase, TypeId, out TypeSize);
                    string TypeName = sb.ToString().Replace("[]", string.Empty);
                    uint TypeSize2 = 0;
                    GetTypeSize(Name, TypeName, out TypeSize2);
                    //---timhe
                    if (TypeSize2 == 0)
                    {
                        arraymember = 0;
                        Offset = 0;
                    }
                    else
                    {
                        arraymember = Offset / TypeSize2;
                        Offset = Offset % TypeSize2;
                    }
                    OutputVerboseLine("Size = {0}, Size2 = {3},member = {1}, Offset = {2}", TypeSize, arraymember, Offset, TypeSize2);
                }

                string tName = sb.ToString();

                OutputVerboseLine("Type Name :{0}", tName);

                sb.Clear();
                sb.Append(Name + "!");
                uint i = 0;

                while (hr == 0)
                {
                    hr = DebugSymbols.GetFieldNameWide(ModuleBase, TypeId, i, sb, sb.Capacity, null);
                    if (FAILED(hr))
                        break;
                    uint offset;
                    GetFieldOffset(Name, tName, sb.ToString(), out offset);

                    if (offset == Offset)
                    {
                        if (array)
                        {
                            sb.Insert(0, "[" + arraymember + "].");
                        }
                        else
                        {
                            sb.Insert(0, ".");
                        }
                        return sb.ToString();
                    }

                    i++;
                }
            }
            return string.Empty;
        }


        /// <summary>
        /// Gets the Name of a field by offset. Currently only work on SymbolNames (Like ntdll!LdrpLoaderLock).
        /// Email timhe if you need this expanded to work on a Type Name
        /// </summary>
        /// <param name="SymbolName">Name of the symbol (eg ntdll!LdrpLoaderLock, this)</param>
        /// <param name="Offset">Field Offset</param>
        /// <returns>String FieldName (includes [x].membername,or just .membername if not an array, or String.Empty on failure or fieldnotfound</returns>
        public string GetFieldNameByOffset(ulong ModuleBase, uint TypeId, uint Offset)
        {
            uint TypeSize = 0;
            uint arraymember = 0;
            int hr;
            using (var wrapper = InstallIgnoreFilter_WRAP_WITH_USING())
            {
                string Name;
                GetModuleName(ModuleBase, out Name);
                
                StringBuilder sb = new StringBuilder(256);

                hr = DebugSymbols.GetTypeName(ModuleBase, TypeId, sb, 256, null);

                bool array = false;
                if (SUCCEEDED(hr))
                {
                    if (sb.ToString().Contains("[]") && !sb.ToString().Contains("char[]"))
                        array = true;
                }

                if (array)
                {
                    DebugSymbols.GetTypeSize(ModuleBase, TypeId, out TypeSize);
                    string TypeName = sb.ToString().Replace("[]", string.Empty);
                    uint TypeSize2 = 0;
                    GetTypeSize(ModuleBase,TypeId, out TypeSize2);
                    //---timhe
                    if (TypeSize2 == 0)
                    {
                        arraymember = 0;
                        Offset = 0;
                    }
                    else
                    {
                        arraymember = Offset / TypeSize2;
                        Offset = Offset % TypeSize2;
                    }
                    OutputVerboseLine("Size = {0}, Size2 = {3},member = {1}, Offset = {2}", TypeSize, arraymember, Offset, TypeSize2);
                }

                string tName = sb.ToString();

                OutputVerboseLine("Type Name :{0}", tName);

                sb.Clear();
                sb.Append(Name + "!");
                uint i = 0;

                while (hr == 0)
                {
                    hr = DebugSymbols.GetFieldNameWide(ModuleBase, TypeId, i, sb, sb.Capacity, null);
                    if (FAILED(hr))
                        break;
                    uint offset;
                    GetFieldOffset(ModuleBase,TypeId, sb.ToString(), out offset);

                    if (offset == Offset)
                    {
                        if (array)
                        {
                            sb.Insert(0, "[" + arraymember + "].");
                        }
                        else
                        {
                            sb.Insert(0, ".");
                        }
                        return sb.ToString();
                    }

                    i++;
                }
            }
            return string.Empty;
        }


        /// <summary>
		/// Gets the offset of a field in a type
		/// </summary>
        /// <param name="symbolName">fully qualified symbol name</param>
		/// <param name="fieldName">Name of the field</param>
		/// <param name="offset">UInt32 to receive the offset</param>
		/// <returns>HRESULT</returns>
		public int GetFieldOffset(string symbolName, string fieldName, out UInt32 offset)
		{
                    string part1 = "";
                    string part2 = symbolName;

                    if (symbolName.Contains("!"))
                    {
                        string[] symbolparts = symbolName.Split("!".ToCharArray(), 2);
                        part1 = symbolparts[0];
                        part2 = symbolparts[1];
                    }
                    return GetFieldOffset(part1, part2, fieldName, out offset);
        }


		/// <summary>
		/// Gets the offset of a field in a type
		/// </summary>
		/// <param name="moduleName">Name of the module that contains the type</param>
		/// <param name="typeName">Name of the type that contains the field</param>
		/// <param name="fieldName">Name of the field</param>
		/// <param name="offset">UInt32 to receive the offset</param>
		/// <returns>HRESULT</returns>
		public int GetFieldOffset(ulong moduleBase, uint typeId, string fieldName, out UInt32 offset)
		{
			int hr;
           
            // Check the Cache.  If Cache contains modulename + TypeName + fieldname, return that value.

            string CacheName = moduleBase.ToString("x") + "!" + typeId.ToString("x") + "." + fieldName;

            if (Cache.GetFieldOffset.TryGetValue(CacheName, out offset))
            {
                return S_OK;
            }

            if (Cache.GetFieldOffset.TryGetValue("-" + CacheName, out offset))
            {
                return E_FAIL;
            }
            
            uint FieldOffset;
            hr = DebugSymbols.GetFieldTypeAndOffsetWide(moduleBase, typeId, fieldName, null, &FieldOffset);
            if (SUCCEEDED(hr))
            {
                offset = FieldOffset;
                OutputDebugLine("GetFieldOffset: {0} is at offset {1:x}", CacheName, offset);
                Cache.GetFieldOffset.Add(CacheName, offset);
            }
            else
            {
                offset = 0;
                if (ShouldBreak(true) == false)
                {
                    Cache.GetFieldOffset.Add("-" + CacheName, offset);
                }
            }
            return hr;
		}


        /// <summary>
        /// Gets the offset of a field in a type
        /// </summary>
        /// <param name="moduleName">Name of the module that contains the type</param>
        /// <param name="typeName">Name of the type that contains the field</param>
        /// <param name="fieldName">Name of the field</param>
        /// <param name="offset">UInt32 to receive the offset</param>
        /// <returns>HRESULT</returns>
        public int GetFieldOffset(string moduleName, string typeName, string fieldName, out UInt32 offset)
        {
            int hr;
            moduleName = FixModuleName(moduleName);

            // Check the Cache.  If Cache contains modulename + TypeName + fieldname, return that value.

            if (Cache.GetFieldOffset.TryGetValue(moduleName + "!" + typeName + "." + fieldName, out offset))
            {
                return S_OK;
            }

            if (Cache.GetFieldOffset.TryGetValue("-" + moduleName + "!" + typeName + "." + fieldName, out offset))
            {
                return E_FAIL;
            }
            UInt64 moduleBase;
            UInt32 typeId;
            hr = GetSymbolTypeIdWide(moduleName + "!" + typeName, out typeId, out moduleBase);
            if (FAILED(hr))
            {
                offset = 0;
                if (ShouldBreak(true) == false)
                {
                    Cache.GetFieldOffset.Add("-" + moduleName + "!" + typeName + "." + fieldName, offset);
                }
                return hr;
            }

            uint FieldOffset;
            hr = DebugSymbols.GetFieldTypeAndOffsetWide(moduleBase, typeId, fieldName, null, &FieldOffset);
            if (SUCCEEDED(hr))
            {
                offset = FieldOffset;
                OutputDebugLine("GetFieldOffset: {0} is at offset {1:x}", moduleName + "!" + typeName + "." + fieldName, offset);
                Cache.GetFieldOffset.Add(moduleName + "!" + typeName + "." + fieldName, offset);
            }
            else
            {
                offset = 0;
                if (ShouldBreak(true) == false)
                {
                    Cache.GetFieldOffset.Add("-" + moduleName + "!" + typeName + "." + fieldName, offset);
                }
            }
            return hr;
        }

        /// <summary>
        /// Gets the size of a type
        /// </summary>
        /// <param name="moduleName">Name of the module that contains the type</param>
        /// <param name="typeName">Name of the type that contains the field</param>
        /// <returns>Size</returns>
        public UInt32 GetTypeSize(string moduleName, string typeName)
        {
            int hr;
            uint size;

            if (FAILED(hr = GetTypeSize(moduleName, typeName, out size)))
                ThrowExceptionHere(hr);
            return size;
        }

		/// <summary>
		/// Gets the size of a type
		/// </summary>
		/// <param name="moduleName">Name of the module that contains the type</param>
		/// <param name="typeName">Name of the type that contains the field</param>
		/// <param name="size">UInt32 to receive the size</param>
		/// <returns>HRESULT</returns>
		public int GetTypeSize(string moduleName, string typeName, out UInt32 size)
		{
			int hr;

            moduleName = FixModuleName(moduleName);

            if (Cache.GetTypeSize.TryGetValue(moduleName + "!" + typeName, out size))
            {
                return S_OK;
            }

			UInt64 moduleBase;
			hr = GetModuleBase(moduleName, out moduleBase);
			if (FAILED(hr))
			{
				size = 0;
				return hr;
			}

			UInt32 typeId;
            hr = GetTypeId(moduleName, typeName, out typeId);
			if (FAILED(hr))
			{
				size = 0;
				return hr;
			}

            
            hr = DebugSymbols.GetTypeSize(moduleBase, typeId, out size);
            if (SUCCEEDED(hr))
            {
                Cache.GetTypeSize.Add(moduleName + "!" + typeName, size);
            }
            return hr;

		}

        /// <summary>
        /// Gets the size of a type
        /// </summary>
        /// <param name="moduleName">Name of the module that contains the type</param>
        /// <param name="typeName">Name of the type that contains the field</param>
        /// <param name="size">UInt32 to receive the size</param>
        /// <returns>HRESULT</returns>
        public int GetTypeSize(ulong moduleBase, uint typeId, out UInt32 size)
        {
            int hr;

            string CacheName = moduleBase.ToString("x") + "!" + typeId.ToString("x");

            if (Cache.GetTypeSize.TryGetValue(CacheName, out size))
            {
                return S_OK;
            }

            hr = DebugSymbols.GetTypeSize(moduleBase, typeId, out size);
            if (SUCCEEDED(hr))
            {
                Cache.GetTypeSize.Add(CacheName, size);
            }
            return hr;

        }

		/// <summary>
		/// Reads an enum from a structure and returns the value as a string.
		/// If the function is unable to map the value to a name the output contains a hex representation of the value.
		/// NOTE! Success is returned if we are able to read the value but not perform a lookup.
		/// </summary>
		/// <param name="moduleName">Name of the module that contains the type</param>
		/// <param name="structureTypeName">Name of the type that contains the field</param>
		/// <param name="structureFieldName">Name of the field</param>
		/// <param name="structureAddress">Address of the structure</param>
		/// <param name="enumTypeName">Name of the type of the enum</param>
		/// <param name="valueAsText">The name of the enum, or a text representation of the </param>
		/// <returns>HRESULT</returns>
		public int ReadEnum32FromStructure(string moduleName, string structureTypeName, string structureFieldName, UInt64 structureAddress, string enumTypeName, out string valueAsText)
		{
			int hr;
			uint fieldOffset;

            moduleName = FixModuleName(moduleName);

            if (Cache.ReadEnum32FromStructure.TryGetValue(moduleName + "!" + structureTypeName + "." + structureFieldName + "." + enumTypeName + "@" + structureAddress.ToString("x"), out valueAsText))
            {
                return S_OK;
            }

			if (FAILED(hr = GetFieldOffset(moduleName, structureTypeName, structureFieldName, out fieldOffset)))
			{
				valueAsText = "FIELD_OFFSET_ERROR!";
				return hr;
			}
			UInt32 valueAsInt;
			hr = ReadVirtual32(structureAddress + fieldOffset, out valueAsInt);
			if (FAILED(hr))
			{
				valueAsText = "READ_FAILURE!";
				return hr;
			}
			hr = GetEnumName(moduleName, enumTypeName, valueAsInt, out valueAsText);
			if (FAILED(hr) || (valueAsText == null) || (valueAsText.Length == 0))
			{
				valueAsText = valueAsInt.ToString("x8", CultureInfo.InvariantCulture);
			}

            Cache.ReadEnum32FromStructure.Add(moduleName + "!" + structureTypeName + "." + structureFieldName + "." + enumTypeName + "@" + structureAddress.ToString("x"), valueAsText);
			return S_OK;
		}

		/// <summary>
		/// Wraps the core function, blocking all output
		/// </summary>
		public int ReadEnum32FromStructure_Silent(string moduleName, string structureTypeName, string structureFieldName, UInt64 structureAddress, string enumTypeName, out string valueAsText)
		{

            moduleName = FixModuleName(moduleName);

			using (var wrapper = InstallIgnoreFilter_WRAP_WITH_USING())
			{
				return ReadEnum32FromStructure(moduleName, structureTypeName, structureFieldName, structureAddress, enumTypeName, out valueAsText);
			}
		}

		/// <summary>
		/// Takes the type and value of an enum and tries to get the symbolic name for the value.
		/// </summary>
		public int GetEnumName(string moduleName, string typeName, ulong enumValue, out string enumName)
		{
			int hr;

            moduleName = FixModuleName(moduleName);

            if (Cache.GetEnumName.TryGetValue(moduleName + "!" + typeName + ":" + enumValue.ToString("x"), out enumName))
            {
                return S_OK;
            }

			UInt64 moduleBase;
            //We can get this information from the next call.  Should improve performance.
            //hr = GetModuleBase(moduleName, out moduleBase);
            //if (FAILED(hr))
            //{
            //    enumName = "";
            //    return hr;
            //}

            UInt32 typeId;
            hr = GetSymbolTypeIdWide(moduleName + "!" + typeName, out typeId, out moduleBase);

			if (FAILED(hr))
			{
				enumName = "";
				return hr;
			}
			uint nameSize = 0;
			StringBuilder sb = new StringBuilder(1024);
			hr = DebugSymbols.GetConstantNameWide(moduleBase, typeId, enumValue, sb, sb.Capacity, &nameSize);
			enumName = SUCCEEDED(hr) ? sb.ToString() : "";

            if (SUCCEEDED(hr))
            {
                Cache.GetEnumName.Add(moduleName + "!" + typeName + ":" + enumValue.ToString("x"), enumName);
            }

			return hr;
		}


		private Dictionary<string, UInt32> KnownRegisterIndexes = new Dictionary<string, UInt32>();

		/// <summary>
		/// Gets the debugger index of a register
		/// </summary>
		/// <param name="registerName">Name of the register (case sensitive, normally lower-case)</param>
		/// <param name="index">UInt32 to receive the index</param>
		/// <returns>HRESULT</returns>
		public int GetRegisterIndex(string registerName, out UInt32 index)
		{
			if (KnownRegisterIndexes.TryGetValue(registerName, out index))
			{
				return S_OK;
			}

			int hr = DebugRegisters.GetIndexByNameWide(registerName, out index);
			if (SUCCEEDED(hr))
			{
				KnownRegisterIndexes.Add(registerName, index);
			}
			return hr;
		}

		/// <summary>
		/// Gets the value of a register
		/// WARNING!!! VALUE MUST BE SIGN EXTENDED ON 32-BIT SYSTEMS!!!
		/// </summary>
		/// <param name="registerName">Name of the register (case sensitive, normally lower-case)</param>
		/// <param name="value">DEBUG_VALUE struct to receive the value</param>
		/// <returns>HRESULT</returns>
		public int GetRegisterValue(string registerName, out DEBUG_VALUE value)
		{
			int hr;

			UInt32 registerIndex;
			hr = GetRegisterIndex(registerName, out registerIndex);
			if (FAILED(hr))
			{
				value = default(DEBUG_VALUE);
				return hr;
			}

			return DebugRegisters.GetValue(registerIndex, out value);
		}

        


        /// <summary>
        /// Gets the value of a register
        /// WARNING!!! VALUE MUST BE SIGN EXTENDED ON 32-BIT SYSTEMS!!!
        /// </summary>
        /// <param name="registerName">Name of the register (case sensitive, normally lower-case)</param>
        /// <param name="FrameNum">Frame Number/param>
        /// <param name="value">DEBUG_VALUE struct to receive the value</param>
        /// <returns>HRESULT</returns>
        public int GetRegisterValueFromFrameContext(string registerName, uint FrameNum, out DEBUG_VALUE value)
        {
            int hr;

            uint currentframe;
            ExpressionToUInt32("@$frame", out currentframe);
            RunCommandSilent(String.Format(".frame 0x{0:x}", FrameNum));
            hr = GetRegisterValueFromFrameContext(registerName, out value);
            RunCommandSilent(String.Format(".frame 0x{0:x}", currentframe));

            return hr;
        }


        /// <summary>
        /// Gets the value of a register
        /// WARNING!!! VALUE MUST BE SIGN EXTENDED ON 32-BIT SYSTEMS!!!
        /// </summary>
        /// <param name="registerName">Name of the register (case sensitive, normally lower-case)</param>
        /// <param name="value">DEBUG_VALUE struct to receive the value</param>
        /// <returns>HRESULT</returns>
        public int GetRegisterValueFromFrameContext(string registerName, out DEBUG_VALUE value)
        {
            int hr;

            UInt32 registerIndex;
            hr = GetRegisterIndex(registerName, out registerIndex);
            if (FAILED(hr))
            {
                value = default(DEBUG_VALUE);
                return hr;
            }

            uint[] indexArray = new uint[1];
            DEBUG_VALUE[] debugValues = new DEBUG_VALUE[1];

            indexArray[0] = registerIndex;

            hr = DebugRegisters.GetValues2((uint) DEBUG_REGSRC.FRAME, 1, indexArray, 0, debugValues);

            value = debugValues[0];

            return hr;
        }

        /// <summary>
        /// Gets the value of a register
        /// WARNING!!! VALUE MUST BE SIGN EXTENDED ON 32-BIT SYSTEMS!!!
        /// </summary>
        /// <param name="registerIndex">Index of the register</param>
        /// <param name="value">DEBUG_VALUE struct to receive the value</param>
        /// <returns>HRESULT</returns>
        public int GetRegisterValueFromFrameContext(uint registerIndex, out DEBUG_VALUE value)
        {
            int hr;

            uint[] indexArray = new uint[1];
            DEBUG_VALUE[] debugValues = new DEBUG_VALUE[1];

            indexArray[0] = registerIndex;

            hr = DebugRegisters.GetValues2((uint)DEBUG_REGSRC.FRAME, 1, indexArray, 0, debugValues);

            value = debugValues[0];

            return hr;
        }


		/// <summary>
		/// Gets the value of a register
		/// WARNING!!! VALUE MUST BE SIGN EXTENDED ON 32-BIT SYSTEMS!!!
		/// </summary>
		/// <param name="registerIndex">Index of the register</param>
		/// <param name="value">DEBUG_VALUE struct to receive the value</param>
		/// <returns>HRESULT</returns>
		public int GetRegisterValue(uint registerIndex, out DEBUG_VALUE value)
		{
			return DebugRegisters.GetValue(registerIndex, out value);
		}

		/// <summary>
		/// Gets the number of registers in the system
		/// </summary>
		/// <param name="numberOfRegisters">Number of registers in the system</param>
		/// <returns>HRESULT</returns>
		public int GetRegisterCount(out uint numberOfRegisters)
		{
			return DebugRegisters.GetNumberRegisters(out numberOfRegisters);
		}

		/// <summary>
		/// Gets the value of a register and sign extends for 32-bit target
		/// </summary>
		/// <param name="registerName">Name of the register (case sensitive, normally lower-case)</param>
		/// <param name="value">DEBUG_VALUE struct to receive the value</param>
		/// <returns>HRESULT</returns>
		public int GetSignExtendedRegisterValue(string registerName, out UInt64 value)
		{
			int hr;
			DEBUG_VALUE debugValue;

			UInt32 registerIndex;
			if (FAILED(hr = GetRegisterIndex(registerName, out registerIndex)) || FAILED(hr = DebugRegisters.GetValue(registerIndex, out debugValue)))
			{
				value = 0UL;
				return hr;
			}

			value = IsPointer64Bit() ? debugValue.I64 : SignExtendAddress(debugValue.I64);
			return hr;
		}

		/// <summary>
		/// Retrieves the address of a global variable.
		/// </summary>
		/// <param name="moduleName">Name of the module the global resides in</param>
		/// <param name="globalName">Name of the global</param>
		/// <param name="address">UInt64 to receive the address</param>
		/// <returns>HRESULT</returns>
		public int GetGlobalAddress(string moduleName, string globalName, out UInt64 address)
		{

            moduleName = FixModuleName(moduleName);

            if (Cache.GetGlobalAddress.TryGetValue(moduleName + "!" + globalName, out address))
            {
                return S_OK;
            }
			
            UInt64 tempAddress;
			int hr = DebugSymbols.GetOffsetByNameWide(moduleName + "!" + globalName, out tempAddress);
			if (SUCCEEDED(hr))
			{
				if (tempAddress != 0)
				{
					address = IsPointer64Bit() ? tempAddress : SignExtendAddress(tempAddress);
                    Cache.GetGlobalAddress.Add(moduleName + "!" + globalName, address);
				}
				else
				{
					address = 0;
					OutputWarningLine("IDebugSymbols::GetOffsetByName() for {0}!{1} returned success ({2:x8}) but returned an address of 0. Overriding return status to E_FAIL.", moduleName, globalName, hr);
					hr = E_FAIL;
				}
			}
			else
			{
				address = 0;
			}
			return hr;
		}

        /// <summary>
        /// Breaks symbol name into module & variable with some error checking
        /// </summary>
        /// <param name="symbolName"></param>
        /// <returns>string[0] = module; string[1] = variable</returns>
        private string[] BreakSymbolName(string symbolName)
        {
            var parts = symbolName.Split(new char[] { '!' });

            if (parts.Length != 2)
                throw new Exception("Error processing symbolName " + symbolName + ": " + parts.Length + " found when we expected 2.");
            else
                return parts;
        }

        /// <summary>
        /// Retrieves the address of a global variable (exception throwing variety).
        /// </summary>
        /// <param name="symbolName">Name of the global, e.g. "nt!MmMaximumNonPagedPoolInBytes"</param>
        /// <returns>Address of structure</returns>
        public UInt64 GetGlobalAddress(string symbolName)
        {
            UInt64 addr = 0;

            var parts = BreakSymbolName(symbolName);

            int hr = GetGlobalAddress(parts[0], parts[1], out addr);

            if (SUCCEEDED(hr))
                return addr;
            else
            {
                ThrowExceptionHere(hr);
                return 0;
            }
        }

		/// <summary>
		/// Retrieves the value of a global pointer-sized variable.
		/// </summary>
		/// <param name="moduleName">Name of the module the global resides in</param>
		/// <param name="globalName">Name of the global</param>
		/// <param name="value">UInt64 to receive the value of the global</param>
		/// <returns>HRESULT</returns>
		public int ReadGlobalAsPointer(string moduleName, string globalName, out UInt64 value)
		{
			int hr;
			UInt64 temp;
			if (FAILED(hr = GetGlobalAddress(moduleName, globalName, out temp)) || FAILED(hr = ReadPointer(temp, out temp)))
			{
				value = 0;
				return hr;
			}
			value = temp;
			return hr;
		}

		/// <summary>
		/// Retrieves the value of a global pointer-sized variable.
		/// </summary>
		/// <param name="moduleName">Name of the module the global resides in</param>
		/// <param name="globalName">Name of the global</param>
		/// <param name="value">UInt64 to receive the value of the global</param>
		/// <returns>HRESULT</returns>
		public int ReadGlobalAsNativeUInt(string moduleName, string globalName, out UInt64 value)
		{
			int hr;
			UInt64 temp;
			if (FAILED(hr = GetGlobalAddress(moduleName, globalName, out temp)) || FAILED(hr = ReadNativeUInt(temp, out temp)))
			{
				value = 0;
				return hr;
			}
			value = temp;
			return hr;
		}

        /// <summary>
        /// Retrieves the value of a global pointer-sized variable.
        /// </summary>
        /// <param name="symbolName">Name of the symbol, e.g. nt!MmMaximumNonPagedPoolInBytes</param>
        /// <param name="globalName">Name of the global</param>
        /// <param name="value">UInt64 to receive the value of the global</param>
        /// <returns>UInt64 value</returns>
        public UInt64 ReadGlobalAsNativeUInt(string symbolName)
        {
            int hr;
            UInt64 temp;

            var parts = BreakSymbolName(symbolName);

            if (FAILED(hr = GetGlobalAddress(parts[0], parts[1], out temp)) || FAILED(hr = ReadNativeUInt(temp, out temp)))
            {
                ThrowExceptionHere(hr);
                return 0;
            }
            return temp;
        }

		/// <summary>
		/// Retrieves the value of a global variable.
		/// </summary>
		/// <param name="moduleName">Name of the module the global resides in</param>
		/// <param name="globalName">Name of the global</param>
		/// <param name="value">SByte to receive the value of the global</param>
		/// <returns>HRESULT</returns>
		public int ReadGlobalAsInt8(string moduleName, string globalName, out SByte value)
		{
			int hr;
			UInt64 tempAddress;
			SByte temp;
			if (FAILED(hr = GetGlobalAddress(moduleName, globalName, out tempAddress)) || FAILED(hr = ReadVirtual8(tempAddress, out temp)))
			{
				value = 0;
				return hr;
			}
			value = temp;
			return hr;
		}

		/// <summary>
		/// Retrieves the value of a global variable.
		/// </summary>
		/// <param name="moduleName">Name of the module the global resides in</param>
		/// <param name="globalName">Name of the global</param>
		/// <param name="value">Byte to receive the value of the global</param>
		/// <returns>HRESULT</returns>
		public int ReadGlobalAsUInt16(string moduleName, string globalName, out Byte value)
		{
			int hr;
			UInt64 tempAddress;
			Byte temp;
			if (FAILED(hr = GetGlobalAddress(moduleName, globalName, out tempAddress)) || FAILED(hr = ReadVirtual8(tempAddress, out temp)))
			{
				value = 0;
				return hr;
			}
			value = temp;
			return hr;
		}

		/// <summary>
		/// Retrieves the value of a global variable.
		/// </summary>
		/// <param name="moduleName">Name of the module the global resides in</param>
		/// <param name="globalName">Name of the global</param>
		/// <param name="value">Int16 to receive the value of the global</param>
		/// <returns>HRESULT</returns>
		public int ReadGlobalAsInt16(string moduleName, string globalName, out Int16 value)
		{
			int hr;
			UInt64 tempAddress;
			Int16 temp;
			if (FAILED(hr = GetGlobalAddress(moduleName, globalName, out tempAddress)) || FAILED(hr = ReadVirtual16(tempAddress, out temp)))
			{
				value = 0;
				return hr;
			}
			value = temp;
			return hr;
		}

		/// <summary>
		/// Retrieves the value of a global variable.
		/// </summary>
		/// <param name="moduleName">Name of the module the global resides in</param>
		/// <param name="globalName">Name of the global</param>
		/// <param name="value">UInt16 to receive the value of the global</param>
		/// <returns>HRESULT</returns>
		public int ReadGlobalAsUInt16(string moduleName, string globalName, out UInt16 value)
		{
			int hr;
			UInt64 tempAddress;
			UInt16 temp;
			if (FAILED(hr = GetGlobalAddress(moduleName, globalName, out tempAddress)) || FAILED(hr = ReadVirtual16(tempAddress, out temp)))
			{
				value = 0;
				return hr;
			}
			value = temp;
			return hr;
		}

		/// <summary>
		/// Retrieves the value of a global variable.
		/// </summary>
		/// <param name="moduleName">Name of the module the global resides in</param>
		/// <param name="globalName">Name of the global</param>
		/// <param name="value">Int32 to receive the value of the global</param>
		/// <returns>HRESULT</returns>
		public int ReadGlobalAsInt32(string moduleName, string globalName, out Int32 value)
		{
			int hr;
			UInt64 tempAddress;
			Int32 temp;
			if (FAILED(hr = GetGlobalAddress(moduleName, globalName, out tempAddress)) || FAILED(hr = ReadVirtual32(tempAddress, out temp)))
			{
				value = 0;
				return hr;
			}
			value = temp;
			return hr;
		}


        /// <summary>
        /// Retrieves the value of a global variable.
        /// </summary>
        /// <param name="moduleName">Name of the module the global resides in</param>
        /// <param name="globalName">Name of the global</param>
        /// <param name="value">byte to receive the value of the global</param>
        /// <returns>HRESULT</returns>
        public int ReadGlobalAsByte(string moduleName, string globalName, out byte value)
        {
            int hr;
            UInt64 tempAddress;
            if (FAILED(hr = GetGlobalAddress(moduleName, globalName, out tempAddress)) || FAILED(hr = ReadVirtual8(tempAddress, out value)))
            {
                value = 0;
                return hr;
            }
            return hr;
        }

        /// <summary>
        /// Retrieves the value of a global variable.
        /// </summary>
        /// <param name="moduleName">Name of the module the global resides in</param>
        /// <param name="globalName">Name of the global</param>
        /// <returns>Value of the global</returns>
        public byte ReadGlobalAsByte(string moduleName, string globalName)
        {
            int hr;
            UInt64 tempAddress;
            byte tempVal;
            if (FAILED(hr = GetGlobalAddress(moduleName, globalName, out tempAddress)))
                ThrowExceptionHere(hr);

            if (FAILED(hr = ReadVirtual8(tempAddress, out tempVal)))
                ThrowExceptionHere(hr);

            return tempVal;
        }


        /// <summary>
        /// Throws a new exception with the name of the parent function and value of the HR
        /// </summary>
        /// <param name="hr">Error # to include</param>
        internal void ThrowExceptionHere(int hr)
        {
            StackTrace stackTrace = new StackTrace();
            System.Diagnostics.StackFrame stackFrame = stackTrace.GetFrame(1);
            throw new Exception(String.Format("Error in {0}: {1}", stackFrame.GetMethod().Name, hr));
        }



		/// <summary>
		/// Retrieves the value of a global variable.
		/// </summary>
		/// <param name="moduleName">Name of the module the global resides in</param>
		/// <param name="globalName">Name of the global</param>
		/// <param name="value">UInt32 to receive the value of the global</param>
		/// <returns>HRESULT</returns>
		public int ReadGlobalAsUInt32(string moduleName, string globalName, out UInt32 value)
		{
			int hr;
			UInt64 tempAddress;
			UInt32 temp;
			if (FAILED(hr = GetGlobalAddress(moduleName, globalName, out tempAddress)) || FAILED(hr = ReadVirtual32(tempAddress, out temp)))
			{
				value = 0;
				return hr;
			}
			value = temp;
			return hr;
		}

		/// <summary>
		/// Retrieves the value of a global variable.
		/// </summary>
		/// <param name="moduleName">Name of the module the global resides in</param>
		/// <param name="globalName">Name of the global</param>
		/// <param name="value">Int64 to receive the value of the global</param>
		/// <returns>HRESULT</returns>
		public int ReadGlobalAsInt64(string moduleName, string globalName, out Int64 value)
		{
			int hr;
			UInt64 tempAddress;
			Int64 temp;
			if (FAILED(hr = GetGlobalAddress(moduleName, globalName, out tempAddress)) || FAILED(hr = ReadVirtual64(tempAddress, out temp)))
			{
				value = 0;
				return hr;
			}
			value = temp;
			return hr;
		}

		/// <summary>
		/// Retrieves the value of a global variable.
		/// </summary>
		/// <param name="moduleName">Name of the module the global resides in</param>
		/// <param name="globalName">Name of the global</param>
		/// <param name="value">UInt64 to receive the value of the global</param>
		/// <returns>HRESULT</returns>
		public int ReadGlobalAsUInt64(string moduleName, string globalName, out UInt64 value)
		{
			int hr;
			UInt64 temp;
			if (FAILED(hr = GetGlobalAddress(moduleName, globalName, out temp)) || FAILED(hr = ReadVirtual64(temp, out temp)))
			{
				value = 0;
				return hr;
			}
			value = temp;
			return hr;
		}

		/// <summary>
		/// Retrieves the value of a global variable.
		/// NOTE: This is the 4-byte Windows BOOL, not the 1-byte C/C++ bool
		/// </summary>
		/// <param name="moduleName">Name of the module the global resides in</param>
		/// <param name="globalName">Name of the global</param>
		/// <param name="value">UInt32 to receive the value of the global</param>
		/// <returns>HRESULT</returns>
		public int ReadGlobalAsBOOL(string moduleName, string globalName, out bool value)
		{
			int hr;
			UInt64 tempAddress;
			UInt32 temp;
			if (FAILED(hr = GetGlobalAddress(moduleName, globalName, out tempAddress)) || FAILED(hr = ReadVirtual32(tempAddress, out temp)))
			{
				value = false;
				return hr;
			}
			value = (temp != 0);
			return hr;
		}

		/// <summary>
		/// Retrieves the value of a global variable.
		/// /// NOTE: This is the 1-byte C/C++ bool, not the 4-byte Windows BOOL
		/// </summary>
		/// <param name="moduleName">Name of the module the global resides in</param>
		/// <param name="globalName">Name of the global</param>
		/// <param name="value">UInt32 to receive the value of the global</param>
		/// <returns>HRESULT</returns>
		public int ReadGlobalAsBool(string moduleName, string globalName, out bool value)
		{
			int hr;
			UInt64 tempAddress;
			Byte temp;
			if (FAILED(hr = GetGlobalAddress(moduleName, globalName, out tempAddress)) || FAILED(hr = ReadVirtual8(tempAddress, out temp)))
			{
				value = false;
				return hr;
			}
			value = (temp != 0);
			return hr;
		}

		private StringBuilder LookupSymbolStringBuilder = new StringBuilder(2048);
		/// <summary>
		/// Looks up a symbol for an address.
		/// </summary>
		/// <param name="offset">Address of the symbol to look up.</param>
		/// <returns>String or NULL if an error occurs</returns>
		public unsafe string LookupSymbol(UInt64 offset)
		{
			lock (LookupSymbolStringBuilder)
			{
				UInt64 displacement;
				LookupSymbolStringBuilder.Length = 0;
				int hr = DebugSymbols.GetNameByOffsetWide(offset, LookupSymbolStringBuilder, LookupSymbolStringBuilder.Capacity, null, &displacement);
				if (hr < 0)
				{
					return null;
				}
				if (displacement != 0)
				{
					LookupSymbolStringBuilder.Append("+0x");
					LookupSymbolStringBuilder.Append(displacement.ToString("x", CultureInfo.InvariantCulture.NumberFormat));
				}
				return LookupSymbolStringBuilder.ToString();
			}
		}

        /// <summary>
        /// Looks up a symbol for an address.
        /// </summary>
        /// <param name="offset">Address of the symbol to look up.</param>
        /// <param name="displacement">displacement from symbol name.</param>       
        /// <returns>String or NULL if an error occurs</returns>
        public unsafe string LookupSymbol(UInt64 offset, out uint Displacement)
        {
            lock (LookupSymbolStringBuilder)
            {
                UInt64 displacement;
                LookupSymbolStringBuilder.Length = 0;
                int hr = DebugSymbols.GetNameByOffsetWide(offset, LookupSymbolStringBuilder, LookupSymbolStringBuilder.Capacity, null, &displacement);
                if (hr < 0)
                {
                    Displacement = 0;
                    return null;
                }
                
                Displacement = (uint)displacement;
                return LookupSymbolStringBuilder.ToString();
            }
        }

		/// <summary>
		/// Looks up a symbol for an address, but only returns the symbol if there is the address is an exact match.
		/// </summary>
		/// <param name="offset">Address of the symbol to look up.</param>
		/// <returns>String or NULL if an error occurs</returns>
		public unsafe string LookupSymbolExactOnly(UInt64 offset)
		{
			lock (LookupSymbolStringBuilder)
			{
				UInt64 displacement;
				LookupSymbolStringBuilder.Length = 0;
				int hr = DebugSymbols.GetNameByOffsetWide(offset, LookupSymbolStringBuilder, LookupSymbolStringBuilder.Capacity, null, &displacement);
				if ((hr < 0) || (displacement != 0))
				{
					return null;
				}
				return LookupSymbolStringBuilder.ToString();
			}
		}

		/// <summary>
		/// Gets the TEB for the current thread. In a WOW64 process the 32-bit TEB will be returned.
		/// </summary>
		/// <param name="teb">UInt64 to receive the teb address</param>
		/// <param name="tebType">The type of TEB structure returned. Normally _TEB, but can be _TEB32 if inside of WOW</param>
		/// <returns>HRESULT</returns>
		public int GetTeb(out UInt64 teb, out string tebType)
		{
			string ntTibType;
			return GetTeb(out teb, out tebType, out ntTibType);
		}

		/// <summary>
		/// Gets the TEB for the current thread. In a WOW64 process the 32-bit TEB will be returned.
		/// </summary>
		/// <param name="teb">UInt64 to receive the teb address</param>
		/// <param name="tebType">The type of TEB structure returned. Normally _TEB, but can be _TEB32 if inside of WOW</param>
		/// <param name="ntTibType">The type of the _NT_TIB contained in the TEB. Normally _NT_TIB, but can be _NT_TIB32 if inside of WOW</param>
		/// <returns>HRESULT</returns>
		public int GetTeb(out UInt64 teb, out string tebType, out string ntTibType)
		{
			int hr;
			UInt64 tebAddress;

			/*
				This seems to be a bit buggy in kernel and can succeed but return a TEB of 0
				int hr = debugSystemObjects.GetCurrentThreadTeb(out teb);
			*/

			hr = ExpressionToPointer("@$teb", out tebAddress);
			if (FAILED(hr))
			{
				teb = 0;
				tebType = "";
				ntTibType = "";
				return hr;
			}

			bool wowPresent = WowPresent();
			if (wowPresent)
			{
				hr = ReadPointer(tebAddress, out teb);
				if (FAILED(hr))
				{
					teb = 0;
					tebType = "";
					ntTibType = "";
					return hr;
				}
				else
				{
					tebType = "_TEB32";
					ntTibType = "_NT_TIB32";
				}
			}
			else
			{
				teb = tebAddress;
				tebType = "_TEB";
				ntTibType = "_NT_TIB";
			}

			return hr;
		}

		/// <summary>
		/// Gets the TEB when debugging usermode, or ETHREAD in kernelmode.
		/// 
		/// NOTE: If Wow is loaded the TEB returned for usermode will be the application TEB, not the external WOW TEB.
		/// </summary>
		/// <param name="threadData">A variable to receive the TEB (Usermode) or ETHREAD (Kernelmode)</param>
		/// <returns>HRESULT</returns>
		public int GetTebOrEThread(out UInt64 threadData)
		{
			int hr;
			UInt64 dataAddress;

			hr = ExpressionToPointer("@$thread", out dataAddress);
			if (FAILED(hr))
			{
				threadData = 0;
				return hr;
			}

			if (IsAddressUserMode(dataAddress) && WowPresent())
			{
				/* If usermode must be a TEB, do Wow translation */

				hr = ReadPointer(dataAddress, out threadData);
				if (FAILED(hr))
				{
					threadData = 0;
					return hr;
				}
			}
			else
			{
				threadData = dataAddress;
			}

			return hr;
		}

		/// <summary>
		/// Determines if the process is using a WOW64 environment.
		/// </summary>
		/// <returns>True if WOW, False otherwise or if an error occurs.</returns>
		public bool WowPresent()
		{
			UInt64 peb32Address;
			return SUCCEEDED(GetGlobalAddress("wow64", "Peb32", out peb32Address));
		}

        /// <summary>
        ///     Trims prefixed "class" or "struct" from string returned from ReadTypeNameFromStructureMember
        /// </summary>
        /// <param name="moduleName">Name of the module that contains the type</param>
        /// <param name="typeName">Name of the type that contains the field</param>
        /// <param name="fieldName">Name of the field</param>
        /// <param name="structureAddress">Address of the structure</param>
        /// <param name="memberTypeName">The type name read from symbols</param>
        /// <returns>HRESULT</returns>
        public int ReadTypeNameFromStructureMemberClean(string moduleName, string typeName, string fieldName, ulong structureAddress, out string memberTypeName)
        {
            
            int hr = ReadTypeNameFromStructureMember(moduleName, typeName, fieldName, structureAddress, out memberTypeName);
            if (memberTypeName.StartsWith("class "))
            {
                memberTypeName = memberTypeName.Substring("class ".Length);
            }
            else if (memberTypeName.StartsWith("struct "))
            {
                memberTypeName = memberTypeName.Substring("struct ".Length);
            }
            else if (memberTypeName.StartsWith("union "))
            {
                memberTypeName = memberTypeName.Substring("union ".Length);
            }

            return hr;
        }


        //
        /// <summary>
        ///     Gets the type information from a structure member
        /// </summary>
        /// <param name="moduleName">Name of the module that contains the type</param>
        /// <param name="typeName">Name of the type that contains the field</param>
        /// <param name="fieldName">Name of the field</param>
        /// <param name="structureAddress">Address of the structure</param>
        /// <param name="memberTypeName">The type name read from symbols</param>
        /// <returns>HRESULT</returns>
        public int ReadTypeNameFromStructureMember(string moduleName, string typeName, string fieldName, ulong structureAddress, out string memberTypeName)
        {

            moduleName = FixModuleName(moduleName);
            
            const int MAX_TYPE_SIZE = 512;  // even most templated types shouldn't be 512 chars
            memberTypeName = "";

            // Make a new Typed Data structure.
            _EXT_TYPED_DATA SymbolTypedData;

            // Get the ModuleBase
            ulong ModuleBase;
            GetModuleBase(moduleName, out ModuleBase);

            // Get the TypeID for the parent structure
            uint TypeId;
            GetTypeId(moduleName, typeName, out TypeId);

            // get the field offset and typeid for the member
            uint FieldTypeId;
            uint offset;
            int hr;
            try
            {
                hr = DebugSymbols.GetFieldTypeAndOffsetWide(ModuleBase, TypeId, fieldName, &FieldTypeId, &offset);
                if (FAILED(hr))
                {
                    OutputVerboseLine("GetFieldTypeAndOffset Failed:  {0:x}", hr);
                    return hr;
                }
            }
            catch
            {
                OutputErrorLine("[ReadTypeNameFromStructureMember] ERROR: IDebugSymbols.GetFieldTypeAndOffset threw an exception.  Your Debugger is probably out of date and does not support the IDebugSymbols5 interface");
                return S_FALSE;
            }

            // Set up the first operation to get symbol data
            SymbolTypedData.Operation = _EXT_TDOP.EXT_TDOP_SET_FROM_TYPE_ID_AND_U64;
            SymbolTypedData.InData.ModBase = ModuleBase;
            SymbolTypedData.InData.Offset = structureAddress;
            SymbolTypedData.InData.TypeId = FieldTypeId;

            //d.OutputVerboseLine("FieldTypeId:{0:x}  ModuleBase:{1:x}   Offset:{2:x}", FieldTypeId, ModuleBase, offset);


            if (FAILED(hr = DebugAdvanced.Request(DEBUG_REQUEST.EXT_TYPED_DATA_ANSI, &SymbolTypedData, sizeof(_EXT_TYPED_DATA), &SymbolTypedData, sizeof(_EXT_TYPED_DATA), null)))
            {
                OutputVerboseLine("[ReadTypeNameFromStructureMember] DebugAdvanced.Request 1 Failed to get {1}!{2}.{3} hr={0:x}", hr, moduleName, typeName, fieldName);
                return hr;
            }


            IntPtr Buffer = IntPtr.Zero;

            try
            {

                _EXT_TYPED_DATA TemporaryTypedDataForBufferConstruction;

                // Allocate Buffers
                int TotalSize = sizeof(_EXT_TYPED_DATA) + MAX_TYPE_SIZE;
                Buffer = Marshal.AllocHGlobal((int)TotalSize);

                // Set up the parameters for the 2nd request call to get the type
                TemporaryTypedDataForBufferConstruction.Operation = _EXT_TDOP.EXT_TDOP_GET_TYPE_NAME;

                // Pass in the OutData from the first call to Request(), so it knows what symbol to use
                TemporaryTypedDataForBufferConstruction.InData = SymbolTypedData.OutData;

                // The index of the string will be immediatly following the _EXT_TYPED_DATA structure
                TemporaryTypedDataForBufferConstruction.StrBufferIndex = (uint)sizeof(_EXT_TYPED_DATA);
                TemporaryTypedDataForBufferConstruction.StrBufferChars = MAX_TYPE_SIZE;


                // I suck at moving buffers around in C#.. but this seems to work :)
                // Copy TemporaryTypedDataForBufferConstruction into the Buffer.

                // Source is our _EXT_TYPED_DATA stucture, Dest is our empty allocated buffer



                DebugUtilities.CopyMemory(Buffer, (IntPtr)(&TemporaryTypedDataForBufferConstruction), sizeof(_EXT_TYPED_DATA));
         
                // Call Request(), Passing in the buffer we created as the In and Out Parameters
                if (FAILED(hr = DebugAdvanced.Request(DEBUG_REQUEST.EXT_TYPED_DATA_ANSI, (void*)Buffer, TotalSize, (void*)Buffer, TotalSize, null)))
                {
                    OutputVerboseLine("[ReadTypeNameFromStructureMember]DebugAdvanced.Request 2 Failed to get {1}!{2}.{3} hr={0:x}", hr, moduleName, typeName, fieldName);
                    return hr;
                }

                EXT_TYPED_DATA TypedDataInClassForm = new EXT_TYPED_DATA();
                // Convert the returned buffer to a _EXT_TYPED_Data _CLASS_ (since it wont let me convert to a struct)
                Marshal.PtrToStructure(Buffer, TypedDataInClassForm);

                memberTypeName = Marshal.PtrToStringAnsi((IntPtr)(Buffer.ToInt64() + TypedDataInClassForm.StrBufferIndex));

            }
            finally
            {
                Marshal.FreeHGlobal(Buffer);
            }

            return hr;
        }



        /// <summary>
        ///     Trims prefixed "class" or "struct" from string returned from ReadTypeNameFromStructureMember
        /// </summary>
        /// <param name="moduleName">Name of the module that contains the type</param>
        /// <param name="typeName">Name of the type that contains the field</param>
        /// <param name="fieldName">Name of the field</param>
        /// <param name="structureAddress">Address of the structure</param>
        /// <param name="memberTypeName">The type name read from symbols</param>
        /// <returns>HRESULT</returns>
        public int ReadTypeNameClean(string symbolName, ulong structureAddress, out string memberTypeName)
        {

            int hr = ReadTypeName(symbolName, structureAddress, out memberTypeName);
            if (memberTypeName.StartsWith("class "))
            {
                memberTypeName = memberTypeName.Substring("class ".Length);
            }
            else if (memberTypeName.StartsWith("struct "))
            {
                memberTypeName = memberTypeName.Substring("struct ".Length);
            }

            return hr;
        }


        //
        /// <summary>
        ///     Gets the type information
        /// </summary>
        /// <param name="moduleName">Name of the module that contains the type</param>
        /// <param name="typeName">Name of the type that contains the field</param>
        /// <param name="fieldName">Name of the field</param>
        /// <param name="structureAddress">Address of the structure</param>
        /// <param name="memberTypeName">The type name read from symbols</param>
        /// <returns>HRESULT</returns>
        public int ReadTypeName(string symbolName, ulong structureAddress, out string memberTypeName)
        {

            const int MAX_TYPE_SIZE = 512;  // even most templated types shouldn't be 512 chars
            memberTypeName = "";

            // Make a new Typed Data structure.
            _EXT_TYPED_DATA SymbolTypedData;

            // Get the ModuleBase
            ulong ModuleBase;

            // Get the TypeID for the parent structure
            uint TypeId;
            int hr = GetSymbolTypeIdWide(symbolName, out TypeId, out ModuleBase);

            if (FAILED(hr))
            {
                OutputVerboseLine("[ReadTypeName] GetSymbolTypeIdWide Failed to get {1} hr={0:x}", hr, symbolName);
                return hr;
            }

            // Set up the first operation to get symbol data
            SymbolTypedData.Operation = _EXT_TDOP.EXT_TDOP_SET_FROM_TYPE_ID_AND_U64;
            SymbolTypedData.InData.ModBase = ModuleBase;
            SymbolTypedData.InData.Offset = structureAddress;
            SymbolTypedData.InData.TypeId = TypeId;

            //d.OutputVerboseLine("FieldTypeId:{0:x}  ModuleBase:{1:x}   Offset:{2:x}", FieldTypeId, ModuleBase, offset);

            if (FAILED(hr = DebugAdvanced.Request(DEBUG_REQUEST.EXT_TYPED_DATA_ANSI, &SymbolTypedData, sizeof(_EXT_TYPED_DATA), &SymbolTypedData, sizeof(_EXT_TYPED_DATA), null)))
            {
                OutputVerboseLine("[ReadTypeName] DebugAdvanced.Request 1 Failed to get {1} hr={0:x}", hr,symbolName);
                return hr;
            }


            IntPtr Buffer = IntPtr.Zero;

            try
            {

                _EXT_TYPED_DATA TemporaryTypedDataForBufferConstruction;

                // Allocate Buffers
                int TotalSize = sizeof(_EXT_TYPED_DATA) + MAX_TYPE_SIZE;
                Buffer = Marshal.AllocHGlobal((int)TotalSize);

                // Set up the parameters for the 2nd request call to get the type
                TemporaryTypedDataForBufferConstruction.Operation = _EXT_TDOP.EXT_TDOP_GET_TYPE_NAME;

                // Pass in the OutData from the first call to Request(), so it knows what symbol to use
                TemporaryTypedDataForBufferConstruction.InData = SymbolTypedData.OutData;

                // The index of the string will be immediatly following the _EXT_TYPED_DATA structure
                TemporaryTypedDataForBufferConstruction.StrBufferIndex = (uint)sizeof(_EXT_TYPED_DATA);
                TemporaryTypedDataForBufferConstruction.StrBufferChars = MAX_TYPE_SIZE;


                // I suck at moving buffers around in C#.. but this seems to work :)
                // Copy TemporaryTypedDataForBufferConstruction into the Buffer.

                // Source is our _EXT_TYPED_DATA stuction, Dest is our empty allocated buffer

                DebugUtilities.CopyMemory(Buffer, (IntPtr)(&TemporaryTypedDataForBufferConstruction), sizeof(_EXT_TYPED_DATA));
             
                // Hack alert.
                // I had to make a new class called EXT_TYPED_DATA so i could call Marshal.PtrToStructure.. The struct wouldnt work.. so we have a struct and a class with the same(ish) fields.

                EXT_TYPED_DATA TypedDataInClassForm = new EXT_TYPED_DATA();

                // Call Request(), Passing in the buffer we created as the In and Out Parameters
                if (FAILED(hr = DebugAdvanced.Request(DEBUG_REQUEST.EXT_TYPED_DATA_ANSI, (void*)Buffer, TotalSize, (void*)Buffer, TotalSize, null)))
                {
                    OutputVerboseLine("[ReadTypeNameFromStructureMember]DebugAdvanced.Request 2 Failed to get {1} hr={0:x}", hr, symbolName);
                    return hr;
                }


                // Convert the returned buffer to a _EXT_TYPED_Data CLASS (since it wont let me convert to a struct)
                Marshal.PtrToStructure(Buffer, TypedDataInClassForm);

                memberTypeName = Marshal.PtrToStringAnsi((IntPtr)(Buffer.ToInt64() + TypedDataInClassForm.StrBufferIndex));

            }
            finally
            {
                Marshal.FreeHGlobal(Buffer);
            }

            return hr;
        }


		/// <summary>
		/// Reads a 8-bit value from a structure
		/// </summary>
		/// <param name="moduleName">Name of the module that contains the type</param>
		/// <param name="typeName">Name of the type that contains the field</param>
		/// <param name="fieldName">Name of the field</param>
		/// <param name="address">Address of the structure</param>
		/// <param name="value">The value read from memory</param>
		/// <returns>HRESULT</returns>
		public int ReadInt8FromStructure(string moduleName, string typeName, string fieldName, UInt64 address, out SByte value)
		{
			int hr;
			uint fieldOffset;
			if (FAILED(hr = GetFieldOffset(moduleName, typeName, fieldName, out fieldOffset)))
			{
				value = 0;
				return hr;
			}
			return ReadVirtual8(address + fieldOffset, out value);
		}


        /// <summary>
        /// Reads a 8-bit value from a structure
        /// </summary>
        /// <param name="moduleName">Name of the module that contains the type</param>
        /// <param name="typeName">Name of the type that contains the field</param>
        /// <param name="fieldName">Name of the field</param>
        /// <param name="address">Address of the structure</param>
        /// <param name="value">The value read from memory</param>
        /// <returns>HRESULT</returns>
        public int ReadInt8FromStructure(ulong moduleBase, uint typeId, string fieldName, UInt64 address, out SByte value)
        {
            int hr;
            ulong fieldAddress;
            if (FAILED(hr = GetFieldVirtualAddress(moduleBase, typeId, fieldName, address, out fieldAddress)))
            {
                value = 0;
                return hr;
            }
            return ReadVirtual8(fieldAddress, out value);
        }


		/// <summary>
		/// Wraps the core function, blocking all output
		/// </summary>
		public int ReadInt8FromStructure_Silent(string moduleName, string typeName, string fieldName, UInt64 address, out SByte value)
		{
			using (var wrapper = InstallIgnoreFilter_WRAP_WITH_USING())
			{
				return ReadInt8FromStructure(moduleName, typeName, fieldName, address, out value);
			}
		}



        /// <summary>
        /// Reads a 8-bit value from a structure
        /// </summary>
        /// <param name="moduleName">Name of the module that contains the type</param>
        /// <param name="typeName">Name of the type that contains the field</param>
        /// <param name="fieldName">Name of the field</param>
        /// <param name="address">Address of the structure</param>
        /// <param name="value">The value read from memory</param>
        /// <returns>HRESULT</returns>
        public int ReadUInt8FromStructure(string moduleName, string typeName, string fieldName, UInt64 address, out Byte value)
        {
            int hr;
            ulong fieldAddress;
            if (FAILED(hr = GetFieldVirtualAddress(moduleName, typeName, fieldName, address, out fieldAddress)))
            {
                value = 0;
                return hr;
            }
            return ReadVirtual8(fieldAddress, out value);
        }


		/// <summary>
		/// Reads a 8-bit value from a structure
		/// </summary>
		/// <param name="moduleName">Name of the module that contains the type</param>
		/// <param name="typeName">Name of the type that contains the field</param>
		/// <param name="fieldName">Name of the field</param>
		/// <param name="address">Address of the structure</param>
		/// <param name="value">The value read from memory</param>
		/// <returns>HRESULT</returns>
        public int ReadUInt8FromStructure(ulong moduleBase, uint typeId, string fieldName, UInt64 address, out Byte value)
		{
			int hr;
			ulong fieldAddress;
            if (FAILED(hr = GetFieldVirtualAddress(moduleBase, typeId, fieldName,address, out fieldAddress)))
			{
				value = 0;
				return hr;
			}
            return ReadVirtual8(fieldAddress, out value);
		}

		/// <summary>
		/// Wraps the core function, blocking all output
		/// </summary>
		public int ReadUInt8FromStructure_Silent(string moduleName, string typeName, string fieldName, UInt64 address, out Byte value)
		{
			using (var wrapper = InstallIgnoreFilter_WRAP_WITH_USING())
			{
				return ReadUInt8FromStructure(moduleName, typeName, fieldName, address, out value);
			}
		}

		/// <summary>
		/// Reads a 16-bit value from a structure
		/// </summary>
		/// <param name="moduleName">Name of the module that contains the type</param>
		/// <param name="typeName">Name of the type that contains the field</param>
		/// <param name="fieldName">Name of the field</param>
		/// <param name="address">Address of the structure</param>
		/// <param name="value">The value read from memory</param>
		/// <returns>HRESULT</returns>
		public int ReadInt16FromStructure(string moduleName, string typeName, string fieldName, UInt64 address, out Int16 value)
		{
			int hr;
            ulong fieldAddress;
            if (FAILED(hr = GetFieldVirtualAddress(moduleName, typeName, fieldName,address, out fieldAddress)))
			{
				value = 0;
				return hr;
			}
            return ReadVirtual16(fieldAddress, out value);
		}


        /// <summary>
        /// Reads a 16-bit value from a structure
        /// </summary>
        /// <param name="moduleName">Name of the module that contains the type</param>
        /// <param name="typeName">Name of the type that contains the field</param>
        /// <param name="fieldName">Name of the field</param>
        /// <param name="address">Address of the structure</param>
        /// <param name="value">The value read from memory</param>
        /// <returns>HRESULT</returns>
        public int ReadInt16FromStructure(ulong moduleBase, uint typeId, string fieldName, UInt64 address, out Int16 value)
        {
            int hr;
            ulong fieldAddress;
            if (FAILED(hr = GetFieldVirtualAddress(moduleBase, typeId, fieldName, address, out fieldAddress)))
            {
                value = 0;
                return hr;
            }
            return ReadVirtual16(fieldAddress, out value);
        }

		/// <summary>
		/// Wraps the core function, blocking all output
		/// </summary>
		public int ReadInt16FromStructure_Silent(string moduleName, string typeName, string fieldName, UInt64 address, out Int16 value)
		{
			using (var wrapper = InstallIgnoreFilter_WRAP_WITH_USING())
			{
				return ReadInt16FromStructure(moduleName, typeName, fieldName, address, out value);
			}
		}

		/// <summary>
		/// Reads a 16-bit value from a structure
		/// </summary>
		/// <param name="moduleName">Name of the module that contains the type</param>
		/// <param name="typeName">Name of the type that contains the field</param>
		/// <param name="fieldName">Name of the field</param>
		/// <param name="address">Address of the structure</param>
		/// <param name="value">The value read from memory</param>
		/// <returns>HRESULT</returns>
		public int ReadUInt16FromStructure(string moduleName, string typeName, string fieldName, UInt64 address, out UInt16 value)
		{
			int hr;
            ulong fieldAddress;
            if (FAILED(hr = GetFieldVirtualAddress(moduleName, typeName, fieldName, address,  out fieldAddress)))
			{
				value = 0;
				return hr;
			}
            return ReadVirtual16(fieldAddress, out value);
		}

        /// <summary>
        /// Reads a 16-bit value from a structure
        /// </summary>
        /// <param name="moduleName">Name of the module that contains the type</param>
        /// <param name="typeName">Name of the type that contains the field</param>
        /// <param name="fieldName">Name of the field</param>
        /// <param name="address">Address of the structure</param>
        /// <param name="value">The value read from memory</param>
        /// <returns>HRESULT</returns>
        public int ReadUInt16FromStructure(ulong moduleBase, uint typeId, string fieldName, UInt64 address, out UInt16 value)
        {
            int hr;
            ulong fieldAddress;
            if (FAILED(hr = GetFieldVirtualAddress(moduleBase, typeId, fieldName, address, out fieldAddress)))
            {
                value = 0;
                return hr;
            }
            return ReadVirtual16(fieldAddress, out value);
        }

		/// <summary>
		/// Wraps the core function, blocking all output
		/// </summary>
		public int ReadUInt16FromStructure_Silent(string moduleName, string typeName, string fieldName, UInt64 address, out UInt16 value)
		{
			using (var wrapper = InstallIgnoreFilter_WRAP_WITH_USING())
			{
				return ReadUInt16FromStructure(moduleName, typeName, fieldName, address, out value);
			}
		}

		/// <summary>
		/// Reads a 32-bit value from a structure
		/// </summary>
		/// <param name="moduleName">Name of the module that contains the type</param>
		/// <param name="typeName">Name of the type that contains the field</param>
		/// <param name="fieldName">Name of the field</param>
		/// <param name="address">Address of the structure</param>
		/// <param name="value">The value read from memory</param>
		/// <returns>HRESULT</returns>
		public int ReadInt32FromStructure(ulong moduleBase, uint typeId, string fieldName, UInt64 address, out Int32 value)
		{
			int hr;
			ulong fieldAddress;
            if (FAILED(hr = GetFieldVirtualAddress(moduleBase, typeId, fieldName, address, out fieldAddress)))
			{
				value = 0;
				return hr;
			}
			return ReadVirtual32(fieldAddress, out value);
		}


        /// <summary>
        /// Reads a 32-bit value from a structure
        /// </summary>
        /// <param name="moduleName">Name of the module that contains the type</param>
        /// <param name="typeName">Name of the type that contains the field</param>
        /// <param name="fieldName">Name of the field</param>
        /// <param name="address">Address of the structure</param>
        /// <param name="value">The value read from memory</param>
        /// <returns>HRESULT</returns>
        public int ReadInt32FromStructure(string moduleName, string typeName, string fieldName, UInt64 address, out Int32 value)
        {
            int hr;
            ulong fieldAddress;
            if (FAILED(hr = GetFieldVirtualAddress(moduleName, typeName, fieldName, address, out fieldAddress)))
            {
                value = 0;
                return hr;
            }
            return ReadVirtual32(fieldAddress, out value);
        }

		/// <summary>
		/// Wraps the core function, blocking all output
		/// </summary>
		public int ReadInt32FromStructure_Silent(string moduleName, string typeName, string fieldName, UInt64 address, out Int32 value)
		{
			using (var wrapper = InstallIgnoreFilter_WRAP_WITH_USING())
			{
				return ReadInt32FromStructure(moduleName, typeName, fieldName, address, out value);
			}
		}

		/// <summary>
		/// Reads a 32-bit value from a structure
		/// </summary>
		/// <param name="moduleName">Name of the module that contains the type</param>
		/// <param name="typeName">Name of the type that contains the field</param>
		/// <param name="fieldName">Name of the field</param>
		/// <param name="address">Address of the structure</param>
		/// <param name="value">The value read from memory</param>
		/// <returns>HRESULT</returns>
		public int ReadUInt32FromStructure(string moduleName, string typeName, string fieldName, UInt64 address, out UInt32 value)
		{
			int hr;
            ulong fieldAddress;
            if (FAILED(hr = GetFieldVirtualAddress(moduleName, typeName, fieldName, address, out fieldAddress)))
            {
                value = 0;
                return hr;
            }
			return ReadVirtual32(fieldAddress, out value);
		}

        /// <summary>
        /// Reads a 32-bit value from a structure
        /// </summary>
        /// <param name="moduleName">Name of the module that contains the type</param>
        /// <param name="typeName">Name of the type that contains the field</param>
        /// <param name="fieldName">Name of the field</param>
        /// <param name="address">Address of the structure</param>
        /// <param name="value">The value read from memory</param>
        /// <returns>HRESULT</returns>
        public int ReadUInt32FromStructure(ulong moduleBase, uint typeId, string fieldName, UInt64 address, out UInt32 value)
        {
            int hr;
            ulong fieldAddress;
            if (FAILED(hr = GetFieldVirtualAddress(moduleBase, typeId, fieldName, address, out fieldAddress)))
            {
                value = 0;
                return hr;
            }
            return ReadVirtual32(fieldAddress, out value);
        }

		/// <summary>
		/// Wraps the core function, blocking all output
		/// </summary>
		public int ReadUInt32FromStructure_Silent(string moduleName, string typeName, string fieldName, UInt64 address, out UInt32 value)
		{
			using (var wrapper = InstallIgnoreFilter_WRAP_WITH_USING())
			{
				return ReadUInt32FromStructure(moduleName, typeName, fieldName, address, out value);
			}
		}

		/// <summary>
		/// Reads a 64-bit value from a structure
		/// </summary>
		/// <param name="moduleName">Name of the module that contains the type</param>
		/// <param name="typeName">Name of the type that contains the field</param>
		/// <param name="fieldName">Name of the field</param>
		/// <param name="address">Address of the structure</param>
		/// <param name="value">The value read from memory</param>
		/// <returns>HRESULT</returns>
        public int ReadInt64FromStructure(ulong moduleBase, uint typeId, string fieldName, UInt64 address, out Int64 value)
		{
			int hr;
            ulong fieldAddress;
            if (FAILED(hr = GetFieldVirtualAddress(moduleBase, typeId, fieldName, address, out fieldAddress)))
            {
                value = 0;
                return hr;
            }
			return ReadVirtual64(fieldAddress, out value);
		}


        /// <summary>
        /// Reads a 64-bit value from a structure
        /// </summary>
        /// <param name="moduleName">Name of the module that contains the type</param>
        /// <param name="typeName">Name of the type that contains the field</param>
        /// <param name="fieldName">Name of the field</param>
        /// <param name="address">Address of the structure</param>
        /// <param name="value">The value read from memory</param>
        /// <returns>HRESULT</returns>
        public int ReadInt64FromStructure(string moduleName, string typeName, string fieldName, UInt64 address, out Int64 value)
        {
            int hr;
            ulong fieldAddress;
            if (FAILED(hr = GetFieldVirtualAddress(moduleName, typeName, fieldName, address, out fieldAddress)))
            {
                value = 0;
                return hr;
            }
            return ReadVirtual64(fieldAddress, out value);
        }

		/// <summary>
		/// Wraps the core function, blocking all output
		/// </summary>
		public int ReadInt64FromStructure_Silent(string moduleName, string typeName, string fieldName, UInt64 address, out Int64 value)
		{
			using (var wrapper = InstallIgnoreFilter_WRAP_WITH_USING())
			{
				return ReadInt64FromStructure(moduleName, typeName, fieldName, address, out value);
			}
		}

		/// <summary>
		/// Reads a 64-bit value from a structure
		/// </summary>
		/// <param name="moduleName">Name of the module that contains the type</param>
		/// <param name="typeName">Name of the type that contains the field</param>
		/// <param name="fieldName">Name of the field</param>
		/// <param name="address">Address of the structure</param>
		/// <param name="value">The value read from memory</param>
		/// <returns>HRESULT</returns>
        public int ReadUInt64FromStructure(ulong moduleBase, uint typeId, string fieldName, UInt64 address, out UInt64 value)
		{
			int hr;
            ulong fieldAddress;
            if (FAILED(hr = GetFieldVirtualAddress(moduleBase, typeId, fieldName, address, out fieldAddress)))
            {
                value = 0;
                return hr;
            }
			return ReadVirtual64(fieldAddress, out value);
		}

        /// <summary>
        /// Reads a 64-bit value from a structure
        /// </summary>
        /// <param name="moduleName">Name of the module that contains the type</param>
        /// <param name="typeName">Name of the type that contains the field</param>
        /// <param name="fieldName">Name of the field</param>
        /// <param name="address">Address of the structure</param>
        /// <param name="value">The value read from memory</param>
        /// <returns>HRESULT</returns>
        public int ReadUInt64FromStructure(string moduleName, string typeName, string fieldName, UInt64 address, out UInt64 value)
        {
            int hr;
            ulong fieldAddress;
            if (FAILED(hr = GetFieldVirtualAddress(moduleName, typeName, fieldName, address, out fieldAddress)))
            {
                value = 0;
                return hr;
            }
            return ReadVirtual64(fieldAddress, out value);
        }

		/// <summary>
		/// Wraps the core function, blocking all output
		/// </summary>
		public int ReadUInt64FromStructure_Silent(string moduleName, string typeName, string fieldName, UInt64 address, out UInt64 value)
		{
			using (var wrapper = InstallIgnoreFilter_WRAP_WITH_USING())
			{
				return ReadUInt64FromStructure(moduleName, typeName, fieldName, address, out value);
			}
		}

		/// <summary>
		/// Reads a 32-bit value from a structure
		/// NOTE: This is the 4-byte Windows BOOL, not the 1-byte C/C++ bool
		/// </summary>
		/// <param name="moduleName">Name of the module that contains the type</param>
		/// <param name="typeName">Name of the type that contains the field</param>
		/// <param name="fieldName">Name of the field</param>
		/// <param name="address">Address of the structure</param>
		/// <param name="value">The value read from memory</param>
		/// <returns>HRESULT</returns>
		public int ReadBOOLFromStructure(string moduleName, string typeName, string fieldName, UInt64 address, out bool value)
		{
			int hr;
			ulong fieldAddress;
            UInt32 tempValue;
            if (FAILED(hr = GetFieldVirtualAddress(moduleName, typeName, fieldName, address, out fieldAddress)) || FAILED(hr = ReadVirtual32(fieldAddress, out tempValue)))
			{
				value = false;
				return hr;
			}
			value = (tempValue != 0);
			return hr;
		}

		/// <summary>
		/// Wraps the core function, blocking all output
		/// </summary>
		public int ReadBOOLFromStructure_Silent(string moduleName, string typeName, string fieldName, UInt64 address, out bool value)
		{
			using (var wrapper = InstallIgnoreFilter_WRAP_WITH_USING())
			{
				return ReadBOOLFromStructure(moduleName, typeName, fieldName, address, out value);
			}
		}

		/// <summary>
		/// Reads a 32-bit value from a structure
		/// NOTE: This is the 1-byte C/C++ bool, not the 4-byte Windows BOOL
		/// </summary>
		/// <param name="moduleName">Name of the module that contains the type</param>
		/// <param name="typeName">Name of the type that contains the field</param>
		/// <param name="fieldName">Name of the field</param>
		/// <param name="address">Address of the structure</param>
		/// <param name="value">The value read from memory</param>
		/// <returns>HRESULT</returns>
		public int ReadBoolFromStructure(string moduleName, string typeName, string fieldName, UInt64 address, out bool value)
		{
			int hr;
			ulong fieldAddress;
			Byte tempValue;
			if (FAILED(hr = GetFieldVirtualAddress(moduleName, typeName, fieldName,address, out fieldAddress)) || FAILED(hr = ReadVirtual8(fieldAddress, out tempValue)))
			{
				value = false;
				return hr;
			}
			value = (tempValue != 0);
			return hr;
		}

		/// <summary>
		/// Wraps the core function, blocking all output
		/// </summary>
		public int ReadBoolFromStructure_Silent(string moduleName, string typeName, string fieldName, UInt64 address, out bool value)
		{
			using (var wrapper = InstallIgnoreFilter_WRAP_WITH_USING())
			{
				return ReadBoolFromStructure(moduleName, typeName, fieldName, address, out value);
			}
		}

		/// <summary>
		/// Reads a pointer from a structure
		/// </summary>
		/// <param name="moduleName">Name of the module that contains the type</param>
		/// <param name="typeName">Name of the type that contains the field</param>
		/// <param name="fieldName">Name of the field</param>
		/// <param name="address">Address of the structure</param>
		/// <param name="value">The value read from memory</param>
		/// <returns>HRESULT</returns>
		public int ReadPointerFromStructure(string moduleName, string typeName, string fieldName, UInt64 address, out UInt64 value)
		{
			int hr;
            ulong fieldAddress;
            if (FAILED(hr = GetFieldVirtualAddress(moduleName, typeName, fieldName, address, out fieldAddress)))
            {
                value = 0;
                return hr;
            }

            hr = ReadPointer(fieldAddress, out value);
            return hr;
		}

        /// <summary>
        /// Reads a pointer from a structure
        /// </summary>
        /// <param name="moduleName">Name of the module that contains the type</param>
        /// <param name="typeName">Name of the type that contains the field</param>
        /// <param name="fieldName">Name of the field</param>
        /// <param name="address">Address of the structure</param>
        /// <param name="value">The value read from memory</param>
        /// <returns>HRESULT</returns>
        public int ReadPointerFromStructure(ulong moduleBase, uint typeId, string fieldName, UInt64 address, out UInt64 value)
        {
            int hr;
            ulong fieldAddress;
            if (FAILED(hr = GetFieldVirtualAddress(moduleBase, typeId, fieldName, address, out fieldAddress)))
            {
                value = 0;
                return hr;
            }
            return ReadPointer(fieldAddress, out value);
        }

		/// <summary>
		/// Wraps the core function, blocking all output
		/// </summary>
		public int ReadPointerFromStructure_Silent(string moduleName, string typeName, string fieldName, UInt64 address, out UInt64 value)
		{
			using (var wrapper = InstallIgnoreFilter_WRAP_WITH_USING())
			{
				return ReadPointerFromStructure(moduleName, typeName, fieldName, address, out value);
			}
		}

		/// <summary>
		/// Reads a byte array from a memory address
		/// </summary>
		/// <param name="Address">Address to read from</param>
		/// <param name="cb">Number of bytes to read</param>
		/// <param name="bin">The data that was retrieved</param>
		/// <returns>HRESULT</returns>
		public int ReadByteArrayFromAddress(UInt64 Address, uint cb, out byte[] bin)
		{
			int hr;

			IntPtr buffer = Marshal.AllocHGlobal((int)cb);
			UInt32 nRead = 0;
			if (FAILED(hr = ReadVirtual(Address, cb, buffer, &nRead)))
			{
				bin = null;
				return hr;
			}

			bin = new byte[cb];
			Marshal.Copy(buffer, bin, 0, (int)nRead);
			Marshal.FreeHGlobal(buffer);
			return S_OK;
		}


        /// <summary>
        /// Reads a byte array from a memory address
        /// </summary>
        /// <param name="Address">Address to read from</param>
        /// <param name="cb">Number of bits to read</param>
        /// <param name="bin">The data that was retrieved</param>
        /// <returns>HRESULT</returns>
        public BitArray ReadBitsFromAddress(UInt64 Address, uint cBits)
        {
            int hr;
            uint cBytes = cBits / 8;                  

            IntPtr buffer = Marshal.AllocHGlobal((int)cBytes);
            UInt32 nRead = 0;
            if (FAILED(hr = ReadVirtual(Address, cBytes, buffer, &nRead)))
            {
                //bin = null;
                ThrowExceptionHere(hr);
                return null;
            }

            var bin = new byte[cBytes];
            Marshal.Copy(buffer, bin, 0, (int)nRead);
            Marshal.FreeHGlobal(buffer);

            BitArray ba = new BitArray(bin);
            return ba;
        }

		/// <summary>
		/// Reads a unicode string from a structure
		/// NOTE: This function assumes the structure has an pointer to the unicode string, NOT a string embedded within the structure itself!
		/// </summary>
		/// <param name="moduleName">Name of the module that contains the type</param>
		/// <param name="typeName">Name of the type that contains the field</param>
		/// <param name="fieldName">Name of the field</param>
		/// <param name="structureAddress">Address of the structure</param>
		/// <param name="maxSize">Maximum number of characters to read</param>
		/// <param name="output">The data that was retrieved</param>
		/// <returns>HRESULT</returns>
		public int ReadUnicodeStringFromStructure(string moduleName, string typeName, string fieldName, UInt64 structureAddress, uint maxSize, out string output)
		{
			int hr;
			UInt64 stringPointer;
			if (FAILED(hr = ReadPointerFromStructure(moduleName, typeName, fieldName, structureAddress, out stringPointer)))
			{
				output = null;
				return hr;
			}
			return ReadUnicodeString(stringPointer, maxSize, out output);
		}

        /// <summary>
        /// Reads a unicode string from a structure
        /// NOTE: This function assumes the structure has an pointer to the unicode string, NOT a string embedded within the structure itself!
        /// </summary>
        /// <param name="moduleName">Name of the module that contains the type</param>
        /// <param name="typeName">Name of the type that contains the field</param>
        /// <param name="fieldName">Name of the field</param>
        /// <param name="structureAddress">Address of the structure</param>
        /// <param name="maxSize">Maximum number of characters to read</param>
        /// <param name="output">The data that was retrieved</param>
        /// <returns>HRESULT</returns>
        public int ReadUnicodeStringFromStructure_WOW(string moduleName, string typeName, string fieldName, UInt64 structureAddress, uint maxSize, out string output)
        {
            int hr;
            UInt64 stringPointer;
            if (FAILED(hr = ReadPointerFromStructure(moduleName, typeName, fieldName, structureAddress, out stringPointer)))
            {
                output = null;
                return hr;
            }

            PEFile PEFile = new PEFile(this, moduleName);
            if (PEFile.GetMachineType() == IMAGE_FILE_MACHINE.I386)
                stringPointer &= 0xFFFFFFFF;

            return ReadUnicodeString(stringPointer, maxSize, out output);
        }

        /// <summary>
        /// Reads a unicode string from a structure
        /// NOTE: This function assumes a string embedded within the structure itself!
        /// </summary>
        /// <param name="moduleName">Name of the module that contains the type</param>
        /// <param name="typeName">Name of the type that contains the field</param>
        /// <param name="fieldName">Name of the field</param>
        /// <param name="structureAddress">Address of the structure</param>
        /// <param name="maxSize">Maximum number of characters to read</param>
        /// <param name="output">The data that was retrieved</param>
        /// <returns>HRESULT</returns>
        public int ReadUnicodeStringFromStructure_Embedded(string moduleName, string typeName, string fieldName, UInt64 address, uint maxSize, out string output)
        {
            int hr;
            ulong fieldAddress;
            if (FAILED(hr = GetFieldVirtualAddress(moduleName, typeName, fieldName, address, out fieldAddress)))
            {
                output = string.Empty;
                return hr;
            }
            return ReadUnicodeString(fieldAddress, maxSize, out output);
        }

		/// <summary>
		/// Wraps the core function, blocking all output
		/// </summary>
		public int ReadUnicodeStringFromStructure_Silent(string moduleName, string typeName, string fieldName, UInt64 structureAddress, uint maxSize, out string output)
		{
			using (var wrapper = InstallIgnoreFilter_WRAP_WITH_USING())
			{
				return ReadUnicodeStringFromStructure(moduleName, typeName, fieldName, structureAddress, maxSize, out output);
			}
		}

		/// <summary>
		/// Reads an ANSI string from a structure
		/// NOTE: This function assumes the structure has an pointer to the ansi string, NOT a string embedded within the structure itself!
		/// </summary>
		/// <param name="moduleName">Name of the module that contains the type</param>
		/// <param name="typeName">Name of the type that contains the field</param>
		/// <param name="fieldName">Name of the field</param>
		/// <param name="structureAddress">Address of the structure</param>
		/// <param name="maxSize">Maximum number of characters to read</param>
		/// <param name="output">The data that was retrieved</param>
		/// <returns>HRESULT</returns>
		public int ReadAnsiStringFromStructure(string moduleName, string typeName, string fieldName, UInt64 structureAddress, uint maxSize, out string output)
		{
			int hr;
			UInt64 stringPointer;
			if (FAILED(hr = ReadPointerFromStructure(moduleName, typeName, fieldName, structureAddress, out stringPointer)))
			{
				output = null;
				return hr;
			}
			return ReadAnsiString(stringPointer, maxSize, out output);
		}

		/// <summary>
		/// Wraps the core function, blocking all output
		/// </summary>
		public int ReadAnsiStringFromStructure_Silent(string moduleName, string typeName, string fieldName, UInt64 structureAddress, uint maxSize, out string output)
		{
			using (var wrapper = InstallIgnoreFilter_WRAP_WITH_USING())
			{
				return ReadAnsiStringFromStructure(moduleName, typeName, fieldName, structureAddress, maxSize, out output);
			}
		}

        public enum ReadUNICODE_STRINGOptions
        {
            Escaped=0,
            Raw,
            Truncated,
        }

		/// <summary>
		/// Reads a UNICODE_STRING from a structure
		/// NOTE: This function assumes that the UNICODE_STRING structure is embedded in the parent struction, NOT that the parent has a pointer to a UNICODE_STRING!
		/// </summary>
		/// <param name="moduleName">Name of the module that contains the type</param>
		/// <param name="typeName">Name of the type that contains the field</param>
		/// <param name="fieldName">Name of the field</param>
		/// <param name="structureAddress">Address of the structure</param>
		/// <param name="output">The data that was retrieved</param>
		/// <returns>HRESULT</returns>
		public int ReadUNICODE_STRINGFromStructure_Embedded(string moduleName, string typeName, string fieldName, UInt64 structureAddress, out string output, ReadUNICODE_STRINGOptions options = ReadUNICODE_STRINGOptions.Escaped)
		{
			int hr;
            ulong fieldAddress;
            if (FAILED(hr = GetFieldVirtualAddress(moduleName, typeName, fieldName, structureAddress, out fieldAddress)))
            {
                output = string.Empty;
                return hr;
            }
            return ReadUNICODE_STRING(fieldAddress, out output, options);
		}


		/// <summary>
		/// Reads a UNICODE_STRING from a structure
		/// NOTE: This function assumes that the structure contains a pointer to the UNICODE_STRING, NOT that the UNICODE_STRING is embedded!
		/// </summary>
		/// <param name="moduleName">Name of the module that contains the type</param>
		/// <param name="typeName">Name of the type that contains the field</param>
		/// <param name="fieldName">Name of the field</param>
		/// <param name="structureAddress">Address of the structure</param>
		/// <param name="output">The data that was retrieved</param>
		/// <returns>HRESULT</returns>
        public int ReadUNICODE_STRINGFromStructure_Pointer(string moduleName, string typeName, string fieldName, UInt64 structureAddress, out string output, ReadUNICODE_STRINGOptions options = ReadUNICODE_STRINGOptions.Escaped)
		{
			int hr;
			UInt64 stringPointer;
			if (FAILED(hr = ReadPointerFromStructure(moduleName, typeName, fieldName, structureAddress, out stringPointer)))
			{
				output = null;
				return hr;
			}
			return ReadUNICODE_STRING(stringPointer, out output, options);
		}


		/// <summary>
		/// Returns true of the debugger has access to the kernel. If in Mex, you should use DumpInfo.IsKernelMode
		/// </summary>
		/// <returns></returns>
		internal bool IsKernelMode()
		{
			DEBUG_CLASS debuggeeClass;
			DEBUG_CLASS_QUALIFIER debuggeeClassQualifier;
			int hr = DebugControl.GetDebuggeeType(out debuggeeClass, out debuggeeClassQualifier);
			if (FAILED(hr))
			{
				OutputVerboseLine("ERROR! Can't tell is debug target is kernel or usermode, assuming user: {0:x8}", hr);
				return false;
			}
			else if (debuggeeClass == DEBUG_CLASS.UNINITIALIZED)
			{
				OutputVerboseLine("ERROR! Debugger target is not initialized!");
				return false;
			}

			return (debuggeeClass == DEBUG_CLASS.KERNEL);
		}

		/// <summary>
		/// Returns whether the debuggee is kernel and if full memory is available.
		/// For kernel dumps, a full kernel dump without usermode memory is NOT considered to have full memory available.
		/// </summary>
		public int GetDebuggeeMode(out bool isKernel, out bool isFullMemoryAvailable)
		{
			DEBUG_CLASS debuggeeClass;
			DEBUG_CLASS_QUALIFIER debuggeeClassQualifier;
			int hr = DebugControl.GetDebuggeeType(out debuggeeClass, out debuggeeClassQualifier);
			if (FAILED(hr))
			{
				OutputVerboseLine("ERROR! Can't tell is debug target is kernel or usermode, assuming user: {0:x8}", hr);
				isKernel = false;
				isFullMemoryAvailable = false;
				return hr;
			}

			isKernel = (debuggeeClass == DEBUG_CLASS.KERNEL);

			if (isKernel)
			{
				switch (debuggeeClassQualifier)
				{
					case DEBUG_CLASS_QUALIFIER.KERNEL_SMALL_DUMP:
					case DEBUG_CLASS_QUALIFIER.KERNEL_DUMP:
					{
						isFullMemoryAvailable = false;
						break;
					}
					case DEBUG_CLASS_QUALIFIER.KERNEL_CONNECTION:
					case DEBUG_CLASS_QUALIFIER.KERNEL_LOCAL:
					case DEBUG_CLASS_QUALIFIER.KERNEL_EXDI_DRIVER:
					case DEBUG_CLASS_QUALIFIER.KERNEL_IDNA:
					case DEBUG_CLASS_QUALIFIER.KERNEL_FULL_DUMP:
					{
						isFullMemoryAvailable = true;
						break;
					}
					default:
					{
						OutputErrorLine("ERROR! Unknown debug class qualifier: {0:x8}", debuggeeClassQualifier);
						isFullMemoryAvailable = false;
						break;
					}
				}
			}
			else
			{
				DEBUG_FORMAT debugFormat;
				hr = DebugControl.GetDumpFormatFlags(out debugFormat);
				if (FAILED(hr))
				{
					OutputVerboseLine("ERROR! Failed getting dump format flags: {0:x8}", hr);
					isFullMemoryAvailable = false;
					return hr;
				}

				isFullMemoryAvailable = ((debugFormat & (DEBUG_FORMAT.USER_SMALL_FULL_MEMORY_INFO | DEBUG_FORMAT.USER_SMALL_FULL_MEMORY)) != 0);
			}
			return hr;
		}

		/// <summary>
		/// Wraps IDebugSymbols2::GetSymbolOptions
		/// </summary>
		/// <param name="options">The current symbol options</param>
		/// <returns>HRESULT</returns>
		public int GetSymbolOptions(out SYMOPT options)
		{
			return DebugSymbols.GetSymbolOptions(out options);
		}

		/// <summary>
		/// Wraps IDebugSymbols3::GetNameByOffset
		/// </summary>
		/// <param name="address">Address to lookup</param>
		/// <param name="name">The returned name for the address passed in</param>
		/// <param name="displacement">Optional displacement from the address passed in</param>
		/// <returns>HRESULT</returns>
		public unsafe int GetNameByOffset(UInt64 address, out string name, ulong* displacement)
		{
          
            StringBuilder sb = new StringBuilder(1024);

			int hr = DebugSymbols.GetNameByOffsetWide(address, sb, sb.Capacity, null, displacement);
			name = SUCCEEDED(hr) ? sb.ToString() : "";
			return hr;
		}

		/// <summary>
		/// Wraps IDebugSymbols5::GetNameByInlineContext, or IDebugSymbols3::GetNameByOffset if not available
		/// </summary>
		/// <param name="address">Address to lookup</param>
		/// <param name="inlineContext">inlineContext</param>
		/// <param name="name">The returned name for the address passed in</param>
		/// <param name="displacement">Optional displacement from the address passed in</param>
		/// <returns>HRESULT</returns>
		public unsafe int GetNameByInlineContext(UInt64 address, UInt32 inlineContext, out string name, ulong* displacement)
		{
            SymbolInfoCache Si;
            int hr = S_OK;
            if (Cache.GetSymbolName.TryGetValue(Tuple.Create(address, inlineContext), out Si))
            {
                name = Si.SymbolName;
                *displacement = Si.offset;
                return hr;
            }
       
            if (DebugSymbols5 == null)
			{
				hr = GetNameByOffset(address, out name, displacement);
                if (SUCCEEDED(hr))
                {

                    Si.SymbolName = name;
                    Si.offset = *displacement;

                    Cache.GetSymbolName.Add(Tuple.Create(address, inlineContext), Si);
                }
                return hr;
			}

			StringBuilder sb = new StringBuilder(1024);

			hr = DebugSymbols5.GetNameByInlineContextWide(address, inlineContext, sb, sb.Capacity, null, displacement);
			name = SUCCEEDED(hr) ? sb.ToString() : "";
            if (SUCCEEDED(hr))
            {
                Si.SymbolName = name;
                Si.offset = *displacement;

                Cache.GetSymbolName.Add(Tuple.Create(address, inlineContext), Si);
            }
			return hr;
		}

		/// <summary>
		/// Wraps IDebugSymbols2::GetLineByOffset
		/// </summary>
		/// <param name="address">Address to lookup</param>
		/// <param name="fileName">Name of the file the address resides in</param>
		/// <param name="lineNumber">Line number inside the file</param>
		/// <param name="displacement">Optional displacement from the address passed in</param>
		/// <returns>HRESULT</returns>
		public unsafe int GetLineByOffset(UInt64 address, out string fileName, out uint lineNumber, ulong* displacement)
		{
			StringBuilder sb = new StringBuilder(1024);
			uint line = 0;

			int hr = DebugSymbols.GetLineByOffsetWide(address, &line, sb, sb.Capacity, null, displacement);
			fileName = SUCCEEDED(hr) ? sb.ToString() : "";
			lineNumber = line;
			return hr;
		}

		/// <summary>
		/// Wraps IDebugSymbols2::GetLineByInlineContext
		/// </summary>
		/// <param name="address">Address to lookup</param>
		/// <param name="inlineContext">inlineContext</param>
		/// <param name="fileName">Name of the file the address resides in</param>
		/// <param name="lineNumber">Line number inside the file</param>
		/// <param name="displacement">Optional displacement from the address passed in</param>
		/// <returns>HRESULT</returns>
		public unsafe int GetLineByInlineContext(UInt64 address, uint inlineContext, out string fileName, out uint lineNumber, ulong* displacement)
		{
			if (DebugSymbols5 == null)
			{
				return GetLineByOffset(address, out fileName, out lineNumber, displacement);
			}

			StringBuilder sb = new StringBuilder(1024);
			uint line = 0;

			int hr = DebugSymbols5.GetLineByInlineContextWide(address, inlineContext, &line, sb, sb.Capacity, null, displacement);
			fileName = SUCCEEDED(hr) ? sb.ToString() : "";
			lineNumber = line;
			return hr;
		}

		/// <summary>
		/// Tries to determine if a module has private symbols
		/// </summary>
		public unsafe bool HasPrivateSymbols(UInt64 moduleBaseAddress, string moduleNameWithExtension)
		{
			IMAGEHLP_MODULE64 ImageHlp;

			DebugAdvanced.GetSymbolInformationWide(DEBUG_SYMINFO.IMAGEHLP_MODULEW64, moduleBaseAddress, 0, &ImageHlp, sizeof(IMAGEHLP_MODULE64), null, null, 0, null);

            if (ImageHlp.SymType == DEBUG_SYMTYPE.DEFERRED)
            {
                OutputVerboseLine("Loading Symbols for {0}", Path.GetFileName(moduleNameWithExtension));
                RunCommandSilent("ld /f {0}", Path.GetFileName(moduleNameWithExtension));
                //ReloadSymbols("/f " + Path.GetFileName(moduleNameWithExtension), false);
				DebugAdvanced.GetSymbolInformationWide(DEBUG_SYMINFO.IMAGEHLP_MODULEW64, moduleBaseAddress, 0, &ImageHlp, sizeof(IMAGEHLP_MODULE64), null, null, 0, null);
            }

			return ImageHlp.GlobalSymbols;
		}

		/// <summary>
		/// Calls IDebugDataSpaces.QueryVirtual with a correctly aligned MEMORY_BASIC_INFORMATION64 structure
		/// </summary>
		public unsafe int QueryVirtual(UInt64 address, out MEMORY_BASIC_INFORMATION64 memoryInfo)
		{
			IntPtr rawPointer = Marshal.AllocHGlobal(sizeof(MEMORY_BASIC_INFORMATION64) + 15);
			IntPtr alignedPointer = new IntPtr((rawPointer.ToInt64() + 15L) & ~15L);

			int hr = DebugDataSpaces.QueryVirtual(address, alignedPointer);
			//memoryInfo = SUCCEEDED(hr) ? (MEMORY_BASIC_INFORMATION64)Marshal.PtrToStructure(alignedPointer, typeof(MEMORY_BASIC_INFORMATION64)) : new MEMORY_BASIC_INFORMATION64();
			memoryInfo = SUCCEEDED(hr) ? *(MEMORY_BASIC_INFORMATION64*)alignedPointer.ToPointer() : new MEMORY_BASIC_INFORMATION64();

			Marshal.FreeHGlobal(rawPointer);
			return hr;
		}

        /// <summary>
		/// Gets the value of any field. Especially useful for bitfields.
		/// MemberName is smart. It will take value.value, and if it encounters a pointer, it will derefence them.  ie : Value.Value->Value, would look like: Value.Value.Value in the MemberName argument
		/// </summary>
		unsafe public int GetFieldValue(string SymbolName, string fieldName, UInt64 structureAddress, out ulong fieldValue)
        {
            string part1 = "";
            string part2 = SymbolName;

            if (SymbolName.Contains("!"))
            {
                string[] symbol = SymbolName.Split("!".ToCharArray());
                part1 = symbol[0];
                part2 = symbol[1];

            }

            return GetFieldValue(part1, part2, fieldName, structureAddress, out fieldValue);
        }

		/// <summary>
		/// Gets the value of any field. Especially useful for bitfields.
		/// MemberName is smart. It will take value.value, and if it encounters a pointer, it will derefence them.  ie : Value.Value->Value, would look like: Value.Value.Value in the MemberName argument
		/// </summary>
		unsafe public int GetFieldValue(string moduleName, string typeName, string fieldName, UInt64 structureAddress, out ulong fieldValue)
		{

            moduleName = FixModuleName(moduleName);

            int hr;
			fieldValue = 0;
			// Begin the runner up for the ugliest code ever:
			// The below code implements "GetFieldValue()" -- Proof of concept only. Trevor should clean this up and add it to the library.
            uint typeId;
			// Get the ModuleBase
			ulong moduleBase;

            bool pointer = false;
            typeName = typeName.TrimEnd();
            if (typeName.EndsWith("*"))
            {
                typeName = typeName.Substring(0, typeName.Length - 1).TrimEnd();
                pointer = true;
            }

            hr = GetSymbolTypeIdWide(moduleName + "!" + typeName, out typeId, out moduleBase);

            if (FAILED(hr))
            {
                GetModuleBase(moduleName, out moduleBase);
                hr = GetTypeId(moduleName, typeName, out typeId);
            }

            if (FAILED(hr))
            {
                return hr;
            }

			// Make a new Typed Data structure.
			_EXT_TYPED_DATA SymbolTypedData;

			// Fill it in from ModuleBase and TypeID
			// Note, we could use EXT_TDOP_SET_PTR_FROM_TYPE_ID_AND_U64 if this was a pointer to the object
			SymbolTypedData.Operation = _EXT_TDOP.EXT_TDOP_SET_FROM_TYPE_ID_AND_U64;
            
			SymbolTypedData.InData.ModBase = moduleBase;
			SymbolTypedData.InData.TypeId = typeId;
            

            if (pointer)
            {
                ReadPointer(structureAddress, out structureAddress);
            }
            SymbolTypedData.InData.Offset = structureAddress;



			if (FAILED(hr = DebugAdvanced.Request(DEBUG_REQUEST.EXT_TYPED_DATA_ANSI, &SymbolTypedData, sizeof(_EXT_TYPED_DATA), &SymbolTypedData, sizeof(_EXT_TYPED_DATA), null)))
			{
                OutputVerboseLine("GetFieldValue: DebugAdvanced.Request Failed to get {1}!{2}.{3} hr={0:x}", hr, moduleName, typeName, fieldName);
				return hr;
			}

			IntPtr Buffer = IntPtr.Zero;
			IntPtr MemPtr = IntPtr.Zero;
			try
			{
				_EXT_TYPED_DATA TemporaryTypedDataForBufferConstruction;

				// Allocate Buffers.

				MemPtr = Marshal.StringToHGlobalAnsi(fieldName);
				int TotalSize = sizeof(_EXT_TYPED_DATA) + fieldName.Length + 1; //+1 to account for the null terminator
				Buffer = Marshal.AllocHGlobal((int)TotalSize);

				// Get_Field. This does all the magic.
				TemporaryTypedDataForBufferConstruction.Operation = _EXT_TDOP.EXT_TDOP_GET_FIELD;

				// Pass in the OutData from the first call to Request(), so it knows what symbol to use
				TemporaryTypedDataForBufferConstruction.InData = SymbolTypedData.OutData;

				// The index of the string will be immediatly following the _EXT_TYPED_DATA structure
				TemporaryTypedDataForBufferConstruction.InStrIndex = (uint)sizeof(_EXT_TYPED_DATA);

				// Source is our _EXT_TYPED_DATA stuction, Dest is our empty allocated buffer
                DebugUtilities.CopyMemory(Buffer, (IntPtr)(&TemporaryTypedDataForBufferConstruction), sizeof(_EXT_TYPED_DATA));

                // Copy the ANSI string of our member name immediatly after the TypedData Structure.
                // Source is our ANSI Buffer, Dest is the byte immediatly after the last byte from the previous copy

                // This fails if we use i<MemberName.Length, made it i<= to copy the null terminator.
                DebugUtilities.CopyMemory(Buffer + sizeof(_EXT_TYPED_DATA), MemPtr, fieldName.Length + 1);
	
				// Call Request(), Passing in the buffer we created as the In and Out Parameters
				if (FAILED(hr = DebugAdvanced.Request(DEBUG_REQUEST.EXT_TYPED_DATA_ANSI, (void*)Buffer, TotalSize, (void*)Buffer, TotalSize, null)))
				{
                    OutputVerboseLine("GetFieldValue2: DebugAdvanced.Request Failed to get {1}!{2}.{3} hr={0:x}", hr, moduleName, typeName, fieldName);
					return hr;
				}

				// Convert the returned buffer to a _EXT_TYPED_Data CLASS (since it wont let me convert to a struct)
				_EXT_TYPED_DATA TypedDataInClassForm = (_EXT_TYPED_DATA)Marshal.PtrToStructure(Buffer, typeof(_EXT_TYPED_DATA));
                // OutData.Data has our field value.  This will always be a ulong
				fieldValue = TypedDataInClassForm.OutData.Data;

			}
			finally
			{
				Marshal.FreeHGlobal(Buffer);
				Marshal.FreeHGlobal(MemPtr);
			}

			return S_OK;
		}

        /// <summary>
        /// Gets the value of any field. Especially useful for bitfields.
        /// MemberName is smart. It will take value.value, and if it encounters a pointer, it will derefence them.  ie : Value.Value->Value, would look like: Value.Value.Value in the MemberName argument
        /// </summary>
        unsafe public int GetFieldValue(ulong moduleBase, uint typeId, string fieldName, UInt64 structureAddress, out ulong fieldValue)
        {

            int hr;
            fieldValue = 0;

            // Make a new Typed Data structure.
            _EXT_TYPED_DATA SymbolTypedData;

            // Fill it in from ModuleBase and TypeID
            // Note, we could use EXT_TDOP_SET_PTR_FROM_TYPE_ID_AND_U64 if this was a pointer to the object
            SymbolTypedData.Operation = _EXT_TDOP.EXT_TDOP_SET_FROM_TYPE_ID_AND_U64;

            SymbolTypedData.InData.ModBase = moduleBase;
            SymbolTypedData.InData.TypeId = typeId;

            SymbolTypedData.InData.Offset = structureAddress;

            if (FAILED(hr = DebugAdvanced.Request(DEBUG_REQUEST.EXT_TYPED_DATA_ANSI, &SymbolTypedData, sizeof(_EXT_TYPED_DATA), &SymbolTypedData, sizeof(_EXT_TYPED_DATA), null)))
            {
                OutputVerboseLine("GetFieldValue3: DebugAdvanced.Request Failed to get {1:x}!{2}.{3} hr={0:x}", hr, moduleBase, typeId, fieldName);
                return hr;
            }

            IntPtr Buffer = IntPtr.Zero;
            IntPtr MemPtr = IntPtr.Zero;
            try
            {
                _EXT_TYPED_DATA TemporaryTypedDataForBufferConstruction;

                // Allocate Buffers.

                MemPtr = Marshal.StringToHGlobalAnsi(fieldName);
                int TotalSize = sizeof(_EXT_TYPED_DATA) + fieldName.Length + 1; //+1 to account for the null terminator
                Buffer = Marshal.AllocHGlobal((int)TotalSize);

                // Get_Field. This does all the magic.
                TemporaryTypedDataForBufferConstruction.Operation = _EXT_TDOP.EXT_TDOP_GET_FIELD;

                // Pass in the OutData from the first call to Request(), so it knows what symbol to use
                TemporaryTypedDataForBufferConstruction.InData = SymbolTypedData.OutData;

                // The index of the string will be immediatly following the _EXT_TYPED_DATA structure
                TemporaryTypedDataForBufferConstruction.InStrIndex = (uint)sizeof(_EXT_TYPED_DATA);

                // Source is our _EXT_TYPED_DATA stuction, Dest is our empty allocated buffer
                DebugUtilities.CopyMemory(Buffer, (IntPtr)(&TemporaryTypedDataForBufferConstruction), sizeof(_EXT_TYPED_DATA));

                // Copy the ANSI string of our member name immediatly after the TypedData Structure.
                // Source is our ANSI Buffer, Dest is the byte immediatly after the last byte from the previous copy

                // This fails if we use i<MemberName.Length, made it i<= to copy the null terminator.
                DebugUtilities.CopyMemory(Buffer + sizeof(_EXT_TYPED_DATA), MemPtr, fieldName.Length + 1);

                // Call Request(), Passing in the buffer we created as the In and Out Parameters
                if (FAILED(hr = DebugAdvanced.Request(DEBUG_REQUEST.EXT_TYPED_DATA_ANSI, (void*)Buffer, TotalSize, (void*)Buffer, TotalSize, null)))
                {
                    OutputVerboseLine("GetFieldValue4: DebugAdvanced.Request Failed to get {1:x}!{2}.{3} hr={0:x}", hr, moduleBase, typeId, fieldName);
                    return hr;
                }

                // Convert the returned buffer to a _EXT_TYPED_Data CLASS (since it wont let me convert to a struct)
                _EXT_TYPED_DATA TypedDataInClassForm = (_EXT_TYPED_DATA)Marshal.PtrToStructure(Buffer, typeof(_EXT_TYPED_DATA));

                // OutData.Data has our field value.  This will always be a ulong
                fieldValue = TypedDataInClassForm.OutData.Data;

            }
            finally
            {
                Marshal.FreeHGlobal(Buffer);
                Marshal.FreeHGlobal(MemPtr);
            }

            return S_OK;
        }

        /// <summary>
        /// Gets the virtual Address of a field.  Useful for Static Fields
        /// MemberName is smart. It will take value.value, and if it encounters a pointer, it will derefence them.  ie : Value.Value->Value, would look like: Value.Value.Value in the MemberName argument
        /// </summary>
        unsafe public int GetFieldVirtualAddress(string moduleName, string typeName, string fieldName, UInt64 structureAddress, out ulong FieldAddress)
        {

            OutputDebugLine("GetFieldVirtualAddress: Module: {0}, Type {1}, Field {2}, StuctAddr: {3:x}", moduleName, typeName, fieldName, structureAddress);
            moduleName = FixModuleName(moduleName);

            int hr;
            FieldAddress = 0;

            uint typeId;
            // Get the ModuleBase
            ulong moduleBase;
            typeName = typeName.TrimEnd();
            if (typeName.EndsWith("*"))
            {
                typeName = typeName.Substring(0, typeName.Length - 1).TrimEnd();
                ReadPointer(structureAddress,out structureAddress);
                if (structureAddress == 0)
                {
                    OutputVerboseLine(" -- GetFieldVirtualAddress: Null pointer {0}, Type {1}, Field {2}, StuctAddr: {3:x}", moduleName, typeName, fieldName, structureAddress);
                    FieldAddress = 0;
                    return E_FAIL;
                }
                OutputDebugLine(" -- GetFieldVirtualAddress: DEREF POINTER: Module: {0}, Type {1}, Field {2}, StuctAddr: {3:x}", moduleName, typeName, fieldName, structureAddress);
            }

            ulong savedStructAddr = structureAddress;
            bool slow = false;
            uint offset = 0;
            
            hr = GetFieldOffset(moduleName, typeName, fieldName, out offset);

            if (FAILED(hr))
            {
                OutputVerboseLine("GetFieldOffset returned {0:x}", hr);
                return hr;
            }

            if (offset == 0)
            {
                slow = true;
                OutputDebugLine(" -- GetFieldVirtualAddress: Offset is 0, Slow is TRUE: Module: {0}, Type {1}, Field {2}, StuctAddr: {3:x}", moduleName, typeName, fieldName, structureAddress);
            }
            else
            {
                uint typeSize=0;
                hr = GetTypeSize(moduleName, typeName, out typeSize);
                if (typeSize == 0 || offset > typeSize)
                {
                    slow = true;
                }
            }

            if (slow == false)
            {
                FieldAddress = structureAddress + offset;
                OutputDebugLine(" *** Final Fixed up Field Offset value {1} = 0x{0:x} ***", FieldAddress, fieldName);
                return S_OK;
            }
        
            hr = GetSymbolTypeIdWide(typeName, out typeId, out moduleBase);

            if (FAILED(hr))
            {
                GetModuleBase(moduleName, out moduleBase);
                hr = GetTypeId(moduleName, typeName, out typeId);
                if (FAILED(hr))
                {
                    return hr;
                }
            }

            // Make a new Typed Data structure.
            _EXT_TYPED_DATA SymbolTypedData;

            // Fill it in from ModuleBase and TypeID
            // Note, we could use EXT_TDOP_SET_PTR_FROM_TYPE_ID_AND_U64 if this was a pointer to the object
            SymbolTypedData.Operation = _EXT_TDOP.EXT_TDOP_SET_FROM_TYPE_ID_AND_U64;

            SymbolTypedData.InData.ModBase = moduleBase;
            SymbolTypedData.InData.TypeId = typeId;

            SymbolTypedData.InData.Offset = structureAddress;

            if (FAILED(hr = DebugAdvanced.Request(DEBUG_REQUEST.EXT_TYPED_DATA_ANSI, &SymbolTypedData, sizeof(_EXT_TYPED_DATA), &SymbolTypedData, sizeof(_EXT_TYPED_DATA), null)))
            {
                OutputVerboseLine("GetFieldVirtualAddress: DebugAdvanced.Request Failed to get {1}!{2}.{3} hr={0:x}", hr, moduleName, typeName, fieldName);
                if (offset == 0)
                {
                    FieldAddress = savedStructAddr;
                    return S_OK;
                }
                return hr;
            }

            IntPtr Buffer = IntPtr.Zero;
            IntPtr MemPtr = IntPtr.Zero;
            try
            {
                _EXT_TYPED_DATA TemporaryTypedDataForBufferConstruction;

                // Allocate Buffers.

                MemPtr = Marshal.StringToHGlobalAnsi(fieldName);
                int TotalSize = sizeof(_EXT_TYPED_DATA) + fieldName.Length + 1;
                Buffer = Marshal.AllocHGlobal((int)TotalSize);

                // Get_Field. This does all the magic.
                TemporaryTypedDataForBufferConstruction.Operation = _EXT_TDOP.EXT_TDOP_GET_FIELD;

                // Pass in the OutData from the first call to Request(), so it knows what symbol to use
                TemporaryTypedDataForBufferConstruction.InData = SymbolTypedData.OutData;

                // The index of the string will be immediatly following the _EXT_TYPED_DATA structure
                TemporaryTypedDataForBufferConstruction.InStrIndex = (uint)sizeof(_EXT_TYPED_DATA);

                // Source is our _EXT_TYPED_DATA stuction, Dest is our empty allocated buffer

                DebugUtilities.CopyMemory(Buffer, (IntPtr)(&TemporaryTypedDataForBufferConstruction), sizeof(_EXT_TYPED_DATA));

                // Copy the ANSI string of our member name immediatly after the TypedData Structure.
                // Source is our ANSI Buffer, Dest is the byte immediatly after the last byte from the previous copy

                // This fails if we use fieldName.Lengthh, made it +1 to copy the null terminator.
                DebugUtilities.CopyMemory(Buffer + sizeof(_EXT_TYPED_DATA), MemPtr, fieldName.Length+1);

                // Call Request(), Passing in the buffer we created as the In and Out Parameters
                if (FAILED(hr = DebugAdvanced.Request(DEBUG_REQUEST.EXT_TYPED_DATA_ANSI, (void*)Buffer, TotalSize, (void*)Buffer, TotalSize, null)))
                {
                    OutputVerboseLine("GetFieldVirtualAddress2: DebugAdvanced.Request Failed to get {1}!{2}.{3} hr={0:x}", hr, moduleName, typeName, fieldName);
                    if (offset == 0)
                    {
                        FieldAddress = savedStructAddr;
                        return S_OK;
                    }
                    return hr;
                }

                // Convert the returned buffer to a _EXT_TYPED_Data CLASS (since it wont let me convert to a struct)
                _EXT_TYPED_DATA TypedDataInClassForm = (_EXT_TYPED_DATA)Marshal.PtrToStructure(Buffer, typeof(_EXT_TYPED_DATA));

                OutputDebugLine("Field Offset value {1} = {0} (Struct Address = {2:x}, offset ={3})", TypedDataInClassForm.OutData.Offset, fieldName, savedStructAddr, offset);
                // OutData.Data has our field value.  This will always be a ulong

                FieldAddress = TypedDataInClassForm.OutData.Offset;

                if (FieldAddress < savedStructAddr && offset == 0)
                {
                    FieldAddress = savedStructAddr;
                }

                OutputDebugLine(" *** Final Fixed up Field Offset value {1} = 0x{0:x} ***", FieldAddress, fieldName);

            }
            finally
            {
                Marshal.FreeHGlobal(Buffer);
                Marshal.FreeHGlobal(MemPtr);
            }

            return S_OK;
        }


        /// <summary>
        /// Gets the virtual Address of a field.  Useful for Static Fields
        /// MemberName is smart. It will take value.value, and if it encounters a pointer, it will derefence them.  ie : Value.Value->Value, would look like: Value.Value.Value in the MemberName argument
        /// </summary>
        unsafe public int GetFieldVirtualAddress(ulong moduleBase, uint typeId, string fieldName, UInt64 structureAddress, out ulong FieldAddress)
        {

            OutputDebugLine("GetFieldVirtualAddress: Module: {0:x}, Type {1}, Field {2}, StuctAddr: {3:x}", moduleBase, typeId, fieldName, structureAddress);
            int hr;
            FieldAddress = 0;

            ulong savedStructAddr = structureAddress;
            bool slow = false;
            uint offset = 0;
            uint typeSize = 0;
            hr = GetFieldOffset(moduleBase, typeId, fieldName, out offset);

            if (FAILED(hr))
            {
                OutputVerboseLine("GetFieldOffset returned {0:x}", hr);
                return hr;
            }

            if (offset == 0)
            {
                slow = true;
                OutputDebugLine(" -- GetFieldVirtualAddress: Offset is 0, Slow is TRUE: Module: {0:x}, Type {1}, Field {2}, StuctAddr: {3:x}", moduleBase, typeId, fieldName, structureAddress);
            }
            else
            {
                hr = GetTypeSize(moduleBase, typeId, out typeSize);
                if (typeSize == 0 || offset > typeSize)
                {
                    slow = true;
                }
            }

            OutputDebugLine(" -- GetFieldVirtualAddress: Offset = {0}, TypeSize = {1}", offset, typeSize);

            if (slow == false)
            {
                FieldAddress = structureAddress + offset;
                return S_OK;
            }

            // Make a new Typed Data structure.
            _EXT_TYPED_DATA SymbolTypedData;

            // Fill it in from ModuleBase and TypeID
            // Note, we could use EXT_TDOP_SET_PTR_FROM_TYPE_ID_AND_U64 if this was a pointer to the object
            SymbolTypedData.Operation = _EXT_TDOP.EXT_TDOP_SET_FROM_TYPE_ID_AND_U64;

            SymbolTypedData.InData.ModBase = moduleBase;
            SymbolTypedData.InData.TypeId = typeId;
        
            SymbolTypedData.InData.Offset = structureAddress;

            if (FAILED(hr = DebugAdvanced.Request(DEBUG_REQUEST.EXT_TYPED_DATA_ANSI, &SymbolTypedData, sizeof(_EXT_TYPED_DATA), &SymbolTypedData, sizeof(_EXT_TYPED_DATA), null)))
            {
                OutputVerboseLine("GetFieldVirtualAddress3: DebugAdvanced.Request Failed to get {1:x}!{2}.{3} hr={0:x}", hr, moduleBase, typeId, fieldName);
                if (offset == 0)
                {
                    FieldAddress = savedStructAddr;
                    return S_OK;
                }
                return hr;
            }

            IntPtr Buffer = IntPtr.Zero;
            IntPtr MemPtr = IntPtr.Zero;
            try
            {
                _EXT_TYPED_DATA TemporaryTypedDataForBufferConstruction;

                // Allocate Buffers.

                MemPtr = Marshal.StringToHGlobalAnsi(fieldName);
                int TotalSize = sizeof(_EXT_TYPED_DATA) + fieldName.Length + 1;
                Buffer = Marshal.AllocHGlobal((int)TotalSize);

                // Get_Field. This does all the magic.
                TemporaryTypedDataForBufferConstruction.Operation = _EXT_TDOP.EXT_TDOP_GET_FIELD;

                // Pass in the OutData from the first call to Request(), so it knows what symbol to use
                TemporaryTypedDataForBufferConstruction.InData = SymbolTypedData.OutData;

                // The index of the string will be immediatly following the _EXT_TYPED_DATA structure
                TemporaryTypedDataForBufferConstruction.InStrIndex = (uint)sizeof(_EXT_TYPED_DATA);

                // Source is our _EXT_TYPED_DATA stuction, Dest is our empty allocated buffer

                DebugUtilities.CopyMemory(Buffer, (IntPtr)(&TemporaryTypedDataForBufferConstruction), sizeof(_EXT_TYPED_DATA));

                // Copy the ANSI string of our member name immediatly after the TypedData Structure.
                // Source is our ANSI Buffer, Dest is the byte immediatly after the last byte from the previous copy

                // This fails if we use fieldName.Lengthh, made it +1 to copy the null terminator.
                DebugUtilities.CopyMemory(Buffer + sizeof(_EXT_TYPED_DATA), MemPtr, fieldName.Length + 1);

                // Call Request(), Passing in the buffer we created as the In and Out Parameters
                if (FAILED(hr = DebugAdvanced.Request(DEBUG_REQUEST.EXT_TYPED_DATA_ANSI, (void*)Buffer, TotalSize, (void*)Buffer, TotalSize, null)))
                {
                    OutputVerboseLine("GetFieldVirtualAddress4: DebugAdvanced.Request Failed to get {1:x}!{2}.{3} hr={0:x}", hr, moduleBase, typeId, fieldName);
                    if (offset == 0)
                    {
                        FieldAddress = savedStructAddr;
                        return S_OK;
                    }
                    return hr;
                }

                // Convert the returned buffer to a _EXT_TYPED_Data CLASS (since it wont let me convert to a struct)
                _EXT_TYPED_DATA TypedDataInClassForm = (_EXT_TYPED_DATA)Marshal.PtrToStructure(Buffer, typeof(_EXT_TYPED_DATA));

                OutputDebugLine("Field Offset value {1} = {0} (Struct Address = {2:x}, offset ={3})", TypedDataInClassForm.OutData.Offset, fieldName, savedStructAddr, offset);
                // OutData.Data has our field value.  This will always be a ulong

                FieldAddress = TypedDataInClassForm.OutData.Offset;

                if (FieldAddress < savedStructAddr && offset == 0)
                {
                    FieldAddress = savedStructAddr;
                }

                OutputDebugLine("Final Fixed up Field Offset value {1} = {0}", FieldAddress, fieldName);

            }
            finally
            {
                Marshal.FreeHGlobal(Buffer);
                Marshal.FreeHGlobal(MemPtr);
            }

            return S_OK;
        }

	}
}
