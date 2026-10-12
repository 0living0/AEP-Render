using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;
namespace AepBatchRenderer {
    // Startup fallback: app-owned and visually aligned with the WebView interface.
    internal static class AppNotice {
        [DllImport("user32.dll")] static extern bool ReleaseCapture();
        [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr hWnd,int msg,IntPtr wParam,IntPtr lParam);
        static GraphicsPath Rounded(Rectangle r,int radius) {
            int d=radius*2;var path=new GraphicsPath();path.AddArc(r.X,r.Y,d,d,180,90);path.AddArc(r.Right-d,r.Y,d,d,270,90);path.AddArc(r.Right-d,r.Bottom-d,d,d,0,90);path.AddArc(r.X,r.Bottom-d,d,d,90,90);path.CloseFigure();return path;
        }
        public static void Show(string title,string message) {
            var pref=Store.Load("settings.json",new Preferences())??new Preferences();UiText.Language=UiText.Normalize(pref.Language);
            bool dark=pref.Theme=="dark";if(pref.FollowSystem)try{dark=Convert.ToInt32(Microsoft.Win32.Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize","AppsUseLightTheme",1))==0;}catch{}
            Color bg=dark?Color.FromArgb(34,33,48):Color.FromArgb(250,250,253),head=dark?Color.FromArgb(39,37,54):Color.White,ink=dark?Color.FromArgb(235,229,244):Color.FromArgb(46,45,64),muted=dark?Color.FromArgb(167,158,183):Color.FromArgb(126,124,145),line=dark?Color.FromArgb(61,57,76):Color.FromArgb(232,230,239),purple=Color.FromArgb(126,103,226);
            using(var dialog=new Form { Text="AEP Render",FormBorderStyle=FormBorderStyle.None,MaximizeBox=false,MinimizeBox=false,ShowInTaskbar=true,StartPosition=FormStartPosition.CenterScreen,ClientSize=new Size(500,244),BackColor=bg,ForeColor=ink,Font=new Font("Microsoft YaHei UI",9),AutoScaleMode=AutoScaleMode.Dpi,KeyPreview=true }) {
                try{dialog.Icon=Icon.ExtractAssociatedIcon(Application.ExecutablePath);}catch{}
                var header=new Panel{Bounds=new Rectangle(1,1,498,51),BackColor=head};
                var mark=new Panel{Bounds=new Rectangle(20,12,30,30),BackColor=head};
                mark.Paint+=(s,e)=>{e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;using(var path=Rounded(new Rectangle(0,0,29,29),7))using(var brush=new SolidBrush(purple))e.Graphics.FillPath(brush,path);using(var font=new Font("Segoe UI",11,FontStyle.Bold))using(var brush=new SolidBrush(Color.White)){var size=e.Graphics.MeasureString("Ae",font);e.Graphics.DrawString("Ae",font,brush,(30-size.Width)/2,(30-size.Height)/2-1);}};
                var appName=new Label{Text="AEP Render",AutoSize=false,Bounds=new Rectangle(62,15,300,25),Font=new Font(dialog.Font.FontFamily,10.5f,FontStyle.Bold),ForeColor=ink,BackColor=Color.Transparent,TextAlign=ContentAlignment.MiddleLeft};
                var close=new Button{Text="",Bounds=new Rectangle(451,9,34,34),FlatStyle=FlatStyle.Flat,BackColor=head,ForeColor=muted,TabStop=false};close.FlatAppearance.BorderSize=0;close.FlatAppearance.MouseOverBackColor=dark?Color.FromArgb(55,51,72):Color.FromArgb(242,239,249);close.Paint+=(s,e)=>{e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;using(var pen=new Pen(muted,1.6f)){e.Graphics.DrawLine(pen,12,12,22,22);e.Graphics.DrawLine(pen,22,12,12,22);}};close.Click+=(s,e)=>dialog.Close();
                header.Controls.AddRange(new Control[]{mark,appName,close});header.Paint+=(s,e)=>{using(var pen=new Pen(line))e.Graphics.DrawLine(pen,0,50,498,50);};
                MouseEventHandler drag=(s,e)=>{if(e.Button==MouseButtons.Left){ReleaseCapture();SendMessage(dialog.Handle,0xA1,(IntPtr)2,IntPtr.Zero);}};header.MouseDown+=drag;appName.MouseDown+=drag;
                var heading=new Label{Text=title,AutoSize=false,Bounds=new Rectangle(26,74,448,31),Font=new Font(dialog.Font.FontFamily,15,FontStyle.Bold),ForeColor=ink};
                var content=new Label{Text=message,AutoSize=false,Bounds=new Rectangle(26,113,448,52),Font=new Font(dialog.Font.FontFamily,10.5f),ForeColor=muted,AutoEllipsis=true};
                var footer=new Panel{Bounds=new Rectangle(1,184,498,59),BackColor=head};footer.Paint+=(s,e)=>{using(var pen=new Pen(line))e.Graphics.DrawLine(pen,0,0,498,0);};
                var ok=new Button{Text=UiText.Get("text.194"),Bounds=new Rectangle(378,197,96,34),BackColor=purple,ForeColor=Color.White,FlatStyle=FlatStyle.Flat,DialogResult=DialogResult.OK,Font=new Font(dialog.Font.FontFamily,9.5f,FontStyle.Bold)};ok.FlatAppearance.BorderSize=0;ok.FlatAppearance.MouseOverBackColor=Color.FromArgb(105,82,207);
                dialog.Controls.AddRange(new Control[]{header,heading,content,footer,ok});dialog.AcceptButton=ok;dialog.CancelButton=ok;
                dialog.Paint+=(s,e)=>{e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;using(var pen=new Pen(line))using(var path=Rounded(new Rectangle(0,0,499,243),12))e.Graphics.DrawPath(pen,path);};
                dialog.Shown+=(s,e)=>{using(var path=Rounded(new Rectangle(0,0,dialog.Width,dialog.Height),12))dialog.Region=new Region(path);};
                dialog.ShowDialog();
            }
        }
    }
}
