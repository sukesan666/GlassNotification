// Windows notification listener. All entry points are called from the WPF UI thread.
#include <windows.h>
#include <appmodel.h>
#include <winrt/Windows.Foundation.h>
#include <winrt/Windows.Foundation.Collections.h>
#include <winrt/Windows.UI.Notifications.h>
#include <winrt/Windows.UI.Notifications.Management.h>
#include <winrt/Windows.ApplicationModel.h>
#include <winrt/Windows.ApplicationModel.Core.h>
#include <winrt/Windows.Management.Deployment.h>
#include <winrt/Windows.Data.Json.h>
#include <winrt/Windows.Data.Xml.Dom.h>
#include <winrt/Windows.Storage.h>
#include <winrt/Windows.Storage.Streams.h>
#include <winrt/Windows.Security.Cryptography.h>
#include <memory>
#include <string>
#include <vector>
#include <map>
#include <algorithm>
using namespace winrt;
using namespace Windows::UI::Notifications;
using namespace Windows::UI::Notifications::Management;
using namespace Windows::Foundation;
using namespace Windows::Data::Json;
static Windows::Foundation::IAsyncOperationWithProgress<Windows::Management::Deployment::DeploymentResult,Windows::Management::Deployment::DeploymentProgress> registration{nullptr};
using namespace Windows::Storage::Streams;
struct AppLogo {
 IAsyncOperation<IRandomAccessStreamWithContentType> opening{nullptr};IAsyncOperation<uint32_t> loading{nullptr};
 IRandomAccessStreamWithContentType stream{nullptr};DataReader reader{nullptr};hstring encoded;bool done{},sent{};
 explicit AppLogo(RandomAccessStreamReference const& logo){try{if(!logo){done=true;return;}opening=logo.OpenReadAsync();if(!opening)done=true;}catch(...){done=true;}}
 void Close() noexcept {try{if(opening)opening.Cancel();}catch(...){}try{if(loading)loading.Cancel();}catch(...){}try{if(reader)reader.Close();}catch(...){}try{if(stream)stream.Close();}catch(...){}opening=nullptr;loading=nullptr;reader=nullptr;stream=nullptr;}
 ~AppLogo(){Close();}
 bool Advance(){if(done)return false;try{
  if(opening){if(opening.Status()==AsyncStatus::Started)return true;stream=opening.GetResults();opening=nullptr;if(!stream){done=true;Close();return false;}auto size=stream.Size();if(size==0||size>65536){done=true;Close();return false;}auto input=stream.GetInputStreamAt(0);if(!input){done=true;Close();return false;}reader=DataReader(input);loading=reader.LoadAsync(static_cast<uint32_t>(size));if(!loading){done=true;Close();return false;}if(loading.Status()==AsyncStatus::Started)return true;}
  if(loading){if(loading.Status()==AsyncStatus::Started)return true;auto size=loading.GetResults();loading=nullptr;std::vector<uint8_t> bytes(size);reader.ReadBytes(bytes);encoded=Windows::Security::Cryptography::CryptographicBuffer::EncodeToBase64String(Windows::Security::Cryptography::CryptographicBuffer::CreateFromByteArray(bytes));done=true;Close();return false;}
  done=true;return false;
 }catch(...){done=true;Close();return false;}}
 void Timeout(){if(!done){done=true;Close();}}
};
struct ListenerContext {
 DWORD thread=GetCurrentThreadId();
 UserNotificationListener listener{nullptr};
 IAsyncOperation<UserNotificationListenerAccessStatus> access{nullptr};
 IAsyncOperation<Collections::IVectorView<UserNotification>> read{nullptr};
 Collections::IVectorView<UserNotification> snapshot{nullptr};ULONGLONG logoDeadline{};
 std::map<std::wstring,std::unique_ptr<AppLogo>> logos;
 std::wstring json;
};
static void PrepareLogos(ListenerContext& c){

 std::vector<std::pair<int64_t,Windows::ApplicationModel::AppInfo>> sources;
 for(auto const& n:c.snapshot)try{sources.emplace_back(n.CreationTime().time_since_epoch().count(),n.AppInfo());}catch(hresult_error const&){}
 std::stable_sort(sources.begin(),sources.end(),[](auto const& a,auto const& b){return a.first>b.first;});
 std::map<std::wstring,Windows::ApplicationModel::AppInfo> selected;
 for(auto const& entry:sources){try{if(!entry.second)continue;std::wstring id=entry.second.AppUserModelId().c_str();if(!id.empty()&&id.size()<=1024&&selected.size()<64)selected.emplace(id,entry.second);}catch(hresult_error const&){}}
 for(auto it=c.logos.begin();it!=c.logos.end();)if(selected.find(it->first)==selected.end())it=c.logos.erase(it);else ++it;
 for(auto const& entry:selected)if(c.logos.find(entry.first)==c.logos.end()){
  RandomAccessStreamReference logo{nullptr};
  try{auto info=entry.second.DisplayInfo();if(info)logo=info.GetLogo(Size{64,64});}catch(...){}
  c.logos.emplace(entry.first,std::make_unique<AppLogo>(logo));
 }
 c.logoDeadline=GetTickCount64()+2000;

}
static ListenerContext& ctx(void* p){if(!p)throw hresult_invalid_argument();auto& c=*static_cast<ListenerContext*>(p);if(c.thread!=GetCurrentThreadId())throw hresult_wrong_thread();return c;}
#define API extern "C" __declspec(dllexport) HRESULT __stdcall
API NotificationsCreate(void** result){if(!result)return E_POINTER;*result=nullptr;try{
 uint32_t length=0;if(GetCurrentPackageFullName(&length,nullptr)==APPMODEL_ERROR_NO_PACKAGE)return HRESULT_FROM_WIN32(APPMODEL_ERROR_NO_PACKAGE);
 auto c=std::make_unique<ListenerContext>();c->listener=UserNotificationListener::Current();*result=c.release();return S_OK;
 }catch(...){return to_hresult();}}
