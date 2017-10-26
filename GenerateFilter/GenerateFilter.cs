//------------------------------------------------------------------------------
// <copyright file="GenerateFilter.cs" company="Company">
//     Copyright (c) Company.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

using System;
using System.ComponentModel.Design;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.Internal.VisualStudio.PlatformUI;
using EnvDTE;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Reflection;
using System.Text;

namespace VisualStudioCppExtensions
{
    internal sealed class GenerateFilter
    {
        #region ATTRIBUTES
        public const int CommandId = 0x0100;
        public static readonly Guid CommandSet = new Guid("acd8036f-19ae-43b2-a2d6-11788cb282fe");
        private readonly Package package;
        #endregion
        #region CALLBACKS
        /// <summary>
        /// Only Display Generate Filter Button if we right click on a C++ Project
        /// </summary>
        void    OnBeforeQueryStatus(object sender, EventArgs e)
        {
            var menuCommand = sender as OleMenuCommand;
            if (menuCommand == null)
                return;
            
            var shouldActivateButton = IsCppProject(GetActiveProject());
            menuCommand.Visible = shouldActivateButton;
            menuCommand.Enabled = shouldActivateButton;
        }
        #endregion
        /// <summary>
        /// Initializes a new instance of the <see cref="GenerateFilter"/> class.
        /// Adds our command handlers for menu (commands must exist in the command table file)
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        private GenerateFilter(Package package)
        {
            if (package == null)
            {
                throw new ArgumentNullException("package");
            }

            this.package = package;

            OleMenuCommandService commandService = this.ServiceProvider.GetService(typeof(IMenuCommandService)) as OleMenuCommandService;
            if (commandService != null)
            {
                var menuCommandID = new CommandID(CommandSet, CommandId);
                var menuItem = new OleMenuCommand(this.MenuItemCallback, menuCommandID);
                menuItem.BeforeQueryStatus += OnBeforeQueryStatus;

                commandService.AddCommand(menuItem);
            }
        }

        /// <summary>
        /// Gets the instance of the command.
        /// </summary>
        public static GenerateFilter Instance
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets the service provider from the owner package.
        /// </summary>
        private IServiceProvider ServiceProvider
        {
            get
            {
                return this.package;
            }
        }

        /// <summary>
        /// Initializes the singleton instance of the command.
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        public static void Initialize(Package package)
        {
            Instance = new GenerateFilter(package);
        }

        #region PROJECT UTILS
        internal static Project GetActiveProject()
        {
            var dte = Package.GetGlobalService(typeof(SDTE)) as DTE;
            return GetActiveProject(dte);
        }

        internal static Project GetActiveProject(DTE dte)
        {
            var activeSolutionProjects = dte.ActiveSolutionProjects as Array;
            if (activeSolutionProjects == null || activeSolutionProjects.Length == 0)
                return null;

            return activeSolutionProjects.GetValue(0) as Project;
        }

        private static bool IsCppProject(Project project)
        {
            return project != null
                   && (project.CodeModel.Language == CodeModelLanguageConstants.vsCMLanguageMC
                       || project.CodeModel.Language == CodeModelLanguageConstants.vsCMLanguageVC);
        }

        public IEnumerable<ProjectItem> Recurse(ProjectItems i)
        {
            if (i != null)
            {
                foreach (ProjectItem j in i)
                {
                    foreach (ProjectItem k in Recurse(j))
                    {
                        yield return k;
                    }
                }
            }
        }

        public IEnumerable<ProjectItem> Recurse(ProjectItem i)
        {
            yield return i;
            foreach (var j in Recurse(i.ProjectItems))
            {
                yield return j;
            }
        }

        static private string GetAssemblyLocalPathFrom(Type type)
        {
            string codebase = type.Assembly.CodeBase;
            var uri = new Uri(codebase, UriKind.Absolute);
            return uri.LocalPath;
        }

