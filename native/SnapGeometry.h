#pragma once
#include <stdint.h>

// Geometry only: screen pixels, visible paper rectangles, no mouse or window APIs.
namespace PitoSnap {
struct Box { int x,y,w,h; int right() const{return x+w;} int bottom() const{return y+h;} };
struct Peer { Box box; uintptr_t key; };
struct Lock { bool active=false; int target=0,kind=0; uintptr_t key=0; };
struct Session { Lock x,y; void clear(){x=Lock();y=Lock();} };
inline int absn(int n){return n<0?-n:n;}
inline int min(int a,int b){return a<b?a:b;}
inline int max(int a,int b){return a>b?a:b;}
inline int intervalGap(int a,int endA,int b,int endB){return max(0,max(a,b)-min(endA,endB));}
struct Axis {
    int raw,enter,leave,low,high,best=0,score=0x7fffffff;
    Lock old,choice; bool held=false,found=false;
    Axis(int p,int distance,int lo,int hi,Lock lock):raw(p),enter(distance),leave(distance+max(4,distance/2)),low(lo),high(hi),old(lock){}
    void offer(int target,int kind,uintptr_t key){
        if(target<low||target>high)return;
        int delta=absn(target-raw);
        if(old.active&&old.target==target&&old.kind==kind&&old.key==key&&delta<=leave){choice=old;best=target;found=held=true;return;}
        // Distance dominates; on near ties prefer matching top/left over spacing.
        int rank=delta*4+kind*2;
        if(!held&&delta<=enter&&rank<score){score=rank;best=target;choice={true,target,kind,key};found=true;}
    }
    int finish(Lock& lock){lock=found?choice:Lock();return found?best:raw;}
};
inline Box resolve(Box moving,const Peer* peers,int count,Box work,int distance,int gap,Session& state,bool bypass=false,int screenGap=-1){
    if(bypass||distance<=0){state.clear();return moving;}
    Axis x(moving.x,distance,work.x,max(work.x,work.right()-moving.w),state.x);
    Axis y(moving.y,distance,work.y,max(work.y,work.bottom()-moving.h),state.y);
    int reach=max(96,gap+distance*2);
    const Peer *left=nullptr,*right=nullptr,*above=nullptr,*below=nullptr;
    for(int i=0;i<count;i++){
        const auto& p=peers[i];const auto& b=p.box;
        int vertical=intervalGap(moving.y,moving.bottom(),b.y,b.bottom());
        int horizontal=intervalGap(moving.x,moving.right(),b.x,b.right());
        if(vertical<=reach){
            x.offer(b.x,0,p.key);
            x.offer(b.right()+gap,1,p.key);
            x.offer(b.x-gap-moving.w,1,p.key);
        }
        if(horizontal<=reach){
            y.offer(b.y,0,p.key);
            y.offer(b.bottom()+gap,1,p.key);
            y.offer(b.y-gap-moving.h,1,p.key);
        }
        if(vertical==0){
            if(b.right()<=moving.x&&(!left||b.right()>left->box.right()))left=&p;
            if(b.x>=moving.right()&&(!right||b.x<right->box.x))right=&p;
        }
        if(horizontal==0){
            if(b.bottom()<=moving.y&&(!above||b.bottom()>above->box.bottom()))above=&p;
            if(b.y>=moving.bottom()&&(!below||b.y<below->box.y))below=&p;
        }
    }
    if(left&&right&&right->box.x-left->box.right()>=moving.w){
        x.offer(left->box.right()+(right->box.x-left->box.right()-moving.w)/2,2,left->key^right->key);
        if(absn(left->box.y-right->box.y)<=distance)y.offer(left->box.y,0,left->key);
    }
    if(above&&below&&below->box.y-above->box.bottom()>=moving.h){
        y.offer(above->box.bottom()+(below->box.y-above->box.bottom()-moving.h)/2,2,above->key^below->key);
        if(absn(above->box.x-below->box.x)<=distance)x.offer(above->box.x,0,above->key);
    }
    // Work is already inset for transparent shadow space. Match the requested visible margin.
    int margin=screenGap<0?gap:screenGap;
    x.offer(work.x+margin,3,0);x.offer(work.right()-moving.w-margin,3,0);
    y.offer(work.y+margin,3,0);y.offer(work.bottom()-moving.h-margin,3,0);
    moving.x=x.finish(state.x);moving.y=y.finish(state.y);return moving;
}
}
