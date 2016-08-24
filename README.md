# Visual Studio C++ Plugin

[![Build status](https://ci.appveyor.com/api/projects/status/xq7g1w19ufbx3htt?svg=true)](https://ci.appveyor.com/project/Dllieu/visualstudiocppextensions)

## About
Custom plugins for Visual Studio 2015+:
- **Generate Filter**
 - Simple Extension which provide the ability to generate C++ project filters to replicate the folder hierarchy of the existing underlying sources
 - It also automatically generate the Additional Include Directories
 - It was originally made to browse easily C++ code hosted on a Linux machine

<p align="center">
  <img src="images/usage_project_no_filter.png" alt="Project without filter"/>
  <img src="images/usage_project_generate_filter_result.png" alt="Project with filter replicating the folder hierarchy"/>
</p>

## Installation
- Download the ```*.vsix``` package from the **[latest release](https://github.com/Dllieu/VisualStudioCppExtensions/releases/latest)**
- Double click on the downloaded package and follow the instructions

## Example Usage
Open an existing C++ solution

![Project without filters](images/usage_project_no_filter.png)


Right click on the project for which you want to generate the filters per folder, a menu ```Generate C++ Project Filters``` will appear (*only appearing when right-clicking on a C++ project*)

![Right click on the project](images/usage_project_right_click.png)

Click on ```Generate C++ Project Filters```, a confirmation will be required to generate the filters

![Notification for confirmation](images/usage_project_generate_filter_confirmation.png)

Once the filters are generated, the extension will automatically reload the current project if needed. Accept the changes made by the extension by clicking yes

![Save change made by Generate Filter](images/usage_project_generate_filter_save_change.png)

Finally, your C++ project will have some filter that replicate your C++ project folder hierarchy

![Result](images/usage_project_generate_filter_result.png)

## Dependencies
You have to install Visual Studio 2015 SDK

### Debug
You have to change the debug settings for the VSIXProject to be able to debug it (```Properties -> Debug```)
- Start external program : ```C:\Program Files (x86)\Microsoft Visual Studio 14.0\Common7\IDE\devenv.exe```
- Command line arguments: ```/rootsuffix EXP```
