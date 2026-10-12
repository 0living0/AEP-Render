using System;
using System.IO;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Collections.Generic;
internal static class IconBuilder
{
    private static void Main(string[] args)
    {
        string destination=args[0]; var sizes=new[]{16,20,24,32,48,64,128,256}; var images=new List<byte[]>();
        foreach(int size in sizes)
        using(var bitmap=new Bitmap(size,size))
        using(var g=Graphics.FromImage(bitmap))
        {
            g.Clear(Color.Transparent); g.SmoothingMode=SmoothingMode.AntiAlias; g.TextRenderingHint=System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            float inset=Math.Max(1,size/24f),radius=size*.23f,d=radius*2; RectangleF rect=new RectangleF(inset,inset,size-2*inset,size-2*inset);
            using(var path=new GraphicsPath())
            using(var fill=new LinearGradientBrush(rect,Color.FromArgb(132,111,231),Color.FromArgb(104,85,207),45))
            {
                path.AddArc(rect.Left,rect.Top,d,d,180,90); path.AddArc(rect.Right-d,rect.Top,d,d,270,90); path.AddArc(rect.Right-d,rect.Bottom-d,d,d,0,90); path.AddArc(rect.Left,rect.Bottom-d,d,d,90,90); path.CloseFigure();g.FillPath(fill,path);
            }
            using(var ae=new Font("Segoe UI",size*.40f,FontStyle.Bold,GraphicsUnit.Pixel))
            using(var render=new Font("Segoe UI",size*.19f,FontStyle.Bold,GraphicsUnit.Pixel))
            using(var white=new SolidBrush(Color.White))
            using(var format=new StringFormat { Alignment=StringAlignment.Center,LineAlignment=StringAlignment.Center })
            { g.DrawString("Ae",ae,white,new RectangleF(0,size*.08f,size,size*.49f),format); g.DrawString("Render",render,white,new RectangleF(0,size*.54f,size,size*.29f),format); }
            using(var bytes=new MemoryStream()) { bitmap.Save(bytes,ImageFormat.Png); images.Add(bytes.ToArray()); }
            if(size==256) bitmap.Save(Path.ChangeExtension(destination,"png"),ImageFormat.Png);
        }
        using(var file=new BinaryWriter(File.Create(destination)))
        { file.Write((ushort)0);file.Write((ushort)1);file.Write((ushort)sizes.Length);int offset=6+16*sizes.Length;for(int i=0;i<sizes.Length;i++){file.Write((byte)(sizes[i]==256?0:sizes[i]));file.Write((byte)(sizes[i]==256?0:sizes[i]));file.Write((byte)0);file.Write((byte)0);file.Write((ushort)1);file.Write((ushort)32);file.Write(images[i].Length);file.Write(offset);offset+=images[i].Length;}foreach(var image in images)file.Write(image);}
    }
}
