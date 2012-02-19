using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;

namespace Build
{
	public class Project
	{
		readonly string projectFilePath;
		const string assemblyNameIdentifierInProjectFile = "<AssemblyName>";
		const string assemblyReferencePrefixInProjectFile = "Reference Include=\"";
		readonly static string[] systemAssemblyPrefixes = new string[] { "System", "Microsoft", "Windows", "Presentation", "nunit" };

		public Project(string projectFilePath)
		{
			this.projectFilePath = projectFilePath;
		}

		public string BuildAndReturnStdOut(string msBuildPath)
		{
			Console.WriteLine("Building project {0}", AssemblyName);

			Process process = new Process();
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
			get
			{
				if (fAssemblyName == null)
				{
					string[] projectFileContents = File.ReadAllLines(projectFilePath);
					string assemblyNameLine = projectFileContents.First(line => line.Contains(assemblyNameIdentifierInProjectFile));
					int indexOfAssemblyName = assemblyNameLine.IndexOf(assemblyNameIdentifierInProjectFile) + assemblyNameIdentifierInProjectFile.Length;
					int indexOfEndAssembly = assemblyNameLine.IndexOf('<', indexOfAssemblyName);

					fAssemblyName = assemblyNameLine.Substring(indexOfAssemblyName, indexOfEndAssembly - indexOfAssemblyName);
				}
				return fAssemblyName;
			}
		}
		string fAssemblyName;

		internal List<string> NonSystemReferences
		{
			get
			{
				if (nonSystemReferences == null)
				{
					List<string> references = new List<string>();
					string[] fileContents = File.ReadAllLines(projectFilePath);
					foreach (string line in fileContents)
					{
						int indexOfReferencePrefix = line.IndexOf(assemblyReferencePrefixInProjectFile, StringComparison.OrdinalIgnoreCase);
						if (indexOfReferencePrefix >= 0)
						{
							int indexOfReference = indexOfReferencePrefix + assemblyReferencePrefixInProjectFile.Length;
							string lineStartingAtReference = line.Substring(indexOfReference);
							string reference = lineStartingAtReference.Split(',', '\"')[0];
							if (!IsSystemReference(reference))
							{
								references.Add(reference);
							}
						}
					}
					nonSystemReferences = references;
				}
				return nonSystemReferences;
			}
		}
		List<string> nonSystemReferences;

		bool IsSystemReference(string reference)
		{
			return systemAssemblyPrefixes.Any(systemRef => reference.StartsWith(systemRef, StringComparison.OrdinalIgnoreCase));
		}
	}
}
