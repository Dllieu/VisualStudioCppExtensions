# Visual Studio C++ Plugin
Custom plugins for Visual Studio 2015+:
- **Generate Filter**
 - Simple Extension which provide the ability to generate C++ project filters to replicate the folder hierarchy of existing underlying sources / headers
 - It also automatically generate the Additional Include Directories
 - It was originally made to browse easily C++ code hosted on a Linux machine

## Requirement
### Dependencies
You need to install Visual Studio 2015 SDK
### Debug
If you download the code from GitHub you have to change the debug settings for the VSIXProject to be able to debug (```Properties -> Debug```)
- Start external program : ```C:\Program Files (x86)\Microsoft Visual Studio 14.0\Common7\IDE\devenv.exe```
- Command line arguments: ```/rootsuffix EXP```
