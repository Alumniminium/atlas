# Atlas
*the launch rocket of the gemini capsule*

* C#
* net7.0
* zero dependencies
* Linux and Windows
* x86, x64 and ARM

# Features
* gemini:// with titan:// file uploads
* spartan:// file uploads and downloads
* CGI interface compatible with [jetforce](https://github.com/michael-lazar/jetforce) 
* CGI streaming (for things like gemini://chat.mozz.us/)
* vhosts
* directory listing
* easy JSON config file
* easy tsv Mimetype map
* dockerfile available

## Sample configuration
```json
{
  "Port": 1965,
  "Capsules": {
    "sallsave.net": {
      "AbsoluteRootPath": "/srv/gemini/sallsave.net/",
      "AbsoluteTlsCertPath": "/srv/gemini/sallsave.net/sallsave.net.pfx",
      "FQDN": "sallsave.net",
      "Index": "index.gmi",
      "Locations": [
        {
          "AbsoluteRootPath": "/srv/gemini/sallsave.net/",
          "Index": "index.gmi",
        }
      ]
    },
    "evilcorp.net": {
      "AbsoluteRootPath": "/srv/gemini/evilcorp.net/",
      "AbsoluteTlsCertPath": "/srv/gemini/evilcorp.net/evilcorp.net.pfx",
      "FQDN": "evilcorp.net",
      "Index": "index.gmi",
      "MaxUploadSize": 4194304,
      "Locations": [
        {
          "AbsoluteRootPath": "/srv/gemini/evilcorp.net/",
          "Index": "index.gmi",
        },
        {
          "AbsoluteRootPath": "/srv/gemini/evilcorp.net/cgi/",
          "Index": "script.csx",
          "CGI": true,
          "RequireClientCert": true,
        },
        {
          "AbsoluteRootPath": "/srv/gemini/evilcorp.net/files/",
          "Index": "index.gmi",
          "DirectoryListing": true,
          "AllowFileUploads": true,
          "AllowedMimeTypes": {
            "text/*": {
              "MaxSizeBytes": 1048576
            },
            "image/*": {},
            "audio/mpeg": {},
            "audio/ogg": {},
            "audio/wave": {}
          }
        }
      ]
    }
  }
}
```

## sample CGI script

```cs
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
        Console.Write("10 Please Enter Filname\r\n");
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
```