API NotificationsAccess(void* p,int request,int* status){if(!status)return E_POINTER;try{auto& c=ctx(p);if(request){if(!c.access)c.access=c.listener.RequestAccessAsync();}
 if(c.access){if(c.access.Status()==AsyncStatus::Started){*status=-1;return S_OK;}*status=static_cast<int>(c.access.GetResults());c.access=nullptr;}
 else *status=static_cast<int>(c.listener.GetAccessStatus());return S_OK;}catch(...){return to_hresult();}}
API NotificationsRead(void* p,int* ready,const wchar_t** json){if(!ready||!json)return E_POINTER;*ready=0;*json=nullptr;try{auto& c=ctx(p);

 if(c.listener.GetAccessStatus()!=UserNotificationListenerAccessStatus::Allowed)return E_ACCESSDENIED;
 if(!c.snapshot){
  if(!c.read)c.read=c.listener.GetNotificationsAsync(NotificationKinds::Toast);
  if(c.read.Status()==AsyncStatus::Started)return S_OK;
  c.snapshot=c.read.GetResults();c.read=nullptr;PrepareLogos(c);
 }
 bool waiting=false;
 for(auto const& entry:c.logos)waiting=entry.second->Advance()||waiting;
 if(waiting&&GetTickCount64()<c.logoDeadline)return S_OK;
 if(waiting)for(auto const& entry:c.logos)entry.second->Timeout();
 JsonArray result,iconKeys;JsonObject icons;
 for(auto const& entry:c.logos)iconKeys.Append(JsonValue::CreateStringValue(entry.first));
 for(auto const& entry:c.logos)if(entry.second->done&&!entry.second->sent){icons.Insert(entry.first,JsonValue::CreateStringValue(entry.second->encoded));entry.second->sent=true;}
 for(auto const& n:c.snapshot){try{
  auto app=n.AppInfo();if(!app)continue;auto display=app.DisplayInfo();hstring appName;if(display)appName=display.DisplayName();JsonObject item;item.Insert(L"Id",JsonValue::CreateNumberValue(n.Id()));item.Insert(L"App",JsonValue::CreateStringValue(appName));
  item.Insert(L"AppId",JsonValue::CreateStringValue(app.AppUserModelId()));
  auto binding=n.Notification().Visual().GetBinding(KnownNotificationBindings::ToastGeneric());hstring title;std::wstring body;
  if(binding){auto text=binding.GetTextElements();for(uint32_t i=0;i<text.Size();i++){if(i==0)title=text.GetAt(i).Text();else{if(!body.empty())body+=L"\n";body+=text.GetAt(i).Text();}}}
  item.Insert(L"Title",JsonValue::CreateStringValue(title));item.Insert(L"Body",JsonValue::CreateStringValue(body));
  item.Insert(L"Ticks",JsonValue::CreateNumberValue(static_cast<double>(n.CreationTime().time_since_epoch().count())));result.Append(item);
 }catch(hresult_error const&){/* Item removed or unavailable during snapshot. */}}
 JsonObject root;root.Insert(L"Notifications",result);root.Insert(L"Icons",icons);root.Insert(L"IconKeys",iconKeys);c.json=root.Stringify();c.snapshot=nullptr;*json=c.json.c_str();*ready=1;return S_OK;
 }catch(...){try{ctx(p).read=nullptr;ctx(p).snapshot=nullptr;}catch(...){}return to_hresult();}}
