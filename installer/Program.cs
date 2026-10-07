using System.Reflection;
using System.Text;
using System.Windows.Forms;

namespace ANAIRelightV53Setup;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new InstallerForm());
    }
}

public sealed class InstallerForm : Form
{
    readonly Color Bg=Color.FromArgb(10,15,28);
    readonly Color Card=Color.FromArgb(22,31,51);
    readonly Color Card2=Color.FromArgb(31,45,71);
    readonly Color Cyan=Color.FromArgb(0,188,212);
    readonly Color Purple=Color.FromArgb(126,87,194);
    readonly Color Green=Color.FromArgb(76,175,80);
    readonly Color Amber=Color.FromArgb(255,193,7);
    readonly Color TextMain=Color.FromArgb(245,248,255);
    readonly Color TextMuted=Color.FromArgb(174,188,212);

    readonly TextBox modulesBox=new();
    readonly TextBox logBox=new();
    readonly Button browseButton=new();
    readonly Button installButton=new();
    readonly Label status=new();

    public InstallerForm()
    {
        Text="AN AI Relight V5.3 — Local Generative Mood Installer";
        Width=850;
        Height=690;
        StartPosition=FormStartPosition.CenterScreen;
        FormBorderStyle=FormBorderStyle.FixedDialog;
        MaximizeBox=false;
        BackColor=Bg;
        ForeColor=TextMain;
        Font=new Font("Segoe UI",10f);
        BuildUi();
        modulesBox.Text=DefaultModules();
        browseButton.Click+=(_,__)=>Browse();
        installButton.Click+=(_,__)=>Install();
    }

    Label L(string text,float size=10f,FontStyle style=FontStyle.Regular,Color? color=null)
        => new(){Text=text,AutoSize=true,ForeColor=color??TextMain,BackColor=Color.Transparent,Font=new Font("Segoe UI",size,style)};

    Panel CardPanel(int height)
        => new(){Width=780,Height=height,BackColor=Card,Margin=new Padding(0,8,0,8),Padding=new Padding(18)};

