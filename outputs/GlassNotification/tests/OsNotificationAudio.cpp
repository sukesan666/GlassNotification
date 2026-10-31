// Test-only WASAPI loopback statistics. No microphone or audio file capture.
#include <windows.h>
#include <mmdeviceapi.h>
#include <audioclient.h>
#include <audiopolicy.h>
#include <endpointvolume.h>
#include <ksmedia.h>
#include <wrl/client.h>
#include <cmath>
#include <algorithm>
using Microsoft::WRL::ComPtr;
struct AudioProbe {ComPtr<IAudioClient> client;ComPtr<IAudioCaptureClient> capture;ComPtr<IAudioSessionManager2> sessions;WAVEFORMATEX* format{};double peak{},systemPeak{};int systemSessions{};unsigned long long samples{};~AudioProbe(){if(client)client->Stop();if(format)CoTaskMemFree(format);}};
extern "C" __declspec(dllexport) HRESULT __stdcall AudioProbeStart(void** result){if(!result)return E_POINTER;*result=nullptr;auto p=new AudioProbe;HRESULT hr;
 ComPtr<IMMDeviceEnumerator> devices;ComPtr<IMMDevice> device;
 if(FAILED(hr=CoCreateInstance(__uuidof(MMDeviceEnumerator),nullptr,CLSCTX_ALL,IID_PPV_ARGS(&devices)))||FAILED(hr=devices->GetDefaultAudioEndpoint(eRender,eConsole,&device))||FAILED(hr=device->Activate(__uuidof(IAudioClient),CLSCTX_ALL,nullptr,&p->client))||FAILED(hr=p->client->GetMixFormat(&p->format))||FAILED(hr=p->client->Initialize(AUDCLNT_SHAREMODE_SHARED,AUDCLNT_STREAMFLAGS_LOOPBACK,1000000,0,p->format,nullptr))||FAILED(hr=p->client->GetService(IID_PPV_ARGS(&p->capture)))||FAILED(hr=p->client->Start())){delete p;return hr;}
 if(FAILED(hr=device->Activate(__uuidof(IAudioSessionManager2),CLSCTX_ALL,nullptr,&p->sessions))){delete p;return hr;}
 *result=p;return S_OK;
}
extern "C" __declspec(dllexport) HRESULT __stdcall AudioProbePoll(void* value,double* peak,unsigned long long* samples,double* systemPeak,int* systemSessions){if(!value||!peak||!samples||!systemPeak||!systemSessions)return E_POINTER;auto p=static_cast<AudioProbe*>(value);UINT32 frames=0;HRESULT hr;
 ComPtr<IAudioSessionEnumerator> sessions;if(SUCCEEDED(p->sessions->GetSessionEnumerator(&sessions))){int count=0;sessions->GetCount(&count);int found=0;for(int i=0;i<count;i++){ComPtr<IAudioSessionControl> control;ComPtr<IAudioSessionControl2> control2;ComPtr<IAudioMeterInformation> meter;if(SUCCEEDED(sessions->GetSession(i,&control))&&SUCCEEDED(control.As(&control2))&&control2->IsSystemSoundsSession()==S_OK&&SUCCEEDED(control.As(&meter))){float level=0;if(SUCCEEDED(meter->GetPeakValue(&level))){p->systemPeak=(std::max)(p->systemPeak,static_cast<double>(level));found++;}}}p->systemSessions=(std::max)(p->systemSessions,found);}
 bool floating=p->format->wFormatTag==WAVE_FORMAT_IEEE_FLOAT||(p->format->wFormatTag==WAVE_FORMAT_EXTENSIBLE&&reinterpret_cast<WAVEFORMATEXTENSIBLE*>(p->format)->SubFormat==KSDATAFORMAT_SUBTYPE_IEEE_FLOAT);
 if(!floating||p->format->wBitsPerSample!=32)return AUDCLNT_E_UNSUPPORTED_FORMAT;
 while(SUCCEEDED(hr=p->capture->GetNextPacketSize(&frames))&&frames){BYTE* bytes;DWORD flags;UINT32 count;if(FAILED(hr=p->capture->GetBuffer(&bytes,&count,&flags,nullptr,nullptr)))return hr;
  if(!(flags&AUDCLNT_BUFFERFLAGS_SILENT)){auto data=reinterpret_cast<float*>(bytes);for(UINT32 i=0;i<count*p->format->nChannels;i++)p->peak=(std::max)(p->peak,std::abs(static_cast<double>(data[i])));}
  p->samples+=count;if(FAILED(hr=p->capture->ReleaseBuffer(count)))return hr;
 }
 *peak=p->peak;*samples=p->samples;*systemPeak=p->systemPeak;*systemSessions=p->systemSessions;return hr;
}
extern "C" __declspec(dllexport) void __stdcall AudioProbeEnd(void* value){delete static_cast<AudioProbe*>(value);}
