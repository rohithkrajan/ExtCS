
#include <windows.h>
#include <dbgeng.h>
#include <imagehlp.h>

//#define DBG_COMMAND_EXCEPTION ( (DWORD)0x40010009L)


#include <strsafe.h>

#define MAJOR_VERSION 1
#define MINOR_VERSION 0

//WINDBG_EXTENSION_APIS64 ExtensionApis;
#ifndef __OUT_HPP__

#define __OUT_HPP__

#include <string>

#include <sstream>

#define EXPORT extern "C"


class StdioOutputCallbacks : public IDebugOutputCallbacks
{
private:

                        std::string m_OutputBuffer;

                        //

                        //This buffer holds the output from the command execution.

                        //

                        CHAR m_OutPutBuffer[4096];

public:

                        void InitOutPutBuffer();

                        std::string GetOutputBuffer()

                        {

                                                return m_OutputBuffer;

                        };

                        void ClearOutPutBuffer()              

                        {

                                                m_OutputBuffer = "";

                        };

    STDMETHOD(QueryInterface)(

        THIS_

        IN REFIID InterfaceId,

        OUT PVOID* Interface

        );

    STDMETHOD_(ULONG, AddRef)(

        THIS

        );

    STDMETHOD_(ULONG, Release)(

        THIS

        );

    // IDebugOutputCallbacks.

    STDMETHOD(Output)(

        THIS_

        IN ULONG Mask,

        IN PCSTR Text

        );

};

extern StdioOutputCallbacks g_OutputCb;

#endif // #ifndef __OUT_HPP__


extern "C" HRESULT help(IDebugClient* debugClient, PCSTR args);
extern "C" HRESULT clearscriptsession(IDebugClient* debugClient, PCSTR args);
extern "C" HRESULT debug(IDebugClient* debugClient, PCSTR args);

#define DECLARE_API(extensionName)                                        \
BSTR bstr_##extensionName = SysAllocString(L#extensionName);                 \
EXPORT HRESULT CALLBACK extensionName(IDebugClient* debugClient, PCSTR args) \
{                                                                            \
	return CallManagedCode((char *)args);                                       \
}


void DbgPrintf(const wchar_t* format, ...);

