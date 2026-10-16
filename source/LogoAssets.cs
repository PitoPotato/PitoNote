using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PitoNote {
    // Original vector mark: a warm paper tile, a folded corner, and a rounded P.
    public static class LogoAssets {
        const string Paper="M18 4 H46 Q60 4 60 18 V43 L43 60 H18 Q4 60 4 46 V18 Q4 4 18 4 Z";
        const string Fold="M43 60 V50 Q43 43 50 43 H60 Z";
        const string Letter="M23 44 V20 H33 C45 20 45 36 33 36 H23";
        static Brush Ink(string hex){return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));}
        static DrawingImage Drawing(){
            var group=new DrawingGroup();group.Children.Add(new GeometryDrawing(Ink("#F3D36B"),null,Geometry.Parse(Paper)));
            group.Children.Add(new GeometryDrawing(Ink("#D4AF42"),null,Geometry.Parse(Fold)));
            var pen=new Pen(Ink("#343B30"),6){StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round,LineJoin=PenLineJoin.Round};
            group.Children.Add(new GeometryDrawing(null,pen,Geometry.Parse(Letter)));group.Freeze();return new DrawingImage(group);
        }
        public static Image Image(double size){return new Image{Source=Drawing(),Width=size,Height=size,Stretch=Stretch.Uniform,VerticalAlignment=VerticalAlignment.Center,IsHitTestVisible=false};}
        public static void SetWindowIcon(Window window){string path=Path.Combine(Engine.Resources,"Brand","PitoNote.ico");if(File.Exists(path))window.Icon=BitmapFrame.Create(new Uri(path),BitmapCreateOptions.None,BitmapCacheOption.OnLoad);}
        static byte[] Png(int size,bool white){
            var visual=new DrawingVisual();using(var dc=visual.RenderOpen()){
                if(white)dc.DrawRectangle(Brushes.White,null,new Rect(0,0,size,size));
                dc.PushTransform(new ScaleTransform(size/64.0,size/64.0));
                var drawing=Drawing().Drawing;dc.DrawDrawing(drawing);dc.Pop();
            }
            var bitmap=new RenderTargetBitmap(size,size,96,96,PixelFormats.Pbgra32);bitmap.Render(visual);
            var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using(var stream=new MemoryStream()){encoder.Save(stream);return stream.ToArray();}
        }
        public static void Export(string directory){
            Directory.CreateDirectory(directory);var sizes=new[]{16,24,32,48,64,128,256};var pngs=new List<byte[]>();
            foreach(int size in sizes){byte[] bytes=Png(size,false);pngs.Add(bytes);File.WriteAllBytes(Path.Combine(directory,"PitoNote-"+size+".png"),bytes);}
            File.WriteAllBytes(Path.Combine(directory,"PitoNote-logo.png"),Png(512,true));
            using(var stream=File.Create(Path.Combine(directory,"PitoNote.ico")))using(var writer=new BinaryWriter(stream)){
                writer.Write((ushort)0);writer.Write((ushort)1);writer.Write((ushort)sizes.Length);int offset=6+16*sizes.Length;
                for(int i=0;i<sizes.Length;i++){writer.Write((byte)(sizes[i]==256?0:sizes[i]));writer.Write((byte)(sizes[i]==256?0:sizes[i]));writer.Write((byte)0);writer.Write((byte)0);writer.Write((ushort)1);writer.Write((ushort)32);writer.Write(pngs[i].Length);writer.Write(offset);offset+=pngs[i].Length;}
                foreach(var bytes in pngs)writer.Write(bytes);
            }
            string svg="<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 64 64\" role=\"img\" aria-labelledby=\"title\"><title id=\"title\">PitoNote</title><rect width=\"64\" height=\"64\" fill=\"white\"/><path fill=\"#F3D36B\" d=\""+Paper+"\"/><path fill=\"#D4AF42\" d=\""+Fold+"\"/><path fill=\"none\" stroke=\"#343B30\" stroke-width=\"6\" stroke-linecap=\"round\" stroke-linejoin=\"round\" d=\""+Letter+"\"/></svg>";
            Engine.AtomicWrite(Path.Combine(directory,"PitoNote.svg"),svg);
        }
    }
}