    void BuildUi()
    {
        var root=new FlowLayoutPanel{
            Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,
            AutoScroll=true,Padding=new Padding(24),BackColor=Bg};

        var hero=new Panel{Width=780,Height=130,BackColor=Color.FromArgb(17,30,55),Margin=new Padding(0,0,0,10)};
        hero.Controls.Add(new Panel{Dock=DockStyle.Left,Width=7,BackColor=Cyan});
        hero.Controls.Add(new Panel{Dock=DockStyle.Right,Width=7,BackColor=Purple});
        var title=L("AN AI RELIGHT V5.3",24f,FontStyle.Bold); title.Location=new Point(28,18);
        var sub=L("Local Generative Mood • AMD Radeon / DirectML",11f,FontStyle.Bold); sub.Location=new Point(30,61);
        var badge=L("NO API  •  NO CLOUD INFERENCE  •  SUBJECT LOCK  •  LIGHT + COLOR",9f,FontStyle.Bold,Cyan); badge.Location=new Point(30,93);
        hero.Controls.Add(title);hero.Controls.Add(sub);hero.Controls.Add(badge);
        root.Controls.Add(hero);

        var location=CardPanel(142);
        var lt=L("INSTALL LOCATION",11f,FontStyle.Bold,Cyan); lt.Location=new Point(18,14);
        var hint=L("Lightroom Modules folder",9.5f,FontStyle.Regular,TextMuted); hint.Location=new Point(18,42);
        modulesBox.SetBounds(18,68,612,32);
        modulesBox.BackColor=Card2;modulesBox.ForeColor=TextMain;modulesBox.BorderStyle=BorderStyle.FixedSingle;
        browseButton.Text="Browse";browseButton.SetBounds(642,67,105,34);
        browseButton.FlatStyle=FlatStyle.Flat;browseButton.FlatAppearance.BorderColor=Cyan;
        browseButton.BackColor=Color.FromArgb(22,70,90);browseButton.ForeColor=TextMain;
        var pi=L("Existing AN AI Relight is backed up before V5.3 installs",9f,FontStyle.Bold,Amber);pi.Location=new Point(18,110);
        location.Controls.Add(lt);location.Controls.Add(hint);location.Controls.Add(modulesBox);location.Controls.Add(browseButton);location.Controls.Add(pi);
        root.Controls.Add(location);

        var features=CardPanel(186);
        var ft=L("LOCAL V5.3 ENGINE",11f,FontStyle.Bold,Purple);ft.Location=new Point(18,12);
        var rows=new[]{
            ("●  Local IC-Light foreground-conditioned mood pass",18,43),
            ("●  PyTorch DirectML for AMD / DirectX 12 GPUs",400,43),
            ("●  MODNet subject matte + YuNet face protection",18,72),
            ("●  Original face / pose / dress / texture preserved",400,72),
            ("●  AI generated frame is only a light/color reference",18,101),
            ("●  Full-resolution TIFF transfer back to Lightroom",400,101)
        };
        foreach(var row in rows){var l=L(row.Item1,9.5f);l.Location=new Point(row.Item2,row.Item3);features.Controls.Add(l);}
        var note=L("FIRST RUN: installs local DirectML/Python packages and downloads the IC-Light + SD model cache. Expect several GB. After cache is ready, no cloud inference/API is used.",9.2f,FontStyle.Bold,Amber);
        note.MaximumSize=new Size(730,0);note.Location=new Point(18,137);
        features.Controls.Add(ft);features.Controls.Add(note);
        root.Controls.Add(features);

        installButton.Text="INSTALL AN AI RELIGHT V5.3 LOCAL";
        installButton.Width=780;installButton.Height=52;
        installButton.FlatStyle=FlatStyle.Flat;installButton.FlatAppearance.BorderSize=0;
        installButton.BackColor=Green;installButton.ForeColor=Color.White;
        installButton.Font=new Font("Segoe UI",11.5f,FontStyle.Bold);
        installButton.Margin=new Padding(0,6,0,10);
        root.Controls.Add(installButton);

        status.AutoSize=true;status.ForeColor=Cyan;status.Font=new Font("Segoe UI",9.5f,FontStyle.Bold);
        status.Margin=new Padding(2,0,0,8);root.Controls.Add(status);

        logBox.Width=780;logBox.Height=135;logBox.Multiline=true;logBox.ReadOnly=true;logBox.ScrollBars=ScrollBars.Vertical;
        logBox.BackColor=Color.FromArgb(8,13,24);logBox.ForeColor=Color.FromArgb(190,230,242);
        logBox.BorderStyle=BorderStyle.FixedSingle;logBox.Font=new Font("Consolas",9f);
        root.Controls.Add(logBox);
        Controls.Add(root);
    }

