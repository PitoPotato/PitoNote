// PitoNote native snapping. Only subclasses this skin's window on Rainmeter's UI thread.
// No global input hooks, polling timers, worker threads or separate resident process.
#define WIN32_LEAN_AND_MEAN
#define NOMINMAX
#include <windows.h>
#include <commctrl.h>
#include <new>
#include <stdio.h>
#include "SnapGeometry.h"

// Minimal declarations for Rainmeter's public plugin ABI. No Rainmeter implementation is copied.
using GetApi=const void* (__stdcall*)(void*,int);
using ReadNumber=double (__stdcall*)(void*,LPCWSTR,double);
using ReadString=LPCWSTR (__stdcall*)(void*,LPCWSTR,LPCWSTR,BOOL);
static GetApi rmGet;
static ReadNumber rmNumber;
static ReadString rmString;
struct Measure {
    void* rm; HWND window; Measure* next; WCHAR scope[260];
    bool attached,enabled,dragging,moveIntent,resizing,diagnostics;
    int distance,gap; POINT resizeOrigin; PitoSnap::Session session; unsigned snapped;
};
static Measure* measures;
static int rounded(double x){return (int)(x>=0?x+0.5:x-0.5);}
static double scale(Measure* m){auto p=(const float*)rmGet(m->rm,5);return p&&*p>0?*p:1.0;}
static PitoSnap::Box paper(Measure* m,RECT r){int inset=rounded(8*scale(m));return {(int)r.left+inset,(int)r.top+inset,(int)(r.right-r.left)-2*inset,(int)(r.bottom-r.top)-2*inset};}
static LRESULT CALLBACK subclass(HWND hwnd,UINT msg,WPARAM w,LPARAM l,UINT_PTR,DWORD_PTR context){
    auto m=(Measure*)context;
    if(msg==WM_SYSCOMMAND&&((w&0xfff0)==SC_MOVE||(w&0xfff0)==SC_SIZE)){
        m->moveIntent=(w&0xfff0)==SC_MOVE;m->dragging=false;m->session.clear();
        auto result=DefSubclassProc(hwnd,msg,w,l);m->moveIntent=m->dragging=false;m->session.clear();return result;
    }
    if(msg==WM_ENTERSIZEMOVE&&m->moveIntent){m->dragging=true;m->session.clear();}
    if(msg==WM_EXITSIZEMOVE){m->dragging=false;m->session.clear();}
    if(msg==WM_NCDESTROY){RemoveWindowSubclass(hwnd,subclass,(UINT_PTR)m);m->attached=false;m->window=nullptr;}
    auto result=DefSubclassProc(hwnd,msg,w,l);
    if(msg!=WM_WINDOWPOSCHANGING||!m->attached)return result;
    auto wp=(WINDOWPOS*)l;
    if(wp->flags&SWP_NOMOVE)return result;
    if(m->resizing){wp->x=m->resizeOrigin.x;wp->y=m->resizeOrigin.y;return result;}
    if(!m->dragging||!m->enabled)return result;
    bool bypass=(GetAsyncKeyState(VK_CONTROL)&0x8000)||(GetAsyncKeyState(VK_SHIFT)&0x8000);
    if(bypass){m->session.clear();return result;}
    RECT current;if(!GetWindowRect(hwnd,&current))return result;
    int width=(wp->flags&SWP_NOSIZE)?current.right-current.left:wp->cx;
    int height=(wp->flags&SWP_NOSIZE)?current.bottom-current.top:wp->cy;
    RECT proposed={wp->x,wp->y,wp->x+width,wp->y+height};
    MONITORINFO monitor={sizeof(monitor)};
    if(!GetMonitorInfo(MonitorFromRect(&proposed,MONITOR_DEFAULTTONEAREST),&monitor))return result;
    double zoom=scale(m);int inset=rounded(8*zoom),gap=rounded(m->gap*zoom);
    auto moving=paper(m,proposed);
    PitoSnap::Peer peers[1000];int count=0;
    for(auto other=measures;other&&count<1000;other=other->next){
        RECT r;
        if(other!=m&&other->attached&&lstrcmpW(m->scope,other->scope)==0&&IsWindowVisible(other->window)&&!IsIconic(other->window)&&GetWindowRect(other->window,&r))
            peers[count++]={paper(other,r),(uintptr_t)other->window};
    }
    PitoSnap::Box work={(int)monitor.rcWork.left+inset,(int)monitor.rcWork.top+inset,(int)(monitor.rcWork.right-monitor.rcWork.left)-2*inset,(int)(monitor.rcWork.bottom-monitor.rcWork.top)-2*inset};
    // The user-facing gap is measured from the visible paper edge, not the shadow edge.
    // Note spacing uses the whole gap; the screen margin accounts for the inset below.
    auto snapped=PitoSnap::resolve(moving,peers,count,work,rounded(m->distance*zoom),gap,m->session,false,PitoSnap::max(0,gap-inset));
    if(snapped.x!=moving.x||snapped.y!=moving.y){wp->x=snapped.x-inset;wp->y=snapped.y-inset;m->snapped++;}
    return result;
}
extern "C" __declspec(dllexport) void Initialize(void** data,void* rm){
    auto module=GetModuleHandleW(L"Rainmeter.dll");
    rmGet=(GetApi)GetProcAddress(module,"RmGet");rmNumber=(ReadNumber)GetProcAddress(module,"RmReadFormula");rmString=(ReadString)GetProcAddress(module,"RmReadString");
    if(!rmGet||!rmNumber||!rmString){*data=nullptr;return;}
    auto memory=HeapAlloc(GetProcessHeap(),0,sizeof(Measure));if(!memory){*data=nullptr;return;}
    auto m=new(memory) Measure{};
    m->rm=rm;m->window=(HWND)rmGet(rm,4);m->next=measures;measures=m;
    m->attached=!!SetWindowSubclass(m->window,subclass,(UINT_PTR)m,(DWORD_PTR)m);*data=m;
}
extern "C" __declspec(dllexport) void Reload(void* data,void* rm,double* maxValue){
    auto m=(Measure*)data;if(!m)return;
    m->enabled=rmNumber(rm,L"SnapEnabled",1)!=0;m->diagnostics=rmNumber(rm,L"AllowDiagnostics",0)!=0;
    m->distance=PitoSnap::max(2,PitoSnap::min(30,(int)rmNumber(rm,L"SnapDistance",10)));
    m->gap=PitoSnap::max(0,PitoSnap::min(80,(int)rmNumber(rm,L"SnapGap",12)));
    lstrcpynW(m->scope,rmString(rm,L"Scope",L"PitoNote",TRUE),260);m->session.clear();*maxValue=1;
}
extern "C" __declspec(dllexport) double Update(void* data){auto m=(Measure*)data;return m&&m->attached?1:0;}
extern "C" __declspec(dllexport) void ExecuteBang(void* data,LPCWSTR command){
    auto m=(Measure*)data;if(!m)return;
    if(lstrcmpiW(command,L"BeginResize")==0){RECT r;if(GetWindowRect(m->window,&r)){m->resizing=true;m->resizeOrigin={r.left,r.top};m->session.clear();}}
    else if(lstrcmpiW(command,L"EndResize")==0)m->resizing=false;
    else if(m->diagnostics&&lstrcmpiW(command,L"ProbeBegin")==0){m->dragging=true;m->session.clear();}
    else if(m->diagnostics&&lstrcmpiW(command,L"ProbeEnd")==0){m->dragging=false;m->session.clear();}
    else if(m->diagnostics){int x,y;if(swscanf_s(command,L"ProbeMove %d %d",&x,&y)==2)SetWindowPos(m->window,nullptr,x,y,0,0,SWP_NOSIZE|SWP_NOZORDER|SWP_NOACTIVATE);}
}
extern "C" __declspec(dllexport) void HandleSkinSettingChange(void* data,int){auto m=(Measure*)data;if(m)m->session.clear();}
extern "C" __declspec(dllexport) void Finalize(void* data){
    auto m=(Measure*)data;if(!m)return;
    if(m->attached)RemoveWindowSubclass(m->window,subclass,(UINT_PTR)m);
    auto link=&measures;while(*link&&*link!=m)link=&(*link)->next;if(*link)*link=m->next;
    m->~Measure();HeapFree(GetProcessHeap(),0,m);
}
