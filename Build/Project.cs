using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;
using System.Xml.Linq;

namespace Build
{
	[DebuggerDisplay("AssemblyName={AssemblyName}")]
	public class Project
	{
		public Project(string projectFilePath)
		{
			this.projectFilePath = projectFilePath;
			this.projectFile = XDocument.Parse(File.ReadAllText(projectFilePath));
		}

		readonly XDocument projectFile;

		public string ProjectFilePath
		{
			get { return projectFilePath; }
		}
		readonly string projectFilePath;

		internal string AssemblyName
		{
			get { return assemblyName ?? (assemblyName = projectFile.Descendants(XName.Get("AssemblyName", xmlns)).First().Value); }
		}
		string assemblyName;

		internal string OutputAssemblyPath
		{
			get { return outputAssemblyPath ?? (outputAssemblyPath = BuildOutputAssemblyPath()); }
		}
		string outputAssemblyPath;

		string BuildOutputAssemblyPath()
		{
			var assemblyPath = Path.Combine(Path.GetDirectoryName(this.projectFilePath), OutputPath, AssemblyName);
			var extension = IsLibrary ? ".dll" : ".exe";
			return assemblyPath + extension;
		}

		bool IsLibrary
		{
			get { return projectFile.Descendants(XName.Get("OutputType", xmlns)).First().Value == "Library"; }
		}

		string OutputPath
		{
			get { return projectFile.Descendants(XName.Get("OutputPath", xmlns)).First().Value; }
		}

		internal IEnumerable<string> References
		{
			get { return references ?? (references = projectFile.Descendants(XName.Get("Reference", xmlns)).SelectMany(e => e.Attributes("Include")).Select(a => a.Value).ToArray()); }
		}
		IEnumerable<string> references;

		const string xmlns = "http://schemas.microsoft.com/developer/msbuild/2003";
	}
}
