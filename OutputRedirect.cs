using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;

namespace AepBatchRenderer
{
    internal static class OutputRedirect
    {
        // Each invocation owns a separate AE process and temporary project. Never save the source.
        internal static string Script(string source, string folder, string project, string result)
        {
            var json = new JavaScriptSerializer();
            return "var messages="+json.Serialize(new[]{UiText.Get("output.noFilename"),UiText.Get("output.noQueue")})+";\nvar source="+json.Serialize(source)+", destination="+json.Serialize(folder)+", copy="+json.Serialize(project)+", result="+json.Serialize(result)+";\n" + @"
(function () {
    app.exitAfterLaunchAndEval = true;
    app.beginSuppressDialogs();
    var message = '', paths = [];
    try {
        app.open(new File(source));
        var queue = app.project.renderQueue;
        for (var i=1; i<=queue.numItems; i++) {
            var item = queue.item(i);
            if (item.status !== RQItemStatus.QUEUED) continue;
            for (var m=1; m<=item.numOutputModules; m++) {
                var module = item.outputModule(m), original = module.file;
                if (!original) throw new Error(messages[0]);
                var file = new File(new Folder(destination).fsName + '/' + original.name);
                module.file = file;
                paths.push(file.fsName);
            }
        }
        if (!paths.length) throw new Error(messages[1]);
        app.project.save(new File(copy));
        message = 'OK\n' + paths.join('\n');
    } catch (e) { message = 'ERROR\n' + e.toString(); }
    finally {
        try { app.project.close(CloseOptions.DO_NOT_SAVE_CHANGES); } catch (_) {}
        var report = new File(result); report.encoding = 'UTF-8';
        if (report.open('w')) { report.write(message); report.close(); }
        app.endSuppressDialogs(false);
    }
})();";
        }

        internal static void ValidateOutputs(string[] paths)
        {
            for (int i=0; i<paths.Length; i++)
            {
                string name=Path.GetFileName(paths[i]);
                string pattern=Regex.Replace(name,@"\[#+\]","*");
                string regex="^"+Regex.Escape(pattern).Replace("\\*", ".*")+"$";
                if (paths.Take(i).Any(p=>String.Equals(p,paths[i],StringComparison.OrdinalIgnoreCase) || Regex.IsMatch(Path.GetFileName(p),regex,RegexOptions.IgnoreCase)))
                    throw new Exception(UiText.Get("text.246")+name);
                if (Directory.GetFileSystemEntries(Path.GetDirectoryName(paths[i]),pattern).Length>0)
                    throw new Exception(UiText.Get("text.247")+paths[i]);
            }
        }

        public static string Prepare(QueueJob job, CancellationToken token)
        {
            string engine=Path.Combine(Path.GetDirectoryName(job.Engine),"AfterFX.com");
            if (!File.Exists(engine)) engine=Path.Combine(Path.GetDirectoryName(job.Engine),"AfterFX.exe");
            if (!File.Exists(engine)) throw new Exception(UiText.Get("text.248")+engine);
            Directory.CreateDirectory(job.Output);
            string folder=Path.Combine(Store.Root,"temporary-output",Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            string project=Path.Combine(folder,Path.GetFileNameWithoutExtension(job.Project)+".aep"), script=Path.Combine(folder,"prepare.jsx"), result=Path.Combine(folder,"result.txt");
            try
            {
                File.WriteAllText(script,Script(job.Project,job.Output,project,result),new UTF8Encoding(true));
                using(var process=new Process())
                using(var scope=new ProcessScope())
                {
                    process.StartInfo=new ProcessStartInfo(engine,"-m -noui -r "+RenderCommand.Quote(script)) { UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden,WorkingDirectory=Path.GetDirectoryName(engine) };
                    token.ThrowIfCancellationRequested(); process.Start();
                    try { scope.Attach(process); } catch { try { process.Kill(); process.WaitForExit(); } catch {} throw; }
                    var timer=Stopwatch.StartNew();
                    while(!process.WaitForExit(100))
                    {
                        if(token.IsCancellationRequested || timer.Elapsed.TotalMinutes >= (job.Options.TimeoutMinutes>0 ? job.Options.TimeoutMinutes : 10))
                        {
                            scope.Dispose(); process.WaitForExit(); token.ThrowIfCancellationRequested();
                            throw new Exception(UiText.Get("text.249"));
                        }
                    }
                    token.ThrowIfCancellationRequested();
                    if(process.ExitCode!=0) throw new Exception(UiText.Get("text.250")+process.ExitCode);
                }
                if(!File.Exists(result)) throw new Exception(UiText.Get("text.251"));
                var lines=File.ReadAllLines(result,Encoding.UTF8);
                if(lines.Length<2 || lines[0]!="OK") throw new Exception(UiText.Get("text.252")+String.Join(" ",lines.Skip(1)));
                if(!File.Exists(project)) throw new Exception(UiText.Get("text.253"));
                ValidateOutputs(lines.Skip(1).Where(p=>p.Length>0).ToArray());
                return project;
            }
            catch { Cleanup(project); throw; }
        }

        public static void Cleanup(string project)
        {
            string folder=Path.GetDirectoryName(project);
            foreach(string name in new[]{Path.GetFileName(project),"prepare.jsx","result.txt"})
                try { File.Delete(Path.Combine(folder,name)); } catch {}
            try { Directory.Delete(folder); } catch {}
        }
    }
}
