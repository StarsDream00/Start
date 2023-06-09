﻿using Start.Utils;
using System.Diagnostics;
using System.IO.Compression;
using System.Text;

Console.InputEncoding = Encoding.UTF8;
Console.OutputEncoding = Encoding.UTF8;

Process process = default;
bool Updateing = false;
Update();
Start();
_ = Task.Run(() =>
{
    while (true)
    {
        Thread.Sleep(600000);
        Update();
    }
});
while (true)
{
    string input = Console.ReadLine();
    process.StandardInput.WriteLine(Encoding.Default.GetString(Encoding.UTF8.GetBytes(input)));
    if (input is "stop")
    {
        process.WaitForExit();
        Environment.Exit(0);
    }
}
void Start()
{
    process = new Process
    {
        StartInfo = new ProcessStartInfo
        {
            FileName = "bedrock_server.exe",
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        }
    };
    if (!process.Start())
    {
        Logger.Trace("开服失败！将重试！", Logger.LogLevel.ERROR);
        Start();
        return;
    }
    process.BeginOutputReadLine();
    process.OutputDataReceived += (_, e) =>
    {
        Console.WriteLine(e.Data);
    };
    process.BeginErrorReadLine();
    process.ErrorDataReceived += (_, e) =>
    {
        Console.WriteLine(e.Data);
    };
    process.Exited += (_, _) =>
    {
        while (Updateing)
        {
            Thread.Yield();
        }
        Logger.Trace("检测到服务器关闭，正在启动", Logger.LogLevel.WARN);
        Start();
    };
}
async void Update()
{
    Logger.Trace("开始检测更新");
    CancellationTokenSource tokenSource = new();
    HttpClient httpClient = new();
    string link = default;
    Task task = Task.Run(async () => link = Regex.UrlRegex().Match(await httpClient.GetStringAsync("https://www.minecraft.net/download/server/bedrock")).Value, tokenSource.Token);
    task.Wait(10000);
    if (!task.IsCanceled)
    {
        tokenSource.Cancel();
    }
    string version = Regex.VersionRegex().Match(link).Value;
    if (!string.IsNullOrWhiteSpace(link) && (!File.Exists("lv.dat") || File.ReadAllText("lv.dat") != version))
    {
        Logger.Trace($"检测到新版本{version}，开始更新");
        if (process is not null && !process.HasExited)
        {
            process.StandardInput.WriteLine(Encoding.Default.GetString(Encoding.UTF8.GetBytes($"say 即将关闭服务器并更新至{version}！")));
        }
        Directory.CreateDirectory("cache");

        File.WriteAllBytes("cache/bds.zip", await httpClient.GetByteArrayAsync(link));
        Updateing = true;
        if (process is not null && !process.HasExited)
        {
            process.StandardInput.WriteLine(Encoding.Default.GetString(Encoding.UTF8.GetBytes("stop")));
            process.WaitForExit();
        }
        Logger.Trace("开始更新");
        File.Copy("server.properties", "cache/server.properties");
        ZipFile.ExtractToDirectory("cache/bds.zip", ".", true);
        File.Copy("cache/server.properties", "server.properties", true);
        Updateing = false;
        File.WriteAllText("lv.dat", version);
        Directory.Delete("cache", true);
        Logger.Trace("更新完毕，正在启动");
    }
    Logger.Trace("检测完毕");
}

