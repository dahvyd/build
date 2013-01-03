using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;

namespace Build
{
	[DebuggerDisplay("AssemblyName={AssemblyName}")]
	public class Project
	{
		public Project(string projectFilePath)
		{
			this.projectFilePath = projectFilePath;
		}

		readonly string projectFilePath;
		const string assemblyNameIdentifierInProjectFile = "<AssemblyName>";
		const string assemblyReferencePrefixInProjectFile = "Reference Include=\"";

		public string BuildAndReturnStdOut(string msBuildPath)
		{
			Console.WriteLine("Building project {0}", AssemblyName);

			var process = new Process();
			process.StartInfo = new ProcessStartInfo(msBuildPath, projectFilePath);
			process.StartInfo.CreateNoWindow = false;
			process.StartInfo.UseShellExecute = false;
			process.StartInfo.RedirectStandardOutput = true;
			process.Start();
			process.WaitForExit(10000); // hack to get around issue with process not returning exit code correctly sometimes
			return process.StandardOutput.ReadToEnd();
		}

		internal string AssemblyName
		{
			get { return assemblyName ?? (assemblyName = BuildAssemblyName()); }
		}
		string assemblyName;
		
		string BuildAssemblyName()
		{
			var projectFileContents = File.ReadAllLines(projectFilePath);
			string assemblyNameLine = projectFileContents.First(line => line.Contains(assemblyNameIdentifierInProjectFile));
			int indexOfAssemblyName = assemblyNameLine.IndexOf(assemblyNameIdentifierInProjectFile) + assemblyNameIdentifierInProjectFile.Length;
			int indexOfEndAssembly = assemblyNameLine.IndexOf('<', indexOfAssemblyName);

			return assemblyNameLine.Substring(indexOfAssemblyName, indexOfEndAssembly - indexOfAssemblyName);
		}

		internal List<string> References
		{
			get
			{
				if (references == null)
				{
					references = new List<string>();
					string[] fileContents = File.ReadAllLines(projectFilePath);
					foreach (string line in fileContents)
					{
						int indexOfReferencePrefix = line.IndexOf(assemblyReferencePrefixInProjectFile, StringComparison.OrdinalIgnoreCase);
						if (indexOfReferencePrefix >= 0)
						{
							int indexOfReference = indexOfReferencePrefix + assemblyReferencePrefixInProjectFile.Length;
							string lineStartingAtReference = line.Substring(indexOfReference);
							string reference = lineStartingAtReference.Split(',', '\"')[0];

							string referenceName;
							if (Path.GetExtension(reference) == ".csproj")
							{
								referenceName = Path.GetFileNameWithoutExtension(reference);
							}
							else
							{
								referenceName = Path.GetFileName(reference);
							}
							references.Add(referenceName);
						}
					}
				}
				return references;
			}
		}
		List<string> references;
	}
}