    static string DefaultModules()
    {
        var appData=Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData,"Adobe","Lightroom","Modules");
    }

    void Browse()
    {
        using var d=new FolderBrowserDialog{
            Description="Select Lightroom's Modules folder",
            UseDescriptionForTitle=true,
            SelectedPath=Directory.Exists(modulesBox.Text)?modulesBox.Text:DefaultModules()
        };
        if(d.ShowDialog(this)==DialogResult.OK)modulesBox.Text=d.SelectedPath;
    }

    void Log(string s)
    {
        logBox.AppendText("> "+s+Environment.NewLine);
        logBox.SelectionStart=logBox.TextLength;
        logBox.ScrollToCaret();
        Application.DoEvents();
    }

    static byte[] ResourceBytes(string name)
    {
        var asm=Assembly.GetExecutingAssembly();
        using var s=asm.GetManifestResourceStream(name)??throw new InvalidOperationException("Missing installer resource: "+name);
        using var ms=new MemoryStream();s.CopyTo(ms);return ms.ToArray();
    }
    static string ResourceText(string name)=>Encoding.UTF8.GetString(ResourceBytes(name));

    static void WriteText(string path,string data)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path,data,new UTF8Encoding(false));
    }
    static void WriteBytes(string path,byte[] data)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path,data);
    }

    void Install()
    {
        installButton.Enabled=false;
        status.ForeColor=Cyan;
        status.Text="Installing V5.3 local plugin...";
        try
        {
            var modules=modulesBox.Text.Trim().Trim('"');
            if(modules.Length==0)throw new InvalidOperationException("Choose a Lightroom Modules folder.");
            Directory.CreateDirectory(modules);

            var root=Path.Combine(modules,"AN AI Relight.lrplugin");
            if(Directory.Exists(root))
            {
                var backup=Path.Combine(modules,"AN AI Relight Backup-"+DateTime.Now.ToString("yyyyMMdd-HHmmss"));
                Directory.Move(root,backup);
                Log("Previous AN AI Relight backed up:");
                Log(backup);
            }

            Directory.CreateDirectory(root);
            Directory.CreateDirectory(Path.Combine(root,"engine"));
            Directory.CreateDirectory(Path.Combine(root,"runtime"));
            Directory.CreateDirectory(Path.Combine(root,"runtime_assets"));
            Directory.CreateDirectory(Path.Combine(root,"models"));
            Directory.CreateDirectory(Path.Combine(root,"licenses"));

            Log("Installing Lightroom plug-in files...");
            WriteText(Path.Combine(root,"Info.lua"),ResourceText("Info.lua"));
            WriteText(Path.Combine(root,"Core.lua"),ResourceText("Core.lua"));
            WriteText(Path.Combine(root,"Engine.lua"),ResourceText("Engine.lua"));
            WriteText(Path.Combine(root,"Relight.lua"),ResourceText("Relight.lua"));
            WriteText(Path.Combine(root,"Relighting.lua"),ResourceText("Relighting.lua"));

            Log("Installing local DirectML bootstrap + Python worker...");
            WriteBytes(Path.Combine(root,"engine","relight_engine.exe"),ResourceBytes("relight_engine.exe"));
            WriteText(Path.Combine(root,"runtime","local_worker.py"),ResourceText("local_worker.py"));
            WriteBytes(Path.Combine(root,"runtime_assets","python-3.10-embed-amd64.zip"),ResourceBytes("python-3.10-embed-amd64.zip"));
            WriteText(Path.Combine(root,"runtime_assets","get-pip.py"),ResourceText("get-pip.py"));

            Log("Installing local matte / face models...");
            WriteBytes(Path.Combine(root,"models","modnet_photographic.onnx"),ResourceBytes("modnet_photographic.onnx"));
            WriteBytes(Path.Combine(root,"models","face_detection_yunet_2023mar.onnx"),ResourceBytes("face_detection_yunet_2023mar.onnx"));

            WriteText(Path.Combine(root,"licenses","MODNET-LICENSE.txt"),ResourceText("MODNET-LICENSE.txt"));
            WriteText(Path.Combine(root,"licenses","ICLIGHT-LICENSE.txt"),ResourceText("ICLIGHT-LICENSE.txt"));
            WriteText(Path.Combine(root,"licenses","OPENCV-LICENSE.txt"),ResourceText("OPENCV-LICENSE.txt"));

            foreach(var f in new[]{
                Path.Combine(root,"Info.lua"),
                Path.Combine(root,"Relighting.lua"),
                Path.Combine(root,"engine","relight_engine.exe"),
                Path.Combine(root,"runtime","local_worker.py"),
                Path.Combine(root,"runtime_assets","python-3.10-embed-amd64.zip"),
                Path.Combine(root,"models","modnet_photographic.onnx")
            })
                if(!File.Exists(f)||new FileInfo(f).Length==0)
                    throw new IOException("Installation verification failed: "+f);

            status.ForeColor=Green;
            status.Text="✓ AN AI Relight V5.3 Local installed";
            Log("V5.3 installation verified.");
            Log("First Lightroom run will bootstrap DirectML and download local model files.");

            MessageBox.Show(this,
                "AN AI Relight V5.3 Local installed successfully.\n\nRestart Lightroom Classic and run:\nFile > Plug-in Extras > AN AI Relight V5.3 — Local Generative Mood...\n\nThe first run downloads and installs several GB of local AI components. Later inference is local.",
                "AN AI Relight V5.3",MessageBoxButtons.OK,MessageBoxIcon.Information);
        }
        catch(Exception ex)
        {
            status.ForeColor=Color.FromArgb(244,67,54);
            status.Text="✕ Installation failed";
            Log("ERROR: "+ex.Message);
            MessageBox.Show(this,ex.Message,"AN AI Relight V5.3 — Installer Error",MessageBoxButtons.OK,MessageBoxIcon.Error);
        }
        finally
        {
            installButton.Enabled=true;
        }
    }
}
