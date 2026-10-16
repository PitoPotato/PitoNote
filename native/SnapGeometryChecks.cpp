#include "SnapGeometry.h"
#include <stdio.h>
#include <stdlib.h>
using namespace PitoSnap;
static int count;
static void check(bool ok,const char* text){if(!ok){printf("FAIL: %s\n",text);exit(1);}count++;printf("PASS: %s\n",text);}
int main(){
    Box work={-3000,-1000,7000,4000};Session state;
    Peer p[]={{{100,100,400,250},1}};
    auto run=[&](Box box,int distance=10,int gap=12){state.clear();return resolve(box,p,1,work,distance,gap,state);};
    auto r=run({106,365,280,180});check(r.x==100,"Stacked notes align left despite different widths");
    r=run({517,106,280,180});check(r.x==512&&r.y==100,"Adjacent notes align top and maintain a visible gap");
    r=run({-195,104,280,180});check(r.x==-192&&r.y==100,"Left-side placement uses the moving note width");
    r=run({104,-95,280,180});check(r.y==-92,"Above placement uses the moving note height");
    r=run({104,365,280,180});check(r.y==362,"Below placement uses the other note height");
    r=run({215,100,280,180});check(r.x==215,"Matching right edges alone does not pull a different-width note");
    r=run({100,175,280,180});check(r.y==175,"Matching bottom edges alone does not pull a different-height note");
    r=run({1000,107,280,180});check(r.y==107,"Distant unrelated notes cannot pull a row into alignment");
    r=run({126,390,280,180});check(r.x==126&&r.y==390,"Outside-threshold movement stays free");
    state.clear();r=resolve({106,365,280,180},p,1,work,10,12,state);check(r.x==100,"Magnet enters at the configured distance");
    r=resolve({114,365,280,180},p,1,work,10,12,state);check(r.x==100,"Small hand jitter does not oscillate at the boundary");
    r=resolve({117,365,280,180},p,1,work,10,12,state);check(r.x==117,"Moving beyond the release distance breaks the magnet");
    r=resolve({106,365,280,180},p,1,work,10,12,state,true);check(r.x==106&&!state.x.active,"Ctrl bypass clears the held magnet");
    Peer changed[]={{{300,100,400,250},1}};state.clear();resolve({106,365,280,180},p,1,work,10,12,state);
    r=resolve({106,365,280,180},changed,1,work,10,12,state);check(r.x==106,"Moving or hiding a target drops its obsolete guide");
    Peer between[]={{{100,100,100,100},1},{{540,100,100,100},2}};state.clear();
    r=resolve({319,102,100,100},between,2,work,10,12,state);check(r.x==320&&r.y==100,"Middle note can balance spacing between two neighbors");
    Peer vertical[]={{{100,100,100,100},1},{{100,540,100,100},2}};state.clear();
    r=resolve({102,319,100,100},vertical,2,work,10,12,state);check(r.x==100&&r.y==320,"Equal spacing also works vertically");
    Peer negative[]={{{-1700,-700,320,200},9}};state.clear();
    r=resolve({-1696,-485,200,160},negative,1,work,10,12,state);check(r.x==-1700&&r.y==-488,"Negative monitor coordinates are supported");
    Box screen={0,0,1920,1040};state.clear();r=resolve({16,16,356,220},nullptr,0,screen,10,12,state);
    check(r.x==12&&r.y==12,"Screen work-area margins are respected");
    state.clear();r=resolve({1558,813,356,220},nullptr,0,screen,10,12,state);check(r.x==1552&&r.y==808,"Screen docking accounts for note dimensions");
    r=run({518,108,280,180},20,24);check(r.x==524&&r.y==100,"Custom snap distance and gap are applied");
    r=run({507,97,700,350});check(r.w==700&&r.h==350,"Moving never resizes a note");
    r=run({106,365,280,40});check(r.x==100,"Collapsed title bars use their actual height");
    Peer zoomed[]={{{200,200,800,500},10}};state.clear();
    r=resolve({1031,208,560,360},zoomed,1,work,20,24,state);check(r.x==1024&&r.y==200,"Scaled distances and paper dimensions work at 200 percent");
    state.clear();r=resolve({1027,206,350,225},zoomed,1,work,13,15,state);check(r.x==1015&&r.y==200,"Different-scale notes use each window's actual paper size");
    state.clear();resolve({106,365,280,180},p,1,work,10,12,state);
    r=resolve({106,365,280,180},nullptr,0,work,10,12,state);check(r.x==106&&!state.x.active,"A removed or hidden peer releases the held guide");
    printf("%d snap geometry checks passed.\n",count);return 0;
}
