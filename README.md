# Atlas
*the launch rocket of the gemini capsule*

* C#
* net7.0
* zero dependencies
* Linux and Windows
* x86, x64 and ARM

## Features
* server side animations on supported clients (eg. Lagrange)
* gemini:// with titan:// file uploads
* automatic certificate generation if none specified
* spartan:// file uploads and downloads
* CGI interface compatible with [jetforce](https://github.com/michael-lazar/jetforce) 
* CGI streaming (for things like gemini://chat.mozz.us/)
* vhosts
* directory listing
* easy JSON config file
* easy tsv Mimetype map
* dockerfile available

### Roadmap (in no particular order):

* FastCGI
* Use single Docker volume
* caching
* certificate validation
* rate limiting
* proper networking with SocketAsyncEventArgs
* * not a priority, testing shows it scales to a few 100 concurrent users

### Sample configuration
```json
{
  "SlowMode": true, // animations, currently only for gemini
  "GeminiPort": 1965,
  "SpartanPort": 300,
  "Capsules": {
    "allsafe.net": {
      "AbsoluteRootPath": "/srv/gemini/allsafe.net/",
      "AbsoluteTlsCertPath": "/srv/gemini/allsafe.net/allsafe.net.pfx",
      "FQDN": "allsafe.net",
      "Index": "index.gmi",
      "Locations": [
        {
          "AbsoluteRootPath": "/srv/gemini/allsafe.net/",
          "Index": "index.gmi",
        }
      ]
    },
    "evilcorp.net": {
      "AbsoluteRootPath": "/srv/gemini/evilcorp.net/",
      "AbsoluteTlsCertPath": "",// will be automatically created and placed at AbsoluteRootPath/FQDN.pfx
      "FQDN": "evilcorp.net",
      "Index": "index.gmi",
      "MaxUploadSize": 4194304, // global max upload size
      "Locations": [
        {
          "AbsoluteRootPath": "/srv/gemini/evilcorp.net/",
          "Index": "index.gmi",
        },
        {
          "AbsoluteRootPath": "/srv/gemini/evilcorp.net/cgi/",
          "Index": "script.csx",
          "CGI": true,
          "RequireClientCert": true,  // disables access for spartan protocol due to lack of support
        },
        {
          "AbsoluteRootPath": "/srv/gemini/evilcorp.net/files/",
          "Index": "index.gmi",
          "DirectoryListing": true,
          "AllowFileUploads": true,
          "AllowedMimeTypes": {
            "text/*": {
              "MaxSizeBytes": 1048576 // override max upload size for text files
            },
            "image/*": {}, // allow all image files to be uploaded
            "audio/mpeg": {}, //
            "audio/ogg": {},  // whitelist certain audio files
            "audio/wave": {}  //
          }
        }
      ]
    }
  }
}
```

### sample CGI script

[atlas-comments](https://github.com/Alumniminium/atlas-comments)
