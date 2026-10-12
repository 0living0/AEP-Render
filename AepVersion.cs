using System;
using System.IO;
using System.Text;
using System.Xml;

namespace AepBatchRenderer
{
    public sealed class AepVersion
    {
        public int Major, Minor, Patch, Build, Format;
        public bool Beta;
        public string Text { get { return Major+"."+Minor+"."+Patch+" (Build "+Build+")"+(Beta ? " Beta" : ""); } }
        public static AepVersion Read(string path)
        {
            if(String.Equals(Path.GetExtension(path),".aepx",StringComparison.OrdinalIgnoreCase)) return ReadXml(path);
            using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))
            using(var reader=new BinaryReader(stream))
            {
                if(stream.Length<12 || Tag(reader)!="RIFX") throw new InvalidDataException(UiText.Get("text.235"));
                long end=8L+Number(reader);
                if(Tag(reader)!="Egg!" || end>stream.Length || end<12) throw new InvalidDataException(UiText.Get("text.236"));
                for(int count=0;stream.Position+8<=end && count<4096;count++)
                {
                    string tag=Tag(reader); uint size=Number(reader); long next=stream.Position+size+(size&1);
                    if(next>end) throw new InvalidDataException(UiText.Get("text.237"));
                    if(tag=="head")
                    {
                        if(size<8 || size>4096) throw new InvalidDataException(UiText.Get("text.238"));
                        return Decode(reader.ReadBytes(8));
                    }
                    stream.Position=next;
                }
                throw new InvalidDataException(UiText.Get("text.239"));
            }
        }
        private static string Tag(BinaryReader r) { return Encoding.ASCII.GetString(r.ReadBytes(4)); }
        private static uint Number(BinaryReader r) { var b=r.ReadBytes(4); if(b.Length!=4) throw new EndOfStreamException(); return ((uint)b[0]<<24)|((uint)b[1]<<16)|((uint)b[2]<<8)|b[3]; }
        private static AepVersion ReadXml(string path)
        {
            var settings=new XmlReaderSettings { DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=67108864,IgnoreComments=true };
            using(var reader=XmlReader.Create(path,settings))
                while(reader.Read())
                    if(reader.NodeType==XmlNodeType.Element && reader.LocalName=="head")
                    {
                        string hex=reader.GetAttribute("bdata");
                        if(hex==null || hex.Length<16 || hex.Length>8192) throw new InvalidDataException(UiText.Get("text.240"));
                        byte[] b=new byte[8]; for(int i=0;i<b.Length;i++) b[i]=Convert.ToByte(hex.Substring(i*2,2),16);
                        return Decode(b);
                    }
            throw new InvalidDataException(UiText.Get("text.241"));
        }
        // Packed version layout independently implemented from the MIT-licensed
        // tinogithub/aftereffects-version-check research; see THIRD-PARTY-NOTICES.md.
        public static AepVersion Decode(byte[] b)
        {
            if(b==null || b.Length<8) throw new InvalidDataException(UiText.Get("text.242"));
            uint word=((uint)b[4]<<24)|((uint)b[5]<<16)|((uint)b[6]<<8)|b[7];
            var v=new AepVersion { Format=(b[0]<<8)|b[1],Major=(int)(((word>>26)&31)<<3 | ((word>>19)&7)),Minor=(int)((word>>15)&15),Patch=(int)((word>>11)&15),Build=(int)(word&255),Beta=(word&512)==0 };
            int platform=(int)((word>>22)&15);
            bool known=(v.Format==0x51 && v.Major==11)||(v.Format==0x56 && v.Major==12)||(v.Format==0x57 && v.Major==13)||(v.Format==0x58 && v.Major==14)||(v.Format==0x5c && v.Major==15)||(v.Format==0x5d && (v.Major==16||v.Major==17||v.Major==18||v.Major==22))||(v.Format==0x5e && v.Major==23)||(v.Format==0x5f && v.Major==24)||(v.Format==0x60 && v.Major==25)||(v.Format==0x61 && v.Major==26);
            if(!known || platform<12 || platform>14) throw new InvalidDataException(UiText.Get("text.243")+v.Format+UiText.Get("text.244")+v.Text+UiText.Get("text.245"));
            return v;
        }
    }
}
