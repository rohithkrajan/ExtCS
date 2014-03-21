
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace DotNetDbg
{


    public class MexLinkedList
    {
        public readonly ulong Head;
        public readonly int EntryOffset;
        public readonly bool skipListHead;


        public MexLinkedList(ulong head, int entryOffset, bool skipListHead)
        {
            Head = head;
            EntryOffset = entryOffset;
            this.skipListHead = skipListHead;
        }

        public IEnumerator<ulong> GetEnumerator()
        {
            return new ListEntryEnumerator(Head, EntryOffset, skipListHead);
        }
    }

    public class ListEntryEnumerator : IEnumerator<ulong>
    {
        private readonly ulong _head;
        private readonly int _entryOffset;
        private readonly bool _skipListHead;

        private bool _isDisposed;
        private ulong _current;
        private ulong _next;
        private bool _isReset;
        private bool _headEnumerated;

        public ListEntryEnumerator(ulong head, int entryOffset, bool skipListHead)
        {
            _head = head;
            _entryOffset = entryOffset;
            _isDisposed = false;
            _skipListHead = skipListHead;
            _headEnumerated = false;

            Reset();
        }

        public ulong Current
        {
            get
            {
                CheckDisposed();

                return (_current == 0) ? 0 : (_current - (ulong)_entryOffset);
            }
        }

        public void Dispose()
        {
            CheckDisposed();

            _isDisposed = true;
        }

        object IEnumerator.Current
        {
            get
            {
                CheckDisposed();

                return Current;
            }
        }

        public bool MoveNext()
        {
            CheckDisposed();

            if (!_isReset && (_next == _head))
                _next = 0;

            _isReset = false;

            _current = _next;

            if (_current != 0)
            {
                for (; ; )
                {
                    int result = MexBase.ds.ReadPointer(_current, out _next);
                    if (result != Mex.S_OK)
                    {
                        //OutputVerboseLine("Cannot read list item at {0:x}", _current);
                        return false;
                    }

                    if (_current == _head)
                    {
                        if (_headEnumerated)
                        {
                            _current = 0;
                            _next = 0;
                            break;
                        }

                        _headEnumerated = true;

                        if (_skipListHead)
                        {
                            _current = _next;
                            continue;
                        }
                    }

                    break;
                }
            }

            return _current != 0;
        }

        public void Reset()
        {
            CheckDisposed();

            _current = 0;
            _next = _head;
            _isReset = true;
            _headEnumerated = false;
        }

        private void CheckDisposed()
        {
            if (_isDisposed)
                throw new ObjectDisposedException(this.GetType().FullName);
        }
    }


    /// 
    /// DebugUtilities
    /// 
    public unsafe partial class DebugUtilities
    {

        /// <summary>
        /// This overload is for walking lists where the Next entry is not at offset 0.  Returns address of all entries in the list.
        /// </summary>
        /// <param name="addr"></param>
        /// <param name="moduleName"></param>
        /// <param name="typeName"></param>
        /// <param name="fieldName"></param>
        /// <param name="skipListHead"></param>
        /// <param name="entries"></param>
        /// <param name="entryOffset"></param>
        /// <returns></returns>
        public int WalkMexLinkedList(ulong addr, string moduleName, string typeName, string fieldName, bool skipListHead, out List<ulong> entries, uint entryOffset = 0)
        {
            entries = new List<ulong>();

            int hr;
            uint fieldOffset;
            if (FAILED(hr = GetFieldOffset(moduleName, typeName, fieldName, out fieldOffset)))
            {
                return hr;
            }            

            return WalkMexLinkedList(addr, skipListHead, out entries, fieldOffset);
        }


        /// <summary>
        /// Walks a linked list using the MexLinkedList implementation
        /// </summary>
        /// <param name="addr">Address of first element in the list</param>
        /// <param name="skipListHead">Skip the list head?  Set to true if the head element is empty.</param>
        /// <param name="entries">Addresses of list entries.</param>
        /// <param name="entryOffset">Offset to apply, if any.</param>
        /// <returns></returns>
        public int WalkMexLinkedList(ulong addr, bool skipListHead, out List<ulong> entries, uint entryOffset = 0)
        {
            entries = new List<ulong>();

            // Checks for duplicates/circular lists
            HashSet<ulong> visited = new HashSet<ulong>();            

            MexLinkedList ItemList = new MexLinkedList(addr, (int)entryOffset, skipListHead);

            //ulong lastItem = 0;
            ulong count    = 0;

            foreach(ulong item in ItemList)
            {
                ++count;

                if(ShouldBreak())
                    break;

                if(visited.Contains(item))
                {
                    // List loop detected
                    // Don't error out, just return the values we have found so far.
                    break;
                }

                visited.Add(item);
                entries.Add(item);
            }

            if (entries.Count > 0)
            {
                return S_OK;
            }
            else return S_FALSE;

        }

        /// Walks a LIST_ENTRY or SINGLE_LIST_ENTRY and returns each node in an array
        /// The output array will contain pointers to the LIST_ENTRY field, not the beginning of the structure! Use the other overload for that.
        /// NOTE! The output list may have entries even if this function fails, as the failure could be due to a memory read after multiple successful reads.
        public List<ulong> WalkList(UInt64 listAddress)
        {
            return WalkList(listAddress, 0);
        }

        /// Walks a LIST_ENTRY or SINGLE_LIST_ENTRY and returns each node in an array
        /// NOTE! The output list may have entries even if this function fails, as the failure could be due to a memory read after multiple successful reads.
        public List<ulong> WalkList(UInt64 listAddress, uint offsetToSubtract)
        {
            int hr = S_OK;
            UInt64 next = listAddress;
            List<ulong> entries = new List<ulong>(32);

            for (; ; )
            {
                if (ShouldBreak())
                {
                    goto Exit;
                }
                else if (FAILED(hr = ReadPointer(next, out next)))
                {
                    goto Exit;
                }
                else if ((next == 0) || (next == listAddress))
                {
                    goto Exit;
                }
                entries.Add(next - offsetToSubtract);
            }

        Exit:
            if (FAILED(hr))
                ThrowExceptionHere(hr);
            return entries;
        }



        /// Walks a LIST_ENTRY or SINGLE_LIST_ENTRY and returns each node in an array
        /// The output array will contain pointers to the LIST_ENTRY field, not the beginning of the structure! Use the other overload for that.
        /// NOTE! The output list may have entries even if this function fails, as the failure could be due to a memory read after multiple successful reads.
        public int WalkList(UInt64 listAddress, out UInt64[] listEntries)
        {
            return WalkList(listAddress, out listEntries, 0);
        }

        /// Walks a LIST_ENTRY or SINGLE_LIST_ENTRY and returns each node in an array
        /// NOTE! The output list may have entries even if this function fails, as the failure could be due to a memory read after multiple successful reads.
        public int WalkList(UInt64 listAddress, out UInt64[] listEntries, uint offsetToSubtract)
        {
            int hr = S_OK;
            UInt64 next = listAddress;
            List<UInt64> entries = new List<UInt64>(32);

            for (; ; )
            {
                if (ShouldBreak())
                {
                    goto Exit;
                }
                else if (FAILED(hr = ReadPointer(next, out next)))
                {
                    goto Exit;
                }
                else if ((next == 0) || (next == listAddress))
                {
                    goto Exit;
                }
                entries.Add(next - offsetToSubtract);
            }

        Exit:
            listEntries = entries.ToArray();
            return hr;
        }


        /// Walks a LIST_ENTRY or SINGLE_LIST_ENTRY for the specified count of entries and returns each node in an array
        /// NOTE! The output list may have entries even if this function fails, as the failure could be due to a memory read after multiple successful reads.
        public int WalkList(UInt64 listAddress, out UInt64[] listEntries, uint offsetToSubtract, uint count)
        {
            int hr = S_OK;
            UInt64 next = listAddress;
            List<UInt64> entries = new List<UInt64>(32);

            for (uint i = 1; i <= count; i++)
            {
                if (ShouldBreak())
                {
                    goto Exit;
                }
                else if (FAILED(hr = ReadPointer(next, out next)))
                {
                    goto Exit;
                }
                else if ((next == 0) || (next == listAddress))
                {
                    goto Exit;
                }
                entries.Add(next - offsetToSubtract);
            }

        Exit:
            listEntries = entries.ToArray();
            return hr;
        }

        /// Walks a LIST_ENTRY or SINGLE_LIST_ENTRY and returns each node in an array
        /// This overload takes a type and field name for the list entries and automatically subtracts the offsets
        /// NOTE! The output list may have entries even if this function fails, as the failure could be due to a memory read after multiple successful reads.
        public int WalkList(UInt64 listAddress, out UInt64[] listEntries, string moduleName, string entryTypeName, string entryLinkFieldName)
        {
            int hr;
            uint entryLinkFieldOffset;
            if (FAILED(hr = GetFieldOffset(moduleName, entryTypeName, entryLinkFieldName, out entryLinkFieldOffset)))
            {
                listEntries = new UInt64[0];
                return hr;
            }
            return WalkList(listAddress, out listEntries, entryLinkFieldOffset);
        }

        /// Walks a LIST_ENTRY or SINGLE_LIST_ENTRY embedded in a structure
        /// The output array will contain pointers to the LIST_ENTRY field, not the beginning of the structure! Use the other overload for that.
        /// NOTE! The output list may have entries even if this function fails, as the failure could be due to a memory read after multiple successful reads.
        public int WalkListInStructure(string moduleName, string typeName, string fieldName, UInt64 address, out UInt64[] listEntries)
        {
            return WalkListInStructure(moduleName, typeName, fieldName, address, out listEntries, null, null);
        }

        /// Walks a LIST_ENTRY or SINGLE_LIST_ENTRY embedded in a structure
        /// This overload takes a type and field name for the list entries and automatically subtracts the offsets
        /// NOTE! The output list may have entries even if this function fails, as the failure could be due to a memory read after multiple successful reads.
        public int WalkListInStructure(string moduleName, string typeName, string fieldName, UInt64 address, out UInt64[] listEntries, string entryTypeName, string entryLinkFieldName)
        {
            int hr;
            uint fieldOffset, entryLinkFieldOffset;
            if (FAILED(hr = GetFieldOffset(moduleName, typeName, fieldName, out fieldOffset)))
            {
                listEntries = new UInt64[0];
                return hr;
            }
            if ((entryTypeName != null) && (entryLinkFieldName != null))
            {
                if (FAILED(hr = GetFieldOffset(moduleName, entryTypeName, entryLinkFieldName, out entryLinkFieldOffset)))
                {
                    listEntries = new UInt64[0];
                    return hr;
                }
            }
            else
            {
                entryLinkFieldOffset = 0;
            }
            return WalkList(address + fieldOffset, out listEntries, entryLinkFieldOffset);
        }

        /// Walks a LIST_ENTRY or SINGLE_LIST_ENTRY stored in a global
        /// The output array will contain pointers to the LIST_ENTRY field, not the beginning of the structure! Use the other overload for that.
        /// NOTE! The output list may have entries even if this function fails, as the failure could be due to a memory read after multiple successful reads.
        public int WalkListGlobal(string moduleName, string globalName, out UInt64[] listEntries)
        {
            return WalkListGlobal(moduleName, globalName, out listEntries, null, null);
        }


        /// Walks a LIST_ENTRY or SINGLE_LIST_ENTRY stored in a global for the specified count
        /// The output array will contain pointers to the LIST_ENTRY field, not the beginning of the structure! Use the other overload for that.
        /// NOTE! The output list may have entries even if this function fails, as the failure could be due to a memory read after multiple successful reads.
        public int WalkListGlobal(string moduleName, string globalName, out UInt64[] listEntries, uint count)
        {
            return WalkListGlobal(moduleName, globalName, out listEntries, null, null, count);
        }

        /// Walks a LIST_ENTRY or SINGLE_LIST_ENTRY stored in a global
        /// This overload takes a type and field name for the list entries and automatically subtracts the offsets
        /// NOTE! The output list may have entries even if this function fails, as the failure could be due to a memory read after multiple successful reads.
        public int WalkListGlobal(string moduleName, string globalName, out UInt64[] listEntries, string entryTypeName, string entryLinkFieldName)
        {
            int hr;
            UInt64 globalAddress;
            uint entryLinkFieldOffset;
            if (FAILED(hr = GetGlobalAddress(moduleName, globalName, out globalAddress)))
            {
                listEntries = new UInt64[0];
                return hr;
            }
            if ((entryTypeName != null) && (entryLinkFieldName != null))
            {
                if (FAILED(hr = GetFieldOffset(moduleName, entryTypeName, entryLinkFieldName, out entryLinkFieldOffset)))
                {
                    listEntries = new UInt64[0];
                    return hr;
                }
            }
            else
            {
                entryLinkFieldOffset = 0;
            }
            return WalkList(globalAddress, out listEntries, entryLinkFieldOffset);
        }


        /// Walks a LIST_ENTRY or SINGLE_LIST_ENTRY stored in a global for the specified count of entries (count should NOT be 0)
        /// This overload takes a type and field name for the list entries and automatically subtracts the offsets
        /// NOTE! The output list may have entries even if this function fails, as the failure could be due to a memory read after multiple successful reads.
        public int WalkListGlobal(string moduleName, string globalName, out UInt64[] listEntries, string entryTypeName, string entryLinkFieldName, uint count)
        {
            int hr;
            UInt64 globalAddress;
            uint entryLinkFieldOffset;
            if (FAILED(hr = GetGlobalAddress(moduleName, globalName, out globalAddress)))
            {
                listEntries = new UInt64[0];
                return hr;
            }
            if ((entryTypeName != null) && (entryLinkFieldName != null))
            {
                if (FAILED(hr = GetFieldOffset(moduleName, entryTypeName, entryLinkFieldName, out entryLinkFieldOffset)))
                {
                    listEntries = new UInt64[0];
                    return hr;
                }
            }
            else
            {
                entryLinkFieldOffset = 0;
            }
            return WalkList(globalAddress, out listEntries, entryLinkFieldOffset, count);
        }



    }
}
