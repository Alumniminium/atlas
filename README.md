# Atlas
*the launch rocket of the gemini capsule*

* C#
* net7.0
* zero dependencies
* Linux and Windows
* x86, x64 and ARM

## Features
* analythics
* server side animations on supported clients (eg. Lagrange)
* gemini:// with titan:// file uploads
* automatic certificate generation if none specified
* spartan:// file uploads and downloads
* CGI interface compatible with [jetforce](https://github.com/michael-lazar/jetforce) 
* CGI streaming (for things like gemini://chat.mozz.us/)
* vhosts
* directory listing
* JSON config file
* tsv Mimetype map
* Docker Support

# Atlas Statistics
gemini://yourserver.com/atlas.stats
## Hits

```
  32┫                                         ███                
    ┫                                         ███                
    ┫                                         ███                
    ┫                                         ███                
  24┫                                         ███                
    ┫                                         ███                
    ┫                                         ███                
    ┫                                         ███                
  16┫                                         ███                
    ┫                                         ███                
    ┫                                         ███                
    ┫                                         ███                
   8┫                                         ███                
    ┫                                         ███                
    ┫                                         ███                
   2┫                                         ███                
    ┗━━╋━━━━╋━━━━╋━━━━╋━━━━╋━━━━╋━━━━╋━━━━╋━━━━╋━━━━╋━━━━╋━━━━╋━━
       J    F    M    A    M    J    J    A    S    O    N    D  
       a    e    a    p    a    u    u    u    e    c    o    e  
       n    b    r    r    y    n    l    g    p    t    v    c  
```
## Requests
```
0 ┣━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
1 ┣━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
2 ┣━━━━━━━━━━━━━━━━━━━━━
3 ┣━━━━━━━━━━━━━━━━━━━━━
4 ┣━━━━━━━━━━━━━━━━━━━━━
5 ┣━━━━━━━━━━━━━━
6 ┣━━━━━━
7 ┣━━━━━━
8 ┣━━━━━━

0  her.st/atlas.stats:                             10
1  her.st/:                                        8
2  her.st/blog/:                                   3
3  her.st/pages/fsociety.gmi:                      3
4  digdeeper.her.st/:                              3
5  digdeeper.her.st/articles/fake_initiatives.gmi: 2
6  her.st/holy-texts/cyberpunk-manifesto.gmi:      1
7  digdeeper.her.st/articles/websites.gmi:         1
8  digdeeper.her.st/index.gmi:                     1

```
## Bandwidth (Month)
```
205K┫                                         ███                
    ┫                                         ███                
    ┫                                         ███                
    ┫                                         ███                
154K┫                                         ███                
    ┫                                         ███                
    ┫                                         ███                
    ┫                                         ███                
102K┫                                         ███                
    ┫                                         ███                
    ┫                                         ███                
    ┫                                         ███                
 51K┫                                         ███                
    ┫                                         ███                
    ┫                                         ███                
 12K┫                                         ███                
    ┗━━╋━━━━╋━━━━╋━━━━╋━━━━╋━━━━╋━━━━╋━━━━╋━━━━╋━━━━╋━━━━╋━━━━╋━━
       J    F    M    A    M    J    J    A    S    O    N    D  
       a    e    a    p    a    u    u    u    e    c    o    e  
       n    b    r    r    y    n    l    g    p    t    v    c  
```
## Bandwidth (Day)
```
Sun:    0 ┣
Mon:  78K ┣━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
Tue: 126K ┣━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
Wed:    0 ┣
Thu:    0 ┣
Fri:    0 ┣
Sat:    0 ┣

```


## Roadmap (in no particular order):

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
          "AllowFileUploads": true, // public Titan/Spartan  uploads in this location
          "AllowedMimeTypes": {
            "text/*": { // whitelist all text files
              "MaxSizeBytes": 1048576 // override max upload size for text files
            },
            "image/*": {}, // whitelist all image files to be uploaded
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
## CGI Interface

The CGI interface provides the following environment variables:

| Variable | Description | Default |
|---|---|---|
| DOTNET_CLI_HOME | Required for .NET assemblies to execute | ~/.dotnet |
| GATEWAY_INTERFACE | CGI Version | CGI/1.1 |
| SERVER_PROTOCOL | Either Gemini or Spartan | GEMINI / SPARTAN |
| SERVER_PORT | Gemini or Spartan Port according to config.json | 1965 / 300 |
| SERVER_SOFTWARE | atlas/version string | atlas/0.2b |
| URL | URL of the Request | gemini://evil.corp/cgi/binary?queryString=value#fragment&token |
| SCRIPT_NAME | the CGI script name | binary |
| PATH_INFO | See CGI documentation | Hopefully correct |
| QUERY_STRING | Query from the URL | ?queryString=value#fragment&token |
| SERVER_NAME | the FQDN of the vhost | evil.corp |
| REMOTE_HOST | The IP of the client sending the request | 127.0.0.1 |
| REMOTE_ADDR | as above | as above |
| TLS_VERSION | Gemini Only | 1.3 |
| REMOTE_USER | TLS Cert Subject without CN= | trbl |
| TLS_CLIENT_SUBJECT | as above | as above |
| TLS_CLIENT_VALID | Certificate is not expired | true |
| TLS_CLIENT_TRUSTED | Certificate issued by atlas | false |
| TLS_CLIENT_HASH | The Certificate Thumbprint | 0baf2asdb23i02.. |
| TLS_CLIENT_NOT_BEFORE | Certificate Valid From Time | 08/28/2022 18:26:30 |
| TLS_CLIENT_NOT_AFTER | Certificate Valid To Time | 08/28/3000 18:26:30 |
| TLS_CLIENT_SERIAL_NUMBER | The Certificate Serial Number | |
| AUTH_TYPE | CERTIFICATE or NONE | NONE | 


## sample CGI script

Commenting **on** Articles
[atlas-comments](https://github.com/Alumniminium/atlas-comments)
