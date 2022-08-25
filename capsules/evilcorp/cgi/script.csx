#!/usr/bin/env dotnet-script

using System.Collections;
using System.Net;
using Internal;
using System.Threading;

try
{
    var query = Environment.GetEnvironmentVariable("QUERY_STRING");
    var SERVER_NAME = Environment.GetEnvironmentVariable("SERVER_NAME");
    var SERVER_PROTOCOL = Environment.GetEnvironmentVariable("SERVER_PROTOCOL");

    if (query == "?upload")
    {
        Console.Write("10 Please Enter Filname\r\n");
    }
    if (query == "?stream")
    {
        Console.Write("20 text/gemini\r\n");
        for (int i = 0; i < 200; i++)
        {
            Thread.Sleep(1000);
            Console.WriteLine("i="+i);
        }
    }
    else if (query == "")
    {
        Console.Write("20 text/gemini\r\n");
        Console.WriteLine();
        Console.WriteLine("=> ?upload Upload");
        Console.WriteLine();
        foreach (DictionaryEntry kvp in Environment.GetEnvironmentVariables())
            Console.WriteLine($"{kvp.Key} = {kvp.Value}");

        Console.WriteLine();
        Console.WriteLine();

        var info = new ProcessStartInfo();
        info.UseShellExecute = false;
        info.FileName = $"/usr/bin/neofetch";
        info.RedirectStandardOutput = true;
        info.RedirectStandardError = true;
        var p = new Process();
        p.StartInfo = info;
        p.Start();
        p.WaitForExit();
        foreach(var line in p.StandardOutput.ReadToEnd().Split("\n"))
            Console.WriteLine($"{line}");
    }
    else
    {
        var pairs = query.Split('?', StringSplitOptions.RemoveEmptyEntries);
        var filename = query.Replace("?","").Replace("/","").Replace("..","");
        var path = $"../files/{filename}";
        var ext = Path.GetExtension(filename);

        if(string.IsNullOrEmpty(ext))
        {
            Console.Write($"40 missing extension!\r\n");
            return;
        }

        if(File.Exists(path))
        {
            Console.Write($"40 {filename} exists!\r\n");
            return;
        }

        File.Create(path);
        Console.Write($"30 {SERVER_PROTOCOL}://{SERVER_NAME}/files/{filename}\r\n");
    }  
}
catch (System.Exception e)
{
    Console.Write($"40 fuq\n{e.Message}\r\n");
}