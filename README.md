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