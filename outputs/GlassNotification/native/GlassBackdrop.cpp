// Windows App SDK composition behind the notification's measured WPF foreground.
// No activation changes or private DWM APIs are used.
#include <windows.h>
#include <DispatcherQueue.h>
#include <dwmapi.h>
#include <d3d11_4.h>
#include <d2d1_1.h>
#include <winrt/Windows.Foundation.h>
#include <windows.ui.composition.interop.h>
#include <winrt/Windows.System.h>
#include <winrt/Windows.UI.Composition.h>
#include <winrt/Windows.UI.Composition.Desktop.h>
#include <winrt/Windows.Graphics.DirectX.h>
#include <winrt/Microsoft.UI.Composition.SystemBackdrops.h>
#include <memory>
#include <chrono>
#include <algorithm>
#include <cmath>
#include <windowsx.h>
extern "C" HRESULT __stdcall WindowsAppRuntime_EnsureIsLoaded();
using namespace winrt;
using namespace Windows::UI::Composition;
using namespace Microsoft::UI::Composition::SystemBackdrops;
struct Context {
 HWND hwnd{};HANDLE removedEvent{},removedWait{};DWORD removedToken{};
 Compositor compositor{nullptr};
 Desktop::DesktopWindowTarget target{nullptr};
 DesktopAcrylicController acrylic{nullptr};
 SystemBackdropConfiguration configuration{nullptr};
 int strength{1};
 Windows::UI::Color tintColor{255,245,247,250};
 CompositionGraphicsDevice graphics{nullptr};
 CompositionDrawingSurface surface{nullptr};
 SpriteVisual foreground{nullptr};
 com_ptr<ID3D11Device> d3d;
 com_ptr<ID2D1Device> d2d;
 com_ptr<ID3D11Device5> device5;
 ~Context() noexcept {
  if(removedWait)UnregisterWaitEx(removedWait,INVALID_HANDLE_VALUE);
  if(device5&&removedEvent)device5->UnregisterDeviceRemoved(removedToken);
  if(removedEvent)CloseHandle(removedEvent);
  try{if(acrylic)acrylic.Close();}catch(...){}try{if(target)target.Close();}catch(...){}
 }
 static VOID CALLBACK DeviceRemoved(PVOID context,BOOLEAN) noexcept {
  auto self=static_cast<Context*>(context);
  // No drawing or COM calls on the pool thread. The owned HWND handles fallback.
  PostMessageW(self->hwnd,WM_APP+0x51,0,0);
 }
 void WatchDevice(){
  device5=d3d.as<ID3D11Device5>();
  removedEvent=CreateEventW(nullptr,FALSE,FALSE,nullptr);check_bool(removedEvent!=nullptr);
  check_bool(RegisterWaitForSingleObject(&removedWait,removedEvent,DeviceRemoved,this,INFINITE,WT_EXECUTEONLYONCE));
  check_hresult(device5->RegisterDeviceRemovedEvent(removedEvent,&removedToken));
 }
};
static float AcrylicTintOpacity(int strength) noexcept {return strength==0?.20f:strength==1?.45f:.70f;}
static float AcrylicLuminosityOpacity(int strength) noexcept {return strength==0?.15f:strength==1?.40f:.65f;}
static void ApplyAcrylicAppearance(Context& ctx) {
 // Explicit values keep reconstruction, mode switches and setter order equal.
 // Luminosity=1 previously hid the backdrop, especially with white/black tint.
 if(ctx.configuration)ctx.configuration.IsInputActive(true);
 ctx.acrylic.TintColor(ctx.tintColor);
 ctx.acrylic.FallbackColor(ctx.tintColor);
 ctx.acrylic.TintOpacity(AcrylicTintOpacity(ctx.strength));
 ctx.acrylic.LuminosityOpacity(AcrylicLuminosityOpacity(ctx.strength));
}
// One queue for this UI thread, rather than creating/shutting one down on each
// mode switch. Shutdown belongs to UI-thread exit; no recurring timer is used.
static thread_local Windows::System::DispatcherQueueController queue{nullptr};
static HRESULT CreateCompositionHost(HWND hwnd,int strength,bool useAcrylic,void** result,bool topmost=false) noexcept {
 if(!result)return E_POINTER;
 *result=nullptr;
 try {
  DWORD process=0;
  if(GetWindowThreadProcessId(hwnd,&process)!=GetCurrentThreadId()||process!=GetCurrentProcessId())return E_INVALIDARG;
  check_hresult(WindowsAppRuntime_EnsureIsLoaded());
  auto ctx=std::make_unique<Context>();
  ctx->hwnd=hwnd;
  if(!Windows::System::DispatcherQueue::GetForCurrentThread()) {
   DispatcherQueueOptions options{sizeof(options),DQTYPE_THREAD_CURRENT,DQTAT_COM_NONE};
   check_hresult(CreateDispatcherQueueController(options,reinterpret_cast<ABI::Windows::System::IDispatcherQueueController**>(put_abi(queue))));
  }
  if(useAcrylic&&!DesktopAcrylicController::IsSupported())return E_NOTIMPL;
  BOOL enabled=TRUE;check_hresult(DwmSetWindowAttribute(hwnd,17,&enabled,sizeof(enabled)));
  // Apply to both the permanent card and the temporary GPU host before either
  // is presented, so Windows cannot add a white outline during the handoff.
  COLORREF noBorder=0xFFFFFFFE;DwmSetWindowAttribute(hwnd,34,&noBorder,sizeof(noBorder));
  ctx->compositor=Compositor();
  auto interop=ctx->compositor.as<ABI::Windows::UI::Composition::Desktop::ICompositorDesktopInterop>();
  check_hresult(interop->CreateDesktopWindowTarget(hwnd,topmost,reinterpret_cast<ABI::Windows::UI::Composition::Desktop::IDesktopWindowTarget**>(put_abi(ctx->target))));
  ctx->target.Root(ctx->compositor.CreateContainerVisual());
  if(strength<0||strength>2)return E_INVALIDARG;
  if(useAcrylic){
   ctx->acrylic=DesktopAcrylicController();
   ctx->configuration=SystemBackdropConfiguration();
   ctx->configuration.IsInputActive(true);
   ctx->configuration.IsHighContrast(false);
   ctx->configuration.Theme(SystemBackdropTheme::Default);
   if(!ctx->acrylic.SetTarget(Microsoft::UI::WindowId{reinterpret_cast<uint64_t>(hwnd)},ctx->target))return E_FAIL;
   ctx->acrylic.SetSystemBackdropConfiguration(ctx->configuration);
   ctx->strength=strength;ApplyAcrylicAppearance(*ctx);
  }
  *result=ctx.release();return S_OK;
 }catch(...){return to_hresult();}
}
extern "C" __declspec(dllexport) HRESULT __stdcall CreateBackdrop(HWND hwnd,int strength,void** result) noexcept {return CreateCompositionHost(hwnd,strength,true,result);}
extern "C" __declspec(dllexport) HRESULT __stdcall SetBackdropColor(void* value,int custom,BYTE red,BYTE green,BYTE blue) noexcept {
 if(!value)return E_POINTER;
 try{
  auto& ctx=*static_cast<Context*>(value);if(!ctx.acrylic)return S_OK;
  ctx.tintColor=custom?Windows::UI::Color{255,red,green,blue}:Windows::UI::Color{255,245,247,250};
  ApplyAcrylicAppearance(ctx);
  return S_OK;
 }catch(...){return to_hresult();}
}
extern "C" __declspec(dllexport) HRESULT __stdcall SetBackdropStrength(void* value,int strength) noexcept {
 if(!value||strength<0||strength>2)return E_INVALIDARG;
 try{
  auto& ctx=*static_cast<Context*>(value);
  ctx.strength=strength;ApplyAcrylicAppearance(ctx);
  return S_OK;
 }catch(...){return to_hresult();}
}
extern "C" __declspec(dllexport) void __stdcall CloseBackdrop(void* ctx) noexcept {delete static_cast<Context*>(ctx);}
extern "C" __declspec(dllexport) int __stdcall ReadBackdropState(void* ctx) noexcept {
 try {return static_cast<int>(static_cast<Context*>(ctx)->acrylic.State());}catch(...){return -1;}
}
extern "C" __declspec(dllexport) HRESULT __stdcall ReadBackdropAppearance(void* value,float* tint,float* luminosity,UINT32* color,UINT32* fallback) noexcept {
 if(!value||!tint||!luminosity||!color||!fallback)return E_POINTER;
 try {
  auto& acrylic=static_cast<Context*>(value)->acrylic;if(!acrylic)return E_UNEXPECTED;
  *tint=acrylic.TintOpacity();*luminosity=acrylic.LuminosityOpacity();
  auto pack=[](Windows::UI::Color c){return (UINT32(c.A)<<24)|(UINT32(c.R)<<16)|(UINT32(c.G)<<8)|UINT32(c.B);};
  *color=pack(acrylic.TintColor());*fallback=pack(acrylic.FallbackColor());return S_OK;
 }catch(...){return to_hresult();}
}
extern "C" __declspec(dllexport) HRESULT __stdcall PaintContent(void* value,int width,int height,int stride,const BYTE* pixels) noexcept {
 if(!value||!pixels||width<=0||height<=0||width>8192||height>8192||stride!=width*4)return E_INVALIDARG;
 try {
  auto& ctx=*static_cast<Context*>(value);
  if(!ctx.graphics){
   check_hresult(D3D11CreateDevice(nullptr,D3D_DRIVER_TYPE_HARDWARE,nullptr,D3D11_CREATE_DEVICE_BGRA_SUPPORT,nullptr,0,D3D11_SDK_VERSION,ctx.d3d.put(),nullptr,nullptr));
   ctx.WatchDevice();
   com_ptr<ID2D1Factory1> factory;check_hresult(D2D1CreateFactory(D2D1_FACTORY_TYPE_SINGLE_THREADED,factory.put()));
   auto dxgi=ctx.d3d.as<IDXGIDevice>();check_hresult(factory->CreateDevice(dxgi.get(),ctx.d2d.put()));
   auto interop=ctx.compositor.as<ABI::Windows::UI::Composition::ICompositorInterop>();
   check_hresult(interop->CreateGraphicsDevice(ctx.d2d.get(),reinterpret_cast<ABI::Windows::UI::Composition::ICompositionGraphicsDevice**>(put_abi(ctx.graphics))));
   ctx.foreground=ctx.compositor.CreateSpriteVisual();
   ctx.target.Root().as<ContainerVisual>().Children().InsertAtTop(ctx.foreground);
  }
  ctx.surface=ctx.graphics.CreateDrawingSurface({static_cast<float>(width),static_cast<float>(height)},Windows::Graphics::DirectX::DirectXPixelFormat::B8G8R8A8UIntNormalized,Windows::Graphics::DirectX::DirectXAlphaMode::Premultiplied);
  auto drawing=ctx.surface.as<ABI::Windows::UI::Composition::ICompositionDrawingSurfaceInterop>();
  com_ptr<ID2D1DeviceContext> dc;POINT offset{};
  check_hresult(drawing->BeginDraw(nullptr,__uuidof(ID2D1DeviceContext),dc.put_void(),&offset));
  try {
   dc->SetTransform(D2D1::Matrix3x2F::Translation(static_cast<float>(offset.x),static_cast<float>(offset.y)));
   dc->Clear(D2D1::ColorF(0,0));
   com_ptr<ID2D1Bitmap1> bitmap;
   auto props=D2D1::BitmapProperties1(D2D1_BITMAP_OPTIONS_NONE,D2D1::PixelFormat(DXGI_FORMAT_B8G8R8A8_UNORM,D2D1_ALPHA_MODE_PREMULTIPLIED));
   check_hresult(dc->CreateBitmap(D2D1::SizeU(width,height),pixels,stride,&props,bitmap.put()));
   dc->DrawBitmap(bitmap.get());
  }catch(...){drawing->EndDraw();throw;}
  check_hresult(drawing->EndDraw());
  ctx.foreground.Size({static_cast<float>(width),static_cast<float>(height)});
  ctx.foreground.Brush(ctx.compositor.CreateSurfaceBrush(ctx.surface));
  return S_OK;
 }catch(...){return to_hresult();}
}
extern "C" __declspec(dllexport) HRESULT __stdcall DeviceStatus(void* value) noexcept {
 if(!value)return S_OK;
 auto& ctx=*static_cast<Context*>(value);
 return ctx.d3d?ctx.d3d->GetDeviceRemovedReason():S_OK;
}
extern "C" __declspec(dllexport) void __stdcall ContentOpacity(void* ctx,float opacity) noexcept {
 try {if(!ctx)return;auto& c=*static_cast<Context*>(ctx);if(c.foreground)c.foreground.Opacity(opacity);}catch(...){}
}
// A temporary composition-only host spans the card's travel. The real HWND stays
// at its final position and is cloaked while the compositor moves the whole card.
// There is no frame timer and no recurring SetWindowPos here.
struct SlideHost {
 HWND owner{},hwnd{};std::unique_ptr<Context> drawing;
 CompositionScopedBatch batch{nullptr};event_token batchToken{};bool batchAttached{};
 float from{},to{},width{},height{};bool entering{},cloaked{};double seconds{};
 std::chrono::steady_clock::time_point started;
 float X() const noexcept {
  double t=std::clamp(std::chrono::duration<double>(std::chrono::steady_clock::now()-started).count()/seconds,0.0,1.0);
  double amount=entering?1-std::pow(1-t,3):std::pow(t,3);
  return from+static_cast<float>((to-from)*amount);
 }
 bool PointOnCard(POINT p) const noexcept {float x=X();return p.x>=x&&p.x<x+width&&p.y>=0&&p.y<height;}
 ~SlideHost() noexcept {
  try{if(batch&&batchAttached)batch.Completed(batchToken);}catch(...){}
  // Keep the proxy visible until the real window has been restored. This order
  // also covers cancellation and error paths, not only normal entry completion.
  if(cloaked&&IsWindow(owner)){BOOL value=FALSE;DwmSetWindowAttribute(owner,13,&value,sizeof(value));if(IsWindowVisible(owner))DwmFlush();}
  drawing.reset();if(hwnd)DestroyWindow(hwnd);
 }
};
static LRESULT CALLBACK SlideWindowProc(HWND hwnd,UINT message,WPARAM w,LPARAM l) noexcept {
 auto slide=reinterpret_cast<SlideHost*>(GetWindowLongPtrW(hwnd,GWLP_USERDATA));
 if(message==WM_NCCREATE){slide=static_cast<SlideHost*>(reinterpret_cast<CREATESTRUCTW*>(l)->lpCreateParams);SetWindowLongPtrW(hwnd,GWLP_USERDATA,reinterpret_cast<LONG_PTR>(slide));}
 if(!slide)return DefWindowProcW(hwnd,message,w,l);
 if(message==WM_MOUSEACTIVATE)return MA_NOACTIVATE;
 if(message==WM_NCHITTEST){POINT p{GET_X_LPARAM(l),GET_Y_LPARAM(l)};ScreenToClient(hwnd,&p);return slide->PointOnCard(p)?HTCLIENT:HTTRANSPARENT;}
 if(message==WM_MOUSEMOVE){
  TRACKMOUSEEVENT track{sizeof(track),TME_LEAVE,hwnd,0};TrackMouseEvent(&track);
  PostMessageW(slide->owner,WM_APP+0x54,slide->PointOnCard(POINT{GET_X_LPARAM(l),GET_Y_LPARAM(l)})?1:0,0);return 0;
 }
 if(message==WM_MOUSELEAVE){PostMessageW(slide->owner,WM_APP+0x54,0,0);return 0;}
 if(message==WM_LBUTTONUP){
  POINT p{GET_X_LPARAM(l),GET_Y_LPARAM(l)};
  if(slide->PointOnCard(p))PostMessageW(slide->owner,WM_APP+0x53,0,MAKELPARAM(static_cast<short>(p.x-slide->X()),static_cast<short>(p.y)));
  return 0;
 }
 if(message==WM_APP+0x51){PostMessageW(slide->owner,WM_APP+0x55,0,0);return 0;}
 return DefWindowProcW(hwnd,message,w,l);
}
extern "C" __declspec(dllexport) HRESULT __stdcall BeginCompositionSlideV3(HWND owner,ULONGLONG token,int fromX,int toX,int y,int width,int height,int durationMs,int entering,int acrylic,int strength,int radius,int customColor,BYTE red,BYTE green,BYTE blue,int stride,const BYTE* pixels,void** result) noexcept {
 if(!result)return E_POINTER;*result=nullptr;
 if(!IsWindow(owner)||width<=0||height<=0||durationMs<=0||!pixels)return E_INVALIDARG;
 try {
  DWORD process=0;if(GetWindowThreadProcessId(owner,&process)!=GetCurrentThreadId()||process!=GetCurrentProcessId())return E_INVALIDARG;
  auto slide=std::make_unique<SlideHost>();slide->owner=owner;slide->width=static_cast<float>(width);slide->height=static_cast<float>(height);slide->entering=entering!=0;slide->seconds=durationMs/1000.0;
  HINSTANCE module=GetModuleHandleW(nullptr);WNDCLASSEXW wc{sizeof(wc)};wc.lpfnWndProc=SlideWindowProc;wc.hInstance=module;wc.lpszClassName=L"GlassNotification.CompositionSlide";wc.hCursor=LoadCursorW(nullptr,MAKEINTRESOURCEW(32649));
  if(!RegisterClassExW(&wc)&&GetLastError()!=ERROR_CLASS_ALREADY_EXISTS)throw_last_error();
  int left=(std::min)(fromX,toX),hostWidth=abs(toX-fromX)+width;
  slide->from=static_cast<float>(fromX-left);slide->to=static_cast<float>(toX-left);
  // This must be an UNOWNED popup. Cloaking the real card propagates to owned
  // windows (DWM_CLOAKED_INHERITED), hiding the animation until it is over.
  slide->hwnd=CreateWindowExW(WS_EX_TOOLWINDOW|WS_EX_NOACTIVATE|WS_EX_NOREDIRECTIONBITMAP,wc.lpszClassName,L"",WS_POPUP,left,y,hostWidth,height,nullptr,nullptr,module,slide.get());check_bool(slide->hwnd!=nullptr);
  // Never attach DesktopAcrylicController to this travel-sized HWND. Its system
  // backdrop can cover the whole HWND independently of the visual's clip.
  void* context=nullptr;check_hresult(CreateCompositionHost(slide->hwnd,strength,false,&context,true));slide->drawing.reset(static_cast<Context*>(context));
  auto& ctx=*slide->drawing;auto root=ctx.target.Root();root.Size({slide->width,slide->height});
  auto shape=ctx.compositor.CreateRoundedRectangleGeometry();shape.Size({slide->width,slide->height});shape.CornerRadius({static_cast<float>(radius),static_cast<float>(radius)});root.Clip(ctx.compositor.CreateGeometricClip(shape));root.Offset({slide->from,0,0});
  if(acrylic){
   // Restrict backdrop sampling and tint to the moving card visual. The rest of
   // the travel surface stays transparent, including the path ahead of the card.
   auto background=ctx.compositor.CreateSpriteVisual();background.Size({slide->width,slide->height});background.Brush(ctx.compositor.CreateHostBackdropBrush());root.as<ContainerVisual>().Children().InsertAtBottom(background);
   auto tint=ctx.compositor.CreateSpriteVisual();tint.Size({slide->width,slide->height});
   Windows::UI::Color color{255,customColor?red:static_cast<BYTE>(245),customColor?green:static_cast<BYTE>(247),customColor?blue:static_cast<BYTE>(250)};
   tint.Brush(ctx.compositor.CreateColorBrush(color));tint.Opacity(AcrylicTintOpacity(strength));root.as<ContainerVisual>().Children().InsertAtTop(tint);
  }
  check_hresult(PaintContent(context,width,height,stride,pixels));
  auto motion=ctx.compositor.CreateVector3KeyFrameAnimation();
  auto easing=ctx.compositor.CreateCubicBezierEasingFunction({1.0f/3,entering?1.0f:0.0f},{2.0f/3,entering?1.0f:0.0f});
  motion.InsertKeyFrame(0,{slide->from,0,0});motion.InsertKeyFrame(1,{slide->to,0,0},easing);motion.Duration(std::chrono::milliseconds(durationMs));
  check_bool(SetWindowPos(slide->hwnd,HWND_TOPMOST,0,0,0,0,SWP_NOMOVE|SWP_NOSIZE|SWP_NOACTIVATE|SWP_SHOWWINDOW));
  BOOL cloak=TRUE;check_hresult(DwmSetWindowAttribute(owner,13,&cloak,sizeof(cloak)));slide->cloaked=true;
  DWORD hidden=0;check_hresult(DwmGetWindowAttribute(slide->hwnd,14,&hidden,sizeof(hidden)));
  if(hidden!=0)return E_FAIL; // destructor uncloaks owner; managed caller uses CPU fallback
  slide->batch=ctx.compositor.CreateScopedBatch(CompositionBatchTypes::Animation);
  slide->batchToken=slide->batch.Completed([owner,token](auto const&,auto const&) noexcept {PostMessageW(owner,WM_APP+0x56,static_cast<WPARAM>(token),0);});slide->batchAttached=true;
  slide->started=std::chrono::steady_clock::now();root.StartAnimation(L"Offset",motion);slide->batch.End();
  *result=slide.release();return S_OK;
 }catch(...){return to_hresult();}
}
extern "C" __declspec(dllexport) HRESULT __stdcall PaintSlideContent(void* value,int width,int height,int stride,const BYTE* pixels) noexcept {
 if(!value)return E_POINTER;auto& slide=*static_cast<SlideHost*>(value);
 return PaintContent(slide.drawing.get(),width,height,stride,pixels);
}
extern "C" __declspec(dllexport) void __stdcall EndCompositionSlide(void* value) noexcept {delete static_cast<SlideHost*>(value);}
extern "C" __declspec(dllexport) HRESULT __stdcall ReadSlideBounds(void* value,RECT* bounds) noexcept {
 if(!value||!bounds)return E_POINTER;auto& slide=*static_cast<SlideHost*>(value);RECT host{};
 if(!GetWindowRect(slide.hwnd,&host))return HRESULT_FROM_WIN32(GetLastError());
 bounds->left=host.left+static_cast<LONG>(std::lround(slide.X()));bounds->top=host.top;bounds->right=bounds->left+static_cast<LONG>(slide.width);bounds->bottom=host.top+static_cast<LONG>(slide.height);return S_OK;
}
extern "C" __declspec(dllexport) HRESULT __stdcall SlideVisibility(void* value) noexcept {
 if(!value)return E_POINTER;auto& slide=*static_cast<SlideHost*>(value);DWORD hidden=0;
 HRESULT hr=DwmGetWindowAttribute(slide.hwnd,14,&hidden,sizeof(hidden));if(FAILED(hr))return hr;
 return IsWindowVisible(slide.hwnd)&&hidden==0?S_OK:E_FAIL;
}
extern "C" __declspec(dllexport) HRESULT __stdcall RevealSlideOwner(void* value) noexcept {
 if(!value)return E_POINTER;auto& slide=*static_cast<SlideHost*>(value);
 if(!IsWindow(slide.owner))return E_HANDLE;
 BOOL cloak=FALSE;HRESULT hr=DwmSetWindowAttribute(slide.owner,13,&cloak,sizeof(cloak));if(SUCCEEDED(hr))slide.cloaked=false;return hr;
}
extern "C" __declspec(dllexport) HRESULT __stdcall WaitForSlideOwner(void* value) noexcept {
 if(!value)return E_POINTER;auto& slide=*static_cast<SlideHost*>(value);
 if(slide.cloaked||!IsWindowVisible(slide.owner))return E_FAIL;
 // A one-time presentation barrier during handoff, never a per-frame CPU loop.
 HRESULT hr=DwmFlush();
 if(slide.hwnd){BOOL hideProxy=TRUE;DwmSetWindowAttribute(slide.hwnd,13,&hideProxy,sizeof(hideProxy));ShowWindow(slide.hwnd,SW_HIDE);}
 return hr;
}
