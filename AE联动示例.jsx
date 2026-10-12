// Enqueue the saved project. Does not start a paused queue; a running queue may dispatch it.
(function () {
    function quote(s) { return '"' + String(s).replace(/\\/g, "\\\\").replace(/"/g, '\\"').replace(/[\x00-\x1f]/g, function(c) { return "\\u" + ("0000" + c.charCodeAt(0).toString(16)).slice(-4); }) + '"'; }
    function pad(n) { return (n < 10 ? "0" : "") + n; }
    function expiry() { var d = new Date(new Date().getTime() + 30000); return d.getUTCFullYear() + "-" + pad(d.getUTCMonth()+1) + "-" + pad(d.getUTCDate()) + "T" + pad(d.getUTCHours()) + ":" + pad(d.getUTCMinutes()) + ":" + pad(d.getUTCSeconds()) + "Z"; }
    function call(action, fields) {
        var base = $.getenv("LOCALAPPDATA") + "/AepBatchRenderer/bridge/";
        if (!Folder(base + "requests").exists) throw Error("请先打开 AEP Render（3.4 或更高版本）。");
        var id = ""; for (var i=0; i<32; i++) id += Math.floor(Math.random()*16).toString(16);
        var req = File(base + "requests/" + id + ".tmp"); req.encoding = "UTF-8";
        if (!req.open("w")) throw Error("无法写入请求；请检查 AE 脚本文件访问权限。");
        req.write('{"action":' + quote(action) + ',"expiresUtc":' + quote(expiry()) + (fields || "") + '}'); req.close();
        if (!req.rename(id + ".json")) throw Error("无法提交请求。");
        var result = File(base + "results/" + id + ".json"), deadline = new Date().getTime() + 5000;
        while (!result.exists && new Date().getTime() < deadline) $.sleep(100);
        if (!result.exists) throw Error("等待响应超时。请求可能仍会执行，请勿盲目重复提交。请求 ID：" + id);
        result.encoding = "UTF-8"; if (!result.open("r")) throw Error("无法读取响应。");
        var text = result.read(); result.close();
        // Do not eval external JSON in ExtendScript. Return raw JSON to the caller.
        if (!/"ok"\s*:\s*true/.test(text)) throw Error(text);
        return text;
    }
    try {
        if (!app.project || !app.project.file) throw Error("请先保存当前 AE 项目。");
        var response = call("enqueue", ',"paths":[' + quote(app.project.file.fsName) + ']');
        alert("加入队列请求已处理。\n暂停的队列不会自动启动；运行中的队列会正常派发。\n" + response);
    } catch(e) { alert("AEP Render 联动：" + e.message); }
})();