API NotificationsDestroy(void* p){try{auto& c=ctx(p);if(c.access)c.access.Cancel();if(c.read)c.read.Cancel();delete &c;return S_OK;}catch(...){return to_hresult();}}
API NotificationsSendTest(){try{
 Windows::Data::Xml::Dom::XmlDocument xml;xml.LoadXml(L"<toast><visual><binding template='ToastGeneric'><text>Glass OS連動テスト</text><text>Windowsから届いた通知です。</text></binding></visual><audio src='ms-winsoundevent:Notification.Default'/></toast>");
 // Exercise the same OS payload/image path as real notifications, using bundled local images.
 std::vector<wchar_t> executable(32768);auto length=GetModuleFileNameW(nullptr,executable.data(),static_cast<DWORD>(executable.size()));
 check_bool(length>0&&length<executable.size());std::wstring folder(executable.data(),length);folder.resize(folder.find_last_of(L"\\/"));
 std::replace(folder.begin(),folder.end(),L'\\',L'/');
 auto binding=xml.SelectSingleNode(L"/toast/visual/binding");
 for(auto const& image:std::vector<std::pair<std::wstring,std::wstring>>{{L"appLogoOverride",L"Square44x44Logo.png"},{L"hero",L"Square150x150Logo.png"}}){
  auto element=xml.CreateElement(L"image");element.SetAttribute(L"placement",image.first);element.SetAttribute(L"src",Uri(L"file:///"+folder+L"/assets/"+image.second).AbsoluteUri());binding.AppendChild(element);
 }
 ToastNotificationManager::CreateToastNotifier().Show(ToastNotification(xml));return S_OK;
 }catch(...){return to_hresult();}}
API NotificationsPackageInfo(const wchar_t** text){static std::wstring value;if(!text)return E_POINTER;try{
 Windows::Management::Deployment::PackageManager manager;value.clear();for(auto const& p:manager.FindPackagesForUser(L"")){if(p.Id().Name()==L"GlassNotification.OSLink"){value=p.InstalledLocation().Path();break;}}
 *text=value.c_str();return S_OK;
 }catch(...){return to_hresult();}}
API NotificationsRegisteredAppId(const wchar_t** text){static std::wstring value;if(!text)return E_POINTER;try{
 Windows::Management::Deployment::PackageManager manager;value.clear();for(auto const& p:manager.FindPackagesForUser(L"")){if(p.Id().Name()==L"GlassNotification.OSLink"){value=std::wstring(p.Id().FamilyName().c_str())+L"!App";break;}}
 *text=value.c_str();return S_OK;
 }catch(...){return to_hresult();}}
API NotificationsRegister(const wchar_t* manifest,const wchar_t* folder,int* ready){if(!ready)return E_POINTER;*ready=0;try{
 using namespace Windows::Management::Deployment;
 if(!registration){if(!manifest||!folder)return E_INVALIDARG;
  std::wstring source=manifest;bool signedPackage=source.size()>5&&_wcsicmp(source.c_str()+source.size()-5,L".msix")==0;
  // Full signed packages carry their own binaries. Only the legacy loose manifest uses an external location.
  if(signedPackage){AddPackageOptions options;registration=PackageManager().AddPackageByUriAsync(Uri(manifest),options);}
  else{RegisterPackageOptions options;options.ExternalLocationUri(Uri(folder));options.DeveloperMode(true);registration=PackageManager().RegisterPackageByUriAsync(Uri(manifest),options);}
  return S_OK;}
 if(registration.Status()==AsyncStatus::Started)return S_OK;auto result=registration.GetResults();registration=nullptr;check_hresult(result.ExtendedErrorCode());*ready=1;return S_OK;
 }catch(...){registration=nullptr;return to_hresult();}}
