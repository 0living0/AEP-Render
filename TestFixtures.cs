using System;
using System.IO;
using System.Text;
namespace AepBatchRenderer
{
    internal static class TestFixtures
    {
        // Minimal synthetic header fixture, never a real renderable AE project.
        public static byte[] Head(int major,int minor=0,int patch=0,bool beta=false)
        {
            int format=major>=23 ? major+71 : major>=16 ? 0x5d : major==15 ? 0x5c : major==14 ? 0x58 : major==13 ? 0x57 : major==12 ? 0x56 : 0x51;
            uint word=(uint)(((major>>3)&31)<<26 | 12<<22 | (major&7)<<19 | minor<<15 | patch<<11 | (beta ? 0 : 512) | 1024 | 59);
            return new byte[]{(byte)(format>>8),(byte)format,0,1,(byte)(word>>24),(byte)(word>>16),(byte)(word>>8),(byte)word,128,0,0,0,0,0,0,0,0,0,0,0};
        }
        public static void WriteProject(string path,int major=23,int minor=0,bool beta=false,bool precedingChunk=true)
        {
            var head=Head(major,minor,0,beta);
            using(var s=new MemoryStream())
            {
                Put(s,"RIFX"); Number(s,precedingChunk ? 42u : 32u); Put(s,"Egg!");
                if(precedingChunk){Put(s,"test");Number(s,1);s.WriteByte(0);s.WriteByte(0);}
                Put(s,"head");Number(s,(uint)head.Length);s.Write(head,0,head.Length);File.WriteAllBytes(path,s.ToArray());
            }
        }
        private static void Put(Stream s,string text){var b=Encoding.ASCII.GetBytes(text);s.Write(b,0,b.Length);}
        private static void Number(Stream s,uint n){s.WriteByte((byte)(n>>24));s.WriteByte((byte)(n>>16));s.WriteByte((byte)(n>>8));s.WriteByte((byte)n);}
    }
}