        static private void SetAdditionalIncludeDirectories(Project project, Dictionary<string, List<string>> filesPerItemType, string projectPath)
        {
            if (!filesPerItemType.ContainsKey("ClInclude"))
                return;

            var includePaths = new HashSet<string> { @"$(StlIncludeDirectories)" };
            foreach (var file in filesPerItemType["ClInclude"])
            {
                includePaths.Add(GetRelativePathIfNeeded(projectPath, Path.GetDirectoryName(file)));
            }

            string filterAssemblyInstallionPath = Path.GetDirectoryName(GetAssemblyLocalPathFrom(typeof(GenerateFilterPackage)));

            DTE dte = (DTE)Package.GetGlobalService(typeof(DTE));
            if (dte.Version.StartsWith("14"))
            {
                Assembly.LoadFrom(Path.Combine(filterAssemblyInstallionPath, @"Resources\\VCProjectEngine_14.0.dll"));
            }
            else
            {
                Assembly.LoadFrom(Path.Combine(filterAssemblyInstallionPath, @"Resources\\VCProjectEngine_15.0.dll"));
            }

            dynamic vcProject = project.Object;
            foreach (dynamic vcConfiguration in vcProject.Configurations)
            {
                foreach (dynamic genericTool in vcConfiguration.Tools)
                {
                    dynamic compilerTool = genericTool;
                    if (compilerTool != null && compilerTool.GetType().FullName == "Microsoft.VisualStudio.Project.VisualC.VCProjectEngine.VCCLCompilerToolShim")
                    {
                        // runtime
                        if (compilerTool.AdditionalIncludeDirectories == null)
                            compilerTool.AdditionalIncludeDirectories = string.Empty;
                        
                        var currentAdditionalIncludeDirectories = new HashSet<string>(compilerTool.AdditionalIncludeDirectories.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries));
                        var pathsToAdd = new StringBuilder();
                        foreach (var includePath in includePaths)
                            // Avoid updating AdditionalIncludeDirectories when applicable to avoid reloading the project
                            if (!currentAdditionalIncludeDirectories.Contains(includePath))
                                pathsToAdd.Append(includePath + ';');

                        if (pathsToAdd.Length > 0)
                            compilerTool.AdditionalIncludeDirectories = pathsToAdd.ToString() + compilerTool.AdditionalIncludeDirectories;
                    }
                }
            }
        }
        #endregion
        #region ERROR BOX
        private void ErrorMessageBox(string errorMessage)
        {
            VsShellUtilities.ShowMessageBox(this.ServiceProvider,
                                            errorMessage,
                                            string.Empty,
                                            OLEMSGICON.OLEMSGICON_CRITICAL,
                                            OLEMSGBUTTON.OLEMSGBUTTON_OK,
                                            OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
        }
        #endregion
        #region PATH UTILS
        private static string FindCommonPath(Dictionary<string, List<string>> filesPerItemType)
        {
            if (filesPerItemType == null || filesPerItemType.Count == 0)
                return string.Empty;

            var result = string.Empty;
            foreach (var entry in filesPerItemType)
            {
                foreach (var path in entry.Value)
                {
                    if (path == null)
                        return string.Empty;

                    if (result == string.Empty)
                    {
                        result = Path.GetDirectoryName(path);
                        continue;
                    }

                    var currentPath = Path.GetDirectoryName(path);
                    var indexMaxEqual = 0;
                    while (indexMaxEqual < result.Length
                        && indexMaxEqual < currentPath.Length
                        && result[indexMaxEqual] == currentPath[indexMaxEqual])
                    {
                        ++indexMaxEqual;
                    }

                    if (indexMaxEqual == 0)
                        return string.Empty;

                    if (indexMaxEqual == result.Length)
                        continue;

                    if (indexMaxEqual < result.Length)
                        result = result.Substring(0, indexMaxEqual);
                }
            }
            return result;
        }

        private static HashSet<string> GenerateUniquePathByFilter(string commonPath, Dictionary<string, List<string>> filesPerItemType)
        {
            var result = new HashSet<string>();
            foreach (var entry in filesPerItemType)
                foreach (var file in entry.Value)
                {
                    var path = Path.GetDirectoryName(file);
                    if (path.Length == commonPath.Length)
                        continue;

                    path = GetPathExtensionFromCommonPath(commonPath, path);
                    result.Add(path);
                    for (var i = path.LastIndexOf(Path.DirectorySeparatorChar); i != -1; i = path.LastIndexOf(Path.DirectorySeparatorChar, i - 1))
                        result.Add(path.Substring(0, i));
                }
            return result;
        }

        static private string GetRelativePathIfNeeded(string parentPath, string file)
        {
            if (Path.GetPathRoot(parentPath) != Path.GetPathRoot(file))
                return file;

            var pathUri = new Uri(file);

            // Folders must end in a slash
            var formalizedUriParentPath = parentPath;
            if (!parentPath.EndsWith(Path.DirectorySeparatorChar.ToString()))
            {
                formalizedUriParentPath += Path.DirectorySeparatorChar;
            }

            var folderUri = new Uri(formalizedUriParentPath);
            return Uri.UnescapeDataString(folderUri.MakeRelativeUri(pathUri).ToString().Replace('/', Path.DirectorySeparatorChar));
        }

        /// <summary>
        /// predicate: commonPath.Length < path.Length
        /// </summary>
        private static string GetPathExtensionFromCommonPath(string commonPath, string path)
        {
            var shift = 0;
            if (path[commonPath.Length] == Path.DirectorySeparatorChar)
                shift = 1;

            return path.Substring(commonPath.Length + shift, path.Length - commonPath.Length - shift);
        }
        #endregion
        #region XML UTILS
        private static void WriteFilter(XmlWriter xmlWriter, HashSet<string> filters)
        {
            if (filters == null || filters.Count == 0)
                return;

            xmlWriter.WriteStartElement("ItemGroup");
            foreach (var filter in filters)
            {
                xmlWriter.WriteStartElement("Filter");
                xmlWriter.WriteAttributeString("Include", filter);

                {
                    xmlWriter.WriteStartElement("UniqueIdentifier");
                    xmlWriter.WriteString("{" + Guid.NewGuid().ToString() + "}");
                    xmlWriter.WriteEndElement();
                }

                xmlWriter.WriteEndElement();
            }
            xmlWriter.WriteEndElement();
        }

        private static void WriteSources(XmlWriter xmlWriter, string itemType, List<string> files, string projectPath, string commonPath)
        {
            if (files == null || files.Count == 0)
                return;

            // Only write if one occurence
            xmlWriter.WriteStartElement("ItemGroup");
            foreach (var file in files)
            {
                var path = Path.GetDirectoryName(file);
                if (path.Length == commonPath.Length)
                    continue;

                xmlWriter.WriteStartElement(itemType);
                xmlWriter.WriteAttributeString("Include", GetRelativePathIfNeeded(projectPath, file));

                {
                    xmlWriter.WriteStartElement("Filter");
                    xmlWriter.WriteString(GetPathExtensionFromCommonPath(commonPath, path));
                    xmlWriter.WriteEndElement();
                }

                xmlWriter.WriteEndElement();
            }
            xmlWriter.WriteEndElement();
        }
        #endregion

        /// <summary>
        /// This function is the callback used to execute the command when the menu item is clicked.
        /// See the constructor to see how the menu item is associated with this function using
        /// OleMenuCommandService service and MenuCommand class.
        /// </summary>
        private void MenuItemCallback(object sender, EventArgs e)
        {
            Project project = GetActiveProject();
            if (!IsCppProject(project))
            {
                ErrorMessageBox("A C++ project must be selected to generate filter!");
                return;
            }

            if (VsShellUtilities.ShowMessageBox(this.ServiceProvider,
                                                string.Format("Generate filter per folder for '{0}'?\nExisting filters will be erased", project.UniqueName),
                                                string.Empty,
                                                OLEMSGICON.OLEMSGICON_WARNING,
                                                OLEMSGBUTTON.OLEMSGBUTTON_OKCANCEL,
                                                OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST) == DialogResult.Cancel)
                return;

            // ClCompile -> .cpp, .cc, .c, ...
            // ClInclude -> .h, .hxx, .hpp, ...
            // None -> Makefile, .gitignore, ...
            var filesPerItemType = new Dictionary<string /* ItemType */, List<string> /* Files FullPath */>();
            foreach (ProjectItem projectItem in Recurse(project.ProjectItems))
            {
                if (projectItem.Kind == EnvDTE.Constants.vsProjectItemKindPhysicalFile)
                {
                    try
                    {
                        var itemType = projectItem.Properties.Item("ItemType").Value as string;
                        if (string.IsNullOrEmpty(itemType))
                            continue;

                        if (!filesPerItemType.ContainsKey(itemType))
                            filesPerItemType.Add(itemType, new List<string>());

                        filesPerItemType[itemType].Add(projectItem.Properties.Item("FullPath").Value as string);
                    }
                    catch (Exception)
                    {
                        // nothing
                    }
                }
            }

            var commonPath = FindCommonPath(filesPerItemType);
            if (string.IsNullOrEmpty(commonPath))
            {
                ErrorMessageBox("No common sub-path between files, cannot generate filter!");
                return;
            }

            // Keep for Post-Unloading
            var projectFilename = project.FileName;
            var projectPath = Path.GetDirectoryName(projectFilename);
            project.DTE.ExecuteCommand("Project.UnloadProject");

            var xmlSettings = new XmlWriterSettings() { Indent = true };
            using (var xmlWriter = XmlWriter.Create(projectFilename + ".filters", xmlSettings))
            {
                xmlWriter.WriteStartElement("Project");
                xmlWriter.WriteAttributeString("ToolsVersion", "4.0");
                xmlWriter.WriteAttributeString("Project", "xmlns", null, @"http://schemas.microsoft.com/developer/msbuild/2003");

                WriteFilter(xmlWriter, GenerateUniquePathByFilter(commonPath, filesPerItemType));
                foreach (var entry in filesPerItemType)
                    WriteSources(xmlWriter, entry.Key, entry.Value, projectPath, commonPath);

                xmlWriter.WriteEndElement();
            }
            project.DTE.ExecuteCommand("Project.ReloadProject");
        }
    }
}
