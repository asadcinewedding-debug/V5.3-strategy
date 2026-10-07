using System.Diagnostics;
using System.IO.Compression;
using System.Text;

namespace ANAIRelightV53Engine;

internal static class Program
{
    static int Main(string[] args)
    {
        try
        {
            var map = ParseArgs(args);
            if (map.ContainsKey("--self-test"))
            {
                Console.WriteLine("AN AI Relight V5.3 local bootstrap OK");
                return 0;
            }

            Require(map, "--plugin-root");
            Require(map, "--manifest");
            Require(map, "--results");

            string pluginRoot = Path.GetFullPath(map["--plugin-root"]);
            string manifest = Path.GetFullPath(map["--manifest"]);
            string results = Path.GetFullPath(map["--results"]);

            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string runtimeRoot = Path.Combine(local, "AN-AI-Relight-V5.3", "runtime");
            string pythonRoot = Path.Combine(runtimeRoot, "python");
            string pythonExe = Path.Combine(pythonRoot, "python.exe");
            string ready = Path.Combine(runtimeRoot, ".runtime-ready-v1");

            Directory.CreateDirectory(runtimeRoot);

            if (!File.Exists(ready))
            {
                BootstrapRuntime(pluginRoot, runtimeRoot, pythonRoot, pythonExe);
                File.WriteAllText(ready, DateTime.UtcNow.ToString("O"));
            }

            string worker = Path.Combine(pluginRoot, "runtime", "local_worker.py");
            if (!File.Exists(worker))
                throw new FileNotFoundException("Local worker missing", worker);

            string modelDir = Path.Combine(pluginRoot, "models");
            if (!Directory.Exists(modelDir))
                throw new DirectoryNotFoundException("V5.3 model folder missing: " + modelDir);

            var env = new Dictionary<string,string?>
            {
                ["PYTHONUTF8"] = "1",
                ["PYTHONNOUSERSITE"] = "1",
                ["HF_HOME"] = Path.Combine(local, "AN-AI-Relight-V5.3", "models", "hf"),
                ["HF_HUB_DISABLE_TELEMETRY"] = "1",
                ["TOKENIZERS_PARALLELISM"] = "false",
            };

            int code = Run(pythonExe,
                Quote(worker) + " --manifest " + Quote(manifest) + " --results " + Quote(results),
                pluginRoot, env, echo:true);

            return code;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("V5.3 bootstrap error: " + ex);
            return 20;
        }
    }

    static Dictionary<string,string> ParseArgs(string[] args)
    {
        var m = new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        for (int i=0;i<args.Length;i++)
        {
            var a=args[i];
            if (!a.StartsWith("--")) continue;
            if (a=="--self-test")
            {
                m[a]="1";
                continue;
            }
            if (i+1>=args.Length) throw new ArgumentException("Missing value for "+a);
            m[a]=args[++i];
        }
        return m;
    }

    static void Require(Dictionary<string,string> m,string key)
    {
        if(!m.ContainsKey(key) || string.IsNullOrWhiteSpace(m[key]))
            throw new ArgumentException("Missing required argument "+key);
    }

    static void BootstrapRuntime(string pluginRoot,string runtimeRoot,string pythonRoot,string pythonExe)
    {
        Console.WriteLine("V5.3 first-run local runtime setup...");
        string assets = Path.Combine(pluginRoot, "runtime_assets");
        string pyZip = Path.Combine(assets, "python-3.10-embed-amd64.zip");
        string getPip = Path.Combine(assets, "get-pip.py");

        if(!File.Exists(pyZip))
            throw new FileNotFoundException("Embedded Python runtime archive missing",pyZip);
        if(!File.Exists(getPip))
            throw new FileNotFoundException("get-pip.py missing",getPip);

        if(Directory.Exists(pythonRoot))
            Directory.Delete(pythonRoot,true);
        Directory.CreateDirectory(pythonRoot);
        ZipFile.ExtractToDirectory(pyZip,pythonRoot,true);

        string pth = Directory.GetFiles(pythonRoot,"python*._pth").FirstOrDefault()
            ?? throw new FileNotFoundException("Python ._pth file not found");
        var lines = File.ReadAllLines(pth).ToList();
        bool hasSite=false;
        for(int i=0;i<lines.Count;i++)
        {
            if(lines[i].Trim()=="#import site" || lines[i].Trim()=="import site")
            {
                lines[i]="import site";
                hasSite=true;
            }
        }
        if(!hasSite) lines.Add("import site");
        File.WriteAllLines(pth,lines,new UTF8Encoding(false));

        Console.WriteLine("Installing pip...");
        int c=Run(pythonExe,Quote(getPip),"",null,true);
        if(c!=0) throw new InvalidOperationException("pip bootstrap failed with code "+c);

        string requirements = string.Join(" ", new[]
        {
            "torch-directml",
            "diffusers==0.27.2",
            "transformers==4.36.2",
            "huggingface-hub==0.20.3",
            "safetensors==0.4.5",
            "pillow==10.2.0",
            "opencv-python-headless==4.10.0.84",
            "numpy==1.26.4",
            "onnxruntime==1.19.2",
            "protobuf==3.20.3",
            "einops==0.8.0"
        });

        Console.WriteLine("Installing local DirectML AI packages. This is a one-time setup...");
        c=Run(pythonExe,"-m pip install --disable-pip-version-check --no-warn-script-location "+requirements,"",null,true);
        if(c!=0) throw new InvalidOperationException("Local AI package installation failed with code "+c);

        Console.WriteLine("Verifying DirectML / Python runtime...");
        string verify="import torch; print('torch',torch.__version__); import torch_directml; print('directml',torch_directml.device())";
        c=Run(pythonExe,"-c "+Quote(verify),"",null,true);
        if(c!=0)
            Console.WriteLine("DirectML verification failed; Auto mode may use CPU fallback.");
    }

    static int Run(string exe,string arguments,string workingDirectory,
        Dictionary<string,string?>? env,bool echo)
    {
        var psi=new ProcessStartInfo
        {
            FileName=exe,
            Arguments=arguments,
            WorkingDirectory=string.IsNullOrWhiteSpace(workingDirectory)
                ? Path.GetDirectoryName(exe)! : workingDirectory,
            UseShellExecute=false,
            RedirectStandardOutput=true,
            RedirectStandardError=true,
            CreateNoWindow=true
        };
        if(env!=null)
            foreach(var kv in env)
                psi.Environment[kv.Key]=kv.Value ?? "";

        using var p=new Process { StartInfo=psi };
        p.OutputDataReceived += (_,e)=> { if(e.Data!=null && echo) Console.WriteLine(e.Data); };
        p.ErrorDataReceived += (_,e)=> { if(e.Data!=null && echo) Console.Error.WriteLine(e.Data); };
        if(!p.Start()) throw new InvalidOperationException("Cannot start "+exe);
        p.BeginOutputReadLine();
        p.BeginErrorReadLine();
        p.WaitForExit();
        return p.ExitCode;
    }

    static string Quote(string s) => "\"" + s.Replace("\"","\\\"") + "\"";
}
